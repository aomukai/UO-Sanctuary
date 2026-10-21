// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sanctuary;

// Source definitions remain separate from NamedGear's reviewed runtime eligibility.
public sealed record NamedProperty(string Slot, string Code, string SourceCode, bool SourceEnabled,
    JsonElement? Parameter, int? SourceMin, int? SourceMax, string Activation = null,
    int RequiredPieces = 0, string RequiredPieceId = null);
public sealed record NamedSourceRecipe(string SourceId, string OutputColumn, JsonElement SourceFields);
public sealed record NamedItemDefinition(string Id, int SourceId, int SourceRow, string SourceKey,
    string Name, string Rarity, string BaseCode, string SourceAvailability, bool SourceSpawnable,
    bool ResurrectedExpansionAddition, int SelectionWeight, int QualityLevel, int RequiredLevel,
    NamedProperty[] Properties, NamedProperty[] AdditionalProperties, string SetId,
    NamedSourceRecipe[] Recipes, JsonElement SourceFields);
public sealed record NamedSetDefinition(string Id, int SourceRow, string SourceKey, string Name,
    bool SourceAvailable, string[] PieceIds, NamedProperty[] PartialBonuses,
    NamedProperty[] FullBonuses, JsonElement SourceFields);
public sealed record NamedAppearance(string Type, string Name);
public sealed record NamedBaseDefinition(string Code, string Name, string SourceTable, string Tier,
    string ItemType, string RequiredClass, int SourceRequiredLevel, bool ProjectBasePresent,
    NamedAppearance[] ProjectAppearances, JsonElement SourceFields);
public sealed record NamedPropertyDefinition(string Code, string Kind, string[] UsedBy, JsonElement SourceFields);
public sealed record NamedBonusPreview(string OwnerId, string Scope, NamedProperty Property);

public sealed class NamedEquipmentCatalog
{
    private readonly Dictionary<string, NamedItemDefinition> _items;
    private readonly Dictionary<string, NamedSetDefinition> _sets;
    private readonly Dictionary<string, NamedBaseDefinition> _bases;
    private readonly Dictionary<string, NamedPropertyDefinition> _properties;
    public IReadOnlyCollection<NamedItemDefinition> Items => _items.Values;
    public IReadOnlyCollection<NamedSetDefinition> Sets => _sets.Values;
    public IReadOnlyCollection<NamedBaseDefinition> Bases => _bases.Values;
    public IReadOnlyCollection<NamedPropertyDefinition> Properties => _properties.Values;

    private NamedEquipmentCatalog(NamedItemDefinition[] items, NamedSetDefinition[] sets,
        NamedBaseDefinition[] bases, NamedPropertyDefinition[] properties)
    {
        _items = Index(items, i => i.Id);
        _sets = Index(sets, s => s.Id);
        _bases = Index(bases, b => b.Code);
        _properties = Index(properties, p => p.Code);
        Validate();
    }

    public static NamedEquipmentCatalog Load(string balanceDirectory)
    {
        using var coverage = JsonDocument.Parse(File.ReadAllText(Path.Combine(balanceDirectory, "named-item-coverage.json")));
        if (coverage.RootElement.GetProperty("schemaVersion").GetInt32() != 1
            || coverage.RootElement.GetProperty("activation").GetString() != "RuntimeGated")
        {
            throw new InvalidDataException("Unknown named-equipment definition format.");
        }
        return new(Read<NamedItemDefinition>(balanceDirectory, "unique-items")
                .Concat(Read<NamedItemDefinition>(balanceDirectory, "set-items")).ToArray(),
            Read<NamedSetDefinition>(balanceDirectory, "equipment-sets"),
            Read<NamedBaseDefinition>(balanceDirectory, "named-item-bases"),
            Read<NamedPropertyDefinition>(balanceDirectory, "named-item-properties"));
    }

