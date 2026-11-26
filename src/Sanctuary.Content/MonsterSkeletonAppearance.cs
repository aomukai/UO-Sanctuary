// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public enum SkeletalMonsterRole { None, Warrior, Mage }

public static class MonsterSkeletonAppearance
{
    public static void Configure() => Mobile.SendingDefaultAppearance += Send;

    private static void Send(Mobile actor, NetState state)
    {
        if (actor is not BaseCreature creature)
            return;

        var appearance = For(creature);
        if (appearance != SkeletonAppearance.None)
            SkeletonAppearancePackets.Send(state, actor.Serial, appearance);
    }

    internal static SkeletonAppearance For(BaseCreature creature)
    {
        if (creature.Controlled || creature.Summoned)
            return SkeletonAppearance.None;

        var role = creature switch
        {
            EncounterCreature encounter => encounter.Dungeon.SkeletalRole,
            SkeletalKnight or BoneKnight => SkeletalMonsterRole.Warrior,
            SkeletalMage or BoneMagi => SkeletalMonsterRole.Mage,
            _ => SkeletalMonsterRole.None
        };
        return role == SkeletalMonsterRole.None ? SkeletonAppearance.None : ForLevel(role, CreatureProfiles.Level(creature));
    }

    private static SkeletonAppearance ForLevel(SkeletalMonsterRole role, int level) => (role, level) switch
    {
        (_, < 10) => SkeletonAppearance.PlainSkeleton,
        (SkeletalMonsterRole.Warrior, >= 50) => SkeletonAppearance.ArmouredWarrior,
        (SkeletalMonsterRole.Warrior, _) => SkeletonAppearance.SwordAndShield,
        (SkeletalMonsterRole.Mage, >= 50) => SkeletonAppearance.MageRobe,
        (SkeletalMonsterRole.Mage, _) => SkeletonAppearance.MageHat,
        _ => SkeletonAppearance.None
    };
}
