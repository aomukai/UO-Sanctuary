// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server.Mobiles;
namespace Sanctuary;

// New learned Clay Golems opt into exact resources; historical Minion saves
// keep their existing type until an explicit migration is designed.
[SerializationGenerator(0)]
public partial class ClayGolemBody:NecromancerMinion,ISourceLifeMaximum
{
    private ClayBirthStats _clayBirth;
    private ClayBirthStats Birth=>_clayBirth??=NecromancerClayBirth.Prepare(CapturedValues,Level,SummonDifficulty);
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
            var b=Birth;return profile with{Level=b.Level,Life=(Math.Clamp(b.Life256,256,65000*256)+255)/256,Defense=b.Armor,AttackRating=b.Attack};
        }
    }
    [Constructible]public ClayGolemBody(){}
    public override void Prepare(PlayerMobile owner,CombatAbility ability)
    {
        if(ability!=CombatAbility.ClayGolem){throw new ArgumentException("Clay Golem body requires Clay Golem");}
        NecromancerClayBirth.Require(owner);_clayBirth=null;base.Prepare(owner,ability);SkillHealth.SetBalance256(this,SkillHealth.Maximum256(this));
    }
    internal override void RefreshDifficulty(){_clayBirth=null;base.RefreshDifficulty();}
    [AfterDeserialization]private void AfterClayLoad(){_clayBirth=null;ResetLifeRecovery();}
}
