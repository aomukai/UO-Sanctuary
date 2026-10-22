// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct SkillHitResult(int LifeLost, int Physical, int LifeBefore);

public static class SkillCombat
{
    public static int HolyImpact(PlayerMobile source,Mobile target,int enhanced256,bool bypassResistance)
    {
        if(target is not {Alive:true,Deleted:false} || ClassCombat.Friendly(source,target)){return 0;}
        source.DoHarmful(target,true);double value;
        if(bypassResistance)
        {
            // Selected Holy species bit: clamp positive resistance to0; preserve negative resistance. Skip MDR/absorb.
            int resist=Math.Min(0,NamedElements.Resistance(source,target,"mag",NamedElements.MagicResistance(target)));
            value=enhanced256/256.0*Math.Clamp(100-resist,0,200)/100;
        }
        else
        {
            double magic=SorceressEffects.Absorb(target,new(0,0,0,0,0,Magic:enhanced256/256.0)).Magic;
            value=NamedElements.Damage(source,target,magic,"mag",enhance:false);
        }
        int loss=SkillHealth.Damage(source,target,(int)Math.Clamp(Math.Floor(value*256),0,int.MaxValue));
        if(target is BaseCreature creature){CreatureHitRecovery.Apply(creature,source,loss);}
        return loss;
    }
    public static int AuraElement(PlayerMobile source,Mobile target,int enhanced256,string element)
    {
        if(element is not ("fire" or "ltng" or "cold" or "mag")){throw new NotSupportedException("Unsupported elemental aura damage: "+element);}
        if(target is not {Alive:true,Deleted:false} || ClassCombat.Friendly(source,target)){return 0;}
        source.DoHarmful(target,true);
        var parts=SorceressEffects.Absorb(target,element=="fire"?new(0,enhanced256/256.0,0,0,0):element=="cold"?new(0,0,enhanced256/256.0,0,0):element=="mag"?new(0,0,0,0,0,Magic:enhanced256/256.0):new(0,0,0,enhanced256/256.0,0));
        double value=NamedElements.Damage(source,target,element=="fire"?parts.Fire:element=="cold"?parts.Cold:element=="mag"?parts.Magic:parts.Lightning,element,enhance:false);
        // Source SOFTHIT pulse: no ordinary attack check, block, weapon proc or get-hit request.
        return SkillHealth.Damage(source,target,(int)Math.Clamp(Math.Floor(value*256),0,int.MaxValue));
    }
    public static int LightningImpact(PlayerMobile source, Mobile target, int enhanced256)
    {
        if (target is not { Alive: true, Deleted: false } || ClassCombat.Friendly(source, target)) { return 0; }
        source.DoHarmful(target, true);
        double lightning = SorceressEffects.Absorb(target, new(0, 0, 0, enhanced256 / 256.0, 0)).Lightning;
        lightning = NamedElements.Damage(source, target, lightning, "ltng", enhance: false);
        int loss = SkillHealth.Damage(source, target, (int)Math.Clamp(Math.Floor(lightning * 256), 0, int.MaxValue));
        if (target is BaseCreature creature) { CreatureHitRecovery.Apply(creature, source, loss); }
        return loss;
    }
    public static int ColdImpact(PlayerMobile source, Mobile target, int enhanced256, int coldFrames)
    {
        if (target is not { Alive: true, Deleted: false } || ClassCombat.Friendly(source, target)) { return 0; }
        source.DoHarmful(target, true);
        double cold = SorceressEffects.Absorb(target, new(0, 0, enhanced256 / 256.0, 0, 0)).Cold;
        cold = NamedElements.Damage(source, target, cold, "cold", enhance: false);
        int loss = SkillHealth.Damage(source, target, (int)Math.Clamp(Math.Floor(cold * 256), 0, int.MaxValue));
        if (target is BaseCreature creature) { CreatureHitRecovery.Apply(creature, source, loss); }
        if (target is { Alive: true, Deleted: false })
        { GearEffects.ApplyChill(target, Core.TickCount, coldFrames * 40 / NecromancerEffects.DifficultyDivisor(target), source); }
        return loss;
    }
    public static int FireImpact(PlayerMobile source, Mobile target, int enhanced256, int? pierceOverride = null, int damageRate = 0)
    {
        int loss = FireFrame(source, target, enhanced256, damageRate, pierceOverride);
        if (target is BaseCreature creature) { CreatureHitRecovery.Apply(creature, source, loss); }
        return loss;
    }
    public static int FireFrame(PlayerMobile source, Mobile target, int enhanced256, int damageRate = 0, int? pierceOverride = null)
    {
        if (target is not { Alive: true, Deleted: false } || ClassCombat.Friendly(source, target)) { return 0; }
        source.DoHarmful(target, true);
        double fire = SorceressEffects.Absorb(target, new(0, enhanced256 / 256.0, 0, 0, 0)).Fire;
        fire = NamedElements.Damage(source, target, fire, "fire", enhance: false, damageRate: damageRate, pierceOverride: pierceOverride);
        return SkillHealth.Damage(source, target, (int)Math.Clamp(Math.Floor(fire * 256), 0, int.MaxValue));
    }
    public static bool Begin(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point, long now,
        Func<PlayerMobile, CombatAbility, Serial, Point3D, bool> validTarget)
    {
        if (ClassAbilities.SelfTarget(ability)) { selected = Serial.Zero; point = owner.Location; }
        var session = ClassCombat.For(owner);
        if (!ClassCombat.Available(owner, ability) || !owner.Alive || !AlliedOverlap.Allowed(owner) || owner.Frozen || owner.Paralyzed
            || owner.Spell != null || HitRecovery.Active(owner, now) || session.PendingAbility != CombatAbility.None
            || now - session.NextAction < 0 || !FireGround.Ready(owner, ability) || !validTarget(owner, ability, selected, point)
            || !NecromancerEffects.CanPay(owner, SkillMechanics.Values(owner, ability).Mana256)) { return false; }
        session.Timing = CombatTiming.For(owner, ability);
        session.PendingAbility = ability; session.PendingTarget = selected; session.PendingPoint = point;
        session.ActionOrigin = owner.Location; session.ActionMap = owner.Map;
        session.PendingWeapon = CombatTiming.Equipped(owner)?.Serial ?? Serial.Zero;
        session.NextAction = now + session.Timing.DurationMs; session.EffectAt = now + session.Timing.EffectMs;
        session.Animation = CombatTiming.WeaponAttack(ability)?ClassCombat.AnimationFor(owner,ability,CombatTiming.Equipped(owner)):(byte)16;
        if (point.X != owner.X || point.Y != owner.Y) { owner.Direction = owner.GetDirectionTo(point); }
        owner.RevealingAction(); ClassCombat.SendAnimation(owner, session.Animation, session.Timing.DurationMs);
        owner.Animate(session.Animation, 5, 1, true, false, 0); ClassCombat.SendState(owner, session); return true;
    }
    internal static List<Mobile> Targets(PlayerMobile owner, Point3D center, double radius)
    {
        var targets = new List<Mobile>();
        foreach (var target in owner.Map.GetMobilesInRange<Mobile>(center, (int)Math.Ceiling(radius)))
        {
            double dx = target.X - center.X, dy = target.Y - center.Y;
            if (dx * dx + dy * dy <= radius * radius && Math.Abs(target.Z - center.Z) < 16
                && ClassCombat.IsHostile(owner, target) && owner.Map.LineOfSight(new Point3D(center.X, center.Y, center.Z + 12), new Point3D(target.X, target.Y, target.Z + 12))) { targets.Add(target); }
        }
        targets.Sort((a, b) => a.Serial.CompareTo(b.Serial)); return targets;
    }
    public static ClassSkillDamage? ResistedDamage(Mobile target, Mobile source, int damage, int physical, int fire, int cold, int poison, int lightning, bool spell, bool weapon)
    {
        bool classPlayer=target is PlayerMobile victim && ClassCombat.Enabled(victim) || source is PlayerMobile attacker && ClassCombat.Enabled(attacker);
        if (!classPlayer && NecromancerEffects.Armor(target) == 0 && NecromancerEffects.Curse(source) == null
            && NecromancerEffects.Curse(target) == null && target is not NecromancerMinion
            && SorceressEffects.Shield(target) == null && SorceressEffects.Armor(target) == null
            && !PaladinResistance.Active(target) && !PaladinThorns.Active(target) && !PaladinConviction.Managed(target)
            && (!weapon || SorceressEffects.Enchantment(source) == null && !PaladinElements.WeaponActive(source) && PaladinAuras.NativeWeaponMultiplier(source,true)==1)) { return null; }
        var parts = Mitigate(source, target, new(damage * physical / 100.0 * PaladinAuras.NativeWeaponMultiplier(source,weapon),
            damage * fire / 100.0 + (weapon ? SorceressEffects.EnchantFire(source, Utility.RandomDouble()) + PaladinElements.WeaponForHit(source,"fire",Utility.RandomDouble(),source.Weapon is BaseRanged) : 0),
            damage * cold / 100.0 + (weapon?PaladinElements.WeaponForHit(source,"cold",Utility.RandomDouble(),source.Weapon is BaseRanged):0), damage * lightning / 100.0 + (weapon?PaladinElements.WeaponForHit(source,"ltng",Utility.RandomDouble(),source.Weapon is BaseRanged):0), damage * poison / 100.0), spell);
        return new ClassSkillDamage((int)Math.Max(0, parts.Total), (int)Math.Max(0, parts.Physical))
            {Total256=NamedElements.To256(parts.Total),Physical256=NamedElements.To256(parts.Physical)};
    }
    private static GearDamage Mitigate(Mobile source, Mobile target, GearDamage damage, bool spell)
    {
        double added = spell && source is PlayerMobile caster ? damage.Total * CombatPotions.Bonus(caster, "magical-penetration") / 100.0 : 0;
        var raw = damage with
        {
            Physical = NamedElements.Enhance(source, damage.Physical * Math.Max(0, NecromancerEffects.OutgoingPhysicalPercent(source)) / 100.0, "physical"),
            Fire = NamedElements.Enhance(source, damage.Fire + added, "fire"),
            Cold = NamedElements.Enhance(source, damage.Cold + added, "cold"),
            Lightning = NamedElements.Enhance(source, damage.Lightning + added, "ltng"),
            Poison = NamedElements.Enhance(source, damage.Poison + added, "pois"),
            Magic = NamedElements.Enhance(source, damage.Magic, "mag")
        };
        return MitigatePrepared(source,target,raw);
    }
    internal static GearDamage MitigatePrepared(Mobile source,Mobile target,GearDamage damage)
    {
        var parts=MitigatePrepared(source,target,damage,out int healing256);
        if(healing256>0){SkillHealth.Heal(target,healing256);}
        return parts;
    }
    internal static GearDamage MitigatePrepared(Mobile source,Mobile target,GearDamage damage,out int absorbed256)
    {
        var raw = SorceressEffects.Absorb(target, damage);
        long healing256=0;
        double Channel(double value,string element)
        {
            var result=NamedElements.Resolve256(source,target,NamedElements.To256(value),element,enhance:false);
            healing256+=result.Absorbed256;return result.Damage256/256.0;
        }
        var parts=raw with
        {
            Physical=Channel(NecromancerEffects.AbsorbPhysical(target,raw.Physical),"physical"),
            Fire=Channel(raw.Fire,"fire"),Cold=Channel(raw.Cold,"cold"),Lightning=Channel(raw.Lightning,"ltng"),
            Poison=Channel(raw.Poison,"pois"),Magic=Channel(raw.Magic,"mag")
        };
        absorbed256=(int)Math.Min(healing256,int.MaxValue);
        return parts;
    }
    public static int Hit(Mobile owner, Mobile target, double damage, string element, bool recover = true) => HitParts(owner, target,
        element switch
        {
            "physical" => new(damage, 0, 0, 0, 0), "fire" => new(0, damage, 0, 0, 0),
            "cold" => new(0, 0, damage, 0, 0), "ltng" => new(0, 0, 0, damage, 0),
            _ => new(0, 0, 0, 0, 0, Magic: damage)
        }, recover, spell: recover && element != "physical");
    public static int HitParts(Mobile source, Mobile target, GearDamage damage, bool recover, bool weapon = false, bool ranged = false, bool spell = false)
        => ResolveHitParts(source, target, damage, recover, weapon, ranged, spell).LifeLost;
    public static SkillHitResult ResolveHitParts(Mobile source, Mobile target, GearDamage damage, bool recover,
        bool weapon = false, bool ranged = false, bool spell = false)
    {
        if (source == null || target is not { Alive: true, Deleted: false } || ClassCombat.Friendly(source, target)) { return default; }
        if (ranged) { SorceressEffects.MissileHit(source, target, "arrow"); }
        if (weapon) { damage = damage with { Physical = damage.Physical * PaladinAuras.NativeWeaponMultiplier(source,true), Fire = damage.Fire + SorceressEffects.EnchantFire(source, Utility.RandomDouble()) + PaladinElements.WeaponForHit(source,"fire",Utility.RandomDouble(),ranged), Cold=damage.Cold+PaladinElements.WeaponForHit(source,"cold",Utility.RandomDouble(),ranged), Lightning=damage.Lightning+PaladinElements.WeaponForHit(source,"ltng",Utility.RandomDouble(),ranged) }; }
        var parts = Mitigate(source, target, damage with { Poison = 0 }, spell);
        double physical = parts.Physical, total = parts.Total;
        int before = target.Hits;
        source.DoHarmful(target, true);
        if(weapon){PaladinThorns.Prepared(source,target,(int)Math.Clamp(Math.Floor(physical*256),0,int.MaxValue),ranged);}
        if (total > 0) { SkillHealth.Damage(source,target,NamedElements.To256(total)); }
        int loss = Math.Max(0, before - target.Hits);
        if (recover && target is BaseCreature creature) { CreatureHitRecovery.Apply(creature, source, loss, weapon); }
        if (weapon) { NecromancerEffects.WeaponHit(source, target, Math.Min(loss, (int)physical), ranged); }
        return new(loss, (int)Math.Clamp(physical, 0, int.MaxValue), before);
    }
}

public abstract class SkillField(PlayerMobile owner, int frames, DiabloClass characterClass)
    {
        protected readonly PlayerMobile Owner = owner;
        protected readonly Map Map = owner.Map;
        private readonly long _deadline = Core.TickCount + frames * 40;
        private TimerExecutionToken _timer;
        protected abstract void Step();
        public void Start() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(40), Tick, out _timer);
        private void Tick()
        {
            if (Owner.Deleted || !Owner.Alive || Owner.Map != Map || Map == Map.Internal
                || !ClassCreation.Profiles.TryGet(Owner.Serial, out var profile) || profile.Class != characterClass
                || Core.TickCount - _deadline >= 0) { _timer.Cancel(); return; }
            Step();
        }
        protected void Stop() => _timer.Cancel();
    }
