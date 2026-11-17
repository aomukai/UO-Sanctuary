// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record PlayerPierceIndex(uint Owner,uint Index);
public sealed class PlayerPierceProfiles:GenericPersistence
{
    private PlayerPierceIndex[] Saved=[];
    public PlayerPierceProfiles():base("SanctuaryPlayerPierce",258){}
    public override void Serialize(){Saved=PlayerPierceActions.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(Saved));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown player Pierce index version");}PlayerPierceActions.Restore(JsonSerializer.Deserialize<PlayerPierceIndex[]>(reader.ReadString()));}
}

// Explicit candidate for the new-protocol owner-index mapping. Saved counter
// rows never enroll an owner;online admission requires a separate approval.
internal static class PlayerPierceActions
{
    internal static bool Approved=>false;
    private static Dictionary<uint,PlayerPierceIndex> Rows=new();
    private static readonly HashSet<uint> Candidates=[];
    public static void Configure()=>_=new PlayerPierceProfiles();
    internal static uint Index(PlayerMobile owner)=>Rows.GetValueOrDefault(owner.Serial.Value)?.Index??0;
    internal static PlayerPierceIndex[] Snapshot()
    {
        foreach(uint id in Rows.Keys.ToArray()){if(World.FindMobile((Serial)id)is not PlayerMobile{Deleted:false}){Rows.Remove(id);}}
        return Rows.Values.OrderBy(x=>x.Owner).ToArray();
    }
    internal static void Restore(PlayerPierceIndex[] rows)
    {
        if(rows==null){throw new InvalidDataException("Missing player Pierce index rows");}
        var next=new Dictionary<uint,PlayerPierceIndex>();
        foreach(var row in rows)
        {if(row==null||!((Serial)row.Owner).IsMobile||!next.TryAdd(row.Owner,row)){throw new InvalidDataException("Invalid player Pierce index");}}
        Rows=next;
    }
    private sealed class Proof(PlayerMobile owner):IDisposable
    {private bool Done;public void Dispose(){if(!Done){Done=true;Candidates.Remove(owner.Serial.Value);}}}
    internal static IDisposable Candidate(PlayerMobile owner)
    {
        if(owner is not{Deleted:false}||!Candidates.Add(owner.Serial.Value)){throw new InvalidOperationException("Pierce candidate needs its distinct actual owner");}
        return new Proof(owner);
    }
    internal static Ticket Capture(PlayerMobile owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if(!(Approved||Candidates.Contains(owner.Serial.Value))){return null;}
        if(owner is not{Alive:true,Deleted:false}){throw new NotSupportedException("Pierce action lost its living shooter");}
        return new(owner.Serial.Value,Index(owner));
    }
    internal sealed class Ticket(uint owner,uint before)
    {
        internal uint Index{get;}=unchecked(before+1);
        private bool Committed;
        internal int Budget(int chance)=>NamedProjectiles.SeededPierceBudget(chance,unchecked((int)Index)).Budget;
        internal void Commit()
        {
            if(Committed){return;}
            if(World.FindMobile((Serial)owner)is not PlayerMobile{Deleted:false} player||PlayerPierceActions.Index(player)!=before)
            {throw new InvalidOperationException("Stale Pierce release cannot overwrite another action");}
            Rows[owner]=new(owner,Index);Committed=true;
        }
    }
    internal static int Budget(Ticket ticket,int chance,Random fallback)=>ticket?.Budget(chance)??NamedProjectiles.PierceBudget(chance,fallback);
}