    private static T[] Read<T>(string directory, string name) => JsonSerializer.Deserialize<T[]>(
        File.ReadAllText(Path.Combine(directory, name + ".json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Empty named-equipment definitions: " + name);

    private static Dictionary<string, T> Index<T>(IEnumerable<T> rows, Func<T, string> key)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            string id = row == null ? null : key(row);
            if (string.IsNullOrEmpty(id) || !result.TryAdd(id, row))
            {
                throw new InvalidDataException("Missing or duplicate named-equipment identity.");
            }
        }
        return result;
    }

    public NamedItemDefinition Item(string id) => _items.TryGetValue(id, out var item) ? item
        : throw new KeyNotFoundException("Unknown named item: " + id);
    public NamedSetDefinition Set(string id) => _sets.TryGetValue(id, out var set) ? set
        : throw new KeyNotFoundException("Unknown item set: " + id);
    public NamedBaseDefinition Base(string code) => _bases.TryGetValue(code, out var basis) ? basis
        : throw new KeyNotFoundException("Unknown named-item base: " + code);
    public int SourceRequiredLevel(string id)
    {
        var item = Item(id);
        return Math.Max(item.RequiredLevel, item.BaseCode == null ? 0 : Base(item.BaseCode).SourceRequiredLevel);
    }

    // Source eligibility only: does not grant project creation/drop permission.
    public IEnumerable<NamedItemDefinition> SourceCandidates(string baseCode, string rarity, int itemLevel)
    {
        if (rarity is not ("Unique" or "Set") || itemLevel is < 1 or > 99)
        {
            throw new ArgumentException("A named rarity and item level1–99 are required.");
        }
        return _items.Values.Where(i => i.BaseCode == baseCode && i.Rarity == rarity
            && i.SourceSpawnable && i.SelectionWeight > 0 && i.QualityLevel <= itemLevel);
    }

