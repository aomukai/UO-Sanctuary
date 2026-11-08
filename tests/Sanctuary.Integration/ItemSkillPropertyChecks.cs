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
public static class ItemSkillPropertyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ItemSkillPropertyChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-skill-property-fixtures.json");
    private sealed record Saved(int Case,uint Item,int Level,string NativeGear,ItemPropertyCreationState State);
    private sealed class Target(ulong seed,bool allocate):EquipmentPropertyServices
    {
        internal readonly Fixture Inner=new(default,0,allocate);
        internal ulong Seed=seed;internal int Draws;
        internal override EquipmentPropertyBase Basis=>Inner.Basis;
        internal override bool Weapon=>Inner.Weapon;
        internal override bool Armor=>Inner.Armor;
        internal override bool Throwable=>Inner.Throwable;
        internal override int BaseValue(int stat)=>Inner.BaseValue(stat);
        internal override void SetBase(int stat,int value)=>Inner.SetBase(stat,value);
        internal override bool GetOrCreateList(int state,int flags)=>Inner.GetOrCreateList(state,flags);
        internal override int ListValue(int stat,int layer)=>Inner.ListValue(stat,layer);
        internal override void SetList(int stat,int value,int layer)=>Inner.SetList(stat,value,layer);
        internal override void AddList(int stat,int value,int layer)=>Inner.AddList(stat,value,layer);
        internal override void SetEthereal()=>Inner.SetEthereal();
        internal override int Roll(int bound)
        {int n=(int)SourceRandom.Limited(ref Seed,bound);Draws++;Inner.Trace.Add([6,bound,n,0]);return n;}
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-skill-property-check",false);Register("item-skill-property-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Raw item proc/charge encoding and item seed;actor dispatch remains gated",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Kernel(JsonElement root)
    {
        var plans=BalanceCatalog.Read<EquipmentPropertyPlan>("equipment-property-plans").Where(ItemSkillPropertyWriter.Supports).ToDictionary(p=>p.Ordinal);
        Require(plans.Count==7&&Enumerable.Range(0,429).All(i=>ItemSkillProperties.Skill(i)?.Id==i)&&ItemSkillProperties.Skill(362).Name==""
            &&ItemSkillProperties.Skill(-1)==null&&ItemSkillProperties.Skill(429)==null,"Current skill table,blank record or plan scope");
        int count=0;
        foreach(string group in new[]{"current","npc","synthetic","missing"})foreach(var row in root.GetProperty(group).EnumerateArray())
        {
            var i=row[0];int V(int k)=>i[k].GetInt32();int skill=V(1);var target=new Target(ulong.Parse(i[6].GetString()),V(7)!=0);
            ItemPropertySkill Lookup(int id)
            {
                var s=ItemSkillProperties.Skill(id);return s!=null&&V(8)>=0?s with{RequiredLevel=V(8),MaximumLevel=V(9)}:s;
            }
            ItemSkillPropertyWriter.Apply(plans[V(0)],new(V(3),V(4),skill),new(V(2),V(5),Lookup),target);
            Require(target.Inner.List.OrderBy(p=>p.Key).Select(p=>(p.Key,p.Value)).SequenceEqual(row[1].EnumerateArray().Select(v=>(v[0].GetUInt32(),v[1].GetInt32())))
                &&target.Seed==ulong.Parse(row[2].GetString())&&target.Draws==row[3].GetInt32(),"Complete proc/charge encoding,allocation or seed at "+count);
            var trace=row[4].EnumerateArray().Select(v=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
            Require(target.Inner.Trace.Count==trace.Length&&target.Inner.Trace.Zip(trace).All(p=>p.First.SequenceEqual(p.Second)),"Complete proc/charge service order at "+count);count++;
        }
        Require(count==72558,"Incomplete proc/charge source controls");
        Log.Information("Item skill properties:72558 complete source controls/7plans/429rows/131NPC specs;encoding,level,capacity,fill,allocation and exact source LCG traces;actor events unchanged");
    }
    private static EquipmentStatValues Services()=>new(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});
    private static bool Same(EquipmentStatValue[] actual,JsonElement expected)=>actual.Select(v=>(v.Packed,v.Value)).OrderBy(p=>p.Packed)
        .SequenceEqual(expected.EnumerateArray().Select(v=>(v[0].GetUInt32(),v[1].GetInt32())).OrderBy(p=>p.Item1));
    private static void Check(bool reload)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-skill-property-cases.json")));var root=doc.RootElement;Kernel(root);
        var graphs=root.GetProperty("fresh").EnumerateArray().ToArray();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[graphs.Length];int index=0;
        foreach(var row in graphs)
        {
            int level=row[0].GetInt32();var metadata=ItemSkillProperties.ForLevel(level);Item item;ItemPropertyCreationContext c;
            if(reload)
            {
                var old=saved[index];item=World.FindItem((Serial)old.Item);Require(old.Case==index&&old.Level==level&&item is{Deleted:false,Visible:false,Movable:false,Parent:null}
                    &&item.Map==Map.Trammel&&item.X==6040+index%3&&item.Y==2950+index/3&&JsonSerializer.Serialize(ClassGear.State(item))==old.NativeGear,"Original encoded-property native item changed");
                c=ItemPropertyCreationContext.Restore(item,Services(),default,true,false,false,old.State);Require(c.Draws==0,"Encoded property restoration rerolled");
            }
            else
            {
                item=new Item(0x14F0){Name="Encoded skill properties [unattached fixture]",Visible=false,Movable=false};item.MoveToWorld(new(6040+index%3,2950+index/3,0),Map.Trammel);
                string before=JsonSerializer.Serialize(ClassGear.State(item));c=new(item,Services(),default,true,false,false,ulong.Parse(row[1].GetString()),[]);
                foreach(var call in root.GetProperty("calls").EnumerateArray())
                {c.ApplySkill(EquipmentPropertyWriter.Plan(call[0].GetString()),new(call[2].GetInt32(),call[3].GetInt32(),call[1].GetInt32()),metadata);}
                Require(c.Draws==row[3].GetInt32()&&JsonSerializer.Serialize(ClassGear.State(item))==before,"Encoded creation changed native item or draw count");
                saved[index]=new(index,item.Serial.Value,level,before,c.Capture());
            }
            var state=c.Capture();Require(state.Seed==ulong.Parse(row[2].GetString())&&state.HasProperties&&Same(state.Properties,row[4]),"Fresh proc accumulation,charge replacement or raw seed differs");
            bool gated=false;try{_=NpcCombatEvents.FromStats(state.Properties);}catch(NotSupportedException){gated=true;}
            Require(gated,"Encoding silently enabled unsupported NPC proc dispatch");
            var continued=ItemPropertyCreationContext.Restore(item,Services(),default,true,false,false,state);
            continued.ApplySkill(EquipmentPropertyWriter.Plan("charged"),new(-30,-8,101),metadata);
            Require(continued.Seed==ulong.Parse(row[5].GetString())&&continued.Draws==row[6].GetInt32()-row[3].GetInt32()&&Same(continued.Capture().Properties,row[7]),"Snapshot charge replacement/seed continuation differs");
            index++;
        }
        Require(index==12,"Raw skill property fixture count changed");
        if(!reload){File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));}
        Log.Information("Encoded skill graphs:12original native items;proc addition,charge overwrite,missing-record no-op,source LCG and snapshot continuation;NPC dispatch gate retained");
    }
}
