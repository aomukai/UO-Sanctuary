// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;
public static class PierceSeedChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PierceSeedChecks));
    private sealed class Never:Random{public override int Next(int n)=>throw new InvalidOperationException("Zero Pierce consumed a draw");}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pierce-seed-check","Real source seed/factory vectors and same-index fan correlation",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("pierce-seed-check: passed");}catch(Exception e){Log.Error(e,"pierce-seed-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Check()
    {
        var world=NpcWorldAllocation.Capture();string players=Json(PlayerItemProcs.Capture()),npcs=Json(NpcEquipmentRandom.Snapshot());
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pierce-seed-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int chance=checked(row[0].GetInt32()+row[1].GetInt32()),index=row[2].GetInt32();var actual=NamedProjectiles.SeededPierceBudget(chance,index);
            ulong? seed=row[5].ValueKind==JsonValueKind.Null?null:ulong.Parse(row[5].GetString());
            Require(actual.Budget==row[3].GetInt32()&&actual.Draws==row[4].GetInt32()&&actual.Seed==seed,"Complete seeded source factory differential: "+index+"/"+chance);
            Require(Enumerable.Range(0,24).All(_=>NamedProjectiles.SeededPierceBudget(chance,index)==actual),"Same-index fan consumed a continuing seed");count++;
        }
        bool refused=false;try{NamedProjectiles.SeededPierceBudget(-1,17);}catch(ArgumentOutOfRangeException){refused=true;}
        Require(count==936&&refused&&NamedProjectiles.PierceBudget(0,new Never())==0,"Seed coverage,negative admission or existing zero-draw contract");
        Require(NpcWorldAllocation.Capture()==world&&Json(PlayerItemProcs.Capture())==players&&Json(NpcEquipmentRandom.Snapshot())==npcs,"Local Pierce borrowed or reseeded live world/player/NPC streams");
    }
}
