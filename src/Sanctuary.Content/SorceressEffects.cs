// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;

namespace Sanctuary;

public sealed record ColdArmorState(CombatAbility Ability, DateTime UntilUtc, int Defense, int FreezeFrames,
    int Min256, int Max256, int ColdFrames);
public sealed record EnergyShieldState(DateTime UntilUtc, int AbsorbPercent);
public sealed record EnchantState(DateTime UntilUtc, int Rank, int Min256, int Max256, int AttackPercent, uint Caster);
public sealed record SorceressState(ColdArmorState Armor = null, EnergyShieldState Shield = null, ThunderStormState Storm = null, EnchantState Enchantment = null, BlazeState Blaze = null);

public sealed class SorceressEffectProfiles : GenericPersistence
{
    private Dictionary<Serial, SorceressState> _states = new();
    public SorceressEffectProfiles() : base("SanctuarySorceressEffects", 113) { }
    public SorceressState Find(Mobile actor) => actor is { Alive: true, Deleted: false } ? _states.GetValueOrDefault(actor.Serial) : null;
    public void Store(Mobile actor, SorceressState state) => _states[actor.Serial] = state;
    public void Clear(Mobile actor) { _states.Remove(actor.Serial); ThunderStorm.Cancel(actor.Serial); FireGround.Cancel(actor); Blizzards.Cancel(actor); FrozenOrbs.Cancel(actor); Hydras.Cancel(actor); InfernoFlames.Cancel(actor); ChainLightning.Cancel(actor); }
    public override void Serialize(IGenericWriter writer)
    {
        Prune(); writer.Write(0); writer.Write(_states.Count);
        foreach (var (serial, state) in _states) { writer.Write(serial.Value); writer.Write(JsonSerializer.Serialize(state)); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Sorceress state version."); }
        int count = reader.ReadInt();
        if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid Sorceress state count."); }
        var states = new Dictionary<Serial, SorceressState>();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); var state = JsonSerializer.Deserialize<SorceressState>(reader.ReadString());
            if (!serial.IsMobile || state == null || state.Armor is { } armor && (!SorceressEffects.IsArmor(armor.Ability)
                || armor.Defense < 0 || armor.FreezeFrames < 0 || armor.Min256 < 0 || armor.Max256 < armor.Min256 || armor.ColdFrames < 0)
                || state.Shield is { AbsorbPercent: < 0 or > 95 }
                || state.Enchantment is { } enchant && (enchant.Rank <= 0 || enchant.Min256 < 0 || enchant.Max256 < enchant.Min256 || enchant.AttackPercent < 0)
                || state.Blaze is { } blaze && (blaze.Rank <= 0 || blaze.Speed < 0)
                || state.Storm is { } storm && !ThunderStorm.Valid(storm) || !states.TryAdd(serial, state))
            { throw new InvalidDataException("Invalid Sorceress state."); }
        }
        _states = states;
    }
    public void Prune()
    {
        var removed = new List<Serial>();
        foreach (var (serial, state) in _states)
        { if (World.FindMobile(serial) is not { Alive: true, Deleted: false }) { removed.Add(serial); } }
        foreach (var serial in removed) { _states.Remove(serial); ThunderStorm.Cancel(serial); }
    }
    public override void PostDeserialize()
    {
        Prune();
        foreach (var (serial, state) in _states)
        { if (state.Storm != null) { ThunderStorm.Schedule(serial, state.Storm); } }
    }
}

