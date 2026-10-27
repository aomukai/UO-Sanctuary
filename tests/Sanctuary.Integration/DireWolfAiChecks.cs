// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class DireWolfAiChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DireWolfAiChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("dire-wolf-ai-check","Fenris policy ordered services and corpse/owner memory",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("dire-wolf-ai-check: passed");}catch(Exception e){Log.Error(e,"dire-wolf-ai-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dire-wolf-ai-source-probe.json")));
        int compared=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            var data=row[1];int N(int i)=>data[i].GetInt32();
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                var requests=new List<SpiritWolfAiRequest>();int rolls=0;
                var common=new SpiritWolfAiContext(N(0)!=0,N(1),N(2)!=0,17,40,70,N(3)!=0,
                    N(4)!=0?10:0,N(5)!=0?20:0,N(6)!=0,N(7)!=0,N(8),N(9),N(11),N(15)!=0,N(16)!=0,
                    difficulty,N(10),5,10);
                // Source dwAiParam order is pursuit, corpse GUID, owner GUID.
                // The native value names these lanes rather than relying on
                // the legacy array layout.
                bool rageDraw=N(17)!=0 && N(18)==0;
                var services=new SpiritWolfAiServices(requests.Add,
                    (target,combat,flag,leash)=>{requests.Add(new(1,target,combat?1:0,flag,leash));return N(13)!=0;},
                    (kind,run,speed,flag)=>{requests.Add(new(5,kind,run?1:0,speed,flag));return N(12)!=0;},
                    ()=>
                {
                    rolls++;int parameter=rageDraw?2:1;rageDraw=false;
                    requests.Add(new(13,parameter,N(14)));return N(14);
                });
                var actual=DruidDireWolfAi.Think(new(common,N(17)!=0,N(18)!=0,N(19),77),new(services,()=>
                {requests.Add(new(12,17,10));return N(20)!=0?new DireWolfCorpse(30,N(21),N(22)!=0):null;}));
                var memory=row[2];var expected=new DireWolfAiMemory(memory[2].GetInt32(),memory[0].GetInt32(),memory[1].GetInt32());
                var actions=row[4].EnumerateArray().Select(x=>new SpiritWolfAiRequest(x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32(),x[3].GetInt32(),x[4].GetInt32()));
                Require(actual==expected && rolls==row[3].GetInt32() && requests.SequenceEqual(actions),"Complete Fenris policy/memory/service-order mismatch family "+row[0].GetInt32()+" context "+data.GetRawText()+" actual "+JsonSerializer.Serialize(new{actual,rolls,requests})+" expected "+row.GetRawText());
                compared++;
            }
        }
        Require(compared==3678,"Incomplete Fenris source fixture");
        Require(!DruidCombat.Implemented(CombatAbility.SummonDireWolf),"Prepared AI must not enable an unfinished learned summon");
        Log.Information("Dire Wolf AI:1226complete source contexts across3current difficulty parameter columns,3678native ordered-service/memory comparisons; no native actor,corpse consumer,rage or learned cast is claimed");
    }
}
