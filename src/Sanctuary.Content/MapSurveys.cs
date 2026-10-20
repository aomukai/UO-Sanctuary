// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Regions;

namespace Sanctuary;

public sealed record SurveyRecipe(string Id, string Name, double Skill, double Maximum, string Effect, int Magnitude);
public sealed record SurveyDungeon(string Map, string Name);
public static class MapSurveys
{
    public static SurveyRecipe[] Recipes { get; private set; } = [];
    public static SurveyDungeon[] Dungeons { get; private set; } = [];
    private static readonly HashSet<string> _dungeons = new(StringComparer.Ordinal);
    public static void Load()
    {
        Recipes = BalanceCatalog.Read<SurveyRecipe>("map-surveys");
        Dungeons = BalanceCatalog.Read<SurveyDungeon>("survey-dungeons");
        if (Recipes.Length != 5 || Dungeons.Length == 0) { throw new InvalidDataException("Map surveys need five recipes and named dungeons."); }
        var ids = new HashSet<string>();
        foreach (var r in Recipes)
        {
            if (!TimedEffects.Identifier(r.Id) || !ids.Add(r.Id) || r.Name is not { Length: > 0 and <= 40 }
                || r.Skill is < 0 or > 100 || r.Maximum <= r.Skill || r.Maximum > 120
                || !(r.Effect is "city" or "world" && r.Magnitude == 5 || r.Effect == "dungeon" && r.Magnitude is 10 or 20 or 30))
            { throw new InvalidDataException("Invalid survey recipe."); }
        }
        foreach (var r in Dungeons)
        {
            if (r.Map is not ("Felucca" or "Trammel") || r.Name is not { Length: > 0 and <= 100 }
                || !_dungeons.Add(r.Map + "/" + r.Name)) { throw new InvalidDataException("Invalid named survey dungeon."); }
        }
    }
    public static SurveyRecipe Find(string id) => Array.Find(Recipes, r => r.Id == id) ?? throw new ArgumentOutOfRangeException(nameof(id));
    public static void Configure()
    {
        ClassMapHooks.LocationChanged = CheckLocation;
        ClassMapHooks.SearchRange = (from, range) => range + (WorldBonus(from) ? 5 : 0);
        EventSink.Connected += CheckLocation;
    }
    public static string Dungeon(Mobile owner)
    {
        var map = owner.Map; var point = owner.Location;
        if (map == Map.Internal && owner is PlayerMobile p) { map = p.LogoutMap; point = p.LogoutLocation; }
        DungeonRegion dungeon = null;
        for (var r = Region.Find(point, map); r != null; r = r.Parent)
        { if (r is DungeonRegion d) { dungeon = d; } }
        string key = dungeon == null ? null : map.Name + "/" + dungeon.Name;
        return key != null && (_dungeons.Contains(key) || dungeon is ExpeditionRegion) ? key : null;
    }
    internal static void CheckLocation(Mobile owner)
    {
        if (owner.Map == Map.Internal) { return; }
        if (TimedEffects.Buff(owner, TimedEffectCategory.Map) is { Id: "survey-dungeon" } buff && buff.Context != Dungeon(owner))
        { TimedEffects.RemoveBuff(owner, TimedEffectCategory.Map); if (owner is PlayerMobile p) { StatNetwork.Send(p); } }
    }
    public static int MagicFind(PlayerMobile owner)
    {
        CheckLocation(owner);
        return ClassProfessions.Enabled && TimedEffects.Buff(owner, TimedEffectCategory.Map) is { Id: "survey-dungeon" } b
            && b.Context == Dungeon(owner) ? b.Magnitude : 0;
    }
    public static bool WorldBonus(Mobile owner) => ClassProfessions.Enabled
        && TimedEffects.Buff(owner, TimedEffectCategory.Map) is { Id: "survey-world" };
    internal static TimedBuff City(Mobile owner) => ClassProfessions.Affected(owner)
        && TimedEffects.Buff(owner, TimedEffectCategory.Map) is { Id: "survey-city" } b ? b : null;
    internal static int Price(int price, bool discount) => discount ? Math.Max(1, (int)(((long)price * 95 + 99) / 100)) : price;
    public static int PurchasePrice(Mobile owner, int price) => Price(price, City(owner) != null);
    public static bool Use(PlayerMobile owner, SurveyMap map)
    {
        if (!ClassProfessions.Enabled || !PotionBelts.Ready(owner) || !ClassProfessions.Ready(owner) || !CarpentryCrafting.Component(owner, map)) { return false; }
        TimedEffects.Settle(owner, TimedEffects.Now);
        if (!owner.Alive) { return false; }
        var r = Find(map.RecipeId); string context = r.Effect == "dungeon" ? Dungeon(owner) : null;
        if (r.Effect == "dungeon" && context == null) { owner.SendMessage("Read this survey inside a supported named dungeon. The map was not consumed."); return false; }
        if (!TimedEffects.ApplyBuff(owner, TimedEffectCategory.Map, "survey-" + r.Effect, r.Magnitude, context)) { return false; }
        map.Consume(1); StatNetwork.Send(owner); owner.PlaySound(0x249);
        owner.SendMessage("Your map effect lasts 30 minutes and replaces your previous map effect."); return true;
    }
    public static string Description(SurveyRecipe r) => r.Effect switch
    { "city" => "Town-shop purchases cost 5% less; sales and fees unchanged", "world" => "Treasure-dig search tolerance +5 tiles", _ => $"+{r.Magnitude} Magic Find in this named dungeon; expires on exit" };
}

[SerializationGenerator(0)]
public partial class SurveyMap : Item
{
    [SerializableField(0, setter: "private")] private string _recipeId;
    [Constructible] public SurveyMap() : this("city") { }
    public SurveyMap(string id) : base(0x14EB) { _recipeId = id; Name = MapSurveys.Find(id).Name; Weight = 1; Stackable = true; }
    public override bool CanStackWith(Item dropped) => dropped is SurveyMap other && other.RecipeId == RecipeId && base.CanStackWith(dropped);
    public override void OnDoubleClick(Mobile from) { if (from is PlayerMobile p) { MapSurveys.Use(p, this); } }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list); var r = MapSurveys.Find(RecipeId);
        list.Add(1060658, $"{"Map effect"}\t{MapSurveys.Description(r)}");
        list.Add(1060659, $"{"Duration"}\t{"30 minutes; consume one map; one map effect, independent of food and potions"}");
    }
}
