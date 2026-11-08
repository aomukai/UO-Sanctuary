// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class GenericPetOwnerSurveyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetOwnerSurveyChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("pet-owner-survey-check",Check);Register("pet-owner-survey-reload-check",Bridge);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Current owner callback and follow/search producer bindings",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static bool B(JsonElement e,string key)=>e.GetProperty(key).GetBoolean();
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-owner-survey-cases.json")));int count=0,unsupported=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<int[]>();
            var state=new GenericPetOwnerSurveyState(N(p,"pet"),N(p,"swarm")){Visits=N(p,"count")};
            foreach(var entry in p.GetProperty("existing").EnumerateArray()){state.Targets.Add(new(entry[0].GetInt32(),entry[1].GetInt32()));}
            var units=Enumerable.Range(1,6).ToDictionary(i=>i,i=>new GenericPetSurveyUnit(N(p,"kind"),100+i,B(p,"monster"),B(p,"ai"),B(p,"generic"),
                B(p,"path")?new GenericPetPathTarget(N(p,"pathTarget"),1,0):null));
            var validity=Ints(p.GetProperty("valid"));int next=0;
            var services=new GenericPetOwnerSurveyServices(i=>units[i],(selector,id)=>{trace.Add([0,selector,id]);return B(p,"row");},
                i=>{trace.Add([1,i]);return N(p,"current");},(unit,target,mode)=>
                {int result=validity[Math.Min(next++,validity.Length-1)];trace.Add([2,unit,target,mode,result]);return result;},
                unit=>
                {
                    trace.Add([3,unit]);
                    if(p.GetProperty("afterKind").ValueKind!=JsonValueKind.Null){units[unit]=units[unit] with{Kind=N(p,"afterKind")};}
                    if(p.GetProperty("afterTarget").ValueKind!=JsonValueKind.Null&&units[unit].Path is{} path){path.Target=N(p,"afterTarget");}
                });
            bool refused=false;
            try{GenericPetOwnerSurvey.Enumerate(checked((byte)N(p,"selector")),state,visit=>{foreach(int unit in Ints(p.GetProperty("visits"))){visit(unit);}},services);}
            catch(NotSupportedException){refused=true;unsupported++;}
            Require(refused==expected.GetProperty("unsupported").GetBoolean(),"Current callback assertion boundary differs");
            Require(state.Visits==N(expected,"visits"),"Owner visit count/filter/wrap differs");
            var swarm=expected.GetProperty("swarm").EnumerateArray().Select(x=>new GenericPetSearchSwarm(x[0].GetInt32(),x[1].GetInt32())).ToArray();
            Require(state.Targets.SequenceEqual(swarm),"Owner target counts/order/duplicate-first-match differs");
            var sourceTrace=expected.GetProperty("trace").EnumerateArray().Select(Ints).ToArray();
            Require(trace.Count==sourceTrace.Length&&trace.Where((t,i)=>!t.SequenceEqual(sourceTrace[i])).Any()==false,"Current target validation/path-refresh service order differs");count++;
        }
        Require(count==78&&unsupported==1,"Missing complete callback contexts");Bridge();
        Log.Information("Owner survey:78complete-current callback contexts/one assertion boundary;source vector growth/copy exercised by offline oracle. Native visit/target counters and ordered services match;follow/search use actual produced values. No automatic demon AI enabled.");
    }
    private static void Bridge()
    {
        var state=new GenericPetOwnerSurveyState(1,2){Visits=6};state.Targets.Add(new(3,2));state.Targets.Add(new(4,1));
        var parameters=GenericPetParameters.Decode(null) with{SwarmRaw=2};
        var search=GenericPetOwnerSurvey.Bind(new GenericPetSearchWrapperContext(1,1,2,parameters,[]),state);
        Require(search.Swarm.SequenceEqual(state.Targets)&&search.Parameters==parameters,"Survey/search state producer binding");
        state.Targets[0]=new(3,7);Require(search.Swarm[0].Count8==2,"Prepared search snapshot changed during subsequent survey");
        var follow=GenericPetOwnerSurvey.Bind(new GenericPetFollowExecution(new(1,true,20,30),new(0,true,45,55),1,60,10,-100),state);
        int radius=-1;
        var leaves=new GenericPetFollowExecutionServices(()=>(65,75),()=>0,(_,_,_)=>0,(_,_,_)=>null,(_,_)=>0,()=>128,
            (_,_,_)=>throw new InvalidOperationException("Unexpected prepare"),()=>throw new InvalidOperationException("Unexpected idle"),
            (_,_)=>throw new InvalidOperationException("Unexpected point"),(_,_,_,_,_)=>throw new InvalidOperationException("Unexpected move"),
            (_,_,_,r,_,_,_)=>{radius=r;return 1;},(_,_,_,_,_,_)=>throw new InvalidOperationException("Unexpected alternate"),
            (_,_,_,_)=>throw new InvalidOperationException("Unexpected toward"),()=>throw new InvalidOperationException("Unexpected warp"));
        Require(follow.Word70==6&&GenericPetFollowExecutor.Execute(follow,new(1),leaves)==1&&radius==5,"Produced count did not reach current follow radius calculation");
        bool refused=false;try{GenericPetOwnerSurvey.Bind(search with{Pet=2},state);}catch(NotSupportedException){refused=true;}
        Require(refused,"Cross-pet survey binding accepted");refused=false;
        try{GenericPetOwnerSurvey.Bind(search with{Parameters=parameters with{SwarmRaw=0}},state);}catch(NotSupportedException){refused=true;}
        Require(refused,"Cross-configuration survey binding accepted");
        foreach(var ability in new[]{CombatAbility.SummonGoatman,CombatAbility.SummonTainted,CombatAbility.SummonDefiler})
        {
            var raw=GenericPetProfiles.Raw(ability);Require(raw.SwarmRaw==0,"Current original demon profile changed");var actual=new GenericPetOwnerSurveyState(1,raw.SwarmRaw);
            var services=new GenericPetOwnerSurveyServices(_=>throw new InvalidOperationException("Swarm0read unit"),(_,_)=>throw new InvalidOperationException("Swarm0read monster"),
                _=>throw new InvalidOperationException("Swarm0read target"),(_,_,_)=>throw new InvalidOperationException("Swarm0validate target"),_=>throw new InvalidOperationException("Swarm0refresh"));
            GenericPetOwnerSurvey.Enumerate(1,actual,visit=>{foreach(int unit in new[]{1,2,0,3,2}){visit(unit);}},services);
            Require(actual.Visits==5&&actual.Targets.Count==0&&!WarlockCombat.Implemented(ability),"Original profile count-only survey or unfinished handler admission");
        }
    }
}
