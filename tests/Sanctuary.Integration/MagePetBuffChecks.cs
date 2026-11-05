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

public static class MagePetBuffChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MagePetBuffChecks));
    private sealed record Saved(uint Owner,uint Ice,uint Fire,uint Ally,int Life,EnchantState Enchant,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-pet-buff-fixtures.json");
    public static void Configure(){Register("mage-pet-buff-check",Check);Register("mage-pet-buff-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved Mage hard20 automatic Enchant and intrinsic Frozen Armor",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    internal static void Train(PlayerMobile p,string id,int rank)
    {
        TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string pre in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre,1);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train Mage buff "+id);}
    }
    internal static PlayerMobile Owner(int rank=19,int column=6900,int firstRow=3600)
    {var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureMageBuff";FixtureSpace.Corridor(p,column,40,firstRow);Train(p,"necromancer.skeletalmage",rank);return p;}
    internal static MageWorldBody Pet(PlayerMobile p,int element,int dx=3,int dy=0,bool future=true)
    {
        var dead=new GraveyardZombie{NoKillAwards=true};dead.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);dead.Damage(100000,p);if(dead.Alive){dead.Kill();}
        var corpse=dead.Corpse as Corpse;Require(SkeletonPets.Usable(p,corpse),"Mage buff corpse");var old=NecromancerSummons.ElementRoll;
        try{NecromancerSummons.ElementRoll=()=>element;NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(NecromancerCombat.Release(p,CombatAbility.RaiseSkeletalMage,corpse.Serial,corpse.Location),"Mage buff learned summon");}
        finally{NecromancerSummons.ElementRoll=old;corpse.Delete();dead.Delete();}
        var b=NecromancerSkeletons.For(p,CombatAbility.RaiseSkeletalMage).OfType<MageWorldBody>().Last();if(future){b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}return b;
    }
    private static void Through(MageWorldBody b,int frame)
    {var s=b.WorldState.Controller;IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),true);Require(b.WorldState.Controller.Fault==null,"Mage buff controller fault");}
    private static void ForceSkill(MageWorldBody b)
    {
        ulong chosen=0;for(ulong i=1;i<10000;i++){ulong seed=i;if(SourceRandom.Roll(ref seed)%100>=15&&SourceRandom.Roll(ref seed)%100<80){chosen=i;break;}}
        Require(chosen!=0,"Mage buff seed");NecromancerMageMissiles.SetActorSeed(b,chosen);
        int frame=b.WorldState.Controller.Frame;b.Ground.Store(IronGolemWorld.Park(b.WorldState.Controller));Through(b,frame+1);
        Require(b.WorldState.Controller.Mode==4,"Mage buff source skill was not selected");
    }
    private static void Check()
    {
        var p=Owner();var ice=Pet(p,1);var fire=Pet(p,2,3,1);var ally=Pet(p,3,2,1);NecroSnapshotChecks.MissingMetadata(ice);NecroSnapshotChecks.MissingMetadata(fire);NecroSnapshotChecks.MissingMetadata(ally);var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+7,p.Y+1,p.Z),p.Map);
        int defense=ClassStats.Defense(ice),life=SkillHealth.Balance256(ice);var helm=PoisonDaggerChecks.Named(p,"Wormskull");
        Require(SkillMechanics.Rank(p,CombatAbility.RaiseSkeletalMage)==20&&MagePetBuffs.HardRank(ice)==19&&SorceressEffects.Armor(ice)==null,"Soft point unlocked intrinsic armor");
        Train(p,"necromancer.skeletalmage",20);var armor=SorceressEffects.Armor(ice);var values=MagePetBuffs.Values(CombatAbility.FrozenArmor,20,ice.Level);
        Require(ice.Rank==19&&armor is{Ability:CombatAbility.FrozenArmor,UntilUtc:var until}&&until==DateTime.MaxValue
            &&armor.Defense==values.Eval("aurastatcalc1")&&armor.FreezeFrames==values.Eval("calc1")
            &&ClassStats.Defense(ice)==ClassStats.ScaleDefense(defense,armor.Defense)&&SkillHealth.Balance256(ice)==life,"Existing Mage did not gain exact intrinsic armor/defense without recapture/heal");
        p.Backpack.DropItem(helm);ClassGear.Refresh(p);
        int mana=NecromancerEffects.ManaBalance256(p);Require(SorceressEffects.Enchantment(ally)==null,"Mage enchanted without combat");ForceSkill(fire);
        var enchant=SorceressEffects.Enchantment(ally);var e=MagePetBuffs.Values(CombatAbility.Enchant,20,fire.Level);
        Require(enchant is{Rank:20}&&enchant.Caster==fire.Serial.Value&&enchant.Min256==e.Damage256(false)&&enchant.Max256==e.Damage256(true)
            &&enchant.AttackPercent==SkillMechanics.For(CombatAbility.Enchant).Number("ToHit")+19*SkillMechanics.For(CombatAbility.Enchant).Number("LevToHit")
            &&enchant.UntilUtc>TimedEffects.Now&&enchant.UntilUtc<=TimedEffects.Now.AddMilliseconds(e.Eval("auralencalc")*40+20)
            &&SorceressEffects.Enchantment(p)!=null&&SorceressEffects.Enchantment(fire)!=null&&SorceressEffects.Enchantment(target)==null
            &&NecromancerEffects.ManaBalance256(p)==mana,"Automatic Enchant context/allegiance/mana/strength/duration");
        // New allies do not trigger a continuous buff scan in the same fight.
        var late=Pet(p,3,2,-1);Require(SorceressEffects.Enchantment(late)==null,"New ally pre-enchanted");ForceSkill(fire);
        Require(SorceressEffects.Enchantment(late)==null&&SorceressEffects.Enchantment(ally)==enchant,"Continuous combat Enchant scan/refill");
        target.Delete();ClassCombat.For(p).Target=Serial.Zero;fire.ObserveCombatTarget(false);
        target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+7,p.Y+1,p.Z),p.Map);ForceSkill(fire);
        Require(SorceressEffects.Enchantment(late)?.Rank==20&&SorceressEffects.Enchantment(ally)==enchant,"New combat did not enchant fresh ally or refreshed existing enchant");late.Delete();
        var enemy=new GraveyardZombie{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.MoveToWorld(new(ice.X+1,ice.Y,ice.Z),p.Map);enemy.AIObject.AITimer.Stop();enemy.SetResistance(ResistanceType.Cold,0);
        try
        {
            NecromancerEffects.WeaponHit(enemy,ice,0,false,false,false);Require(!FreezeStates.Shattered(enemy),"Zero-damage hit froze attacker");
            NecromancerEffects.WeaponHit(enemy,ice,1,true,false,false);Require(!FreezeStates.Shattered(enemy),"Ranged hit froze attacker");
            NecromancerEffects.WeaponHit(enemy,ice,1,false,false,false);Require(FreezeStates.Shattered(enemy)&&TimedEffects.TryActor(enemy,out var frozen)&&frozen.Control.Freeze.Source==ice.Serial.Value,"Intrinsic armor did not use shared freeze source/state");
        }
        finally{enemy.Delete();}
        // Respec updates the already-existing actor. No timer refresh or heal.
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
        Require(!ice.Deleted&&SorceressEffects.Armor(ice)==null&&ClassStats.Defense(ice)==defense&&SkillHealth.Balance256(ice)==life,"Respec retained intrinsic armor or changed baseline/wounds");
        Train(p,"necromancer.skeletalmage",20);Require(SorceressEffects.Armor(ice)?.Defense==armor.Defense,"Relearn did not restore intrinsic armor");
        SkillHealth.SetBalance256(ice,SkillHealth.Maximum256(ice)-257);TimedEffects.PreventHealing(ice,TimedEffects.Now);target.Delete();
        foreach(var pet in new[]{ice,fire,ally}){pet.Ground.Store(IronGolemWorld.Park(pet.WorldState.Controller) with{NextUtc=TimedEffects.Now.AddHours(1)});}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,ice.Serial.Value,fire.Serial.Value,ally.Serial.Value,SkillHealth.Balance256(ice),SorceressEffects.Enchantment(ally),0)));
        Log.Information("Mage buffs:hard19+real soft1 refuses;live hard20 unlock/respec/relearn,exact intrinsic armor/defense/shared freeze,actual AI skill combat-start NPC Enchant,no mana/continuous scan/replacement;fixed Sorc context excludes owner talents/mastery/potion");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var ice=(MageWorldBody)World.FindMobile((Serial)s.Ice);var fire=(MageWorldBody)World.FindMobile((Serial)s.Fire);var ally=(MageWorldBody)World.FindMobile((Serial)s.Ally);
        Require(ice.ControlMaster==p&&fire.ControlMaster==p&&ally.ControlMaster==p&&ice.Rank==19&&MagePetBuffs.HardRank(ice)==20&&SkillHealth.Balance256(ice)==s.Life
            &&SorceressEffects.Armor(ice)?.UntilUtc==DateTime.MaxValue,"Mage buff reload changed ownership/captured rank/hard unlock/wounds/intrinsic armor");
        // The combined input cohort can outlast Enchant's real ten-minute
        // duration. Preserve the exact saved payload/deadline, then require
        // its ordinary consumer to stop applying it after that deadline.
        // Do not extend or refresh a temporary buff to suit the fixture.
        Require(s.Enchant!=null&&SorceressEffects.Profiles.Find(ally)?.Enchantment==s.Enchant,
            "Mage buff reload changed saved Enchant caster/rank/damage/AR/absolute deadline");
        bool expired=s.Enchant.UntilUtc<=TimedEffects.Now;
        Require(SorceressEffects.Enchantment(ally)==(expired?null:s.Enchant),"Mage buff reload ignored absolute Enchant expiry");
        Log.Information("Mage buff reload retains original Enchant payload/deadline;expired={Expired}",expired);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
