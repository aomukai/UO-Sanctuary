// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetStateMaskChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetStateMaskChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current literal state masks and complete search",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-state-masks-check");Register("generic-pet-state-masks-reload-check");}
    private static GenericPetSearchProducerChecks.Context Create(JsonElement p)
    {
        var c=GenericPetSearchProducerChecks.Create(p);byte selector=p.GetProperty("selector").GetByte();
        var bound=GenericPetStateMasks.Bind(c.Nested,GenericPetStateMasks.Bind(selector));
        var nested=bound with{WideStateCount=s=>{ulong n=bound.WideStateCount(s);c.Scene.Trace.Add([70,s,n]);return n;}};
        var world=GenericPetSearchNested.Bind(c.Scene.World,nested);nested=nested with{World=world};
        var callback=GenericPetSearchWorld.Bind(c.Scene.Wrapper.Callback,1,world,id=>{var r=p.GetProperty("graphRooms").GetProperty(id.ToString());return new(r[0].GetInt32(),r[1].GetUInt32(),r[2].GetInt32());});
        return c with{Nested=nested,Scene=c.Scene with{World=world,Wrapper=c.Scene.Wrapper with{Callback=callback}}};
    }
    private static void Check()
    {
        int boundaries=0;
        foreach(byte selector in new byte[]{0,4,255})
        {try{GenericPetStateMasks.Bind(selector);throw new InvalidOperationException("Invalid selector accepted");}catch(NotSupportedException){boundaries++;}}
        foreach(byte selector in new byte[]{1,2,3})
        {
            var v=GenericPetStateMasks.Bind(selector);var mask=v.Mask(selector);uint original=mask[0];mask[0]^=uint.MaxValue;
            Require(v.Mask(selector)[0]==original&&GenericPetStateMasks.Bind(selector).Mask(selector)[0]==original,"Mask catalog exposed mutable storage");
            try{v.Mask((byte)(selector==3?1:3));throw new InvalidOperationException("Mismatched mask selector accepted");}catch(NotSupportedException){boundaries++;}
            try{v.StateCount((byte)(selector==3?1:3));throw new InvalidOperationException("Mismatched count selector accepted");}catch(NotSupportedException){boundaries++;}
        }
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-state-masks-source-probe.json")));
        int count=0,pipeline=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var p=entry[0];var c=Create(p);string name=entry[1].GetString();byte selector=p.GetProperty("selector").GetByte();
            int result=name=="mask"?GenericPetSearchNested.StateMask(1,c.Nested.CorpseMask410(selector),c.Nested):GenericPetSearchWorld.Corpse(1,c.Scene.World);
            string actual=JsonSerializer.Serialize(new object[]{0,result,GenericPetSearchServiceChecks.Caches(c.Scene),c.Scene.Trace});
            Require(actual==entry[2].GetRawText(),"State mask source differs:"+count+":"+name+":"+actual);count++;
        }
        foreach(var entry in doc.RootElement.GetProperty("pipeline").EnumerateArray())
        {
            var p=entry[0];var c=Create(p);var scene=c.Scene;int mask=p.GetProperty("mask").GetInt32(),target=p.GetProperty("explicit").GetInt32();
            var result=pipeline%2==0?GenericPetSearchWrapper.Run(scene.Input,scene.Rows,mask,target,scene.Wrapper):GenericPetSearchWrapper.Provider(scene.Input,scene.Wrapper)(scene.Rows,mask,target);
            var rows=scene.Rows.Select(r=>new object[]{r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39}).ToArray();
            string actual=JsonSerializer.Serialize(new object[]{0,result.Targets,new[]{result.Word40,result.Word48},rows,scene.Traversal.Values.Select(u=>u.Path?.Target??0).ToArray(),GenericPetSearchServiceChecks.Caches(scene),scene.Trace});
            Require(actual==entry[1].GetRawText(),"State mask pipeline differs:"+pipeline+":"+actual[..Math.Min(actual.Length,1800)]);pipeline++;
        }
        Require(count==2784&&pipeline==216&&boundaries==9,"Incomplete state mask matrix:"+count+"/"+pipeline+"/"+boundaries);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Masks enabled incomplete demon");
        Log.Information("GenericPet state masks:2784current mask/corpse contexts,216full search compositions and9selector boundaries;all232states in3explicit profiles match;real world bindings,ordering and actors remain gated");
    }
}
