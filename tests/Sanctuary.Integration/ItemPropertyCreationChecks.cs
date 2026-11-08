// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Fixture=Sanctuary.Integration.EquipmentPropertyWriterChecks.Fixture;

namespace Sanctuary.Integration;
public static class ItemPropertyCreationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ItemPropertyCreationChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-property-creation-fixtures.json");
    private sealed record Saved(int Case,uint Item,int X,int Y,string NativeGear,ItemPropertyCreationState State);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-property-creation-check",false);Register("item-property-creation-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Fresh item-owned numeric/layered property graph;no full NPC item factory",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static EquipmentPropertyBase[] Bases(JsonElement root)=>root.GetProperty("bases").EnumerateArray()
        .Select(r=>r.GetProperty("Values").EnumerateArray().Select(v=>v.GetInt32()).ToArray())
        .Select(v=>new EquipmentPropertyBase(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7])).ToArray();
    private static bool Same(Dictionary<uint,int> values,JsonElement expected)=>values.OrderBy(p=>p.Key).Select(p=>(p.Key,p.Value))
        .SequenceEqual(expected.EnumerateArray().Select(v=>(v[0].GetUInt32(),v[1].GetInt32())));
    private static void Kernel(JsonElement root,EquipmentPropertyBase[] bases)
    {
        var plans=root.GetProperty("plans").EnumerateArray().ToDictionary(r=>r.GetProperty("Ordinal").GetInt32(),r=>EquipmentPropertyWriter.Plan(r.GetProperty("Code").GetString()));
        Require(plans.Count==284&&plans.Values.Count(EquipmentPropertyWriter.CanCreate)==202&&plans.Values.Count(ItemPropertyCreationContext.Admitted)==201
            &&plans.Values.Count(p=>p.Supported)==191,"Fresh/source/original admission scopes changed");
        foreach(var r in root.GetProperty("plans").EnumerateArray())
        {Require(EquipmentPropertyWriter.CanCreate(plans[r.GetProperty("Ordinal").GetInt32()])==r.GetProperty("Creatable").GetBoolean(),"Creation source plan admission");}
        var edge=root.GetProperty("synthetic");var steps=JsonSerializer.Deserialize<EquipmentPropertyStep[]>(edge.GetProperty("Steps").GetRawText());
        plans.Add(121,new(edge.GetProperty("Code").GetString(),121,false,steps));int count=0;
        foreach(var row in root.GetProperty("cases").EnumerateArray())
        {
            var i=row[0].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var f=new Fixture(bases[i[1]],i[1],i[7]!=0);
            EquipmentPropertyWriter.Create(plans[i[0]],new(i[3],i[4],i[5]),i[2],f);if(i[6]!=0){EquipmentPropertyWriter.Ethereality(f);}
            Require(Same(f.BaseStats,row[1])&&Same(f.List,row[2])&&f.Draws==row[3].GetInt32(),"Complete source creation base/list/range/seed-service result at "+count);
            var trace=row[4].EnumerateArray().Select(v=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
            Require(f.Trace.Count==trace.Length&&f.Trace.Zip(trace).All(p=>p.First.SequenceEqual(p.Second)),"Complete creation service trace at "+count);count++;
        }
        Require(count==61472,"Incomplete fresh numeric/layered source controls");
        Log.Information("Item property creation:61472 complete writer controls;202older kernel/201fresh-admitted/191original projection plans;skilltab current gap retained");
    }
    private static EquipmentStatValues Services(int basis)=>new(_=>basis==3?3:4,_=>null,_=>null,(_,_,_,_,_)=>{});
    private static bool RawEqual(EquipmentStatValue[] values,JsonElement expected)
    {
        var e=expected.EnumerateArray().Select(v=>new EquipmentStatValue(v[0].GetUInt32(),v[1].GetInt32())).ToArray();
        return values.Select(v=>v.Packed).Concat(e.Select(v=>v.Packed)).Distinct()
            .All(k=>EquipmentStatValues.Raw(values,k)==EquipmentStatValues.Raw(e,k));
    }
    private static void State(ItemPropertyCreationContext c,JsonElement row,bool continued=false)
    {
        var s=c.Capture();int offset=continued?7:3;
        Require(s.Seed==ulong.Parse(row[offset].GetString())&&s.Ethereal==(row[2].GetInt32()!=0)&&s.HasProperties
            &&RawEqual(s.ItemBase,row[continued?9:5])&&RawEqual(s.Properties,row[continued?10:6]),"Fresh source graph/LCG/raw property/ethereal result");
        Require(c.Unit.Kind==4&&c.Unit.Stats is{OwnerKind:4,Parent:null}&&c.Unit.Stats.Owner==c.Unit,"Fresh graph ownership or attachment changed");
    }
    private static void Refusals(ItemPropertyCreationContext c)
    {
        string before=JsonSerializer.Serialize(c.Capture());int draws=c.Draws;
        foreach(string code in new[]{"skilltab","war-tab-rand","charged","hit-skill","sock"})
        {
            bool refused=false;try{c.Apply(EquipmentPropertyWriter.Plan(code),new(1,3,21));}catch(NotSupportedException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(c.Capture())==before&&c.Draws==draws,"Unresolved fresh property mutated seed or graph: "+code);
        }
    }
    private static void Check(bool reload)
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-property-creation-cases.json")));var root=source.RootElement;var bases=Bases(root);Kernel(root,bases);
        var fresh=root.GetProperty("fresh").EnumerateArray().ToArray();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[fresh.Length];int count=0;
        foreach(var row in fresh)
        {
            int b=row[0].GetInt32();Item item;ItemPropertyCreationContext c;
            if(reload)
            {
                var old=saved[count];item=World.FindItem((Serial)old.Item);Require(old.Case==count&&item is{Deleted:false,Visible:false,Movable:false,Parent:null}
                    &&item.Map==Map.Trammel&&item.X==old.X&&item.Y==old.Y&&JsonSerializer.Serialize(ClassGear.State(item))==old.NativeGear,"Original native item/identity/profile changed");
                c=ItemPropertyCreationContext.Restore(item,Services(b),bases[b],b!=3,b==3,b==2,old.State);Require(c.Draws==0,"Restore rerolled properties");
            }
            else
            {
                item=new Item(0x14F0){Name="Fresh property graph [unattached fixture]",Visible=false,Movable=false};item.MoveToWorld(new(6020+count%3,2920+count/3,0),Map.Trammel);
                string original=JsonSerializer.Serialize(ClassGear.State(item));var initial=new Fixture(bases[b],b,true).BaseStats.Select(p=>new EquipmentStatValue(p.Key,p.Value)).ToArray();
                c=new(item,Services(b),bases[b],b!=3,b==3,b==2,ulong.Parse(row[1].GetString()),initial);
                foreach(var call in root.GetProperty("calls").EnumerateArray())
                {c.Apply(EquipmentPropertyWriter.Plan(call[0].GetString()),new(call[1].GetInt32(),call[2].GetInt32(),call[3].GetInt32()));}
                if(row[2].GetInt32()!=0){EquipmentPropertyWriter.Ethereality(c);}
                Require(c.Draws==row[4].GetInt32()&&JsonSerializer.Serialize(ClassGear.State(item))==original,"Fresh draw count/native profile changed");
                saved[count]=new(count,item.Serial.Value,item.X,item.Y,original,c.Capture());
            }
            State(c,row);Refusals(c);
            // Resume from the exact captured graph, preserving all raw
            // properties/flags and continuing the item seed without rerolls.
            var restored=ItemPropertyCreationContext.Restore(item,Services(b),bases[b],b!=3,b==3,b==2,c.Capture());
            Require(restored.Capture().ItemBase.SequenceEqual(c.Capture().ItemBase)&&restored.Capture().Properties.SequenceEqual(c.Capture().Properties),"Fresh snapshot raw arrays changed");
            restored.Apply(EquipmentPropertyWriter.Plan("hp"),new(1,5,0));State(restored,row,true);
            Require(restored.Draws==row[8].GetInt32()-row[4].GetInt32(),"Restored seed continued with extra draws");count++;
        }
        Require(count==24,"Fresh item graph fixture count changed");
        if(!reload){File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));}
        Log.Information("Fresh property graphs:24original native items/source LCG compositions;class layers,raw values,ethereal snapshots,unresolved refusals and exact seed continuation;no full item initialization or NPC actor");
    }
}
