// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class LeapGeometryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LeapGeometryChecks));
    private static readonly int[][] Masks=[[15,15,15],[0,15,15],[0,0,15],[0,0,0],[14,15,15],[13,15,15],[11,15,15],[7,15,15]];
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("leap-geometry-check","Differential pinned source Leap target geometry",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("leap-geometry-check: passed");}catch(Exception ex){Log.Error(ex,"leap-geometry-check: failed");}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Check()
    {
        Require(!BarbarianCombat.Implemented(CombatAbility.Leap),"Geometry foundation enabled an incomplete flight handler");
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"leap-source-probe.json")));
        int count=0;
        foreach(var row in source.RootElement.GetProperty("targetCases").EnumerateArray())
        {
            if(row[0].GetInt32()==0){continue;}
            count++;
            int dx=row[1].GetInt32(),dy=row[2].GetInt32(),range=row[3].GetInt32(),scenario=row[4].GetInt32(),attempt=-1;
            var trace=new List<LeapCell>();
            LeapCell? Free(LeapCell cell)
            {
                trace.Add(cell);attempt++;int mask=Masks[scenario][attempt];
                if((mask&8)==0){cell=new(cell.X+40,cell.Y+40);}
                return (mask&1)!=0?cell:null;
            }
            bool result=LeapGeometry.Find(new(100,100),new(100+dx,100+dy),range,Free,
                _=>(Masks[scenario][attempt]&2)!=0,_=>(Masks[scenario][attempt]&4)!=0,out var destination);
            Require(result==(row[5].GetInt32()!=0) && destination==new LeapCell(row[6].GetInt32(),row[7].GetInt32())
                && trace.Count==row[8].GetInt32(),$"Leap source destination/call count at case{count}");
            for(int i=0;i<trace.Count;i++)
            {Require(trace[i]==new LeapCell(row[9+i*2].GetInt32(),row[10+i*2].GetInt32()),$"Leap perpendicular/free-cell order at case{count}");}
        }
        Require(count==3240,"Incomplete compiled Leap geometry fixture");
        for(int dx=-40;dx<=40;dx++)for(int dy=-40;dy<=40;dy++)for(int size=0;size<=4;size++)
        {
            int x=Math.Abs(Math.Abs(dx)-size),y=Math.Abs(Math.Abs(dy)-size);
            Require(SourceTerror.Distance(dx,dy,size)==Math.Max(x,y)+Math.Min(x,y)/2,"Shared distance changed Terror size adjustment");
        }
        Log.Information("Leap native differential target cases: {Count}; existing Terror distance cases: {Terror}",count,81*81*5);
    }
}
