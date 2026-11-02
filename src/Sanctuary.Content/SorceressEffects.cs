// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;

namespace Sanctuary;

public sealed record ColdArmorState(CombatAbility Ability, DateTime UntilUtc, int Defense, int FreezeFrames,
    int Min256, int Max256, int ColdFrames)
{
    // Optional V0 extension. Older saves retain their captured reaction values;
    // do not infer a rank from rounded damage/defense or current equipment.
    public int Rank { get; init; }
}
public sealed record EnergyShieldState(DateTime UntilUtc, int AbsorbPercent)
{
    // Optional V0 extension; old states retain their captured absorption.
    public int Rank {get;init;}
}
public sealed record EnchantState(DateTime UntilUtc, int Rank, int Min256, int Max256, int AttackPercent, uint Caster);
public sealed record SorceressState(ColdArmorState Armor = null, EnergyShieldState Shield = null, ThunderStormState Storm = null, EnchantState Enchantment = null, BlazeState Blaze = null);

public sealed class SorceressEffectProfiles : GenericPersistence
{
    private Dictionary<Serial, SorceressState> _states = new();
    public SorceressEffectProfiles() : base("SanctuarySorceressEffects", 113) { }
    public SorceressState Find(Mobile actor) => actor is { Alive: true, Deleted: false } ? _states.GetValueOrDefault(actor.Serial) : null;
    public void Store(Mobile actor, SorceressState state) => _states[actor.Serial] = state;
    public void Clear(Mobile actor) { _states.Remove(actor.Serial); ThunderStorm.Cancel(actor.Serial); FireGround.Cancel(actor); Blizzards.Cancel(actor); FrozenOrbs.Cancel(actor); Hydras.Cancel(actor); InfernoFlames.Cancel(actor); ChainLightning.Cancel(actor); SorceressSpellMissiles.Cancel(actor); HolyBoltFlights.Cancel(actor); BlessedHammerFlights.Cancel(actor); HeavenFists.Cancel(actor); }
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
                || armor.Defense < 0 || armor.FreezeFrames < 0 || armor.Min256 < 0 || armor.Max256 < armor.Min256 || armor.ColdFrames < 0 || armor.Rank is < 0 or > 255)
                || state.Shield is { AbsorbPercent: < 0 or > 95 } or {Rank:<0 or >255}
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
    public static ColdArmorState Armor(Mobile actor) => MagePetBuffs.Armor(actor) ?? (Profiles?.Find(actor)?.Armor is { } armor && armor.UntilUtc > TimedEffects.Now ? armor : null);
    public static EnergyShieldState Shield(Mobile actor) => Profiles?.Find(actor)?.Shield is { } shield && shield.UntilUtc > TimedEffects.Now ? shield : null;
    public static EnchantState Enchantment(Mobile actor) => Profiles?.Find(actor)?.Enchantment is { } enchant && enchant.UntilUtc > TimedEffects.Now ? enchant : null;
    internal static bool CanEnchant(Mobile target,SkillValues values)
        =>Enchantment(target) is not{} existing||existing.Rank<=values.Rank;
    public static void CastEnchant(PlayerMobile owner, Mobile target, SkillValues values)
        =>ApplyEnchant(owner,target,values,value=>(int)Math.Clamp(NamedElements.Enhance(owner,value,"fire"),0,int.MaxValue));
    internal static void CastNpcEnchant(Mobile caster,Mobile target,SkillValues values)
    {
        if(caster is PlayerMobile||values.Ability!=CombatAbility.Enchant){throw new ArgumentException("NPC Enchant needs its own caster and spell values");}
        ApplyEnchant(caster,target,values,value=>value);
    }
    private static void ApplyEnchant(Mobile caster,Mobile target,SkillValues values,Func<int,int> scale)
    {
        var existing = Enchantment(target);
        if (!CanEnchant(target,values)) { return; }
        var row = SkillMechanics.For(CombatAbility.Enchant);
        var enchant = new EnchantState(TimedEffects.Now.AddMilliseconds(values.Eval("auralencalc") * 40), values.Rank,
            scale(values.Damage256(false)),scale(values.Damage256(true)),
            row.Number("ToHit") + (values.Rank - 1) * row.Number("LevToHit"),
            // The shared source state helper reuses an equal-rank list. Its
            // values/deadline refresh, but the list's original owner survives.
            existing?.Rank == values.Rank ? existing.Caster : caster.Serial.Value);
        Profiles.Store(target, (Profiles.Find(target) ?? new()) with { Enchantment = enchant });
        target.FixedEffect(0x375A, 10, 15, 0x489, 0); // Placeholder Enchant weapon glow.
    }
    public static double EnchantFire(Mobile actor, double roll) => Enchantment(actor) is { } enchant
        ? (enchant.Min256 + (enchant.Max256 - enchant.Min256) * Math.Clamp(roll, 0, 1)) / 256 : 0;
    public static int Defense(Mobile actor) => Armor(actor)?.Defense ?? 0;
    public static void CastArmor(PlayerMobile owner, CombatAbility ability, SkillValues values)
    {
        var armor = PrepareColdArmor(ability,values,TimedEffects.Now.AddMilliseconds(values.Eval("auralencalc") * 40));
        ApplyArmor(owner, armor);
    }
    internal static void ApplyArmor(PlayerMobile owner, ColdArmorState armor)
    {
        DefensiveArmors.ClearGroup(owner);
        Profiles.Store(owner, (Profiles.Find(owner) ?? new()) with { Armor = armor });
        owner.FixedEffect(0x375A, 10, 15, 0x480, 0); // Placeholder cold armor.
    }
    internal static ColdArmorState PrepareColdArmor(CombatAbility ability,SkillValues values,DateTime until)
        =>new(ability,until,(int)values.Eval("aurastatcalc1"),ability==CombatAbility.FrozenArmor?(int)values.Eval("calc1"):0,
            values.Damage256(false),values.Damage256(true),values.DurationFrames) { Rank = values.Rank };
    public static void CastShield(PlayerMobile owner, SkillValues values)
        =>EnergyShields.Apply(owner,EnergyShields.Prepare(values,TimedEffects.Now));
    public static GearDamage Absorb(Mobile target, GearDamage damage)
    {
        damage=DruidCycloneArmor.Absorb(target,damage);
        if (target is not PlayerMobile owner || Shield(owner) is not { } shield) { return damage; }
        return EnergyShields.Absorb(owner,damage,shield);
    }
    private static bool Hostile(Mobile attacker, Mobile defender, out PlayerMobile owner)
    {
        owner = defender as PlayerMobile;
        return owner != null && attacker is { Alive: true, Deleted: false } && ClassCombat.IsHostile(owner, attacker);
    }
    public static void MeleeAttempt(Mobile attacker, Mobile defender)
    {
        // Ordinary NPC attacks already have a real native/custom hit-or-miss
        // scope. Their reaction belongs after that attempt's damage, not AR.
        if(attacker is BaseCreature&&defender is PlayerMobile){return;}
        if (!Hostile(attacker, defender, out var owner) || NamedCombat.Ranged(attacker) || !attacker.InRange(owner, 1)
            || Armor(owner) is not { Ability: CombatAbility.ShiverArmor } armor) { return; }
        ShiverArmor.React(owner,attacker,armor);
    }
    public static void MeleeHit(Mobile attacker, Mobile defender, int physical, bool ranged, bool frozenArmor = true)
    {
        if(physical<=0||ranged){return;}
        if(frozenArmor&&Hostile(attacker,defender,out var owner)&&Armor(owner) is{Ability:CombatAbility.FrozenArmor})
        {ColdArmors.PhysicalMelee(attacker,defender,(int)Math.Min((long)physical*256,int.MaxValue),ranged);return;}
        if(defender is NecromancerMinion{ControlMaster:PlayerMobile master} mage&&MagePetBuffs.Armor(mage) is{} innate
            &&attacker is{Alive:true,Deleted:false}&&attacker.Map==mage.Map&&ClassCombat.IsHostile(master,attacker))
        {FreezeStates.Apply(mage,attacker,innate.FreezeFrames,Core.TickCount);}
    }
    public static void MissileHit(Mobile attacker, Mobile defender, string missile)
    {
        if (!Hostile(attacker, defender, out var owner) || Armor(owner) is not { Ability: CombatAbility.ChillingArmor } armor
            || NecromancerSummons.Source(missile, "ReturnFire") == 0) { return; }
        SorceressSpellMissiles.ReactiveArmor(owner, attacker.Location, armor);
    }
}
