// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Server.Mobiles;

namespace Sanctuary;

public sealed record SkillDefinition(string TalentId, string Name, DiabloClass CharacterClass, string Tree,
    int SourceId, string SourceKey, CombatAbility Ability, int SourceTab, bool Passive, bool Left, bool Right,
    Dictionary<string, JsonElement> SourceFields, Dictionary<string, int> SourceParameters)
{
    public int Number(string field, int fallback = 0) => SourceFields.TryGetValue(field, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : fallback;
    public string Text(string field) => SourceFields.TryGetValue(field, out var value)
        ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText() : "0";
    public int Parameter(int index) => SourceParameters.GetValueOrDefault(index.ToString(CultureInfo.InvariantCulture));
}

public static class SkillMechanics
{
    private static readonly Dictionary<CombatAbility, SkillDefinition> _abilities = new();
    private static readonly Dictionary<string, SkillDefinition> _source = new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyCollection<SkillDefinition> All => _abilities.Values;
    public static SkillDefinition For(CombatAbility ability) => _abilities[ability];
    public static SkillDefinition Source(string key) => _source[key];
    public static void Load()
    {
        _abilities.Clear(); _source.Clear();
        foreach (var row in BalanceCatalog.Read<SkillDefinition>("class-skills"))
        {
            if (!ClassAbilities.Valid(row.Ability) || ClassAbilities.Find(row.Ability)?.TalentId != row.TalentId)
            { throw new InvalidDataException("Skill protocol/catalog mismatch."); }
            _abilities.Add(row.Ability, row); _source.Add(row.SourceKey, row);
        }
        if (_abilities.Count != 240) { throw new InvalidDataException("Expected all 240 class skills."); }
    }
    public static int Rank(PlayerMobile owner, CombatAbility ability) => ability == CombatAbility.Teeth
        ? ClassCombat.TeethRank(owner) : NamedSkills.Rank(owner, For(ability).TalentId);
    public static SkillValues Values(PlayerMobile owner, CombatAbility ability, int? rankOverride = null)
    {
        var effective = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var learned = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        ClassCreation.Profiles.TryGet(owner.Serial, out var profile);
        foreach (var row in _abilities.Values)
        {
            if (row.CharacterClass != profile?.Class) { continue; }
            effective[row.SourceKey] = row.Ability == ability && rankOverride.HasValue ? rankOverride.Value
                : row.Ability == CombatAbility.Teeth ? ClassCombat.TeethRank(owner) : NamedSkills.Rank(owner, row.TalentId);
            learned[row.SourceKey] = ClassCreation.Profiles.GetRank(owner.Serial, row.TalentId);
        }
        // Launched spells retain their release ranks through gear changes and level-ups.
        return new(For(ability), key => effective.GetValueOrDefault(key), key => learned.GetValueOrDefault(key), ClassStats.Level(owner));
    }
}

// D2 formulas operate on integers. Damage retains 8 fractional bits through synergy evaluation.
public sealed class SkillValues
{
    private readonly SkillDefinition _skill;
    private readonly Func<string, int> _rank, _baseRank;
    private readonly int _level, _depth;
    public int Rank => _rank(_skill.SourceKey);
    public SkillValues(SkillDefinition skill, Func<string, int> rank, Func<string, int> baseRank, int level, int depth = 0)
    { _skill = skill; _rank = rank; _baseRank = baseRank; _level = level; _depth = depth; }
    public int Param(int index) => _skill.Parameter(index);
    public int Linear(int first, int second) => Param(first) + (Rank - 1) * Param(second);
    public int Diminishing(int first, int second) => Rank <= 0 ? 0
        : Param(first) + (Param(second) - Param(first)) * Math.Min(100, 110 * Rank / (Rank + 6)) / 100;
    public long Eval(string field)
    {
        string expression = _skill.Text(field);
        // Fog's EOF flush evaluates this outer group despite its missing closing token. Keep the pinned text intact.
        if (_skill.Ability == CombatAbility.FireWall && field == "EDmgSymPerCalc"
            && expression == "(skill('Warmth'.blvl)*par8+skill('Inferno'.blvl)*par7") { expression += ")"; }
        return Formula(expression);
    }
    public long Formula(string expression) => SkillFormula.Evaluate(expression, Resolve);
    public long ElementalEval(string field, int minimum256, int maximum256) => SkillFormula.Evaluate(_skill.Text(field),
        (name, argument, property) => name == "enms" ? minimum256 : name == "exms" ? maximum256 : Resolve(name, argument, property));
    private long Resolve(string name, string argument, string property)
    {
        if (_depth > 24) { throw new InvalidDataException("Recursive skill formula."); }
        if (name == "skill")
        {
            var next = new SkillValues(SkillMechanics.Source(argument), _rank, _baseRank, _level, _depth + 1);
            return next.Resolve(property, null, null);
        }
        if (name is "lvl") { return Rank; }
        if (name is "blvl") { return _baseRank(_skill.SourceKey); }
        if (name is "ulvl") { return _level; }
        if (name.StartsWith("par", StringComparison.Ordinal) && int.TryParse(name.AsSpan(3), out int param)) { return Param(param); }
        if (name.Length == 4 && name.StartsWith("pa", StringComparison.Ordinal) && int.TryParse(name.AsSpan(2), out param)) { return Param(param); }
        if (name.Length == 4 && (name.StartsWith("ln", StringComparison.Ordinal) || name.StartsWith("dm", StringComparison.Ordinal)))
        {
            int a = name[2] - '0', b = name[3] - '0';
            // ln91/dm91 use parameters 9 and 10, as encoded by the game formula compiler.
            if (a == 9 && b == 1) { b = 10; }
            return name[0] == 'l' ? Linear(a, b) : Diminishing(a, b);
        }
        if (name.StartsWith("clc", StringComparison.Ordinal) && int.TryParse(name.AsSpan(3), out int calc))
        { return new SkillValues(_skill, _rank, _baseRank, _level, _depth + 1).Eval("calc" + (calc + 1)); }
        return name switch
        {
            "toht" => _skill.Number("ToHit") + Math.Max(0,Rank-1)*_skill.Number("LevToHit"),
            "edmn" => Damage256(false) / 256, "edmx" => Damage256(true) / 256,
            // Formula callback 52 is unshifted elemental minimum including mastery. Prayer has no element/mastery.
            "edns" when _skill.Ability == CombatAbility.Prayer => Rank > 0 ? Damage256(false) : 0,
            "edln" => DurationFrames,
            _ => throw new NotSupportedException($"Unsupported D2 skill formula symbol: {name}({argument}).{property}")
        };
    }
    // Aura handlers use SKILLS_GetManaCosts directly; minmana belongs to ordinary cast validation.
    public int Mana256 => Math.Max(_skill.Number("aura") != 0 ? 0 : _skill.Number("minmana") * 256,
        Math.Max(0, _skill.Number("mana") + Math.Max(0, Rank - 1) * _skill.Number("lvlmana")) << _skill.Number("manashift", 8));
    public int DurationFrames => (int)(Tier(_skill.Number("ELen"), Rank, i => _skill.Number("ELevLen" + Math.Min(i, 3))) * (100 + Eval("ELenSymPerCalc")) / 100);
    public int Damage256(bool maximum)
    {
        if(Rank<=0){return 0;}
        string key = maximum ? "EMax" : "EMin";
        long value = (long)Tier(_skill.Number(key), Rank, i => _skill.Number(key + "Lev" + i)) << _skill.Number("HitShift", 8);
        // SKILLS_GetMinElemDamage deliberately leaves a fixed minimum of one
        // (or less) unsynergized when the first tier increment is zero.
        if(maximum || value>256 || _skill.Number("EMinLev1")!=0){value += value*Eval("EDmgSymPerCalc")/100;}
        return (int)Math.Clamp(value, 0, int.MaxValue);
    }
    public double Damage(bool maximum) => Damage256(maximum) / 256.0;
    internal int PhysicalDamage256(bool maximum)
    {
        // Physical SKILLS_GetMin/MaxPhysDamage truncates synergy before the
        // HitShift, unlike elemental endpoints. No weapon/kick component here.
        string key=maximum?"Max":"Min";
        long basis=Tier(_skill.Number(key+"Dam"),Rank,i=>_skill.Number(key+"LevDam"+i));
        basis+=basis*Eval("DmgSymPerCalc")/100;
        return (int)Math.Clamp(basis<<_skill.Number("HitShift",8),0,int.MaxValue);
    }
    public static int Tier(int baseline, int rank, Func<int, int> increase)
    {
        int extra = Math.Max(0, rank - 1);
        int result = baseline;
        int[] lengths = [7, 8, 6, 6, int.MaxValue];
        for (int i = 0; i < lengths.Length && extra > 0; i++)
        { int count = Math.Min(extra, lengths[i]); result += count * increase(i + 1); extra -= count; }
        return result;
    }
}

// A small expression interpreter, with no execution of source text or unknown-symbol fallback.
public static class SkillFormula
{
    public static long Evaluate(string source, Func<string, string, string, long> resolve)
    {
        source = source.Trim().Trim('"');
        if (source.Length == 0) { return 0; }
        var parser = new Parser(source, resolve);
        long value = parser.Conditional(true); parser.Space();
        if (!parser.End) { throw new InvalidDataException($"Unexpected D2 formula input at {parser.Offset}: {source}"); }
        return value;
    }
    private sealed class Parser(string source, Func<string, string, string, long> resolve)
    {
        internal int Offset;
        internal bool End => Offset >= source.Length;
        internal void Space() { while (!End && char.IsWhiteSpace(source[Offset])) { Offset++; } }
        private bool Eat(string value)
        { Space(); if (!source.AsSpan(Offset).StartsWith(value, StringComparison.Ordinal)) { return false; } Offset += value.Length; return true; }
        private void Require(string value) { if (!Eat(value)) { throw new InvalidDataException($"Expected {value} in D2 formula: {source}"); } }
        internal long Conditional(bool active)
        {
            long condition = Compare(active);
            if (!Eat("?")) { return condition; }
            long yes = Conditional(active && condition != 0); Require(":");
            long no = Conditional(active && condition == 0);
            return condition != 0 ? yes : no;
        }
        private long Compare(bool active)
        {
            long a = Add(active);
            foreach (string op in new[] { ">=", "<=", "==", "!=", ">", "<" })
            {
                if (!Eat(op)) { continue; }
                long b = Add(active);
                return (op switch { ">=" => a >= b, "<=" => a <= b, "==" => a == b, "!=" => a != b, ">" => a > b, _ => a < b }) ? 1 : 0;
            }
            return a;
        }
        private long Add(bool active)
        {
            long a = Multiply(active);
            while (true)
            { if (Eat("+")) { a += Multiply(active); } else if (Eat("-")) { a -= Multiply(active); } else { return a; } }
        }
        private long Multiply(bool active)
        {
            long a = Atom(active);
            while (true)
            {
                if (Eat("*")) { a *= Atom(active); }
                else if (Eat("/")) { long b = Atom(active); a = active ? a / b : 0; }
                else { return a; }
            }
        }
        private long Atom(bool active)
        {
            if (Eat("-")) { return -Atom(active); }
            if (Eat("+")) { return Atom(active); }
            if (Eat("(")) { long value = Conditional(active); Require(")"); return value; }
            Space(); int start = Offset;
            if (!End && char.IsDigit(source[Offset]))
            {
                while (!End && char.IsDigit(source[Offset])) { Offset++; }
                return long.Parse(source.AsSpan(start, Offset - start), CultureInfo.InvariantCulture);
            }
            while (!End && (char.IsLetterOrDigit(source[Offset]) || source[Offset] == '_')) { Offset++; }
            if (Offset == start) { throw new InvalidDataException($"Missing D2 formula operand: {source}"); }
            string name = source[start..Offset];
            if (name is "min" or "max")
            {
                Require("("); long a = Conditional(active); Require(","); long b = Conditional(active); Require(")");
                return name == "min" ? Math.Min(a, b) : Math.Max(a, b);
            }
            string argument = null, property = null;
            if (name is "skill" or "stat")
            {
                Require("("); Require("'"); start = Offset;
                while (!End && source[Offset] != '\'') { Offset++; }
                argument = source[start..Offset]; Require("'"); Require("."); Space(); start = Offset;
                while (!End && char.IsLetterOrDigit(source[Offset])) { Offset++; }
                property = source[start..Offset]; Require(")");
            }
            return active ? resolve(name, argument, property) : 0;
        }
    }
}
