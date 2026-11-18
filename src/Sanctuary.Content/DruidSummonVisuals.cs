// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class DruidSummonVisuals
{
    private static readonly HashSet<Serial> VisibleBodies = new();
    private static readonly Dictionary<Serial, (int Until, int Refresh)> Rage = new();
    private static readonly Dictionary<Serial, DateTime> Blind = new();
    private static readonly List<Serial> BlindTargets = new();

    public static void Initialize()
    {
        var previous = CorpseRecoveryHooks.Created;
        CorpseRecoveryHooks.Created = corpse =>
        {
            if (corpse.Owner is BaseCreature body) { Depart(body, corpse.Map, corpse.Location); }
            previous?.Invoke(corpse);
        };
        Timer.StartTimer(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), RefreshBlind);
    }

    private static bool OnMap(Map map) => !World.Loading && map != null && map != Map.Internal;

    internal static void Arrive(BaseCreature body)
    {
        if (!OnMap(body.Map) || body.ControlMaster is not PlayerMobile) { return; }

        var (art, cue) = body switch
        {
            DruidRavenBody => (SpellEffectGraphics.RavenArrival, SpellEffectSounds.RavenArrival),
            DruidSpiritWolfBody => (SpellEffectGraphics.SpiritWolfArrival, SpellEffectSounds.SpiritWolfArrival),
            DruidDireWolfBody => (SpellEffectGraphics.DireWolfArrival, SpellEffectSounds.DireWolfArrival),
            DruidGrizzlyBody => (SpellEffectGraphics.GrizzlyArrival, SpellEffectSounds.GrizzlyArrival),
            DruidOakBody { SpiritAbility: CombatAbility.OakSage } => (SpellEffectGraphics.OakSageArrival, SpellEffectSounds.OakSageArrival),
            DruidOakBody { SpiritAbility: CombatAbility.HeartOfWolverine } => (SpellEffectGraphics.WolverineArrival, SpellEffectSounds.WolverineArrival),
            DruidOakBody { SpiritAbility: CombatAbility.SpiritOfBarbs } => (SpellEffectGraphics.BarbsArrival, SpellEffectSounds.BarbsArrival),
            DruidCreeperBody => (SpellEffectGraphics.CreeperArrival, SpellEffectSounds.CreeperArrival),
            DruidRecyclerBody { VineAbility: CombatAbility.CarrionVine } => (SpellEffectGraphics.CarrionArrival, SpellEffectSounds.CarrionArrival),
            DruidRecyclerBody { VineAbility: CombatAbility.SolarCreeper } => (SpellEffectGraphics.SolarArrival, SpellEffectSounds.SolarArrival),
            _ => ((ushort)0, 0)
        };
        if (art == 0 || !VisibleBodies.Add(body.Serial)) { return; }

        Effects.SendLocationEffect(body.Location, body.Map, art, 10);
        Effects.PlaySound(body.Location, body.Map, cue);
    }

    internal static void Depart(BaseCreature body) => Depart(body, body.Map, body.Location);

    private static void Depart(BaseCreature body, Map map, Point3D point)
    {
        ClearRage(body);
        if (!VisibleBodies.Remove(body.Serial) || !OnMap(map)) { return; }

        int art = body switch
        {
            DruidRavenBody => SpellEffectGraphics.RavenDeparture,
            DruidSpiritWolfBody => SpellEffectGraphics.SpiritWolfDeparture,
            DruidDireWolfBody => SpellEffectGraphics.DireWolfDeparture,
            DruidOakBody => SpellEffectGraphics.DruidSpiritDeparture,
            DruidCreeperBody => SpellEffectGraphics.CreeperDeparture,
            DruidRecyclerBody { VineAbility: CombatAbility.SolarCreeper } => SpellEffectGraphics.SolarDeparture,
            DruidRecyclerBody => SpellEffectGraphics.CarrionDeparture,
            _ => SpellEffectGraphics.GrizzlyDeparture
        };
        int cue = body is DruidOakBody ? SpellEffectSounds.DruidSpiritDeparture : SpellEffectSounds.DruidAnimalDeparture;
        Effects.SendLocationEffect(point, map, art, 10);
        SpellAreaSounds.Pulse((body.ControlMaster ?? body).Serial.Value, cue,
            point, map, TimedEffects.Now, 170);
    }

    internal static void Contact(BaseCreature body, Point3D point, Map map, bool smite = false)
    {
        var (art, cue) = body switch
        {
            DruidRavenBody => (SpellEffectGraphics.RavenContact, SpellEffectSounds.RavenContact),
            DruidSpiritWolfBody => (SpellEffectGraphics.SpiritWolfContact, SpellEffectSounds.SpiritWolfContact),
            DruidDireWolfBody => (SpellEffectGraphics.DireWolfContact, SpellEffectSounds.DireWolfContact),
            _ when smite => (SpellEffectGraphics.GrizzlySmite, SpellEffectSounds.DruidMaulImpact),
            _ => (SpellEffectGraphics.GrizzlyContact, SpellEffectSounds.GrizzlyContact)
        };
        Effects.SendLocationEffect(point, map, art, 6);
        SpellAreaSounds.Pulse((body.ControlMaster ?? body).Serial.Value, cue, point, map, TimedEffects.Now, 140);
    }

    internal static void RageStarted(DruidDireWolfBody body) => body.PlaySound(SpellEffectSounds.DireWolfRage);

    internal static void ObserveRage(DruidDireWolfBody body, DireWolfWorldState state)
    {
        int frames = state.RageUntil - state.Frame;
        if (frames <= 0)
        {
            ClearRage(body);
            return;
        }
        if (!OnMap(body.Map)) { return; }
        var lease = Rage.GetValueOrDefault(body.Serial);
        if (lease.Until == state.RageUntil && lease.Refresh > state.Frame) { return; }

        int duration = (Math.Min(frames, 16) * 40 + 49) / 50;
        body.FixedEffect(SpellEffectGraphics.DireWolfRage, 10, duration);
        Rage[body.Serial] = (state.RageUntil, state.Frame + 9);
    }

    private static void ClearRage(BaseCreature body)
    {
        if (Rage.Remove(body.Serial) && OnMap(body.Map)) { body.FixedEffect(SpellEffectGraphics.DireWolfRage, 10, 0); }
    }

    internal static void ApplyBlind(Mobile target, DateTime until)
    {
        Blind[target.Serial] = until;
        DrawBlind(target, until, TimedEffects.Now);
    }

    private static void RefreshBlind()
    {
        var now = TimedEffects.Now;
        BlindTargets.Clear();
        BlindTargets.AddRange(Blind.Keys);
        foreach (var serial in BlindTargets)
        {
            var target = World.FindMobile(serial);
            var until = Blind[serial];
            if (until > now && NamedControl.State(target)?.BlindUntil == until)
            {
                DrawBlind(target, until, now);
            }
            else
            {
                Blind.Remove(serial);
                if (OnMap(target?.Map)) { target.FixedEffect(SpellEffectGraphics.RavenBlind, 10, 0); }
            }
        }
    }

    private static void DrawBlind(Mobile target, DateTime until, DateTime now)
    {
        if (!OnMap(target.Map) || until <= now) { return; }
        int duration = (int)Math.Ceiling(Math.Min(550, (until - now).TotalMilliseconds) / 50);
        target.FixedEffect(SpellEffectGraphics.RavenBlind, 10, duration);
    }
}
