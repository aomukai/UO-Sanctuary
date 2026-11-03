// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record GrimWardBodyRow(int Id,string Name,int Period,int Dispatch);
internal sealed record GrimWardPulseResult(int CollisionResult,bool Dispatched,int Candidates,int Applied);

// Bind the accepted body do14/do30 composition to actual native recipients.
// The controller supplies total/current frames, collision and formula lookup.
// No owner lookup, global clock, child insertion or snapshot policy is guessed.
internal static class GrimWardWorldPulse
{
    private static readonly Dictionary<int,GrimWardBodyRow> Rows=new();
    public static void Configure()
    {
        foreach(var row in BalanceCatalog.Read<GrimWardBodyRow>("grim-ward-bodies"))
        {
            if(row.Id is not(250 or 253 or 256)||row.Period!=6||row.Dispatch!=30||!Rows.TryAdd(row.Id,row))
            {throw new InvalidDataException("Unsupported pinned Ward body row");}
        }
        if(Rows.Count!=3){throw new InvalidDataException("Missing pinned Ward body rows");}
    }
    internal static GrimWardPulseResult Step(Item body,PlayerMobile owner,int missileClass,int rank,int total,int current,
        Func<SkillValues> formulaContext,Func<int> collision,DateTime at)
    {
        if(body is not{Deleted:false}||owner is not{Alive:true,Deleted:false}||body.Map==Map.Internal||owner.Map!=body.Map
            ||rank is <1 or >255||total<1||current<1||current>total||!Rows.ContainsKey(missileClass))
        {throw new NotSupportedException("Prepared Ward pulse requires an active mapped body,owner,rank and explicit frame state");}
        ArgumentNullException.ThrowIfNull(formulaContext);ArgumentNullException.ThrowIfNull(collision);
        bool dispatched=false;int candidates=0,applied=0;
        int serial=unchecked((int)body.Serial.Value);
        var unit=new GrimWardSourceUnit(serial,3,missileClass,body.X*2,body.Y*2,150,rank);
        var services=new GrimWardLifecycleServices(null,null,null,null,null,null,null,null,null,null,null,
            id=>Rows.TryGetValue(id,out var row)?new(-1,0,row.Period,row.Dispatch):null,null,null,
            (creator,fn,skill,skillRank)=>
            {
                if(creator!=serial||fn!=30||skill!=150||skillRank!=rank)
                {throw new NotSupportedException("Ward body dispatcher changed source identity");}
                var context=formulaContext();
                if(context?.Ability!=CombatAbility.GrimWard||context.Rank!=rank)
                {throw new NotSupportedException("Ward body requires its explicit current formula context");}
                var packet=GrimWardCurseOperation.Packet(context,SummonDifficulties.For(owner.Map));
                var targets=new List<BaseCreature>();
                foreach(var target in body.Map.GetMobilesInRange<BaseCreature>(body.Location,(int)Math.Ceiling(SkillGeometry.Tiles(packet.Radius))))
                {
                    // Shared curse geometry/floor projection. Exact source
                    // room traversal/ties and client scale remain port limits.
                    if(AmazonInnerSight.InRadius(target.X-body.X,target.Y-body.Y,packet.Radius)&&Math.Abs(target.Z-body.Z)<16
                        &&ClassCombat.IsHostile(owner,target)){targets.Add(target);}
                }
                targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));candidates=targets.Count;
                foreach(var target in targets){if(GrimWardCurseTransport.ApplyPacket(owner,body,target,packet,at)){applied++;}}
                dispatched=true;
            },id=>id==serial?collision():throw new NotSupportedException("Ward collision lost body identity"));
        int result=GrimWardLifecycle.Tick(unit,GrimWardLifecycle.Elapsed(total,current),services);
        return new(result,dispatched,candidates,applied);
    }
}
