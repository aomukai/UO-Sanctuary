// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record NpcItemChainPacket(NpcItemSpellSource Source,SlowMissilePayload Slow,ulong Seed,SourceStepTrace Steps,int VelocityFp8,int Ordinal=0,string Fault=null);
internal sealed record NpcChainRequest(long Cast,int Ordinal,uint Actor,uint Owner,uint Target,int Skill,int Rank,int Remaining,int Map,int Z,
    double X,double Y,int TargetX,int TargetY,int Flags,DateTime At);
internal readonly record struct NpcChainAllocation(bool Allocated,ulong Seed);

// Prepared original NPC do26/hit12 context. The actual allocator is a caller
// service at each attempted child,not a seed list consumed at root creation.
internal static class NpcItemChainFlights
{
    private static void Route()
    {
        _=NpcItemSpellDamage.Require(53);var row=SkillMechanics.For(CombatAbility.ChainLightning);int M(string key)=>NecromancerSummons.Source("chainlightning",key);
        if(row.Text("aurarangecalc")!="par1"||row.Number("Param1")!=20||M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=12||M("pSrvDmgFunc")!=0
            ||M("Vel")!=30||M("MaxVel")!=30||M("VelLev")!=0||M("Range")!=25||M("Size")!=2||M("CollideType")!=3||M("CollideKill")!=1
            ||M("LastCollide")!=1||M("NextHit")!=1||M("NextDelay")!=4||M("InitSteps")!=0||M("Activate")!=0||M("CanSlow")!=1
            ||M("SrcDamage")!=0||M("ToHit")!=0||M("Pierce")!=0||M("ReturnFire")!=1||M("GetHit")!=1||M("ResultFlags")!=4||M("HitFlags")!=0)
        {throw new NotSupportedException("Unqualified original NPC Chain Lightning route");}
    }
    internal static void Validate(ChainFlight f)
    {
        Route();var n=f?.OriginalNpc;var s=n?.Source;var p=s?.Packet;
        if(f==null||n==null||p==null||f.PlayerPacket!=null||f.StrikeContext!=null||f.NpcCaster!=0||f.NpcSeed!=0||f.NpcSlow!=SlowMissilePayload.Normal
            ||f.Cast<=0||!((Serial)f.Owner).IsMobile||!((Serial)s.Actor).IsMobile||f.Owner==s.Actor||s.Binding is not{Length:64}
            ||s.Binding.Any(c=>c is not(>='0'and<='9'or>='a'and<='f'))||p.Skill!=53||p.Missile!="chainlightning"||p.Element!="ltng"||p.ColdFrames!=0
            ||p.Rank is<1 or>63||p.Rank!=f.Rank||p.Emissions!=(p.Rank+25)/5||n.Ordinal<0||f.Remaining<1||n.Ordinal+f.Remaining!=p.Emissions
            ||p.Minimum256<0||p.Maximum256<p.Minimum256||f.Min256!=n.Slow.Damage256(p.Minimum256)||f.Max256!=n.Slow.Damage256(p.Maximum256)
            ||f.Frame is<0 or>=25||f.Map<0||f.Map>=Map.Maps.Length||Map.Maps[f.Map]==null||Map.Maps[f.Map]==Map.Internal||f.Z is<-128 or>127
            ||!double.IsFinite(f.X+f.Y+f.Vx+f.Vy)||f.X<=0||f.Y<=0||f.X>=Map.Maps[f.Map].Width||f.Y>=Map.Maps[f.Map].Height||Math.Abs(f.Vx)>.703125||Math.Abs(f.Vy)>.703125
            ||n.Slow.VelocityPercent is<0 or>100||n.Slow.DamagePercent is<0 or>100||n.VelocityFp8!=SlowMissilePayload.VelocityFp8(30,0,f.Rank,33,0,true,n.Slow.VelocityPercent)
            ||n.Steps?.Cells is not{Length:>=1 and<=10}||n.Steps.Cells.Any(c=>c.X<0||c.Y<0||c.X>=Map.Maps[f.Map].Width*2||c.Y>=Map.Maps[f.Map].Height*2)
            ||f.NextUtc.Kind!=DateTimeKind.Utc||f.UntilUtc.Kind!=DateTimeKind.Utc||f.NextUtc>DateTime.MaxValue.AddMilliseconds(-1000)
            ||f.UntilUtc-f.NextUtc!=TimeSpan.FromMilliseconds((24-f.Frame)*40)||f.Delays==null
            ||f.Delays.Any(d=>!((Serial)d.Target).IsMobile||d.UntilUtc.Kind!=DateTimeKind.Utc)||f.Delays.Select(d=>d.Target).Distinct().Count()!=f.Delays.Length
            ||f.AimTarget!=0&&!((Serial)f.AimTarget).IsMobile)
        {throw new InvalidDataException("Invalid original NPC chain context");}
    }
    private static NpcChainRequest Request(ChainFlight f,int ordinal,uint actor,uint target,double x,double y,Point3D aim,DateTime at)
        =>new(f.Cast,ordinal,actor,f.Owner,target,53,f.Rank,f.Remaining,f.Map,f.Z,x,y,aim.X,aim.Y,0x21,at);
    internal static ChainFlight Prepare(long cast,SkeletonGuardian body,Point3D aim,int rank,DateTime at,Func<NpcChainRequest,NpcChainAllocation> allocate)
    {
        ArgumentNullException.ThrowIfNull(allocate);Route();var owner=body?.ControlMaster as PlayerMobile;
        if(cast<=0||!NpcItemSpellFlights.Attached(body,owner,body?.Map?.MapID??-1)||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-2000)
            ||aim.X<=0||aim.Y<=0||aim.X>=body.Map.Width||aim.Y>=body.Map.Height||SpellHelper.IsTown(aim,owner))
        {throw new NotSupportedException("Original NPC chain needs its active source,aim and explicit UTC allocation context");}
        var request=new NpcItemMissileRequest(93,53,rank,body.X*2,body.Y*2,aim.X*2,aim.Y*2,33,0,0);
        var f=PrepareFactory(cast,body,request,at);if(f==null){return null;}
        var allocated=allocate(Request(f,0,body.Serial.Value,0,body.X,body.Y,aim,at));
        return allocated.Allocated?f with{OriginalNpc=f.OriginalNpc with{Seed=allocated.Seed}}:null;
    }
    // Shared ordinary/item root constructor in source subtile coordinates.
    // Returns a fully checked candidate before actual unit allocation. Item
    // execution keeps its private handle budget unset until do26/151 writes it.
    internal static ChainFlight PrepareFactory(long cast,SkeletonGuardian body,NpcItemMissileRequest request,DateTime at)
    {
        Route();var owner=body?.ControlMaster as PlayerMobile;
        if(cast<=0||!NpcItemSpellFlights.Attached(body,owner,body?.Map?.MapID??-1)||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-2000)
            ||request is not{Missile:93,Skill:53,Rank:>=1 and<=63,Velocity:0,ChargedOrdinal:-1}
            ||(request.Flags&~0x1000)!=33||request.AttackRating<0||(request.X&1)!=0||(request.Y&1)!=0)
        {throw new NotSupportedException("Original chain factory needs its qualified NPC source,request and UTC context");}
        double x=request.X/2.0,y=request.Y/2.0;
        if(x<=0||y<=0||x>=body.Map.Width||y>=body.Map.Height||request.TargetX==0||request.TargetY==0
            ||SpellHelper.IsTown(new Point3D((int)x,(int)y,body.Z),owner)) {return null;}
        int dx=request.TargetX-request.X,dy=request.TargetY-request.Y;
        if(Math.Abs((long)dx)>=100||Math.Abs((long)dy)>=100){return null;}
        _=NpcEquipmentRandom.Require(body);int rank=request.Rank;
        var packet=NpcItemSpellDamage.CaptureOriginal(body,53,rank);var slow=AmazonSlowMissiles.Capture(body,true);
        int velocity=SlowMissilePayload.VelocityFp8(30,0,rank,request.Flags,0,true,slow.VelocityPercent);
        if(velocity!=0&&dx==0&&dy==0){dx=dy=1;}
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);
        var f=new ChainFlight(cast,owner.Serial.Value,body.Map.MapID,rank,packet.Emissions,0,x,y,body.Z,vector.X,vector.Y,0,
            slow.Damage256(packet.Minimum256),slow.Damage256(packet.Maximum256),[],at.AddMilliseconds(1000),at.AddMilliseconds(40))
            {OriginalNpc=new(new(body.Serial.Value,OriginalNpcEquipment.Binding(body),packet),slow,0,new([new(request.X,request.Y)]),velocity)};
        Validate(f);return f;
    }
    private static ChainFlight Age(ChainFlight f,DateTime now,bool loaded)
    {
        int elapsed=loaded?(int)Math.Min(25-f.Frame,(now-f.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1):1;
        return f.Frame+elapsed>=25?null:f with{Frame=f.Frame+elapsed,NextUtc=f.NextUtc.AddMilliseconds(40L*elapsed)};
    }
    internal static ChainFlight Advance(ChainFlight f,DateTime now,bool? actions,bool loaded,Func<NpcChainRequest,NpcChainAllocation> allocate)
    {
        Validate(f);if(now.Kind!=DateTimeKind.Utc){throw new ArgumentException("Original chain clock needs UTC");}
        if(f.UntilUtc<=now){return null;}if(f.NextUtc>now){return f;}
        var n=f.OriginalNpc;var body=World.FindMobile((Serial)n.Source.Actor)as SkeletonGuardian;var owner=World.FindMobile((Serial)f.Owner)as PlayerMobile;
        if(loaded||n.Fault!=null||!NpcItemSpellFlights.Attached(body,owner,f.Map,n.Source.Binding)||!(actions??owner?.NetState is{Running:true}))
        {return Age(f,now,loaded);}
        var moved=ChainLightning.Trace(f,body,owner,n.Steps,n.VelocityFp8);
        if(moved.Removed){return null;}
        f=f with{OriginalNpc=n with{Steps=moved.Steps}};n=f.OriginalNpc;
        if(moved.First==null){return f with{X=moved.X,Y=moved.Y,Frame=f.Frame+1,NextUtc=f.NextUtc.AddMilliseconds(40)};}
        _=NpcItemSpellFlights.RequireTarget(owner,moved.First,n.Source,f.Map,f.NextUtc);
        var delays=f.Delays.Where(d=>d.UntilUtc>f.NextUtc).Append(new ChainDelay(moved.First.Serial.Value,f.NextUtc.AddMilliseconds(160))).ToArray();
        var contact=f with{Delays=delays};var next=f.Remaining>1?ChainLightning.NextTarget(body,owner,contact,moved.First,moved.X,moved.Y,f.NextUtc):null;
        ChainFlight child=null;
        if(next!=null)
        {
            _=NpcItemSpellFlights.RequireTarget(owner,next,n.Source,f.Map,f.NextUtc);
            if(allocate==null){throw new NotSupportedException("Original NPC chain child needs its actual per-hop allocator service before HP/RNG");}
            var packet=NpcItemSpellDamage.CaptureOriginal(body,53,f.Rank);var slow=AmazonSlowMissiles.Capture(body,true);
            int velocity=SlowMissilePayload.VelocityFp8(30,0,f.Rank,33,0,true,slow.VelocityPercent),dx=(int)Math.Round((next.X-moved.X)*2),dy=(int)Math.Round((next.Y-moved.Y)*2);if(dx==0&&dy==0){dx=dy=1;}
            var vector=MissileVectors.VelocityFp8(dx,dy,velocity);
            child=f with{Remaining=f.Remaining-1,AimTarget=next.Serial.Value,X=moved.X,Y=moved.Y,Vx=vector.X,Vy=vector.Y,Frame=0,Min256=slow.Damage256(packet.Minimum256),Max256=slow.Damage256(packet.Maximum256),
                Delays=delays,UntilUtc=f.NextUtc.AddMilliseconds(1000),NextUtc=f.NextUtc.AddMilliseconds(40),
                OriginalNpc=new(n.Source with{Packet=packet},slow,0,new([new((int)Math.Round(moved.X*2),(int)Math.Round(moved.Y*2))]),velocity,n.Ordinal+1)};
            Validate(child);var allocation=allocate(Request(child,child.OriginalNpc.Ordinal,body.Serial.Value,next.Serial.Value,moved.X,moved.Y,next.Location,f.NextUtc));
            child=allocation.Allocated?child with{OriginalNpc=child.OriginalNpc with{Seed=allocation.Seed}}:null;
        }
        // Hit12 creates a child before the current hit's ordinary damage phase.
        // Its rank/owner/stats never come from a player/Mage spell supplier.
        NpcItemSpellFlights.ImpactCaptured(owner,moved.First,n.Source,f.Map,f.Min256,f.Max256,n.Seed,f.NextUtc,out _);
        Effects.SendLocationEffect(new((int)Math.Round(moved.X),(int)Math.Round(moved.Y),f.Z),body.Map,0x3818,3,10,0x482,0); // Identified chain placeholder.
        return child;
    }
    internal static ChainFlight Suspend(ChainFlight f,string reason)
        =>f.Frame+1>=25?null:f with{Frame=f.Frame+1,NextUtc=f.NextUtc.AddMilliseconds(40),OriginalNpc=f.OriginalNpc with{Fault=reason}};
}
