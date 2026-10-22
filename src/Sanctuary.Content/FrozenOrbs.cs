using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public enum FrozenKind { Orb, Bolt, Nova }
public sealed record FrozenMissile(long Id, uint Owner, int Rank, int Map, FrozenKind Kind, double X, double Y, int Z,
    double Vx, double Vy, int Frame, int DirectionIndex, int VectorX, int VectorY, int Min256, int Max256, int ColdFrames,
    uint LastCollision, DateTime UntilUtc, DateTime NextUtc);

public sealed class FrozenOrbProfiles : GenericPersistence
{
    public FrozenOrbProfiles() : base("SanctuaryFrozenOrb", 117) { }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(FrozenOrbs.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Frozen Orb version"); }
        FrozenOrbs.Restore(JsonSerializer.Deserialize<FrozenMissile[]>(reader.ReadString()));
    }
}

public static class FrozenOrbs
{
    private static readonly int[] _xs = [30,29,29,28,27,26,24,23,21,19,16,14,11,8,5,2,0,-2,-5,-8,-11,-14,-16,-19,-21,-23,-24,-26,-27,-28,-29,-29,-30,-29,-29,-28,-27,-26,-24,-23,-21,-19,-16,-14,-11,-8,-5,-2,0,2,5,8,11,14,16,19,21,23,24,26,27,28,29,29];
    private static readonly int[] _ys = [0,2,5,8,11,14,16,19,21,23,24,26,27,28,29,29,30,29,29,28,27,26,24,23,21,19,16,14,11,8,5,2,0,-2,-5,-8,-11,-14,-16,-19,-21,-23,-24,-26,-27,-28,-29,-29,-30,-29,-29,-28,-27,-26,-24,-23,-21,-19,-16,-14,-11,-8,-5,-2];
    private static readonly Dictionary<long, FrozenMissile> _missiles = new();
    private static readonly List<long> _keys = new();
    private static long _nextId;
    public static FrozenOrbProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(40), Tick);
    public static Point2D Direction(int index) => new(_xs[index & 63], _ys[index & 63]);
    public static Point2D Turn(int x, int y) => new((x - y) / 2, (x + y) / 2); // Signed division truncates toward zero, as in the source.
    private static string Key(FrozenKind kind) => kind switch { FrozenKind.Orb => "frozenorb", FrozenKind.Bolt => "frozenorbbolt", _ => "frozenorbnova" };
    public static void Cast(PlayerMobile owner, Point3D aim, SkillValues values)
    {
        int dx = aim.X - owner.X, dy = aim.Y - owner.Y;
        if (dx == 0 && dy == 0) { dx = dy = 1; }
        var velocity = MissileVectors.Velocity(dx, dy, NecromancerSummons.Source("frozenorb", "Vel")); var now = TimedEffects.Now;
        long id = ++_nextId;
        _missiles.Add(id, new(id, owner.Serial.Value, values.Rank, owner.Map.MapID, FrozenKind.Orb, owner.X, owner.Y, owner.Z,
            velocity.X, velocity.Y, 0, 0, 0, 0, 0, 0, 0, 0, now.AddMilliseconds(NecromancerSummons.Source("frozenorb", "Range") * 40), now));
    }
    private static void Child(PlayerMobile owner, FrozenMissile orb, FrozenKind kind, int index, DateTime now)
    {
        var direction = Direction(index); var velocity = MissileVectors.Velocity(direction.X, direction.Y, NecromancerSummons.Source(Key(kind), "Vel"));
        var values = SkillMechanics.Values(owner, CombatAbility.FrozenOrb, orb.Rank); int potion = CombatPotions.Bonus(owner, "magical-penetration");
        int Scale(int damage) => (int)Math.Clamp(NamedElements.Enhance(owner, damage * (100 + potion) / 100.0, "cold"), 0, int.MaxValue);
        long id = ++_nextId;
        _missiles.Add(id, new(id, owner.Serial.Value, orb.Rank, orb.Map, kind,
            Math.Floor(orb.X * 2) / 2, Math.Floor(orb.Y * 2) / 2, orb.Z, velocity.X, velocity.Y, 0, index,
            direction.X, direction.Y, Scale(values.Damage256(false)), Scale(values.Damage256(true)), values.DurationFrames, 0,
            now.AddMilliseconds(NecromancerSummons.Source(Key(kind), "Range") * 40), now));
    }
    private static void Burst(PlayerMobile owner, FrozenMissile orb, DateTime now)
    {
        int step = Math.Max(1, NecromancerSummons.Source("frozenorb", "sHitPar1"));
        for (int index = 0; index < 64; index += step) { Child(owner, orb, FrozenKind.Nova, index, now); }
    }
    private static PlayerMobile Owner(FrozenMissile missile) => World.FindMobile((Serial)missile.Owner) is PlayerMobile { Alive: true, Deleted: false } player
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == DiabloClass.Sorceress
        && (player.Map == Map.Internal || player.Map.MapID == missile.Map && !SpellHelper.IsTown(player.Location, player)) ? player : null;
    public static void Cancel(Mobile owner)
    {
        var ids = new List<long>(); foreach (var (id, missile) in _missiles) { if (missile.Owner == owner.Serial.Value) { ids.Add(id); } }
        foreach (long id in ids) { _missiles.Remove(id); }
    }
    private static void Tick()
    {
        var now=TimedEffects.Now;_keys.Clear();_keys.AddRange(_missiles.Keys);
        foreach(long id in _keys)
        {
            if(!_missiles.TryGetValue(id,out var missile)){continue;}
            var owner=Owner(missile);
            if(owner==null){_missiles.Remove(id);continue;}
            if(owner.Map==Map.Internal)
            {if(now>=missile.UntilUtc){_missiles.Remove(id);}continue;}
            // A delayed callback still processes each due source frame before natural expiry.
            // Work is bounded by the missile's remaining source Range, not elapsed wall time.
            while(_missiles.TryGetValue(id,out missile) && missile.NextUtc<=now && missile.NextUtc<missile.UntilUtc)
            {Step(id,missile,owner,missile.NextUtc);}
            if(_missiles.TryGetValue(id,out missile) && now>=missile.UntilUtc)
            {
                _missiles.Remove(id);
                if(missile.Kind==FrozenKind.Orb){Burst(owner,missile,missile.UntilUtc);}
            }
        }
    }
    private static void Step(long id,FrozenMissile missile,PlayerMobile owner,DateTime now)
    {
            if (missile.Kind == FrozenKind.Orb)
            {
                int interval = Math.Max(1, NecromancerSummons.Source("frozenorb", "Param1"));
                if (missile.Frame % interval == 0)
                {
                    Child(owner, missile, FrozenKind.Bolt, missile.DirectionIndex, now);
                    missile = missile with { DirectionIndex = (missile.DirectionIndex + NecromancerSummons.Source("frozenorb", "Param2")) & 63 };
                }
            }
            else if (missile.Kind == FrozenKind.Nova && missile.Frame < NecromancerSummons.Source("frozenorbnova", "Param1")
                && missile.Frame % Math.Max(1, NecromancerSummons.Source("frozenorbnova", "Param2")) == 0)
            {
                var vector = Turn(missile.VectorX, missile.VectorY);
                var velocity = MissileVectors.Velocity(vector.X, vector.Y, NecromancerSummons.Source("frozenorbnova", "Vel"));
                missile = missile with { VectorX = vector.X, VectorY = vector.Y, Vx = velocity.X, Vy = velocity.Y };
            }
            double x = missile.X + missile.Vx, y = missile.Y + missile.Vy;
            var from = new Point3D((int)Math.Round(missile.X), (int)Math.Round(missile.Y), missile.Z + 12);
            var to = new Point3D((int)Math.Round(x), (int)Math.Round(y), missile.Z + 12);
            if (x < 0 || y < 0 || x >= owner.Map.Width || y >= owner.Map.Height || !owner.Map.LineOfSight(from, to))
            { _missiles.Remove(id); return; } // A terrain kill has a nonzero source frame and produces no terminal nova.
            int frame = missile.Frame + 1;
            if (frame >= NecromancerSummons.Source(Key(missile.Kind), "Range"))
            {
                _missiles.Remove(id);
                if (missile.Kind == FrozenKind.Orb) { Burst(owner, missile with { X = x, Y = y }, now); }
                return; // Source expiry precedes unit collision on the zero frame.
            }
            Mobile first = null; double nearest = double.MaxValue;
            var point = new Point3D(to.X, to.Y, missile.Z);
            foreach (var candidate in SkillCombat.Targets(owner, point, 2))
            {
                if (candidate.Serial.Value != missile.LastCollision
                    && CombatHitboxes.Intersect(missile.X, missile.Y, x, y, missile.Z, candidate.Location, out double entry) && entry < nearest)
                { first = candidate; nearest = entry; }
            }
            if (first != null)
            {
                SorceressEffects.MissileHit(owner, first, Key(missile.Kind));
                if (missile.Kind != FrozenKind.Orb)
                {
                    int damage = missile.Max256 <= missile.Min256 ? missile.Min256 : Utility.RandomMinMax(missile.Min256, missile.Max256 - 1);
                    SkillCombat.ColdImpact(owner, first, damage, missile.ColdFrames); _missiles.Remove(id); return;
                }
                missile = missile with { LastCollision = first.Serial.Value }; // Orb callback only; its source row carries no direct damage/chill.
            }
            if (_missiles.ContainsKey(id))
            { _missiles[id] = missile with { X = x, Y = y, Frame = frame, NextUtc = missile.NextUtc.AddMilliseconds(40) }; }
            Effects.SendLocationEffect(point, owner.Map, missile.Kind == FrozenKind.Orb ? 0x375A : 0x36E4, 3, 10, 0x480, 0); // Placeholder orb/ice shard.
    }
    public static FrozenMissile[] Snapshot() => new List<FrozenMissile>(_missiles.Values).ToArray();
    public static void Restore(FrozenMissile[] saved)
    {
        if (saved == null) { throw new InvalidDataException("Missing Frozen Orb state"); }
        _missiles.Clear(); _nextId = 0;
        foreach (var missile in saved)
        {
            if (missile.Id <= 0 || missile.Rank <= 0 || missile.Kind is < FrozenKind.Orb or > FrozenKind.Nova
                || missile.Frame < 0 || missile.Frame >= NecromancerSummons.Source(Key(missile.Kind), "Range") || missile.DirectionIndex is < 0 or > 63
                || missile.Min256 < 0 || missile.Max256 < missile.Min256 || missile.ColdFrames < 0
                || !double.IsFinite(missile.X + missile.Y + missile.Vx + missile.Vy) || !_missiles.TryAdd(missile.Id, missile))
            { throw new InvalidDataException("Invalid Frozen Orb missile"); }
            _nextId = Math.Max(_nextId, missile.Id);
        }
    }
}
