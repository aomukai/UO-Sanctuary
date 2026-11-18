// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class CorpseSearchVisuals
{
    internal static void Search(PlayerMobile owner, Corpse corpse, CombatAbility ability)
    {
        bool potion = ability == CombatAbility.FindPotion;
        owner.FixedEffect(potion ? SpellEffectGraphics.FindPotionCast : SpellEffectGraphics.FindItemCast, 10, 8);
        owner.PlaySound(potion ? SpellEffectSounds.FindPotionCast : SpellEffectSounds.FindItemCast);
        Effects.SendLocationEffect(corpse, potion ? SpellEffectGraphics.FindPotionSearch : SpellEffectGraphics.FindItemSearch, 6);
    }

    internal static void Result(Corpse corpse, CombatAbility ability, bool discovered)
    {
        bool potion = ability == CombatAbility.FindPotion;
        int graphic = (potion, discovered) switch
        {
            (true, true) => SpellEffectGraphics.FindPotionYield,
            (true, false) => SpellEffectGraphics.FindPotionEmpty,
            (false, true) => SpellEffectGraphics.FindItemYield,
            _ => SpellEffectGraphics.FindItemEmpty
        };
        int cue = !discovered ? SpellEffectSounds.CorpseSearchEmpty
            : potion ? SpellEffectSounds.FindPotionYield : SpellEffectSounds.FindItemYield;
        var point = corpse.Location;
        var map = corpse.Map;
        var delay = TimeSpan.FromMilliseconds(230);
        var showAt = TimedEffects.Now + delay;
        Timer.StartTimer(delay, ShowResult);

        void ShowResult()
        {
            var remaining = showAt - TimedEffects.Now;
            if (remaining > TimeSpan.Zero)
            {
                // A timer-wheel bucket can wake before the visual deadline.
                Timer.StartTimer(remaining < TimeSpan.FromMilliseconds(40) ? TimeSpan.FromMilliseconds(40) : remaining,
                    ShowResult);
                return;
            }
            Effects.SendLocationEffect(point, map, graphic, 8);
            Effects.PlaySound(point, map, cue);
        }
    }
}
