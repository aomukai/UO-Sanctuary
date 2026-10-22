using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record HydraHead(long Id, long Group, int Index, uint Owner, int Rank, int Map, double X, double Y, int Z,
    int Min256, int Max256, int Pierce, ulong Seed, DateTime UntilUtc, DateTime DecisionUtc, DateTime ShotUtc, uint Target);
public sealed record HydraBolt(long Id, long Head, uint Owner, int Map, double X, double Y, int Z, double Vx, double Vy,
    int Frame, int Min256, int Max256, int Pierce, DateTime UntilUtc, DateTime NextUtc);
public sealed record HydraSave(HydraHead[] Heads, HydraBolt[] Bolts);
public sealed record HydraAnimation(int SpawnFrames, int AttackFrames, int ActionFrame);
public enum HydraDecision { Die, Shoot, Idle }

public sealed class HydraProfiles : GenericPersistence
{
    public HydraProfiles() : base("SanctuaryHydra", 118) { }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(Hydras.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Hydra version"); }
        Hydras.Restore(JsonSerializer.Deserialize<HydraSave>(reader.ReadString()));
    }
}

public static class Hydras
{
    private static readonly Dictionary<long, HydraHead> _heads = new();
    private static readonly Dictionary<long, HydraBolt> _bolts = new();
    private static readonly List<long> _keys = new();
    private static long _nextId;
    private static HydraAnimation _animation;
    public static HydraProfiles Profiles { get; private set; }
    public static void Configure() { Profiles = new(); _animation = BalanceCatalog.Read<HydraAnimation>("hydra-animation")[0]; }
    public static void Initialize() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(40), Tick);
    public static HydraDecision Decide(bool expired, bool target, double distance, int roll)
        => expired ? HydraDecision.Die : target && distance < 25 && roll < 60 ? HydraDecision.Shoot : HydraDecision.Idle;
    private static List<(int Index, double X, double Y)> Places(PlayerMobile owner, Point3D point)
    {
        var result = new List<(int, double, double)>();
        for (int index = 0; index < 3; index++)
        {
            double x = point.X + SkillGeometry.Tiles(index - 1), y = point.Y - (index == 1 ? 0 : SkillGeometry.Tiles(1));
            var tile = new Point3D((int)Math.Round(x), (int)Math.Round(y), point.Z);
            if (tile.X >= 0 && tile.Y >= 0 && tile.X < owner.Map.Width && tile.Y < owner.Map.Height
                && owner.Map.CanFit(tile, 1, false, false, true)) { result.Add((index, x, y)); }
        }
        return result;
    }
    public static bool CanPlace(PlayerMobile owner, Point3D point) => Places(owner, point).Count > 0;
    public static void Cast(PlayerMobile owner, Point3D point, SkillValues values)
    {
        var groups = new SortedSet<long>();
        foreach (var head in _heads.Values) { if (head.Owner == owner.Serial.Value) { groups.Add(head.Group); } }
        int cap = Math.Max(1, (int)values.Eval("petmax") / 3);
        while (groups.Count >= cap)
        {
            long oldest = groups.Min; var remove = new List<long>();
            foreach (var head in _heads.Values) { if (head.Owner == owner.Serial.Value && head.Group == oldest) { remove.Add(head.Id); } }
            foreach (long id in remove) { _heads.Remove(id); } groups.Remove(oldest);
        }
        var now = TimedEffects.Now; long group = ++_nextId;
        int potion = CombatPotions.Bonus(owner, "magical-penetration");
        int Scale(int damage) => (int)Math.Clamp(NamedElements.Enhance(owner, damage * (100 + potion) / 100.0, "fire"), 0, int.MaxValue);
        int min = Scale(values.Damage256(false)), max = Scale(values.Damage256(true));
        int pierce = Math.Max(0, ClassGear.Bonus(owner, "pierce-fire"));
        foreach (var place in Places(owner, point))
        {
            long id = ++_nextId;
            _heads.Add(id, new(id, group, place.Index, owner.Serial.Value, values.Rank, owner.Map.MapID, place.X, place.Y, point.Z,
                min, max, pierce, ((ulong)666 << 32) | (uint)Utility.RandomMinMax(1, int.MaxValue),
                now.AddMilliseconds(values.Eval("calc1") * 40), now.AddMilliseconds(25 * 40), default, 0));
        }
    }
    private static PlayerMobile Owner(uint serial, int map) => World.FindMobile((Serial)serial) is PlayerMobile { Alive: true, Deleted: false } player
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == DiabloClass.Sorceress
        && (player.Map == Map.Internal || player.Map.MapID == map && !SpellHelper.IsTown(player.Location, player)) ? player : null;
    public static void Cancel(Mobile owner)
    {
        var ids = new List<long>(); foreach (var head in _heads.Values) { if (head.Owner == owner.Serial.Value) { ids.Add(head.Id); } }
        foreach (long id in ids) { _heads.Remove(id); } ids.Clear();
        foreach (var bolt in _bolts.Values) { if (bolt.Owner == owner.Serial.Value) { ids.Add(bolt.Id); } }
        foreach (long id in ids) { _bolts.Remove(id); }
    }
    private static Mobile Target(PlayerMobile owner, HydraHead head)
    {
        var point = new Point3D((int)Math.Round(head.X), (int)Math.Round(head.Y), head.Z);
        Mobile first = null; double nearest = SkillGeometry.Tiles(25) * SkillGeometry.Tiles(25);
        foreach (var candidate in SkillCombat.Targets(owner, point, SkillGeometry.Tiles(25)))
        {
            double dx = candidate.X - head.X, dy = candidate.Y - head.Y, distance = dx * dx + dy * dy;
            if (distance < nearest) { first = candidate; nearest = distance; }
        }
        return first;
    }
    private static void Shoot(PlayerMobile owner, HydraHead head, Mobile target, DateTime now)
    {
        int dx = (int)Math.Round((target.X - head.X) * 2), dy = (int)Math.Round((target.Y - head.Y) * 2);
        if (dx == 0 && dy == 0) { dx = dy = 1; }
        var velocity = MissileVectors.Velocity(dx, dy, NecromancerSummons.Source("hydra", "Vel"));
        long id = ++_nextId;
        _bolts.Add(id, new(id, head.Id, head.Owner, head.Map, head.X, head.Y, head.Z, velocity.X, velocity.Y,
            0, head.Min256, head.Max256, head.Pierce, now.AddMilliseconds(NecromancerSummons.Source("hydra", "Range") * 40), now));
    }
    private static void Tick()
    {
        var now = TimedEffects.Now; _keys.Clear(); _keys.AddRange(_heads.Keys);
        foreach (long id in _keys)
        {
            if (!_heads.TryGetValue(id, out var head)) { continue; }
            var owner = Owner(head.Owner, head.Map);
            if (owner == null || owner.Map == Map.Internal && now > head.UntilUtc) { _heads.Remove(id); continue; }
            if (owner.Map == Map.Internal) { continue; }
            if (head.Target != 0 && head.ShotUtc <= now)
            {
                var target = World.FindMobile((Serial)head.Target);
                if (target is { Alive: true, Deleted: false } && target.Map == owner.Map && ClassCombat.IsHostile(owner, target))
                { Shoot(owner, head, target, now); }
                head = head with { Target = 0, ShotUtc = default };
            }
            if (head.DecisionUtc <= now)
            {
                if (head.UntilUtc < now) { _heads.Remove(id); continue; }
                var target = Target(owner, head); ulong seed = target == null ? head.Seed : 0x6AC690C5UL * (head.Seed & uint.MaxValue) + (head.Seed >> 32);
                if (Decide(false, target != null, target == null ? 25 : 0, (int)((uint)seed % 100)) == HydraDecision.Shoot)
                {
                    string key = "hydra" + (head.Index + 1);
                    head = head with { Seed = seed, Target = target.Serial.Value, ShotUtc = now.AddMilliseconds(_animation.ActionFrame * 40),
                        DecisionUtc = now.AddMilliseconds((_animation.AttackFrames + NecromancerSummons.Source(key, "aidel", 15)) * 40) };
                }
                else { head = head with { Seed = seed, DecisionUtc = now.AddMilliseconds(10 * 40) }; }
            }
            if (_heads.ContainsKey(id)) { _heads[id] = head; }
            if ((now.Ticks / TimeSpan.TicksPerMillisecond / 40) % 5 == 0)
            { Effects.SendLocationEffect(new Point3D((int)Math.Round(head.X), (int)Math.Round(head.Y), head.Z), owner.Map, 0x398C, 6, 10, 0x489, 0); } // Placeholder untargetable/nonblocking Hydra head.
        }
        _keys.Clear(); _keys.AddRange(_bolts.Keys);
        foreach (long id in _keys)
        {
            if (!_bolts.TryGetValue(id, out var bolt)) { continue; }
            var owner = Owner(bolt.Owner, bolt.Map);
            if (owner == null || bolt.UntilUtc <= now) { _bolts.Remove(id); continue; }
            if (owner.Map == Map.Internal || bolt.NextUtc > now) { continue; }
            double x = bolt.X + bolt.Vx, y = bolt.Y + bolt.Vy;
            var from = new Point3D((int)Math.Round(bolt.X), (int)Math.Round(bolt.Y), bolt.Z + 12); var to = new Point3D((int)Math.Round(x), (int)Math.Round(y), bolt.Z + 12);
            if (x < 0 || y < 0 || x >= owner.Map.Width || y >= owner.Map.Height || !owner.Map.LineOfSight(from, to)
                || bolt.Frame + 1 >= NecromancerSummons.Source("hydra", "Range")) { _bolts.Remove(id); continue; }
            Mobile first = null; double nearest = double.MaxValue;
            foreach (var target in SkillCombat.Targets(owner, new(to.X, to.Y, bolt.Z), 2))
            {
                if (CombatHitboxes.Intersect(bolt.X, bolt.Y, x, y, bolt.Z, target.Location, out double entry) && entry < nearest)
                { first = target; nearest = entry; }
            }
            if (first != null)
            {
                SorceressEffects.MissileHit(owner, first, "hydra");
                int damage = bolt.Max256 <= bolt.Min256 ? bolt.Min256 : Utility.RandomMinMax(bolt.Min256, bolt.Max256 - 1);
                SkillCombat.FireImpact(owner, first, damage, bolt.Pierce); _bolts.Remove(id); continue;
            }
            _bolts[id] = bolt with { X = x, Y = y, Frame = bolt.Frame + 1, NextUtc = bolt.NextUtc.AddMilliseconds(40) };
            Effects.SendLocationEffect(new(to.X, to.Y, bolt.Z), owner.Map, 0x36D4, 3, 10, 0x489, 0); // Placeholder Hydra bolt.
        }
    }
    public static HydraSave Snapshot() => new(new List<HydraHead>(_heads.Values).ToArray(), new List<HydraBolt>(_bolts.Values).ToArray());
    public static void Restore(HydraSave save)
    {
        if (save?.Heads == null || save.Bolts == null) { throw new InvalidDataException("Missing Hydra state"); }
        _heads.Clear(); _bolts.Clear(); _nextId = 0;
        foreach (var head in save.Heads)
        {
            if (head.Id <= 0 || head.Group <= 0 || head.Index is < 0 or > 2 || head.Rank <= 0 || head.Min256 < 0 || head.Max256 < head.Min256
                || head.Pierce < 0 || !double.IsFinite(head.X + head.Y) || !_heads.TryAdd(head.Id, head)) { throw new InvalidDataException("Invalid Hydra head"); }
            _nextId = Math.Max(_nextId, Math.Max(head.Id, head.Group));
        }
        foreach (var bolt in save.Bolts)
        {
            if (bolt.Id <= 0 || bolt.Min256 < 0 || bolt.Max256 < bolt.Min256 || bolt.Pierce < 0 || bolt.Frame is < 0 or >= 30
                || !double.IsFinite(bolt.X + bolt.Y + bolt.Vx + bolt.Vy) || _heads.ContainsKey(bolt.Id) || !_bolts.TryAdd(bolt.Id, bolt))
            { throw new InvalidDataException("Invalid Hydra bolt"); }
            _nextId = Math.Max(_nextId, bolt.Id);
        }
    }
}
