// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server.Mobiles;
namespace Sanctuary;

// New learned Fire Golems opt into exact resources; historical Minion saves
// keep their existing type until an explicit migration is designed.
[SerializationGenerator(0)]
public partial class FireGolemBody:NecromancerMinion,ISourceLifeMaximum,ISourceUnarmedNpc
{
    private FireBirthStats _fireBirth;
    private FireBirthStats Birth=>_fireBirth??=NecromancerFireBirth.Prepare(CapturedValues,Level,SummonDifficulty);
    internal override string SourceMonsterId=>Kind==CombatAbility.FireGolem?"firegolem":base.SourceMonsterId;
    NpcPhysicalStats ISourceUnarmedNpc.UnarmedPhysical=>UnarmedPhysical;
    internal NpcPhysicalStats UnarmedPhysical=>Kind==CombatAbility.FireGolem?new(
        NecromancerSummons.Source("firegolem","A1MinD",SummonDifficulty),NecromancerSummons.Source("firegolem","A1MaxD",SummonDifficulty),0,
        checked(CombatAuras.DamagePercent(this)+NecromancerEffects.DamageStatPercent(this)),0,0,false)
        :throw new InvalidOperationException("Unprepared Fire Golem physical packet");
    // Captured aurastat3/4 are whole-point fire stats; the ordinary hit filler
    // shifts the accrued range into FP256 once. Holy Fire supplies a separate
    // aura stat list; the optional monstats element remains uncertain.
    internal (int Min,int Max) InnateFire256=>(checked(Birth.FireMinimum*256),checked(Birth.FireMaximum*256));
    internal (int Action,int End) SourceAttackClock=>DruidWolfGeometry.Clock("A1",
        SourceAttackRate.Speed(NecromancerSummons.Source("firegolem-animation","A1Speed"),0,SourceAttackRate.WolfRate(this)),"firegolem-animation");
    protected override (int Cycle,int Action) MeleeClock()
    {var clock=SourceAttackClock;return(checked(clock.End*40),checked(clock.Action*40));}
    protected override bool CheckAttackHit(Server.Mobile target)=>ClassStats.CheckHit(this,target,0,()=>NpcEquipmentRandom.Limited(this,100)/100.0)==true;
    protected override void Strike(Server.Mobile target)
    {
        // Bypass the historical Minion's separate follow-up fire hit: physical
        // and accrued fire damage now share one packet and intrinsic critical.
        if(!NpcEquipmentDamage.TryResolve(this,target,out _)){throw new NotSupportedException("Missing Fire Golem unarmed packet");}
    }
    protected override void RefreshResistances()
    {
        base.RefreshResistances();
        // Preserve native modifier/Conviction consumers while replacing only
        // the captured fire seed. Positive absorption suppresses fire SR.
        if(Kind==CombatAbility.FireGolem){SetResistance(Server.ResistanceType.Fire,Birth.FireResistance);}
    }
    public override long SourceLife256=>Kind==CombatAbility.FireGolem?Birth.Life256:base.SourceLife256;
    int ISourceLifeMaximum.MaximumLife256=>NpcEquipmentResources.Maximum256(this,0)??NpcEquipmentResources.UnboundLifeMaximum256(this);
    internal override int SourceRegeneration256
    {
        get
        {
            if(Kind!=CombatAbility.FireGolem){return base.SourceRegeneration256;}
            int maximum=Math.Clamp(Birth.Life256,256,65000*256);
            return BattleOrders.RegenerationAfter(Birth.Life256,maximum,maximum,NecromancerSummons.Source("firegolem","DamageRegen"),Birth.Regeneration256);
        }
    }
    public override MonsterProfile CombatProfile
    {
        get
        {
            var profile=base.CombatProfile;
            if(Kind!=CombatAbility.FireGolem){return profile;}
            var b=Birth;return profile with{Level=b.Level,Life=(Math.Clamp(b.Life256,256,65000*256)+255)/256,Defense=b.Armor,AttackRating=b.Attack,
                AttackMs=DruidWolfGeometry.Clock("A1",source:"firegolem-animation").End*40};
        }
    }
    [Constructible]public FireGolemBody(){}
    public override void Prepare(PlayerMobile owner,CombatAbility ability)
    {
        if(ability!=CombatAbility.FireGolem){throw new ArgumentException("Fire Golem body requires Fire Golem");}
        NecromancerFireBirth.Require(owner);_fireBirth=null;base.Prepare(owner,ability);SkillHealth.SetBalance256(this,SkillHealth.Maximum256(this));
    }
    internal override void RefreshDifficulty(){_fireBirth=null;base.RefreshDifficulty();}
    public override void OnAfterDelete(){PaladinAuras.Clear(this);NpcEquipmentRandom.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterFireLoad(){_fireBirth=null;ResetLifeRecovery();}
}
