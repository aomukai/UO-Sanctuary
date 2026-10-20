// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;

namespace Sanctuary;

public enum GemQuality : byte { Chipped, Flawed, Standard, Flawless, Perfect }
public sealed record GemEffect(string Code, string Label, int Minimum, int Maximum, int DurationMs);
public sealed record GemEffects(GemEffect[] Weapon, GemEffect[] Shield, GemEffect[] Armor);
public sealed record GemFamily(string Id, string Name, string Source, int Art, int Hue, GemEffects[] Effects);
public sealed record SocketGemSettings(int SchemaVersion, string Source, int DropDenominator, int DropTickets,
    int Quantity, string Selection, bool FullPackAtFeet, double[] MinimumSkills, int[] Prices,
    int UpgradeRatio, double[] UpgradeSkills, GemFamily[] Families);

public static class SocketGems
{
    public static SocketGemSettings Settings { get; private set; }
    private static readonly Dictionary<string, GemFamily> _families = new(StringComparer.Ordinal);
    private static readonly HashSet<Type> _legacy = [typeof(Amber), typeof(Amethyst), typeof(Citrine), typeof(Diamond),
        typeof(Emerald), typeof(Ruby), typeof(Sapphire), typeof(StarSapphire), typeof(Tourmaline)];
    private static readonly HashSet<string> _codes = new(StringComparer.Ordinal)
    { "att", "ac", "str", "dex", "hp", "mana", "dmg-undead%", "res-all", "res-pois", "res-fire", "res-cold", "res-ltng",
        "poison-damage", "fire-damage", "cold-damage", "lightning-damage", "mag%", "life-steal%", "mana-steal%", "thorns",
        "replenish-life", "mana-regen%", "magic-reduction" };
    public static bool Legacy(Type type) => _legacy.Contains(type);
    public static GemFamily Family(string id) => _families.TryGetValue(id, out var row) ? row : throw new InvalidDataException("Unknown gem family.");
    public static void Load()
    {
        var rows = BalanceCatalog.Read<SocketGemSettings>("socket-gems");
        if (rows.Length != 1) { throw new InvalidDataException("One gem catalogue is required."); }
        Settings = rows[0]; var s = Settings; _families.Clear();
        if (s.SchemaVersion != 1 || s.Families.Length != 9 || s.MinimumSkills.Length != 5 || s.Prices.Length != 5
            || s.UpgradeSkills.Length != 4 || s.UpgradeRatio is < 2 or > 10 || s.Quantity != 1 || s.DropDenominator != 100
            || s.DropTickets is < 0 or > 100 || s.Selection != "uniform-family-and-unlocked-quality")
        { throw new InvalidDataException("Invalid gem progression."); }
        for (int i = 0; i < 5; i++)
        {
            if (s.MinimumSkills[i] != 40 + i * 10 || s.Prices[i] is < 1 or > 60000
                || i < 4 && s.UpgradeSkills[i] is < 0 or > 120) { throw new InvalidDataException("Invalid gem quality band."); }
        }
        foreach (var row in s.Families)
        {
            if (row.Art is < 1 or > 65535 || string.IsNullOrWhiteSpace(row.Name) || row.Effects.Length != 5 || !_families.TryAdd(row.Id, row))
            { throw new InvalidDataException("Invalid gem family."); }
            foreach (var effects in row.Effects) { Validate(effects); }
        }
    }
    internal static void Validate(GemEffects effects)
    {
        if (effects == null) { throw new InvalidDataException("Missing gem effects."); }
        foreach (var group in new[] { effects.Weapon, effects.Shield, effects.Armor })
        {
            if (group is not { Length: >= 1 and <= 3 }) { throw new InvalidDataException("Invalid gem effects."); }
            var seen = new HashSet<string>();
            foreach (var effect in group)
            {
                if (effect == null || !_codes.Contains(effect.Code) || !seen.Add(effect.Code) || string.IsNullOrWhiteSpace(effect.Label)
                    || effect.Minimum is < 0 or > 10000 || effect.Maximum < effect.Minimum || effect.Maximum > 10000
                    || effect.DurationMs is < 0 or > 60000) { throw new InvalidDataException("Invalid gem effect value."); }
            }
        }
    }
    public static int Unlocked(double skill)
    { int count = 0; foreach (double minimum in Settings.MinimumSkills) { if (skill >= minimum) { count++; } } return count; }
    public static SocketGem Roll(double trainedSkill, Random random = null)
    {
        int count = Unlocked(trainedSkill); if (count == 0) { return null; } random ??= Random.Shared;
        if (random.Next(Settings.DropDenominator) >= Settings.DropTickets) { return null; }
        var family = Settings.Families[random.Next(Settings.Families.Length)]; var quality = (GemQuality)random.Next(count);
        return ItemOrigins.Create(ItemOrigin.Drop, () => new SocketGem(family.Id, quality));
    }
    public static void Award(Mobile from, double trainedSkill, Random random = null)
    {
        if (from is not { Deleted: false, Alive: true } || from.Map == null || from.Map == Map.Internal) { return; }
        var gem = Roll(trainedSkill, random); if (gem == null) { return; } string name = gem.Name;
        if (from.PlaceInBackpack(gem)) { from.SendMessage($"You find {name} and place it in your backpack."); }
        else if (Settings.FullPackAtFeet)
        { gem.MoveToWorld(from.Location, from.Map); from.SendMessage($"You find {name}. Your backpack is full; the gem is at your feet."); }
        else { gem.Delete(); from.SendMessage("You found a gem but could not carry it."); }
    }
}
