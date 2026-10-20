// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record RareRecipe(string Id, string Name, SkillName Skill, double Required);
public static class RareRecipes
{
    private static readonly Dictionary<string, RareRecipe> _rows = new(StringComparer.Ordinal);
    public static IReadOnlyCollection<RareRecipe> All => _rows.Values;
    public static LearnedRecipes Profiles { get; private set; }
    internal static string Equipment(EquipmentRecipe row) => "equipment/" + row.Id;
    internal static string Meal(MealRecipe row) => "meal/" + row.Id;
    internal static string Alchemy(AlchemyRecipe row) => $"alchemy/{row.Kind}/{row.Tier}";
    public static RareRecipe Find(string id) => id != null ? _rows.GetValueOrDefault(id) : null;
    public static void Load()
    {
        _rows.Clear();
        foreach (var row in EquipmentCrafting.Recipes)
        { if (row.RequiredSkill == 120) { Add(new(Equipment(row), $"Elite {row.Name}", row.Skill, 120)); } }
        foreach (var row in Meals.Recipes)
        { if (row.Tier == 3) { Add(new(Meal(row), row.Name, SkillName.Cooking, row.Skill)); } }
        foreach (var row in AlchemyRestoratives.Recipes)
        { if (row.Skill == 120) { Add(new(Alchemy(row), PotionBeltCatalog.Potion(row.Kind, row.Tier).Name, SkillName.Alchemy, 120)); } }
        Add(new("key/splendid", "Splendid skeleton key", SkillName.Tinkering, 120));
        Add(new("imbuing/elite", "Elite equipment imbuing", SkillName.Imbuing, 120));
    }
    private static void Add(RareRecipe row)
    { if (!_rows.TryAdd(row.Id, row)) { throw new InvalidDataException("Duplicate rare recipe identity."); } }
    public static void Configure()
    {
        Profiles = new(); EventSink.WorldSave += Profiles.Prune;
    }
    public static bool Allowed(Mobile owner, string id, bool prompt = true)
    {
        if (!ClassProfessions.Affected(owner) || Find(id) == null || Profiles.Knows(owner, id)) { return true; }
        if (prompt) { owner.SendMessage($"You must first learn the treasure recipe: {Find(id).Name}."); }
        return false;
    }
    internal static bool Learn(PlayerMobile owner, RareRecipeScroll scroll)
    {
        if (!ClassProfessions.Affected(owner) || !ClassProfessions.Ready(owner) || !RuneCrafting.Accessible(owner, scroll)
            || Find(scroll.RecipeId) is not { } row || Profiles.Knows(owner, row.Id)) { return false; }
        Profiles.Learn(owner, row.Id); scroll.Delete();
        owner.SendMessage($"You permanently learned {row.Name}. Its skill, tool and workshop requirements still apply.");
        return true;
    }
    internal static RareRecipeScroll Roll(Random random)
    {
        int index = random.Next(_rows.Count);
        foreach (var row in _rows.Values) { if (index-- == 0) { return new(row.Id); } }
        throw new InvalidOperationException("Empty rare recipe pool.");
    }
}
public sealed class LearnedRecipes : GenericPersistence
{
    private readonly Dictionary<Serial, HashSet<string>> _known = new();
    public LearnedRecipes() : base("SanctuaryLearnedRecipes", 127) { }
    public bool Knows(Mobile owner, string id) => _known.GetValueOrDefault(owner.Serial)?.Contains(id) == true;
    internal void Learn(Mobile owner, string id)
    {
        if (RareRecipes.Find(id) == null) { throw new ArgumentException("Unknown rare recipe."); }
        if (!_known.TryGetValue(owner.Serial, out var rows)) { _known.Add(owner.Serial, rows = new(StringComparer.Ordinal)); }
        rows.Add(id);
    }
    public void Prune()
    {
        var removed = new List<Serial>();
        foreach (var serial in _known.Keys) { if (World.FindMobile(serial) is not { Deleted: false }) { removed.Add(serial); } }
        foreach (var serial in removed) { _known.Remove(serial); }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_known.Count);
        foreach (var (serial, rows) in _known)
        { writer.Write(serial.Value); writer.Write(rows.Count); foreach (string id in rows) { writer.Write(id); } }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown learned recipe save version."); }
        int count = reader.ReadInt(); if (count < 0) { throw new InvalidDataException("Invalid recipe owner count."); }
        _known.Clear();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); int size = reader.ReadInt();
            if (size < 0 || size > RareRecipes.All.Count) { throw new InvalidDataException("Invalid learned recipe count."); }
            var rows = new HashSet<string>(StringComparer.Ordinal);
            for (int n = 0; n < size; n++)
            { string id = reader.ReadString(); if (RareRecipes.Find(id) == null || !rows.Add(id)) { throw new InvalidDataException("Invalid learned recipe identity."); } }
            _known.Add(serial, rows);
        }
    }
    public override void PostDeserialize() => Prune();
}
[SerializationGenerator(0)]
public partial class RareRecipeScroll : Item
{
    [SerializableField(0, setter: "private")] private string _recipeId;
    [Constructible] public RareRecipeScroll() : this("key/splendid") { }
    public RareRecipeScroll(string id) : base(0x2831)
    {
        var row = RareRecipes.Find(id) ?? throw new ArgumentException("Unknown rare recipe.");
        _recipeId = id; Name = $"recipe: {row.Name}"; Weight = 0.1;
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (RareRecipes.Find(_recipeId) is { } row)
        { list.Add($"{ClassProfessions.Name(row.Skill)}\t{row.Required:F1}"); list.Add($"{"Permanently learned by this character; duplicate recipes are not consumed."}"); }
    }
    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile owner && !RareRecipes.Learn(owner, this))
        { owner.SendMessage("Keep the recipe in your backpack. Already learned recipes remain tradable."); }
    }
}
