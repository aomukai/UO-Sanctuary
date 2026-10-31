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

public static class SkeletonWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SkeletonWorldChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,uint Legacy,int Life,int TargetLife,ulong Seed,ulong LegacySeed,Point3D Point,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"skeleton-world-fixtures.json");
    public static void Configure()
    {
        Register("skeleton-world-check",Check);Register("skeleton-world-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("skeleton-world-live","Connected automatic skeleton AI67 combat",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("skeleton-world-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared skeleton shared AI67 frame/motion/contact and persistence",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureSkeletonWorld";FixtureSpace.Corridor(p,7020,40,3500);
        foreach(var pair in new[]{("necromancer.raiseskeleton",4),("necromancer.skeletonmastery",20)})
        {while(ClassCreation.Profiles.GetRank(p.Serial,pair.Item1)<pair.Item2){Require(ClassCreation.Profiles.TryTrain(p.Serial,pair.Item1,out _),"World train "+pair.Item1);}}
        return p;
    }
    private static SkeletonWorldBody Body(PlayerMobile p,int dx=1,DateTime? at=null)=>SkeletonWorldBody.CreatePrepared(p,4,20,new(p.X+dx,p.Y,p.Z),at??TimedEffects.Now.AddHours(1),1);
    private static SkeletonWorldBody Learned(PlayerMobile p)
    {
        var mob=new GraveyardZombie{NoKillAwards=true};mob.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);mob.Damage(100000,p);if(mob.Alive){mob.Kill();}
        var corpse=mob.Corpse as Corpse;Require(SkeletonPets.Usable(p,corpse),"Actual world corpse eligibility");
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
        Require(SkeletonPets.Raise(p,corpse)&&NecromancerEffects.ManaBalance256(p)==mana-9*256&&(corpse.Deleted||corpse.SanctuaryConsumed),"Learned world creation corpse/once-only payment");
        var body=NecromancerSkeletons.For(p,CombatAbility.RaiseSkeleton).OfType<SkeletonWorldBody>().Last();corpse.Delete();
        Require(body.WorldState.Owner==p.Serial.Value&&body.ControlMaster==p&&body.WorldState.Controller is{Mode:8,EndAt:24,NextThink:25},"Learned world ownership/initial schedule");return body;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,SkeletonWorldBody body)
    {
        var t=PoisonDaggerChecks.Target(p);t.SetHits(20000);t.MoveToWorld(body.Location,p.Map);t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static void Through(SkeletonWorldBody b,int frame,bool actions=true)
    {
        var s=b.WorldState.Controller;Require(frame>s.Frame,"Reverse skeleton world clock");
        IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
        Require(!b.Deleted&&b.WorldState.Controller.Fault==null,"Skeleton controller fault/deletion");
    }
    private static ulong Seed(SkeletonWorldBody body,BaseCreature target)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRating(body),ClassStats.Defense(target),ClassStats.Level(body),ClassStats.Level(target));
        for(ulong n=1;n<10000;n++){ulong s=n;SourceRandom.Roll(ref s);if(SourceRandom.Roll(ref s)%100<80&&SourceRandom.Roll(ref s)%100/100.0<chance){return n;}}
        throw new InvalidOperationException("No skeleton world seed");
    }
    private static void Combat()
    {
        var p=Owner();var b=Body(p);var t=Target(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            NpcEquipmentRandom.Set(b,Seed(b,t));int hp=SkillHealth.Balance256(t);
            Require(b.WorldState.Controller is{Mode:8,EndAt:24,NextThink:25},"Pinned skeleton S1/first-think contract");
            Through(b,24);Require(b.WorldState.Controller is{Mode:1,NextThink:25}&&b.WorldContact==null&&SkillHealth.Balance256(t)==hp,"Emergence prematurely attacked or moved first think");
            Through(b,25);var state=b.WorldState.Controller;var packet=b.WorldContact;
            Require(state is{Mode:4,ActionAt:37,EndAt:44,AttackSpeed:224}&&packet!=null&&SkillHealth.Balance256(t)==hp,"AI67 did not capture skeleton A1 at25/action37/end44");
            Require((packet.Impact.Packet.ResultFlags&1)!=0,"Chosen skeleton hit seed failed");int damage=packet.Impact.Packet.Total256;ulong seed=NpcEquipmentRandom.State(b).Seed;
            t.SetResistance(ResistanceType.Physical,100);Through(b,36);Require(SkillHealth.Balance256(t)==hp,"Early skeleton world contact");
            Through(b,37);Require(b.WorldState.Controller is{Attempts:1,Hits:1}&&b.WorldContact==null&&packet.Consumed&&hp-SkillHealth.Balance256(t)==damage
                &&NpcEquipmentRandom.State(b).Seed==seed,"World contact recomputed mitigation,rerolled or lost captured HP");
            int after=SkillHealth.Balance256(t);Through(b,44);Require(b.WorldState.Controller.Mode==1&&SkillHealth.Balance256(t)==after,"Animation end replayed skeleton hit");
            t.SetResistance(ResistanceType.Physical,0);b.StoreWorld(IronGolemWorld.Park(b.WorldState.Controller));NpcEquipmentRandom.Set(b,Seed(b,t));Through(b,45);
            packet=b.WorldContact;Require(packet!=null,"Second skeleton attack failed to prepare");seed=NpcEquipmentRandom.State(b).Seed;hp=SkillHealth.Balance256(t);
            b.InterruptForRecovery();Require(packet.Consumed&&b.WorldContact==null&&b.WorldState.Controller.Mode==1,"Recovery retained skeleton world packet");
            Through(b,80,false);Require(b.Map==Map.Internal&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.State(b).Seed==seed,"Offline skeleton fought or rerolled canceled action");
        }
        finally{SourceBlocking.BlockRoll=roll;t.Delete();b.Delete();p.Delete();}
    }
    private static void Motion()
    {
        var p=Owner();var b=Body(p,16);
        try
        {
            var point=b.Location;Through(b,150);Require(b.Location!=point&&p.InRange(b,20),"Shared skeleton owner-follow did not move");
            b.StoreWorld(IronGolemWorld.Park(b.WorldState.Controller) with{NextThink=b.WorldState.Controller.Frame+1000});
            SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/3+17);int hp=SkillHealth.Balance256(b),rate=BattleOrders.Regeneration256(b);int frame=b.WorldState.Controller.Frame;
            Through(b,frame+10);Require(SkillHealth.Balance256(b)==hp+10*rate,"World skeleton recovery omitted or doubled source rate");
            var state=b.WorldState.Controller;bool bad=false;try{b.StoreWorld(state with{Mode=4,Target=0,EndAt=0});}catch(InvalidDataException){bad=true;}
            Require(bad&&b.WorldState.Controller==state,"Malformed skeleton clock partially changed state");
            var source=NpcEquipmentRandom.State(b).Seed;hp=SkillHealth.Balance256(b);Through(b,frame+100,false);
            Require(b.Map==Map.Internal&&b.WorldState.Controller.Mode==1&&SkillHealth.Balance256(b)==hp&&NpcEquipmentRandom.State(b).Seed==source,"Suspended skeleton healed or acted");
        }
        finally{b.Delete();p.Delete();}
    }
    private static void NativeRecipient()
    {
        var p=Owner();var b=Body(p);var t=new Dog{Frozen=true,CantWalk=true,NoKillAwards=true,FightMode=FightMode.Closest};
        t.SetHits(20000);t.MoveToWorld(b.Location,p.Map);t.AIObject.AITimer.Stop();
        try
        {
            ulong seed=1;for(;seed<10000;seed++){ulong n=seed;SourceRandom.Roll(ref n);if(SourceRandom.Roll(ref n)%100<80){break;}}
            NpcEquipmentRandom.Set(b,seed);Require(IronGolemWorld.Eligible(b,p,t),"Native recipient silently excluded by missing source footprint");
            Through(b,25);Require(b.WorldState.Controller.Mode==4&&b.WorldContact==null&&b.LegacyContactReason?.StartsWith("legacy: ")==true,"Native recipient did not retain reported pre-RNG fallback");
            Through(b,44);Require(b.WorldState.Controller.Fault==null&&b.WorldState.Controller.Attempts==1,"Native recipient fallback faulted the world actor");
        }
        finally{t.Delete();b.Delete();p.Delete();}
    }
    private static void Check()
    {
        Combat();Motion();NativeRecipient();var p=Owner();var b=Learned(p);var t=Target(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try{NpcEquipmentRandom.Set(b,Seed(b,t));Through(b,25);Require(b.WorldContact!=null,"Saved skeleton windup missing");}finally{SourceBlocking.BlockRoll=roll;}
        // Hold this inspected cohort's next observation only, so other fixture
        // families/live inputs cannot cancel its windup before the snapshot.
        // Natural unheld timing is checked separately above and in Live.
        b.StoreWorld(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMinutes(1)});
        SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
        var legacy=new RaisedSkeleton{Frozen=true,CantWalk=true};legacy.InitializeExact(p,4,20);legacy.SetControlMaster(p);legacy.MoveToWorld(p.Location,p.Map);legacy.AIObject.AITimer.Stop();
        PersistentMinions.Profiles.Track(legacy);NpcEquipmentRandom.Set(legacy,173);legacy.Internalize();
        var saved=new Saved(p.Serial.Value,b.Serial.Value,t.Serial.Value,legacy.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Balance256(t),NpcEquipmentRandom.State(b).Seed,
            NpcEquipmentRandom.State(legacy).Seed,p.Location,p.Map.MapID,0);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));Require(b.WorldContact!=null&&b.WorldState.Controller.Mode==4,"Snapshot cohort lost active windup");
        Log.Information("Skeleton world:shared AI67 S1end24/think25/A1action37/end44,captured damage/no replay,cancel,owner-follow,exact recovery,offline and malformed clock controls pass; active windup and historical actor staged");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(SkeletonWorldBody)World.FindMobile((Serial)s.Body);
        var t=(GraveyardSkeleton)World.FindMobile((Serial)s.Target);var legacy=(RaisedSkeleton)World.FindMobile((Serial)s.Legacy);
        Require(b.ControlMaster==p&&b.Map==Map.Internal&&SkillHealth.Balance256(b)==s.Life&&b.WorldContact==null&&b.WorldState.Controller.Mode==1
            &&b.WorldState.Controller.ActionAt==0&&NpcEquipmentRandom.State(b).Seed==s.Seed&&SkillHealth.Balance256(t)==s.TargetLife,"Loaded skeleton replayed pending contact or changed wounds/stream");
        Require(legacy.GetType()==typeof(RaisedSkeleton)&&legacy.ControlMaster==p&&legacy.Map==Map.Internal&&NpcEquipmentRandom.State(legacy).Seed==s.LegacySeed,"Historical skeleton layout/ownership changed");
        Through(b,b.WorldState.Controller.Frame+1,false);Require(NpcEquipmentRandom.State(b).Seed==s.Seed&&SkillHealth.Balance256(t)==s.TargetLife,"Loaded skip replayed contact RNG/HP");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"skeleton-world");var b=Learned(p);var t=Target(p,b);
        var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;NpcEquipmentRandom.Set(b,Seed(b,t));int hp=SkillHealth.Balance256(t);int damage=0;bool captured=false;long deadline=Core.TickCount+10000;
        void End(){SourceBlocking.BlockRoll=roll;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural skeleton controller timeout/fault");
                if(!captured&&b.WorldContact is{} packet){captured=true;damage=packet.Impact.Packet.Total256;Require(SkillHealth.Balance256(t)==hp,"Natural packet dealt HP at preparation");t.SetResistance(ResistanceType.Physical,100);}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(captured&&damage>0&&hp-SkillHealth.Balance256(t)==damage&&b.WorldState.Controller.Attempts==1,"Natural automatic skeleton hit did not release captured packet once");
                End();Log.Information("skeleton-world-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"skeleton-world-live-result: failed");}
        }
        Log.Information("skeleton-world-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
