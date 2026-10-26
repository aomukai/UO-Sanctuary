// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class NecromancerCombat
{
    public static bool Implemented(CombatAbility ability) => NecromancerEffects.IsCurse(ability)
        || ability is CombatAbility.Teeth or CombatAbility.RaiseSkeleton or CombatAbility.SkeletonMastery
        or CombatAbility.BoneArmor or CombatAbility.PoisonDagger or CombatAbility.CorpseExplosion
        or CombatAbility.BoneSpear or CombatAbility.BoneSpirit or CombatAbility.PoisonExplosion or CombatAbility.PoisonNova
        or CombatAbility.ClayGolem or CombatAbility.BloodGolem or CombatAbility.FireGolem
        or CombatAbility.BoneWall or CombatAbility.BonePrison
        or CombatAbility.GolemMastery or CombatAbility.SummonResist or CombatAbility.RaiseSkeletalMage;
    public static bool Handles(CombatAbility ability) => ClassAbilities.Find(ability)?.Class == DiabloClass.Necromancer
        && ability != CombatAbility.RaiseSkeleton && Implemented(ability);
    public static bool CorpseTarget(CombatAbility ability) => ability is CombatAbility.CorpseExplosion
        or CombatAbility.PoisonExplosion or CombatAbility.RaiseSkeletalMage or CombatAbility.Revive;
    public static bool Available(PlayerMobile owner, CombatAbility ability) => Implemented(ability)
        && ClassAbilities.Find(ability) is { Passive: false, Class: DiabloClass.Necromancer }
        && ClassCreation.Profiles.TryGet(owner.Serial, out var profile) && profile.Class == DiabloClass.Necromancer
        && SkillMechanics.Rank(owner, ability) > 0;
    public static bool ValidTarget(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point)
    {
        if (owner.Map == null || owner.Map == Map.Internal || !owner.InRange(point, CombatProtocol.TeethRange)
            || point.X < 0 || point.Y < 0 || point.X >= owner.Map.Width || point.Y >= owner.Map.Height
            || !owner.InLOS(point)) { return false; }
        if (CorpseTarget(ability))
        {
            var corpse=World.FindItem(selected) as Corpse;
            return ability==CombatAbility.CorpseExplosion
                ?CorpseExplosionCasts.Supported(corpse) && CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)
                :SkeletonPets.Usable(owner,corpse);
        }
        if (ability == CombatAbility.Attract && selected == Serial.Zero) { return false; }
        if (ability == CombatAbility.BoneWall) { return BoneBarriers.Space(owner, point); }
        if (ability == CombatAbility.BonePrison)
        { return selected != Serial.Zero && ClassCombat.IsHostile(owner, World.FindMobile(selected)) && Server.Spells.SpellHelper.CheckTown(point, owner); }
        if (ability == CombatAbility.PoisonDagger)
        {
            var weapon = CombatTiming.Equipped(owner);
            return weapon != null && ClassGear.Base(weapon) is { } basis && EquipmentBalance.IsType(basis.Type, "knif")
                && ClassCombat.IsHostile(owner, World.FindMobile(selected)) && owner.InRange(point, CombatTiming.Range(owner, CombatAbility.BasicAttack));
        }
        return selected == Serial.Zero || World.FindMobile(selected) is { } target && ClassCombat.IsHostile(owner, target);
    }
    public static bool Begin(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point, long now)
    {
        if (ability == CombatAbility.BoneArmor) { selected = Serial.Zero; point = owner.Location; }
        var session = ClassCombat.For(owner);
        if (!Available(owner, ability) || !owner.Alive || !AlliedOverlap.Allowed(owner) || owner.Frozen || owner.Paralyzed
            || owner.Spell != null || HitRecovery.Active(owner, now) || session.PendingAbility != CombatAbility.None
            || now - session.NextAction < 0 || !ValidTarget(owner, ability, selected, point)
            || !NecromancerEffects.CanPay(owner, SkillMechanics.Values(owner, ability).Mana256)) { return false; }
        session.Timing = CombatTiming.For(owner, ability);
        session.PendingAbility = ability; session.PendingTarget = selected; session.PendingPoint = point;
        session.ActionOrigin = owner.Location; session.ActionMap = owner.Map;
        session.PendingWeapon = CombatTiming.Equipped(owner)?.Serial ?? Serial.Zero;
        session.NextAction = now + session.Timing.DurationMs; session.EffectAt = now + session.Timing.EffectMs;
        session.Animation = ability == CombatAbility.PoisonDagger ? (byte)10 : (byte)16;
        if (point.X != owner.X || point.Y != owner.Y) { owner.Direction = owner.GetDirectionTo(point); }
        owner.RevealingAction(); ClassCombat.SendAnimation(owner, session.Animation, session.Timing.DurationMs);
        owner.Animate(session.Animation, 5, 1, true, false, 0); ClassCombat.SendState(owner, session); return true;
    }
    public static bool Release(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point)
    {
        if (!Available(owner, ability) || !ValidTarget(owner, ability, selected, point)) { return false; }
        var values = SkillMechanics.Values(owner, ability);
        if (!NecromancerEffects.CanPay(owner, values.Mana256)) { return false; }
        if (ability is CombatAbility.BoneWall or CombatAbility.BonePrison)
        {
            if (!BoneBarriers.Create(owner, ability, point, values)) { return false; }
            return NecromancerEffects.Pay(owner, values.Mana256);
        }
        if (ability is CombatAbility.ClayGolem or CombatAbility.BloodGolem or CombatAbility.FireGolem or CombatAbility.RaiseSkeletalMage)
        {
            if (!NecromancerSummons.Create(owner, ability, selected, point)) { return false; }
            return NecromancerEffects.Pay(owner, values.Mana256);
        }
        if (!NecromancerEffects.Pay(owner, values.Mana256)) { return false; }
        owner.PlaySound(0x1E5); // Existing UO cast sound, pending skill-specific asset pass.
        if (NecromancerEffects.IsCurse(ability))
        {
            double radius = Math.Max(0.5, SkillGeometry.Tiles(values.Eval("aurarangecalc")));
            if (ability == CombatAbility.Attract)
            { return NecromancerEffects.Apply(owner, World.FindMobile(selected), ability, values.Rank); }
            foreach (var target in SkillCombat.Targets(owner, point, radius)) { NecromancerEffects.Apply(owner, target, ability, values.Rank); }
            return true;
        }
        if (ability == CombatAbility.BoneArmor) { NecromancerEffects.BoneArmor(owner, values); return true; }
        if (ability == CombatAbility.CorpseExplosion)
        {
            return CorpseExplosionCasts.Release(owner,(Corpse)World.FindItem(selected),values);
        }
        if (ability == CombatAbility.PoisonExplosion)
        {
            Consume((Corpse)World.FindItem(selected));
            // A persistent local cloud re-applies poison to enemies entering its footprint.
            var cloud = new PoisonCloud(owner, point, values);
            MissileFootprints.RegisterTransient(new("poisonexplosioncloud", owner.Map.MapID, point.X * 2, point.Y * 2, point.Z,
                TimedEffects.Now.AddMilliseconds(NecromancerSummons.Source("poisonexplosioncloud", "Range") * 40L)), () => cloud.Active);
            cloud.Start(); return true;
        }
        if (ability == CombatAbility.PoisonDagger)
        {
            var target = World.FindMobile(selected);
            var definition = SkillMechanics.For(ability);
            int rating = definition.Number("ToHit") + (values.Rank - 1) * definition.Number("LevToHit");
            using var attemptedMelee=PaladinThorns.Attempt(owner,target,false);
            if (Utility.RandomDouble() < ClassStats.HitChance(ClassStats.AttackRating(owner) * (100 + rating) / 100,
                ClassStats.Defense(target), ClassStats.Level(owner), ClassStats.Level(target)))
            {
                GearEffects.Strike(owner, target, Utility.RandomDouble());
                Poison(owner, target, values);
            }
            return true;
        }
        if (ability == CombatAbility.PoisonNova) { new Nova(owner, values).Start(); return true; }
        if (ability is CombatAbility.Teeth or CombatAbility.BoneSpear or CombatAbility.BoneSpirit)
        {
            var lastHits = new Dictionary<Serial, long>();
            if(ability==CombatAbility.Teeth)
            {
                // The same do8 lattice feeds Multiple Shot; damage/collision stay skill-specific.
                int sourceDx=(int)((point.X-owner.X)*SkillGeometry.SubtilesPerTile),sourceDy=(int)((point.Y-owner.Y)*SkillGeometry.SubtilesPerTile);
                foreach(var ray in FanGeometry.Create(sourceDx,sourceDy,(int)values.Eval("calc1"),(int)values.Eval("calc3"),(int)values.Eval("calc2")))
                {new BoneMissile(owner,ability,values,0,null,lastHits,ray).Start();}
                return true;
            }
            double dx = point.X - owner.X, dy = point.Y - owner.Y;
            double angle = Math.Atan2(dy, dx);
            new BoneMissile(owner, ability, values, angle, World.FindMobile(selected), lastHits).Start();
            return true;
        }
        throw new InvalidOperationException("Necromancer skill advertised without a release handler.");
    }
    internal static void Consume(Corpse corpse)
    { corpse.SanctuaryConsumed = true; corpse.Animated = true; ClassLoot.RemoveHarvestedIfEmpty(corpse); }
    internal static void Poison(Mobile owner, Mobile target, SkillValues values,bool missileDefense=false)
    {
        if(target is not{Alive:true,Deleted:false} || ClassCombat.Friendly(owner,target)){return;}
        double rate = values.Damage(false) + (values.Damage(true) - values.Damage(false)) * Utility.RandomDouble();
        if(missileDefense && ClassAvoidance.Missile(target)){return;}
        GearEffects.ApplyPoison(owner, target, rate * values.DurationFrames, Core.TickCount, values.DurationFrames * 40);
    }
    private sealed class PoisonCloud(PlayerMobile owner, Point3D point, SkillValues values) : SkillField(owner, NecromancerSummons.Source("poisonexplosioncloud", "Range"), DiabloClass.Necromancer)
    {
        private int _frame;
        protected override void Step()
        {
            if (_frame++ % Math.Max(1, NecromancerSummons.Source("poisonexplosioncloud", "Param1")) != 0) { return; }
            foreach (var target in SkillCombat.Targets(Owner, point, 2)) { Poison(Owner, target, values,missileDefense:true); }
            Effects.SendLocationEffect(point, Map, 0x374A, 10, 10, 0x3F, 0);
        }
    }
    private sealed class Nova(PlayerMobile owner, SkillValues values) : SkillField(owner, 31, DiabloClass.Necromancer)
    {
        private readonly Point3D _origin = owner.Location;
        private readonly HashSet<Serial> _hit = new();
        private double _radius;
        protected override void Step()
        {
            _radius += SkillGeometry.Velocity(12);
            foreach (var target in SkillCombat.Targets(Owner, _origin, _radius))
            { if (_hit.Add(target.Serial)) { Poison(Owner, target, values,missileDefense:true); target.FixedEffect(0x374A, 10, 10, 0x3F, 0); } }
        }
    }
    private sealed class BoneMissile(PlayerMobile owner, CombatAbility ability, SkillValues values, double angle, Mobile target, Dictionary<Serial, long> lastHits,FanRay? fan=null)
        : SkillField(owner, ability == CombatAbility.BoneSpirit ? 128 : ability == CombatAbility.BoneSpear ? 40 : 50, DiabloClass.Necromancer)
    {
        private double _x = owner.X, _y = owner.Y, _angle = angle, _travel;
        private readonly int _z = owner.Z;
        private Mobile _target = target;
        private readonly HashSet<Serial> _hits = new();
        private int _frame;
        protected override void Step()
        {
            if (ability == CombatAbility.BoneSpirit && _frame % 5 == 0)
            {
                if (!ClassCombat.IsHostile(Owner, _target) || !Owner.InLOS(_target))
                {
                    _target = null; double distance = double.MaxValue;
                    foreach (var candidate in SkillCombat.Targets(Owner, new((int)_x, (int)_y, _z), SkillGeometry.Tiles(15)))
                    { double next = Math.Pow(candidate.X - _x, 2) + Math.Pow(candidate.Y - _y, 2); if (next < distance) { distance = next; _target = candidate; } }
                }
                if (_target != null) { _angle = Math.Atan2(_target.Y - _y, _target.X - _x); }
            }
            _frame++;
            double speed = SkillGeometry.Velocity(ability == CombatAbility.BoneSpear ? 24 : ability == CombatAbility.Teeth ? 16 : 12);
            var velocity=fan is{} ray?FanGeometry.Velocity(ray,16):(Math.Cos(_angle)*speed,Math.Sin(_angle)*speed);
            var start = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            _x += velocity.Item1; _y += velocity.Item2;
            _travel += fan.HasValue?Math.Sqrt(velocity.Item1*velocity.Item1+velocity.Item2*velocity.Item2):speed;
            var end = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            if (_travel > CombatProtocol.TeethRange || !Map.LineOfSight(new Point3D(start.X, start.Y, start.Z + 12), new Point3D(end.X, end.Y, end.Z + 12))) { Stop(); return; }
            var collisions = new List<(Mobile Target, double Entry)>();
            foreach (var candidate in SkillCombat.Targets(Owner, end, 2))
            { if (!_hits.Contains(candidate.Serial) && CombatHitboxes.Intersect(start, end, candidate.Location, out double entry)) { collisions.Add((candidate, entry)); } }
            collisions.Sort((a, b) => a.Entry != b.Entry ? a.Entry.CompareTo(b.Entry) : a.Target.Serial.CompareTo(b.Target.Serial));
            if (_frame % 3 == 0) { Effects.SendLocationEffect(end, Map, 0x36E4, 3); }
            foreach (var collision in collisions)
            {
                _hits.Add(collision.Target.Serial);
                if (ability == CombatAbility.Teeth && lastHits.TryGetValue(collision.Target.Serial, out long previous) && Core.TickCount - previous < 160)
                { Stop(); return; }
                lastHits[collision.Target.Serial] = Core.TickCount;
                SkillCombat.Hit(Owner, collision.Target, values.Damage(false) + (values.Damage(true) - values.Damage(false)) * Utility.RandomDouble(), "mag",missileDefense:true);
                if (ability != CombatAbility.BoneSpear) { Stop(); return; }
            }
        }
    }
}
