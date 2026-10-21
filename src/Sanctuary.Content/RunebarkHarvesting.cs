// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Engines.Harvest;
using Server.Items;

namespace Sanctuary;

public sealed record RunebarkTier(int Tier, string Name, double MinimumSkill, int SellPrice);

public static class RunebarkHarvesting
{
    public static RunebarkTier[] Tiers { get; private set; } = [];
    public static void Load()
    {
        Tiers = BalanceCatalog.Read<RunebarkTier>("runebark");
        if (Tiers.Length != 8) { throw new InvalidDataException("Eight Runebark tiers are required."); }
        for (int i = 0; i < Tiers.Length; i++)
        {
            var row = Tiers[i];
            if (row.Tier != i + 1 || row.Name != MouldRewards.Tier(i + 1).Name
                || row.MinimumSkill != 45 + i * 5 || row.SellPrice is < 1 or > 60000)
            { throw new InvalidDataException("Invalid Runebark tier."); }
        }
    }
    public static void Configure() => ClassHarvestHooks.Successful = (from, definition, trainedSkill, resourceType) =>
        Successful(from, definition, trainedSkill, resourceType);
    internal static void Successful(Mobile from, HarvestDefinition definition, double trainedSkill, Type resourceType, Random random = null)
    {
        if (!ClassProfessions.Enabled) { return; }
        ProfessionProgression.HarvestGain(from, definition, trainedSkill, resourceType);
        if (definition.Skill == SkillName.Lumberjacking && typeof(Log).IsAssignableFrom(resourceType)) { Award(from, trainedSkill, random); }
        else if (definition.Skill == SkillName.Mining && typeof(BaseOre).IsAssignableFrom(resourceType)) { SocketGems.Award(from, trainedSkill, random); }
    }
    public static int Unlocked(double trainedSkill)
    {
        int count = 0;
        foreach (var tier in Tiers) { if (trainedSkill >= tier.MinimumSkill) { count++; } }
        return count;
    }
    public static Runebark Roll(double trainedSkill, Random random = null)
    {
        int count = Unlocked(trainedSkill);
        if (count == 0) { return null; }
        random ??= Random.Shared;
        if (random.Next(100) >= 5) { return null; }
        return ItemOrigins.Create(ItemOrigin.Drop, () => new Runebark(random.Next(count) + 1));
    }
    public static void Award(Mobile from, double trainedSkill, Random random = null)
    {
        if (from is not { Deleted: false, Alive: true } || from.Map == null || from.Map == Map.Internal) { return; }
        var bark = Roll(trainedSkill, random);
        if (bark == null) { return; }
        string name = bark.Name;
        if (from.PlaceInBackpack(bark)) { from.SendMessage($"You find {name} and place it in your backpack."); }
        else
        {
            bark.MoveToWorld(from.Location, from.Map);
            from.SendMessage($"You find {name}. Your backpack is full; the Runebark is at your feet.");
        }
    }
}
