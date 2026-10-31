// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class BoneBarriers
{
    public static void Configure() => ClassCombatHooks.ProjectileTarget = (source, target) => Intercept(source, target) ?? target;
    // Legacy prison anchors are subtiles; raster connectors close gaps introduced by the provisional tile mapping.
    private static readonly (int X, int Y)[] _prison = [(-1,-4),(1,-4),(3,-3),(4,-1),(4,1),(3,3),
        (1,4),(-1,4),(-3,3),(-4,1),(-4,-1),(-3,-3)];
    public static bool Space(PlayerMobile owner, Point3D point) => owner.Map is { } map && map != Map.Internal
        && point.X >= 0 && point.Y >= 0 && point.X < map.Width && point.Y < map.Height
        && SpellHelper.CheckTown(point, owner) && Teleportation.Space(map, point);
    public static bool Create(PlayerMobile owner, CombatAbility ability, Point3D point, SkillValues values)
    {
        if (ability == CombatAbility.BoneWall)
        {
            if (!Space(owner, point)) { return false; }
            var root = BoneBarrierSegment.Spawn(owner, ability, point, values, null);
            double dx = point.X - owner.X, dy = point.Y - owner.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            root.Grow(length == 0 ? 0 : -dy / length, length == 0 ? 1 : dx / length, (int)values.Eval("calc2") / 2);
            return true;
        }
        var anchors = new List<Point3D>();
        foreach (var (x, y) in _prison)
        {
            var cell = new Point3D(point.X + Round(SkillGeometry.Tiles(x)), point.Y + Round(SkillGeometry.Tiles(y)), point.Z);
            if (!anchors.Contains(cell)) { anchors.Add(cell); }
        }
        BoneBarrierSegment first = null;
        var created = new List<BoneBarrierSegment>();
        foreach (var anchor in anchors)
        {
            if (!Space(owner, anchor)) { continue; }
            var segment = BoneBarrierSegment.Spawn(owner, ability, anchor, values, first);
            first ??= segment; created.Add(segment);
        }
        if (first == null) { return false; }
        for (int i = 0; i < anchors.Count; i++)
        {
            var a = anchors[i]; var b = anchors[(i + 1) % anchors.Count];
            int steps = Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
            for (int j = 1; j < steps; j++)
            {
                var cell = new Point3D(a.X + Round((b.X - a.X) * j / (double)steps), a.Y + Round((b.Y - a.Y) * j / (double)steps), a.Z);
                var host = created.Find(s => s.Location == a) ?? created.Find(s => s.Location == b);
                if (host != null && Space(owner, cell)) { host.AddTile(cell); }
            }
        }
        return true;
    }
    internal static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    public static BoneBarrierSegment Intercept(Mobile source, Mobile target)
    {
        if (source is not { Deleted: false, Map: { } map } || target == null || target.Map != map || target is BoneBarrierSegment) { return null; }
        BoneBarrierSegment nearest = null; double first = double.MaxValue;
        var center = new Point3D((source.X + target.X) / 2, (source.Y + target.Y) / 2, source.Z);
        int radius = (int)Math.Ceiling(source.GetDistanceToSqrt(target) / 2) + 2;
        foreach (var tile in map.GetItemsInRange<BoneBarrierTile>(center, radius))
        {
            if (tile.Segment is not { Alive: true, Deleted: false } segment || ClassCombat.Friendly(source, segment)
                || Math.Abs(tile.Z - target.Z) >= 16 || !CombatHitboxes.Intersect(source.Location, target.Location, tile.Location, out double entry)
                || entry <= 0 || entry >= first) { continue; }
            nearest = segment; first = entry;
        }
        return nearest;
    }
}

