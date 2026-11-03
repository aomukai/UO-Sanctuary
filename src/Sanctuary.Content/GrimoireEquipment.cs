// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
namespace Sanctuary;

// Binds a validated original capture; it does not roll a new item or enable
// named Grimoire factories, ordinary drops,staffmod selection,or crafting.
internal static class GrimoireEquipment
{
    internal static bool Ready(Item item)=>item is WarlockGrimoire{HasCapture:true};
    internal static void Register(Dictionary<string,GearItemType> types,Dictionary<string,NormalBase> bases,Dictionary<string,GearDefinition> gear)
    {
        types.Add("warl",new("warl",["clas"],false,true,"war"));
        types.Add("grim",new("grim",["shld","warl"],false,true,"war"));
        foreach(var row in GrimoirePrepared.AllBases)
        {bases.Add(row.Code,new(row.Code,row.Name,"grim",row.QualityLevel,0,row.RequiredLevel,row.Strength,0,0,0,0,0,0,0,
            row.MinimumDefense,row.MaximumDefense,0,row.Block,0,0,row.MaximumSockets,row.Tier));}
        gear.Add(nameof(WarlockGrimoire),new(nameof(WarlockGrimoire),"Shield","grim",GearWeight.None,"Normal",false,2,
            "Owned3.3.93847 Grimoire capture;stock book placeholder","Captured originals only;random and named acquisition are not enabled",GearSlot.Shield,"wa1",true));
    }
    internal static GearDefinition Definition(Item item)=>item is WarlockGrimoire{HasCapture:true} book
        &&EquipmentBalance.TryGet(item,out var definition)?definition with{BaseCode=book.Captured.Code}:null;
    internal static int RequiredLevel(Item item)
    {
        if(item is not WarlockGrimoire{HasCapture:true} book){return 0;}
        var capture=book.Captured;int result=GrimoirePrepared.Base(capture.Code).RequiredLevel;
        // The older normal-quality required-level path includes direct skill
        // requirements,not the automatic row's magic/rare requirement field.
        foreach(var grant in capture.Grants)
        {result=Math.Max(result,SkillMechanics.All.Single(s=>s.SourceId==grant.Skill).Number("reqlevel"));}
        return result;
    }
    internal static bool Identity(Type type,GearTier tier,string code)=>type==typeof(WarlockGrimoire)
        &&GrimoirePrepared.AllBases.Any(row=>row.Code==code&&row.Tier==tier.ToString());
    internal static void Validate(Item item,GearState state)
    {
        if(item is not WarlockGrimoire book){return;}
        if(state?.Roll==null||state.NamedProperties==null||state.MaterialRolls==null)
        {throw new InvalidDataException("Missing Grimoire equipment state");}
        if(!book.HasCapture){throw new InvalidDataException("Grimoire gear has no original capture");}
        var capture=book.Captured;GrimoirePrepared.Validate(capture);
        if(state.BaseCode!=capture.Code||state.Defense!=capture.Defense||state.Roll.ItemLevel!=capture.ItemLevel
            ||state.Roll.Rarity!=GearRarity.Normal||state.Roll.Affixes.Length!=0||state.NamedItemId!=null||state.NamedProperties.Length!=0
            ||state.CraftedEquipment||state.Quality!=CraftQuality.Average||state.DurabilityPercent!=0||state.MaterialRolls.Length!=0)
        {throw new InvalidDataException("Grimoire equipment changed its captured base,level,defense or unsupported generation profile");}
    }
    internal static void Bind(WarlockGrimoire book)
    {
        if(book.Deleted||!book.HasCapture){return;}
        var capture=book.Captured;GrimoirePrepared.Validate(capture);
        if(ClassGear.Profiles.Find(book.Serial) is{} existing){Validate(book,existing);return;}
        var basis=GrimoirePrepared.Base(capture.Code);var tier=Enum.Parse<GearTier>(basis.Tier);
        var state=new GearState(capture.Code,capture.Defense,false,new(GearRarity.Normal,capture.ItemLevel,
            AffixBalance.Level(capture.ItemLevel,basis.QualityLevel,0),Math.Max(RequiredLevel(book),EquipmentCrafting.EquipFloor(tier)),[])){Tier=tier};
        // Deterministic adoption of old prepared items; no capture or native
        // durability/resource rewrite and no random draws.
        ClassGear.Profiles.Store(book,state);
    }
    internal static IEnumerable<(NamedProperty Property,NamedPropertyRoll Roll)> Properties(Item item)
        =>item is WarlockGrimoire{HasCapture:true} book?book.CapturedProperties:[];
    internal static int Bonus(Item item,string code,int level)
    {int total=0;foreach(var (property,roll) in Properties(item)){total+=NamedPropertyRuntime.Bonus(property,roll,code,level);}return total;}
    internal static void AddProperties(Item item,IPropertyList list)
    {
        if(item is not WarlockGrimoire{HasCapture:true} book||ClassGear.State(item) is not{Identified:true}){return;}
        var automatic=GrimoirePrepared.Auto(book.Captured.Automatic);
        if(automatic.FireMaximum>0){list.Add($"Adds {automatic.FireMinimum}–{automatic.FireMaximum} Fire Damage");}
        foreach(var (property,roll) in Properties(item))
        {if(property.Code is not("fire-min" or "fire-max")){list.Add(NamedGear.PropertyText(property,roll,ClassGear.RecipientLevel(item)));}}
    }
}
