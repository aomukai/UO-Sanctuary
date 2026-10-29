// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetMetadataChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetMetadataChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private static object[] Group(GenericPetStateGroup g)=>[g.Slot,g.StateGroup,g.States.ToArray(),g.Flag4?1:0,g.Flag2?1:0];
    private static GenericPetCombatCandidate Candidate(int id,byte type)=>new(7,0,int.MinValue,id,3,16,2,type,0,91,92);
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Owned compiled pet metadata and source preparation composition",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-metadata-check");Register("generic-pet-metadata-reload-check");}
    private static void Unsupported(Action action)
    {bool failed=false;try{action();}catch(NotSupportedException){failed=true;}Require(failed,"Unbound pet metadata context silently accepted");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-metadata-source-probe.json")));
        var data=doc.RootElement;var table=GenericPetMetadata.Table;
        Require(table is{}&&table.SkillCount==429&&table.StateCount==232,"Missing owned pet catalog");
        foreach(var expected in data.GetProperty("skills").EnumerateArray())
        {
            int id=expected.GetProperty("id").GetInt32();var actual=table.Skill(id);
            Require(actual==new GenericPetSkillMetadata(id,expected.GetProperty("name").GetString(),expected.GetProperty("flags36").GetByte(),expected.GetProperty("flags39").GetByte(),expected.GetProperty("aiType").GetByte(),expected.GetProperty("auraState").GetInt16(),expected.GetProperty("auraTargetState").GetInt16()),"Owned skill row differs:"+id);
            foreach(byte selector in new byte[]{1,2,3})
            {
                var view=table.Bind(selector);var ai=view.SkillInfo(selector,id);var buff=view.BuffSkill(selector,id);
                Require(ai==new GenericPetSkillAiInfo(actual.Flags36,actual.Flags39,actual.AiType)&&buff==new GenericPetBuffSkill(actual.AuraState,actual.Flags39)
                    &&view.SearchSkill(selector,id)==actual.AuraTargetState&&view.SkillCount(selector)==429,"Metadata field adapter differs:"+id);
            }
        }
        foreach(var expected in data.GetProperty("states").EnumerateArray())
        {
            int id=expected.GetProperty("id").GetInt32();var row=table.State(id);
            Require(row==new GenericPetStateMetadata(id,expected.GetProperty("name").GetString(),expected.GetProperty("group").GetInt16()),"Owned state row differs:"+id);
            foreach(byte selector in new byte[]{1,2,3})
            {var view=table.Bind(selector);Require(view.StateGroup(selector,id)==row.Group&&view.RawStateGroup(selector,id)==row.Group&&view.StateCount(selector,false)==232&&view.StateCount(selector,true)==232,"Metadata state adapter differs:"+id);}
        }
        Require(table.Skill(362).Name=="Map"&&table.Skill(-1)==null&&table.Skill(429)==null&&table.State(-1)==null&&table.State(232)==null,"Owned row identity/bounds changed");
        foreach(byte invalid in new byte[]{0,4,255}){Unsupported(()=>table.Bind(invalid));}
        var one=table.Bind(1);Unsupported(()=>one.SearchSkill(2,52));Unsupported(()=>one.StateGroup(3,16));Unsupported(()=>one.RawStateGroup(1,232));
        Require(one.SearchSkill(1,-1)==null&&one.BuffSkill(1,429)==null&&one.SkillInfo(1,-1)==null,"Missing metadata lookup fabricated a row");
        int count=0;var search=new GenericPetSearchStates();
        foreach(var entry in data.GetProperty("cases").EnumerateArray())
        {
            byte selector=entry[0].GetByte();int id=entry[1].GetInt32();var view=table.Bind(selector);var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);
            int StateCount(byte s,bool wide){int n=view.StateCount(s,wide);A(2,s,wide?1:0,n);return n;}
            short? State(byte s,int state){short? group=view.StateGroup(s,state);A(3,s,state,group);return group;}
            short Raw(byte s,int state){short group=view.RawStateGroup(s,state);A(5,s,state,group);return group;}
            var rows=new List<GenericPetCombatCandidate>{Candidate(id,1)};
            var services=new GenericPetPreparationServices(
                (s,skill)=>{var info=view.BuffSkill(s,skill);A(1,s,skill,info is{}?new object[]{info.State,info.Flags39}:null);return info;},
                StateCount,State,Raw,(unit,state)=>{A(4,unit,state,0);return false;},
                _=>throw new InvalidOperationException("Unexpected owner traversal"),_=>throw new InvalidOperationException("Unexpected nested traversal"),
                _=>throw new InvalidOperationException("Unexpected unit geometry"),(_,_,_)=>throw new InvalidOperationException("Unexpected distance"));
            GenericPetPreparation.Apply(new(selector,1,0),rows,services);
            string actual=JsonSerializer.Serialize(new object[]{0,rows.Select(Row).ToArray(),trace});
            Require(actual==entry[2].GetRawText(),"Owned buff/source composition differs:"+selector+"/"+id);
            rows=[Candidate(id,2)];trace.Clear();search.Groups.Clear();var old=new GenericPetStateGroup(0,6);old.States.AddRange([1,2]);search.Groups.Add(old);
            var searchServices=new GenericPetSearchStateServices((s,skill)=>{var word=view.SearchSkill(s,skill);A(1,s,skill,word);return word;},StateCount,State,Raw);
            search.Prepare(selector,rows,searchServices);
            actual=JsonSerializer.Serialize(new object[]{0,rows.Select(Row).ToArray(),search.Groups.Select(Group).ToArray(),trace});
            Require(actual==entry[3].GetRawText(),"Owned search/source composition differs:"+selector+"/"+id);count++;
        }
        Require(count==1287,"Incomplete owned metadata compositions:"+count);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Metadata enabled incomplete demons");
        Log.Information("GenericPet metadata:429skills/232states;1287buff+1287search current-code compositions,explicit selector/missing-row guards and Map362identity pass;world selector/search/actor remain gated");
    }
}
