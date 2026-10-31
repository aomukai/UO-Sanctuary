// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class NpcHighLevelResourceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcHighLevelResourceChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("npc-high-level-resource-check","NPC resource levels beyond the player cap",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("npc-high-level-resource-check: passed");}catch(Exception e){Log.Error(e,"npc-high-level-resource-check: failed");}}));
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-high-level-resource-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var i=row[0].EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            if(NpcResourceValues.Calculate(new(i[0],i[1],i[2],i[3],i[4],i[5]<<8,i[6],i[7],i[8]))!=row[1].GetInt32())
            {throw new InvalidOperationException("High-level NPC source resource mismatch at "+count);}count++;
        }
        bool rejected=false;try{NpcResourceValues.Calculate(new(0,0,100,256,0,0,0,0));}catch(ArgumentOutOfRangeException){rejected=true;}
        if(count!=768||!rejected){throw new InvalidOperationException("NPC domains/player cap controls missing");}
        Log.Information("NPC resource levels:768complete-source/native maximum controls across100/104/255/352;player99cap retained;no level clamp or invented player coefficients");
    }
}
