// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetSearchNestedChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetSearchNestedChecks));
    private static GenericPetMonsterSecondaryLink[] _links;
    private static GenericPetMonster2Metadata[] _rows;
    private static int[] Ints(JsonElement a)=>a.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current nested search helpers and source composition",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-search-nested-check");Register("generic-pet-search-nested-reload-check");}
    internal static (GenericPetSearchServiceChecks.Scene Scene,GenericPetSearchNestedServices Nested) Create(JsonElement p)
    {
        _links??=BalanceCatalog.Read<GenericPetMonsterSecondaryLink>("generic-pet-search-monster-links");_rows??=BalanceCatalog.Read<GenericPetMonster2Metadata>("generic-pet-search-monster2");
        var scene=GenericPetSearchServiceChecks.Create(p);void A(params object[] values)=>scene.Trace.Add(values);
        var units=p.GetProperty("nested").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>
        {var r=x.Value;var sizes=Ints(r.GetProperty("objectSizes"));int id=p.GetProperty("unitIds").GetProperty(x.Name).GetInt32();return new GenericPetSearchNestedUnit(id,r.GetProperty("hasObjectBase").GetBoolean(),sizes[0],sizes[1],r.GetProperty("relationHead").GetInt32(),r.GetProperty("primary").GetInt32(),r.GetProperty("secondary").GetInt32());});
        var relations=p.GetProperty("relations").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>new GenericPetSearchRelationNode(x.Value[0].GetInt32(),x.Value[1].GetUInt32(),x.Value[2].GetInt32()));
        var nodes=p.GetProperty("nodes").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>new GenericPetSearchStatNode(x.Value[0].GetInt32(),x.Value[1].GetInt32()));
        var links=(GenericPetMonsterSecondaryLink[])_links.Clone();var rows=(GenericPetMonster2Metadata[])_rows.Clone();
        foreach(var r in p.GetProperty("linkOverrides").EnumerateObject()){int id=int.Parse(r.Name);links[id]=links[id] with{SecondaryId=r.Value.GetInt16()};}
        foreach(var r in p.GetProperty("dimensionOverrides").EnumerateObject()){int id=int.Parse(r.Name);rows[id]=rows[id] with{SizeX=r.Value[0].GetSByte(),SizeY=r.Value[1].GetSByte()};}
        var table=p.GetProperty("linkOverrides").GetRawText()=="{}"&&p.GetProperty("dimensionOverrides").GetRawText()=="{}"&&p.GetProperty("secondaryCount").GetInt32()==623
            ?GenericPetSecondaryTables.Table:new GenericPetSecondaryTable(links,rows.Take(p.GetProperty("secondaryCount").GetInt32()).ToArray());
        var maskRaw=p.GetProperty("mask410");uint[] mask=maskRaw.ValueKind==JsonValueKind.Null?null:maskRaw.EnumerateArray().Select(x=>x.GetUInt32()).ToArray();
        var nested=new GenericPetSearchNestedServices(scene.World,id=>units[id],id=>relations[id],id=>nodes[id],
            (selector,id)=>{bool value=p.GetProperty("statMetadata").GetBoolean();A(71,selector,id,value);return value?EquipmentStatValues.Definition(id):null;},
            (node,key,metadata)=>{int value=p.GetProperty("nodes").GetProperty(node.ToString())[2].GetInt32();A(72,node,key,value);return value;},
            selector=>{if(selector is <1 or >3){throw new NotSupportedException("Unbound wide state table");}ulong count=p.GetProperty("wideCount").GetUInt64();A(70,selector,count);return count;},
            _=>mask,(selector,cls)=>table.Bind(selector).Row(selector,cls),
            (selector,cls)=>{var raw=p.GetProperty("missileSize");byte? value=raw.ValueKind==JsonValueKind.Null?null:raw.GetByte();A(74,selector,cls,value);return value;},
            (room,x,y,tx,ty,bits)=>{int value=p.GetProperty("traceResult").GetInt32();A(73,room,x,y,tx,ty,bits,value);return value;});
        var world=GenericPetSearchNested.Bind(scene.World,nested);nested=nested with{World=world};
        var callback=GenericPetSearchWorld.Bind(scene.Wrapper.Callback,1,world,id=>
        {var r=p.GetProperty("graphRooms").GetProperty(id.ToString());return new(r[0].GetInt32(),r[1].GetUInt32(),r[2].GetInt32());});
        return(scene with{World=world,Wrapper=scene.Wrapper with{Callback=callback}},nested);
    }
    private static void Check()
    {
        _links=BalanceCatalog.Read<GenericPetMonsterSecondaryLink>("generic-pet-search-monster-links");_rows=BalanceCatalog.Read<GenericPetMonster2Metadata>("generic-pet-search-monster2");
        Require(_links.Length==751&&_rows.Length==623&&_rows.Count(r=>r.Id!=r.TextHcIdx)==19,"Incomplete secondary catalog");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-search-nested-source-probe.json")));
        int count=0,fatal=0,pipeline=0,pipelineFatal=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var p=entry[0];var(scene,s)=Create(p);int failed=0;object result=0;string name=entry[1].GetString();int[] args=entry[2].EnumerateArray().Select(x=>x[1].GetInt32()).ToArray();
            try
            {
                var raw=p.GetProperty("mask410");uint[] mask=raw.ValueKind==JsonValueKind.Null?null:raw.EnumerateArray().Select(x=>x.GetUInt32()).ToArray();
                result=name switch
                {
                    "secondary"=>s.Secondary((byte)args[0],args[1]) is{} row?new[]{row.Id,(int)row.Flags4,row.Flags6,row.SizeX,row.SizeY}:(object)(-1),
                    "sizeX"=>GenericPetSearchNested.SizeX(args[0],s),"sizeY"=>GenericPetSearchNested.SizeY(args[0],s),
                    "alignment"=>GenericPetSearchNested.Alignment(args[0],s),"allegiance"=>GenericPetSearchNested.Allegiance(args[0],args[1],s),
                    "relation"=>GenericPetSearchNested.Relation(args[0],args[1],s),"mask"=>GenericPetSearchNested.StateMask(args[0],mask,s),
                    "ray"=>GenericPetSearchNested.Ray(args[0],args[1],args[2],args[3],args[4],args[5],args[6],args[7],s),
                    "point"=>GenericPetSearchNested.Point(args[0],args[1],args[2],args[3],s),"index"=>GenericPetSearchNested.SpecialIndex(args[0],args[1],s),
                    "valid"=>GenericPetSearchWorld.Valid(args[0],args[1],args[2],(byte)args[3],s.World),
                    "corpse"=>GenericPetSearchWorld.Corpse(args[0],s.World),"collision"=>GenericPetSearchWorld.Collision(args[0],args[1],args[2],s.World),
                    "special"=>GenericPetSearchWorld.Special(args[0],args[1],s.World),_=>throw new InvalidOperationException("Unknown nested helper")
                };
            }
            catch(NotSupportedException){failed=1;fatal++;scene.Trace.Add([99]);}
            string actual=JsonSerializer.Serialize(new object[]{failed,result,GenericPetSearchServiceChecks.Caches(scene),scene.Trace});
            Require(actual==entry[3].GetRawText(),"Nested search differs:"+count+":"+name+":"+actual[..Math.Min(actual.Length,1500)]);count++;
        }
        foreach(var entry in doc.RootElement.GetProperty("pipeline").EnumerateArray())
        {
            var p=entry[0];var(scene,_)=Create(p);int failed=0;var result=new GenericPetCombatSelection(new int[5],0,0);
            try
            {
                int mask=p.GetProperty("mask").GetInt32(),target=p.GetProperty("explicit").GetInt32();
                result=pipeline%2==0?GenericPetSearchWrapper.Run(scene.Input,scene.Rows,mask,target,scene.Wrapper):GenericPetSearchWrapper.Provider(scene.Input,scene.Wrapper)(scene.Rows,mask,target);
            }
            catch(NotSupportedException){failed=1;pipelineFatal++;scene.Trace.Add([99]);}
            var rows=scene.Rows.Select(r=>new object[]{r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39}).ToArray();
            string actual=JsonSerializer.Serialize(new object[]{failed,result.Targets,new[]{result.Word40,result.Word48},rows,scene.Traversal.Values.Select(u=>u.Path?.Target??0).ToArray(),GenericPetSearchServiceChecks.Caches(scene),scene.Trace});
            Require(actual==entry[1].GetRawText(),"Nested search pipeline differs:"+pipeline+":"+actual[..Math.Min(actual.Length,1800)]);pipeline++;
        }
        Require(count==10423&&fatal==408&&pipeline==216&&pipelineFatal==0,"Incomplete nested matrix:"+count+"/"+fatal+"/"+pipeline+"/"+pipelineFatal);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Nested helpers enabled incomplete demons");
        Log.Information("GenericPet nested search:10423current helper/composition contexts,408explicit boundaries and216full search compositions;actual allegiance/stat nodes,relations,secondary metadata,mask loops,signed sizes,ray/point/index geometry pass;stat/mask/terminal world producers and actor remain gated");
    }
}
