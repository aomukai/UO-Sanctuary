// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class NecromancerCurseVisuals
{
    private readonly record struct Lease(ushort Graphic, uint Source, DateTime Until, DateTime Refresh);
    private static readonly Dictionary<Serial, Lease> Marks = new();
    private static readonly List<Serial> Keys = new();

    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(150),
        TimeSpan.FromMilliseconds(150), Tick);

    internal static void Cast(Mobile owner)
    {
        Effects.SendLocationEffect(owner.Location, owner.Map, SpellEffectGraphics.CurseCast, SpellEffectGraphics.CurseBurstDuration);
        owner.PlaySound(SpellEffectSounds.CurseCast);
    }

    internal static void LifeTapHeal(Mobile actor) =>
        Burst(actor, SpellEffectGraphics.LifeTapHeal, SpellEffectSounds.LifeTapHeal);

    internal static void IronMaidenReturn(Mobile actor) =>
        Burst(actor, SpellEffectGraphics.IronMaidenReturn, SpellEffectSounds.IronMaidenReturn);

    private static void Burst(Mobile actor, ushort graphic, int sound)
    {
        actor.FixedEffect(graphic, 10, SpellEffectGraphics.CurseBurstDuration);
        actor.PlaySound(sound);
    }

    private static void Tick()
    {
        Keys.Clear();
        Keys.AddRange(Marks.Keys);
        foreach (var serial in Keys)
        {
            if (World.FindMobile(serial) is not { Alive: true, Deleted: false } actor)
            {
                Marks.Remove(serial);
                continue;
            }

            var state = NecromancerEffects.Profiles.Find(actor);
            Track(actor, state?.Curse ?? state?.EliteCurse);
        }
    }

    internal static void Track(Mobile actor, NecromancerCurse curse)
    {
        var now = TimedEffects.Now;
        ushort graphic = curse == null || curse.UntilUtc <= now ? (ushort)0 : Graphic(curse);
        var lease = Marks.GetValueOrDefault(actor.Serial);
        bool visible = !World.Loading && actor is { Alive: true, Deleted: false, Map: { } map } && map != Map.Internal;
        if (lease.Graphic != 0 && lease.Graphic != graphic && visible)
        {
            actor.FixedEffect(lease.Graphic, 10, 0);
            if (graphic == 0)
            {
                bool cloak = lease.Graphic is SpellEffectGraphics.CloakBlind or SpellEffectGraphics.CloakExposure;
                actor.FixedEffect(cloak ? SpellEffectGraphics.CloakEnd : SpellEffectGraphics.CurseFade, 10, SpellEffectGraphics.CurseBurstDuration);
                SpellAreaSounds.Pulse(lease.Source, cloak ? SpellEffectSounds.CloakEnd : SpellEffectSounds.CurseFade,
                    actor.Location, actor.Map, now, 300);
            }
        }

        if (graphic == 0)
        {
            Marks.Remove(actor.Serial);
            return;
        }

        if (!visible)
        {
            Marks[actor.Serial] = new Lease(graphic, curse.Source, curse.UntilUtc, default);
            return;
        }

        if (lease.Graphic == graphic && lease.Until == curse.UntilUtc && lease.Refresh > now) { return; }
        double remaining = (curse.UntilUtc - now).TotalMilliseconds;
        actor.FixedEffect(graphic, 10, (int)Math.Ceiling(Math.Min(remaining, 650) / 50));
        Marks[actor.Serial] = new Lease(graphic, curse.Source, curse.UntilUtc, now.AddMilliseconds(Math.Min(remaining, 350)));
    }

    private static ushort Graphic(NecromancerCurse curse) => curse.Ability switch
    {
        CombatAbility.AmplifyDamage => SpellEffectGraphics.CurseAmplify,
        CombatAbility.Weaken => SpellEffectGraphics.CurseWeaken,
        CombatAbility.Decrepify => SpellEffectGraphics.CurseDecrepify,
        CombatAbility.LowerResist => SpellEffectGraphics.CurseLowerResist,
        CombatAbility.IronMaiden => SpellEffectGraphics.CurseIronMaiden,
        CombatAbility.LifeTap => SpellEffectGraphics.CurseLifeTap,
        CombatAbility.DimVision => SpellEffectGraphics.CurseDimVision,
        CombatAbility.Terror when curse.Terror?.Skill == CombatAbility.Howl => SpellEffectGraphics.HowlMark,
        CombatAbility.Terror => SpellEffectGraphics.CurseTerror,
        CombatAbility.Confuse => SpellEffectGraphics.CurseConfuse,
        CombatAbility.Attract => SpellEffectGraphics.CurseAttract,
        CombatAbility.Taunt => SpellEffectGraphics.TauntMark,
        CombatAbility.BattleCry => SpellEffectGraphics.BattleCryMark,
        CombatAbility.GrimWard => SpellEffectGraphics.GrimWardMark,
        CombatAbility.CloakOfShadows => curse.CloakBlind ? SpellEffectGraphics.CloakBlind : SpellEffectGraphics.CloakExposure,
        _ => 0
    };
}
