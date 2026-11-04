// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
namespace Sanctuary.Integration;

public static class FrozenEquipmentCombatChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FrozenEquipmentCombatChecks));
    private sealed record Saved(string Name,FrozenEquipmentSnapshot Snapshot,FrozenCombatBase Basis,FrozenCombatLevel[] Levels,EquipmentStatValue[] Numeric,string NumericIssue);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"frozen-equipment-combat-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("frozen-equipment-combat-check",Check);Register("frozen-equipment-combat-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Frozen independent NPC combat suppliers",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static FrozenCombatLevel Live(Item item,int level)
    {
        var c=new ItemStatContext(item,level);FrozenResourceInput R(string code,int stat)
        {var v=c.ResourceInput(code,stat);return new(v.Flat256,v.LevelCoefficientRaw);}
        var one=ClassGear.Damage(item,level,false,true);var two=ClassGear.Damage(item,level,true,true);
        return new(level,c.Elements(),c.Poison(),R("hp",216),R("mana",217),R("stam",242),one.Min,one.Max,two.Min,two.Max);
    }
    private static void Assert(Saved s)
    {
        var c=FrozenEquipmentEffects.Bind(s.Snapshot);Require(c.CombatBase(1)==s.Basis,"Frozen base/type/coefficients changed: "+s.Name);
        for(int level=1;level<=99;level++)
        {
            var expected=s.Levels[level-1];Require(c.CombatAt(level)==expected&&c.Elements(level)==expected.Elements&&c.Poison(level)==expected.Poison,"Frozen typed/physical supplier changed: "+s.Name+"/"+level);
            Require(c.ResourceInput("hp",216,level)==expected.Life&&c.ResourceInput("mana",217,level)==expected.Mana
                &&c.ResourceInput("stam",242,level)==expected.Stamina,"Frozen fractional resource supplier changed");
        }
        if(s.NumericIssue==null)
        {
            var raw=c.NumericProperties(1);Require(raw.SequenceEqual(s.Numeric),"Frozen raw numeric properties changed");
            if(raw.Length>0){raw[0]=raw[0]with{Value=raw[0].Value+1};Require(c.NumericProperties(1).SequenceEqual(s.Numeric),"Returned numeric array mutated frozen context");}
        }
        else
        {
            bool refused=false;try{_=c.NumericProperties(1);}catch(NotSupportedException e){refused=e.Message==s.NumericIssue;}
            Require(refused,"Unsupported raw numeric projection silently supplied zeros");
        }
        foreach(int level in new[]{0,100})
        {bool refused=false;try{_=c.CombatAt(level);}catch(ArgumentOutOfRangeException){refused=true;}Require(refused,"Frozen combat silently clamped level");}
    }
    private static void Compatibility(Saved s)
    {
        var legacy=s.Snapshot with{CombatJson=null};FrozenEquipmentEffects.Validate(legacy);var old=FrozenEquipmentEffects.Bind(legacy);var current=FrozenEquipmentEffects.Bind(s.Snapshot);
        Require(old.At(37)==current.At(37)&&old.Bonus("hp",37)==current.Bonus("hp",37),"Optional combat field changed historical basic suppliers");
        bool refused=false;try{_=old.CombatAt(37);}catch(NotSupportedException){refused=true;}Require(refused,"Historical snapshot invented combat suppliers");
        var unavailable=s.Snapshot with{CombatJson=JsonSerializer.Serialize(new FrozenCombatSnapshot(0,null,[],"Explicit unsupported fixture projection"))};
        FrozenEquipmentEffects.Validate(unavailable);refused=false;try{_=FrozenEquipmentEffects.Bind(unavailable).Elements(37);}catch(NotSupportedException e){refused=e.Message.Contains("Explicit unsupported");}
        Require(refused&&FrozenEquipmentEffects.Bind(unavailable).At(37)==current.At(37),"Unsupported projection lost basic snapshot or silently supplied zeros");
        var data=FrozenEquipmentEffects.Decode<FrozenCombatSnapshot>(s.Snapshot.CombatJson);var tail=(FrozenCombatLevel[])data.Levels.Clone();tail[^1]=tail[^1]with{Level=98};
        foreach(var bad in new[]{data with{Version=1},data with{Levels=[]},data with{Levels=tail},data with{Issue="unsupported"},
            data with{Levels=[..data.Levels.Take(98),data.Levels[^1]with{Elements=data.Levels[^1].Elements with{ColdFrames=-1}}]},
            data with{Numeric=[new(EquipmentStatKey.Pack(ushort.MaxValue),1)],NumericIssue=null},
            data with{Numeric=[new(EquipmentStatKey.Pack(ushort.MaxValue,42),0)],NumericIssue=null}})
        {
            refused=false;try{FrozenEquipmentEffects.Validate(s.Snapshot with{CombatJson=JsonSerializer.Serialize(bad)});}catch(InvalidDataException){refused=true;}
            Require(refused,"Malformed frozen combat tail/version/issue/numeric ID accepted");
        }
        Log.Information("Frozen numeric admission: unknown stat IDs refused at zero/nonzero layers and values; supported snapshots retained");
    }
    private static void Check()
    {
        string[] names=["Crown of Thieves","Harlequin Crest","Frostburn","Baranar's Star","Blacktongue","Ripsaw"];
        var results=names.Select(name=>
        {
            var item=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
            try
            {
                var levels=Enumerable.Range(1,99).Select(level=>Live(item,level)).ToArray();var live=new ItemStatContext(item,1);live.RequireOrdinaryNpcDrainInputs();var b=ClassGear.Base(item);
                var basis=new FrozenCombatBase(ClassGear.Slot(item),b.Type,ClassGear.Slot(item)==GearSlot.Weapon&&!ClassGear.Broken(item),b.TwoHanded,b.OneOrTwoHanded,b.StrengthBonus,b.DexterityBonus,
                    ClassGear.Slot(item)==GearSlot.Weapon&&!ClassGear.Broken(item)?b.Speed:0);
                EquipmentStatValue[] numeric=[];string issue=null;
                try{numeric=live.ActiveNumericProperties();}catch(NotSupportedException e){issue="Frozen NPC numeric projection unavailable: "+e.Message;}
                var snapshot=FrozenEquipmentEffects.Capture(item);var result=new Saved(name,snapshot,basis,levels,numeric,issue);Assert(result);
                var state=ClassGear.State(item);ClassGear.Profiles.Store(item,state with{Identified=false});item.Delete();Require(item.Deleted,"Original loot deletion failed");Assert(result);return result;
            }
            finally{if(!item.Deleted){item.Delete();}}
        }).ToArray();
        Require(results.Any(s=>s.Levels.Any(l=>l.Elements.FireMax>0))&&results.Any(s=>s.Levels.Any(l=>l.Poison.PoisonMax>0))
            &&results.Any(s=>s.Levels.Any(l=>l.OneMaximum!=l.TwoMaximum))&&results.Any(s=>s.Levels.Any(l=>l.Life.LevelCoefficientRaw!=0)),"Typed,poison,hybrid or fractional controls were vacuous");
        Compatibility(results[0]);File.WriteAllText(Manifest,JsonSerializer.Serialize(results));
        var unsupported=new Item(1);
        try
        {
            var saved=FrozenEquipmentEffects.Capture(unsupported);
            Require(!ClassGear.Converted(unsupported),"Unsupported native fixture unexpectedly has a source base");
            Require(saved.UnavailableReason!=null&&saved.CombatJson==null,"Unconverted native equipment invented combat suppliers");
            bool refused=false;try{_=FrozenEquipmentEffects.Bind(saved).CombatAt(37);}catch(NotSupportedException){refused=true;}Require(refused,"Unsupported source supplied an empty working context");
        }
        finally{unsupported.Delete();}
        var hellplague=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Hellplague").Id,99,new Random(173),true);
        try
        {
            var captured=FrozenEquipmentEffects.Capture(hellplague);var data=FrozenEquipmentEffects.Decode<FrozenCombatSnapshot>(captured.CombatJson);
            Require(data.NumericIssue?.Contains("fireskill")==true&&data.Issue==null&&FrozenEquipmentEffects.Properties(captured).Any(p=>p.Property.Code=="fireskill"),"Unsupported fire-skill property was lost or silently projected");
            var frozen=FrozenEquipmentEffects.Bind(captured);Require(frozen.Elements(37)==new ItemStatContext(hellplague,37).Elements(),"Unsupported raw projection blocked independent supported element supplier");
            bool refused=false;try{_=frozen.NumericProperties(37);}catch(NotSupportedException){refused=true;}Require(refused,"Unmapped fire-skill raw projection became a working numeric list");
        }
        finally{hellplague.Delete();}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));Require(saved.Length==6,"Missing saved frozen combat items");
        // Native serial allocation may reuse a deleted item's number. Frozen
        // reads must ignore that current object and must not create any item.
        int count=World.Items.Count;foreach(var row in saved){Assert(row);}
        Require(World.Items.Count==count,"Frozen access recreated loot");
        Compatibility(saved[0]);
    }
}
