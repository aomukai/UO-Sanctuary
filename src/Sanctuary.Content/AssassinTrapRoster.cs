// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record AssassinTrapEntry(uint Owner,uint Actor,long Order);
internal sealed record AssassinTrapSave(AssassinTrapEntry[] Entries,long NextOrder);
public sealed class AssassinTrapProfiles:GenericPersistence
{
    private AssassinTrapSave _snapshot;
    public AssassinTrapProfiles():base("SanctuaryAssassinTraps",156){}
    public override void Serialize(){_snapshot=AssassinTrapRoster.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer)
    {writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown assassin trap roster version");}
        AssassinTrapRoster.Restore(JsonSerializer.Deserialize<AssassinTrapSave>(reader.ReadString()));
    }
    public override void PostDeserialize()=>AssassinTrapRoster.Prune();
}

// The assassintrap pet type is one owner list, shared by blade and sentries.
// A registration is appended after creation succeeds; at capacity the oldest
// entry is removed first. No UO follower-slot or ordinary pet cap is substituted.
internal static class AssassinTrapRoster
{
    private static readonly Dictionary<uint,AssassinTrapEntry> Entries=new();
    private static long _nextOrder=1;
    public static void Configure()=>_ = new AssassinTrapProfiles();
    internal static AssassinTrapEntry[] For(PlayerMobile owner)
    {Prune();return Entries.Values.Where(x=>x.Owner==owner.Serial.Value).OrderBy(x=>x.Order).ToArray();}
    internal static void Register(PlayerMobile owner,BaseCreature actor,int maximum)
    {
        if(owner is not{Alive:true,Deleted:false} || actor is not{Alive:true,Deleted:false,Summoned:true}
            || actor.SummonMaster!=owner || maximum is <1 or >1000 || Entries.ContainsKey(actor.Serial.Value) || _nextOrder==long.MaxValue)
        {throw new InvalidOperationException("Invalid assassin trap registration");}
        var existing=For(owner);
        foreach(var old in existing.Take(Math.Max(0,existing.Length+1-maximum)))
        {Entries.Remove(old.Actor);World.FindMobile((Serial)old.Actor)?.Delete();}
        Entries.Add(actor.Serial.Value,new(owner.Serial.Value,actor.Serial.Value,_nextOrder++));
    }
    internal static void Forget(BaseCreature actor)=>Entries.Remove(actor.Serial.Value);
    internal static void Prune()
    {
        foreach(var entry in Entries.Values.ToArray())
        {
            if(World.FindMobile((Serial)entry.Actor) is not BaseCreature{Alive:true,Deleted:false,Summoned:true} actor
                || actor.SummonMaster?.Serial.Value!=entry.Owner || World.FindMobile((Serial)entry.Owner) is not PlayerMobile{Deleted:false})
            {Entries.Remove(entry.Actor);}
        }
    }
    internal static AssassinTrapSave Snapshot()
    {Prune();return new(Entries.Values.OrderBy(x=>x.Order).ToArray(),_nextOrder);}
    internal static void Restore(AssassinTrapSave saved)
    {
        if(saved?.Entries==null || saved.NextOrder<1){throw new InvalidDataException("Missing assassin trap roster");}
        var entries=new Dictionary<uint,AssassinTrapEntry>();var orders=new HashSet<long>();
        foreach(var entry in saved.Entries)
        {
            if(!((Serial)entry.Owner).IsMobile || !((Serial)entry.Actor).IsMobile || entry.Order<1 || entry.Order>=saved.NextOrder
                || !orders.Add(entry.Order) || !entries.TryAdd(entry.Actor,entry))
            {throw new InvalidDataException("Invalid saved assassin trap registration");}
        }
        Entries.Clear();foreach(var pair in entries){Entries.Add(pair.Key,pair.Value);}_nextOrder=saved.NextOrder;
    }
}
