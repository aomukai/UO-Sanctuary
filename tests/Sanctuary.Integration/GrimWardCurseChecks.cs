// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class GrimWardCurseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrimWardCurseChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("grim-ward-curse-check","Ward explicit-context curse results and ordered operations",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("grim-ward-curse-check: passed");}catch(Exception e){Log.Error(e,"grim-ward-curse-check: failed");}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Refuse(Action run)
    {try{run();throw new InvalidOperationException("Unsupported Ward context accepted");}catch(NotSupportedException){}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"grim-ward-curse-source-probe.json")));
        int count=0,accepted=0,retained=0,immune=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row[0];int I(int i)=>p[i].GetInt32();int rank=I(0),hard=I(1),oldKind=I(8);
            // Effective Find Potion deliberately differs from its hard rank.
            // The explicit formula context must consume blvl,not +skills.
            var values=new SkillValues(SkillMechanics.For(CombatAbility.GrimWard),key=>key=="Grim Ward"?rank:key=="Find Potion"?hard+7:0,
                key=>key=="Find Potion"?hard:0,80);
            var difficulty=I(2) switch{1=>MonsterDifficulty.Normal,2=>MonsterDifficulty.Nightmare,4=>MonsterDifficulty.Hell,_=>throw new InvalidOperationException()};
            var packet=GrimWardCurseOperation.Packet(values,difficulty);
            var old=oldKind==0?null:new GrimWardCurseList(oldKind==5?57:56,oldKind==4?77:oldKind==5?86:150,
                oldKind==2?rank+1:oldKind==3?rank-1:rank,999,777,-101,-102,-103,-104);
            var target=I(9)<0?null:new GrimWardCurseTarget(I(9),I(4),I(3)!=0,I(5),I(6),I(7)!=0,oldKind==5);
            var result=GrimWardCurseOperation.Apply(packet,target,old,9,100);var list=result.List;
            object[] fields=list==null?null:[list.State,list.Skill,list.Rank,list.Creator,list.Expires,list.Velocity,list.AttackRate,list.OtherRate,list.Physical];
            object[] actual=[result.Accepted?1:0,fields,result.PermissionCalls,result.AiCalls,result.Removed,result.Events,result.AnimationUpdates,result.Operations,9,packet.Radius];
            Require(JsonSerializer.Serialize(actual)==JsonSerializer.Serialize(row[1]),$"Ward curse case{count}: {JsonSerializer.Serialize(actual)} expected {row[1]}");
            Require(packet.Physical==-20-5*hard&&packet.Duration==60/I(2)&&packet.AttackRate==packet.Velocity&&packet.OtherRate==packet.Velocity,
                "Pinned Ward packet used an effective synergy or divergent slow components");
            if(result.Accepted){accepted++;if(list.Creator==999){retained++;}if(I(6)>=100&&I(7)==0){immune++;}}
            count++;
        }
        Require(count==31134&&accepted==5184&&retained==1296&&immune==864,"Incomplete Ward curse matrix");
        Refuse(()=>GrimWardCurseOperation.Packet(new(SkillMechanics.For(CombatAbility.Terror),_=>1,_=>0,80),MonsterDifficulty.Normal));
        Refuse(()=>GrimWardCurseOperation.Packet(new(SkillMechanics.For(CombatAbility.GrimWard),_=>0,_=>0,80),MonsterDifficulty.Normal));
        Require(BarbarianCombat.Implemented(CombatAbility.GrimWard),"Prepared receiver enabled incomplete Ward world/cast");
        Log.Information("Ward curse:{Count}complete legacy list/operation comparisons,{Accepted}accepted,{Retained}equal-rank creator/primary retentions,{Immune}immune reductions;explicit formula context,secondary rewrites and permission/factory/AI/stat/animation order pass;actual missile-owner/AI/world/learned context remains unverified",count,accepted,retained,immune);
    }
}
