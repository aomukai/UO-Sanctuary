// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
namespace Sanctuary.Integration;

public static class OriginalItemProcProjectionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(OriginalItemProcProjectionChecks));
    private sealed record Saved(uint Item,string Gear,NamedPropertyRoll[] Rolls,OriginalItemProcFields Fields);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"original-item-proc-projection-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"original-item-proc-projection-reload-check":"original-item-proc-projection-check";
            ConsoleInputHandler.RegisterCommand(name,"Complete retained source property projection and qualified event partition",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static OriginalItemProcFields Project(Item item,NamedItemDefinition definition,NamedPropertyRoll[] rolls)
    {
        var basis=ClassGear.Base(item);var b=new EquipmentPropertyBase(basis.MinDamage,basis.MaxDamage,basis.TwoHandMin,basis.TwoHandMax,basis.MissileMin,basis.MissileMax,basis.MinDefense,basis.MaxDefense);
        var initial=NpcItemStatCapture.Capture(item);var values=new EquipmentStatValues(_=>initial.Body,_=>null,_=>null,(_,_,_,_,_)=>{});
        var graph=new ItemPropertyCreationContext(item,values,b,true,false,false,SourceRandom.InitLow(13),initial.ItemBase);
        var properties=NamedPropertyRuntime.Templates(definition).Select(t=>(t.Property,rolls.Single(r=>r.Key==t.Key))).ToArray();
        Require(EquipmentRawProperties.ProjectRecorded(item,graph.Unit,values,b,true,false,false,properties).Length==0,"Complete source recipe acquired an omission");
        Require(graph.Draws==0&&graph.Seed==SourceRandom.InitLow(13),"Complete retained source properties rerolled");
        return OriginalItemProcProjection.FromCaptured(item,new(initial.Body,graph.Unit.Stats.BaseValues,graph.Unit.Stats.Last.BaseValues));
    }
    private static void Check(bool reload)
    {
        Require(NamedGear.Available("unique/254"),"Retained complete Hellslayer admission regressed");
        var definition=NamedGear.Catalog.Item("unique/254");Require(definition.Properties.Count(p=>p.SourceEnabled)==7,"Incomplete pinned Hellslayer recipe");
        int[] levels=[1,40,99];int[] ranks=[1,8,20];var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[3];
        for(int index=0;index<levels.Length;index++)
        {
            Item item;NamedPropertyRoll[] rolls;
            if(!reload)
            {
                item=new DoubleAxe{Visible=false,Movable=false};ClassGear.Profiles.Convert(item,levels[index],GearRarity.Normal,new Random(173));
                item.MoveToWorld(new(6070+index,3750,0),Map.Trammel);
                Require(!PlayerItemProcs.RegisterOriginal(item),"Plain original enrolled an invented event provider");
                rolls=NamedPropertyRuntime.Templates(definition).Select(t=>NamedPropertyRuntime.Roll(t.Key,t.Property,new Random(173))).ToArray();
            }
            else
            {
                item=World.FindItem((Serial)saved[index].Item);rolls=saved[index].Rolls;
                Require(item is{Deleted:false,Visible:false,Movable:false,Parent:null}&&item.Map==Map.Trammel&&item.X==6070+index&&item.Y==3750
                    &&Json(ClassGear.State(item))==saved[index].Gear,"Saved original/level/gear changed");
            }
            var fields=Project(item,definition,rolls);uint key=EquipmentStatKey.Pack(195,(47<<6)|ranks[index]);
            var expected=IronGolemBodyRecipes.Fields((7,6400),(17,100),(18,100),(48,150),(49,250),(219,24),(220,4),(223,4))
                .Append(new(key,10)).OrderBy(v=>v.Packed).ToArray();
            Require(fields.Item==item.Serial.Value&&fields.ItemLevel==levels[index]&&fields.Full.Properties.SequenceEqual(expected)
                &&fields.Events.ItemBase.Length==0&&fields.Events.Properties.SequenceEqual(new[]{new EquipmentStatValue(key,10)}),"Complete nine numeric/event fields,source rank or original identity changed");
            Require(OriginalItemProcProjection.Legacy(fields.Full.Properties).SequenceEqual(expected.Where(v=>v.Stat!=195)),"Legacy partition dropped or duplicated numeric fields");
            var stable=Json(fields);var source=fields.Full with{ItemBase=(EquipmentStatValue[])fields.Full.ItemBase.Clone(),Properties=(EquipmentStatValue[])fields.Full.Properties.Clone()};
            var isolated=OriginalItemProcProjection.FromCaptured(item,source);source.Properties[0]=new(0,0);source.ItemBase[0]=new(0,0);Require(Json(isolated)==stable,"Original full/event capture aliases caller arrays");
            bool refused=false;
            try{OriginalItemProcProjection.FromCaptured(item,fields.Full with{Properties=fields.Full.Properties.Append(new(EquipmentStatKey.Pack(197,(47<<6)|20),10)).ToArray()});}
            catch(NotSupportedException){refused=true;}Require(refused,"Unsupported death event silently discarded");
            refused=false;try{OriginalItemProcProjection.Legacy([new(EquipmentStatKey.Pack(195,(49<<6)|20),10)]);}catch(NotSupportedException){refused=true;}Require(refused,"Unsupported spell body escaped legacy preflight");
            PlayerItemProcs.Register(item,fields.Full.Body,fields.Events);
            Require(PlayerItemProcs.Provider(item.Serial.Value).State.Properties.SequenceEqual(fields.Events.Properties),"Projected event provider lost full-source-derived fields");
            if(!reload){saved[index]=new(item.Serial.Value,Json(ClassGear.State(item)),rolls,fields);}
            else{Require(Json(fields)==Json(saved[index].Fields),"Complete source capture or event partition changed after reload");}
        }
        if(!reload){File.WriteAllText(Manifest,Json(saved));}
        // These are explicit complete source-property component captures on
        // plain originals,not complete named GearStates or acquisition proof.
    }
}
