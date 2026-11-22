// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal static class CompanionCharacterStats
{
    internal static int[] Snapshot(BaseCreature companion, IClassCompanion build, int level)
    {
        var weapon = companion.Weapon as BaseWeapon;
        var mastery = companion is Wolfgang ? Wolfgang.SwordMastery(weapon, level) : null;
        var (minimum, maximum, factor) = weapon is null or Fists || ClassGear.Broken(weapon)
            ? (0, 0, 0)
            : CompanionBuild.WeaponDamage(companion, weapon, level, (int)(mastery?.Eval("passivecalc2") ?? 0));
        int attack = CompanionBuild.Bonus(companion, "att%") + (int)(mastery?.Eval("passivecalc1") ?? 0);

        return
        [
            level, companion.Str + CompanionBuild.Bonus(companion, "str"), companion.Dex + CompanionBuild.Bonus(companion, "dex"),
            companion.Hits, companion.HitsMax, minimum * factor / 100, maximum * factor / 100,
            ClassStats.AttackRating(companion, attack), ClassStats.Defense(companion),
            CompanionBuild.AttackSpeedBonus(companion), CompanionBuild.Recovery(companion, build.CombatClass),
            companion.FireResistance, companion.ColdResistance, companion.PoisonResistance, companion.EnergyResistance,
            companion is Wolfgang wolfgang ? wolfgang.BlockChance : 0,
            companion is Merydwen ? AmazonAvoidance.CompanionChance(CombatAbility.Dodge, level) : 0,
            companion is Merydwen ? AmazonAvoidance.CompanionChance(CombatAbility.Avoid, level) : 0,
            companion is Merydwen ? AmazonAvoidance.CompanionChance(CombatAbility.Evade, level) : 0
        ];
    }

    internal static void Send(NetState net, BaseCreature companion, IClassCompanion build, int level)
    {
        var writer = new SpanWriter(stackalloc byte[CompanionStatProtocol.SnapshotLength]);
        writer.Write((byte)0xBF);
        writer.Write((ushort)CompanionStatProtocol.SnapshotLength);
        writer.Write(CompanionStatProtocol.SubCommand);
        writer.Write(CompanionStatProtocol.Version);
        writer.Write(companion.Serial.Value);
        writer.Write((byte)build.EquipmentStyle);
        foreach (int value in Snapshot(companion, build, level))
            writer.Write(value);
        net.Send(writer.Span);
    }
}
