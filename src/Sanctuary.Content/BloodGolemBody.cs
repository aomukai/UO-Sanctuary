// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server.Mobiles;
namespace Sanctuary;

// New learned Blood Golems opt into exact resources; historical Minion saves
// keep their existing type until an explicit migration is designed.
[SerializationGenerator(0)]
public partial class BloodGolemBody:NecromancerMinion,ISourceLifeMaximum
{
    private BloodBirthStats _bloodBirth;
    private BloodBirthStats Birth=>_bloodBirth??=NecromancerBloodBirth.Prepare(CapturedValues,Level,SummonDifficulty);
    internal override string SourceMonsterId=>Kind==CombatAbility.BloodGolem?"bloodgolem":base.SourceMonsterId;
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
            var b=Birth;return profile with{Level=b.Level,Life=(Math.Clamp(b.Life256,256,65000*256)+255)/256,Defense=b.Armor,AttackRating=b.Attack};
        }
    }
    [Constructible]public BloodGolemBody(){}
    public override void Prepare(PlayerMobile owner,CombatAbility ability)
    {
        if(ability!=CombatAbility.BloodGolem){throw new ArgumentException("Blood Golem body requires Blood Golem");}
        NecromancerBloodBirth.Require(owner);_bloodBirth=null;base.Prepare(owner,ability);SkillHealth.SetBalance256(this,SkillHealth.Maximum256(this));
    }
    internal override void RefreshDifficulty(){_bloodBirth=null;base.RefreshDifficulty();}
    [AfterDeserialization]private void AfterBloodLoad(){_bloodBirth=null;ResetLifeRecovery();}
}
