// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sanctuary;

public readonly record struct NamedPropertyRoll(string Key, int Value, int Maximum);

// Source arguments differ between scalar rolls, combat ranges and scaled stats.
// Unknown effects remain in the catalogue and cannot enter live acquisition.
public static class NamedPropertyRuntime
{
    private static readonly HashSet<string> _scalar = new(StringComparer.Ordinal)
    {
        "str", "dex", "enr", "vit", "all-stats", "hp", "mana", "stam", "att", "att%", "ac", "ac%",
        "dmg%", "dmg-min", "dmg-max", "res-all", "res-fire", "res-cold", "res-ltng", "res-pois",
        "swing1", "swing2", "swing3", "cast1", "cast2", "cast3", "move1", "move2", "move3", "block",
        "balance1", "balance2", "balance3", "gold%", "mag%", "lifesteal", "manasteal", "regen", "regen-mana",
        "red-mag", "thorns", "dmg-undead", "ease", "dur", "sock",
        "allskills", "fireskill", "ama", "sor", "nec", "pal", "bar", "dru", "ass", "skilltab", "skill",
        "hp%", "mana%", "regen-stam", "nofreeze", "half-freeze", "res-pois-len", "indestruct", "ethereal",
        "fire-min", "fire-max", "cold-min", "cold-max", "cold-len", "ltng-min", "ltng-max", "pois-min", "pois-max", "pois-len",
        "red-dmg", "red-dmg%", "res-all-max", "res-fire-max", "res-cold-max", "res-ltng-max", "res-pois-max",
        "mana-kill", "heal-kill", "demon-heal", "dmg-demon", "att-undead", "att-demon", "light-thorns", "dmg", "light",
        "deadly", "crush", "openwounds", "noheal", "ignore-ac", "reduce-ac", "ac-miss", "ac-hth", "block2", "block3", "knock",
        "res-mag", "extra-mag", "pierce-mag", "extra-fire", "extra-cold", "extra-ltng", "extra-pois", "pierce-fire", "pierce-cold", "pierce-ltng", "pierce-pois",
        "fade", "bloody", "pierce", "slow", "freeze", "stupidity", "howl", "stamdrain", "rip", "dmg-ac", "rep-dur", "rep-quant", "stack", "dmg-to-mana", "abs-fire", "abs-cold", "abs-ltng", "abs-fire%", "abs-cold%", "abs-ltng%", "addxp"
    };
    private static readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal)
    {
        ["lifesteal"] = "life-steal%", ["manasteal"] = "mana-steal%", ["regen"] = "replenish-life",
        ["regen-mana"] = "mana-regen%", ["red-mag"] = "magic-reduction", ["dmg-undead"] = "dmg-undead%",
        ["red-dmg%"] = "res-physical", ["red-dmg"] = "physical-reduction", ["dmg-demon"] = "dmg-demon%"
    };
    private static readonly HashSet<string> _range = new(StringComparer.Ordinal) { "dmg-fire", "dmg-ltng", "dmg-cold", "dmg-pois", "dmg-norm", "dmg-mag", "dmg-elem" };
    private static readonly Dictionary<string, string> _scaled = new(StringComparer.Ordinal)
    {
        ["str/lvl"] = "str", ["dex/lvl"] = "dex", ["vit/lvl"] = "vit", ["hp/lvl"] = "hp",
        ["mana/lvl"] = "mana", ["stam/lvl"] = "stam", ["ac/lvl"] = "ac", ["att/lvl"] = "att",
        ["att%/lvl"] = "att%", ["mag%/lvl"] = "mag%", ["gold%/lvl"] = "gold%", ["dmg/lvl"] = "dmg-max",
        ["dmg%/lvl"] = "dmg%", ["thorns/lvl"] = "thorns", ["dmg-dem/lvl"] = "dmg-demon%",
        ["dmg-und/lvl"] = "dmg-undead%", ["att-dem/lvl"] = "att-demon", ["att-und/lvl"] = "att-undead",
        ["regen-stam/lvl"] = "regen-stam", ["dmg-cold/lvl"] = "cold-max", ["dmg-ltng/lvl"] = "ltng-max",
        ["deadly/lvl"] = "deadly", ["res-ltng/lvl"] = "res-ltng", ["abs-fire/lvl"] = "abs-fire",
        ["abs-cold/lvl"] = "abs-cold", ["abs-ltng/lvl"] = "abs-ltng", ["dmg-fire/lvl"] = "fire-max"
    };
    public static bool Supported(NamedProperty property) => !property.SourceEnabled
        || property.Code == "state" && property.Parameter?.ToString() == "fullsetgeneric"
        || _scalar.Contains(property.Code) || _range.Contains(property.Code) || _scaled.ContainsKey(property.Code);

    public static int Parameter(NamedProperty property) => property.Parameter is { } value
        && value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt32(out int number) ? number : 0;

    public static NamedPropertyRoll Roll(string key, NamedProperty template, Random random)
    {
        if (!Supported(template) || !template.SourceEnabled) { throw new InvalidOperationException("An implemented named property is required."); }
        if (_scaled.ContainsKey(template.Code)) { return new(key, Parameter(template), Parameter(template)); }
        if (template.Code is "rep-dur" or "rep-quant" || template.Code == "sock" && Parameter(template) > 0) { return new(key, Parameter(template), Parameter(template)); }
        int min = template.SourceMin ?? 0, max = template.SourceMax ?? min;
        if (_range.Contains(template.Code)) { return new(key, min, max); }
        if (max < min) { throw new InvalidDataException("Invalid scalar property range."); }
        int rolled = random.Next(min, checked(max + 1));
        return new(key, rolled, rolled);
    }

    public static void Validate(NamedProperty template, NamedPropertyRoll roll)
    {
        if (!Supported(template) || !template.SourceEnabled) { throw new InvalidDataException("Unsupported saved named property."); }
        int min = template.SourceMin ?? 0, max = template.SourceMax ?? min;
        bool fixedParameter = _scaled.ContainsKey(template.Code) || template.Code is "rep-dur" or "rep-quant" || template.Code == "sock" && Parameter(template) > 0;
        bool valid = fixedParameter ? roll.Value == Parameter(template) && roll.Maximum == roll.Value
            : _range.Contains(template.Code) ? roll.Value == min && roll.Maximum == max
            : roll.Value >= min && roll.Value <= max && roll.Maximum == roll.Value;
        if (!valid) { throw new InvalidDataException("Named property escaped its definition."); }
    }

    public static int Bonus(NamedProperty template, NamedPropertyRoll roll, string code, int level)
    {
        if (_scaled.TryGetValue(template.Code, out string scaled))
        { return scaled == code ? (int)((long)roll.Value * Math.Clamp(level, 1, 99) / 8) : 0; }
        if (template.Code == "all-stats") { return code is "str" or "dex" or "enr" or "vit" ? roll.Value : 0; }
        if (template.Code == "dmg-norm") { return code == "dmg-min" ? roll.Value : code == "dmg-max" ? roll.Maximum : 0; }
        // Keep item_normaldamage distinct for shield-based Smite. Existing weapon
        // endpoints still consume its min/max projection exactly once.
        if (template.Code == "dmg") { return code is "dmg-min" or "dmg-max" or "normal-damage" ? roll.Value : 0; }
        return (_aliases.GetValueOrDefault(template.Code) ?? template.Code) == code ? roll.Value : 0;
    }

    public static IEnumerable<(string Key, NamedProperty Property)> Templates(NamedItemDefinition item)
    {
        foreach (var property in item.Properties.Where(p => p.SourceEnabled)) { yield return ("p/" + property.Slot, property); }
        foreach (var property in item.AdditionalProperties.Where(p => p.SourceEnabled)) { yield return ("a/" + property.Slot, property); }
    }
}
