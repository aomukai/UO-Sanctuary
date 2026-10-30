// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class ReviveSpeciesChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveSpeciesChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Pinned Revive species gates and approved lifetime metadata",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("revive-species-check");Register("revive-species-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"revive-species-rules-data-probe.json")));
        int rows=0,allowed=0,classes=0;
        foreach(var raw in doc.RootElement.GetProperty("secondary").EnumerateArray())
        {
            var row=ReviveSpecies.Row(raw.GetProperty("id").GetInt32());
            Require(row!=null&&row.Id==rows&&row.Name==raw.GetProperty("name").GetString()&&row.Allowed==raw.GetProperty("allowed").GetBoolean(),"Compiled secondary Revive flag differs");
            rows++;if(row.Allowed){allowed++;}
        }
        foreach(var raw in doc.RootElement.GetProperty("classes").EnumerateArray())
        {
            int id=raw[0].GetInt32();bool? expected=raw[2].ValueKind==JsonValueKind.Null?null:raw[2].GetBoolean();
            Require(ReviveSpecies.ForClass(id)==expected,"Primary-to-secondary Revive permission differs:"+id);classes++;
        }
        foreach(int invalid in new[]{-1,751,int.MaxValue}){Require(ReviveSpecies.ForClass(invalid)==null,"Unknown species treated as eligible/ineligible instead of missing");}
        foreach(int invalid in new[]{-1,623,int.MaxValue}){Require(ReviveSpecies.Row(invalid)==null,"Invalid secondary row accepted");}
        string fixture=Path.Join(Core.BaseDirectory,"source-corpse-identity-fixtures.json");
        if(!File.Exists(fixture)){throw new InvalidOperationException("Saved original corpse fixtures are required");}
        using var saved=JsonDocument.Parse(File.ReadAllText(fixture));int corpses=0;
        foreach(var row in saved.RootElement.GetProperty("Rows").EnumerateArray())
        {
            int original=checked((int)row.GetProperty("BaseLife").GetProperty("OriginalMonsterSerial").GetUInt32());
            var unit=SourceCorpseIdentity.Find(original);Require(unit!=null&&unit.SpeciesAllowsRevive==ReviveSpecies.ForClass(unit.ClassId),"Saved corpse bypassed source species permission");corpses++;
        }
        var policy=ReviveSpecies.Lifetime;
        Require(policy is{MaximumAgeFrames:null,RemoveOnReviveDeath:true,RemoveOnNecromancerDeath:true,RemoveOnReplacement:true},"Approved no-expiration Revive policy lost");
        Require(rows==623&&allowed==391&&classes==751&&corpses==2,"Incomplete Revive species coverage");
        Require(!NecromancerCombat.Implemented(CombatAbility.Revive),"Metadata enabled incomplete Revive actor");
        Log.Information("Revive species:623compiled secondary flags,391species permissions,751primary links,6missing-row boundaries and2saved original corpses match;approved no-expiration metadata loaded;full eligibility,in-place actor and lifecycle remain gated");
    }
}
