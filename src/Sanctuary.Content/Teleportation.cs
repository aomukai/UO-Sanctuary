// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Factions;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;
using Server.Spells.Fifth;
using Server.Spells.Fourth;
using Server.Spells.Sixth;
using Server.Targeting;

namespace Sanctuary;

public sealed record TeleportCast(Point3D Destination, Point3D Origin, Map Map, NetState Connection,
    Serial Weapon, int Rank, Serial Scroll, object Parent, ItemOrigin ItemOrigin, int Amount, int Cell);

public static class Teleportation
{
    public const int Range = 18;
    public static int Rank(PlayerMobile p) => ClassCreation.Profiles.TryGet(p.Serial, out var profile)
        && profile.Class == DiabloClass.Sorceress ? ClassCreation.Profiles.GetRank(p.Serial, "sorceress.teleport") : 0;
    public static int ManaCost(int rank) => Math.Max(1, 25 - rank);
    private static bool Ready(PlayerMobile p, long now) => EquipmentEconomy.Ready(p)
        && p.Map != null && p.Map != Map.Internal && p.Spell == null && !HitRecovery.Active(p, now)
        && !Sigil.ExistsOn(p) && !StaminaSystem.IsOverloaded(p);
    public static bool Landing(PlayerMobile p, Point3D point)
    {
        var map = p.Map;
        return map != null && map != Map.Internal && point.X >= 0 && point.Y >= 0 && point.X < map.Width && point.Y < map.Height
            && TreasureHunts.CanLand(p, map, point) && point.Z is >= -128 and <= 127 && point != p.Location && p.InRange(point, Range)
            && SpellHelper.CheckTravel(p, TravelCheckType.TeleportFrom, out _)
            && SpellHelper.CheckTravel(p, map, point, TravelCheckType.TeleportTo, out _)
            && !SpellHelper.CheckMulti(point, map) && !Region.Find(point, map).IsPartOf<HouseRegion>()
            && Space(map, point);
    }
    internal static bool Space(Map map, Point3D point, Mobile traveller = null, bool allowAllies = false)
    {
        if (!map.CanFit(point.X, point.Y, point.Z, 16, checkMobiles: traveller == null, requireSurface: true)) { return false; }
        if (traveller != null)
        {
            foreach (var mobile in map.GetMobilesInRange<Mobile>(point, 0))
            {
                if (mobile != traveller && (mobile.AccessLevel == AccessLevel.Player || !mobile.Hidden)
                    && mobile.Z + 16 > point.Z && point.Z + 16 > mobile.Z
                    && (!allowAllies || !AlliedOverlap.PassThrough(traveller, mobile))) { return false; }
            }
        }
        // Native spell fields block spawning, but arrivals must trigger their hazards.
        foreach (var item in map.GetItemsInRange<Item>(point, 0))
        {
            if (item.BlocksFit && item is not (FireFieldItem or PoisonField or ParalyzeField)
                && item.Z + item.ItemData.CalcHeight > point.Z && point.Z + 16 > item.Z) { return false; }
        }
        return true;
    }
    public static bool ScrollReady(PlayerMobile p, TeleportScroll scroll, int cell = -1) => scroll is
        { Deleted: false, Movable: true, Amount: 1, IsLockedDown: false, IsSecure: false }
        && (cell < 0 ? scroll.IsChildOf(p.Backpack) && scroll.IsAccessibleTo(p) && scroll.VerifyMove(p)
            : cell < PotionBelts.Rows(p) * 4 && PotionBelts.Entry(p, cell) == scroll && scroll.Parent == PotionBelts.Storage(p));
    public static bool Begin(PlayerMobile p, Point3D point, long now, TeleportScroll scroll = null, int cell = -1)
    {
        if (!Ready(p, now)) { return false; }
        var s = ClassCombat.For(p);
        if (now - s.NextAction < 0 || s.PendingAbility != CombatAbility.None || !Landing(p, point)
            || (scroll == null ? Rank(p) < 1 || p.Mana < ManaCost(Rank(p)) : !ScrollReady(p, scroll, cell)))
        { return false; }
        if (scroll != null) { ClassCombat.ReleaseCast(p); }
        s.Target = Serial.Zero;
        s.PendingTeleport = new(point, p.Location, p.Map, p.NetState, CombatTiming.Equipped(p)?.Serial ?? Serial.Zero,
            Rank(p), scroll?.Serial ?? Serial.Zero, scroll?.Parent, scroll?.Origin ?? ItemOrigin.Drop, scroll?.Amount ?? 0, cell);
        s.PendingAbility = CombatAbility.Teleport;
        s.Timing = CombatTiming.For(p, CombatAbility.Teleport);
        s.NextAction = now + s.Timing.DurationMs;
        s.EffectAt = now + s.Timing.EffectMs;
        s.Animation = 16;
        p.Direction = p.GetDirectionTo(point);
        p.RevealingAction();
        ClassCombat.SendState(p, s);
        ClassCombat.SendAnimation(p, 16, s.Timing.DurationMs);
        p.Animate(16, 5, 1, true, false, 0);
        return true;
    }
    private static bool Valid(PlayerMobile p, TeleportCast q, long now) => q != null && Ready(p, now)
        && p.Location == q.Origin && p.Map == q.Map && ReferenceEquals(p.NetState, q.Connection)
        && (CombatTiming.Equipped(p)?.Serial ?? Serial.Zero) == q.Weapon
        && (q.Scroll == Serial.Zero ? Rank(p) == q.Rank && q.Rank > 0 && p.Mana >= ManaCost(q.Rank)
            : World.FindItem(q.Scroll) is TeleportScroll scroll && ScrollReady(p, scroll, q.Cell)
                && ReferenceEquals(scroll.Parent, q.Parent) && scroll.Origin == q.ItemOrigin && scroll.Amount == q.Amount);
    public static bool Resolve(PlayerMobile p, long now)
    {
        var s = ClassCombat.For(p); var q = s.PendingTeleport;
        if (!Valid(p, q, now)) { s.PendingAbility = CombatAbility.None; s.PendingTeleport = null; return false; }
        if (now - s.EffectAt < 0) { return false; }
        s.PendingAbility = CombatAbility.None; s.PendingTeleport = null;
        if (!Landing(p, q.Destination)) { return false; }
        // Claim the effect before moving: location hooks cannot replay this action.
        if (q.Scroll == Serial.Zero) { p.Mana -= ManaCost(q.Rank); }
        else { World.FindItem(q.Scroll).Consume(); if (q.Cell >= 0) { PotionBelts.Compact(p, q.Cell % 4); } }
        p.Location = q.Destination; p.ProcessDelta();
        Effects.SendLocationParticles(EffectItem.Create(q.Origin, q.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
        Effects.SendLocationParticles(EffectItem.Create(p.Location, p.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);
        p.PlaySound(0x1FE);
        ArrivalFields(p);
        if (q.Cell >= 0) { PotionBeltNetwork.Send(p); }
        return true;
    }
    internal static void ArrivalFields(PlayerMobile p)
    {
        using var fields = PooledRefQueue<Item>.Create();
        foreach (var item in p.GetItemsAt())
        { if (item is ParalyzeField or PoisonField or FireFieldItem) { fields.Enqueue(item); } }
        while (fields.Count > 0) { fields.Dequeue().OnMoveOver(p); }
    }
    public static void Use(PlayerMobile p, TeleportScroll scroll)
    {
        if (!Ready(p, Core.TickCount) || !ScrollReady(p, scroll) || p.Target != null) { return; }
        p.SendMessage("Choose a clear landing spot within 18 tiles. The scroll is used only on success.");
        p.Target = new ScrollTarget(p, scroll);
    }
    private sealed class ScrollTarget : Target
    {
        private readonly Serial _owner, _scroll;
        private readonly object _parent;
        private readonly Map _map;
        private readonly NetState _connection;
        private bool _used;
        public ScrollTarget(PlayerMobile p, TeleportScroll scroll) : base(Teleportation.Range, true, TargetFlags.None)
        { _owner = p.Serial; _scroll = scroll.Serial; _parent = scroll.Parent; _connection = p.NetState; _map = p.Map; CheckLOS = false; }
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_used) { return; } _used = true;
            if (from is not PlayerMobile p || p.Serial != _owner || p.Map != _map || !ReferenceEquals(p.NetState, _connection)
                || World.FindItem(_scroll) is not TeleportScroll scroll || !ReferenceEquals(scroll.Parent, _parent)
                || targeted is not IPoint3D point) { return; }
            SpellHelper.GetSurfaceTop(ref point);
            if (!Begin(p, new Point3D(point), Core.TickCount, scroll)) { p.SendMessage("You cannot teleport there now. No scroll was used."); }
        }
    }
}

[SerializationGenerator(0)]
public partial class TeleportScroll : Item
{
    [Constructible]
    public TeleportScroll() : base(BeltProtocol.TeleportGraphic)
    { Name = "Teleport scroll"; Hue = BeltProtocol.TeleportHue; Weight = 0.1; }
    public override void OnDoubleClick(Mobile from)
    { if (from is PlayerMobile p) { Teleportation.Use(p, this); } }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Single use: Teleport within 18 tiles; no mana cost"}");
        list.Add($"{"Right-click in your backpack to target; belt hotkey uses the world cursor"}");
    }
}
