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

public static class ClayGolemWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ClayGolemWorldChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,uint Historical,int Life,int TargetLife,ulong Seed,Point3D Point,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"clay-golem-world-fixtures.json");
    public static void Configure()
    {
        Register("clay-golem-world-check",Check);Register("clay-golem-world-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("clay-golem-world-live","Connected learned Clay automatic combat",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("clay-golem-world-live-result",Live)));
        foreach(bool quick in new[]{false,true}){bool q=quick;Register(q?"clay-golem-world-quick-check":"clay-golem-world-right-check",()=>FixtureNetwork.Connected(q?"clay-golem-world-quick-result":"clay-golem-world-right-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned Clay shared unarmed world controller",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureClayWorld";FixtureSpace.Corridor(p,7060,40,3550);
        foreach(var pair in new[]{("necromancer.claygolem",4),("necromancer.golemmastery",20)})
        {while(ClassCreation.Profiles.GetRank(p.Serial,pair.Item1)<pair.Item2){Require(ClassCreation.Profiles.TryTrain(p.Serial,pair.Item1,out _),"World train "+pair.Item1);}}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.ClayGolem)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ClayGolem),"Clay right/quick bindings");return p;
    }
    private static ClayGolemWorldBody Learned(PlayerMobile p,int dx=1,bool future=true)
    {
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.ClayGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.ClayGolem,Serial.Zero,new(p.X+dx,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned Clay release/once-only payment");
        var b=NecromancerGolems.For(p).OfType<ClayGolemWorldBody>().Single();Require(b.WorldState.Owner==p.Serial.Value&&b.ControlMaster==p&&b.WorldState.Controller is{Mode:8,EndAt:32,NextThink:25},"Learned Clay owner/startup descriptor");
        if(future){b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}return b;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,ClayGolemWorldBody b)
    {var t=PoisonDaggerChecks.Target(p);t.SetHits(20000);t.MoveToWorld(b.Location,p.Map);t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void Through(ClayGolemWorldBody b,int frame,bool actions=true)
    {
        var s=b.WorldState.Controller;Require(frame>s.Frame,"Reverse Clay world clock");
        IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
        Require(!b.Deleted&&b.WorldState.Controller.Fault==null,"Clay controller fault/deletion");
    }
    private static ulong Seed(ClayGolemWorldBody b,BaseCreature t)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRating(b),ClassStats.Defense(t),ClassStats.Level(b),ClassStats.Level(t));
        for(ulong n=1;n<10000;n++){ulong s=n;SourceRandom.Roll(ref s);if(SourceRandom.Roll(ref s)%100<80&&SourceRandom.Roll(ref s)%100/100.0<chance){return n;}}
        throw new InvalidOperationException("No Clay world hit seed");
    }
    private static void Combat()
    {
        var p=Owner();var b=Learned(p);var t=Target(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            NpcEquipmentRandom.Set(b,Seed(b,t));int hp=SkillHealth.Balance256(t);Through(b,24);
            Require(b.WorldState.Controller is{Mode:8,EndAt:32,NextThink:25}&&b.WorldContact==null&&SkillHealth.Balance256(t)==hp,"Clay acted before first think");
            // First-think25/S1end32 overlap retains the existing conductor
            // convention. Current executable mode/event ordering is still open.
            Through(b,25);var packet=b.WorldContact;
            Require(b.WorldState.Controller is{Mode:4,ActionAt:35,EndAt:43,AttackSpeed:232}&&packet!=null&&SkillHealth.Balance256(t)==hp,"Clay G1 action10/end18 clock or capture");
            Require((packet.Impact.Packet.ResultFlags&1)!=0,"Chosen Clay hit seed failed");int damage=packet.Impact.Packet.Total256;ulong seed=NpcEquipmentRandom.State(b).Seed;
            t.SetResistance(ResistanceType.Physical,100);Through(b,34);Require(SkillHealth.Balance256(t)==hp,"Early Clay world contact");
            Through(b,35);Require(b.WorldState.Controller is{Attempts:1,Hits:1}&&b.WorldContact==null&&packet.Consumed&&hp-SkillHealth.Balance256(t)==damage
                &&NpcEquipmentRandom.State(b).Seed==seed&&TimedEffects.Slow(t,TimedEffects.Now)==b.ReactiveSlowPercent,"Clay changed captured mitigation/RNG or lost outgoing slow");
            int after=SkillHealth.Balance256(t);Through(b,43);Require(b.WorldState.Controller.Mode==1&&SkillHealth.Balance256(t)==after,"Clay animation end replayed contact");
            t.SetResistance(ResistanceType.Physical,0);TimedEffects.Clear(t);b.Ground.Store(IronGolemWorld.Park(b.WorldState.Controller));NpcEquipmentRandom.Set(b,Seed(b,t));Through(b,44);
            packet=b.WorldContact;Require(packet!=null,"Second Clay action not captured");seed=NpcEquipmentRandom.State(b).Seed;hp=SkillHealth.Balance256(t);
            b.InterruptForRecovery();Require(packet.Consumed&&b.WorldContact==null&&b.WorldState.Controller.Mode==1,"Clay recovery retained packet");
            Through(b,80,false);Require(b.Map==Map.Internal&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.State(b).Seed==seed,"Offline Clay acted or replayed canceled packet");
        }
        finally{SourceBlocking.BlockRoll=roll;t.Delete();b.Delete();p.Delete();}
    }
    private static void Movement()
    {
        var p=Owner();var b=Learned(p,16);
        try
        {
            var point=b.Location;Through(b,25);var s=b.WorldState.Controller;
            int expected=SourceActorMotion.Speed(8,NecromancerSummons.Source("claygolem-animation","WLSpeed"),100+b.VelocityPercent+s.MovingBonus,0).Velocity;
            Require(b.VelocityPercent>0&&s.Mode==2&&s.Motion.Velocity==expected,"Clay lost captured Golem Mastery movement bonus");
            Through(b,150);Require(b.Location!=point&&p.InRange(b,20),"Clay owner-follow did not move");
            b.Ground.Store(IronGolemWorld.Park(b.WorldState.Controller) with{NextThink=b.WorldState.Controller.Frame+1000});
            SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/3+17);int hp=SkillHealth.Balance256(b),rate=BattleOrders.Regeneration256(b);int frame=b.WorldState.Controller.Frame;
            Through(b,frame+10);Require(SkillHealth.Balance256(b)==hp+10*rate,"Clay world regeneration omitted or doubled");
            var state=b.WorldState.Controller;bool bad=false;try{b.Ground.Store(state with{Mode=4,Target=0,EndAt=0});}catch(InvalidDataException){bad=true;}
            Require(bad&&b.WorldState.Controller==state,"Malformed Clay state partially applied");
            hp=SkillHealth.Balance256(b);ulong seed=NpcEquipmentRandom.State(b).Seed;Through(b,frame+100,false);
            Require(b.Map==Map.Internal&&SkillHealth.Balance256(b)==hp&&NpcEquipmentRandom.State(b).Seed==seed,"Offline Clay healed or rerolled");
        }
        finally{b.Delete();p.Delete();}
    }
    private static void NativeRecipient()
    {
        var p=Owner();var b=Learned(p);var t=new Dog{Frozen=true,CantWalk=true,NoKillAwards=true,FightMode=FightMode.Closest};t.SetHits(20000);t.MoveToWorld(b.Location,p.Map);t.AIObject.AITimer.Stop();
        try
        {NpcEquipmentRandom.Set(b,1);Require(IronGolemWorld.Eligible(b,p,t),"Clay native recipient excluded");Through(b,25);Require(b.WorldState.Controller.Mode==4&&b.WorldContact==null&&b.LegacyContactReason?.StartsWith("legacy: ")==true,"Clay native fallback not reported");Through(b,43);Require(b.WorldState.Controller.Attempts==1,"Clay native fallback did not finish");}
        finally{t.Delete();b.Delete();p.Delete();}
    }
    private static void Check()
    {
        Combat();Movement();NativeRecipient();var p=Owner();var b=Learned(p);var t=Target(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try{NpcEquipmentRandom.Set(b,Seed(b,t));Through(b,25);Require(b.WorldContact!=null,"Saved Clay windup missing");}finally{SourceBlocking.BlockRoll=roll;}
        // Snapshot-only observation hold; Live checks the unheld natural clock.
        b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMinutes(2)});
        SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
        var old=new ClayGolemBody{Frozen=true,CantWalk=true};old.Prepare(p,CombatAbility.ClayGolem);old.SetControlMaster(p);old.MoveToWorld(p.Location,p.Map);old.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(old);old.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,t.Serial.Value,old.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Balance256(t),NpcEquipmentRandom.State(b).Seed,p.Location,p.Map.MapID,0)));
        Require(b.WorldContact!=null,"Clay snapshot lost active packet");
        Log.Information("Clay world:shared AI67,G1action10/end18,captured packet/outgoing slow,no replay,cancel,mastery movement,follow,exact regeneration,offline,native fallback and malformed-state checks pass;startup mode/event parity remains open");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(ClayGolemWorldBody)World.FindMobile((Serial)s.Body);var t=(GraveyardSkeleton)World.FindMobile((Serial)s.Target);var old=(ClayGolemBody)World.FindMobile((Serial)s.Historical);
        Require(b.ControlMaster==p&&b.Map==Map.Internal&&SkillHealth.Balance256(b)==s.Life&&b.WorldContact==null&&b.WorldState.Controller is{Mode:1,ActionAt:0}
            &&NpcEquipmentRandom.State(b).Seed==s.Seed&&SkillHealth.Balance256(t)==s.TargetLife,"Loaded Clay replayed contact or changed wounds/stream");
        Require(old.GetType()==typeof(ClayGolemBody)&&old.ControlMaster==p&&old.Map==Map.Internal,"Historical Clay save/owner changed");Through(b,b.WorldState.Controller.Frame+1,false);
        Require(NpcEquipmentRandom.State(b).Seed==s.Seed&&SkillHealth.Balance256(t)==s.TargetLife,"Loaded Clay skip replayed RNG/HP");File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"clay-world");var b=Learned(p,future:false);var t=Target(p,b);var roll=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        NpcEquipmentRandom.Set(b,Seed(b,t));int hp=SkillHealth.Balance256(t),damage=0;bool captured=false;long deadline=Core.TickCount+10000;
        void End(){SourceBlocking.BlockRoll=roll;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural Clay controller timeout/fault");
                if(!captured&&b.WorldContact is{} packet){captured=true;damage=packet.Impact.Packet.Total256;Require(SkillHealth.Balance256(t)==hp,"Clay committed HP at preparation");t.SetResistance(ResistanceType.Physical,100);}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(captured&&damage>0&&hp-SkillHealth.Balance256(t)==damage&&b.WorldState.Controller.Attempts==1&&TimedEffects.Slow(t,TimedEffects.Now)==b.ReactiveSlowPercent,"Natural Clay captured packet/slow did not release once");End();Log.Information("clay-golem-world-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"clay-golem-world-live-result: failed");}
        }
        Log.Information("clay-golem-world-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"clay-world-input");PoisonDaggerChecks.Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.ClayGolem).Mana256;string label=quick?"clay-golem-world-quick":"clay-golem-world-right";var point=new Point3D(p.X+2,p.Y,p.Z);bool began=false;long deadline=Core.TickCount+6000;
        void End(){foreach(var b in NecromancerGolems.For(p)){b.Delete();}p.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Clay input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.ClayGolem&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana,"Clay input paid early");began=true;Log.Information("{Label}: windup",label);}
                var b=NecromancerGolems.For(p).OfType<ClayGolemWorldBody>().SingleOrDefault();if(b==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&b.WorldState.Owner==p.Serial.Value&&b.WorldState.Controller.Fault==null&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Clay real input/controller/payment");End();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){End();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.ClayGolem,(int)DiabloClass.Necromancer,point.X,point.Y,point.Z);Inspect();});
    }
}
