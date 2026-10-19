// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record PracticeRecipe(int Skill, string Resource, int[] Amounts);

public static class ProfessionPractice
{
    private static readonly Dictionary<SkillName, PracticeRecipe> _recipes = new();
    private static readonly Dictionary<string, Type> _resources = new(StringComparer.Ordinal)
    {
        ["Ginseng"] = typeof(Ginseng), ["IronIngot"] = typeof(IronIngot), ["Log"] = typeof(Log),
        ["Board"] = typeof(Board), ["BlankScroll"] = typeof(BlankScroll), ["RawFishSteak"] = typeof(RawFishSteak),
        ["MagicEssence"] = typeof(MagicEssence), ["Cloth"] = typeof(Cloth)
    };
    public static void Load()
    {
        _recipes.Clear();
        foreach (var recipe in BalanceCatalog.Read<PracticeRecipe>("profession-practice"))
        {
            if (!TradeSkills.IsCraft(recipe.Skill) || !_resources.ContainsKey(recipe.Resource) || recipe.Amounts?.Length != 4)
            {
                throw new InvalidDataException("Invalid profession practice recipe.");
            }
            foreach (int amount in recipe.Amounts)
            {
                if (amount is < 1 or > 100) { throw new InvalidDataException("Invalid practice quantity."); }
            }
            _recipes.Add((SkillName)recipe.Skill, recipe);
        }
        if (_recipes.Count != TradeSkills.Crafting.Length) { throw new InvalidDataException("Practice must cover all ten crafts."); }
    }
    public static PracticeRecipe Recipe(SkillName skill) => _recipes.GetValueOrDefault(skill);
    public static string ResourceName(string resource) => resource switch
    {
        "IronIngot" => "Iron Ingots", "Log" => "Logs", "Board" => "Boards", "BlankScroll" => "Blank Scrolls",
        "RawFishSteak" => "Raw Fish Steaks", "MagicEssence" => "Magic Essence", _ => resource
    };
    public static int Band(int baseFixedPoint) => baseFixedPoint is >= 300 and < 700 ? baseFixedPoint / 100 - 3 : -1;
    public static int Chance(int baseFixedPoint) => Band(baseFixedPoint) < 0 ? 0 : 100 - baseFixedPoint % 100;
    internal static bool Attempt(PlayerMobile player, BaseTool tool, int expectedBand, out bool gained, Random random = null)
    {
        gained = false;
        if (!ClassProfessions.Affected(player) || !ClassProfessions.Ready(player) || tool is not { Deleted: false, UsesRemaining: > 0 }
            || !(tool.IsChildOf(player.Backpack) || tool.Parent == player) || !player.CanBeginAction<CraftSystem>() || player.NextSkillTime - Core.TickCount > 0)
        {
            return false;
        }
        var skillName = tool.CraftSystem.MainSkill;
        var skill = player.Skills[skillName];
        int band = Band(skill.BaseFixedPoint);
        var recipe = Recipe(skillName);
        if (band < 0 || band != expectedBand || recipe == null || skill.BaseFixedPoint >= skill.CapFixedPoint
            || skill.Lock != SkillLock.Up || !ClassProfessions.Request(player, skillName, tool))
        {
            return false;
        }
        if (!CraftFacilities.Allows(player, skillName)) { return false; }
        int badCraft = tool.CraftSystem.CanCraft(player, tool, null);
        if (badCraft != 0) { player.SendLocalizedMessage(badCraft); return false; }
        int needed = recipe.Amounts[band];
        var inputs = new List<Item>();
        // Exact types keep practice from consuming a player's colored materials as plain inputs.
        foreach (var item in player.Backpack.FindItemsByType<Item>(true))
        {
            if (item.GetType() != _resources[recipe.Resource] || !item.Movable) { continue; }
            inputs.Add(item);
            needed -= item.Amount;
            if (needed <= 0) { break; }
        }
        if (needed > 0) { return false; }
        int chance = Chance(skill.BaseFixedPoint);
        needed = recipe.Amounts[band];
        foreach (var input in inputs)
        {
            int take = Math.Min(needed, input.Amount);
            input.Consume(take);
            needed -= take;
            if (needed == 0) { break; }
        }
        CraftTransactions.SpendUse(player, tool, random);
        if (tool.UsesRemaining == 0 && tool.BreakOnDepletion) { tool.Delete(); }
        gained = (random ?? Random.Shared).Next(100) < chance;
        if (gained) { skill.BaseFixedPoint++; }
        player.NextSkillTime = Core.TickCount + 1000;
        player.SendMessage(gained ? "Practice consumed its ingredients. You gained 0.1 skill." : "Practice consumed its ingredients. No skill gained.");
        return true;
    }
}
