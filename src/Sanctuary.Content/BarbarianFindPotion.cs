// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Acts are explicit area designations, not inferred from monster level or loot.
// The closest named ancestor wins; an untagged area has no fallback Act.
public sealed record RegionAct(int Map, string Region, int Act);
public readonly record struct PotionArea(int Act, MonsterDifficulty Difficulty);
public static class PotionAreas
{
    private static Dictionary<(int,string),int> _acts = new();
    public static void Configure() => Set(BalanceCatalog.Read<RegionAct>("region-acts"));
    internal static RegionAct[] Entries()
    {
        var rows = new List<RegionAct>();
        foreach (var (key,act) in _acts) { rows.Add(new(key.Item1,key.Item2,act)); }
        return rows.ToArray();
    }
    internal static void Set(RegionAct[] definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var acts = new Dictionary<(int,string),int>();
        foreach (var row in definitions)
        {
            if (row == null || row.Map < 0 || row.Map >= Map.Maps.Length || Map.Maps[row.Map] == null
                || Map.Maps[row.Map] == Map.Internal || string.IsNullOrWhiteSpace(row.Region)
                || row.Region != row.Region.Trim() || row.Act is < 1 or > 5
                || !acts.TryAdd((row.Map,row.Region),row.Act))
            { throw new InvalidDataException("Invalid or duplicate region Act designation"); }
        }
        _acts = acts;
    }
    public static bool TryGet(PlayerMobile owner, out PotionArea area)
    {
        area = default;
        if (owner is not { Deleted:false, Map:not null } || owner.Map == Map.Internal) { return false; }
        for (var region = Region.Find(owner.Location,owner.Map); region != null; region = region.Parent)
        {
            if (region.Name != null && _acts.TryGetValue((owner.Map.MapID,region.Name),out int act))
            { area = new(act,SummonDifficulties.For(owner.Map)); return true; }
        }
        return false;
    }
}

// Complete legacy do69 policy. Current tables corroborate skill formula and
// 30/10 type weights; this is not a certificate of current executable parity.
internal static class FindPotionPolicy
{
    internal static bool Eligible(int type,int mode,uint flags,bool undead,bool noSelect,bool rowPresent)
        => type == 1 && mode == 12 && rowPresent && (flags & 4224) == 4224 && !undead && !noSelect;
    internal static uint Code(string code) => (uint)code[0] | (uint)code[1] << 8 | (uint)code[2] << 16 | 32u << 24;
    private static readonly string[] Health = ["hp2","hp3","hp3","hp4","hp4","hp4","hp4","hp5","hp5","hp5","hp5","hp5","hp5","hp5","hp5"];
    private static readonly string[] Mana = ["mp2","mp3","mp3","mp4","mp4","mp4","mp5","mp5","mp5","mp5","mp5","mp5","mp5","mp5","mp5"];
    internal static int Execute(bool skillPresent,bool eligible,Action reserveAndRefresh,Func<int> chance,
        Func<uint> roll,bool room,int act,int difficulty,int manaChance,int rejuvenationChance,Action<uint,int> drop)
    {
        if (!skillPresent || !eligible) { return 0; }
        // The source game enum cannot be negative. Reject unsafe fixture input
        // before reserving, rather than indexing before the native table.
        if (difficulty < 0) { throw new ArgumentOutOfRangeException(nameof(difficulty)); }
        reserveAndRefresh();
        if (roll() % 100 >= chance()) { return 1; }
        long index = act + 5L * difficulty;
        if (!room || act is < 0 or >= 5 || index >= 15) { drop(0,2); return 1; }
        uint draw = roll() % 100;
        string code = draw < manaChance ? Mana[index] : draw < manaChance + rejuvenationChance
            ? index < 3 ? "rvs" : "rvl" : Health[index];
        drop(Code(code),2); return 1;
    }
}

public static class BarbarianFindPotion
{
    internal static Func<uint> Roll = () => (uint)Utility.Random(100);
    internal static bool ValidTarget(PlayerMobile owner,Serial selected)
    {
        var corpse = World.FindItem(selected) as Corpse;
        return PotionAreas.TryGet(owner,out _) && CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)
            && CorpseSkillRules.Soft(corpse) && SpellHelper.CheckTown(owner.Location,owner)
            && SpellHelper.CheckTown(corpse.Location,owner);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if (!PotionAreas.TryGet(owner,out _))
        { owner.SendMessage("Find Potion requires an Act designation for this region."); return false; }
        if (!ValidTarget(owner,selected) || !SkillCombat.Begin(owner,CombatAbility.FindPotion,selected,
            ((Corpse)World.FindItem(selected)).Location,now,(p,_,id,_) => ValidTarget(p,id))) { return false; }
        if (NecromancerEffects.Pay(owner,SkillMechanics.Values(owner,CombatAbility.FindPotion).Mana256))
        { ClassCombat.For(owner).FindPotionTarget = selected; return true; }
        ClassCombat.Stop(owner); return false;
    }
    public static bool Release(PlayerMobile owner,Serial selected)
    {
        var session = ClassCombat.For(owner);
        bool paid = selected != Serial.Zero && session.FindPotionTarget == selected;
        session.FindPotionTarget = Serial.Zero;
        if (!paid || !BarbarianCombat.Available(owner,CombatAbility.FindPotion) || !ValidTarget(owner,selected)
            || !PotionAreas.TryGet(owner,out var area)) { return false; }
        var corpse = (Corpse)World.FindItem(selected);
        if (!CorpseSkillRules.Reserve(corpse)) { return false; }
        var values = SkillMechanics.Values(owner,CombatAbility.FindPotion);
        // Identified search-cry/flash placeholders, independent of potion output.
        owner.PlaySound(0x1E5); Effects.SendLocationEffect(corpse.Location,corpse.Map,0x375A,5,10,0x481,0);
        return FindPotionPolicy.Execute(true,true,() => { },() => (int)values.Eval("calc1"),Roll,
            true,area.Act-1,(int)area.Difficulty,SkillMechanics.For(CombatAbility.FindPotion).Parameter(3),SkillMechanics.For(CombatAbility.FindPotion).Parameter(4),
            (code,_) => corpse.DropItem(CreatePotion(code))) != 0;
    }
    internal static RestorationPotion CreatePotion(uint code)
    {
        string leaf = new([(char)(code & 255),(char)((code >> 8) & 255),(char)((code >> 16) & 255)]);
        RestorationKind kind; int tier;
        if (leaf is "rvs" or "rvl") { kind = RestorationKind.Rejuvenation; tier = leaf == "rvs" ? 1 : 2; }
        else if (leaf.Length == 3 && leaf[1] == 'p' && leaf[0] is 'h' or 'm' && leaf[2] is >= '2' and <= '5')
        { kind = leaf[0] == 'h' ? RestorationKind.Healing : RestorationKind.Mana; tier = leaf[2]-'0'; }
        else { throw new NotSupportedException("Find Potion generic item-code fallback is not a potion"); }
        return ItemOrigins.Create(ItemOrigin.Drop,() => new RestorationPotion(kind,tier));
    }
}
