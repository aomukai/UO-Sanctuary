// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetSearchProducerChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetSearchProducerChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private sealed class Cells(int room,ushort[] values,Action<int,int,ushort> read):IReadOnlyList<ushort>
    {
        public int Count=>values.Length;
        public ushort this[int index]{get{ushort value=values[index];read(room,index,value);return value;}}
        public IEnumerator<ushort> GetEnumerator()=>((IEnumerable<ushort>)values).GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    internal sealed record Context(GenericPetSearchServiceChecks.Scene Scene,GenericPetSearchNestedServices Nested,GenericPetProducerServices Services,
        EquipmentStatValue[] Lookup,Func<int,EquipmentStatDefinition> DirectCost,SourceGridPoint From,SourceGridPoint To);
    private static EquipmentStatValue[] Values(JsonElement raw)=>raw.EnumerateArray().Select(r=>new EquipmentStatValue(unchecked((uint)r[0].GetInt64()),r[1].GetInt32())).ToArray();
    internal static Context Create(JsonElement p)
    {
        var(scene,nested)=GenericPetSearchNestedChecks.Create(p);void A(params object[] values)=>scene.Trace.Add(values);
        EquipmentStatDefinition Override(EquipmentStatDefinition d)
        {
            if(d!=null&&p.GetProperty("costOverrides").TryGetProperty(d.Id.ToString(),out var r))
            {return d with{Flags=(d.Flags&~1024u)|(r[0].GetBoolean()?1024u:0u),Shift=r[1].GetInt32(),Minimum=r[2].GetInt32()};}return d;
        }
        EquipmentStatDefinition DirectCost(int id)=>id<0?null:Override(EquipmentStatValues.Definition(id));
        GenericPetProducerView View(byte selector)=>GenericPetProducerTables.Bind(selector,p.GetProperty("statCount").GetInt32(),p.GetProperty("missileCount").GetInt32());
        var nodes=p.GetProperty("nodes").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>
        {
            if(p.GetProperty("nodeReads").TryGetProperty(x.Name,out var r))
            {var owner=r.GetProperty("owner");return new EquipmentStatNode{State=x.Value[0].GetInt32(),Flags=unchecked((uint)r.GetProperty("flags").GetInt32()),Owner=owner.ValueKind==JsonValueKind.Null?null:new(null,owner.GetInt32()),BaseValues=Values(r.GetProperty("base")),FullValues=Values(r.GetProperty("full"))};}
            int value=x.Value[2].GetInt32();return new EquipmentStatNode{State=x.Value[0].GetInt32(),Flags=0x80000000,Owner=new(null,1),BaseValues=[new(0xac0000,value)],FullValues=[new(0xac0000,value)]};
        });
        var rooms=p.GetProperty("traceRooms").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>
        {
            int id=int.Parse(x.Name);var r=x.Value;IReadOnlyList<ushort> cells=r[6].ValueKind==JsonValueKind.Null?null:new Cells(id,r[6].EnumerateArray().Select(x=>x.GetUInt16()).ToArray(),(room,index,value)=>A(80,room,index,value));
            return new SourceCollisionRoom(r[0].GetInt32(),r[1].GetInt32(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),cells,r[5].EnumerateArray().Select(x=>x.GetInt32()).ToArray());
        });
        var services=new GenericPetProducerServices((selector,id)=>{A(81,selector,id);return Override(View(selector).Stat(selector,id));},id=>nodes.GetValueOrDefault(id),
            (selector,id)=>{A(82,selector,id);var row=View(selector).Missile(selector,id);return row!=null&&p.GetProperty("missileOverrides").TryGetProperty(id.ToString(),out var size)?row with{Size434=size.GetByte()}:row;},id=>rooms[id]);
        nested=GenericPetSearchProducers.Bind(nested,services);var world=GenericPetSearchNested.Bind(scene.World,nested);nested=nested with{World=world};
        var callback=GenericPetSearchWorld.Bind(scene.Wrapper.Callback,1,world,id=>{var r=p.GetProperty("graphRooms").GetProperty(id.ToString());return new(r[0].GetInt32(),r[1].GetUInt32(),r[2].GetInt32());});
        var from=p.GetProperty("from");var to=p.GetProperty("to");return new(scene with{World=world,Wrapper=scene.Wrapper with{Callback=callback}},nested,services,Values(p.GetProperty("lookupValues")),DirectCost,new(from[0].GetInt32(),from[1].GetInt32()),new(to[0].GetInt32(),to[1].GetInt32()));
    }
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current stat/missile producers and complete room/grid trace",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-search-producers-check");Register("generic-pet-search-producers-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-search-producers-source-probe.json")));
        int count=0,fatal=0,pipeline=0,pipelineFatal=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var p=entry[0];var c=Create(p);var scene=c.Scene;string name=entry[1].GetString();int[] args=entry[2].EnumerateArray().Select(x=>unchecked((int)x[1].GetInt64())).ToArray();int failed=0;object result=0;
            try
            {
                result=name switch
                {
                    "stat"=>c.Services.Stat((byte)args[0],args[1]) is{} d?new object[]{d.Id,(d.Flags&1024)!=0,d.Shift,d.Minimum}:(object)(-1),
                    "missile"=>c.Services.Missile((byte)args[0],args[1]) is{} m?new[]{m.Id,(int)m.Size434}:(object)(-1),
                    "lookup"=>EquipmentStatValues.SourceIndex(c.Lookup,unchecked((uint)args[1])),
                    "read"=>EquipmentStatValues.SourceRead(c.Services.Node(args[0]),unchecked((uint)args[1]),c.DirectCost(args[2])),
                    "resolve"=>SourceRoomTrace.Resolve(args[0],args[1],args[2],c.Services.Room),
                    "trace"=>SourceRoomTrace.Run(args[0],p.GetProperty("nullFrom").GetBoolean()?null:c.From,p.GetProperty("nullTo").GetBoolean()?null:c.To,args[3],c.Services.Room),
                    _=>throw new InvalidOperationException("Unknown producer")
                };
            }
            catch(NotSupportedException){failed=1;fatal++;scene.Trace.Add([99]);}
            string actual=JsonSerializer.Serialize(new object[]{failed,result,new[]{c.From.X,c.From.Y},new[]{c.To.X,c.To.Y},GenericPetSearchServiceChecks.Caches(scene),scene.Trace});
            Require(actual==entry[3].GetRawText(),"Search producer differs:"+count+":"+name+":"+actual[..Math.Min(actual.Length,1500)]);count++;
        }
        foreach(var entry in doc.RootElement.GetProperty("pipeline").EnumerateArray())
        {
            var p=entry[0];var c=Create(p);var scene=c.Scene;int failed=0;var result=new GenericPetCombatSelection(new int[5],0,0);
            try{int mask=p.GetProperty("mask").GetInt32(),target=p.GetProperty("explicit").GetInt32();result=pipeline%2==0?GenericPetSearchWrapper.Run(scene.Input,scene.Rows,mask,target,scene.Wrapper):GenericPetSearchWrapper.Provider(scene.Input,scene.Wrapper)(scene.Rows,mask,target);}
            catch(NotSupportedException){failed=1;pipelineFatal++;scene.Trace.Add([99]);}
            var rows=scene.Rows.Select(r=>new object[]{r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39}).ToArray();
            string actual=JsonSerializer.Serialize(new object[]{failed,result.Targets,new[]{result.Word40,result.Word48},rows,scene.Traversal.Values.Select(u=>u.Path?.Target??0).ToArray(),GenericPetSearchServiceChecks.Caches(scene),scene.Trace});
            Require(actual==entry[1].GetRawText(),"Search producer pipeline differs:"+pipeline+":"+actual[..Math.Min(actual.Length,1800)]);pipeline++;
        }
        Require(count==9058&&fatal==28&&pipeline==216&&pipelineFatal==0,"Incomplete producer matrix:"+count+"/"+fatal+"/"+pipeline+"/"+pipelineFatal);
        AssassinTrapMovementChecks.Paths();
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Producers enabled incomplete demons");
        Log.Information("GenericPet search producers:9058current producer contexts,28explicit boundaries and216full search compositions;current stat lookup/clamping,missile sizes and complete grid trace match;13200retained wall-follow paths share the stepper;actual mask/world/ordering/actor remain gated");
    }
}
