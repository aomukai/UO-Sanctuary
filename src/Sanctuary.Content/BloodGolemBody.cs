// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server.Mobiles;
namespace Sanctuary;

// New learned Blood Golems opt into exact resources; historical Minion saves
// keep their existing type until an explicit migration is designed.
[SerializationGenerator(0)]
public partial class BloodGolemBody:NecromancerMinion,ISourceLifeMaximum,ISourceUnarmedNpc
{
    private BloodBirthStats _bloodBirth;
    private BloodBirthStats Birth=>_bloodBirth??=NecromancerBloodBirth.Prepare(CapturedValues,Level,SummonDifficulty);
    internal override string SourceMonsterId=>Kind==CombatAbility.BloodGolem?"bloodgolem":base.SourceMonsterId;
    NpcPhysicalStats ISourceUnarmedNpc.UnarmedPhysical=>UnarmedPhysical;
    internal NpcPhysicalStats UnarmedPhysical=>Kind==CombatAbility.BloodGolem?new(NecromancerSummons.Source("bloodgolem","A1MinD",SummonDifficulty),NecromancerSummons.Source("bloodgolem","A1MaxD",SummonDifficulty),0,
        checked(Birth.DamagePercent+CombatAuras.DamagePercent(this)+NecromancerEffects.DamageStatPercent(this)),0,0,false):throw new InvalidOperationException("Unprepared Blood Golem packet");
    internal (int Action,int End) SourceAttackClock=>DruidWolfGeometry.Clock("A1",
        SourceAttackRate.Speed(NecromancerSummons.Source("bloodgolem-animation","A1Speed"),0,SourceAttackRate.WolfRate(this)),"bloodgolem-animation");
    protected override (int Cycle,int Action) MeleeClock(){var clock=SourceAttackClock;return(checked(clock.End*40),checked(clock.Action*40));}
    protected override bool CheckAttackHit(Server.Mobile target)=>ClassStats.CheckHit(this,target,0,()=>NpcEquipmentRandom.Limited(this,100)/100.0)==true;
    public override long SourceLife256=>Kind==CombatAbility.BloodGolem?Birth.Life256:base.SourceLife256;
    int ISourceLifeMaximum.MaximumLife256=>NpcEquipmentResources.Maximum256(this,0)??NpcEquipmentResources.UnboundLifeMaximum256(this);
    internal override int SourceRegeneration256
    {
        get
        {
            if(Kind!=CombatAbility.BloodGolem){return base.SourceRegeneration256;}
            int maximum=Math.Clamp(Birth.Life256,256,65000*256);
            return BattleOrders.RegenerationAfter(Birth.Life256,maximum,maximum,NecromancerSummons.Source("bloodgolem","DamageRegen"),Birth.Regeneration256);
        }
    }
    public override MonsterProfile CombatProfile
    {
        get
        {
            var profile=base.CombatProfile;
            if(Kind!=CombatAbility.BloodGolem){return profile;}
            var b=Birth;return profile with{Level=b.Level,Life=(Math.Clamp(b.Life256,256,65000*256)+255)/256,Defense=b.Armor,AttackRating=b.Attack,AttackMs=DruidWolfGeometry.Clock("A1",source:"bloodgolem-animation").End*40};
        }
    }
    [Constructible]public BloodGolemBody(){}
    public override void Prepare(PlayerMobile owner,CombatAbility ability)
    {
        if(ability!=CombatAbility.BloodGolem){throw new ArgumentException("Blood Golem body requires Blood Golem");}
        NecromancerBloodBirth.Require(owner);_bloodBirth=null;base.Prepare(owner,ability);SkillHealth.SetBalance256(this,SkillHealth.Maximum256(this));
    }
    internal override void RefreshDifficulty(){_bloodBirth=null;base.RefreshDifficulty();}
    public override void OnAfterDelete(){NpcEquipmentRandom.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterBloodLoad(){_bloodBirth=null;ResetLifeRecovery();}
}
