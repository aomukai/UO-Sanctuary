// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
namespace Sanctuary.Integration;

public static class SavedItemProcPropertiesChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SavedItemProcPropertiesChecks));
    private sealed class NoRoll:Random
    {public override int Next(int minValue,int maxValue)=>throw new InvalidOperationException("Retained proc consumed item RNG");}
    private sealed record Case(string Code,object Skill,int? Chance,int? Rank,int Level,int ExpectedSkill,int ExpectedRank,int ExpectedChance);
    private sealed record Saved(uint Item,string Gear,NamedProperty Property,NamedPropertyRoll Roll,EquipmentStatValue[] Projected);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"saved-item-proc-properties-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"saved-item-proc-properties-reload-check":"saved-item-proc-properties-check";
            ConsoleInputHandler.RegisterCommand(name,"Retained property11 arguments,full source skill names,item-level projection and native originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static readonly Case[] Cases=[
        new("att-skill","Fire Ball",10,0,99,47,20,10),
        new("hit-skill",47,100,0,12,47,1,100),
        new("gethit-skill","47",0,-8,99,47,8,5),
        new("kill-skill","fire ball",-7,65,20,47,1,5),
        new("death-skill","HeraldThorns",5,7,20,428,7,5),
        new("levelup-skill",362,9,0,1,362,1,9),
        new("att-skill",47,10,0,1,47,1,10),
        new("hit-skill",47,17,63,99,47,63,17),
        new("att-skill",47,100,0,40,47,8,100),
        new("att-skill",47,10,null,99,47,20,10),
        new("gethit-skill",47,null,null,99,47,20,5)];
    private static EquipmentStatValue[] Project(Item item,(NamedProperty Property,NamedPropertyRoll Roll)[] pairs)
    {
        var values=new EquipmentStatValues(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});
        var graph=new ItemPropertyCreationContext(item,values,default,true,false,false,SourceRandom.InitLow(91),[]);
        Require(EquipmentRawProperties.ProjectRecorded(item,graph.Unit,values,default,true,false,false,pairs).Length==0,"Retained proc projection omitted fields");
        Require(graph.Draws==0&&graph.Seed==SourceRandom.InitLow(91),"Projection rerolled source item seed");
        return graph.Unit.Stats.Last?.BaseValues.ToArray()??[];
    }
    private static void Check(bool reload)
    {
        Require(!NamedGear.Available("unique/254"),"Retaining arguments admitted incomplete Hellslayer");
        var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[Cases.Length];
        for(int index=0;index<Cases.Length;index++)
        {
            var c=Cases[index];Item item;NamedProperty property;NamedPropertyRoll roll;
            if(!reload)
            {
                item=new Dagger{Visible=false,Movable=false};ClassGear.Profiles.Convert(item,c.Level,GearRarity.Normal,new Random(173));
                item.MoveToWorld(new(6080+index,3700,0),Map.Trammel);
                property=new("proc",c.Code,c.Code,true,JsonSerializer.SerializeToElement(c.Skill),c.Chance,c.Rank);
                roll=NamedPropertyRuntime.Roll("p/proc",property,new NoRoll());
            }
            else
            {
                var old=saved[index];item=World.FindItem((Serial)old.Item);property=old.Property;roll=old.Roll;
                Require(item is{Deleted:false,Visible:false,Movable:false,Parent:null}&&item.Map==Map.Trammel
                    &&item.X==6080+index&&item.Y==3700&&Json(ClassGear.State(item))==old.Gear,"Saved original or item level changed");
            }
            Require(!NamedPropertyRuntime.Supported(property)&&NamedPropertyRuntime.ProcArguments(property),"Storage silently admitted a proc runtime");
            Require(roll.Value==(c.Chance??0)&&roll.Maximum==(c.Rank??0)&&ItemSkillProperties.Parameter(property)==c.ExpectedSkill,"Raw chance/rank/source skill changed");
            NamedPropertyRuntime.Validate(property,roll);
            var projected=Project(item,[(property,roll)]);int stat=EquipmentPropertyWriter.Plan(c.Code).Steps[0].Stat;
            Require(projected.Length==1&&projected[0].Packed==EquipmentStatKey.Pack(stat,(c.ExpectedSkill<<6)|c.ExpectedRank)
                &&projected[0].Value==c.ExpectedChance,"Source item-level/default/negative/packed rank projection differs at "+index);
            var twice=Project(item,[(property,roll),(property,roll)]);
            Require(twice.Length==1&&twice[0].Packed==projected[0].Packed&&twice[0].Value==2*c.ExpectedChance,"Equal proc layers did not accumulate");
            bool invalid=false;try{NamedPropertyRuntime.Validate(property,roll with{Maximum=roll.Maximum+1});}catch(InvalidDataException){invalid=true;}
            Require(invalid,"Saved proc rank tampering accepted");
            invalid=false;try{NamedPropertyRuntime.Validate(property,roll with{ColdDurationFrames=1});}catch(InvalidDataException){invalid=true;}
            Require(invalid,"Unrelated saved proc field accepted");
            if(reload){Require(Json(projected)==Json(saved[index].Projected),"Projection changed after reload");}
            else{saved[index]=new(item.Serial.Value,Json(ClassGear.State(item)),property,roll,projected);}
        }
        var first=saved[0].Property;
        foreach(object bad in new object[]{-1,429,"", "not a source skill"})
        {bool refused=false;try{NamedPropertyRuntime.Roll("bad",first with{Parameter=JsonSerializer.SerializeToElement(bad)},new NoRoll());}catch(NotSupportedException){refused=true;}Require(refused,"Unresolved proc skill silently mapped");}
        var charged=first with{Code="charged"};Require(!NamedPropertyRuntime.ProcArguments(charged),"Charges confused with deterministic proc arguments");
        bool chargeRefused=false;try{NamedPropertyRuntime.Roll("charge",charged,new NoRoll());}catch(InvalidOperationException){chargeRefused=true;}Require(chargeRefused,"Unretained charge fill silently generated");
        if(!reload){File.WriteAllText(Manifest,Json(saved));}
    }
}
