// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class BladeAiChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeAiChecks));
    public static void Configure(){Register("blade-ai-check");Register("blade-ai-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Source Blade Sentinel AI decisions; no autonomous/playable skill",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-sentinel-source-probe.json")));int contexts=0,missing=0,ownerCases=0;
        foreach(var row in doc.RootElement.GetProperty("ai").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int creates=0,stored=0,prepares=0,fallback=0,walks=0,first=0,second=0;
            var stats=new AssassinTrapCreationStats(N(0),99,1,25600,100,35,100+12*(N(0)-1),[],[],2);
            var result=BladeSentinelAi.Think(new(N(1),N(7)!=0,N(2)!=0),1000,stats.DurationFrames,true,N(3)==392,N(6)!=0,new(100,200),new(108,208),
                ()=>{creates++;if(N(5)!=0){stored++;stats=AssassinTrapCreation.Arm(stats,N(4)!=0?80:35,N(4)!=0?11:2,N(4)!=0?17:3);}},
                ()=>prepares++,target=>{int goal=target==new Point2D(108,208)?1:0;Require(target==new Point2D(100,200) || goal==1,"Invented blade endpoint");if(walks==0){first=goal;}else{second=goal;}return ((N(8)>>walks++)&1)!=0;},
                ()=>{fallback++;return N(9)!=0;});
            var actual=new[]{result.State.Expiry,result.State.Forward?1:0,result.State.MissileAttempted?1:0,result.Dead?1:0,result.IdleFrames,creates,stored,stats.Attack-35,stats.AttackPercent-2,prepares,fallback,walks,first,second,0,0};
            for(int i=0;i<actual.Length;i++){Require(actual[i]==N(i+10),$"Blade AI context{contexts} field{i}: actual{actual[i]} expected{N(i+10)}");}
            Require(JsonSerializer.Serialize(result.State)==JsonSerializer.Serialize(JsonSerializer.Deserialize<BladeAiState>(JsonSerializer.Serialize(result.State))),"Blade AI state JSON round-trip");contexts++;
        }
        foreach(var row in doc.RootElement.GetProperty("missing").EnumerateArray())
        {
            int rank=row[1].GetInt32();var result=BladeSentinelAi.Think(new(),1000,100+12*(rank-1),false,true,true,new(100,200),new(108,208),
                ()=>throw new InvalidOperationException("Missing skill created missile"),()=>throw new InvalidOperationException("Missing skill set velocity"),_=>throw new InvalidOperationException("Missing skill walked"),()=>throw new InvalidOperationException("Missing skill followed owner"));
            Require(result.Dead && result.State==new BladeAiState(),"Missing skill did not request death before first-think duration");missing++;
        }
        using var modes=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-mode-source-probe.json")));
        int modeCases=0;
        foreach(var row in modes.RootElement.GetProperty("modes").EnumerateArray())
        {
            int bits=row.GetProperty("input").GetInt32(),calls=0;var paths=new System.Collections.Generic.List<int[]>();var parameters=new BladeWalkParameters().Set(15,0,20);
            bool Walk(int x,int y)
            {
                var take=parameters.TakeWalk();parameters=take.Remaining;
                return BladeWalkParameters.Calculate(take.Request,request=>{paths.Add([request.Path,request.Distance,x,y]);return ((bits>>calls++)&1)!=0;});
            }
            bool first=Walk(108,100);int second=first?-1:Walk(100,100)?1:0;
            Require((first?1:0)==row.GetProperty("result")[0].GetInt32() && second==row.GetProperty("result")[1].GetInt32(),"Source mode walk return values");
            Require(JsonSerializer.Serialize(paths)==row.GetProperty("paths").GetRawText() && parameters==new BladeWalkParameters(),"Consumed walk override/fallback trace");modeCases++;
        }
        foreach(var row in modes.RootElement.GetProperty("owner").EnumerateArray())
        {
            bool owner=row[0].GetInt32()!=0;ulong seed=SourceRandom.InitLow(row[1].GetInt32());
            var target=BladeSentinelAi.OwnerDestination(owner?new(110,112):new(100,100),owner?5:2,ref seed);
            Require(target==new Point2D(row[2].GetInt32(),row[3].GetInt32()) && seed==row[4].GetUInt64(),"Source owner fallback coordinates/RNG draw count");ownerCases++;
        }
        Require(contexts==7680 && missing==10 && ownerCases==512 && modeCases==8,"Incomplete Blade AI/mode/owner fixtures");
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"AI component enabled unfinished skill");
        Log.Information("Blade AI: {Contexts} complete source decisions, {Missing} missing-skill gates, {Modes} consumed-override traces and {Owners} exact owner RNG contexts pass; automatic mode/clock/missile remains separate",contexts,missing,modeCases,ownerCases);
    }
}
