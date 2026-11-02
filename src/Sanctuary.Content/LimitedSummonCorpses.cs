// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record LimitedCorpseRules(string ReferenceVersion,int Capacity,string[] Sources);
internal interface ILimitedSummonCorpse { string CorpseSource{get;} bool SourceDead{get;} }
internal sealed record LimitedCorpseEntry(uint Actor,string Source,long Order);
internal sealed record LimitedCorpseSave(LimitedCorpseEntry[] Entries,long NextOrder);
public sealed class LimitedCorpseProfiles:GenericPersistence
{
    private LimitedCorpseSave _snapshot;
    public LimitedCorpseProfiles():base("SanctuaryLimitedSummonCorpses",227){}
    public override void Serialize(){_snapshot=LimitedSummonCorpses.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown limited corpse pool version");}LimitedSummonCorpses.Restore(JsonSerializer.Deserialize<LimitedCorpseSave>(reader.ReadString()));}
    public override void PostDeserialize()=>LimitedSummonCorpses.Prune();
}

// The shipped guide defines a shared count bound, not an expiry timer. Callers
// must supply a completed source death; this does not guess DT/DD insertion or
// replace native/other-class death handlers. Retain the original registered NPC.
internal static class LimitedSummonCorpses
{
    private static Dictionary<uint,LimitedCorpseEntry> _entries=new();
    private static HashSet<string> _sources;
    private static int _capacity;
    private static long _nextOrder=1;
    public static void Configure()
    {
        var rules=JsonSerializer.Deserialize<LimitedCorpseRules>(File.ReadAllText(Path.Join(Core.BaseDirectory,"Data","Sanctuary","Balance","limited-summon-corpses.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        if(rules is not{ReferenceVersion:"3.3.93847",Capacity:50,Sources:{Length:15}} || rules.Sources.Any(string.IsNullOrWhiteSpace) || rules.Sources.Distinct(StringComparer.Ordinal).Count()!=15)
        {throw new InvalidDataException("Invalid current limited summon corpse rules");}
        _capacity=rules.Capacity;_sources=new(rules.Sources,StringComparer.Ordinal);_ =new LimitedCorpseProfiles();
    }
    internal static bool Contains(Mobile actor)=>actor!=null&&_entries.ContainsKey(actor.Serial.Value);
    internal static void Forget(Mobile actor){if(actor!=null){_entries.Remove(actor.Serial.Value);}}
    internal static void Retain(BaseCreature actor)
    {
        if(actor is not{Deleted:false,Alive:false} || actor is not ILimitedSummonCorpse{SourceDead:true} corpse
            || !_sources.Contains(corpse.CorpseSource) || !actor.Serial.IsMobile)
        {throw new InvalidOperationException("Limited pool requires the original completed dead summon");}
        Prune();if(_entries.TryGetValue(actor.Serial.Value,out var existing))
        {if(existing.Source!=corpse.CorpseSource){throw new InvalidDataException("Corpse source changed");}return;}
        if(_nextOrder==long.MaxValue){throw new InvalidOperationException("Limited corpse sequence exhausted");}
        _entries.Add(actor.Serial.Value,new(actor.Serial.Value,corpse.CorpseSource,_nextOrder++));
        while(_entries.Count>_capacity)
        {var oldest=_entries.Values.MinBy(x=>x.Order);_entries.Remove(oldest.Actor);World.FindMobile((Serial)oldest.Actor)?.Delete();}
    }
    internal static void Prune()
    {
        foreach(var entry in _entries.Values.ToArray())
        {
            if(World.FindMobile((Serial)entry.Actor) is not BaseCreature{Deleted:false,Alive:false} actor
                || actor is not ILimitedSummonCorpse{SourceDead:true} corpse || corpse.CorpseSource!=entry.Source)
            {_entries.Remove(entry.Actor);}
        }
    }
    internal static LimitedCorpseSave Snapshot()=>new(_entries.Values.OrderBy(x=>x.Order).ToArray(),_nextOrder);
    internal static void Restore(LimitedCorpseSave saved)
    {
        if(saved?.Entries==null || saved.Entries.Length>_capacity || saved.NextOrder<1){throw new InvalidDataException("Missing or oversized limited corpse pool");}
        var candidate=new Dictionary<uint,LimitedCorpseEntry>();var orders=new HashSet<long>();
        foreach(var entry in saved.Entries)
        {
            if(entry==null || !((Serial)entry.Actor).IsMobile || !_sources.Contains(entry.Source??"")
                || entry.Order<1 || entry.Order>=saved.NextOrder || !orders.Add(entry.Order) || !candidate.TryAdd(entry.Actor,entry))
            {throw new InvalidDataException("Invalid limited corpse pool entry");}
        }
        _entries=candidate;_nextOrder=saved.NextOrder;
    }
}