public static class SorceressEffects
{
    public static SorceressEffectProfiles Profiles { get; private set; }
    public static void Configure()
    {
        Profiles = new(); EventSink.WorldSave += Profiles.Prune;
        ClassCombatHooks.SkillMissileHit = MissileHit;
        ClassCombatHooks.CreatureEffectsEnded = actor => { Profiles.Clear(actor); PaladinAuras.Clear(actor);HolyShields.Clear(actor); SkillHealth.Clear(actor); };
    }
    public static bool IsArmor(CombatAbility ability) => ability is CombatAbility.FrozenArmor or CombatAbility.ShiverArmor or CombatAbility.ChillingArmor;
    public static ColdArmorState Armor(Mobile actor) => Profiles?.Find(actor)?.Armor is { } armor && armor.UntilUtc > TimedEffects.Now ? armor : null;
    public static EnergyShieldState Shield(Mobile actor) => Profiles?.Find(actor)?.Shield is { } shield && shield.UntilUtc > TimedEffects.Now ? shield : null;
    public static EnchantState Enchantment(Mobile actor) => Profiles?.Find(actor)?.Enchantment is { } enchant && enchant.UntilUtc > TimedEffects.Now ? enchant : null;
    public static void CastEnchant(PlayerMobile owner, Mobile target, SkillValues values)
    {
        if (Enchantment(target) is { } existing && existing.Rank > values.Rank) { return; }
        var row = SkillMechanics.For(CombatAbility.Enchant);
        var enchant = new EnchantState(TimedEffects.Now.AddMilliseconds(values.Eval("auralencalc") * 40), values.Rank,
            (int)Math.Clamp(NamedElements.Enhance(owner, values.Damage256(false), "fire"), 0, int.MaxValue),
            (int)Math.Clamp(NamedElements.Enhance(owner, values.Damage256(true), "fire"), 0, int.MaxValue),
            row.Number("ToHit") + (values.Rank - 1) * row.Number("LevToHit"), owner.Serial.Value);
        Profiles.Store(target, (Profiles.Find(target) ?? new()) with { Enchantment = enchant });
        target.FixedEffect(0x375A, 10, 15, 0x489, 0); // Placeholder Enchant weapon glow.
    }
    public static double EnchantFire(Mobile actor, double roll) => Enchantment(actor) is { } enchant
        ? (enchant.Min256 + (enchant.Max256 - enchant.Min256) * Math.Clamp(roll, 0, 1)) / 256 : 0;
    public static int Defense(Mobile actor) => Armor(actor)?.Defense ?? 0;
    public static void CastArmor(PlayerMobile owner, CombatAbility ability, SkillValues values)
    {
        var armor = new ColdArmorState(ability, TimedEffects.Now.AddMilliseconds(values.Eval("auralencalc") * 40),
            (int)values.Eval("aurastatcalc1"), ability == CombatAbility.FrozenArmor ? (int)values.Eval("calc1") : 0,
            values.Damage256(false), values.Damage256(true), values.DurationFrames);
        Profiles.Store(owner, (Profiles.Find(owner) ?? new()) with { Armor = armor });
        owner.FixedEffect(0x375A, 10, 15, 0x480, 0); // Placeholder cold armor.
    }
    public static void CastShield(PlayerMobile owner, SkillValues values)
    {
        var shield = new EnergyShieldState(TimedEffects.Now.AddMilliseconds(values.Eval("auralencalc") * 40), (int)values.Eval("calc1"));
        Profiles.Store(owner, (Profiles.Find(owner) ?? new()) with { Shield = shield });
        owner.FixedEffect(0x375A, 10, 15, 0x482, 0); // Placeholder mana shield.
    }
    public static GearDamage Absorb(Mobile target, GearDamage damage)
    {
        if (target is not PlayerMobile owner || Shield(owner) is not { } shield) { return damage; }
        int divisor = Math.Max(1, SkillMechanics.For(CombatAbility.EnergyShield).Parameter(5)
            - ClassCreation.Profiles.GetRank(owner.Serial, "sorceress.telekinesis"));
        // The source event spends a shared mana balance in physical, fire, lightning, cold, magic order.
        double physical = Part(damage.Physical), fire = Part(damage.Fire), lightning = Part(damage.Lightning),
            cold = Part(damage.Cold), magic = Part(damage.Magic);
        if (NecromancerEffects.ManaBalance256(owner) == 0)
        { Profiles.Store(owner, Profiles.Find(owner) with { Shield = null }); }
        return damage with { Physical = physical, Fire = fire, Lightning = lightning, Cold = cold, Magic = magic };
        double Part(double incoming)
        {
            if (incoming <= 0) { return 0; }
            long raw = (long)Math.Clamp(incoming * 256, 0, int.MaxValue);
            long absorbed = Math.Min(raw * shield.AbsorbPercent / 100, (long)NecromancerEffects.ManaBalance256(owner) * 16 / divisor);
            int cost = (int)(absorbed * divisor / 16);
            NecromancerEffects.Pay(owner, cost);
            return (raw - absorbed) / 256.0;
        }
    }
    private static bool Hostile(Mobile attacker, Mobile defender, out PlayerMobile owner)
    {
        owner = defender as PlayerMobile;
        return owner != null && attacker is { Alive: true, Deleted: false } && ClassCombat.IsHostile(owner, attacker);
    }
    public static void MeleeAttempt(Mobile attacker, Mobile defender)
    {
        if (!Hostile(attacker, defender, out var owner) || NamedCombat.Ranged(attacker) || !attacker.InRange(owner, 1)
            || Armor(owner) is not { Ability: CombatAbility.ShiverArmor } armor) { return; }
        Retaliate(owner, attacker, armor);
    }
    public static void MeleeHit(Mobile attacker, Mobile defender, int physical, bool ranged)
    {
        if (physical <= 0 || ranged || !Hostile(attacker, defender, out var owner)
            || Armor(owner) is not { Ability: CombatAbility.FrozenArmor } armor) { return; }
        NamedControl.FreezeSkill(owner, attacker, armor.FreezeFrames * 40);
    }
    public static void MissileHit(Mobile attacker, Mobile defender, string missile)
    {
        if (!Hostile(attacker, defender, out var owner) || Armor(owner) is not { Ability: CombatAbility.ChillingArmor } armor
            || NecromancerSummons.Source(missile, "ReturnFire") == 0) { return; }
        new ReturnBolt(owner, attacker.Location, armor).Start();
    }
    private static void Retaliate(PlayerMobile owner, Mobile target, ColdArmorState armor)
    {
        double damage = (armor.Min256 + Utility.RandomDouble() * (armor.Max256 - armor.Min256)) / 256;
        SkillCombat.Hit(owner, target, damage, "cold");
        if (target is { Alive: true, Deleted: false })
        { GearEffects.ApplyChill(target, Core.TickCount, armor.ColdFrames * 40 / NecromancerEffects.DifficultyDivisor(target), owner); }
    }
    private sealed class ReturnBolt(PlayerMobile owner, Point3D target, ColdArmorState armor)
        : SkillField(owner, NecromancerSummons.Source("chillingarmorbolt", "Range"), DiabloClass.Sorceress)
    {
        private double _x = owner.X, _y = owner.Y;
        private readonly int _z = owner.Z;
        private readonly double _angle = Math.Atan2(target.Y - owner.Y, target.X - owner.X);
        protected override void Step()
        {
            var before = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            double speed = SkillGeometry.Velocity(NecromancerSummons.Source("chillingarmorbolt", "Vel"));
            _x += Math.Cos(_angle) * speed; _y += Math.Sin(_angle) * speed;
            var after = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            if (!Map.LineOfSight(new Point3D(before.X, before.Y, _z + 12), new Point3D(after.X, after.Y, _z + 12))) { Stop(); return; }
            Mobile first = null; double nearest = double.MaxValue;
            foreach (var candidate in SkillCombat.Targets(Owner, after, 2))
            {
                if (CombatHitboxes.Intersect(before, after, candidate.Location, out double entry) && entry < nearest)
                { first = candidate; nearest = entry; }
            }
            Effects.SendLocationEffect(after, Map, 0x36E4, 3); // Placeholder armor bolt.
            if (first != null) { if(!ClassAvoidance.Missile(first)){Retaliate(Owner, first, armor);} Stop(); }
        }
    }
}
