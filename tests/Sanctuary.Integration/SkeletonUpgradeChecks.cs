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
public static class SkeletonUpgradeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SkeletonUpgradeChecks));
    private sealed record Saved(uint Owner,uint Body,uint Legacy,uint Raised,int Life,int LegacyLife,int RaisedLife,int Rank,int Mastery,int Maximum,long Order,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"skeleton-upgrades-fixtures.json");
    public static void Configure()
    {
        Register("skeleton-upgrades-check",Check);Register("skeleton-upgrades-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("skeleton-upgrades-live","Connected rank20 skeleton shared Extra Fast movement",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("skeleton-upgrades-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved live hard10 ATD34/hard20 Extra Fast",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,int rank)=>MagePetBuffChecks.Train(p,"necromancer.raiseskeleton",rank);
    private static PlayerMobile Owner(int rank=9,int row=3300)
    {var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6100,40,row);Train(p,rank);return p;}
    private static SkeletonWorldBody Learned(PlayerMobile p)
    {
        var dead=new GraveyardZombie{NoKillAwards=true};dead.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);dead.Damage(100000,p);if(dead.Alive){dead.Kill();}var corpse=dead.Corpse as Corpse;
        NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(SkeletonPets.Raise(p,corpse),"Upgrade learned skeleton");corpse.Delete();dead.Delete();var pet=NecromancerSkeletons.For(p,CombatAbility.RaiseSkeleton).OfType<SkeletonWorldBody>().Last();Hold(pet);return pet;
    }
    private static void Hold(SkeletonWorldBody b)=>b.StoreWorld(IronGolemWorld.Park(b.WorldState.Controller) with{NextUtc=TimedEffects.Now.AddHours(1)});
    private static Ogre Enemy(PlayerMobile p,Mobile pet)
    {var m=new Ogre{NoKillAwards=true,CantWalk=true,Frozen=true};m.MoveToWorld(pet.Location,p.Map);m.AIObject.AITimer.Stop();m.SetHits(20000);m.Hits=m.HitsMax;foreach(var r in Enum.GetValues<ResistanceType>()){m.SetResistance(r,0);}TimedEffects.PreventHealing(m,TimedEffects.Now);return m;}
    private static void ReturnCases(PlayerMobile p,SkeletonGuardian b,int expected)
    {
        var enemy=Enemy(p,b);
        try
        {
            int hp=SkillHealth.Balance256(enemy);using(PaladinThorns.Attempt(enemy,b,false)){}Require(hp-SkillHealth.Balance256(enemy)==expected*256,"Miss/blocked attempt flat ATD");
            hp=SkillHealth.Balance256(enemy);using(PaladinThorns.Attempt(enemy,b,false)){PaladinThorns.PreparedFlatAttempt(enemy,b);PaladinThorns.PreparedFlatAttempt(enemy,b);}Require(hp-SkillHealth.Balance256(enemy)==expected*256,"Duplicate return in one attempt");
            hp=SkillHealth.Balance256(enemy);AOS.Damage(b,enemy,1,false,100,0,0,0,0,weaponDamage:true);Require(hp-SkillHealth.Balance256(enemy)==expected*256,"Actual native prepared melee return");
            hp=SkillHealth.Balance256(enemy);AOS.Damage(b,enemy,1,false,0,0,0,0,0,weaponDamage:true);Require(hp-SkillHealth.Balance256(enemy)==expected*256,"Zero-damage attempt lost flat return");
            hp=SkillHealth.Balance256(enemy);AOS.Damage(b,enemy,1,false,100,0,0,0,0,archer:true,weaponDamage:true);AOS.Damage(b,enemy,1,false,100,0,0,0,0,spellDamage:true);Require(SkillHealth.Balance256(enemy)==hp,"Ranged/spell reflected flat ATD");
            var old=ClassCombatHooks.ClassHitCheck;try{ClassCombatHooks.ClassHitCheck=(_,_)=>false;enemy.Frozen=false;hp=SkillHealth.Balance256(enemy);((BaseWeapon)enemy.Weapon).OnSwing(enemy,b);Require(hp-SkillHealth.Balance256(enemy)==expected*256,"Real native missed swing lost flat ATD");}finally{ClassCombatHooks.ClassHitCheck=old;enemy.Frozen=true;}
            enemy.SetResistance(ResistanceType.Physical,50);hp=SkillHealth.Balance256(enemy);using(PaladinThorns.Attempt(enemy,b,false)){}Require(hp-SkillHealth.Balance256(enemy)==expected*128,"Flat return skipped shared physical resistance");
        }
        finally{enemy.Delete();}
    }
    private static void EncounterReuse()
    {
        foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())foreach(var grade in new[]{EncounterGrade.Ordinary,EncounterGrade.Unique,EncounterGrade.Minion})
        {
            var profile=EncounterProfiles.Profile("crypt-sentinel-60");var a=new EncounterCreature();var b=new EncounterCreature();
            try
            {
                a.Assign(profile,60,difficulty,grade,ChampionKind.Champion,[],EliteAura.None,null,new Random(836));b.Assign(profile,60,difficulty,grade,ChampionKind.Champion,[EliteModifier.ExtraFast],EliteAura.None,null,new Random(836));
                Require(b.CombatProfile with{MoveMs=a.CombatProfile.MoveMs}==a.CombatProfile&&b.CombatProfile.MoveMs==EncounterProfiles.MovementInterval(a.CombatProfile.MoveMs,EncounterProfiles.ModifierMovementPercent(EliteModifier.ExtraFast))&&b.NormalStats==a.NormalStats,"Shared Extra Fast changed wild stats/attacks or movement rule");
            }
            finally{a.Delete();b.Delete();}
        }
        Require(EncounterProfiles.MovementInterval(40,100)==40&&EncounterProfiles.ModifierMovementPercent(EliteModifier.ExtraStrong)==0,"Existing movement floor/other affix changed");
    }
    private static void Motion(PlayerMobile p,SkeletonWorldBody b)
    {
        // Isolate a real actor frame on the existing toward-path controller.
        var origin=b.Location;var c=b.WorldState.Controller;int x=b.X*2,y=b.Y*2;
        var initial=DruidRavenMotion.Center(x,y) with{PathType=13,Flags=0x100,Distance=20,Velocity=2048,MaximumVelocity=2048};
        var motion=SourceActorPath.Initialize(new(initial,new(x+20,y),initial.FinalTarget),20,new(1,1,p.Map.Width*2-1,p.Map.Height*2-1),(_,_)=>0).Motion;
        Require((motion.Flags&0x20)!=0,"Movement fixture lacks active source path");
        var walk=c with{Mode=2,ActionAt=0,EndAt=0,NextThink=0,Target=0,Motion=motion,MovingBonus=0};
        b.StoreWorld(walk);IronGolemWorld.Advance(b,walk.NextUtc,true);var normal=b.WorldState.Controller.Motion;uint delta=normal.X-motion.X;
        Train(p,20);b.MoveToWorld(origin,p.Map);b.StoreWorld(walk);IronGolemWorld.Advance(b,walk.NextUtc,true);var fast=b.WorldState.Controller.Motion;
        Require(delta>0&&fast.X-motion.X==2*delta&&fast.Velocity==2*normal.Velocity&&b.SourceAttackClock==(12,19)&&b.CombatProfile.AttackMs==760,"Live Extra Fast did not double actual source motion or changed attack speed");Hold(b);
    }
    private static void Direct()
    {
        EncounterReuse();var p=Owner();var helm=PoisonDaggerChecks.Named(p,"Wormskull");var b=Learned(p);var original=b.Serial;int maximum=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,maximum-257);TimedEffects.PreventHealing(b,TimedEffects.Now);int life=SkillHealth.Balance256(b);long order=BattleCommand.PetOrder(b);ulong seed=NpcEquipmentRandom.State(b).Seed;
        try
        {
            Require(b.Rank==10&&SkeletonPetUpgrades.HardRank(b)==9&&PaladinThorns.FlatDamage(b)==0&&b.CombatProfile.MoveMs==240,"Real soft1 unlocked hard10 ATD");
            Train(p,10);Require(PaladinThorns.FlatDamage(b)==34&&SkeletonPetUpgrades.FlatDamage(b)==34,"Hard10 did not unlock fixed34");ReturnCases(p,b,34);SkillHealth.SetBalance256(b,life);
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Train(p,19);Require(p.EquipItem(helm),"Reequip actual soft point");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.RaiseSkeleton)==20&&SkeletonPetUpgrades.HardRank(b)==19&&SkeletonPetUpgrades.MovementPercent(b)==0&&b.CombatProfile.MoveMs==240,"Soft point unlocked Extra Fast");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Motion(p,b);Require(SkeletonPetUpgrades.Has(b,EliteModifier.ExtraFast)&&SkeletonPetUpgrades.MovementPercent(b)==100&&b.CombatProfile.MoveMs==120,"Hard20 existing Extra Fast rule");
            Require(b.Serial==original&&b.Rank==10&&SkillHealth.Balance256(b)==life&&SkillHealth.Maximum256(b)==maximum&&BattleCommand.PetOrder(b)==order&&NpcEquipmentRandom.State(b).Seed==seed,"Unlock recaptured birth state,healed,replaced,reordered or rerolled");
            var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Lance of Yaggai").Id,99,new Random(836),true);item.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);Require(IronGolemItems.TryAttach(p,b,item,out var why),"Shared item ATD control: "+why);
            Require(NpcEquipmentCombat.Bonus(b,"thorns")==8&&PaladinThorns.FlatDamage(b)==42,"Skill34 did not aggregate with actual item8");ReturnCases(p,b,42);IronGolemItems.DestroyBoundItem(b);Require(PaladinThorns.FlatDamage(b)==34,"Item removal dropped skill or retained item");
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(!b.Deleted&&PaladinThorns.FlatDamage(b)==0&&b.CombatProfile.MoveMs==240,"Respec retained unlocks or replaced pet");Train(p,20);Require(PaladinThorns.FlatDamage(b)==34&&b.CombatProfile.MoveMs==120,"Relearn failed existing actor");
            var mage=new NecromancerMinion();try{MagePetBuffChecks.Train(p,"necromancer.skeletalmage",1);mage.Prepare(p,CombatAbility.RaiseSkeletalMage);mage.SetControlMaster(p);Require(SkeletonPetUpgrades.FlatDamage(mage)==0&&SkeletonPetUpgrades.MovementPercent(mage)==0,"Mage inherited warrior unlocks");}finally{mage.Delete();}
        }
        finally{b.Delete();p.Delete();}
        Log.Information("Skeleton upgrades:hard9+real soft1/19+soft1 locked,live hard10 ATD34/hard20 existing movement-only Extra Fast,item8aggregation/real native hit/miss/zero/ranged/spell/resistance controls,respec/relearn,no identity/wound/rank/order/seed/attack-clock changes;9wild affix controls and real doubled source motion pass");
    }
    private static void Check()
    {
        Direct();var p=Owner(20);var b=Learned(p);var legacy=new SkeletonGuardian();legacy.Initialize(p,10,0);legacy.SetControlMaster(p);legacy.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);legacy.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(legacy);BattleCommand.Track(legacy);
        var raised=new RaisedSkeleton();raised.InitializeExact(p,10,0);raised.SetControlMaster(p);raised.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);raised.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(raised);BattleCommand.Track(raised);
        foreach(var pet in new SkeletonGuardian[]{b,legacy,raised}){SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)-257);TimedEffects.PreventHealing(pet,TimedEffects.Now);Require(PaladinThorns.FlatDamage(pet)==34&&pet.CombatProfile.MoveMs==120,"Historical warrior unlock path");}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,legacy.Serial.Value,raised.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Balance256(legacy),SkillHealth.Balance256(raised),b.Rank,b.Mastery,SkillHealth.Maximum256(b),BattleCommand.PetOrder(b),NpcEquipmentRandom.State(b).Seed,0)));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(SkeletonWorldBody)World.FindMobile((Serial)s.Body);var old=(SkeletonGuardian)World.FindMobile((Serial)s.Legacy);var raised=(RaisedSkeleton)World.FindMobile((Serial)s.Raised);
        Require(new SkeletonGuardian[]{b,old,raised}.All(x=>x.ControlMaster==p&&PaladinThorns.FlatDamage(x)==34&&x.CombatProfile.MoveMs==120)&&old.GetType()==typeof(SkeletonGuardian)&&raised.GetType()==typeof(RaisedSkeleton)&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(old)==s.LegacyLife&&SkillHealth.Balance256(raised)==s.RaisedLife&&b.Rank==s.Rank&&b.Mastery==s.Mastery&&SkillHealth.Maximum256(b)==s.Maximum&&BattleCommand.PetOrder(b)==s.Order&&NpcEquipmentRandom.State(b).Seed==s.Seed&&b.SourceAttackClock==(12,19),"Saved original warrior unlocks/type/wounds/ranks/order/stream changed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(20,3600);FixtureNetwork.Authenticate(network,p,"warrior-upgrades");var b=Learned(p);b.MoveToWorld(new(p.X+16,p.Y,p.Z),p.Map);b.StoreWorld(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);var origin=b.Location;int life=SkillHealth.Balance256(b);long deadline=Core.TickCount+10000;
        void End(){b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural upgraded warrior movement timeout/fault");
                if(b.Location==origin){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);return;}
                var c=b.WorldState.Controller;var expected=SourceActorMotion.Speed(NecromancerSummons.Source("necroskeleton","Velocity"),NecromancerSummons.Source("necroskeleton-animation","WLSpeed"),checked(200+c.MovingBonus),0);
                Require(network.Running&&c.Motion.Velocity==expected.Velocity&&b.SourceAttackClock==(12,19)&&SkillHealth.Balance256(b)==life&&PaladinThorns.FlatDamage(b)==34, $"Natural Extra Fast network={network.Running},velocity={c.Motion.Velocity}/{expected.Velocity},followBonus={c.MovingBonus},mode={c.Mode},clock={b.SourceAttackClock},life={SkillHealth.Balance256(b)}/{life},ATD={PaladinThorns.FlatDamage(b)}");End();Log.Information("skeleton-upgrades-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"skeleton-upgrades-live-result: failed");}
        }
        Log.Information("skeleton-upgrades-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);
    }
}
