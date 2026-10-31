// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server.Mobiles;
namespace Sanctuary;

// New learned Clay Golems opt into exact resources; historical Minion saves
// keep their existing type until an explicit migration is designed.
[SerializationGenerator(0)]
public partial class ClayGolemBody:NecromancerMinion,ISourceLifeMaximum,ISourceUnarmedNpc
{
    private ClayBirthStats _clayBirth;
    private ClayBirthStats Birth=>_clayBirth??=NecromancerClayBirth.Prepare(CapturedValues,Level,SummonDifficulty);
    internal override string SourceMonsterId=>Kind==CombatAbility.ClayGolem?"claygolem":base.SourceMonsterId;
    NpcPhysicalStats ISourceUnarmedNpc.UnarmedPhysical=>UnarmedPhysical;
    internal NpcPhysicalStats UnarmedPhysical=>Kind==CombatAbility.ClayGolem?new(
        NecromancerSummons.Source("claygolem","A1MinD",SummonDifficulty),NecromancerSummons.Source("claygolem","A1MaxD",SummonDifficulty),0,
        checked(Birth.DamagePercent+CombatAuras.DamagePercent(this)+NecromancerEffects.DamageStatPercent(this)),0,0,false)
        :throw new InvalidOperationException("Unprepared Clay Golem unarmed packet");
    internal (int Action,int End) SourceAttackClock=>DruidWolfGeometry.Clock("A1",
        SourceAttackRate.Speed(NecromancerSummons.Source("claygolem-animation","A1Speed"),0,SourceAttackRate.WolfRate(this)),"claygolem-animation");
    protected override (int Cycle,int Action) MeleeClock()
    {var clock=SourceAttackClock;return(checked(clock.End*40),checked(clock.Action*40));}
    protected override bool CheckAttackHit(Server.Mobile target)=>ClassStats.CheckHit(this,target,0,()=>NpcEquipmentRandom.Limited(this,100)/100.0)==true;
    internal int ReactiveSlowPercent=>Kind==CombatAbility.ClayGolem?Birth.SlowPercent:0;
    public override long SourceLife256=>Kind==CombatAbility.ClayGolem?Birth.Life256:base.SourceLife256;
    int ISourceLifeMaximum.MaximumLife256=>NpcEquipmentResources.Maximum256(this,0)??NpcEquipmentResources.UnboundLifeMaximum256(this);
    internal override int SourceRegeneration256
    {
        get
        {
            if(Kind!=CombatAbility.ClayGolem){return base.SourceRegeneration256;}
            int maximum=Math.Clamp(Birth.Life256,256,65000*256);
            return BattleOrders.RegenerationAfter(Birth.Life256,maximum,maximum,NecromancerSummons.Source("claygolem","DamageRegen"),Birth.Regeneration256);
        }
    }
    public override MonsterProfile CombatProfile
    {
        get
        {
            var profile=base.CombatProfile;
            if(Kind!=CombatAbility.ClayGolem){return profile;}
            var b=Birth;return profile with{Level=b.Level,Life=(Math.Clamp(b.Life256,256,65000*256)+255)/256,Defense=b.Armor,AttackRating=b.Attack,AttackMs=DruidWolfGeometry.Clock("A1",source:"claygolem-animation").End*40};
        }
    }
    [Constructible]public ClayGolemBody(){}
    public override void Prepare(PlayerMobile owner,CombatAbility ability)
    {
        if(ability!=CombatAbility.ClayGolem){throw new ArgumentException("Clay Golem body requires Clay Golem");}
        NecromancerClayBirth.Require(owner);_clayBirth=null;base.Prepare(owner,ability);SkillHealth.SetBalance256(this,SkillHealth.Maximum256(this));
    }
    internal override void RefreshDifficulty(){_clayBirth=null;base.RefreshDifficulty();}
    public override void OnAfterDelete(){NpcEquipmentRandom.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterClayLoad(){_clayBirth=null;ResetLifeRecovery();}
}
