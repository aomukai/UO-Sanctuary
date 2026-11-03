// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace Sanctuary;

internal sealed record GrimoireBase(string Code,string Name,string Tier,string Normal,int QualityLevel,int RequiredLevel,
    int Strength,int MinimumDefense,int MaximumDefense,int Block,int Durability,int MaximumSockets);
internal sealed record GrimoireAutomatic(string Name,int AffixLevel,int RequiredLevel,bool Rare,int Frequency,
    int MagicMinimum,int MagicMaximum,int FireMinimum,int FireMaximum);
internal sealed record GrimoireGrant(int Skill,int Rank);
internal sealed record GrimoireSnapshot(string Code,int ItemLevel,int Defense,string Automatic,GrimoireGrant[] Grants)
{public string Profile{get;init;}="captured-normal-grimoire-v1";}

// Captured NORMAL-quality item inputs only. This does not select an automatic
// row,generate staffmods,roll defense,or authorize player acquisition. Recipient
// and native equip policy are supplied by the separate original-item binding.
internal static class GrimoirePrepared
{
    private static readonly Dictionary<string,GrimoireBase> Bases=new(StringComparer.Ordinal);
    private static readonly Dictionary<string,GrimoireAutomatic> Automatic=new(StringComparer.Ordinal);
    internal static IReadOnlyCollection<GrimoireBase> AllBases=>Bases.Values;
    internal static IReadOnlyCollection<GrimoireAutomatic> AllAutomatic=>Automatic.Values;
    internal static void Load()
    {
        Bases.Clear();Automatic.Clear();
        foreach(var row in BalanceCatalog.Read<GrimoireBase>("warlock-grimoire-bases"))
        {
            if(string.IsNullOrWhiteSpace(row.Code)||string.IsNullOrWhiteSpace(row.Name)||row.Tier is not("Normal" or "Exceptional" or "Elite")
                ||row.QualityLevel is<1 or>99||row.RequiredLevel is<0 or>99||row.Strength<0||row.MinimumDefense<0
                ||row.MaximumDefense<row.MinimumDefense||row.Block is<0 or>100||row.Durability!=20||row.MaximumSockets!=2||!Bases.TryAdd(row.Code,row))
            {throw new InvalidDataException("Invalid Grimoire base catalogue");}
        }
        if(Bases.Count!=15||Bases.Values.Count(r=>r.Tier=="Normal")!=5
            ||Bases.Values.Any(r=>!Bases.TryGetValue(r.Normal,out var normal)||normal.Tier!="Normal"))
        {throw new InvalidDataException("Incomplete Grimoire families");}
        foreach(var row in BalanceCatalog.Read<GrimoireAutomatic>("warlock-grimoire-automatic"))
        {
            if(string.IsNullOrWhiteSpace(row.Name)||row.AffixLevel is<1 or>99||row.RequiredLevel is<0 or>99||row.Frequency<=0
                ||row.MagicMinimum<0||row.MagicMaximum<row.MagicMinimum||row.FireMinimum<0||row.FireMaximum<row.FireMinimum
                ||(row.MagicMaximum>0)==(row.FireMaximum>0)||!Automatic.TryAdd(row.Name,row))
            {throw new InvalidDataException("Invalid Grimoire automatic-property catalogue");}
        }
        if(Automatic.Count!=8){throw new InvalidDataException("Incomplete Grimoire automatic properties");}
    }
    internal static GrimoireBase Base(string code)=>code!=null&&Bases.TryGetValue(code,out var row)?row
        :throw new InvalidDataException("Unknown Grimoire base");
    internal static GrimoireAutomatic Auto(string name)=>name!=null&&Automatic.TryGetValue(name,out var row)?row
        :throw new InvalidDataException("Unknown Grimoire automatic property");
    internal static void Validate(GrimoireSnapshot snapshot)
    {
        if(snapshot==null||snapshot.Profile!="captured-normal-grimoire-v1"||snapshot.ItemLevel is<1 or>99
            ||snapshot.Grants==null||snapshot.Grants.Length>3)
        {throw new InvalidDataException("Invalid captured Grimoire profile");}
        var basis=Base(snapshot.Code);var automatic=Auto(snapshot.Automatic);
        if(snapshot.Defense<basis.MinimumDefense||snapshot.Defense>basis.MaximumDefense
            ||AffixBalance.Level(snapshot.ItemLevel,basis.QualityLevel,0)<automatic.AffixLevel)
        {throw new InvalidDataException("Captured Grimoire defense/automatic level is outside source bounds");}
        var seen=new HashSet<int>();
        foreach(var grant in snapshot.Grants)
        {
            if(grant==null||grant.Rank is<1 or>3||!seen.Add(grant.Skill)
                ||!SkillMechanics.All.Any(row=>row.SourceId==grant.Skill&&row.CharacterClass==DiabloClass.Warlock))
            {throw new InvalidDataException("Invalid captured Warlock staffmod");}
        }
    }
    internal static GrimoireSnapshot Copy(GrimoireSnapshot snapshot)
    {Validate(snapshot);return snapshot with{Grants=(GrimoireGrant[])snapshot.Grants.Clone()};}
    internal static (NamedProperty Property,NamedPropertyRoll Roll)[] Properties(GrimoireSnapshot snapshot)
    {
        Validate(snapshot);var automatic=Auto(snapshot.Automatic);var result=new List<(NamedProperty,NamedPropertyRoll)>();
        void Add(string slot,string code,int min,int max,JsonElement? parameter=null)
            =>result.Add((new(slot,code,code,true,parameter,min,max),new(slot,min,max)));
        if(automatic.MagicMaximum>0){Add("grimoire/magic","dmg-mag",automatic.MagicMinimum,automatic.MagicMaximum);}
        else{Add("grimoire/fire-min","fire-min",automatic.FireMinimum,automatic.FireMinimum);Add("grimoire/fire-max","fire-max",automatic.FireMaximum,automatic.FireMaximum);}
        foreach(var grant in snapshot.Grants)
        {Add("grimoire/skill/"+grant.Skill,"skill",grant.Rank,grant.Rank,JsonSerializer.SerializeToElement(grant.Skill));}
        return result.ToArray();
    }
}
