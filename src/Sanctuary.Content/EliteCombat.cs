// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class EliteCombat
{
    internal static bool CanAct(EncounterCreature source) => source is { Deleted: false, Alive: true, Frozen: false, Paralyzed: false }
        && ClassCombatHooks.ActionAllowed?.Invoke(source) != false;
    internal static bool Enemy(EncounterCreature source, Mobile target, int range, bool death = false) => source != null
        && (death || source is { Deleted: false, Alive: true }) && target is { Deleted: false, Alive: true, Blessed: false } && target != source
        && source.Map != null && source.Map != Map.Internal && target.Map == source.Map && source.InRange(target, range)
        && Math.Abs(source.Z - target.Z) < 16 && source.IsEnemy(target) && source.CanSee(target) && source.InLOS(target)
        && source.CanBeHarmful(target, false) && ClassCombatHooks.Friendly?.Invoke(source, target) != true;
    internal static int Roll(int minimum, int maximum, Random random) => random.Next(Math.Max(0, minimum), Math.Max(minimum, maximum) + 1);
    internal static int ElementRoll(EncounterCreature source, bool minion, Random random)
        => Roll(source.Basis.MinDamage * (minion ? 33 : 66) / 100, source.Basis.MaxDamage * (minion ? 50 : 100) / 100, random);
    internal static int MagicResistance(Mobile target) => NamedElements.MagicResistance(target);
    // One composite hit reaches PlayerMobile once, so Protection and hit recovery are not multiplied by its component count.
    internal static int Damage(EncounterCreature source, Mobile target, double physical = 0, double fire = 0,
        double cold = 0, double lightning = 0, double magic = 0, bool weapon = false, bool ranged = false, bool death = false)
    {
        if (!Enemy(source, target, 18, death)) { return 0; }
        double preparedPhysical=GemCombat.Component(target,physical,target.PhysicalResistance,false);
        double total = preparedPhysical
            + NamedElements.Damage(source, target, fire, "fire")
            + NamedElements.Damage(source, target, cold, "cold")
            + NamedElements.Damage(source, target, lightning, "ltng")
            + NamedElements.Damage(source, target, magic, "mag");
        int amount = (int)Math.Floor(total), before = target.Hits;
        if(weapon){PaladinThorns.Prepared(source,target,(int)Math.Clamp(Math.Floor(preparedPhysical*256),0,int.MaxValue),ranged);}
        if (amount > 0)
        {
            source.DoHarmful(target); target.Damage(amount, source); CombatDamage.Finish(target, source);
            int loss = Math.Max(0, before - target.Hits);
            if (target is BaseCreature creature) { CreatureHitRecovery.Apply(creature, source, loss, weapon); }
            if (target is PlayerMobile player)
            {
                HitRecovery.Apply(player, loss, Core.TickCount);
                if (weapon) { GearEffects.WearArmor(player, (BaseWeapon)source.Weapon, amount); }
            }
            if (weapon) { GemCombat.WeaponHit(source, target, loss, ranged); }
        }
        EliteAuras.Refresh(target);
        return Math.Max(0, before - target.Hits);
    }
    internal static void Hit(EncounterCreature source, Mobile target, int baseDamage, ResistanceType type, bool ranged = false, Random random = null,SlowMissilePayload? missile=null)
    {
        if (!Enemy(source, target, 18) || !ranged && !CanAct(source)) { return; }
        random ??= Random.Shared;
        bool minion = source.Grade == EncounterGrade.Minion;
        bool inheritElements = !minion || source.Difficulty != MonsterDifficulty.Normal;
        double physical = type == ResistanceType.Physical ? baseDamage : 0;
        double fire = type == ResistanceType.Fire ? baseDamage : 0, cold = 0, lightning = 0, magic = 0, poison = 0;
        // Shared STAT_DAMAGEPERCENT belongs to the native weapon ED pool;
        // Slow Missiles remains a distinct later multiplication.
        physical *= (100 + Math.Max(-90,EliteAuras.DamageBonus(source)+NecromancerEffects.DamageStatPercent(source))) / 100.0;
        if (inheritElements)
        {
            if (source.Inherits(EliteModifier.FireEnchanted)) { fire += ElementRoll(source, minion, random); }
            if (source.Inherits(EliteModifier.ColdEnchanted)) { cold += ElementRoll(source, minion, random); }
            if (source.Inherits(EliteModifier.LightningEnchanted)) { lightning += ElementRoll(source, minion, random); }
        }
        if (source.Grade == EncounterGrade.Champion && source.Champion == ChampionKind.Ghostly)
        { cold += ElementRoll(source, true, random); }
        int coldMs = cold > 0 || source.Inherits(EliteModifier.ColdEnchanted) && (!minion || inheritElements || ranged)
            ? 4000 + CreatureProfiles.Level(source) * 200 : 0;
        if (source.Has(EliteModifier.SpectralHit))
        {
            int amount = ElementRoll(source, false, random);
            switch (random.Next(5))
            {
                case 0: magic += amount; break;
                case 1: fire += amount; break;
                case 2: lightning += amount; break;
                case 3: cold += amount; coldMs = Math.Max(coldMs, 1600); break;
                case 4: poison = amount; break;
            }
        }
        foreach (var aura in new[] { EliteAura.HolyFire, EliteAura.HolyFreeze, EliteAura.HolyShock })
        {
            int level = EliteAuras.Level(source, aura, false);
            if (level == 0) { continue; }
            int amount = EliteAuras.ElementDamage(aura, level, random, aura == EliteAura.HolyFreeze ? 5 : 6);
            if (aura == EliteAura.HolyFire) { fire += amount; }
            else if (aura == EliteAura.HolyFreeze) { cold += amount; }
            else { lightning += amount; }
        }
        if(missile is{} payload && payload.DamagePercent!=100)
        {
            // Native encounter poison is an amount over40 frames. Project it
            // into the existing rate/count packet before scaling; never scale
            // poison duration or merge this into the physical bonus pool.
            var scaled=ScaleMissile(new(physical,fire,cold,lightning,poison,1600,coldMs,magic),payload);
            physical=scaled.Physical;fire=scaled.Fire;cold=scaled.Cold;lightning=scaled.Lightning;magic=scaled.Magic;poison=scaled.Poison;
        }
        Damage(source, target, physical, fire, cold, lightning, magic, true, ranged);
        if (target.Deleted || !target.Alive) { return; }
        if (coldMs > 0) { GearEffects.ApplyChill(target, Core.TickCount, coldMs); }
        if (poison > 0) { GearEffects.ApplyPoison(source, target, poison, Core.TickCount, 1600); }
        if (source.Inherits(EliteModifier.ManaBurn) && inheritElements)
        {
            int mana = ElementRoll(source, minion, random);
            mana = mana * (source.Difficulty switch { MonsterDifficulty.Nightmare => 140, MonsterDifficulty.Hell => 200, _ => 100 }) / 100;
            target.Mana -= Math.Min(target.Mana, mana);
        }
        if (source.Has(EliteModifier.Cursed) && random.Next(100) < 75)
        {
            int level = CreatureProfiles.Level(source) / 5 + 1;
            int radius = Math.Clamp((3 + level - 1 + 2) / 3, 1, 18);
            foreach (var mobile in source.Map.GetMobilesInRange<Mobile>(target.Location, radius))
            {
                if (Enemy(source, mobile, 18)) { EliteAuras.Amplify(mobile, TimeSpan.FromSeconds(8 + 3 * (level - 1))); }
            }
        }
    }
    internal static GearDamage ScaleMissile(GearDamage native,SlowMissilePayload slow)
    {
        if(slow.DamagePercent==100){return native;}
        int poisonFrames=native.PoisonMs/40;
        if(native.Poison>0 && (poisonFrames<=0 || native.PoisonMs%40!=0))
        {throw new NotSupportedException("Unsupported native poison frame projection");}
        var packet=new MissileSourceStats(FireMin:NamedElements.To256(native.Fire),FireMax:NamedElements.To256(native.Fire),
            LightningMin:NamedElements.To256(native.Lightning),LightningMax:NamedElements.To256(native.Lightning),
            MagicMin:NamedElements.To256(native.Magic),MagicMax:NamedElements.To256(native.Magic),
            ColdMin:NamedElements.To256(native.Cold),ColdMax:NamedElements.To256(native.Cold),ColdFrames:native.ColdMs/40,
            PoisonMin:poisonFrames>0?NamedElements.To256(native.Poison)/poisonFrames:0,
            PoisonMax:poisonFrames>0?NamedElements.To256(native.Poison)/poisonFrames:0,PoisonFrames:poisonFrames,PoisonCount:1);
        var scaled=slow.Scale(packet);
        return native with{Physical=slow.Damage256(NamedElements.To256(native.Physical))/256.0,
            Fire=scaled.FireMin/256.0,Lightning=scaled.LightningMin/256.0,Magic=scaled.MagicMin/256.0,Cold=scaled.ColdMin/256.0,
            Poison=(long)scaled.PoisonMin*scaled.PoisonImpactFrames/256.0};
    }
    internal static void OnStruck(BaseCreature creature, Mobile attacker, int damage)
    {
        if (damage <= 0 || creature is not EncounterCreature source || !Enemy(source, attacker, 18) || !source.Has(EliteModifier.LightningEnchanted)) { return; }
        EliteBolts.Lightning(source);
    }
    internal static void Death(EncounterCreature source)
    {
        if (source.Map == null || source.Map == Map.Internal) { return; }
        if (source.Has(EliteModifier.FireEnchanted))
        {
            int index = (int)source.Difficulty;
            int[] minimum = [1125, 700, 75], maximum = [1875, 1166, 125];
            int amount = (int)((long)source.Basis.Life.Max * Roll(minimum[index], maximum[index], Random.Shared) / 10000);
            int radius = new[] { 3, 3, 4 }[index];
            source.FixedParticles(0x36BD, 10, 20, 5044, EffectLayer.Waist);
            foreach (var mobile in source.Map.GetMobilesInRange<Mobile>(source.Location, radius))
            { if (Enemy(source, mobile, radius, true)) { Damage(source, mobile, amount / 2.0, amount / 2.0, death: true); } }
        }
        if (source.Has(EliteModifier.ColdEnchanted))
        {
            int level = Math.Max(1, CreatureProfiles.Level(source) / 2);
            int min = EliteAuras.SkillDamage(6, [4, 5, 6, 8, 10], level);
            int max = EliteAuras.SkillDamage(8, [4, 5, 6, 8, 10], level);
            int amount = Roll(min, max, Random.Shared);
            source.FixedParticles(0x374A, 10, 30, 5052, EffectLayer.Waist);
            foreach (var mobile in source.Map.GetMobilesInRange<Mobile>(source.Location, 6))
            {
                if (!Enemy(source, mobile, 6, true)) { continue; }
                Damage(source, mobile, cold: amount, death: true);
                GearEffects.ApplyChill(mobile, Core.TickCount, (3 + level - 1) * 1000);
            }
        }
    }
    internal static bool Teleport(EncounterCreature source)
    {
        int radius = EncounterProfiles.Settings.TeleportRadius;
        int start = Utility.Random(8);
        for (int distance = radius; distance >= 2; distance--)
        {
            for (int index = 0; index < 8; index++)
            {
                int dir = (start + index) % 8;
                int[] xs = [0, 1, 1, 1, 0, -1, -1, -1], ys = [-1, -1, 0, 1, 1, 1, 0, -1];
                var point = new Point3D(source.X + xs[dir] * distance, source.Y + ys[dir] * distance, source.Z);
                if (Math.Max(Math.Abs(point.X - source.Home.X), Math.Abs(point.Y - source.Home.Y)) > LeashedMeleeAI.HomeRadius || !EncounterProfiles.ClearSight(source.Map, source.Location, point)
                    || !EncounterProfiles.Free(source.Map, point)) { continue; }
                source.StopCombatActions(); source.FixedEffect(0x3728, 10, 13);
                source.MoveToWorld(point, source.Map);
                if (source.Hits * 100L < source.HitsMax * 30L)
                { source.Hits += source.HitsMax * EncounterProfiles.Settings.TeleportHealPercent / 100; }
                source.FixedEffect(0x3728, 10, 13); return true;
            }
        }
        return false;
    }
}
