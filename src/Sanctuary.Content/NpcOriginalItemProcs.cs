// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NpcOriginalProcVisit(int Function,int Phase,uint Packed,int Result,ulong Before,ulong After);

// Full original item graph -> actual shared callbacks and protected spell
// wrappers. The world caller supplies allocation and any targetless path;
// the explicit original combat profile never fabricates a game seed or AI.
internal static class NpcOriginalItemProcs
{
    // Mandatory world allocation boundary. The host owns its persistent game
    // stream; absence refuses before ordinary allocation/HP,never reseeds.
    internal static Func<AmazonValkyrieBody,NpcItemMissileRequest,DateTime,NpcItemSpellAllocation> Allocator;
    internal static Func<AmazonValkyrieBody,Point3D> FirstPath;
    internal static Action<AmazonValkyrieBody,NpcOriginalProcVisit> Observed;
    internal static bool Active(BaseCreature actor)=>!World.Loading&&actor is AmazonValkyrieBody{Alive:true,Deleted:false} b
        &&b.Prepared?.EquipmentContactProfile==ValkyrieEquipmentContact.OriginalProfile;
    internal static GeneratedNpcStatGraph RequireHost(AmazonValkyrieBody actor)=>Host(actor,true);
    internal static GeneratedNpcStatGraph RequireRegistrationHost(AmazonValkyrieBody actor)=>Host(actor,false);
    private static GeneratedNpcStatGraph Host(AmazonValkyrieBody actor,bool world)
    {
        if(Allocator==null){throw new NotSupportedException("Original NPC contact needs its world missile allocation service");}
        var graph=world?Require(actor):RequireRegistration(actor);
        if(graph.Stats.Any(v=>v.Value>0&&v.Stat is 195 or 196 or 198 or 201&&((ushort)v.Packed>>6)==53)&&ChainLightning.OriginalNpcAllocator==null)
        {throw new NotSupportedException("Original Chain proc contact needs its world child allocation service");}
        return graph;
    }
    internal static PreparedMonsterDamagePacket Hosted(AmazonValkyrieBody actor,BaseCreature other,string phase,PreparedMonsterDamagePacket packet,
        DateTime at,Func<int,int> roll=null,NpcItemTarget retained=null)
    {
        _=RequireHost(actor);var allocate=Allocator;
        return Dispatch(actor,other,phase,packet,at,r=>allocate(actor,r,at),roll,retained,
            observed:v=>Observed?.Invoke(actor,v),readPath:()=>FirstPath?.Invoke(actor)
                ??throw new NotSupportedException("Missing original NPC path service"));
    }
    private static readonly Dictionary<uint,GeneratedNpcStatGraph> Frames=new();
    internal static int? Bonus(BaseCreature actor,string code)=>actor!=null&&Frames.TryGetValue(actor.Serial.Value,out var graph)
        ?ValkyrieEquipmentContact.Number(graph,code):null;

