// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed record PowerScrollTier(int Cap, int MinimumLevel);
public sealed record PowerScrollSettings(int SchemaVersion, int DropDenominator, int UniqueTickets,
    int SuperUniqueTickets, PowerScrollTier[] Tiers);

public sealed class PowerScrollRequest
{
    internal PlayerMobile Owner;
    internal PowerScroll Scroll;
    internal SkillName Skill;
    internal int Base, Cap, Target, Amount;
    internal SkillLock Lock;
    internal object Parent;
    internal ItemOrigin Origin;
    internal NetState Connection;
    internal bool Claimed;
}

public static class ProfessionPowerScrolls
{
    private static SkillName[] _skills = [];
    public static ReadOnlySpan<SkillName> Skills => _skills;
    public static PowerScrollSettings Settings { get; private set; }
    public static void Load()
    {
        var rows = BalanceCatalog.Read<PowerScrollSettings>("power-scrolls");
        if (rows.Length != 1 || rows[0] is not { SchemaVersion: 1, Tiers.Length: 4 } row
            || row.DropDenominator is < 1 or > 1000000 || row.UniqueTickets < 0 || row.UniqueTickets > row.DropDenominator
            || row.SuperUniqueTickets < 0 || row.SuperUniqueTickets > row.DropDenominator)
        { throw new InvalidDataException("Invalid Power Scroll probability settings."); }
        int previous = 0;
        for (int i = 0; i < row.Tiers.Length; i++)
        {
            var tier = row.Tiers[i];
            if (tier.Cap != 105 + i * 5 || tier.MinimumLevel <= previous || tier.MinimumLevel is < 1 or > 99)
            { throw new InvalidDataException("Invalid Power Scroll cap ladder."); }
            previous = tier.MinimumLevel;
        }
        Settings = row;
        _skills = new SkillName[TradeSkills.Count]; int index = 0;
        foreach (byte skill in TradeSkills.Crafting) { _skills[index++] = (SkillName)skill; }
        foreach (byte skill in TradeSkills.Gathering) { _skills[index++] = (SkillName)skill; }
        Array.Sort(_skills);
    }
    public static void Configure()
    {
        TradeSkillHooks.PowerScrollUsed = (from, scroll) =>
        {
            if (!ClassProfessions.Affected(from)) { return false; }
            if (from is PlayerMobile owner) { PowerScrollGump.DisplayTo(owner, scroll); }
            return true;
        };
    }
    internal static int TierCount(int level)
    {
        if (level is < 1 or > 99) { return 0; }
        int count = 0;
        foreach (var tier in Settings.Tiers) { if (level >= tier.MinimumLevel) { count++; } }
        return count;
    }
    internal static void Award(RewardLife life, Random random)
    {
        if (life.Creature is not EncounterCreature { Grade: EncounterGrade.Unique or EncounterGrade.SuperUnique } boss) { return; }
        int count = TierCount(life.Level);
        int tickets = boss.Grade == EncounterGrade.SuperUnique ? Settings.SuperUniqueTickets : Settings.UniqueTickets;
        if (count == 0 || tickets == 0 || random.Next(Settings.DropDenominator) >= tickets) { return; }
        int cap = Settings.Tiers[random.Next(count)].Cap;
        var skill = _skills[random.Next(_skills.Length)];
        var scroll = ItemOrigins.Create(ItemOrigin.Drop, () => new PowerScroll(skill, cap) { LootType = LootType.Regular });
        life.Corpse.DropItem(scroll);
    }
    internal static PowerScrollRequest Quote(PlayerMobile owner, PowerScroll scroll)
    {
        if (!ClassProfessions.Affected(owner) || !ClassProfessions.Ready(owner) || !RuneCrafting.Accessible(owner, scroll)) { return null; }
        if (!TradeSkills.Includes((int)scroll.Skill) || scroll.Value is not (105 or 110 or 115 or 120))
        { owner.SendMessage("This scroll does not name a retained trade skill and supported cap."); return null; }
        var skill = owner.Skills[scroll.Skill]; int target = (int)(scroll.Value * 10);
        if (skill.CapFixedPoint >= target)
        { owner.SendMessage($"Your {ClassProfessions.Name(scroll.Skill)} cap is already at least {scroll.Value:F1}. The scroll is unchanged."); return null; }
        if (skill.CapFixedPoint != target - 50)
        { owner.SendMessage($"First unlock the {scroll.Value - 5:F1} {ClassProfessions.Name(scroll.Skill)} cap. The scroll is unchanged."); return null; }
        return new() { Owner = owner, Scroll = scroll, Skill = scroll.Skill, Base = skill.BaseFixedPoint, Cap = skill.CapFixedPoint,
            Target = target, Lock = skill.Lock, Parent = scroll.Parent, Amount = scroll.Amount, Origin = scroll.Origin, Connection = owner.NetState };
    }
    private static bool Valid(PowerScrollRequest q) => q is { Claimed: false } && ClassProfessions.Affected(q.Owner)
        && ClassProfessions.Ready(q.Owner) && RuneCrafting.Accessible(q.Owner, q.Scroll)
        && q.Owner.NetState == q.Connection && q.Scroll.Parent == q.Parent && q.Scroll.Amount == q.Amount && q.Scroll.Origin == q.Origin
        && q.Scroll.Skill == q.Skill && q.Scroll.Value * 10 == q.Target && TradeSkills.Includes((int)q.Skill)
        && q.Owner.Skills[q.Skill].BaseFixedPoint == q.Base && q.Owner.Skills[q.Skill].CapFixedPoint == q.Cap
        && q.Owner.Skills[q.Skill].Lock == q.Lock && q.Target == q.Cap + 50;
    internal static bool Commit(PowerScrollRequest q)
    {
        if (!Valid(q)) { if (q != null) { q.Claimed = true; } return false; }
        q.Claimed = true;
        q.Owner.Skills[q.Skill].CapFixedPoint = q.Target; q.Scroll.Consume(1);
        q.Owner.SendMessage($"Your {ClassProfessions.Name(q.Skill)} cap is now {q.Target / 10.0:F1}. Your trained skill remains {q.Base / 10.0:F1}.");
        q.Owner.PlaySound(0x243);
        return true;
    }
}
