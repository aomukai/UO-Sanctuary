// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class GrizzlyAiChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrizzlyAiChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("grizzly-ai-check","Complete prepared AI112 policy; native body/sequence remain separate",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("grizzly-ai-check: passed");}catch(Exception e){Log.Error(e,"grizzly-ai-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.SummonGrizzly),"Grizzly exposed before actor/sequence acceptance");
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"grizzly-ai-source-probe.json")));
        int compared=0,sequences=0,ordinary=0,walks=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[1][i].GetInt32();
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                var context=new SpiritWolfAiContext(N(0)!=0,N(1),N(2)!=0,17,40,70,false,N(3)!=0?10:0,N(4)!=0?20:0,
                    N(5)!=0,N(6)!=0,N(7),N(8),N(9),false,N(12)!=0,difficulty,0,N(13),N(14));
                var actual=new List<SpiritWolfAiRequest>();int rolls=0;
                var services=new GrizzlyAiServices(actual.Add,(mode,run,speed,flag)=>
                    {actual.Add(new(5,mode,run?1:0,speed,flag));return N(10)!=0;},
                    index=>{actual.Add(new(13,index,N(11)));rolls++;return N(11);});
                int remembered=DruidGrizzlyAi.Think(context,services);
                var expected=row[4].EnumerateArray().Select(x=>new SpiritWolfAiRequest(x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32(),x[3].GetInt32(),x[4].GetInt32())).ToArray();
                Require(actual.SequenceEqual(expected)&&rolls==row[3].GetInt32()&&remembered==row[2][2].GetInt32(),
                    "Complete AI112 ordered requests/rolls/remembered owner, family "+row[0].GetInt32()+" context "+row[1].GetRawText());
                sequences+=actual.Count(x=>x.Kind==15);ordinary+=actual.Count(x=>x.Kind==6);walks+=actual.Count(x=>x.Kind==14);compared++;
            }
        }
        Require(compared==2016&&sequences>0&&ordinary>0&&walks>0,"Incomplete AI112 source comparison");
        var c=new SpiritWolfAiContext(true,0,true,17,0,0,false,10,0,true,true,0,0,1,false,true);
        var s=new GrizzlyAiServices(_=>{},(_,_,_,_)=>false,_=>100);
        bool rejected=false;try{DruidGrizzlyAi.Think(c,s);}catch(NotSupportedException){rejected=true;}
        Require(rejected,"Out-of-domain chance result accepted");
        rejected=false;try{DruidGrizzlyAi.Think(c with{Difficulty=(MonsterDifficulty)3},s);}catch(ArgumentException){rejected=true;}
        Require(rejected,"Unmapped difficulty accepted");
        Log.Information("Grizzly AI112:2016native ordered-service/owner-memory comparisons match672complete source contexts across3difficulty columns; walk/sequence/ordinary/follow thresholds remain distinct. No native actor,sequence execution,save or learned handler claimed");
    }
}
