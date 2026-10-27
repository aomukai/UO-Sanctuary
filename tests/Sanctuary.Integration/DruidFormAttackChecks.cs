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

public static class DruidFormAttackChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFormAttackChecks));
    private static bool _creating;private static int _scene;
    public static void Configure(){Rates();Register("druid-form-attack-check",Check);Register("druid-form-attack-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var old=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>_creating||p.Name?.StartsWith("DruidFormAttackFixture",StringComparison.Ordinal)==true
            ?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared form A1clock and actual melee attack consumers",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        var definition=SkillMechanics.For(ability);TalentCatalog.TryGet(DiabloClass.Druid,definition.TalentId,out var talent);
        foreach(var prior in talent.Prerequisites)
        {if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        if(ClassCreation.Profiles.GetRank(p.Serial,definition.TalentId)==0)
        {Require(ClassCreation.Profiles.TryTrain(p.Serial,definition.TalentId,out _),"Train "+definition.TalentId);}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="DruidFormAttackFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var ability in new[]{CombatAbility.Werewolf,CombatAbility.Lycanthropy,CombatAbility.Werebear}){Train(p,ability);}
        while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}
        while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        FixtureSpace.Corridor(p,6100,20,2100+32*_scene++);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BladeMissileTarget Target(PlayerMobile p,int dx)
    {
        var t=new BladeMissileTarget{Name="DruidFormAttackFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};
        t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}
        return t;
    }
    private static void Equip(PlayerMobile p,BaseWeapon weapon)
    {
        if(CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}
        if(weapon is SoulGlaive){ClassGear.Profiles.FromMonster(weapon,30,GearRarity.Normal,"glv",GearTier.Normal,new Random(173));}
        else{ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));}p.AddToBackpack(weapon);
        Require(p.EquipItem(weapon),"Equip "+weapon.GetType().Name);ClassGear.Refresh(p);
    }
    private static void Schedule()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-form-attack-clock-source-probe.json")));
        int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var actual=DruidFormAttacks.Schedule(row[0].GetInt32()==2?CombatAbility.Werewolf:CombatAbility.Werebear,row[7].GetInt32());
            var events=row[8];Require(events.GetArrayLength()==2&&events[0][0].GetInt32()==1&&events[0][2].GetInt32()==1
                &&actual.EffectFrame==events[0][1].GetInt32()&&actual.Frames==events[1][1].GetInt32(),"Complete source form A1schedule mismatch");count++;
        }
        Require(count==14400,"Incomplete form A1schedule matrix");
        Require(DruidFormAttacks.HumanFrames("STF")==17&&DruidFormAttacks.HumanFrames("XBW")==20&&DruidFormAttacks.HumanFrames("unknown")==0,"Current human normalization metadata");
        Log.Information("Druid form A1:14400native action/end schedules match complete source scheduler at prepared modern advances; ENDANIM stays distinct from calculator cycles");
    }
    private static void Strike(PlayerMobile p,Mobile target)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),life=target.Hits;
        Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,target,target.Location,Core.TickCount),"Form basic start");
        var session=ClassCombat.For(p);long at=session.EffectAt;
        Require(session.Timing==DruidFormAttacks.Timing(p)&&session.Animation==(byte)WeaponAnimation.Wrestle,"Form clock or identified melee-pose placeholder");
        Require(!ClassCombat.ResolveAction(p,at-1)&&target.Hits==life,"Form attack hit before action frame");
        Require(ClassCombat.ResolveAction(p,at)&&target.Hits<life&&NecromancerEffects.ManaBalance256(p)==mana,"Form melee impact/payment");
        life=target.Hits;Require(!ClassCombat.ResolveAction(p,at)&&target.Hits==life,"Form basic duplicate impact");
    }
    private static void Consumers()
    {
        var p=Fresh();var near=Target(p,1);var far=Target(p,4);var oldRoll=DruidFormAttacks.HitRoll;
        try
        {
            DruidFormAttacks.HitRoll=()=>0;
            foreach(var form in new[]{CombatAbility.Werewolf,CombatAbility.Werebear})
            {
                Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Prepared form");
                foreach(var weapon in new BaseWeapon[]{new Bow(),new Crossbow(),ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive()),new Dagger()})
                {
                    Equip(p,weapon);Require(DruidFormAttacks.Supported(p)&&!NamedCombat.Ranged(p)&&NamedProjectiles.Budget(p)==0,
                        $"Form weapon classification: weapon={weapon.GetType().Name},code={WeaponAnimationClasses.Equipped(p)},supported={DruidFormAttacks.Supported(p)},active={DruidFormAttacks.Active(p)},ranged={NamedCombat.Ranged(p)}");
                    if(weapon is SoulGlaive)
                    {Require(NamedAmmunition.Maximum(weapon)>0,"Throwing quantity control missing");NamedAmmunition.Quantities[weapon.Serial]=new(0,DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc));Require(ClassCombat.Available(p,CombatAbility.BasicAttack),"Empty throwing stack blocked form melee");}
                    Require(CombatTiming.For(p,CombatAbility.BasicAttack)==DruidFormAttacks.Timing(p),"Actual basic timing path");
                    if(weapon is BaseRanged){Require(CombatTiming.Range(p,CombatAbility.BasicAttack)==1,"Bow projectile distance became melee reach");}
                    int quantity=NamedAmmunition.Remaining(weapon);Strike(p,near);
                    Require(NamedAmmunition.Remaining(weapon)==quantity,"Form consumed projectile ammunition");
                    Ready(p);int life=far.Hits;
                    Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,far,far.Location,Core.TickCount),"Out-of-reach swing start");
                    Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&far.Hits==life,"Form ranged hit escaped melee reach");
                }
                Ready(p);int before=near.Hits;
                Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,near,near.Location,Core.TickCount),"Form-changing windup");
                long at=ClassCombat.For(p).EffectAt;DruidPlayerForms.Clear(p);
                Require(!ClassCombat.ResolveAction(p,at)&&near.Hits==before,"Unshifted windup retained form impact");
                Ready(p);Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,near,near.Location,Core.TickCount),"Human windup");
                at=ClassCombat.For(p).EffectAt;Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Shift during basic windup");
                Require(!ClassCombat.ResolveAction(p,at)&&near.Hits==before,"Human windup retained wrong form impact");
                Ready(p);Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,near,near.Location,Core.TickCount),"Moved windup");
                at=ClassCombat.For(p).EffectAt;var origin=p.Location;p.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
                Require(!ClassCombat.ResolveAction(p,at)&&near.Hits==before,"Moved form attack landed");p.MoveToWorld(origin,p.Map);
                DruidPlayerForms.Clear(p);
            }
            Equip(p,new Bow());Require(NamedCombat.Ranged(p),"Human bow classification was changed");
            var human=CombatTiming.For(p,CombatAbility.BasicAttack);Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now),"Final form");
            DruidPlayerForms.Clear(p);Require(CombatTiming.For(p,CombatAbility.BasicAttack)==human,"Human timing failed to restore");
        }
        finally{DruidFormAttacks.HitRoll=oldRoll;ClassCombat.Stop(p);DruidPlayerForms.Clear(p);near.Delete();far.Delete();p.Delete();}
        Log.Information("Druid form actual basic:both forms use mapped bow/crossbow/empty-quantity javelin/dagger melee impacts,once-only action,no ammunition/pierce/ranged reach,and cancel changed-form/movement windups; human bow/timing restore");
    }
    private static void Check(){Schedule();Consumers();Reload();}
    private static void Reload()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-form-world-fixtures.json")));
        var p=World.FindMobile((Serial)doc.RootElement.GetProperty("Owner").GetUInt32()) as PlayerMobile;
        Require(p!=null&&DruidPlayerForms.State(p)?.Form.Ability==CombatAbility.Werebear&&!NamedCombat.Ranged(p)
            &&CombatTiming.For(p,CombatAbility.BasicAttack)==DruidFormAttacks.Timing(p),"Restored bear did not supply live basic clock/classification");
    }
}
