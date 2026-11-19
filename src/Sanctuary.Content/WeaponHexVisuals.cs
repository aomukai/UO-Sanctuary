// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class WeaponHexVisuals
{
    private readonly record struct Art(ushort Cast, ushort Coat, ushort Mark, ushort Fade, int CastCue, int MarkCue);
    private readonly record struct Key(Serial Actor, CombatAbility Ability, bool Weapon);
    private readonly record struct Lease(Map Map, int Layer, DateTime Until, DateTime Renew);
    private sealed class ContactClock { internal DateTime Next; }
    private static readonly Dictionary<Key, Lease> Active = new();
    private static readonly ConditionalWeakTable<PlayerMobile, ContactClock> Contacts = new();
    private static DateTime _next;

    private static Art Style(CombatAbility ability) => ability switch
    {
        CombatAbility.HexBane => new(SpellEffectGraphics.HexBaneCast, SpellEffectGraphics.HexBaneCoat,
            SpellEffectGraphics.HexBaneMark, SpellEffectGraphics.HexBaneFade, SpellEffectSounds.HexBaneCast, SpellEffectSounds.HexBaneMark),
        CombatAbility.HexPurge => new(SpellEffectGraphics.HexPurgeCast, SpellEffectGraphics.HexPurgeCoat,
            SpellEffectGraphics.HexPurgeMark, SpellEffectGraphics.HexPurgeFade, SpellEffectSounds.HexPurgeCast, SpellEffectSounds.HexPurgeMark),
        CombatAbility.HexSiphon => new(SpellEffectGraphics.HexSiphonCast, SpellEffectGraphics.HexSiphonCoat,
            SpellEffectGraphics.HexSiphonMark, SpellEffectGraphics.HexSiphonFade, SpellEffectSounds.HexSiphonCast, SpellEffectSounds.HexSiphonMark),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    private static bool Visible(Mobile actor) => PaladinAuraVisuals.InWorld(actor)
        && actor is { Alive: true, Hidden: false, IsDeadBondedPet: false };

    private static bool Renew(Mobile actor, CombatAbility ability, bool weapon, DateTime until, DateTime at)
    {
        if (!Visible(actor) || until <= at) { return false; }
        int layer = 10;
        if (weapon)
        {
            if (actor is not PlayerMobile owner || !ClassCreation.Profiles.TryGet(owner.Serial, out var profile)
                || profile.Class != DiabloClass.Warlock || !WeaponHexes.Equipped(owner))
            { return false; }
            layer = (int)CombatTiming.Equipped(owner).Layer;
        }
        var key = new Key(actor.Serial, ability, weapon);
        if (Active.TryGetValue(key, out var old) && old.Map == actor.Map && old.Layer == layer && old.Until == until && old.Renew > at)
        { return true; }
        var art = Style(ability);
        int duration = Math.Min(18, (int)Math.Ceiling((until - at).TotalMilliseconds / 50));
        actor.FixedEffect(weapon ? art.Coat : art.Mark, layer, duration);
        Active[key] = new(actor.Map, layer, until, at.AddMilliseconds(460));
        return true;
    }

    internal static void Cast(PlayerMobile owner, CombatAbility ability, DateTime until, DateTime at)
    {
        var art = Style(ability);
        owner.FixedEffect(art.Cast, 10, 8);
        owner.PlaySound(art.CastCue);
        Renew(owner, ability, true, until, at);
    }

    internal static void Mark(BaseCreature target, CombatAbility ability, DateTime until, DateTime at, bool newMark)
    {
        if (Renew(target, ability, false, until, at) && newMark) { target.PlaySound(Style(ability).MarkCue); }
    }

    internal static void Clear(Mobile actor, CombatAbility ability, bool weapon)
    {
        if (actor != null) { Clear(new(actor.Serial, ability, weapon), actor, false); }
    }

    private static void Clear(Key key, Mobile actor, bool expired)
    {
        if (!Active.Remove(key, out var old) || !PaladinAuraVisuals.InWorld(actor) || old.Map != actor.Map) { return; }
        var art = Style(key.Ability);
        actor.FixedEffect(key.Weapon ? art.Coat : art.Mark, old.Layer, 0);
        if (expired && key.Weapon && Visible(actor))
        {
            actor.FixedEffect(art.Fade, 10, 7);
            actor.PlaySound(SpellEffectSounds.WeaponHexFade);
        }
    }

    internal static void Tick(DateTime at)
    {
        if (World.Loading || at < _next) { return; }
        _next = at.AddMilliseconds(220);
        var live = new HashSet<Key>();
        void Refresh(IEnumerable<(uint Serial, DateTime Until)> entries, CombatAbility ability, bool weapon)
        {
            foreach (var (id, until) in entries)
            {
                if (until <= at) { continue; }
                var serial = (Serial)id;
                if (Renew(World.FindMobile(serial), ability, weapon, until, at))
                { live.Add(new(serial, ability, weapon)); }
            }
        }
        Refresh(HexBane.WeaponVisuals, CombatAbility.HexBane, true);
        Refresh(HexBane.TargetVisuals, CombatAbility.HexBane, false);
        Refresh(HexPurge.WeaponVisuals, CombatAbility.HexPurge, true);
        Refresh(HexPurge.TargetVisuals, CombatAbility.HexPurge, false);
        Refresh(HexSiphon.WeaponVisuals, CombatAbility.HexSiphon, true);
        Refresh(HexSiphon.TargetVisuals, CombatAbility.HexSiphon, false);
        foreach (var (key, lease) in Active.ToArray())
        {
            if (!live.Contains(key)) { Clear(key, World.FindMobile(key.Actor), lease.Until <= at); }
        }
    }

    internal static void BaneContact(PlayerMobile owner, BaseCreature target)
    {
        if (!Visible(target)) { return; }
        Effects.SendLocationEffect(target.Location, target.Map, SpellEffectGraphics.HexBaneContact, 7);
        var clock = Contacts.GetValue(owner, static _ => new ContactClock());
        var at = TimedEffects.Now;
        if (at < clock.Next) { return; }
        clock.Next = at.AddMilliseconds(150);
        target.PlaySound(SpellEffectSounds.HexBaneContact);
    }

    internal static void Recover(PlayerMobile owner, bool life, bool mana)
    {
        if (life) { owner.FixedEffect(SpellEffectGraphics.HexSiphonLife, 10, 8); }
        if (mana) { owner.FixedEffect(SpellEffectGraphics.HexSiphonMana, 10, 8); }
        if (life || mana) { owner.PlaySound(SpellEffectSounds.HexSiphonRecover); }
    }
}
