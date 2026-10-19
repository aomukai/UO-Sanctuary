// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Items;

namespace Sanctuary;

public static class PotionRestoration
{
    private sealed class Lane
    {
        public long Units, Remainder;
        public int Duration;
        public void Add(int amount, int duration) { Units += amount * 1000000L; Duration += duration; }
        public int Advance(int elapsed)
        {
            int step = Math.Min(Duration, elapsed);
            long value = step == Duration ? Units : Units * step / Duration;
            Units -= value;
            Duration -= step;
            Remainder += value;
            int result = (int)(Remainder / 1000000);
            Remainder %= 1000000;
            return result;
        }
        public void Clear() { Units = Remainder = 0; Duration = 0; }
    }
    private sealed class Effect(PlayerMobile player)
    {
        public readonly PlayerMobile Player = player;
        public readonly Lane Life = new(), Mana = new();
        public long Last = Core.TickCount;
    }
    private static readonly Dictionary<Serial, Effect> _effects = new();
    private static readonly List<Serial> _finished = new();
    public static void Configure()
    {
        EventSink.Disconnected += Clear;
        EventSink.Logout += Clear;
    }
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), Tick);
    public static void Clear(Mobile player) => _effects.Remove(player.Serial);
    public static bool CanUse(PlayerMobile player, RestorationPotion potion) => PotionBelts.Ready(player)
        && potion is { Deleted: false, Movable: true, Amount: > 0 } && player.Holding != potion
        && (PotionBelts.Active(player, potion) || player.Backpack != null && potion.IsChildOf(player.Backpack));
    public static int Amount(PlayerMobile player, RestorationBase basis)
    {
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        int halves = profile.Class switch
        {
            DiabloClass.Amazon or DiabloClass.Assassin or DiabloClass.Paladin => 3,
            DiabloClass.Barbarian => basis.Kind == RestorationKind.Healing ? 4 : 2,
            _ => basis.Kind == RestorationKind.Mana ? 4 : 2
        };
        return basis.Amount * halves / 2;
    }
    public static bool Drink(PlayerMobile player, RestorationPotion potion)
    {
        if (!CanUse(player, potion))
        {
            return false;
        }
        var row = PotionBeltCatalog.Potion(potion.Kind, potion.Tier);
        if (row.Kind == RestorationKind.Healing ? player.Hits >= player.HitsMax : player.Mana >= player.ManaMax)
        {
            return false;
        }
        if (!_effects.TryGetValue(player.Serial, out var effect))
        {
            _effects[player.Serial] = effect = new Effect(player);
        }
        // Settle elapsed time before extending the lane. Same-tier drinks extend duration,
        // rather than multiplying the rate; mixed tiers use the remaining weighted rate.
        Advance(effect, Core.TickCount);
        if (row.Kind == RestorationKind.Healing ? player.Hits >= player.HitsMax : player.Mana >= player.ManaMax)
        {
            return false;
        }
        var lane = row.Kind == RestorationKind.Healing ? effect.Life : effect.Mana;
        if (lane.Duration > 600000 - row.DurationMs)
        {
            return false;
        }
        lane.Add(Amount(player, row), row.DurationMs);
        int column = potion.X;
        bool inBelt = PotionBelts.Active(player, potion);
        var origin = potion.Origin;
        potion.Consume(1);
        if (Utility.RandomDouble() < 0.20)
        {
            var bottle = ItemOrigins.Create(origin, () => new Bottle());
            if (player.Backpack == null || !player.Backpack.TryDropItem(player, bottle, false))
            {
                bottle.Delete();
            }
        }
        if (inBelt)
        {
            PotionBelts.Compact(player, column);
        }
        player.RevealingAction();
        player.PlaySound(0x2D6);
        // No drinking animation, free-hand/cast gate, or combat action deadline changes.
        return true;
    }
    private static void Advance(Effect effect, long now)
    {
        int elapsed = (int)Math.Clamp(now - effect.Last, 0, 600000);
        effect.Last = now;
        var player = effect.Player;
        if (player.Hits >= player.HitsMax) { effect.Life.Clear(); }
        else if (effect.Life.Duration > 0)
        {
            player.Hits = Math.Min(player.HitsMax, player.Hits + effect.Life.Advance(elapsed));
            if (player.Hits >= player.HitsMax) { effect.Life.Clear(); }
        }
        if (player.Mana >= player.ManaMax) { effect.Mana.Clear(); }
        else if (effect.Mana.Duration > 0)
        {
            player.Mana = Math.Min(player.ManaMax, player.Mana + effect.Mana.Advance(elapsed));
            if (player.Mana >= player.ManaMax) { effect.Mana.Clear(); }
        }
    }
    private static void Tick()
    {
        long now = Core.TickCount;
        foreach (var (serial, effect) in _effects)
        {
            if (!PotionBelts.Ready(effect.Player))
            {
                _finished.Add(serial);
                continue;
            }
            Advance(effect, now);
            if (effect.Life.Duration == 0 && effect.Mana.Duration == 0)
            {
                _finished.Add(serial);
            }
        }
        foreach (var serial in _finished) { _effects.Remove(serial); }
        _finished.Clear();
    }
}
