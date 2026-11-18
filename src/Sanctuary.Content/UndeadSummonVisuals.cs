// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class UndeadSummonVisuals
{
    private static readonly HashSet<Serial> VisibleBodies = new();

    public static void Initialize()
    {
        var previous = CorpseRecoveryHooks.Created;
        CorpseRecoveryHooks.Created = corpse =>
        {
            if (corpse.Owner is BaseCreature body) { Depart(body, corpse.Map, corpse.Location, death: true); }
            previous?.Invoke(corpse);
        };
        EventSink.WorldSave += () => VisibleBodies.RemoveWhere(id => World.FindMobile(id) is not { Alive: true, Deleted: false });
    }

    private static CombatAbility Kind(BaseCreature body)
    {
        if (ReviveNativeActivation.State(body) != null) { return CombatAbility.Revive; }
        if (body is IronGolemBody { Prepared: { RosterMember: true } }) { return CombatAbility.IronGolem; }
        if (body is NecromancerMinion minion && NecromancerGolems.Ability(minion.Kind)) { return minion.Kind; }
        if (body is SkeletonGuardian skeleton) { return NecromancerSkeletons.Kind(skeleton); }
        return CombatAbility.None;
    }

    internal static void Arrive(BaseCreature body)
    {
        if (World.Loading || body.Map == null || body.Map == Map.Internal || body.ControlMaster is not PlayerMobile) { return; }
        var (graphic, sound) = Kind(body) switch
        {
            CombatAbility.RaiseSkeleton => (SpellEffectGraphics.SkeletonRise, SpellEffectSounds.SkeletonRise),
            CombatAbility.RaiseSkeletalMage => (SpellEffectGraphics.SkeletalMageRise, SpellEffectSounds.SkeletalMageRise),
            CombatAbility.Revive => (SpellEffectGraphics.ReviveRise, SpellEffectSounds.ReviveRise),
            CombatAbility.ClayGolem => (SpellEffectGraphics.ClayGolemRise, SpellEffectSounds.ClayGolemRise),
            CombatAbility.BloodGolem => (SpellEffectGraphics.BloodGolemRise, SpellEffectSounds.BloodGolemRise),
            CombatAbility.IronGolem => (SpellEffectGraphics.IronGolemRise, SpellEffectSounds.IronGolemRise),
            CombatAbility.FireGolem => (SpellEffectGraphics.FireGolemRise, SpellEffectSounds.FireGolemRise),
            _ => ((ushort)0, 0)
        };
        if (graphic == 0 || !VisibleBodies.Add(body.Serial)) { return; }

        Effects.SendLocationEffect(body.Location, body.Map, graphic, SpellEffectGraphics.UndeadRitualDuration);
        Effects.PlaySound(body.Location, body.Map, sound);
        if (body is NecromancerMinion { IsMage: true })
        {
            SorceressWardVisuals.Track(body, SorceressEffects.Profiles.Find(body));
        }
    }

    internal static void Depart(BaseCreature body) => Depart(body, body.Map, body.Location);

    private static void Depart(BaseCreature body, Map map, Point3D point, bool death = false)
    {
        if (!VisibleBodies.Remove(body.Serial) || World.Loading || map == null || map == Map.Internal) { return; }
        if (body is FireGolemDeathBody fire && (death || fire.Retiring)) { return; }
        bool golem = NecromancerGolems.Ability(Kind(body));
        ushort graphic = golem ? SpellEffectGraphics.GolemRubble : SpellEffectGraphics.UndeadFade;
        int sound = golem ? SpellEffectSounds.GolemRubble : SpellEffectSounds.UndeadFade;
        Effects.SendLocationEffect(point, map, graphic, SpellEffectGraphics.UndeadRitualDuration);
        uint group = (body.ControlMaster ?? body).Serial.Value;
        SpellAreaSounds.Pulse(group, sound, point, map, TimedEffects.Now, 190);
    }

    internal static void MeleeContact(BaseCreature body, Map map, Point3D point,
        PreparedMonsterDamagePacket packet = null, SpectralHitRoll spectral = null)
    {
        var kind = Kind(body);
        if (NecromancerGolems.Ability(kind))
        {
            GolemEffects.Contact(body, kind, map, point, packet, spectral);
        }
        else if (kind is CombatAbility.RaiseSkeleton or CombatAbility.Revive)
        {
            Effects.SendLocationEffect(point, map, SpellEffectGraphics.BoneSpearContact, 3);
            SpellAreaSounds.Pulse(body.Serial.Value, SpellEffectSounds.SpearContact, point, map, TimedEffects.Now, 130);
        }
    }
}
