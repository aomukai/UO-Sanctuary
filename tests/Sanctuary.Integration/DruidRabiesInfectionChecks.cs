// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class DruidRabiesInfectionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidRabiesInfectionChecks));
    private sealed record Saved(uint Owner,uint Host,uint Other,RabiesInfectionSave State,TimedEffectState[] Poison,DateTime Clock,int Reloads);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-rabies-infection-fixtures.json");
    private static int _scene;
    public static void Configure()
    {Register("druid-rabies-infection-check",Check);Register("druid-rabies-infection-reload-check",Reload);Register("druid-rabies-infection-snapshot-check",Snapshot);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Rabies independent infection/emitter and saved poison spread",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static PlayerMobile Fresh()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="RabiesInfectionFixture"};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);
        FixtureSpace.Corridor(owner,6040,12,3000+32*_scene++);return owner;
    }
    private static BladeMissileTarget Target(PlayerMobile owner,int offset)
    {
        var target=new BladeMissileTarget{Name="RabiesInfectionFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};
        target.MoveToWorld(new(owner.X+offset,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){target.SetResistance(type,0);}return target;
    }
    private static TimedEffectState[] Poison(uint a,uint b)=>TimedEffects.Snapshot().Where(x=>x.Owner==a||x.Owner==b).OrderBy(x=>x.Owner).ToArray();
    private static void Cases()
    {
        var owner=Fresh();var host=Target(owner,1);var other=Target(owner,8);var third=Target(owner,10);
        var original=DruidRabiesInfections.Snapshot();var low=DruidRabiesInfections.SeedLow;
        try
        {
            DruidRabiesInfections.SeedLow=()=>17;var at=TimedEffects.Now.Add(FixtureSpace.Retention);
            int mana=NecromancerEffects.ManaBalance256(owner);
            var first=DruidRabiesInfections.Mark(owner,host,1,100,at);
            Require(first.UntilUtc==at.AddSeconds(4)&&first.Rank==1&&first.Age==0,"Initial infection lifetime/rank");
            Require(DruidRabiesInfections.Mark(owner,host,20,290,at.AddMilliseconds(200))==first,"Repeated bite refreshed deadline, rank or owner");
            Require(NecromancerEffects.ManaBalance256(owner)==mana,"Infection marking spent cast mana");
            DruidRabiesInfections.Tick(at.AddMilliseconds(40),true,owner.Serial.Value);
            var child=DruidRabiesInfections.Children(owner.Serial.Value).Single();
            Require(child.Age==0&&child.X==host.X&&child.Y==host.Y&&child.InfectionUntilUtc==first.UntilUtc&&child.Damage.Elements.PoisonMin==48,"First elapsed-age0emission/host origin/retained deadline");
            var seed=DruidRabiesInfections.Snapshot().Seeds.Single(x=>x.Caster==owner.Serial.Value).Seed;
            DruidRabiesInfections.Tick(at.AddMilliseconds(160),true,owner.Serial.Value);
            Require(DruidRabiesInfections.Children(owner.Serial.Value).Length==1&&DruidRabiesInfections.Snapshot().Seeds.Single(x=>x.Caster==owner.Serial.Value).Seed==seed,"Emitted before elapsed-age4");
            host.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
            DruidRabiesInfections.Tick(at.AddMilliseconds(200),true,owner.Serial.Value);
            var moved=DruidRabiesInfections.Children(owner.Serial.Value).Last();
            Require(moved.Age==0&&moved.X==host.X&&moved.Y==host.Y&&moved.InfectionUntilUtc==first.UntilUtc,"Emitter failed to follow live host or reset original expiry");
            other.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);third.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
            Require(DruidRabiesInfections.Contact(moved,owner,other,at.AddMilliseconds(200),out var contact),"Valid child contact removed projectile");
            var infected=DruidRabiesInfections.Infection(other);
            Require(infected!=null&&infected.UntilUtc==first.UntilUtc&&infected.TotalFrames==95&&contact.LastCollision==other.Serial.Value,"Secondary infection did not inherit remaining lifetime");
            Require(Poison(host.Serial.Value,other.Serial.Value).Any(x=>x.Owner==other.Serial.Value&&x.PoisonMs>0),"Contact did not deliver actual poison");
            var before=Json(Poison(host.Serial.Value,other.Serial.Value));
            Require(DruidRabiesInfections.Contact(contact,owner,other,at.AddMilliseconds(200),out var repeated)&&repeated==contact
                &&before==Json(Poison(host.Serial.Value,other.Serial.Value)),"Consecutive same-recipient contact replayed poison/RNG");
            Require(DruidRabiesInfections.Contact(contact,owner,third,at.AddMilliseconds(200),out var alternate)
                &&DruidRabiesInfections.Contact(alternate,owner,other,at.AddMilliseconds(200),out var again)
                &&again.LastCollision==other.Serial.Value&&again.Seed!=contact.Seed&&DruidRabiesInfections.Infection(other)==infected,"A-B-A contacts used a permanent visited set or refreshed marker");
            TimedEffects.CleansePoison(other,0,at.AddMilliseconds(200));
            Require(DruidRabiesInfections.Infection(other)==infected&&!Poison(host.Serial.Value,other.Serial.Value).Any(x=>x.Owner==other.Serial.Value&&x.PoisonMs>0),"Poison cure removed infection or failed to cure poison");
            var saved=DruidRabiesInfections.Snapshot();string frozen=Json(saved);
            try{DruidRabiesInfections.Restore(saved with{Infections=[..saved.Infections,saved.Infections[0]]});throw new InvalidOperationException("Duplicate marker accepted");}
            catch(InvalidDataException){}
            Require(Json(DruidRabiesInfections.Snapshot())==frozen,"Failed restore partially mutated live infections");
            saved.Contagions[0].Steps.Cells[0]=new(1,1);
            Require(Json(DruidRabiesInfections.Snapshot())==frozen,"Snapshot exposed mutable path cells");
            // Existing children are independent after their spawning host dies.
            host.Kill();var children=DruidRabiesInfections.Children(owner.Serial.Value).Select(x=>x.Id).ToArray();
            DruidRabiesInfections.Tick(at.AddMilliseconds(240),true,owner.Serial.Value);
            Require(DruidRabiesInfections.Infection(host) is{Emitting:false}&&children.All(id=>DruidRabiesInfections.Children(owner.Serial.Value).Any(x=>x.Id==id)),"Host death erased emitted children or kept emitting");
            Require(DruidRabiesInfections.Contact(moved,owner,other,first.UntilUtc.AddMilliseconds(-400),out _),"Inclusive10frame contact was rejected");
            Require(!DruidRabiesInfections.Contact(moved,owner,third,first.UntilUtc.AddMilliseconds(-360),out _),"Invalid9frame contact retained child");
            DruidRabiesInfections.Tick(at.AddMinutes(1),false,owner.Serial.Value);
            Require(DruidRabiesInfections.Infection(host)==null&&DruidRabiesInfections.Infection(other)==null&&DruidRabiesInfections.Infection(third)==null
                &&DruidRabiesInfections.Children(owner.Serial.Value).Length==0,"Absolute expiry kept inactive markers/children alive");
        }
        finally
        {DruidRabiesInfections.Restore(original);DruidRabiesInfections.SeedLow=low;host.Corpse?.Delete();host.Delete();other.Delete();third.Delete();owner.Delete();}
    }
    private static void Check()
    {
        Cases();var owner=Fresh();var host=Target(owner,1);var other=Target(owner,2);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        DruidRabiesInfections.Mark(owner,host,3,120,at);DruidRabiesInfections.Tick(at.AddMilliseconds(40),true,owner.Serial.Value);
        var child=DruidRabiesInfections.Children(owner.Serial.Value).Single();
        Require(DruidRabiesInfections.Contact(child,owner,other,at.AddMilliseconds(40),out _),"Saved secondary infection");
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,host.Serial.Value,other.Serial.Value,DruidRabiesInfections.Snapshot(),Poison(host.Serial.Value,other.Serial.Value),at.AddMilliseconds(80),0)));
        Log.Information("Rabies infection cases: elapsed-age cadence, following host, independent poison cure, no refresh, deadline inheritance, A-B-A contacts, host death, inactive expiry and atomic restore passed; learned casting remains disabled");
    }
    private static void Snapshot()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(Json(DruidRabiesInfections.Snapshot())==Json(s.State)&&Json(Poison(s.Host,s.Other))==Json(s.Poison),"Prepared infection changed before world save");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(World.FindMobile((Serial)s.Owner) is PlayerMobile{Alive:true}&&World.FindMobile((Serial)s.Host) is BaseCreature{Alive:true}
            &&World.FindMobile((Serial)s.Other) is BaseCreature{Alive:true},"Rabies saved owner/hosts missing");
        Require(Json(DruidRabiesInfections.Snapshot())==Json(s.State)&&Json(Poison(s.Host,s.Other))==Json(s.Poison),"Saved infection/poison/seed/path mismatch");
        var seeds=Json(s.State.Seeds);int children=s.State.Contagions.Length;
        DruidRabiesInfections.Tick(s.Clock,true,s.Owner);
        var state=DruidRabiesInfections.Snapshot();
        Require(Json(state.Seeds)==seeds&&state.Contagions.Length==children&&Json(Poison(s.Host,s.Other))==Json(s.Poison),"Load replayed overdue emissions or poison impacts");
        Require(state.Infections.Where(x=>x.Caster==s.Owner).All(x=>x.UntilUtc==s.State.Infections.Single(y=>y.Id==x.Id).UntilUtc),"Load extended infection deadline");
        File.WriteAllText(Manifest,Json(s with{State=state,Clock=s.Clock.AddMilliseconds(40),Reloads=s.Reloads+1}));
    }
}
