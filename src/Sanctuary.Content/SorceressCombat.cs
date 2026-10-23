// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class SorceressCombat
{
    public static bool Implemented(CombatAbility a) => a is CombatAbility.FireBolt or CombatAbility.Fireball
        or CombatAbility.IceBolt or CombatAbility.IceBlast or CombatAbility.GlacialSpike or CombatAbility.Nova
        or CombatAbility.FrostNova or CombatAbility.StaticField or CombatAbility.Warmth
        or CombatAbility.FrozenArmor or CombatAbility.ShiverArmor or CombatAbility.ChillingArmor or CombatAbility.EnergyShield
        or CombatAbility.Telekinesis or CombatAbility.ThunderStorm or CombatAbility.Lightning or CombatAbility.ChainLightning or CombatAbility.Enchant or CombatAbility.ChargedBolt
        or CombatAbility.Blaze or CombatAbility.FireWall or CombatAbility.Meteor or CombatAbility.Blizzard or CombatAbility.FrozenOrb or CombatAbility.Hydra
        or CombatAbility.FireMastery or CombatAbility.LightningMastery or CombatAbility.ColdMastery or CombatAbility.Teleport;
    public static bool Handles(CombatAbility a) => Implemented(a) && a != CombatAbility.Teleport && ClassAbilities.Find(a) is { Passive: false };
    public static bool Available(PlayerMobile owner, CombatAbility a) => Handles(a)
        && ClassCreation.Profiles.TryGet(owner.Serial, out var profile) && profile.Class == DiabloClass.Sorceress && SkillMechanics.Rank(owner, a) > 0;
    public static int Passive(PlayerMobile owner, CombatAbility ability)
    {
        if (!ClassCreation.Profiles.TryGet(owner.Serial, out var profile) || profile.Class != DiabloClass.Sorceress
            || SkillMechanics.Rank(owner, ability) <= 0) { return 0; }
        var row = SkillMechanics.For(ability); int rank = SkillMechanics.Rank(owner, ability);
        return (int)new SkillValues(row, key => key == row.SourceKey ? rank : 0, _ => 0, profile.Level).Eval("passivecalc1");
    }
    public static bool ValidTarget(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point)
    {
        if (ability == CombatAbility.Enchant)
        {
            if (selected == Serial.Zero) { return owner.Map != null && owner.Map != Map.Internal; }
            var actor = World.FindMobile(selected);
            return actor is { Alive: true, Deleted: false } && actor.Map == owner.Map && owner.InRange(actor, CombatProtocol.TeethRange)
                && owner.InLOS(actor) && (actor == owner || ClassCombat.Friendly(owner, actor) || ClassCombat.IsHostile(owner, actor));
        }
        if (ClassAbilities.SelfTarget(ability))
        { return ability is not (CombatAbility.ThunderStorm or CombatAbility.Blaze) || Server.Spells.SpellHelper.CheckTown(owner.Location, owner); }
        if (ability is CombatAbility.FireWall or CombatAbility.Meteor or CombatAbility.Blizzard or CombatAbility.FrozenOrb or CombatAbility.Hydra or CombatAbility.ChainLightning && (!Server.Spells.SpellHelper.CheckTown(owner.Location, owner)
            || !Server.Spells.SpellHelper.CheckTown(point, owner))) { return false; }
        if (ability == CombatAbility.Telekinesis && (selected == Serial.Zero || !ClassCombat.IsHostile(owner, World.FindMobile(selected))
            || !Server.Spells.SpellHelper.CheckTown(point, owner))) { return false; }
        return owner.Map != null && owner.Map != Map.Internal && owner.InRange(point, CombatProtocol.TeethRange) && owner.InLOS(point)
            && point.X >= 0 && point.Y >= 0 && point.X < owner.Map.Width && point.Y < owner.Map.Height
            && (selected == Serial.Zero || ClassCombat.IsHostile(owner, World.FindMobile(selected)));
    }
    public static bool Begin(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point, long now)
        => SkillCombat.Begin(owner, ability, selected, point, now, ValidTarget);
    public static bool Release(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point)
    {
        if (!Available(owner, ability) || !ValidTarget(owner, ability, selected, point) || !FireGround.Ready(owner, ability)) { return false; }
        var values = SkillMechanics.Values(owner, ability);
        if (ability == CombatAbility.Hydra && !Hydras.CanPlace(owner, point)) { return false; }
        if (!NecromancerEffects.Pay(owner, values.Mana256)) { return false; }
        FireGround.Delay(owner, ability);
        owner.PlaySound(0x1E5); // Placeholder cast sound.
        if (SorceressEffects.IsArmor(ability)) { SorceressEffects.CastArmor(owner, ability, values); return true; }
        if (ability == CombatAbility.EnergyShield) { SorceressEffects.CastShield(owner, values); return true; }
        if (ability == CombatAbility.ThunderStorm) { ThunderStorm.Cast(owner, values); return true; }
        if (ability == CombatAbility.Blaze) { FireGround.CastBlaze(owner, values); return true; }
        if (ability == CombatAbility.FireWall) { FireGround.CastWall(owner, point, values); return true; }
        if (ability == CombatAbility.Meteor) { FireGround.CastMeteor(owner, point, values); return true; }
        if (ability == CombatAbility.Blizzard) { Blizzards.Cast(owner, point, values); return true; }
        if (ability == CombatAbility.FrozenOrb) { FrozenOrbs.Cast(owner, point, values); return true; }
        if (ability == CombatAbility.Hydra) { Hydras.Cast(owner, point, values); return true; }
        if (ability == CombatAbility.ChainLightning) { ChainLightning.Cast(owner, point, values); return true; }
        if (ability == CombatAbility.Enchant)
        {
            var target = World.FindMobile(selected);
            SorceressEffects.CastEnchant(owner, target == owner || ClassCombat.Friendly(owner, target) ? target : owner, values);
            return true;
        }
        if (ability == CombatAbility.Telekinesis)
        {
            var target = World.FindMobile(selected);
            double damage = values.Damage(false) + Utility.RandomDouble() * (values.Damage(true) - values.Damage(false));
            SkillCombat.HitParts(owner, target, new(0, 0, 0, damage, 0), recover: !NamedCombat.Elite(target), spell: true);
            if (target is { Alive: true, Deleted: false } && !NamedCombat.Elite(target) && Utility.Random(100) < values.Param(2))
            { CombatDisplacement.TryStep(target, (Direction)(((int)target.GetDirectionTo(owner) + 4) & 7)); }
            return true;
        }
        if (ability == CombatAbility.StaticField)
        {
            foreach (var target in SkillCombat.Targets(owner, owner.Location, SkillGeometry.Tiles(values.Eval("aurarangecalc"))))
            {
                int percent = target is BaseCreature creature ? MonsterVariants.Difficulty(creature) switch
                { MonsterDifficulty.Nightmare => 33, MonsterDifficulty.Hell => 50, _ => 0 } : 0;
                int floor = Math.Max(1, target.HitsMax * percent / 100);
                // Static uses current life and lightning resistance; it cannot finish a monster.
                int resist = NamedElements.Resistance(owner, target, "ltng", target.EnergyResistance);
                double damage = target.Hits * values.Param(4) / 100.0 * Math.Clamp(100 - resist, 0, 200) / 100.0;
                int loss = Math.Min(Math.Max(0, target.Hits - floor), (int)damage);
                if (loss > 0) { owner.DoHarmful(target); target.Damage(loss, owner); }
            }
            return true;
        }
        if (ability is CombatAbility.Nova or CombatAbility.FrostNova)
        { new Nova(owner, ability, values).Start(); return true; }
        double angle = Math.Atan2(point.Y - owner.Y, point.X - owner.X);
        if (ability == CombatAbility.ChargedBolt) { ChargedBolts.Cast(owner, point, values); return true; }
        if (ability == CombatAbility.Lightning) { new LightningMissile(owner, values, angle).Start(); return true; }
        new Missile(owner, ability, values, angle).Start(); return true;
    }
    internal static void Impact(PlayerMobile owner, Mobile target, CombatAbility ability, SkillValues values)
    {
        if(target is not{Alive:true,Deleted:false} || ClassCombat.Friendly(owner,target)){return;}
        var definition = SkillMechanics.For(ability); string element = definition.Text("EType");
        double damage = values.Damage(false) + Utility.RandomDouble() * (values.Damage(true) - values.Damage(false));
        if(AmazonAvoidance.Missile(target)){return;} // Direct missile or copied area damage; no shield request.
        SkillCombat.Hit(owner, target, damage, element);
        if (element != "cold" || !target.Alive || target.Deleted) { return; }
        int frames = ability == CombatAbility.GlacialSpike
            ? (int)values.Formula("ln34 * (100 + skill('Blizzard'.blvl) * par7) / 100") : values.DurationFrames;
        if (ability is CombatAbility.IceBlast or CombatAbility.GlacialSpike)
        { NamedControl.FreezeSkill(owner, target, frames * 40); }
        else { GearEffects.ApplyChill(target, Core.TickCount, frames * 40 / NecromancerEffects.DifficultyDivisor(target), owner); }
    }
    private sealed class Nova(PlayerMobile owner, CombatAbility ability, SkillValues values) : SkillField(owner, ability == CombatAbility.Nova ? 14 : 15, DiabloClass.Sorceress)
    {
        private readonly Point3D _origin = owner.Location;
        private readonly HashSet<Serial> _hit = new();
        private double _radius;
        protected override void Step()
        {
            _radius += SkillGeometry.Velocity(24);
            foreach (var target in SkillCombat.Targets(Owner, _origin, _radius))
            { if (_hit.Add(target.Serial)) { Impact(Owner, target, ability, values); target.FixedEffect(0x374A, 10, 10, ability == CombatAbility.Nova ? 0x482 : 0x480, 0); } }
        }
    }
    private sealed class Missile(PlayerMobile owner, CombatAbility ability, SkillValues values, double angle) : SkillField(owner, ability == CombatAbility.GlacialSpike ? 40 : 50, DiabloClass.Sorceress)
    {
        private double _x = owner.X, _y = owner.Y, _travel;
        private readonly int _z = owner.Z;
        protected override void Step()
        {
            double speed = SkillGeometry.Velocity(ability is CombatAbility.FireBolt or CombatAbility.Fireball ? 20 : ability == CombatAbility.GlacialSpike ? 16 : 12);
            var start = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            _x += Math.Cos(angle) * speed; _y += Math.Sin(angle) * speed; _travel += speed;
            var end = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            if (_travel > CombatProtocol.TeethRange || !Map.LineOfSight(new Point3D(start.X, start.Y, _z + 12), new Point3D(end.X, end.Y, _z + 12))) { Stop(); return; }
            Mobile first = null; double entry = double.MaxValue;
            foreach (var target in SkillCombat.Targets(Owner, end, 2))
            {
                if (CombatHitboxes.Intersect(start, end, target.Location, out double next) && next < entry) { entry = next; first = target; }
            }
            Effects.SendLocationEffect(end, Map, 0x36E4, 3); // Placeholder missile.
            if (first == null) { return; }
            if (ability is CombatAbility.Fireball or CombatAbility.GlacialSpike)
            {
                double radius = ability == CombatAbility.Fireball ? SkillGeometry.Tiles(values.Param(1)) : SkillGeometry.Tiles(values.Eval("aurarangecalc"));
                foreach (var target in SkillCombat.Targets(Owner, first.Location, radius)) { Impact(Owner, target, ability, values); }
            }
            else { Impact(Owner, first, ability, values); }
            Stop();
        }
    }
    private sealed class LightningMissile(PlayerMobile owner, SkillValues values, double angle)
        : SkillField(owner, NecromancerSummons.Source("lightningbolt", "Range"), DiabloClass.Sorceress)
    {
        private double _x = owner.X, _y = owner.Y;
        private readonly int _z = owner.Z;
        private Serial _lastCollision;
        protected override void Step()
        {
            var start = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            double speed = SkillGeometry.Velocity(NecromancerSummons.Source("lightningbolt", "Vel"));
            _x += Math.Cos(angle) * speed; _y += Math.Sin(angle) * speed;
            var end = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            if (end.X < 0 || end.Y < 0 || end.X >= Map.Width || end.Y >= Map.Height
                || !Map.LineOfSight(new Point3D(start.X, start.Y, _z + 12), new Point3D(end.X, end.Y, _z + 12))) { Stop(); return; }
            Mobile first = null; double nearest = double.MaxValue;
            foreach (var candidate in SkillCombat.Targets(Owner, end, 2))
            {
                if (candidate.Serial != _lastCollision && CombatHitboxes.Intersect(start, end, candidate.Location, out double entry)
                    && entry < nearest) { first = candidate; nearest = entry; }
            }
            Effects.SendLocationEffect(end, Map, 0x3818, 3, 10, 0x482, 0); // Placeholder lightning trail.
            if (first == null) { return; }
            _lastCollision = first.Serial;
            SorceressEffects.MissileHit(Owner, first, "lightningbolt");
            Impact(Owner, first, CombatAbility.Lightning, values);
        }
    }
}
