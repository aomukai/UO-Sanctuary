// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class DruidFormEffects
{
    private readonly record struct Lease(ushort Graphic, DateTime Refresh);
    private static readonly Dictionary<(uint Owner, CombatAbility Ability), Lease> Charges = new();

    private static bool Visible(PlayerMobile owner) =>
        !World.Loading && owner is { Alive: true, Deleted: false, Map: { } map } && map != Map.Internal;

    internal static void Transform(PlayerMobile owner, CombatAbility form, bool entering)
    {
        if (!Visible(owner)) { return; }
        var (graphic, sound) = (form, entering) switch
        {
            (CombatAbility.Werewolf, true) => (SpellEffectGraphics.DruidWolfEnter, SpellEffectSounds.DruidWolfEnter),
            (CombatAbility.Werewolf, false) => (SpellEffectGraphics.DruidWolfLeave, SpellEffectSounds.DruidWolfLeave),
            (CombatAbility.Werebear, true) => (SpellEffectGraphics.DruidBearEnter, SpellEffectSounds.DruidBearEnter),
            _ => (SpellEffectGraphics.DruidBearLeave, SpellEffectSounds.DruidBearLeave)
        };
        owner.FixedEffect(graphic, 10, 11);
        owner.PlaySound(sound);
    }

    internal static void Swing(PlayerMobile owner, CombatAbility ability) => owner.PlaySound(ability switch
    {
        CombatAbility.Maul => SpellEffectSounds.DruidMaulSwing,
        CombatAbility.FeralRage => SpellEffectSounds.DruidFeralSwing,
        CombatAbility.FireClaws => SpellEffectSounds.DruidFireClawsSwing,
        CombatAbility.Hunger => SpellEffectSounds.DruidHungerSwing,
        CombatAbility.Fury => SpellEffectSounds.DruidFurySwing,
        CombatAbility.Rabies => SpellEffectSounds.RabiesSwing,
        _ => -1
    });

    internal static void Impact(CombatAbility ability, Point3D point, Map map)
    {
        var (graphic, sound, duration) = ability switch
        {
            CombatAbility.Maul => (SpellEffectGraphics.DruidMaulImpact, SpellEffectSounds.DruidMaulImpact, 7),
            CombatAbility.FeralRage => (SpellEffectGraphics.DruidFeralImpact, SpellEffectSounds.DruidFeralImpact, 7),
            CombatAbility.FireClaws => (SpellEffectGraphics.DruidFireClaws, SpellEffectSounds.DruidFireClawsImpact, 8),
            CombatAbility.Hunger => (SpellEffectGraphics.DruidHungerBite, SpellEffectSounds.DruidHungerBite, 7),
            CombatAbility.Fury => (SpellEffectGraphics.DruidFuryImpact, SpellEffectSounds.DruidFuryImpact, 4),
            CombatAbility.Rabies => (SpellEffectGraphics.RabiesBite, SpellEffectSounds.RabiesBite, 8),
            _ => ((ushort)0, -1, 0)
        };
        if (graphic == 0) { return; }
        Effects.SendLocationEffect(point, map, graphic, duration);
        Effects.PlaySound(point, map, sound);
    }

    internal static void HungerRecovery(PlayerMobile owner, int life256, int mana256)
    {
        if (!Visible(owner) || (life256 <= 0 && mana256 <= 0)) { return; }
        if (life256 > 0) { owner.FixedEffect(SpellEffectGraphics.BloodGolemHeal, 10, 10); }
        if (mana256 > 0) { owner.FixedEffect(SpellEffectGraphics.DruidHungerMana, 10, 10); }
        owner.PlaySound(SpellEffectSounds.DruidHungerDrain);
    }

    internal static void RefreshCharge(PlayerMobile owner, DruidAttackCharge charge, DateTime now)
    {
        if (!Visible(owner)) { return; }
        var key = (owner.Serial.Value, charge.Ability);
        ushort first = charge.Ability == CombatAbility.Maul ? SpellEffectGraphics.DruidMaulCharge : SpellEffectGraphics.DruidFeralCharge;
        int tier = Math.Min(charge.Count, 3) - 1;
        ushort graphic = (ushort)(first + tier * SpellEffectGraphics.DruidFormEffectFrames);
        var previous = Charges.GetValueOrDefault(key);
        if (previous.Graphic == graphic && previous.Refresh > now) { return; }
        if (previous.Graphic != 0 && previous.Graphic != graphic) { owner.FixedEffect(previous.Graphic, 10, 0); }
        int remaining = (int)Math.Clamp(Math.Ceiling((charge.UntilUtc - now).TotalMilliseconds), 1, 750);
        owner.FixedEffect(graphic, 10, (remaining + 49) / 50);
        Charges[key] = new Lease(graphic, now.AddMilliseconds(Math.Min(remaining, 450)));
    }

    internal static void ClearCharge(PlayerMobile owner, uint serial, CombatAbility ability)
    {
        if (Charges.Remove((serial, ability), out var lease) && Visible(owner))
        {
            owner.FixedEffect(lease.Graphic, 10, 0);
        }
    }
}
