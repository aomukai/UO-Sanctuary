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

public static class FireGolemWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireGolemWorldChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,int Life,int TargetLife,ulong Seed,int AuraFrame,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-golem-world-fixtures.json");
    public static void Configure()
    {
        Register("fire-golem-world-check",Check);Register("fire-golem-world-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("fire-golem-world-live","Learned automatic Fire contact and frozen periodic aura",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("fire-golem-world-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Fire shared ground/tied startup/periodic/death and saves",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureFireWorld";FixtureSpace.Corridor(p,7020,40,3650);
        void Train(string id,int rank){TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"World train "+id);}}
        Train("necromancer.firegolem",4);Train("necromancer.golemmastery",20);return p;
    }
    private static FireGolemWorldBody Learned(PlayerMobile p,int dx=1,bool future=true)
    {
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.FireGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.FireGolem,Serial.Zero,new(p.X+dx,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned Fire release/once-only payment");
        var b=NecromancerGolems.For(p).OfType<FireGolemWorldBody>().Single();Require(b.WorldState.Owner==p.Serial.Value&&b.ControlMaster==p&&b.WorldState.Controller is{Mode:8,EndAt:25,NextThink:25}&&b.NextAuraFrame==50,"Fire startup/aura descriptors");
        if(future){b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}return b;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,FireGolemWorldBody b,int dx=0)
    {var t=PoisonDaggerChecks.Target(p);t.SetHits(20000);t.MoveToWorld(new(b.X+dx,b.Y,b.Z),p.Map);t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Fire,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void Through(FireGolemWorldBody b,int frame,bool actions=true)
    {
        var s=b.WorldState.Controller;Require(frame>s.Frame,"Reverse Fire world clock");IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
        Require(!b.Deleted&&b.WorldState.Controller.Fault==null,"Fire world fault/deletion");
    }
    private static ulong Seed(FireGolemWorldBody b,BaseCreature t)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRating(b),ClassStats.Defense(t),ClassStats.Level(b),ClassStats.Level(t));
        for(ulong n=1;n<10000;n++){ulong s=n;SourceRandom.Roll(ref s);if(SourceRandom.Roll(ref s)%100<80&&SourceRandom.Roll(ref s)%100/100.0<chance){return n;}}
        throw new InvalidOperationException("No Fire world hit seed");
    }
    private static void Combat(bool retire)
    {
        var p=Owner();var b=Learned(p);var t=Target(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            NpcEquipmentRandom.Set(b,Seed(b,t));int hp=SkillHealth.Balance256(t);Through(b,24);Require(b.WorldState.Controller.Mode==8&&b.WorldContact==null,"Fire premature startup action");
            Through(b,25);var packet=b.WorldContact;
            Require(b.WorldState.Controller is{Mode:4,ActionAt:35,EndAt:42,AttackSpeed:256}&&packet!=null&&(packet.Impact.Packet.ResultFlags&1)!=0&&packet.Impact.Packet.Physical256>0&&packet.Impact.Packet.Fire256>0&&SkillHealth.Balance256(t)==hp,"Tied S1end25/think25 lost Fire A1 or mixed packet");
            int damage=packet.Impact.Packet.Total256;ulong seed=NpcEquipmentRandom.State(b).Seed;t.SetResistance(ResistanceType.Fire,100);Through(b,34);Require(SkillHealth.Balance256(t)==hp,"Early Fire contact");
            if(retire)
            {
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,0,TimedEffects.Now.AddHours(1),DamagePercent:10000));SkillHealth.SetBalance256(b,256);
                Through(b,90);Require(b.Retiring&&!b.Alive&&!b.Deleted&&SkillHealth.Balance256(b)==0&&b.WorldContact==null&&packet.Consumed&&hp-SkillHealth.Balance256(t)==damage,"Thorns death did not retain source/cancel ground work");
                var state=b.WorldState.Controller;int aura=b.NextAuraFrame;seed=NpcEquipmentRandom.State(b).Seed;Through(b,120);
                Require(b.WorldState.Controller==state&&b.NextAuraFrame==aura&&NpcEquipmentRandom.State(b).Seed==seed&&SkillHealth.Balance256(b)==0,"Retiring source continued catch-up/regeneration/aura/RNG");return;
            }
            Through(b,35);Require(b.WorldState.Controller is{Attempts:1,Hits:1}&&b.WorldContact==null&&packet.Consumed&&hp-SkillHealth.Balance256(t)==damage&&NpcEquipmentRandom.State(b).Seed==seed,"Fire changed captured packet/mitigation/RNG");
            hp=SkillHealth.Balance256(t);Through(b,42);Require(b.WorldState.Controller.Mode==1&&SkillHealth.Balance256(t)==hp,"Fire end replayed contact");
            t.SetResistance(ResistanceType.Fire,0);b.Ground.Store(IronGolemWorld.Park(b.WorldState.Controller));NpcEquipmentRandom.Set(b,Seed(b,t));Through(b,43);packet=b.WorldContact;Require(packet!=null,"Second Fire capture missing");seed=NpcEquipmentRandom.State(b).Seed;
            b.InterruptForRecovery();Require(packet.Consumed&&b.WorldContact==null,"Fire recovery retained packet");Through(b,130,false);
            Require(b.Map==Map.Internal&&b.NextAuraFrame==150&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.State(b).Seed==seed,"Offline Fire replayed contact/aura or lost periodic phase");
        }
        finally{PaladinAuras.Clear(t);SourceBlocking.BlockRoll=roll;t.Corpse?.Delete();t.Delete();b.Delete();p.Delete();}
    }
    private static void Movement()
    {
        var p=Owner();var b=Learned(p,16);
        try
        {
            var point=b.Location;Through(b,25);var s=b.WorldState.Controller;
            Require(s.Mode==2&&b.VelocityPercent>0&&s.Motion.Velocity==SourceActorMotion.Speed(10,192,100+b.VelocityPercent+s.MovingBonus,0).Velocity,"Fire movement/Mastery supplier or tied first think");
            Through(b,150);Require(b.Location!=point&&p.InRange(b,20),"Fire follow did not move");b.Ground.Store(IronGolemWorld.Park(b.WorldState.Controller) with{NextThink=b.WorldState.Controller.Frame+1000});
            SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/3+17);int hp=SkillHealth.Balance256(b),rate=BattleOrders.Regeneration256(b),frame=b.WorldState.Controller.Frame;Through(b,frame+10);Require(SkillHealth.Balance256(b)==hp+rate*10,"Fire regeneration omitted/doubled");
            var state=b.WorldState.Controller;bool bad=false;try{b.Ground.Store(state with{Mode=4,Target=0,EndAt=0});}catch(InvalidDataException){bad=true;}Require(bad&&b.WorldState.Controller==state,"Malformed Fire state partially applied");
        }
        finally{b.Delete();p.Delete();}
    }
    private static void NativeRecipient()
    {
        var p=Owner();var b=Learned(p);var t=new Dog{Frozen=true,CantWalk=true,NoKillAwards=true,FightMode=FightMode.Closest};t.SetHits(20000);t.MoveToWorld(b.Location,p.Map);t.AIObject.AITimer.Stop();
        try{NpcEquipmentRandom.Set(b,1);Through(b,25);Require(b.WorldState.Controller.Mode==4&&b.WorldContact==null&&b.LegacyContactReason?.StartsWith("legacy: ")==true,"Fire native fallback not reported");Through(b,42);Require(b.WorldState.Controller.Attempts==1,"Fire native fallback failed");}
        finally{t.Delete();b.Delete();p.Delete();}
    }
    private static void Check()
    {
        Combat(false);Combat(true);Movement();NativeRecipient();var p=Owner();var b=Learned(p);var t=Target(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try{NpcEquipmentRandom.Set(b,Seed(b,t));Through(b,25);Require(b.WorldContact!=null,"Saved Fire windup missing");}finally{SourceBlocking.BlockRoll=roll;}
        // Snapshot-only hold survives the complete retained multi-boot suite;
        // unheld natural contact/pulses are checked separately in Live.
        b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,t.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Balance256(t),NpcEquipmentRandom.State(b).Seed,b.NextAuraFrame,0)));
        Log.Information("Fire world:tied S1end25/think25,G3action10/end17,mixed captured contact/no reroll,recovery/offline cancel,periodic phase skip,retirement during Thorns stops catchup/regen,preserved source,Mastery movement and native fallback pass;initial aura/event priority remain adapters");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(FireGolemWorldBody)World.FindMobile((Serial)s.Body);var t=(GraveyardSkeleton)World.FindMobile((Serial)s.Target);
        Require(b.ControlMaster==p&&b.Map==Map.Internal&&b.WorldContact==null&&b.WorldState.Controller is{Mode:1,ActionAt:0}&&b.NextAuraFrame==s.AuraFrame&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife&&NpcEquipmentRandom.State(b).Seed==s.Seed,"Loaded Fire changed wounds/stream/phase or replayed contact");
        Through(b,b.WorldState.Controller.Frame+1,false);Require(SkillHealth.Balance256(t)==s.TargetLife&&NpcEquipmentRandom.State(b).Seed==s.Seed,"Loaded Fire skip replayed damage/RNG");File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{AuraFrame=b.NextAuraFrame,Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"fire-world");var b=Learned(p,future:false);var t=Target(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        NpcEquipmentRandom.Set(b,Seed(b,t));int hp=SkillHealth.Balance256(t),damage=0;bool captured=false,closed=false;long deadline=Core.TickCount+10000;
        void End(){if(closed){return;}closed=true;SourceBlocking.BlockRoll=roll;t.Delete();b.Delete();p.Delete();}
        void PulseWait()
        {
            try
            {
                Require(Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural Fire periodic timeout/fault");
                if(b.WorldState.Controller.Frame<50){Require(SkillHealth.Balance256(t)==hp&&b.NextAuraFrame==50,"Frozen Fire emitted before periodic deadline");Timer.StartTimer(TimeSpan.FromMilliseconds(20),PulseWait);return;}
                Require(b.Frozen&&b.WorldState.Controller.Attempts==0&&b.NextAuraFrame==100&&hp-SkillHealth.Balance256(t)==damage&&PaladinAuras.Effect(b,CombatAbility.HolyFire) is{Rank:10,WeaponMin:51,WeaponMax:90}&&NecromancerEffects.ManaBalance256(p)==0,"Frozen Fire lost automatic no-mana pulse/self weapon lease");
                ulong seed=NpcEquipmentRandom.State(b).Seed;int after=SkillHealth.Balance256(t);IronGolemWorld.Advance(b,b.WorldState.Controller.NextUtc.AddMilliseconds(80*40L),false);
                Require(b.Map==Map.Internal&&b.NextAuraFrame==150&&SkillHealth.Balance256(t)==after&&NpcEquipmentRandom.State(b).Seed==seed,"Offline Fire periodic skip attacked/rerolled");End();Log.Information("fire-golem-world-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"fire-golem-world-live-result: failed");}
        }
        void MeleeWait()
        {
            try
            {
                Require(Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural Fire melee timeout/fault");
                if(!captured&&b.WorldContact is{} packet){captured=true;damage=packet.Impact.Packet.Total256;Require(packet.Impact.Packet.Physical256>0&&packet.Impact.Packet.Fire256>0&&SkillHealth.Balance256(t)==hp,"Natural Fire mixed capture/early HP");t.SetResistance(ResistanceType.Fire,100);}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),MeleeWait);return;}
                Require(captured&&damage>0&&hp-SkillHealth.Balance256(t)==damage&&b.WorldState.Controller.Attempts==1,"Natural Fire immutable release failed");t.Delete();b.Delete();
                b=Learned(p,future:false);b.Frozen=true;t=Target(p,b,1);NecromancerEffects.SetMana256(p,0);NpcEquipmentRandom.Set(b,17);ulong seed=17;
                int min=b.HolyFireValues.Damage256(false),max=b.HolyFireValues.Damage256(true);damage=2*(min+(int)SourceRandom.Limited(ref seed,max-min));hp=SkillHealth.Balance256(t);Timer.StartTimer(TimeSpan.FromMilliseconds(20),PulseWait);
            }
            catch(Exception e){End();Log.Error(e,"fire-golem-world-live-result: failed");}
        }
        Log.Information("fire-golem-world-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),MeleeWait);
    }
}
