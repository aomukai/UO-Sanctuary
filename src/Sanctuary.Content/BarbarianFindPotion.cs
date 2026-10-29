// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Approved Sanctuary yield policy:use the corpse's recorded monster level.
public sealed record PotionLevelBand(int MinLevel,int HealingTier,int ManaTier,int RejuvenationTier);
public static class PotionLevelBands
{
    private static PotionLevelBand[] _bands=[];
    public static void Configure()=>Set(BalanceCatalog.Read<PotionLevelBand>("find-potion-level-bands"));
    internal static PotionLevelBand[] Entries()=>(PotionLevelBand[])_bands.Clone();
    internal static void Set(PotionLevelBand[] definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);int previous=0;
        foreach(var row in definitions)
        {
            if(row==null||row.MinLevel<=previous||previous==0&&row.MinLevel!=1||row.HealingTier is <2 or >5
                ||row.ManaTier is <2 or >5||row.RejuvenationTier is <1 or >2)
            {throw new InvalidDataException("Invalid Find Potion monster-level bands");}
            previous=row.MinLevel;
        }
        if(definitions.Length==0){throw new InvalidDataException("Missing Find Potion monster-level bands");}
        _bands=(PotionLevelBand[])definitions.Clone();
    }
    internal static PotionLevelBand For(int level)
    {
        if(level<1||_bands.Length==0){throw new NotSupportedException("No recorded monster level or potion policy");}
        var selected=_bands[0];foreach(var band in _bands){if(level<band.MinLevel){break;}selected=band;}return selected;
    }
    internal static uint Code(PotionLevelBand band,uint draw,int manaChance,int rejuvenationChance)
    {
        draw%=100;
        return FindPotionPolicy.Code(draw<manaChance?"mp"+band.ManaTier:draw<manaChance+rejuvenationChance
            ?band.RejuvenationTier==1?"rvs":"rvl":"hp"+band.HealingTier);
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
        return CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)
            && CorpseSkillRules.TryMonsterLevel(corpse,out _)
            && CorpseSkillRules.Soft(corpse) && SpellHelper.CheckTown(owner.Location,owner)
            && SpellHelper.CheckTown(corpse.Location,owner);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
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
            || !CorpseSkillRules.TryMonsterLevel(World.FindItem(selected) as Corpse,out int level)) { return false; }
        var corpse = (Corpse)World.FindItem(selected);
        if (!CorpseSkillRules.Reserve(corpse)) { return false; }
        var values = SkillMechanics.Values(owner,CombatAbility.FindPotion);
        // Identified search-cry/flash placeholders, independent of potion output.
        owner.PlaySound(0x1E5); Effects.SendLocationEffect(corpse.Location,corpse.Map,0x375A,5,10,0x481,0);
        if(Roll()%100>=(int)values.Eval("calc1")){return true;}
        uint code=PotionLevelBands.Code(PotionLevelBands.For(level),Roll(),
            SkillMechanics.For(CombatAbility.FindPotion).Parameter(3),SkillMechanics.For(CombatAbility.FindPotion).Parameter(4));
        corpse.DropItem(CreatePotion(code));return true;
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
