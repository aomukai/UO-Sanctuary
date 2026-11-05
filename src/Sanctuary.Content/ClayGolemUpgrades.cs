// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved Sanctuary replacement: captured summon/mastery damage and born
// hard-point affixes for new learned casts. Other birth resources and existing hit services stay intact.
internal static class ClayGolemUpgrades
{
    internal static int HardRank(Mobile actor)=>actor is NecromancerMinion{Kind:CombatAbility.ClayGolem,ControlMaster:PlayerMobile{Deleted:false} owner}
        ?SummonUpgradeSnapshots.HardRank(actor,CombatAbility.ClayGolem):0;
    internal static (int Min,int Max,int Percent) Weapon(NecromancerMinion actor)=>GolemPetDamage.Weapon(actor);
    internal static int Defense(NecromancerMinion actor,int basis)=>HardRank(actor)>=10
        ?EncounterProfiles.ModifierDefense(basis,EliteModifier.StoneSkin):basis;
    internal static int Resistance(NecromancerMinion actor,ResistanceType type,Func<ResistanceType,int> original)
    {
        int hard=HardRank(actor);if(hard<10){return original(type);}
        var basis=new CreatureResistances(original(ResistanceType.Physical),NamedElements.MagicResistance(actor),
            original(ResistanceType.Fire),original(ResistanceType.Energy),original(ResistanceType.Cold),original(ResistanceType.Poison));
        var values=EncounterProfiles.Resistances(basis,null,hard>=20?[EliteModifier.StoneSkin,EliteModifier.MagicResistant]:[EliteModifier.StoneSkin]);
        return type switch{ResistanceType.Physical=>values[0],ResistanceType.Fire=>values[2],ResistanceType.Energy=>values[3],
            ResistanceType.Cold=>values[4],ResistanceType.Poison=>values[5],_=>original(type)};
    }
}
