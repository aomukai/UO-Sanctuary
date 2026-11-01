// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Approved Sanctuary summon unlocks. Spell context is the NPC's own fixed
// rank: no owner Sorceress talents, mastery, potion, mana or casting action.
internal static class MagePetBuffs
{
    private static ColdArmorState _armor;
    internal static int HardRank(NecromancerMinion actor)=>actor is{IsMage:true,Deleted:false,ControlMaster:PlayerMobile owner}
        ?ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.skeletalmage"):0;
    internal static SkillValues Values(CombatAbility ability,int rank,int level)
    {
        if(rank is <1 or >255||level is <1 or >99){throw new ArgumentOutOfRangeException();}
        var row=SkillMechanics.For(ability);
        if(row.CharacterClass!=DiabloClass.Sorceress){throw new NotSupportedException("Mage NPC spell requires a Sorceress row");}
        return new(row,key=>string.Equals(key,row.SourceKey,StringComparison.OrdinalIgnoreCase)?rank:0,_=>0,level);
    }
    internal static ColdArmorState Armor(Mobile actor)=>actor is NecromancerMinion{Alive:true,IsMage:true,MageElement:1} mage&&HardRank(mage)>=20
        ?_armor??=SorceressEffects.PrepareColdArmor(CombatAbility.FrozenArmor,Values(CombatAbility.FrozenArmor,20,1),DateTime.MaxValue):null;
    internal static void CombatStart(NecromancerMinion actor,PlayerMobile owner)
    {
        if(actor.MageElement!=2||HardRank(actor)<20||!actor.Alive||actor.Map!=owner.Map||SpellHelper.IsTown(actor.Location,owner)){return;}
        var values=Values(CombatAbility.Enchant,20,actor.Level);
        foreach(var ally in actor.Map.GetMobilesInRange<Mobile>(actor.Location,CombatProtocol.TeethRange))
        {
            if(ally is not{Alive:true,Deleted:false}||Math.Abs(ally.Z-actor.Z)>=16||!actor.InRange(ally,CombatProtocol.TeethRange)
                ||ally!=owner&&!ClassCombat.Friendly(owner,ally)||!actor.InLOS(ally)||SorceressEffects.Enchantment(ally)!=null){continue;}
            SorceressEffects.CastNpcEnchant(actor,ally,values);
        }
    }
}
