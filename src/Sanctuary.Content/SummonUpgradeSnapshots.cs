// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record SummonUpgradeSnapshot(uint Actor,uint Owner,CombatAbility Ability,int HardRank);

public sealed class SummonUpgradeProfiles:GenericPersistence
{
    public SummonUpgradeProfiles():base("SanctuarySummonUpgrades",192){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(SummonUpgradeSnapshots.SnapshotJson);}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown summon upgrade snapshot version");}SummonUpgradeSnapshots.Restore(JsonSerializer.Deserialize<SummonUpgradeSnapshot[]>(reader.ReadString()));}
}

// Optional supplement for birth records lacking hard-point metadata. Historical
// actors without an entry retain their legacy upgrade rule until resummoned;
// never invent their birth hard points from a soft-inclusive effective rank.
internal static class SummonUpgradeSnapshots
{
    private static readonly Dictionary<uint,SummonUpgradeSnapshot> Saved=new();
    private static string Cached="[]";
    internal static string SnapshotJson=>Volatile.Read(ref Cached);
    public static SummonUpgradeProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    private static void Publish()=>Volatile.Write(ref Cached,JsonSerializer.Serialize(Saved.Values.OrderBy(s=>s.Actor).ToArray()));
    private static void Validate(SummonUpgradeSnapshot state)
    {
        if(state==null||state.Actor==0||state.Owner==0||!((Serial)state.Actor).IsMobile||!((Serial)state.Owner).IsMobile
            ||state.HardRank is <0 or >20||!SkillMechanics.All.Any(r=>r.Ability==state.Ability&&r.Text("summon").Length>0))
        {throw new InvalidDataException("Invalid summon hard-point snapshot");}
    }
    internal static SummonUpgradeSnapshot State(Mobile actor)=>actor==null?null:Saved.GetValueOrDefault(actor.Serial.Value);
    internal static void Capture(PlayerMobile owner,SkeletonGuardian actor,CombatAbility ability,int hardRank)
    {
        if(owner is not{Deleted:false}||actor is not{Alive:true,Deleted:false}||actor.ControlMaster!=owner
            ||actor.SourceMonsterId!=SkillMechanics.For(ability).Text("summon"))
        {throw new InvalidOperationException("Summon hard points require their original actor and owner");}
        var state=new SummonUpgradeSnapshot(actor.Serial.Value,owner.Serial.Value,ability,hardRank);Validate(state);
        if(!Saved.TryAdd(state.Actor,state)){throw new InvalidOperationException("Summon birth hard points already captured");}Publish();
    }
    internal static int HardRank(Mobile actor,CombatAbility ability)
    {
        if(actor is not{Deleted:false}){return 0;}
        if(State(actor)is{} state)
        {
            if(state.Ability!=ability){throw new NotSupportedException("Summon upgrade snapshot species mismatch");}
            return state.HardRank;
        }
        // Explicit historical compatibility, not a claimed birth snapshot.
        return actor is BaseCreature{ControlMaster:PlayerMobile{Deleted:false} owner}
            ?ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(ability).TalentId):0;
    }
    internal static void Forget(Mobile actor){if(actor!=null&&Saved.Remove(actor.Serial.Value)){Publish();}}
    internal static void Restore(SummonUpgradeSnapshot[] states)
    {
        if(states==null){throw new InvalidDataException("Missing summon upgrade snapshots");}
        foreach(var state in states){Validate(state);}
        if(states.Select(s=>s.Actor).Distinct().Count()!=states.Length){throw new InvalidDataException("Duplicate summon upgrade actor");}
        Saved.Clear();foreach(var state in states){Saved.Add(state.Actor,state);}Publish();
    }
}
