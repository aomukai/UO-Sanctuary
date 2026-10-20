// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Collections;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Regions;

namespace Sanctuary;

public sealed class ExpeditionSession
{
    internal Serial Chart, Owner;
    internal int Tier, Seed, Slot, Find, BuildIndex, FirstImmunity, SecondImmunity, HighResistance;
    internal Map Map, ReturnMap;
    internal Point3D Return;
    internal DateTime Expires;
    internal bool Active;
    internal TreasurePlan Plan;
    internal readonly List<Serial> Items = new(), Creatures = new(), Guardians = new();
    internal ExpeditionRegion Region;
    internal HashSet<Point2D> Floor;
    internal TimerExecutionToken Building;
    public Point3D Origin { get; internal set; }
    public Rectangle2D Bounds => new(Origin.X, Origin.Y, 224, 224);
    internal Point3D WorldPoint(Point2D local) => new(Origin.X + local.X, Origin.Y + local.Y, 0);
    internal Point3D Entry => WorldPoint(Plan.Rooms[0].Center);
    internal bool Contains(Mobile mobile) => mobile.Map == Map && Bounds.Contains(mobile.Location);
    internal bool Owns(Mobile mobile) => mobile.Serial == Owner || mobile is BaseCreature bc
        && (bc.ControlMaster?.Serial == Owner || bc.SummonMaster?.Serial == Owner);
    internal bool Walkable(Point3D point) => point.Z == 0 && Floor.Contains(new(point.X - Origin.X, point.Y - Origin.Y));
}
public sealed class TreasureExpeditions : GenericPersistence
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TreasureExpeditions));
    private readonly Dictionary<Serial, ExpeditionSession> _sessions = new();
    public TreasureExpeditions() : base("SanctuaryTreasureExpeditions", 128) { }
    internal ExpeditionSession Find(Serial chart) => _sessions.GetValueOrDefault(chart);
    internal IReadOnlyCollection<ExpeditionSession> All => _sessions.Values;
    internal ExpeditionSession Begin(PlayerMobile owner, TreasureChart chart)
    {
        Tick();
        if (!ClassProfessions.Ready(owner) || !RuneCrafting.Accessible(owner, chart) || chart.Spent || chart.Decoder == null
            || !ClassProfessions.Active(owner, SkillName.Cartography) || owner.Skills.Cartography.Base < TreasureHunts.Tier(chart.Tier).Cartography
            || owner.Map != chart.Facet || !owner.InRange(new Point3D(chart.Site, 0), 6 + TreasureChart.SearchTolerance(owner)))
        { return null; }
        foreach (var session in _sessions.Values)
        { if (session.Owner == owner.Serial) { owner.SendMessage("Finish your current treasure expedition first."); return null; } }
        var plan = TreasureLayout.Generate(chart.Seed); ExpeditionSession hunt = null;
        for (int slot = 0; slot < TreasureHunts.Settings.SlotsPerFacet; slot++)
        {
            bool reserved = false;
            foreach (var session in _sessions.Values) { if (session.Map == chart.Facet && session.Slot == slot) { reserved = true; break; } }
            if (reserved) { continue; }
            var candidate = new ExpeditionSession { Slot = slot, Map = chart.Facet, Plan = plan,
                Origin = new(TreasureHunts.Settings.SlotX + slot % 3 * 224, TreasureHunts.Settings.SlotY + slot / 3 * 224, 0) };
            if (Clear(candidate)) { hunt = candidate; break; }
        }
        if (hunt == null) { owner.SendMessage("All treasure areas are occupied. Your map is unchanged; try again later."); return null; }
        hunt.Chart = chart.Serial; hunt.Owner = owner.Serial; hunt.Tier = chart.Tier; hunt.Seed = chart.Seed;
        hunt.ReturnMap = owner.Map; hunt.Return = new(chart.Site, owner.Map.GetAverageZ(chart.Site.X, chart.Site.Y));
        hunt.FirstImmunity = chart.FirstImmunity; hunt.SecondImmunity = chart.SecondImmunity; hunt.HighResistance = chart.HighResistance;
        hunt.Find = GearDrops.MagicFind(owner); hunt.Expires = DateTime.UtcNow.AddMinutes(TreasureHunts.Settings.LifetimeMinutes);
        chart.Spent = true; _sessions.Add(chart.Serial, hunt); Register(hunt); Build(hunt);
        owner.SendMessage("You uncover a dungeon entrance. Its rooms and rewards are now fixed; the entrance will be ready shortly.");
        return hunt;
    }
    private static bool Clear(ExpeditionSession hunt)
    {
        foreach (var _ in hunt.Map.GetItemsInBounds(hunt.Bounds)) { return false; }
        foreach (var _ in hunt.Map.GetMobilesInBounds(hunt.Bounds)) { return false; }
        foreach (var local in hunt.Plan.Floor)
        { var p = hunt.WorldPoint(local); if (!hunt.Map.CanFit(p.X, p.Y, 0, 16)) { return false; } }
        return true;
    }
    private static void Register(ExpeditionSession hunt)
    {
        hunt.Floor = new(hunt.Plan.Floor); hunt.Region = new(hunt); hunt.Region.Register();
    }
    private void Build(ExpeditionSession hunt)
    {
        hunt.Building.Cancel();
        Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(20), () =>
        {
            if (!_sessions.ContainsKey(hunt.Chart) || hunt.Active) { hunt.Building.Cancel(); return; }
            try
            {
                int total = hunt.Plan.Floor.Length + hunt.Plan.Walls.Length;
                int end = Math.Min(total, hunt.BuildIndex + 128);
                while (hunt.BuildIndex < end)
                {
                    int index = hunt.BuildIndex;
                    bool floor = index < hunt.Plan.Floor.Length;
                    var point = floor ? hunt.Plan.Floor[index] : hunt.Plan.Walls[index - hunt.Plan.Floor.Length];
                    var item = new Static(floor ? 0x519 : 0x80) { Hue = 0, Movable = false };
                    hunt.Items.Add(item.Serial); item.MoveToWorld(hunt.WorldPoint(point), hunt.Map); hunt.BuildIndex++;
                }
                if (hunt.BuildIndex == total) { Finish(hunt); hunt.Building.Cancel(); }
            }
            catch (Exception error)
            {
                Expire(hunt, refund: true);
                _logger.Error(error, "Treasure entrance construction failed; the map was refunded");
            }
        }, out hunt.Building);
    }
    private void Finish(ExpeditionSession hunt)
    {
        var random = new Random(hunt.Seed ^ 0x51A73); var row = TreasureHunts.Tier(hunt.Tier);
        for (int room = 1; room < hunt.Plan.Rooms.Length; room++)
        {
            var anchor = hunt.WorldPoint(hunt.Plan.Rooms[room].Center);
            var members = new List<Serial>();
            bool boss = room == hunt.Plan.Rooms.Length - 1;
            var kind = (MonsterArchetype)random.Next(4);
            var profile = TreasureHunts.Profile($"treasure-{hunt.Tier}-{kind}");
            var modifiers = boss ? EncounterProfiles.RollModifiers(profile, row.Difficulty, random) : [];
            var aura = Array.IndexOf(modifiers, EliteModifier.AuraEnchanted) >= 0 ? EncounterProfiles.RollAura(row.MonsterMaximum, random) : EliteAura.None;
            int count = boss ? 6 : TreasureHunts.Settings.PackSize;
            int packLevel = random.Next(row.MonsterMinimum, row.MonsterMaximum - (boss ? 3 : 0) + 1);
            for (int n = 0; n < count; n++)
            {
                var grade = boss ? n == 0 ? EncounterGrade.Unique : EncounterGrade.Minion : EncounterGrade.Ordinary;
                int level = packLevel;
                var source = row.Creatures[random.Next(row.Creatures.Length)];
                var mob = new TreasureMonster(); hunt.Creatures.Add(mob.Serial); members.Add(mob.Serial);
                mob.Bind(hunt.Tier, hunt.FirstImmunity, hunt.SecondImmunity, hunt.HighResistance, source.Type);
                mob.Assign(profile, level, row.Difficulty, grade, ChampionKind.Champion, modifiers, aura, null, random);
                mob.GroupLeader = n == 0 ? mob.Serial.Value : members[0].Value;
                var at = new Point3D(anchor.X + n % 3 - 1, anchor.Y + n / 3, anchor.Z);
                if (!hunt.Map.CanFit(at.X, at.Y, at.Z, 16)) { throw new InvalidOperationException("Treasure pack placement was blocked."); }
                mob.Home = at; mob.HomeMap = hunt.Map; mob.RangeHome = 4; mob.MoveToWorld(at, hunt.Map); EliteAuras.Observe(mob);
            }
            if (room == hunt.Plan.Rooms.Length - 1) { hunt.Guardians.AddRange(members); }
        }
        var center = hunt.WorldPoint(hunt.Plan.Rooms[^1].Center);
        var chest = new ExpeditionChest(hunt.Chart.Value); hunt.Items.Add(chest.Serial);
        TreasureHunts.Fill(chest, row, hunt.Find, random); chest.MoveToWorld(new(center.X, center.Y + 4, center.Z), hunt.Map);
        var entrance = new ExpeditionPortal(hunt.Chart, false); hunt.Items.Add(entrance.Serial); entrance.MoveToWorld(hunt.Return, hunt.ReturnMap);
        var exit = new ExpeditionPortal(hunt.Chart, true); hunt.Items.Add(exit.Serial); exit.MoveToWorld(new(hunt.Entry.X, hunt.Entry.Y + 4, hunt.Entry.Z), hunt.Map);
        hunt.Active = true;
        if (World.FindMobile(hunt.Owner) is PlayerMobile owner) { owner.SendMessage("Your treasure entrance is ready. Double-click it to enter with your pets."); }
    }
    internal bool Travel(PlayerMobile owner, ExpeditionPortal portal)
    {
        var hunt = Find(portal.ChartSerial);
        if (hunt == null || !hunt.Active || hunt.Expires <= DateTime.UtcNow || owner.Serial != hunt.Owner
            || !ClassProfessions.Ready(owner) || owner.Map != portal.Map || !owner.InRange(portal, 2) || !owner.InLOS(portal)
            || !hunt.Items.Contains(portal.Serial)) { return false; }
        var map = portal.Exit ? hunt.ReturnMap : hunt.Map;
        var center = portal.Exit ? hunt.Return : hunt.Entry;
        if (!Landing(owner, map, center, out var target, portal.Exit ? null : hunt))
        { owner.SendMessage("The arrival area is blocked. Nothing was moved."); return false; }
        ClassCombat.Stop(owner); owner.RevealingAction(); BaseCreature.TeleportPets(owner, target, map);
        if (portal.Exit)
        {
            using var followers = PooledRefList<Mobile>.Create();
            foreach (var mob in hunt.Map.GetMobilesInBounds(hunt.Bounds)) { if (mob != owner && hunt.Owns(mob)) { followers.Add(mob); } }
            foreach (var mob in followers) { mob.MoveToWorld(target, map); }
        }
        owner.MoveToWorld(target, map); AlliedOverlap.DisperseFollowers(owner); owner.ProcessDelta(); owner.PlaySound(0x1FE);
        return true;
    }
    private static bool Landing(Mobile owner, Map map, Point3D center, out Point3D target, ExpeditionSession hunt = null)
    {
        for (int radius = 0; radius <= 8; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) { continue; }
                    int x = center.X + dx, y = center.Y + dy;
                    if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) { continue; }
                    var p = new Point3D(x, y, hunt == null ? map.GetAverageZ(x, y) : 0);
                    if ((hunt == null || hunt.Walkable(p)) && !Region.Find(p, map).IsPartOf<HouseRegion>()
                        && Teleportation.Space(map, p, owner, allowAllies: true)) { target = p; return true; }
                }
            }
        }
        target = default; return false;
    }
    internal void Tick()
    {
        var expired = new List<ExpeditionSession>();
        foreach (var hunt in _sessions.Values)
        { if (hunt.Expires <= DateTime.UtcNow || World.FindMobile(hunt.Owner) is not { Deleted: false }) { expired.Add(hunt); } }
        foreach (var hunt in expired) { Expire(hunt); }
    }
    public void Prune() => Tick();
    internal void CheckLogin(Mobile owner)
    {
        Tick();
        foreach (var hunt in _sessions.Values)
        {
            if (hunt.Owner == owner.Serial && hunt.Contains(owner))
            { owner.SendMessage("Your treasure expedition retains its rooms, defeated packs and chest state."); return; }
        }
    }
    internal void Expire(ExpeditionSession hunt, bool refund = false)
    {
        if (!_sessions.Remove(hunt.Chart)) { return; }
        hunt.Building.Cancel(); hunt.Region?.Unregister(); hunt.Region = null;
        var owner = World.FindMobile(hunt.Owner);
        if (owner is PlayerMobile portalOwner && TownPortals.For(portalOwner) is { } pair && pair.Field.Map == hunt.Map
            && hunt.Bounds.Contains(pair.Field.Location)) { TownPortals.Close(hunt.Owner); }
        var map = hunt.ReturnMap; var target = hunt.Return;
        if (!Landing(owner, map, target, out target))
        {
            target = new Point3D(1432, 1690, 0);
            if (Landing(owner, map, target, out var safe)) { target = safe; }
        }
        using var mobiles = PooledRefList<Mobile>.Create();
        foreach (var mobile in hunt.Map.GetMobilesInBounds(hunt.Bounds)) { if (hunt.Owns(mobile)) { mobiles.Add(mobile); } }
        foreach (var mobile in mobiles) { if (mobile is PlayerMobile player) { ClassCombat.Stop(player); } mobile.MoveToWorld(target, map); }
        if (owner is PlayerMobile p)
        {
            if (p.Map == Map.Internal && p.LogoutMap == hunt.Map && hunt.Bounds.Contains(p.LogoutLocation))
            { p.LogoutMap = map; p.LogoutLocation = target; }
            else if (hunt.Contains(p)) { ClassCombat.Stop(p); p.MoveToWorld(target, map); }
            AlliedOverlap.DisperseFollowers(p);
            p.SendMessage(refund ? "The entrance could not be prepared. Your map was returned safely." : "Your treasure expedition ended. Recoverable belongings were returned to its entrance.");
        }
        using var items = PooledRefList<Item>.Create();
        foreach (var item in hunt.Map.GetItemsInBounds(hunt.Bounds)) { items.Add(item); }
        var generated = new HashSet<Serial>(hunt.Items); ExpeditionRecovery recovery = null;
        foreach (var item in items)
        {
            if (generated.Contains(item.Serial)) { continue; }
            if (item is Corpse corpse)
            { if (corpse.Owner is PlayerMobile) { corpse.MoveToWorld(target, map); } else { corpse.Delete(); } continue; }
            if (item.Parent != null) { continue; }
            recovery ??= new ExpeditionRecovery(hunt.Owner.Value);
            recovery.DropItem(item);
        }
        if (recovery != null) { recovery.MoveToWorld(target, map); }
        foreach (var serial in hunt.Creatures) { if (World.FindMobile(serial) is { Deleted: false } mob && !hunt.Owns(mob)) { mob.Delete(); } }
        foreach (var serial in hunt.Items) { World.FindItem(serial)?.Delete(); }
        hunt.Items.Clear(); hunt.Creatures.Clear(); hunt.Guardians.Clear(); hunt.Floor.Clear();
        if (refund && World.FindItem(hunt.Chart) is TreasureChart chart) { chart.Spent = false; }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_sessions.Count);
        foreach (var h in _sessions.Values)
        {
            writer.Write(h.Chart.Value); writer.Write(h.Owner.Value); writer.Write(h.Tier); writer.Write(h.Seed); writer.Write(h.Slot);
            writer.Write(h.Origin); writer.Write(h.Find); writer.Write(h.BuildIndex); writer.Write(h.FirstImmunity); writer.Write(h.SecondImmunity); writer.Write(h.HighResistance); writer.Write(h.Map); writer.Write(h.ReturnMap); writer.Write(h.Return);
            writer.Write(h.Expires); writer.Write(h.Active);
            Points(writer, h.Plan.Floor); Points(writer, h.Plan.Walls); writer.Write(h.Plan.Rooms.Length);
            foreach (var r in h.Plan.Rooms) { writer.Write(r.Center); writer.Write(r.Shape); }
            writer.Write(h.Plan.Links.Length); foreach (var e in h.Plan.Links) { writer.Write(e[0]); writer.Write(e[1]); }
            Serials(writer, h.Items); Serials(writer, h.Creatures); Serials(writer, h.Guardians);
        }
    }
    private static void Points(IGenericWriter w, Point2D[] points)
    { w.Write(points.Length); foreach (var p in points) { w.Write(p); } }
    private static Point2D[] Points(IGenericReader r)
    { int n = Count(r, 50000); var result = new Point2D[n]; for (int i = 0; i < n; i++) { result[i] = r.ReadPoint2D(); } return result; }
    private static void Serials(IGenericWriter w, List<Serial> values)
    { w.Write(values.Count); foreach (var v in values) { w.Write(v.Value); } }
    private static void Serials(IGenericReader r, List<Serial> values)
    { int n = Count(r, 50000); for (int i = 0; i < n; i++) { values.Add((Serial)r.ReadUInt()); } }
    private static int Count(IGenericReader r, int max)
    { int n = r.ReadInt(); return n < 0 || n > max ? throw new InvalidDataException("Invalid expedition list length.") : n; }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown expedition save version."); }
        foreach (var old in _sessions.Values) { old.Building.Cancel(); old.Region?.Unregister(); }
        _sessions.Clear(); int count = Count(reader, 18);
        for (int i = 0; i < count; i++)
        {
            var h = new ExpeditionSession { Chart = (Serial)reader.ReadUInt(), Owner = (Serial)reader.ReadUInt(), Tier = reader.ReadInt(),
                Seed = reader.ReadInt(), Slot = reader.ReadInt(), Origin = reader.ReadPoint3D(), Find = reader.ReadInt(), BuildIndex = reader.ReadInt(),
                FirstImmunity = reader.ReadInt(), SecondImmunity = reader.ReadInt(), HighResistance = reader.ReadInt(),
                Map = reader.ReadMap(), ReturnMap = reader.ReadMap(), Return = reader.ReadPoint3D(), Expires = reader.ReadDateTime(), Active = reader.ReadBool() };
            var floor = Points(reader); var walls = Points(reader); var rooms = new TreasureRoom[Count(reader, 16)];
            for (int n = 0; n < rooms.Length; n++) { rooms[n] = new(reader.ReadPoint2D(), reader.ReadInt()); }
            var links = new int[Count(reader, 32)][];
            for (int n = 0; n < links.Length; n++) { links[n] = [reader.ReadInt(), reader.ReadInt()]; }
            h.Plan = new(floor, walls, rooms, links); Serials(reader, h.Items); Serials(reader, h.Creatures); Serials(reader, h.Guardians);
            if (h.Tier is < 1 or > 5 || h.Slot is < 0 or >= 9 || h.Map != Map.Felucca && h.Map != Map.Trammel
                || h.ReturnMap != Map.Felucca && h.ReturnMap != Map.Trammel || rooms.Length < 8 || rooms.Length > 12
                || floor.Length == 0 || h.BuildIndex < 0 || h.BuildIndex > floor.Length + walls.Length)
            { throw new InvalidDataException("Invalid expedition identity or layout."); }
            foreach (var existing in _sessions.Values)
            { if (existing.Map == h.Map && existing.Slot == h.Slot || existing.Owner == h.Owner) { throw new InvalidDataException("Duplicate expedition slot or owner."); } }
            _sessions.Add(h.Chart, h);
        }
    }
    public override void PostDeserialize()
    {
        foreach (var hunt in _sessions.Values) { Register(hunt); if (!hunt.Active) { Build(hunt); } }
        Tick();
    }
}
public sealed class ExpeditionRegion : DungeonRegion
{
    private readonly ExpeditionSession _hunt;
    internal ExpeditionRegion(ExpeditionSession hunt) : base("Treasure " + TreasureHunts.Tier(hunt.Tier).Name + " " + hunt.Chart.Value,
        hunt.Map, 200, new Rectangle3D(hunt.Origin.X, hunt.Origin.Y, -128, 224, 224, 256)) { _hunt = hunt; Entrance = hunt.Return; }
    internal bool Accepts(Mobile mobile, Point3D point) => mobile.AccessLevel > AccessLevel.Player
        || _hunt.Active && _hunt.Walkable(point) && (_hunt.Owns(mobile) || _hunt.Creatures.Contains(mobile.Serial));
    public override bool OnMoveInto(Mobile mobile, Direction direction, Point3D target, Point3D oldLocation) => Accepts(mobile, target);
    public override void OnEnter(Mobile mobile)
    {
        if (mobile.AccessLevel > AccessLevel.Player || _hunt.Owns(mobile) || _hunt.Creatures.Contains(mobile.Serial)) { return; }
        mobile.MoveToWorld(_hunt.Return, _hunt.ReturnMap);
    }
}