[SerializationGenerator(0)]
public partial class BoneBarrierSegment : BaseCreature
{
    [SerializableField(0, setter: "private")] private CombatAbility _ability;
    [SerializableField(1, setter: "private")] private DateTime _expiresUtc;
    [SerializableField(2, setter: "private")] private BoneBarrierSegment _root;
    [SerializableField(3, setter: "private")] private bool _child;
    [SerializableField(4, setter: "private")] private int _sourceLevel;
    [SerializableField(5, setter: "private")] private int _sourceDefense;
    [SerializableField(6, setter: "private")] private List<BoneBarrierTile> _tiles = new();
    [SerializableField(7, setter: "private")] private double _growX;
    [SerializableField(8, setter: "private")] private double _growY;
    [SerializableField(9, setter: "private")] private int _armLimit;
    [SerializableField(10, setter: "private")] private int _left;
    [SerializableField(11, setter: "private")] private int _right;
    [SerializableField(12, setter: "private")] private int _growthFrame;
    [SerializableField(13, setter: "private")] private int _life;
    private TimerExecutionToken _timer;
    private long _lastRegen = Core.TickCount, _regenFraction;
    public override bool Commandable => false;
    public override bool IsBondable => false;
    public override bool IsDispellable => false;
    public override bool CanRegenHits => false;
    public override bool CanRegenMana => false;
    public override bool DeleteCorpseOnDeath => true;
    public override bool BardImmune => true;
    public override bool BleedImmune => true;
    public override bool IsEnemy(Mobile mobile) => false;
    [Constructible]
    public BoneBarrierSegment() : base(AIType.AI_Melee, FightMode.None, 0)
    {
        Name = "bone barrier [placeholder]"; Body = 50; Hue = 0x481;
        CantWalk = true; Summoned = true; ControlSlots = 0; NoKillAwards = true;
        SetStr(1); SetDex(0); SetInt(0); SetMana(0); SetStam(0); SetDamage(0, 0);
        AIObject?.AITimer.Stop();
    }
    internal static BoneBarrierSegment Spawn(PlayerMobile owner, CombatAbility ability, Point3D point, SkillValues values, BoneBarrierSegment root)
    {
        var prepared=root is BoneBarrierBody parent?parent.Prepared:root==null?BoneBarrierBirth.Capture(values,SummonDifficulties.For(owner.Map)):null;
        int life = root?.Life ?? (prepared!=null?(prepared.Numbers.Life256+255)/256:Math.Max(1, (int)((long)NecromancerSummons.Source("bonewall", "minHP") * (100 + values.Eval("calc1")) / 100)));
        BoneBarrierSegment segment=prepared==null?new BoneBarrierSegment():new BoneBarrierBody();
        segment._ability=ability;segment._root=root;segment._child=root!=null;segment._life=life;
        segment._sourceLevel=root?.SourceLevel??prepared?.Numbers.Level??ClassStats.Level(owner);
        segment._sourceDefense=root?.SourceDefense??prepared?.Numbers.Defense??NecromancerSummons.Source("bonewall","AC");
        segment._expiresUtc=root?.ExpiresUtc??TimedEffects.Now.AddMilliseconds(values.Param(2)*40);
        segment.SummonMaster=owner;
        if(segment is BoneBarrierBody body){body.Prepare(prepared);}
        segment.SummonEnd = segment._expiresUtc;
        segment.SetHits(life); segment.Hits = life;
        segment.SetResistance(ResistanceType.Physical, NecromancerSummons.Source("bonewall", "ResDm"));
        segment.SetResistance(ResistanceType.Cold, NecromancerSummons.Source("bonewall", "ResCo"));
        segment.SetResistance(ResistanceType.Poison, NecromancerSummons.Source("bonewall", "ResPo"));
        if(prepared!=null){segment.ApplyBirth(prepared.Numbers);SkillHealth.SetBalance256(segment,prepared.Numbers.Life256);}
        segment.MoveToWorld(point, owner.Map); segment.AddTile(point); segment.Start(); return segment;
    }
    internal void ApplyBirth(BarrierBirthStats stats)
    {
        _life=(stats.Life256+255)/256;_sourceLevel=stats.Level;_sourceDefense=stats.Defense;SetHits(_life);
        SetResistance(ResistanceType.Physical,stats.Physical);SetResistance(ResistanceType.Fire,stats.Fire);
        SetResistance(ResistanceType.Cold,stats.Cold);SetResistance(ResistanceType.Poison,stats.Poison);SetResistance(ResistanceType.Energy,stats.Lightning);
    }
    internal void AddTile(Point3D point)
    {
        var tile = new BoneBarrierTile(this); _tiles.Add(tile); tile.MoveToWorld(point, Map);
    }
    internal void Grow(double x, double y, int count) { _growX = x; _growY = y; _armLimit = count; }
    private void Start()
    {
        _timer.Cancel(); ResetRecovery();
        Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(40), Tick, out _timer);
    }
    private void Tick()
    {
        if (Deleted) { _timer.Cancel(); return; }
        if (!Alive || TimedEffects.Now >= _expiresUtc || _child && _root is not { Alive: true, Deleted: false }
            || SummonMaster is not PlayerMobile { Deleted: false } || Map == null || Map == Map.Internal) { Delete(); return; }
        RecoverLife(Core.TickCount);
        if (_child || _ability != CombatAbility.BoneWall || _left >= _armLimit && _right >= _armLimit) { return; }
        _growthFrame++;
        double distance = _growthFrame * SkillGeometry.Velocity(NecromancerSummons.Source("bonewallmaker", "Vel"));
        if (_left < _armLimit && distance >= _left + 1) { Extend(-1, ref _left); }
        if (_right < _armLimit && distance >= _right + 1) { Extend(1, ref _right); }
    }
    protected virtual void ResetRecovery(){_lastRegen=Core.TickCount;_regenFraction=0;}
    protected virtual void RecoverLife(long now)
    {
        long elapsed = Math.Max(0, now - _lastRegen); _lastRegen = now;
        if (Hits < HitsMax && TimedEffects.CanRegenerateHits(this))
        {
            int regen = (NecromancerSummons.Source("bonewall", "minHP") * 256 * NecromancerSummons.Source("bonewall", "DamageRegen")) >> 12;
            _regenFraction += elapsed * regen * 25;
            int healing = (int)Math.Min(HitsMax - Hits, _regenFraction / 256000); _regenFraction %= 256000; Hits += healing;
        }
        else { _regenFraction = 0; }
    }
    private void Extend(int sign, ref int count)
    {
        count++;
        var point = new Point3D(X + BoneBarriers.Round(sign * count * _growX), Y + BoneBarriers.Round(sign * count * _growY), Z);
        if (SummonMaster is not PlayerMobile owner || !BoneBarriers.Space(owner, point)) { count = _armLimit; return; }
        Spawn(owner, _ability, point, null, this);
    }
    public override void GenerateLoot(bool spawning) { }
    public override bool OnBeforeDeath() { Delete(); return false; }
    public override void OnDelete()
    {
        _timer.Cancel();
        foreach (var tile in _tiles) { tile?.Delete(); }
        _tiles.Clear();
        if (!_child && Map is { } map && map != Map.Internal)
        {
            var children = new List<BoneBarrierSegment>();
            foreach (var segment in map.GetMobilesInRange<BoneBarrierSegment>(Location, 16))
            { if (segment._root == this) { children.Add(segment); } }
            foreach (var segment in children) { segment.Delete(); }
        }
        _root = null; base.OnDelete();
    }
    [AfterDeserialization]
    private void AfterLoad()
    {
        AIObject?.AITimer.Stop(); _lastRegen = Core.TickCount;
        if (TimedEffects.Now >= _expiresUtc) { Timer.StartTimer(TimeSpan.Zero, Delete); }
        else { Start(); }
    }
}

[SerializationGenerator(0)]
public partial class BoneBarrierTile : Item
{
    [SerializableField(0, setter: "private")] private BoneBarrierSegment _segment;
    public override bool BlocksFit => true;
    [Constructible] public BoneBarrierTile() : base(0x21A4) { Movable = false; Name = "bone barrier collision [placeholder]"; }
    public BoneBarrierTile(BoneBarrierSegment segment) : this() { _segment = segment; }
    public override bool OnMoveOver(Mobile mobile) => false;
    public override void OnDelete() { _segment = null; base.OnDelete(); }
    [AfterDeserialization] private void AfterLoad()
    {
        if (_segment is not { Deleted: false }) { Timer.StartTimer(TimeSpan.Zero, Delete); }
    }
}
