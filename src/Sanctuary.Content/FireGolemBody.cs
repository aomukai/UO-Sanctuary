// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server.Mobiles;
namespace Sanctuary;

// New learned Fire Golems opt into exact resources; historical Minion saves
// keep their existing type until an explicit migration is designed.
[SerializationGenerator(0)]
public partial class FireGolemBody:NecromancerMinion,ISourceLifeMaximum
{
    private FireBirthStats _fireBirth;
    private FireBirthStats Birth=>_fireBirth??=NecromancerFireBirth.Prepare(CapturedValues,Level,SummonDifficulty);
    internal override string SourceMonsterId=>Kind==CombatAbility.FireGolem?"firegolem":base.SourceMonsterId;
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
            var b=Birth;return profile with{Level=b.Level,Life=(Math.Clamp(b.Life256,256,65000*256)+255)/256,Defense=b.Armor,AttackRating=b.Attack};
        }
    }
    [Constructible]public FireGolemBody(){}
    public override void Prepare(PlayerMobile owner,CombatAbility ability)
    {
        if(ability!=CombatAbility.FireGolem){throw new ArgumentException("Fire Golem body requires Fire Golem");}
        NecromancerFireBirth.Require(owner);_fireBirth=null;base.Prepare(owner,ability);SkillHealth.SetBalance256(this,SkillHealth.Maximum256(this));
    }
    internal override void RefreshDifficulty(){_fireBirth=null;base.RefreshDifficulty();}
    [AfterDeserialization]private void AfterFireLoad(){_fireBirth=null;ResetLifeRecovery();}
}