    public string[] UnresolvedProperties(string id)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string code)
        {
            if (!visited.Add(code)) { return; }
            var definition = _properties[code];
            if (definition.Kind == "MissingSourceDefinition") { found.Add(code); }
            if (definition.Kind == "PropertyGroup")
            {
                foreach (var entry in definition.SourceFields.EnumerateObject())
                {
                    if (GroupChild(entry)) { Visit(entry.Value.GetString()); }
                }
            }
        }
        foreach (var prop in Item(id).Properties.Concat(Item(id).AdditionalProperties))
        {
            if (prop.SourceEnabled) { Visit(prop.Code); }
        }
        return found.Order(StringComparer.Ordinal).ToArray();
    }

    // Pure authoring preview. The eventual caller must supply only identified,
    // legal, working equipment on this wearer; inventory and wardrobes are absent.
    // Returns property templates, not rolled numbers or live stat modifications.
    public NamedBonusPreview[] PreviewSetBonuses(IEnumerable<string> activePieceIds)
    {
        var active = new HashSet<string>(activePieceIds, StringComparer.Ordinal);
        foreach (string id in active)
        {
            if (Item(id).Rarity != "Set") { throw new ArgumentException("Expected a set-piece identity."); }
        }
        var result = new List<NamedBonusPreview>();
        foreach (var group in active.Select(Item).GroupBy(i => i.SetId))
        {
            var set = Set(group.Key);
            int count = group.Count();
            foreach (var piece in group)
            {
                foreach (var prop in piece.AdditionalProperties)
                {
                    bool enabled = prop.Activation switch
                    {
                        "Always" => true,
                        "PieceCount" => count >= prop.RequiredPieces,
                        "SpecificPiece" => active.Contains(prop.RequiredPieceId),
                        _ => false
                    };
                    if (prop.SourceEnabled && enabled) { result.Add(new(piece.Id, "Piece", prop)); }
                }
            }
            foreach (var prop in set.PartialBonuses)
            {
                if (prop.SourceEnabled && count >= prop.RequiredPieces) { result.Add(new(set.Id, "Partial", prop)); }
            }
            if (set.PieceIds.All(active.Contains))
            {
                foreach (var prop in set.FullBonuses)
                {
                    if (prop.SourceEnabled) { result.Add(new(set.Id, "Full", prop)); }
                }
            }
        }
        return result.ToArray();
    }

    private static bool GroupChild(JsonProperty entry) => entry.Name.StartsWith("Prop", StringComparison.Ordinal)
        && entry.Name.Length > 4 && int.TryParse(entry.Name.AsSpan(4), out _)
        && entry.Value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(entry.Value.GetString());

    private void ValidateProperties(NamedProperty[] rows)
    {
        if (rows == null) { throw new InvalidDataException("Missing named properties."); }
        var slots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row == null || !slots.Add(row.Slot) || string.IsNullOrEmpty(row.SourceCode)
                || !_properties.ContainsKey(row.Code))
            {
                throw new InvalidDataException("Missing, duplicate-slot or unknown named property.");
            }
        }
    }

    private void Validate()
    {
        foreach (var definition in _properties.Values)
        {
            if (definition.Kind is not ("Property" or "PropertyGroup" or "MissingSourceDefinition")
                || definition.SourceFields.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Invalid named property definition.");
            }
            if (definition.Kind == "PropertyGroup")
            {
                foreach (var field in definition.SourceFields.EnumerateObject())
                {
                    if (GroupChild(field) && !_properties.ContainsKey(field.Value.GetString()))
                    {
                        throw new InvalidDataException("Unknown property-group child.");
                    }
                }
            }
        }
        foreach (var basis in _bases.Values)
        {
            if (string.IsNullOrEmpty(basis.Name) || basis.SourceFields.ValueKind != JsonValueKind.Object
                || basis.ProjectAppearances == null)
            {
                throw new InvalidDataException("Invalid named-item base.");
            }
        }
        foreach (var item in _items.Values)
        {
            if (item.Rarity is not ("Unique" or "Set") || item.Id != (item.Rarity == "Unique" ? "unique/" : "piece/") + item.SourceId
                || string.IsNullOrEmpty(item.Name) || item.SourceFields.ValueKind != JsonValueKind.Object
                || item.BaseCode != null && !_bases.ContainsKey(item.BaseCode)
                || item.BaseCode == null && item.SourceAvailability != "IncompleteSourceRow")
            {
                throw new InvalidDataException("Invalid named-item identity or base.");
            }
            ValidateProperties(item.Properties);
            ValidateProperties(item.AdditionalProperties);
            if (item.Rarity == "Set")
            {
                var set = Set(item.SetId);
                if (!set.PieceIds.Contains(item.Id)) { throw new InvalidDataException("Set membership is not reciprocal."); }
                foreach (var prop in item.AdditionalProperties)
                {
                    if (prop.Activation is not ("Always" or "PieceCount" or "SpecificPiece")
                        || prop.Activation == "PieceCount" && prop.RequiredPieces < 2
                        || prop.Activation == "SpecificPiece" && (prop.RequiredPieceId == item.Id || !set.PieceIds.Contains(prop.RequiredPieceId)))
                    {
                        throw new InvalidDataException("Invalid set-piece bonus activation.");
                    }
                }
            }
            else if (item.SetId != null || item.AdditionalProperties.Length != 0)
            {
                throw new InvalidDataException("Unique definition has set membership.");
            }
        }
        foreach (var set in _sets.Values)
        {
            if (set.Id != "set/" + set.SourceRow || set.PieceIds.Length is < 2 or > 6
                || set.PieceIds.Distinct().Count() != set.PieceIds.Length
                || set.PieceIds.Any(id => Item(id).SetId != set.Id))
            {
                throw new InvalidDataException("Invalid item-set identity or membership.");
            }
            ValidateProperties(set.PartialBonuses);
            ValidateProperties(set.FullBonuses);
            if (set.PartialBonuses.Any(p => p.RequiredPieces < 2))
            {
                throw new InvalidDataException("Invalid partial-set threshold.");
            }
        }
    }
}
