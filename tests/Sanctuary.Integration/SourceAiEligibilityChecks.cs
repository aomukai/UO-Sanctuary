// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
namespace Sanctuary.Integration;
public static class SourceAiEligibilityChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SourceAiEligibilityChecks));
    private static void Require(bool condition,string reason){if(!condition){throw new InvalidOperationException(reason);}}
    public static void Configure(){Register("source-ai-eligibility-check");Register("source-ai-eligibility-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Source AI permission and existing current species catalog",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Check()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"source-ai-eligibility-source-probe.json")));
        int contexts=0,current=0,metadata=0,corpses=0;
        foreach(var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var p=row[0];var expected=row[1];int bits=p[4].GetInt32();
            var unit=p[0].GetInt32()==0?null:new SourceAiUnit(p[1].GetInt32(),p[2].GetInt32(),(bits&1)!=0,(bits&2)!=0,(bits&4)!=0,(bits&8)!=0,(bits&16)!=0,(bits&32)!=0,(bits&64)==0);
            uint state=p[3].GetUInt32();
            SourceAiMonster Lookup(int cls)=>cls>=0&&cls<751?new(p[5].GetInt32(),p[6].GetInt32()!=0&&p[9].GetInt32()!=0,p[7].GetInt32()!=0,p[8].GetInt32()!=0):null;
            var actual=new[]{SourceAiEligibility.Allowed(unit,state,751,Lookup),SourceAiEligibility.Utility(unit,state,Lookup),
                SourceAiEligibility.Think(unit,state,true,Lookup),SourceAiEligibility.Think(unit,state,false,Lookup),SourceAiEligibility.CanSwitch(p[2].GetInt32(),Lookup)};
            for(int i=0;i<actual.Length;i++){Require(actual[i]==(expected[i].GetInt32()!=0),"Source AI predicate differs:"+contexts+"/"+i);}
            contexts++;
        }
        foreach(var row in fixture.RootElement.GetProperty("metadata").EnumerateArray())
        {
            var actual=SourceAiEligibility.CurrentMonster(metadata);
            Require(actual==new SourceAiMonster(row[0].GetInt32(),row[1].GetInt32()!=0,row[2].GetInt32()!=0,row[3].GetInt32()!=0),"Current AI metadata differs:"+metadata);
            Require(GenericPetSearchTables.SourceId(GenericPetSearchTables.SourceName(metadata))==metadata,"Existing species identity changed");metadata++;
        }
        var expectedCurrent=new Dictionary<(int Class,int Bits),bool>();
        foreach(var row in fixture.RootElement.GetProperty("current").EnumerateArray())
        {
            int cls=row[0].GetInt32(),bits=row[1].GetInt32();bool expected=row[2].GetInt32()!=0;
            var unit=new SourceAiUnit(1,cls,(bits&1)!=0,(bits&2)!=0,(bits&4)!=0,(bits&8)!=0,(bits&16)!=0,(bits&32)!=0,(bits&64)==0);
            Require(SourceAiEligibility.Current(unit,SourceAiEligibility.Revived)==expected,"Current Revive AI permission differs:"+cls+"/"+bits);
            expectedCurrent.Add((cls,bits),expected);current++;
        }
        using var saved=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"revive-creature-snapshot-fixtures.json")));
        foreach(var row in saved.RootElement.GetProperty("Rows").EnumerateArray())
        {
            var corpse=World.FindItem((Serial)row.GetProperty("Corpse").GetUInt32()) as Corpse;
            var unit=SourceCorpseIdentity.For(corpse);
            for(int bits=0;bits<128;bits++)
            {
                var state=new ReviveAiStatus((bits&1)!=0,(bits&4)!=0,(bits&8)!=0,(bits&16)!=0,(bits&32)!=0,(bits&64)==0);
                Require(unit.AllowsRevivedAi(state)==expectedCurrent[(unit.ClassId,bits|2)],"Saved original corpse bypassed AI permission");corpses++;
            }
        }
        foreach(int missing in new[]{-1,751,int.MaxValue}){Require(SourceAiEligibility.CurrentMonster(missing)==null,"Missing species supplied invented AI metadata");}
        Require(contexts==61440&&current==96128&&metadata==751&&corpses==1152,"Incomplete AI-permission coverage");
        Log.Information("Source AI eligibility:61440complete older contexts/five predicates,96128current Revive contexts,751reused species rows and1152saved-corpse contexts pass;dynamic source state remains explicit input;native producer,actor restoration and lifecycle remain unfinished");
    }
}
