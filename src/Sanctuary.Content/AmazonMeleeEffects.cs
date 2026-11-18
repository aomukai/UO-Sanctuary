// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class AmazonMeleeEffects
{
    internal static void Thrust(PlayerMobile owner, CombatAbility ability, bool basic)
    {
        owner.PlaySound(basic ? 0x238 : ability == CombatAbility.Impale
            ? SpellEffectSounds.HeavySpearThrust : SpellEffectSounds.SpearThrust);
    }

    internal static void ReleaseContact(PlayerMobile owner, BaseCreature target, PlayerWeaponContactPrepared prepared,
        CombatAbility ability, bool basic)
    {
        var point = target.Location;
        var map = target.Map;
        var result = PlayerWeaponContact.Release(owner, target, prepared, TimedEffects.Now, allowNative: true);
        if (basic || result.Commit == null) { return; }

        var (graphic, sound) = ability switch
        {
            CombatAbility.Jab => (SpellEffectGraphics.JabContact, SpellEffectSounds.SpearContact),
            CombatAbility.Fend => (SpellEffectGraphics.FendContact, SpellEffectSounds.SpearContact),
            CombatAbility.Impale => (SpellEffectGraphics.ImpaleContact, SpellEffectSounds.ImpaleContact),
            CombatAbility.PowerStrike => (SpellEffectGraphics.PowerStrikeContact, SpellEffectSounds.LightningContact),
            CombatAbility.ChargedStrike => (SpellEffectGraphics.LightningContact, SpellEffectSounds.SpearContact),
            CombatAbility.LightningStrike => (SpellEffectGraphics.ChainLightningContact, SpellEffectSounds.SpearContact),
            _ => (0, 0)
        };
        if (graphic == 0) { return; }
        Effects.SendLocationEffect(point, map, graphic, SpellEffectGraphics.ThrustEffectFrames);
        Effects.PlaySound(point, map, sound);
    }

    internal static void EmitLightning(PlayerMobile owner, Point3D origin, int sound)
    {
        Effects.SendLocationEffect(origin, owner.Map, SpellEffectGraphics.NovaContact, SpellEffectGraphics.UtilityFrames);
        Effects.PlaySound(origin, owner.Map, sound);
    }
}
