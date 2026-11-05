// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved direct affix, not a boss modifier inherited by encounter minions.
// New learned casts freeze upgrade eligibility; missing historical metadata
// retains the explicit legacy rule. Item recipes remain independently captured.
internal static class IronGolemUpgrades
{
    internal static int HardRank(BaseCreature actor)=>actor is SkeletonGuardian
        &&(actor is IronGolemBody||actor is NecromancerMinion{Kind:CombatAbility.IronGolem})
        &&actor.ControlMaster is PlayerMobile{Deleted:false} owner
        ?SummonUpgradeSnapshots.HardRank(actor,CombatAbility.IronGolem):0;
    private static (int DamagePercent,int AttackPercent) Strength(BaseCreature actor)=>HardRank(actor)>=10
        ?EncounterProfiles.ExtraStrong(((SkeletonGuardian)actor).SummonDifficulty,false):default;
    internal static int DamagePercent(BaseCreature actor)=>Strength(actor).DamagePercent;
    internal static int AttackPercent(BaseCreature actor)=>Strength(actor).AttackPercent;
    internal static int Resistance(BaseCreature actor,ResistanceType type,Func<ResistanceType,int> original)
    {
        if(HardRank(actor)<20){return original(type);}
        var basis=new CreatureResistances(original(ResistanceType.Physical),NamedElements.MagicResistance(actor),
            original(ResistanceType.Fire),original(ResistanceType.Energy),original(ResistanceType.Cold),original(ResistanceType.Poison));
        var values=EncounterProfiles.Resistances(basis,null,[EliteModifier.SpectralHit]);
        return type switch{ResistanceType.Physical=>values[0],ResistanceType.Fire=>values[2],ResistanceType.Energy=>values[3],
            ResistanceType.Cold=>values[4],ResistanceType.Poison=>values[5],_=>original(type)};
    }
}
