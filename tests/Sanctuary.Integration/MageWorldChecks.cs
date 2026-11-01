// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class MageWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageWorldChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,uint Historical,int Life,int TargetLife,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-world-fixtures.json");
    public static void Configure()
    {
        Register("mage-world-check",Check);Register("mage-world-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("mage-world-live","Learned automatic ranged Mage cast and collision",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("mage-world-live-result",Live)));
        foreach(bool quick in new[]{false,true}){bool q=quick;Register(q?"mage-world-quick-check":"mage-world-right-check",()=>FixtureNetwork.Connected(q?"mage-world-quick-result":"mage-world-right-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Mage saved ranged AI67/motion/kind2 missiles and owner stream",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureMageWorld";FixtureSpace.Corridor(p,6900,40,3500);
        void Train(string id,int rank){TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"World train "+id);}}
        Train("necromancer.skeletalmage",4);Train("necromancer.skeletonmastery",20);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.RaiseSkeletalMage)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.RaiseSkeletalMage),"Mage right/quick bindings");return p;
    }
    private static Corpse Corpse(PlayerMobile p,int dx=1)
    {var dead=new GraveyardZombie{NoKillAwards=true};dead.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);dead.Damage(100000,p);if(dead.Alive){dead.Kill();}var corpse=dead.Corpse as Corpse;Require(SkeletonPets.Usable(p,corpse),"Actual world Mage corpse");return corpse;}
    private static MageWorldBody Learned(PlayerMobile p,int dx=3,bool future=true)
    {
        var corpse=Corpse(p,dx);var roll=NecromancerSummons.ElementRoll;try{NecromancerSummons.ElementRoll=()=>2;NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.RaiseSkeletalMage).Mana256;
            Require(NecromancerCombat.Release(p,CombatAbility.RaiseSkeletalMage,corpse.Serial,corpse.Location)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&(corpse.Deleted||corpse.SanctuaryConsumed),"Learned world Mage corpse/payment");}finally{NecromancerSummons.ElementRoll=roll;corpse.Delete();}
        var b=NecromancerSkeletons.For(p,CombatAbility.RaiseSkeletalMage).OfType<MageWorldBody>().Last();
        Require(b.WorldState.Owner==p.Serial.Value&&b.WorldState.Controller is{Mode:8,EndAt:24,NextThink:25,Ai.Parameter0:1}&&NpcEquipmentRandom.State(b)==null&&b is not ISourceUnarmedNpc,"Mage birth/controller role or fake physical random binding");
        if(future){b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}return b;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,MageWorldBody b,int dx=4)
    {var t=PoisonDaggerChecks.Target(p);t.SetHits(20000);t.MoveToWorld(new(b.X+dx,b.Y,b.Z),p.Map);t.SetResistance(ResistanceType.Fire,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static MageMissileState Bolt(MageWorldBody b)=>NecromancerMageMissiles.Snapshot().Bolts.FirstOrDefault(x=>x.Actor==b.Serial.Value);
    private static void Clear(MageWorldBody b)
    {var saved=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(saved with{Bolts=saved.Bolts.Where(x=>x.Actor!=b.Serial.Value).ToArray(),Seeds=saved.Seeds.Where(x=>x.Actor!=b.Serial.Value).ToArray()});}
    internal static void Through(MageWorldBody b,int frame,bool actions=true)
    {var s=b.WorldState.Controller;Require(frame>s.Frame,"Reverse Mage world clock");IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);Require(!b.Deleted&&b.WorldState.Controller.Fault==null,"Mage world fault/deletion");}
    private static ulong Seed()
    {for(ulong n=1;n<10000;n++){ulong s=n;if(SourceRandom.Roll(ref s)%100>=15&&SourceRandom.Roll(ref s)%100<80){return n;}}throw new InvalidOperationException("No Mage ranged skill seed");}
    private static void Combat()
    {
        var p=Owner();var b=Learned(p);var t=Target(p,b);var low=NecromancerMageMissiles.SeedLow;NecromancerMageMissiles.SeedLow=()=>1;
        try
        {
            NecromancerMageMissiles.SetActorSeed(b,Seed());int hp=SkillHealth.Balance256(t);Through(b,24);Require(b.WorldState.Controller is{Mode:1,NextThink:25}&&Bolt(b)==null,"Mage emerged into early cast");
            Through(b,25);Require(b.WorldState.Controller is{Mode:4,ActionAt:35,EndAt:45,AttackSpeed:208}&&b.WorldState.Controller.Target==t.Serial.Value&&Bolt(b)==null&&SkillHealth.Balance256(t)==hp,"Mage ranged AI/A1kind2 entry/action35/end45");
            ulong seed=NecromancerMageMissiles.ActorSeed(b).Seed;Through(b,34);Require(Bolt(b)==null&&SkillHealth.Balance256(t)==hp,"Mage projectile before skill event");
            Through(b,35);var bolt=Bolt(b);Require(bolt!=null&&bolt.Packet.Element=="fire"&&bolt.Packet.Rank==b.Rank&&bolt.Packet.Spell==CombatAbility.FireBolt&&SkillHealth.Balance256(t)==hp&&NecromancerMageMissiles.ActorSeed(b).Seed==seed&&b.WorldState.Controller is{Attempts:1,Hits:1},"Mage launch was direct damage or lost rank/stream");
            for(int i=0;i<12&&Bolt(b) is{} flight;i++){NecromancerMageMissiles.Tick(flight.NextUtc,true,flight.Id);}
            Require(SkillHealth.Balance256(t)<hp&&Bolt(b)==null,"World Mage never hit through traced missile collision");
            ulong expected=seed;SourceRandom.Roll(ref expected);Require(NecromancerMageMissiles.ActorSeed(b).Seed==expected,"AI and intrinsic critical did not share the same existing actor stream");
            hp=SkillHealth.Balance256(t);Through(b,45);Require(b.WorldState.Controller.Mode==1&&Bolt(b)==null,"Mage end replayed launch");
            b.Ground.Store(IronGolemWorld.Park(b.WorldState.Controller));NecromancerMageMissiles.SetActorSeed(b,Seed());Through(b,46);Require(b.WorldState.Controller.Mode==4,"Second Mage windup missing");seed=NecromancerMageMissiles.ActorSeed(b).Seed;b.InterruptForRecovery();
            Require(b.WorldState.Controller is{Mode:1,ActionAt:0},"Mage recovery retained skill event");Through(b,100,false);Require(b.Map==Map.Internal&&Bolt(b)==null&&SkillHealth.Balance256(t)==hp&&NecromancerMageMissiles.ActorSeed(b).Seed==seed,"Offline canceled Mage fired or replayed RNG");
        }
        finally{NecromancerMageMissiles.SeedLow=low;Clear(b);t.Delete();b.Delete();p.Delete();}
    }
    private static void Movement()
    {
        var p=Owner();var b=Learned(p);b.MoveToWorld(new(p.X+16,p.Y,p.Z),p.Map);
        try
        {
            var point=b.Location;Through(b,150);Require(b.Location!=point&&p.InRange(b,20),"Ranged Mage owner-follow did not move");
            b.Ground.Store(IronGolemWorld.Park(b.WorldState.Controller) with{NextThink=b.WorldState.Controller.Frame+1000});SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/3+17);int hp=SkillHealth.Balance256(b),rate=BattleOrders.Regeneration256(b),frame=b.WorldState.Controller.Frame;
            Through(b,frame+10);Require(SkillHealth.Balance256(b)==hp+rate*10,"Mage source regeneration omitted or doubled");
            var old=b.WorldState.Controller;bool bad=false;try{b.Ground.Store(old with{Ai=old.Ai with{Parameter0=0}});}catch(InvalidDataException){bad=true;}Require(bad&&b.WorldState.Controller==old,"Mage ranged role validation silently normalized state");
        }
        finally{Clear(b);b.Delete();p.Delete();}
    }
    private static void Check()
    {
        Combat();Movement();var p=Owner();var b=Learned(p);var t=Target(p,b);NecromancerMageMissiles.SetActorSeed(b,Seed());Through(b,25);Require(b.WorldState.Controller.Mode==4,"Saved Mage windup missing");
        // Snapshot-only hold; Live checks natural unheld launch/collision.
        b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMinutes(3)});SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
        var old=new NecromancerMinion{Frozen=true,CantWalk=true};old.Prepare(p,CombatAbility.RaiseSkeletalMage);old.SetControlMaster(p);old.MoveToWorld(p.Location,p.Map);old.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(old);old.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,t.Serial.Value,old.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Balance256(t),NecromancerMageMissiles.ActorSeed(b).Seed,0)));
        Log.Information("Mage world:ranged AI1,S1end24/think25,A1kind2action35/end45,actual delayed projectile,shared AI/critical stream,no fake physical binding,interruption/offline cancellation,owner-follow/regeneration and strict ranged state pass;world/seed/search adapters remain explicit");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(MageWorldBody)World.FindMobile((Serial)s.Body);var t=(GraveyardSkeleton)World.FindMobile((Serial)s.Target);var old=(NecromancerMinion)World.FindMobile((Serial)s.Historical);
        Require(b.ControlMaster==p&&b.Map==Map.Internal&&b.WorldState.Controller is{Mode:1,ActionAt:0,Ai.Parameter0:1}&&Bolt(b)==null&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife&&NecromancerMageMissiles.ActorSeed(b).Seed==s.Seed,"Loaded ranged Mage changed wounds/stream or replayed launch");
        Require(old.GetType()==typeof(NecromancerMinion)&&old.IsMage&&old.ControlMaster==p&&old.Map==Map.Internal,"Historical Mage layout/type changed");Through(b,b.WorldState.Controller.Frame+1,false);Require(NecromancerMageMissiles.ActorSeed(b).Seed==s.Seed&&Bolt(b)==null,"Loaded Mage skip rolled/launched");File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"mage-world");var b=Learned(p,future:false);var t=Target(p,b,5);NecromancerMageMissiles.SetActorSeed(b,Seed());int hp=SkillHealth.Balance256(t);bool launched=false;long id=0,deadline=Core.TickCount+12000;
        void End(){Clear(b);t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural Mage world timeout/fault");
                if(!launched&&Bolt(b) is{} bolt){Require(b.WorldState.Controller.Frame>=35&&SkillHealth.Balance256(t)==hp&&bolt.Fault==null,"Natural Mage launch too early or immediate damage");launched=true;id=bolt.Id;b.SuspendActions();}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(launched&&NecromancerMageMissiles.Snapshot().Bolts.All(x=>x.Id!=id)&&network.Running,"Natural Mage hit lacked traced missile collision");End();Log.Information("mage-world-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"mage-world-live-result: failed");}
        }
        Log.Information("mage-world-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var corpse=Corpse(p);FixtureNetwork.Authenticate(network,p,"mage-world-input");PoisonDaggerChecks.Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.RaiseSkeletalMage).Mana256;string label=quick?"mage-world-quick":"mage-world-right";bool began=false;long deadline=Core.TickCount+6000;
        void End(){foreach(var b in NecromancerSkeletons.For(p,CombatAbility.RaiseSkeletalMage).OfType<MageWorldBody>()){Clear(b);b.Delete();}corpse.Delete();p.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Mage input timeout");var session=ClassCombat.For(p);if(session.PendingAbility==CombatAbility.RaiseSkeletalMage&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&!corpse.SanctuaryConsumed,"Mage paid/consumed during windup");began=true;Log.Information("{Label}: windup",label);}
                var b=NecromancerSkeletons.For(p,CombatAbility.RaiseSkeletalMage).OfType<MageWorldBody>().SingleOrDefault();if(b==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&b.WorldState.Controller.Ai.Parameter0==1&&NecromancerEffects.ManaBalance256(p)==mana-cost&&(corpse.Deleted||corpse.SanctuaryConsumed),"Mage real input role/corpse/payment");End();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){End();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.RaiseSkeletalMage,(int)DiabloClass.Necromancer,corpse.Serial.Value,corpse.X,corpse.Y,corpse.Z);Inspect();});
    }
}
