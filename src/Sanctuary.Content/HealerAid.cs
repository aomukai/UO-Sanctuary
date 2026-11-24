// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal static class HealerAid
{
    private const int Range = 4;
    private static readonly TimeSpan _cooldown = TimeSpan.FromSeconds(15);

    public static void Initialize() => ClassMapHooks.LocationChanged += OnApproach;

    private static void OnApproach(Mobile mobile)
    {
        if (mobile is not PlayerMobile { Alive: true, Deleted: false, Map: { } map } player || map == Map.Internal
            || !player.CanBeginAction(typeof(HealerAid)) || SkillHealth.Full(player))
            return;

        foreach (var healer in map.GetMobilesInRange<BaseHealer>(player.Location, Range))
        {
            if (!healer.Alive || healer.Deleted || healer.Controlled || healer.Summoned
                || Math.Abs(healer.Z - player.Z) >= 16 || !player.CanSee(healer)
                || !healer.CanSee(player) || !healer.InLOS(player) || !healer.CanBeBeneficial(player, false))
                continue;

            if (SkillHealth.Heal(player, SkillHealth.Maximum256(player) - SkillHealth.Balance256(player)) > 0)
            {
                player.BeginAction(typeof(HealerAid));
                Timer.StartTimer(_cooldown, () => player.EndAction(typeof(HealerAid)));
                healer.PublicOverheadMessage(MessageType.Regular, 0x3B2, false, "Let me tend thy wounds.");
                player.PlaySound(0x1F2);
                player.FixedEffect(0x376A, 9, 32);
            }
            return;
        }
    }
}
