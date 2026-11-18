// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class BarbarianWardVisuals
{
    private static readonly Dictionary<(CombatAbility Ability, uint Recipient), DateTime> RefreshAfter = new();

    private static (int Ward, int Fade) Style(CombatAbility ability) => ability switch
    {
        CombatAbility.Shout => (SpellEffectGraphics.ShoutWard, SpellEffectGraphics.ShoutFade),
        CombatAbility.BattleOrders => (SpellEffectGraphics.BattleOrdersWard, SpellEffectGraphics.BattleOrdersFade),
        CombatAbility.BattleCommand => (SpellEffectGraphics.BattleCommandWard, SpellEffectGraphics.BattleCommandFade),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Apply(PlayerMobile source, Mobile recipient, CombatAbility ability, DateTime until, DateTime previous, DateTime at)
    {
        var now = TimedEffects.Now;
        if (until <= now || !PaladinAuraVisuals.InWorld(recipient)) { return; }
        Refresh(recipient, ability, until, now);
        if (recipient != source && !recipient.Hidden && previous <= at)
        { BarbarianCryVisuals.Contact(source, ability, recipient.Location, recipient.Map, at); }
    }

    internal static void Refresh(Mobile recipient, CombatAbility ability, DateTime until, DateTime now)
    {
        var key = (ability, recipient.Serial.Value);
        if (RefreshAfter.GetValueOrDefault(key) > now) { return; }
        var end = now.AddMilliseconds(1900);
        if (PaladinAuraVisuals.Attach(recipient, Style(ability).Ward, until < end ? until : end, now))
        { RefreshAfter[key] = now.AddMilliseconds(950); }
    }

    internal static void Clear(uint serial, Mobile recipient, CombatAbility ability)
    {
        if (!RefreshAfter.Remove((ability, serial)) || !PaladinAuraVisuals.InWorld(recipient)) { return; }
        var style = Style(ability);
        PaladinAuraVisuals.Clear(recipient, style.Ward);
        if (!recipient.Alive || recipient.IsDeadBondedPet) { return; }
        recipient.FixedEffect(style.Fade, 10, 8);
        if (!recipient.Hidden)
        {
            SpellAreaSounds.Pulse(serial, SpellEffectSounds.BarbarianWardFade, recipient.Location, recipient.Map,
                TimedEffects.Now, 300);
        }
    }
}
