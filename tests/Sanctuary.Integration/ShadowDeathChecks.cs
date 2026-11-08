// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowDeathChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowDeathChecks));
    private sealed record Held(uint Actor,string Body,uint[] Items,NpcEquipmentSeed Random);
    private sealed record Saved(uint Owner,Held[] Pending,Held[] Emitted,SorceressSpellFlight[] Flights,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-death-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Reject(Action f){try{f();}catch(NotSupportedException){return;}throw new InvalidOperationException("Unqualified dead spell source accepted");}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true}){string name=reload?"shadow-death-reload-check":"shadow-death-check";ConsoleInputHandler.RegisterCommand(name,"Retained actual Shadow death nova and persistent children",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        ConsoleInputHandler.RegisterCommand("shadow-death-live","Connected native lethal-hit/four-frame original Shadow death nova",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("shadow-death-live-result",Live)));
    }
    private static PlayerMobile Owner()
    {var p=BladeBodyChecks.Fresh(6500,30,3570);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);return p;}
    private static AssassinShadowBody Body(PlayerMobile p,CombatAbility ability)
    {
        var world=NpcWorldAllocation.Capture();AssassinShadowBody b;
        try{NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));b=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);}
        finally{NpcWorldAllocation.Restore(world);}
        GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);return b;
    }
    private static uint[] Items(AssassinShadowBody b)=>GeneratedNpcEquipment.Items(b).Select(i=>i.Serial.Value).ToArray();
    private static Held Hold(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Items(b),NpcEquipmentRandom.State(b));
    private static void Defer(AssassinShadowBody b)
    {var d=b.DeathState;var shift=FixtureSpace.Retention;b.RestorePrepared(b.Prepared with{Death=d with{StartedUtc=d.StartedUtc+shift,DueUtc=d.DueUtc+shift,ExpiryUtc=d.ExpiryUtc+shift}});}
    private static ShadowTailChecks.Witness Target(PlayerMobile p,AssassinShadowBody b,int offset=2)
    {var t=new ShadowTailChecks.Witness();t.SetHits(65000);t.MoveToWorld(new(b.X+offset,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static SorceressSpellFlight[] Flights(AssassinShadowBody b)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();
    private static void Kill(AssassinShadowBody b,PlayerMobile p)
    {
        var items=Items(b);var rng=NpcEquipmentRandom.State(b);long next=SorceressSpellMissiles.Snapshot().NextId;b.Kill();
        Require(b.Retiring&&!b.Alive&&!b.Deleted&&World.FindMobile(b.Serial)==b&&b.ControlMaster==p&&b.Frozen&&b.DeathState is{Emitted:false,Fault:null},"Real death lost retained original/ownership or emitted immediately");
        Require(Items(b).SequenceEqual(items)&&NpcEquipmentRandom.State(b)==rng&&SorceressSpellMissiles.Snapshot().NextId==next,"Death lost original items/RNG or allocated before four frames");
        Require(b.DeathState.DueUtc-b.DeathState.StartedUtc==TimeSpan.FromMilliseconds(160)&&b.DeathState.Source.Packet.Rank==11,"Death rank/deadline ignored summon-birth Warrior snapshot");Defer(b);
    }
    private static void Emit(AssassinShadowBody b,PlayerMobile p)
    {
        var d=b.DeathState;var before=NpcWorldAllocation.Capture();var next=SorceressSpellMissiles.Snapshot().NextId;b.AdvanceDeath(d.DueUtc.AddTicks(-1),true);Require(Flights(b).Length==0&&NpcWorldAllocation.Capture()==before,"Nova before four frames");
        b.AdvanceDeath(d.DueUtc,true);var flights=Flights(b);
        Require(flights.Length==64&&flights.Select(f=>f.RadialIndex).SequenceEqual(Enumerable.Range(0,64))&&flights.All(f=>f.Npc==d.Source&&f.Owner==p.Serial.Value&&f.X==b.X&&f.Y==b.Y&&f.Rank==11)&&b.DeathState is{Emitted:true,Fault:null},"Death did not emit exact own saved ring: count="+flights.Length+",attached="+ShadowDeathNovas.Attached(b,p,d.Map,d.Source)+",map="+b.Map+",ownerMap="+p.Map+",location="+b.Location+",owner="+b.ControlMaster?.Serial+",state="+Json(b.DeathState));
        Require(SorceressSpellMissiles.Snapshot().NextId==next+64&&NpcWorldAllocation.Capture()!=before,"Death did not allocate private children");var after=NpcWorldAllocation.Capture();b.AdvanceDeath(d.DueUtc,true);Require(Flights(b).Length==64&&NpcWorldAllocation.Capture()==after,"Duplicate death callback replayed");
        var target=Target(p,b);
        try
        {
            int hp=SkillHealth.Balance256(target);Reject(()=>NpcElementSpellHit.Deliver(b,p,target,"ltng",256,0,d.DueUtc,"default dead-source guard"));Require(SkillHealth.Balance256(target)==hp,"Default dead-source refusal changed HP");Reject(()=>NpcItemSpellFlights.ImpactCaptured(p,target,d.Source with{DeathNova=false},d.Map,d.Source.Packet.Minimum256,d.Source.Packet.Maximum256,flights[0].Seed,d.DueUtc,out _));Require(SkillHealth.Balance256(target)==hp,"Ordinary dead-source refusal changed HP");
            int damage=NpcItemSpellFlights.Impact(p,target,flights[0],out _);Require(damage>0&&SkillHealth.Balance256(target)==hp-damage,"Retained actual dead source failed typed NPC damage");Require(NativeMonsterReturn.Session(target).LastHitClass==64,"Death nova lost source lightning hit class");
            SkillHealth.SetBalance256(target,256);NpcItemSpellFlights.Impact(p,target,flights[1],out _);Require(!target.Alive&&target.LastKiller==b&&ClassLoot.Killer(target)==p,"Dead source lost actual killer/root owner credit");
        }
        finally{target.Corpse?.Delete();target.Delete();}
    }
    private static void Finish(AssassinShadowBody b)
    {
        var items=Items(b);foreach(var f in Flights(b)){SorceressSpellMissiles.Tick(b.DeathState.ExpiryUtc,false,f.Id);}b.AdvanceDeath(b.DeathState.ExpiryUtc,false);Require(b.Deleted&&World.FindMobile(b.Serial)==null&&NpcEquipmentRandom.State(b)==null&&items.All(i=>World.FindItem((Serial)i)==null),"Retirement failed original/item/RNG cleanup");
    }
    private static void Differential(AssassinShadowBody b)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-death-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            count++;if(row.GetProperty("Unique").GetInt32()==0){continue;}
            int rank=row.GetProperty("Owner").GetInt32()==0||row.GetProperty("Skill").GetInt32()==0?0:row.GetProperty("Rank").GetInt32();
            var capture=b.Prepared.Capture with{Skills=Json(b.Prepared.Capture.SkillRanks().Select(s=>s.Id==268?s with{Rank=rank,Hard=0}:s).ToArray())};
            Require(ShadowDeathNovas.Rank(capture,row.GetProperty("Life").GetInt32())==row.GetProperty("Expected").GetProperty("Rank").GetInt32(),"Whole death-source rank branch differs");
            if(count==169){int i=0;foreach(var direction in row.GetProperty("Expected").GetProperty("Directions").EnumerateArray()){Require(FrozenOrbs.Direction(i++)==new Point2D(direction[0].GetInt32(),direction[1].GetInt32()),"Nova64 source geometry differs");}}
        }
        Require(count==336,"Incomplete death-source controls");
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));p=(PlayerMobile)World.FindMobile((Serial)s.Owner);generation=s.Generation+1;p.MoveToWorld(new(6500,3570,0),Map.Trammel);
            foreach(var h in s.Pending.Concat(s.Emitted)){var b=(AssassinShadowBody)World.FindMobile((Serial)h.Actor);Require(Json(Hold(b))==Json(h)&&!b.Alive&&b.Retiring,"Reload changed retained identity/items/RNG/state");}
            Require(Json(SorceressSpellMissiles.Snapshot().Flights.Where(f=>s.Emitted.Any(h=>h.Actor==f.Npc?.Actor)).ToArray())==Json(s.Flights),"Reload changed travelling death nova packets");
            foreach(var h in s.Pending){var b=(AssassinShadowBody)World.FindMobile((Serial)h.Actor);Emit(b,p);Finish(b);}
            foreach(var h in s.Emitted){var b=(AssassinShadowBody)World.FindMobile((Serial)h.Actor);long id=SorceressSpellMissiles.Snapshot().NextId;b.AdvanceDeath(b.DeathState.DueUtc,true);Require(SorceressSpellMissiles.Snapshot().NextId==id,"Reload replayed emitted nova");Finish(b);}
        }
        else{p=Owner();}
        var pending=new Held[2];var emitted=new Held[2];int n=0;
        foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})
        {
            var deleted=Body(p,ability);Differential(deleted);long id=SorceressSpellMissiles.Snapshot().NextId;deleted.Delete();Require(deleted.Deleted&&SorceressSpellMissiles.Snapshot().NextId==id,"Ordinary Delete emitted death nova");
            var offline=Body(p,ability);Kill(offline,p);offline.AdvanceDeath(offline.DeathState.DueUtc,false);Require(offline.DeathState.Emitted&&Flights(offline).Length==0,"Inactive death callback emitted damage");Finish(offline);
            var held=Body(p,ability);Kill(held,p);pending[n]=Hold(held);
            var active=Body(p,ability);Kill(active,p);Emit(active,p);emitted[n++]=Hold(active);
        }
        p.Internalize();Require(pending.Concat(emitted).All(h=>World.FindMobile((Serial)h.Actor) is AssassinShadowBody b&&b.Map.MapID==b.DeathState.Map),"Owner parking moved a retained dead caster off its original facet");var flights=SorceressSpellMissiles.Snapshot().Flights.Where(f=>emitted.Any(h=>h.Actor==f.Npc?.Actor)).ToArray();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,pending,emitted,flights,generation)));
        Log.Information("Both Shadows:real death retention/four-frame rank11 ring,ordinary deletion separate,inactive expiry,64 private children,once-only delivery,actual dead-source HP/killer/root credit,original item/RNG cleanup and pending/emitted saves pass.");
    }
    private static void Live(Server.Network.NetState net)
    {
        var p=Owner();var account=FixtureNetwork.Authenticate(net,p,"shadow-death-live-");AssassinShadowBody b=null;ShadowTailChecks.Witness t=null;bool early=false;
        try
        {
            b=Body(p,CombatAbility.ShadowWarrior);t=Target(p,b);int hp=SkillHealth.Balance256(t);var items=Items(b);b.Damage(b.HitsMax+100,t);Require(b.Retiring&&!b.Deleted&&!b.Alive,"Native lethal hit did not retain dead original");
            Timer.StartTimer(TimeSpan.FromMilliseconds(90),()=>{try{Require(!b.Deleted&&Flights(b).Length==0&&SkillHealth.Balance256(t)==hp,"Actual timer emitted early");early=true;}catch(Exception e){Log.Error(e,"shadow-death-live-result: failed");}});
            Timer.StartTimer(TimeSpan.FromMilliseconds(1050),()=>{try{Require(early&&b.Deleted&&SkillHealth.Balance256(t)<hp&&NpcEquipmentRandom.State(b)==null&&items.All(i=>World.FindItem((Serial)i)==null),"Actual delayed ring failed travel/damage/cleanup");Log.Information("shadow-death-live-result: passed");}catch(Exception e){Log.Error(e,"shadow-death-live-result: failed");}finally{t.Corpse?.Delete();t.Delete();p.Delete();account.Delete();}});
            Log.Information("shadow-death-live: passed");
        }
        catch(Exception e){if(b?.Retiring==true){b.AdvanceDeath(b.DeathState.ExpiryUtc,false);}b?.Delete();t?.Delete();p.Delete();account.Delete();Log.Error(e,"shadow-death-live: failed");}
    }
}
