// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Factions;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;

namespace Sanctuary;

public static class TownPortals
{
    public sealed record Pair(TownPortalEnd Field, TownPortalEnd Town, string TownName);
    private static readonly Dictionary<Serial, Pair> _pairs = new();
    public static Pair For(PlayerMobile p) => _pairs.GetValueOrDefault(p.Serial);
    public static void Configure()
    {
        EventSink.Disconnected += p => Close(p.Serial);
        EventSink.Logout += p => Close(p.Serial);
    }
    public static void Close(Serial owner)
    { if (_pairs.Remove(owner, out var pair)) { pair.Field.Delete(); pair.Town.Delete(); } }
    internal static void Deleted(TownPortalEnd end)
    { if (_pairs.TryGetValue(end.Owner, out var pair) && (pair.Field == end || pair.Town == end)) { Close(end.Owner); } }
    private static bool Ready(PlayerMobile p) => EquipmentEconomy.Ready(p) && p.Map != null && p.Map != Map.Internal
        && p.Spell == null && !HitRecovery.Active(p, Core.TickCount) && !StaminaSystem.IsOverloaded(p) && !Sigil.ExistsOn(p);
    private static bool Allowed(PlayerMobile p, Point3D from, Point3D to) =>
        SpellHelper.CheckTravel(p, p.Map, from, TravelCheckType.GateFrom, out _)
        && SpellHelper.CheckTravel(p, p.Map, to, TravelCheckType.GateTo, out _)
        && !Region.Find(from, p.Map).IsPartOf<HouseRegion>() && !Region.Find(to, p.Map).IsPartOf<HouseRegion>()
        && !SpellHelper.CheckMulti(from, p.Map) && !SpellHelper.CheckMulti(to, p.Map);
    private static bool Floor(PlayerMobile p, Point3D anchor, int radius, bool nearby, out Point3D result)
    {
        for (int r = 1; r <= radius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) { continue; }
                    int x = anchor.X + dx, y = anchor.Y + dy;
                    if (x < 0 || y < 0 || x >= p.Map.Width || y >= p.Map.Height) { continue; }
                    foreach (int z in new[] { anchor.Z, p.Map.GetAverageZ(x, y) })
                    {
                        var q = new Point3D(x, y, z);
                        if ((!nearby || Math.Abs(z - p.Z) < 16 && p.InLOS(q)) && Teleportation.Space(p.Map, q, p, allowAllies: true)
                            && Allowed(p, p.Location, q)) { result = q; return true; }
                    }
                }
            }
        }
        result = default; return false;
    }
    public static bool Open(PlayerMobile p, Item source, int cell = -1)
    {
        if (source is not (TownPortalScroll or TownPortalTome) || !Ready(p) || !UtilityScrolls.Ready(p, source, cell)) { return false; }
        var action = ClassCombat.For(p); long now = Core.TickCount;
        if (action.PendingAbility != CombatAbility.None || now - action.NextAction < 0) { return false; }
        RecognitionHall nearest = null; long distance = long.MaxValue;
        foreach (var h in TownRecognition.Halls)
        {
            if (h.Map != p.Map.Name) { continue; }
            long dx = h.X - p.X, dy = h.Y - p.Y, d = dx * dx + dy * dy;
            if (d < distance) { nearest = h; distance = d; }
        }
        if (nearest == null || !Floor(p, p.Location, 2, true, out var field)
            || !Floor(p, new Point3D(nearest.X, nearest.Y, nearest.Z), 8, false, out var town) || town == field)
        { p.SendMessage("No safe Town Portal route is available. Nothing was spent."); return false; }
        var a = new TownPortalEnd(p.Serial, false); var b = new TownPortalEnd(p.Serial, true);
        a.MoveToWorld(field, p.Map); b.MoveToWorld(town, p.Map);
        Close(p.Serial); _pairs[p.Serial] = new(a, b, nearest.Town);
        UtilityScrolls.Spend(p, source, cell);
        ClassCombat.Stop(p); action.NextAction = now + CombatTiming.For(p, CombatAbility.Teleport).DurationMs;
        p.RevealingAction(); p.PlaySound(0x20E); p.SendMessage($"Your Town Portal leads to {nearest.Town}. It closes after your return trip, death or logout.");
        return true;
    }
    private static bool Arrival(PlayerMobile p, Point3D anchor, out Point3D target)
    {
        for (int radius = 0; radius <= 2; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) { continue; }
                    var point = new Point3D(anchor.X + dx, anchor.Y + dy, anchor.Z);
                    if (Teleportation.Space(p.Map, point, p) && Allowed(p, p.Location, point)) { target = point; return true; }
                }
            }
        }
        target = default; return false;
    }
    public static bool Travel(PlayerMobile p, TownPortalEnd end)
    {
        if (!Ready(p) || end is not { Deleted: false } || end.Owner != p.Serial || !_pairs.TryGetValue(p.Serial, out var pair)
            || (pair.Field != end && pair.Town != end) || end.Map != p.Map || !p.InRange(end, 1) || Math.Abs(p.Z - end.Z) >= 16 || !p.InLOS(end)) { return false; }
        var other = end == pair.Field ? pair.Town : pair.Field;
        if (other.Deleted || other.Map != p.Map || !Arrival(p, other.Location, out var target))
        { p.SendMessage("The other end of your portal is blocked or travel is restricted."); return false; }
        bool returning = end == pair.Town;
        if (returning) { Close(p.Serial); }
        ClassCombat.Stop(p); p.RevealingAction(); BaseCreature.TeleportPets(p, target, p.Map);
        p.Location = target; AlliedOverlap.DisperseFollowers(p); p.ProcessDelta(); p.PlaySound(0x1FE); Teleportation.ArrivalFields(p);
        return true;
    }
}

[SerializationGenerator(0)]
public partial class TownPortalEnd : Item
{
    [SerializableField(0, setter: "private")] private Serial _owner;
    [SerializableField(1, setter: "private")] private bool _townSide;
    private TimerExecutionToken _cleanup;
    [Constructible] public TownPortalEnd() : this(Serial.Zero, false) { }
    internal TownPortalEnd(Serial owner, bool townSide) : base(0xF6C)
    { _owner = owner; _townSide = townSide; Name = "Town Portal"; Hue = 0x482; Movable = false; Light = LightType.Circle300; }
    [AfterDeserialization] private void Restore() => Timer.StartTimer(TimeSpan.Zero, Delete, out _cleanup);
    public override void OnAfterDelete() { _cleanup.Cancel(); TownPortals.Deleted(this); base.OnAfterDelete(); }
    public override void OnDoubleClick(Mobile from) { if (from is PlayerMobile p) { TownPortals.Travel(p, this); } }
    // A successful teleport must stop the native step from overwriting its new location.
    public override bool OnMoveOver(Mobile from) => from is not PlayerMobile p || !TownPortals.Travel(p, this);
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list); list.Add($"{"Owner: "}{World.FindMobile(_owner)?.Name ?? "Unavailable"}");
        list.Add($"{(_townSide ? "Return to your field portal; closes the pair" : "Travel to the nearest supported town")}");
    }
}
