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

public sealed class NpcItemProcProfiles:GenericPersistence
{
    public NpcItemProcProfiles():base("SanctuaryNpcItemProcs",191){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(NpcItemProcs.SnapshotJson);}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown NPC item proc version");}NpcItemProcs.Restore(JsonSerializer.Deserialize<NpcProcState[]>(reader.ReadString()));}
}

internal static class NpcItemProcs
{
    private static readonly Dictionary<uint,NpcItemProcGraph> Live=new();
    private static readonly Dictionary<uint,NpcProcState> Saved=new();
    private static string Cached="[]";
    internal static string SnapshotJson=>Volatile.Read(ref Cached);
    public static NpcItemProcProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    private static void Publish()=>Volatile.Write(ref Cached,JsonSerializer.Serialize(Saved.Values.OrderBy(s=>s.Actor).ToArray()));
    private static void Changed(NpcItemProcGraph graph)
    {if(Live.TryGetValue(graph.Actor,out var current)&&ReferenceEquals(current,graph)){Saved[graph.Actor]=graph.Capture();Publish();}}
    internal static bool Available(uint item,uint actor)=>!Saved.Values.Any(s=>s.Actor!=actor&&s.Items.Any(i=>i.Item==item));
    internal static NpcItemProcGraph Create(SkeletonGuardian actor)
    {
        if(Get(actor)!=null){throw new InvalidOperationException("NPC already has an item event graph");}
        var result=NpcItemProcGraph.Create(actor,Changed);Live.Add(actor.Serial.Value,result);Changed(result);return result;
    }
    internal static NpcItemProcGraph Get(SkeletonGuardian actor)
    {
        if(actor==null||actor.Deleted){return null;}
        if(Live.TryGetValue(actor.Serial.Value,out var live)){return live;}
        if(!Saved.TryGetValue(actor.Serial.Value,out var state)){return null;}
        var restored=NpcItemProcGraph.Restore(state,Changed);Live.Add(actor.Serial.Value,restored);Changed(restored);return restored;
    }
    internal static void Refresh(SkeletonGuardian actor,NpcCombatEvents legacy)=>Get(actor)?.Refresh(legacy);
    // The shared ordered damage receiver owns these phases. Do not infer a
    // qualified packet from native scalar Damage or dispatch a second copy
    // through the per-actor ordinary event adapter.
    internal static void RequireIncoming(Mobile attacker,BaseCreature recipient,PreparedMonsterDamagePacket packet)
    {
        if(recipient is not SkeletonGuardian actor||Get(actor)is not{} graph){return;}
        graph.Refresh(NpcCombatEvents.LegacyForSource(actor));
        if((packet.ResultFlags&4)!=0&&graph.IncomingRequired&&attacker is not BaseCreature{Alive:true,Deleted:false})
        {throw new NotSupportedException("NPC on-struck item curses need a living NPC attacker;player/removed-target casting is separate");}
    }
    internal static void Received(string phase,Mobile attacker,BaseCreature recipient,PreparedMonsterDamagePacket packet,DateTime at)
    {
        if(phase is not("damagedinmelee"or"damagedbymissile")){throw new InvalidOperationException("Unmapped incoming NPC proc phase");}
        if(recipient is not SkeletonGuardian actor||Get(actor)is not{} graph){return;}
        RequireIncoming(attacker,recipient,packet);
        graph.Dispatch(phase,packet,actor,attacker as BaseCreature,at,null);
    }
    internal static void Forget(SkeletonGuardian actor)
    {Live.Remove(actor.Serial.Value);if(Saved.Remove(actor.Serial.Value)){Publish();}}
    internal static void Restore(NpcProcState[] states)
    {
        if(states==null||states.Any(s=>s==null))
        {throw new InvalidDataException("Invalid NPC proc ownership snapshot");}
        foreach(var state in states){NpcItemProcGraph.ValidateSnapshot(state);}
        if(states.Select(s=>s.Actor).Distinct().Count()!=states.Length
            ||states.SelectMany(s=>s.Items).Select(i=>i.Item).Distinct().Count()!=states.Sum(s=>s.Items.Length))
        {throw new InvalidDataException("Invalid NPC proc ownership snapshot");}
        Live.Clear();Saved.Clear();foreach(var state in states){Saved.Add(state.Actor,state);}Publish();
    }
}
