// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class FoodEffects
{
    private sealed class Encounter(long until) { internal long Until = until, OutsideSince; internal bool Outside, Passed; }
    private static readonly Dictionary<(Serial Creature, Serial Player), Encounter> _encounters = new();
    private static readonly List<(Serial Creature, Serial Player)> _keys = new();
    private static readonly Dictionary<Serial, long> _deadlines = new();
    private static readonly List<Serial> _expired = new();
    public static void Configure()
    {
        ClassFoodHooks.NoHunger = m => ClassProfessions.Enabled && m is PlayerMobile p && ClassCombat.Enabled(p);
        ClassFoodHooks.CarryCapacity = p => Bonus(p, "capacity");
        ClassFoodHooks.HarvestDuration = (m, d, duration) => m is PlayerMobile p && ClassProfessions.Enabled && ClassCombat.Enabled(p)
            && d.Skill is SkillName.Mining or SkillName.Lumberjacking or SkillName.Fishing
            ? TimeSpan.FromMilliseconds(Math.Max(1000, duration.TotalMilliseconds - Bonus(p, "speed") * 1000)) : duration;
        ClassFoodHooks.Harvested = (m, d, item) => HarvestExtra(m, d, item);
        ClassFoodHooks.CarveBonus = (m, corpse) => !corpse.Carved && m is PlayerMobile p ? Bonus(p, "carving") : 0;
        ClassFoodHooks.ExtraCarvable = c => ClassProfessions.Enabled && Exotic(c) != null;
        ClassFoodHooks.ExtraCarved = ExtraCarved;
        ClassFoodHooks.CanAcquire = (c, m) => CanAcquire(c, m, Core.TickCount);
        ClassFoodHooks.AcquireDeadline = (c, deadline) => _deadlines.TryGetValue(c.Serial, out long until)
            && until - Core.TickCount > 0 && until - deadline < 0 ? until : deadline;
    }
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), () => Observe(Core.TickCount));
    public static int Bonus(PlayerMobile p, string effect) => p != null && ClassProfessions.Enabled && ClassCombat.Enabled(p)
        && TimedEffects.Buff(p, TimedEffectCategory.Food) is { } buff && buff.Id == Id(effect) ? Math.Max(0, buff.Magnitude) : 0;
    private static string Id(string effect) => effect switch
    {
        "light" => "food-light", "capacity" => "food-capacity", "resources" => "food-resources",
        "speed" => "food-speed", "carving" => "food-carving", "quiet" => "food-quiet", _ => null
    };
    public static bool Apply(PlayerMobile p, MealRecipe meal, DateTime? at = null)
    {
        if (!ClassProfessions.Enabled || !TimedEffects.ApplyBuff(p, TimedEffectCategory.Food, Id(meal.Effect), meal.Magnitude, at: at)) { return false; }
        Refresh(p); return true;
    }
    internal static void Refresh(PlayerMobile p) { p.Delta(MobileDelta.Weight); StatNetwork.Send(p); }
    internal static void HarvestExtra(Mobile m, HarvestDefinition definition, Item item, Random random = null)
    {
        if (m is not PlayerMobile p || item is not { Stackable: true, Deleted: false } || Bonus(p, "resources") is not > 0) { return; }
        bool primary = definition.Skill == SkillName.Mining && item is BaseOre
            || definition.Skill == SkillName.Lumberjacking && item is Log || definition.Skill == SkillName.Fishing && item is Fish;
        if (primary && (random?.NextDouble() ?? Utility.RandomDouble()) < .1) { item.Amount += Bonus(p, "resources"); }
    }
    private static Type Exotic(BaseCreature c) => c is VampireBat or Mongbat or GreaterMongbat or StrongMongbat ? typeof(BatWing)
        : c is Daemon or Balron ? typeof(DaemonBlood) : null;
    internal static void ExtraCarved(Mobile from, BaseCreature c, Corpse corpse)
    {
        if (!ClassProfessions.Enabled || from is not PlayerMobile p || !ClassCombat.Enabled(p) || corpse.Carved || corpse.Animated || corpse.Owner != c
            || c.Controlled || c.Summoned || c.IsBonded || !corpse.CanLoot(from, null) || corpse.IsCriminalAction(from) || Exotic(c) is not { } type) { return; }
        var extra = ItemOrigins.Create(ItemOrigin.Drop, () => type.CreateInstance<Item>());
        corpse.DropItem(extra); p.SendMessage("You recover an unusual cooking ingredient from the corpse.");
    }
    private static bool Hostile(BaseCreature c, Mobile m)
    {
        if (c.Combatant == m || m.Combatant == c) { return true; }
        foreach (var a in c.Aggressors) { if (a.Attacker == m) { return true; } }
        foreach (var a in c.Aggressed) { if (a.Defender == m) { return true; } }
        return false;
    }
    internal static bool CanAcquire(BaseCreature c, Mobile m, long now)
    {
        if (!ClassProfessions.Enabled || m is not PlayerMobile p || c.Controlled || c.Summoned || c is BaseVendor || !ClassCombat.Enabled(p)) { return true; }
        var key = (c.Serial, p.Serial);
        if (Hostile(c, p)) { if (_encounters.TryGetValue(key, out var engaged)) { engaged.Passed = true; } return true; }
        int seconds = Bonus(p, "quiet");
        if (seconds == 0) { if (_encounters.TryGetValue(key, out var unprotected)) { unprotected.Passed = true; } return true; }
        if (!_encounters.TryGetValue(key, out var encounter)) { _encounters[key] = encounter = new(now + seconds * 1000L); }
        if (encounter.Outside && now - encounter.OutsideSince >= 10000)
        { _encounters[key] = encounter = new(now + seconds * 1000L); }
        encounter.Outside = false;
        if (encounter.Passed || now - encounter.Until >= 0) { encounter.Passed = true; return true; }
        if (!_deadlines.TryGetValue(c.Serial, out long previous) || previous - now <= 0 || encounter.Until - previous < 0)
        { _deadlines[c.Serial] = encounter.Until; }
        return false;
    }
    internal static void Observe(long now)
    {
        _expired.Clear();
        foreach (var (serial, until) in _deadlines)
        { if (now - until >= 0 || World.FindMobile(serial) is not { Deleted: false, Alive: true }) { _expired.Add(serial); } }
        foreach (var serial in _expired) { _deadlines.Remove(serial); }
        _keys.Clear(); foreach (var key in _encounters.Keys) { _keys.Add(key); }
        foreach (var key in _keys)
        {
            if (World.FindMobile(key.Creature) is not BaseCreature { Deleted: false, Alive: true } c
                || World.FindMobile(key.Player) is not PlayerMobile { Deleted: false, Alive: true } p)
            { _encounters.Remove(key); _deadlines.Remove(key.Creature); continue; }
            var e = _encounters[key];
            bool visible = p.Map == c.Map && c.InRange(p, c.RangePerception) && c.CanSee(p) && c.InLOS(p);
            if (visible)
            {
                if (e.Outside && now - e.OutsideSince >= 10000) { _encounters.Remove(key); }
                else { e.Outside = false; }
            }
            else if (!e.Outside) { e.Outside = true; e.OutsideSince = now; }
            else if (now - e.OutsideSince >= 10000) { _encounters.Remove(key); }
        }
    }
}
