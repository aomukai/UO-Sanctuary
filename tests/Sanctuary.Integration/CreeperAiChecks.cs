// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class CreeperAiChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperAiChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("creeper-ai-check","Complete source Vines outer AI through declared services",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("creeper-ai-check: passed");}catch(Exception e){Log.Error(e,"creeper-ai-check: failed");}}));
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.PoisonCreeper),"Policy enabled unfinished learned Creeper");
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"creeper-ai-source-probe.json")));
        int count=0;
        foreach(MonsterDifficulty difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        foreach(var row in document.RootElement.EnumerateArray())
        {
            var input=row[0];int N(int index)=>input[index].GetInt32();var actual=new List<CreeperAiRequest>();
            int scans=0,poisonQueries=0,resistanceQueries=0;
            void Request(CreeperAiRequest request)=>actual.Add(request);
            bool Common(int target,bool fighting,int flag,int leash)
            {Request(new(CreeperAiKind.CommonMove,target,fighting?1:0,flag,leash));return N(4)!=0;}
            bool Move(int mode,bool run,int speed,int flag)
            {Request(new(CreeperAiKind.PetMove,mode,run?1:0,speed,flag));return N(2)!=0;}
            CreeperAiTarget Find(){scans++;return new(N(5)==0?0:3,N(5)==1?19:N(5)==2?20:0,N(6)!=0);}
            bool Poisoned(int target){Require(target==3,"State query on missing/filtered target");poisonQueries++;return N(7)!=0;}
            int Resistance(int target){Require(target==3,"Resistance query on missing/filtered target");resistanceQueries++;return N(8);}
            bool Use(CreeperAiRequest request){Request(request);return N(12)!=0;}
            var state=DruidCreeperAi.Initialize(new(777,N(11)));
            var actualState=DruidCreeperAi.Think(new(N(0)!=0,N(1),N(3)!=0,N(11)+N(10),N(9),8,difficulty),state,
                new(Request,Common,Move,Find,Poisoned,Resistance,Use));
            var expected=row[2].EnumerateArray().Select(a=>new CreeperAiRequest((CreeperAiKind)a[0].GetInt32(),
                a[1].GetInt32(),a[2].GetInt32(),a[3].GetInt32(),a[4].GetInt32(),a[5].GetInt32())).ToArray();
            Require(actualState.Parameter0==row[1][0].GetInt32() && actualState.LastSkillFrame==row[1][1].GetInt32()
                && actual.SequenceEqual(expected),$"Creeper source context{count} {difficulty}: "+input+" actual="+JsonSerializer.Serialize(actual)+" expected="+row[2]);
            bool early=N(0)==0 || N(1)>=35&&N(2)!=0 || N(3)!=0;
            Require(scans==(early?0:1),"Target scan was moved across owner/warp/town gates");
            Require(resistanceQueries<=poisonQueries && (N(7)==0 || resistanceQueries==0),"Source poison short-circuit lost");
            Require(state==new CreeperAiState(0,N(11)),"Decision mutated caller's saved state");count++;
        }
        Require(count==5619,"Incomplete Creeper source/difficulty matrix");
        bool rejected=false;
        try{DruidCreeperAi.Think(new(true,0,false,0,294,8),new(0,1),new(_=>{},(_,_,_,_)=>false,(_,_,_,_)=>false,()=>default,_=>false,_=>0,_=>false));}
        catch(ArgumentException){rejected=true;}
        Require(rejected,"Unverified game-frame reversal/wrap was silently accepted");
        rejected=false;
        try{DruidCreeperAi.Think(new(true,0,false,int.MaxValue,294,8),new(0,int.MaxValue-1),
            new(_=>{},(_,_,_,_)=>false,(_,_,_,_)=>false,()=>new(3,0,true),_=>false,_=>0,_=>false));}
        catch(NotSupportedException){rejected=true;}
        Require(rejected,"Unverified cooldown-sum overflow was silently accepted");
        Log.Information("Creeper AI:1873complete source contexts replayed across3current difficulty parameter rows,5619native comparisons; ordered requests/remembered attempt frame, lazy scan and poison short-circuit pass; actual state/movement/mats and learned cast remain pending");
    }
}
