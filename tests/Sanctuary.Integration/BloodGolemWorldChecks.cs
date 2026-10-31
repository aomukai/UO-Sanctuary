// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BloodGolemWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BloodGolemWorldChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,uint Historical,int OwnerLife,int Life,int TargetLife,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blood-golem-world-fixtures.json");
    public static void Configure()
    {
        Register("blood-golem-world-check",Check);Register("blood-golem-world-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("blood-golem-world-live","Connected learned Blood automatic combat and healing",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("blood-golem-world-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned Blood shared world/contact healing and persistence",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureBloodWorld";FixtureSpace.Corridor(p,7100,40,3600);
        foreach(var pair in new[]{("necromancer.claygolem",1),("necromancer.bloodgolem",4),("necromancer.golemmastery",20)})
        {while(ClassCreation.Profiles.GetRank(p.Serial,pair.Item1)<pair.Item2){Require(ClassCreation.Profiles.TryTrain(p.Serial,pair.Item1,out _),"World train "+pair.Item1);}}
        return p;
    }
    private static BloodGolemWorldBody Learned(PlayerMobile p,int dx=1,bool future=true)
    {
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.BloodGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.BloodGolem,Serial.Zero,new(p.X+dx,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned Blood release/once-only payment");
        var b=NecromancerGolems.For(p).OfType<BloodGolemWorldBody>().Single();Require(b.WorldState.Owner==p.Serial.Value&&b.ControlMaster==p&&b.WorldState.Controller is{Mode:8,EndAt:26,NextThink:25},"Learned Blood startup descriptor");
        if(future){b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}return b;
    }
    private static GraveyardZombie Target(PlayerMobile p,BloodGolemWorldBody b)
    {
        var t=new GraveyardZombie{Name="PoisonDaggerFixtureBloodWorldTarget",NoKillAwards=true,Frozen=true,CantWalk=true};t.SetHits(20000);t.MoveToWorld(b.Location,p.Map);t.AIObject.AITimer.Stop();t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);
        Require(GemCombat.Drain(t)==100&&BloodGolemHealing.Alignment(t)==0,"Blood world recipient drain/alignment");return t;
    }
    private static void Through(BloodGolemWorldBody b,int frame,bool actions=true)
    {
        var s=b.WorldState.Controller;Require(frame>s.Frame,"Reverse Blood world clock");IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
        Require(!b.Deleted&&b.WorldState.Controller.Fault==null,"Blood controller fault/deletion");
    }
    private static ulong Seed(BloodGolemWorldBody b,BaseCreature t)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRating(b),ClassStats.Defense(t),ClassStats.Level(b),ClassStats.Level(t));
        for(ulong n=1;n<10000;n++){ulong s=n;SourceRandom.Roll(ref s);if(SourceRandom.Roll(ref s)%100<80&&SourceRandom.Roll(ref s)%100/100.0<chance){return n;}}
        throw new InvalidOperationException("No Blood world hit seed");
    }
    private static void Wound(PlayerMobile p,BloodGolemWorldBody b)
    {SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-10001);SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-20003);TimedEffects.PreventHealing(b,TimedEffects.Now);}
    private static (int Owner,int Body) Healed(PlayerMobile p,BloodGolemWorldBody b,int physical,int targetLife)
    {
        int budget=SourcePercentage.Calculate(Math.Min(physical,targetLife),BloodGolemHealing.Conversion(b.Rank),100);
        int ownerMissing=SkillHealth.Maximum256(p)-SkillHealth.Balance256(p),bodyMissing=SkillHealth.Maximum256(b)-SkillHealth.Balance256(b);
        int owner=Math.Min(ownerMissing,SourcePercentage.Calculate(budget,30,100)),body=Math.Min(bodyMissing,budget-owner);
        return(owner+Math.Min(ownerMissing-owner,budget-owner-body),body);
    }
    private static void Combat(bool lethal)
    {
        var p=Owner();var b=Learned(p);var t=Target(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            Wound(p,b);NpcEquipmentRandom.Set(b,Seed(b,t));int owner=SkillHealth.Balance256(p),body=SkillHealth.Balance256(b),hp=SkillHealth.Balance256(t);Through(b,24);
            Require(b.WorldState.Controller is{Mode:8,EndAt:26,NextThink:25}&&b.WorldContact==null&&SkillHealth.Balance256(t)==hp,"Blood acted before first think");
            // Existing first-think25/S1end26 convention; exact current mode
            // eligibility/event ordering remains an explicit reference gap.
            Through(b,25);var packet=b.WorldContact;
            Require(b.WorldState.Controller is{Mode:4,ActionAt:35,EndAt:41,AttackSpeed:256}&&packet!=null&&(packet.Impact.Packet.ResultFlags&1)!=0,"Blood G2action10/end16 clock/capture");
            Require(SkillHealth.Balance256(p)==owner&&SkillHealth.Balance256(b)==body&&SkillHealth.Balance256(t)==hp,"Blood healed or damaged at preparation");
            int damage=packet.Impact.Packet.Total256;ulong seed=NpcEquipmentRandom.State(b).Seed;
            if(lethal){SkillHealth.SetBalance256(t,257);hp=257;Require(damage>hp,"Lethal Blood fixture is not overkill");}
            var heal=Healed(p,b,packet.Impact.Packet.Physical256,hp);t.SetResistance(ResistanceType.Physical,100);Through(b,34);
            Require(SkillHealth.Balance256(t)==hp&&SkillHealth.Balance256(p)==owner&&SkillHealth.Balance256(b)==body,"Early Blood damage/healing");
            Through(b,35);Require(b.WorldState.Controller is{Attempts:1,Hits:1}&&b.WorldContact==null&&packet.Consumed&&NpcEquipmentRandom.State(b).Seed==seed
                &&SkillHealth.Balance256(p)==owner+heal.Owner&&SkillHealth.Balance256(b)==body+heal.Body,"Blood world heal order/budget,duplicate healing or RNG changed");
            Require(lethal?!t.Alive&&t.LastKiller==b&&ClassLoot.Killer(t)==p:hp-SkillHealth.Balance256(t)==damage,"Blood captured mitigation/kill credit");
            owner=SkillHealth.Balance256(p);body=SkillHealth.Balance256(b);hp=SkillHealth.Balance256(t);Through(b,41);
            Require(b.WorldState.Controller.Mode==1&&SkillHealth.Balance256(p)==owner&&SkillHealth.Balance256(b)==body&&SkillHealth.Balance256(t)==hp,"Blood animation end replayed damage/heal");
            if(lethal){return;}
            t.SetResistance(ResistanceType.Physical,0);b.Ground.Store(IronGolemWorld.Park(b.WorldState.Controller));NpcEquipmentRandom.Set(b,Seed(b,t));Through(b,42);packet=b.WorldContact;
            Require(packet!=null,"Second Blood action not captured");seed=NpcEquipmentRandom.State(b).Seed;b.InterruptForRecovery();
            Require(packet.Consumed&&b.WorldContact==null&&b.WorldState.Controller.Mode==1,"Blood recovery retained packet");Through(b,80,false);
            Require(b.Map==Map.Internal&&SkillHealth.Balance256(p)==owner&&SkillHealth.Balance256(b)==body&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.State(b).Seed==seed,"Offline Blood healed,damaged or rerolled canceled contact");
        }
        finally{SourceBlocking.BlockRoll=roll;t.Corpse?.Delete();t.Delete();b.Delete();p.Delete();}
    }
    private static void Movement()
    {
        var p=Owner();var b=Learned(p,16);
        try
        {
            var point=b.Location;Through(b,25);var s=b.WorldState.Controller;int expected=SourceActorMotion.Speed(9,184,100+b.VelocityPercent+s.MovingBonus,0).Velocity;
            Require(b.VelocityPercent>0&&s.Mode==2&&s.Motion.Velocity==expected,"Blood lost its own velocity/Mastery movement");Through(b,150);Require(b.Location!=point&&p.InRange(b,20),"Blood owner-follow did not move");
            b.Ground.Store(IronGolemWorld.Park(b.WorldState.Controller) with{NextThink=b.WorldState.Controller.Frame+1000});SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/3+17);int hp=SkillHealth.Balance256(b),rate=BattleOrders.Regeneration256(b),frame=b.WorldState.Controller.Frame;
            Through(b,frame+10);Require(SkillHealth.Balance256(b)==hp+10*rate,"Blood world regeneration omitted or doubled");
            var state=b.WorldState.Controller;bool bad=false;try{b.Ground.Store(state with{Mode=4,Target=0,EndAt=0});}catch(InvalidDataException){bad=true;}Require(bad&&b.WorldState.Controller==state,"Malformed Blood state partially applied");
        }
        finally{b.Delete();p.Delete();}
    }
    private static void NativeRecipient()
    {
        var p=Owner();var b=Learned(p);var t=new Dog{Frozen=true,CantWalk=true,NoKillAwards=true,FightMode=FightMode.Closest};t.SetHits(20000);t.MoveToWorld(b.Location,p.Map);t.AIObject.AITimer.Stop();
        try{NpcEquipmentRandom.Set(b,1);Require(IronGolemWorld.Eligible(b,p,t),"Blood native recipient excluded");Through(b,25);Require(b.WorldState.Controller.Mode==4&&b.WorldContact==null&&b.LegacyContactReason?.StartsWith("legacy: ")==true,"Blood native fallback not reported");Through(b,41);Require(b.WorldState.Controller.Attempts==1,"Blood native fallback did not complete");}
        finally{t.Delete();b.Delete();p.Delete();}
    }
    private static void Check()
    {
        Combat(false);Combat(true);Movement();NativeRecipient();var p=Owner();var b=Learned(p);var t=Target(p,b);Wound(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try{NpcEquipmentRandom.Set(b,Seed(b,t));Through(b,25);Require(b.WorldContact!=null,"Saved Blood windup missing");}finally{SourceBlocking.BlockRoll=roll;}
        // Snapshot-only observation hold, distinct from unheld Live combat.
        b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMinutes(2)});
        var old=new BloodGolemBody{Frozen=true,CantWalk=true};old.Prepare(p,CombatAbility.BloodGolem);old.SetControlMaster(p);old.MoveToWorld(p.Location,p.Map);old.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(old);old.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,t.Serial.Value,old.Serial.Value,SkillHealth.Balance256(p),SkillHealth.Balance256(b),SkillHealth.Balance256(t),NpcEquipmentRandom.State(b).Seed,0)));
        Require(b.WorldContact!=null,"Blood snapshot lost active packet");Log.Information("Blood world:shared AI67,G2action10/end16,exact owner/pet pre-HP healing including lethal257FP clamp,no double healing,capture/no replay,cancel,mastery movement,regeneration,native fallback and malformed-state controls pass;modern startup parity remains open");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(BloodGolemWorldBody)World.FindMobile((Serial)s.Body);var t=(GraveyardZombie)World.FindMobile((Serial)s.Target);var old=(BloodGolemBody)World.FindMobile((Serial)s.Historical);
        Require(b.ControlMaster==p&&b.Map==Map.Internal&&b.WorldContact==null&&b.WorldState.Controller is{Mode:1,ActionAt:0}&&SkillHealth.Balance256(p)==s.OwnerLife&&SkillHealth.Balance256(b)==s.Life
            &&NpcEquipmentRandom.State(b).Seed==s.Seed&&SkillHealth.Balance256(t)==s.TargetLife,"Loaded Blood replayed contact/healing or changed wounds/stream");
        Require(old.GetType()==typeof(BloodGolemBody)&&old.ControlMaster==p&&old.Map==Map.Internal,"Historical Blood save/owner changed");Through(b,b.WorldState.Controller.Frame+1,false);
        Require(SkillHealth.Balance256(p)==s.OwnerLife&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife&&NpcEquipmentRandom.State(b).Seed==s.Seed,"Loaded Blood skip replayed heal/RNG/HP");File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"blood-world");var b=Learned(p,future:false);var t=Target(p,b);Wound(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        NpcEquipmentRandom.Set(b,Seed(b,t));int hp=SkillHealth.Balance256(t),owner=SkillHealth.Balance256(p),body=SkillHealth.Balance256(b),damage=0;var heal=(Owner:0,Body:0);bool captured=false;long deadline=Core.TickCount+10000;
        void End(){SourceBlocking.BlockRoll=roll;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural Blood timeout/fault");
                if(!captured&&b.WorldContact is{} packet){captured=true;damage=packet.Impact.Packet.Total256;heal=Healed(p,b,packet.Impact.Packet.Physical256,hp);Require(SkillHealth.Balance256(t)==hp&&SkillHealth.Balance256(p)==owner&&SkillHealth.Balance256(b)==body,"Natural Blood healed/damaged at preparation");t.SetResistance(ResistanceType.Physical,100);}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(captured&&damage>0&&hp-SkillHealth.Balance256(t)==damage&&b.WorldState.Controller.Attempts==1&&SkillHealth.Balance256(p)==owner+heal.Owner&&SkillHealth.Balance256(b)==body+heal.Body,"Natural Blood damage/exact owner/pet healing did not release once");End();Log.Information("blood-golem-world-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"blood-golem-world-live-result: failed");}
        }
        Log.Information("blood-golem-world-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
