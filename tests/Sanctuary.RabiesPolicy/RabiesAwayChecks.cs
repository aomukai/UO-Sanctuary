// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

// Separate policy acceptance leaves the concurrently tested class-suite
// assembly untouched. Both suites load the identical gameplay assembly/data.
public static class RabiesAwayChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RabiesAwayChecks));
    private sealed record Saved(uint Owner,uint Host,Point3D Origin,DateTime Started,DateTime Until,DateTime Clock,
        int Life256,int Mana256,RabiesInfectionSave State,TimedEffectState[] Poison,int Reloads);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"rabies-away-fixtures.json");
    public static void Configure()
    {
        Register("rabies-away-start",()=>FixtureNetwork.Connected("rabies-away-start-result",Start));
        Register("rabies-away-return",()=>FixtureNetwork.Connected("rabies-away-return-result",Return));
        Register("rabies-away-expire",Expire);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved Rabies away policy",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static Saved Read()=>JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
    private static TimedEffectState[] Poison(uint host)=>TimedEffects.Snapshot().Where(x=>x.Owner==host).ToArray();
    private static void Attach(NetState network,PlayerMobile p)
    {
        var account=p.Account as Account??new Account("rabies-away-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;
        network.Seeded=true;network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(protocol!=null,"Protocol bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
    }
    private static void Tick(PlayerMobile p,DateTime at)=>DruidRabiesInfections.Tick(at,casterScope:p.Serial.Value);
    private static void Disconnected(PlayerMobile p,string result,Action finish)
    {
        // Exercise the real graceful-disconnect API with a draining peer. The
        // transport's bounded drain may take20seconds; this is not a skill timer.
        long deadline=Core.TickCount+30000;p.NetState?.Disconnect("Rabies away-policy fixture");
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Real socket disconnect timeout");
                if(p.NetState is{Running:true}){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                finish();Log.Information("{Result}: passed",result);
            }
            catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        }
        Log.Information("rabies-away: disconnect-ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
    private static void Start(NetState network)
    {
        var p=new PlayerMobile{Name="RabiesAwayOwner",Body=0x190,Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Corridor(p,6200,12,1200);
        var host=new BladeMissileTarget{Name="RabiesAwayHost",NoKillAwards=true,Frozen=true,CantWalk=true};host.SetHits(50000);host.Hits=host.HitsMax;
        host.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);host.AIObject.AITimer.Stop();host.SetResistance(ResistanceType.Poison,0);Attach(network,p);
        NecromancerEffects.SetMana256(p,p.ManaMax*256);
        var at=TimedEffects.Now.Add(FixtureSpace.Retention);var origin=p.Location;int life=SkillHealth.Balance256(host),mana=NecromancerEffects.ManaBalance256(p);
        var marker=DruidRabiesInfections.Mark(p,host,1,100,at);Require(PoisonJavelins.Poison(p,host,48,100,at),"Initial real poison");
        Tick(p,at.AddMilliseconds(40));var before=DruidRabiesInfections.Snapshot();Require(before.Contagions.Length==1,"Connected baseline did not emit");
        var poison=Json(Poison(host.Serial.Value));var seed=Json(before.Seeds);var child=before.Contagions.Single();
        p.MoveToWorld(origin,Map.Felucca);Require(p.NetState is{Running:true},"Facet test lost real connection");Tick(p,at.AddMilliseconds(440));
        var away=DruidRabiesInfections.Snapshot();var aged=away.Contagions.Single();
        Require(Json(away.Seeds)==seed&&away.Markers.Single()==marker&&aged.Id==child.Id&&aged.Age==10&&aged.X==child.X&&aged.Y==child.Y
            &&away.Infections.Single().Age==11&&Json(Poison(host.Serial.Value))==poison,"Other facet emitted,contacted,extended or froze clocks");
        p.MoveToWorld(origin,Map.Trammel);Tick(p,at.AddMilliseconds(480));Require(DruidRabiesInfections.Children(p.Serial.Value).Length==1,"Facet return replayed missed emission");
        Tick(p,at.AddMilliseconds(520));Require(DruidRabiesInfections.Children(p.Serial.Value).Length==2,"Next eligible facet-return emission missing");
        Disconnected(p,"rabies-away-start-result",()=>
        {
            var savedSeed=Json(DruidRabiesInfections.Snapshot().Seeds);var savedPoison=Json(Poison(host.Serial.Value));
            Tick(p,at.AddMilliseconds(1080));Tick(p,at.AddSeconds(3));var state=DruidRabiesInfections.Snapshot();
            Require(state.Contagions.Length==0&&state.Markers.Single()==marker&&state.Infections.Single().Age==75
                &&Json(state.Seeds)==savedSeed&&Json(Poison(host.Serial.Value))==savedPoison,"Offline spreading,clock pause,poison reset or missed-action replay");
            File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,host.Serial.Value,origin,at,marker.UntilUtc,at.AddSeconds(3),life,mana,state,Poison(host.Serial.Value),0)));
        });
    }
    private static void Return(NetState network)
    {
        var s=Read();var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var host=(BaseCreature)World.FindMobile((Serial)s.Host);
        Require(Json(DruidRabiesInfections.Snapshot())==Json(s.State)&&Json(Poison(s.Host))==Json(s.Poison),"Actual reload changed away deadlines,age,seed or poison");
        p.MoveToWorld(s.Origin,Map.Trammel);Attach(network,p);Tick(p,s.Clock.AddMilliseconds(40));
        Require(DruidRabiesInfections.Children(s.Owner).Length==0&&DruidRabiesInfections.Infection(host).UntilUtc==s.Until,"Loaded owner return replayed overdue emission or refreshed deadline");
        Tick(p,s.Clock.AddMilliseconds(80));Require(DruidRabiesInfections.Children(s.Owner).Length==1&&DruidRabiesInfections.Emitters(s.Owner).Single().Age==77,"Connected return failed to resume only the future eligible event");
        Disconnected(p,"rabies-away-return-result",()=>
        {
            File.WriteAllText(Manifest,Json(s with{State=DruidRabiesInfections.Snapshot(),Poison=Poison(s.Host),Clock=s.Clock.AddMilliseconds(80),Reloads=1}));
        });
    }
    private static void Expire()
    {
        var s=Read();var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var host=(BaseCreature)World.FindMobile((Serial)s.Host);
        Require(s.Reloads==1&&Json(DruidRabiesInfections.Snapshot())==Json(s.State)&&Json(Poison(s.Host))==Json(s.Poison),"Second reload changed retained state");
        Require(p.NetState is not{Running:true},"Final expiry owner unexpectedly online");Tick(p,s.Started.AddSeconds(5));var state=DruidRabiesInfections.Snapshot();
        Require(state.Markers.Length==0&&state.Infections.Length==0&&state.Contagions.Length==0,"Away deadlines extended or expired activity retained");
        TimedEffects.Settle(host,s.Started.AddSeconds(5));Require(SkillHealth.Balance256(host)<s.Life256&&!Poison(s.Host).Any(x=>x.PoisonMs>0),"Existing poison failed to damage/expire independently while caster offline");
        Require(NecromancerEffects.ManaBalance256(p)==s.Mana256,"Owner transitions spent or reset mana");
        File.WriteAllText(Manifest,Json(s with{Life256=SkillHealth.Balance256(host),State=state,Poison=Poison(s.Host),Clock=s.Started.AddSeconds(5),Reloads=2}));
        Log.Information("Rabies approved away policy: real facet departure/return,two actual disconnects/reloads,clock-only absence,future-only resumption,original expiry and independent poison pass");
    }
}
