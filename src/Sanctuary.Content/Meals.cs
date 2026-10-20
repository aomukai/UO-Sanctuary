// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record MealIngredient(string Type, string Name, int Amount);
public sealed record MealRecipe(string Id, string Name, int Tier, string Effect, int Magnitude, double Skill, int Chips, int Graphic, MealIngredient[] Ingredients);
public static class Meals
{
    public static MealRecipe[] Recipes { get; private set; } = [];
    public static void Load()
    {
        Recipes = BalanceCatalog.Read<MealRecipe>("cooking-meals");
        if (Recipes.Length != 18) { throw new InvalidDataException("Eighteen meals are required."); }
        var ids = new System.Collections.Generic.HashSet<string>();
        var identities = new System.Collections.Generic.HashSet<(int, string)>();
        foreach (var r in Recipes)
        {
            int expected = r.Effect == "capacity" ? r.Tier switch { 1 => 25, 2 => 50, 3 => 74, _ => 0 } : r.Tier;
            if (!TimedEffects.Identifier(r.Id) || !ids.Add(r.Id) || !identities.Add((r.Tier, r.Effect)) || r.Name is not { Length: > 0 and <= 45 }
                || r.Effect is not ("light" or "capacity" or "resources" or "speed" or "carving" or "quiet") || r.Tier is < 1 or > 3
                || r.Magnitude != expected || r.Skill < (r.Tier == 1 ? 0 : r.Tier == 2 ? 50 : 100)
                || r.Skill >= (r.Tier == 1 ? 50 : r.Tier == 2 ? 100 : 120) || r.Chips != (r.Tier == 1 ? 5 : r.Tier == 2 ? 10 : 30)
                || r.Graphic is < 1 or > 0xFFFF || r.Ingredients is not { Length: >= 2 and <= 6 })
            { throw new InvalidDataException("Invalid cooking meal."); }
            var types = new System.Collections.Generic.HashSet<Type>();
            foreach (var input in r.Ingredients)
            {
                var t = EquipmentCrafting.Resolve(input.Type);
                if (!types.Add(t) || input.Amount is < 1 or > 10 || input.Name is not { Length: > 0 and <= 24 }
                    || !(typeof(Food).IsAssignableFrom(t) || typeof(CookableFood).IsAssignableFrom(t)
                        || typeof(BaseReagent).IsAssignableFrom(t) || t == typeof(SackFlour) || t == typeof(CookingSpices)))
                { throw new InvalidDataException("Invalid meal ingredient."); }
            }
        }
    }
    public static MealRecipe Find(string id) => Array.Find(Recipes, r => r.Id == id) ?? throw new ArgumentOutOfRangeException(nameof(id));
    public static string Description(MealRecipe r) => r.Effect switch
    {
        "light" => $"Personal light radius +{r.Magnitude} tiles",
        "capacity" => $"Carry capacity +{r.Magnitude} stones; normal encumbrance on expiry",
        "resources" => $"10% chance of +{r.Magnitude} primary logs, ore or fish per harvest",
        "speed" => $"Gathering {r.Magnitude} seconds faster; minimum 1 second",
        "carving" => $"+{r.Magnitude} of each eligible meat, skin and feather yield, once per corpse",
        _ => $"First monster acquisition delayed {r.Magnitude} seconds; attacking cancels the delay"
    };
}

[SerializationGenerator(0)]
public partial class PreparedMeal : Food
{
    [SerializableField(0, setter: "private")] private string _recipeId;
    [Constructible] public PreparedMeal() : this("lamplighter-broth") { }
    public PreparedMeal(string recipeId) : base(Meals.Find(recipeId).Graphic)
    { _recipeId = recipeId; Name = Meals.Find(recipeId).Name; Weight = 1; }
    public override bool CanStackWith(Item dropped) => dropped is PreparedMeal other && other.RecipeId == RecipeId && base.CanStackWith(dropped);
    public override bool Eat(Mobile from)
    {
        if (from is not PlayerMobile p || !PotionBelts.Ready(p) || !RuneCrafting.Accessible(p, this)) { return false; }
        TimedEffects.Settle(p, TimedEffects.Now);
        if (!p.Alive || !FoodEffects.Apply(p, Meals.Find(RecipeId))) { return false; }
        if (Poison != null) { p.ApplyPoison(Poisoner, Poison); }
        Consume(1); p.PlaySound(0x3A);
        p.SendMessage("Your meal's effect lasts one hour and replaces your previous food effect.");
        return true;
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list); var r = Meals.Find(RecipeId);
        list.Add(1060658, $"{"Meal effect"}\t{Meals.Description(r)}");
        list.Add(1060659, $"{"Duration"}\t{"One hour; one food effect, independent of potions; no hunger requirement"}");
    }
}