    internal static GeneratedNpcStatGraph Require(AmazonValkyrieBody actor)
    {
        if(actor is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile owner}
            ||!NpcItemSpellFlights.Attached(actor,owner,actor.Map?.MapID??-1)||NpcItemProcs.Get(actor)!=null)
        {throw new NotSupportedException("Original NPC callbacks require their attached original world actor,not event-only carriers");}
        return RequireRegistration(actor);
    }
    // Validates the saved original graph without granting combat,callbacks or
    // missile allocation to a parked/in-town/inactive actor.
    internal static GeneratedNpcStatGraph RequireRegistration(AmazonValkyrieBody actor)
    {
        if(actor is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false} owner}
            ||actor.Prepared?.Owner!=owner.Serial.Value||!actor.Permitted(owner)||NpcItemProcs.Get(actor)!=null)
        {throw new NotSupportedException("Original registration needs its living original body and recorded owner,not an event-only carrier");}
        _=ValkyrieEquipmentDamage.Passives(actor);_=NpcEquipmentRandom.Require(actor);
        var graph=actor.EquipmentNumbers;
        if(graph?.ItemEvents==null){throw new NotSupportedException("Original NPC callbacks need their full saved registration history");}
        foreach(var value in graph.Stats.Where(v=>v.Value!=0))
        {
            var bindings=ItemEventDefinitions.Bindings(value.Stat);if(bindings==null){continue;}
            foreach(var row in bindings.Where(e=>e.Event>0))
            {
                if(row.Function is 20 or 21)
                {
                    if(value.Stat is not(195 or 196 or 198 or 201)||(value.Packed&63)==0)
                    {throw new NotSupportedException("Original NPC proc needs its supported six-bit skill layer");}
                    _=NpcItemSpellDamage.Require((ushort)value.Packed>>6);
                }
                else if(row.Function is not(6 or 7 or 8 or 9 or 10 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 28 or 29))
                {throw new NotSupportedException("Original NPC registration needs its actual effect body:"+row.Function);}
            }
        }
        return graph;
    }
    internal static PreparedMonsterDamagePacket Dispatch(AmazonValkyrieBody actor,BaseCreature other,string phase,PreparedMonsterDamagePacket packet,
        DateTime at,Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,Func<int,int> roll=null,NpcItemTarget retained=null,
        Point3D? firstPathPoint=null,Action<NpcOriginalProcVisit> observed=null,Func<Point3D> readPath=null)
    {
        ArgumentNullException.ThrowIfNull(allocate);var graph=Require(actor);byte code=ItemEventDefinitions.Code(phase);
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-2000))
        {throw new NotSupportedException("Original NPC callback needs its actual UTC frame");}
        var target=retained??(other is{Alive:true,Deleted:false}?NpcItemTarget.Capture(other):null);
        if(retained!=null&&other!=null&&retained.Original!=other)
        {throw new NotSupportedException("Original NPC callback target changed identity");}
        if(target!=null){_=target.Point(actor.Map);}
        var services=new Services(actor,graph,target,packet,at,allocate,firstPathPoint,readPath);
        uint id=actor.Serial.Value;bool had=Frames.TryGetValue(id,out var previous);Frames[id]=graph;
        try{graph.ItemEvents.Trigger(code,(function,eventPhase,guid1,guid2)=>
        {
            ulong before=NpcEquipmentRandom.Require(actor).Seed;int result;
            if(function is 20 or 21){result=ItemProcCallbacks.Run(function,unchecked((uint)guid1),services);}
            // Flat physical return is owned once by PreparedFlatAttempt in the
            // ordinary damage producer. A registry is not another return hit.
            else if(function==6){result=0;}
            else{var current=services.Packet;result=NpcItemEventEffects.Run(function,eventPhase,actor,other,ref current,at,roll);services.Packet=current;}
            observed?.Invoke(new(function,eventPhase,unchecked((uint)guid1),result,before,NpcEquipmentRandom.Require(actor).Seed));
            return result;
        });}
        finally{if(had){Frames[id]=previous;}else{Frames.Remove(id);}}
        return services.Packet;
    }
    private sealed class Services(AmazonValkyrieBody actor,GeneratedNpcStatGraph graph,NpcItemTarget target,PreparedMonsterDamagePacket packet,
        DateTime at,Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,Point3D? path,Func<Point3D> readPath):ItemProcServices
    {
        internal PreparedMonsterDamagePacket Packet=packet;
        internal override bool HasSource=>actor is{Deleted:false};
        internal override bool HasTarget=>target!=null;
        internal override bool HasPacket=>Packet!=null;
        internal override uint HitFlags=>Packet.HitFlags;
        internal override ushort ResultFlags=>(ushort)Packet.ResultFlags;
        internal override int LevelBits=>6;
        internal override int Chance(int stat,int layer)=>graph.Values.Query(graph.Actor.Stats,EquipmentStatKey.Pack(stat,layer));
        internal override uint Roll()=>NpcEquipmentRandom.Raw(actor);
        internal override ItemProcSkill Skill(int id)=>new(NpcItemSpellDamage.Require(id).ItemTargetDo);
        internal override (int X,int Y) FirstPathPoint()
        {var p=path??readPath?.Invoke()??throw new NotSupportedException("Missing original NPC path service");return(p.X,p.Y);}
        internal override void CastTarget(bool targetAsCaster,int skill,int level,int flag)
        {
            if(targetAsCaster){throw new NotSupportedException("Original NPC target-as-caster proc needs its separate context");}
            NpcItemSpellCasts.CastOriginal(actor,target,skill,level,flag,at,allocate);
        }
        internal override void CastPosition(int skill,int level,int x,int y,int flag)
            =>NpcItemSpellCasts.CastOriginalPosition(actor,new(x,y,actor.Z),skill,level,flag,at,allocate);
    }
}
