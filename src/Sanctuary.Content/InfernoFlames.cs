using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record InfernoFlame(long Id, uint Owner, int Rank, int Map, double X, double Y, int Z, double Vx, double Vy,
    int Frame, int Frames, int Min256, int Max256, uint LastCollision, DateTime UntilUtc, DateTime NextUtc);

public sealed class InfernoFlameProfiles : GenericPersistence
{
    public InfernoFlameProfiles() : base("SanctuaryInfernoFlames", 119) { }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(InfernoFlames.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Inferno flame version"); }
        InfernoFlames.Restore(JsonSerializer.Deserialize<InfernoFlame[]>(reader.ReadString()));
    }
}

// Confirmed projectile primitive only. Inferno remains unavailable until channel payment/re-entry is established.
public static class InfernoFlames
{
    private static readonly Dictionary<long, InfernoFlame> _flames = new();
    private static readonly List<long> _keys = new();
    private static long _nextId;
    public static InfernoFlameProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(40), Tick);
    public static int StartThreshold256 => SkillMechanics.For(CombatAbility.Inferno).Number("startmana") * 256;
    public static bool StartAllowed(int mana256, bool stateActive) => stateActive || mana256 >= StartThreshold256;
    public static void Launch(PlayerMobile owner, Point3D aim, SkillValues values)
    {
        int dx = aim.X - owner.X, dy = aim.Y - owner.Y;
        if (dx == 0 && dy == 0) { dx = dy = 1; }
        var velocity = MissileVectors.Velocity(dx, dy, NecromancerSummons.Source("infernoflame1", "Vel"));
        int frames = Math.Max(1, (int)values.Eval("calc1")); var now = TimedEffects.Now; int potion = CombatPotions.Bonus(owner, "magical-penetration");
        int Scale(int damage) => (int)Math.Clamp(NamedElements.Enhance(owner, damage * (100 + potion) / 100.0, "fire"), 0, int.MaxValue);
        long id = ++_nextId;
        _flames.Add(id, new(id, owner.Serial.Value, values.Rank, owner.Map.MapID, owner.X, owner.Y, owner.Z, velocity.X, velocity.Y,
            0, frames, Scale(values.Damage256(false)), Scale(values.Damage256(true)), 0, now.AddMilliseconds(frames * 40), now));
        // Resource payment and emission cadence belong to the still-gated channel caller, not this launched missile.
    }
    private static PlayerMobile Owner(InfernoFlame flame) => World.FindMobile((Serial)flame.Owner) is PlayerMobile { Alive: true, Deleted: false } player
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == DiabloClass.Sorceress
        && (player.Map == Map.Internal || player.Map.MapID == flame.Map && !SpellHelper.IsTown(player.Location, player)) ? player : null;
    public static void Cancel(Mobile owner)
    {
        var ids = new List<long>(); foreach (var flame in _flames.Values) { if (flame.Owner == owner.Serial.Value) { ids.Add(flame.Id); } }
        foreach (long id in ids) { _flames.Remove(id); }
    }
    private static void Tick()
    {
        var now = TimedEffects.Now; _keys.Clear(); _keys.AddRange(_flames.Keys);
        foreach (long id in _keys)
        {
            if (!_flames.TryGetValue(id, out var flame)) { continue; }
            var owner = Owner(flame);
            if (owner == null || flame.UntilUtc <= now) { _flames.Remove(id); continue; }
            if (owner.Map == Map.Internal || flame.NextUtc > now) { continue; }
            double x = flame.X + flame.Vx, y = flame.Y + flame.Vy;
            var from = new Point3D((int)Math.Round(flame.X), (int)Math.Round(flame.Y), flame.Z + 12);
            var to = new Point3D((int)Math.Round(x), (int)Math.Round(y), flame.Z + 12);
            if (x < 0 || y < 0 || x >= owner.Map.Width || y >= owner.Map.Height || !owner.Map.LineOfSight(from, to) || flame.Frame + 1 >= flame.Frames)
            { _flames.Remove(id); continue; }
            Mobile first = null; double nearest = double.MaxValue;
            foreach (var target in SkillCombat.Targets(owner, new(to.X, to.Y, flame.Z), 2))
            {
                if (target.Serial.Value != flame.LastCollision
                    && CombatHitboxes.Intersect(flame.X, flame.Y, x, y, flame.Z, target.Location, out double entry) && entry < nearest)
                { first = target; nearest = entry; }
            }
            uint last = flame.LastCollision;
            if (first != null)
            {
                last = first.Serial.Value; SorceressEffects.MissileHit(owner, first, "infernoflame1");
                int damage = flame.Max256 <= flame.Min256 ? flame.Min256 : Utility.RandomMinMax(flame.Min256, flame.Max256 - 1);
                SkillCombat.FireImpact(owner, first, damage, damageRate: NecromancerSummons.Source("infernoflame1", "DamageRate"));
            }
            if (_flames.ContainsKey(id))
            { _flames[id] = flame with { X = x, Y = y, Frame = flame.Frame + 1, LastCollision = last, NextUtc = flame.NextUtc.AddMilliseconds(40) }; }
            Effects.SendLocationEffect(new(to.X, to.Y, flame.Z), owner.Map, 0x36D4, 3, 10, 0x489, 0); // Placeholder Inferno flame.
        }
    }
    public static InfernoFlame[] Snapshot() => new List<InfernoFlame>(_flames.Values).ToArray();
    public static void Restore(InfernoFlame[] saved)
    {
        if (saved == null) { throw new InvalidDataException("Missing Inferno flames"); }
        _flames.Clear(); _nextId = 0;
        foreach (var flame in saved)
        {
            if (flame.Id <= 0 || flame.Rank <= 0 || flame.Frame < 0 || flame.Frames < 1 || flame.Frame >= flame.Frames
                || flame.Min256 < 0 || flame.Max256 < flame.Min256 || !double.IsFinite(flame.X + flame.Y + flame.Vx + flame.Vy) || !_flames.TryAdd(flame.Id, flame))
            { throw new InvalidDataException("Invalid Inferno flame"); }
            _nextId = Math.Max(_nextId, flame.Id);
        }
    }
}
