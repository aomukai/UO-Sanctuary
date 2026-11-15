// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record MiasmaChainState(long Id,uint Owner,int Map,int Z,uint Target,double TargetX,double TargetY,double HeadX,double HeadY,
    bool Maker,bool Attached,MiasmaChainBirth Birth,ulong Seed,SourceStepTrace Trace,DateTime CreatedUtc,DateTime NextUtc,DateTime NextStampUtc,DateTime UntilUtc);
internal sealed record MiasmaChainStamp(long Id,long Cast,uint Owner,int Map,int Z,double X,double Y,double EndX,double EndY,
    MiasmaChainBirth Birth,ulong Seed,DateTime CreatedUtc,DateTime NextUtc);
internal readonly record struct MiasmaChainAdvance(int Updates,int DirectDamaged,int Stamps,int Clouds,int Damage256,string Fault=null);

// Approved711maker/712head+link/713stamp mapping. Cloud696reuses the
// saved Miasma scanner/lease/damage service on the existing world clock.
// Modern do43/45/hit41bodies remain explicitly adapted,not recovered.
internal static class MiasmaChainWorld
{
    internal static bool Approved=>true;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MiasmaChainWorld));
    private static ImmutableDictionary<long,MiasmaChainState> Links=ImmutableDictionary<long,MiasmaChainState>.Empty;
    private static ImmutableDictionary<long,MiasmaChainStamp> Makers=ImmutableDictionary<long,MiasmaChainStamp>.Empty;
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    internal static MiasmaChainState[] Snapshot()=>Links.Values.OrderBy(n=>n.Id).ToArray();
    internal static MiasmaChainStamp[] Stamps()=>Makers.Values.OrderBy(n=>n.Id).ToArray();
    internal static MiasmaChainState[] For(Mobile p)=>Snapshot().Where(n=>n.Owner==p.Serial.Value).ToArray();
    internal static void Clear(Mobile p)
    {foreach(var n in For(p)){Links=Links.Remove(n.Id);}foreach(var n in Stamps().Where(n=>n.Owner==p.Serial.Value)){Makers=Makers.Remove(n.Id);}}
    private static PlayerMobile Owner(uint id)=>World.FindMobile((Serial)id)is PlayerMobile{Alive:true,Deleted:false} p
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock?p:null;
    private static Point3D Point(double x,double y,int z)=>new((int)Math.Round(x),(int)Math.Round(y),z);
    internal static bool Aim(PlayerMobile p,Serial selected,ref Point3D point)
    {
        if(p?.Map==null||Owner(p.Serial.Value)!=p||!MiasmaWorld.Fighting(p,p.Map.MapID)||!ShapeRestrictions.Allowed(p,CombatAbility.MiasmaChain)){return false;}
        if(selected!=Serial.Zero&&(World.FindMobile(selected)is not BaseCreature t||!MiasmaWorld.Enemy(p,t,p.Z))){return false;}
        return LobProjectiles.Aim(p,selected,ref point)&&MiasmaWorld.LinkLength(p.X,p.Y,point.X,point.Y)<=40;
    }
    internal static long ReleasePrepared(PlayerMobile p,Serial selected,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at<=DateTime.UnixEpoch){throw new ArgumentException("Chain clock must be UTC");}
        if(!Aim(p,selected,ref point)){return 0;}var birth=MiasmaChainBirths.Capture(p);
        if(!NecromancerEffects.CanPay(p,birth.Numbers.Mana256)){return 0;}
        if(selected!=Serial.Zero){TaintedFireBallMissiles.RequireOrdinaryRecipient(p,World.FindMobile(selected));}
        MiasmaChainRequest request=null;var result=MiasmaChainLaunch.Run(new(p.Serial.Value,0,true,p.X*2,p.Y*2,399,birth.Numbers.Rank,0),
            new(()=>true,()=>MiasmaChainLaunch.PinnedMaker(SkillMechanics.Values(p,CombatAbility.MiasmaChain)),()=>(true,point.X*2,point.Y*2),_=>{},r=>{request=r;return true;}));
        if(result.Result!=1||request==null){return 0;}long id=MiasmaWorld.NextIdentity;
        var n=new MiasmaChainState(id,p.Serial.Value,p.Map.MapID,p.Z,selected.Value,point.X,point.Y,p.X,p.Y,true,false,birth,
            SourceRandom.InitLow(SeedLow()),new([new(p.X*2,p.Y*2)]),at,at.AddMilliseconds(40),at.AddMilliseconds(40),at.AddMilliseconds(40+birth.Numbers.LinkFrames*40L));Validate(n);
        if(!NecromancerEffects.Pay(p,birth.Numbers.Mana256)){return 0;}
        foreach(var old in For(p).OrderBy(n=>n.CreatedUtc).ThenBy(n=>n.Id).Take(Math.Max(0,For(p).Length-birth.Numbers.MaximumChains+1)))
        {Links=Links.Remove(old.Id);} // Paid oldest-first replacement;already emitted clouds keep their own clocks.
        MiasmaWorld.CommitIdentity(id);Links=Links.Add(id,n);p.PlaySound(0x1E5);return id; // Identified placeholder.
    }
    private static bool ClearSegment(Map map,double x,double y,double endX,double endY,int z)
    {
        int count=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(endX-x),Math.Abs(endY-y))*2));var previous=Point(x,y,z);
        for(int i=0;i<=count;i++)
        {
            var point=Point(x+(endX-x)*i/count,y+(endY-y)*i/count,z);
            if(point.X<0||point.Y<0||point.X>=map.Width||point.Y>=map.Height||LobProjectiles.Barrier(map,point)
                ||!map.LineOfSight(new Point3D(previous.X,previous.Y,z+12),new Point3D(point.X,point.Y,z+12))){return false;}previous=point;
        }
        return true;
    }
    private static int Head(MiasmaChainState n,PlayerMobile p)
    {
        double aimX=n.TargetX,aimY=n.TargetY;
        if(World.FindMobile((Serial)n.Target)is BaseCreature{Alive:true,Deleted:false} target&&MiasmaWorld.Enemy(p,target,n.Z))
        {aimX=target.X;aimY=target.Y;}
        if(n.Attached)
        {Links=Links.SetItem(n.Id,n with{TargetX=aimX,TargetY=aimY,HeadX=aimX,HeadY=aimY,NextUtc=n.NextUtc.AddMilliseconds(40)});return 0;}
        int speed=SlowMissilePayload.VelocityFp8(35,0,n.Birth.Numbers.Rank,33,0,true,n.Birth.Slow.VelocityPercent);
        var v=MissileVectors.VelocityFp8((int)Math.Round((aimX-n.HeadX)*2),(int)Math.Round((aimY-n.HeadY)*2),speed);
        double dx=aimX-n.HeadX,dy=aimY-n.HeadY;bool arrived=dx*dx+dy*dy<=v.X*v.X+v.Y*v.Y;
        double x=arrived?aimX:n.HeadX+v.X,y=arrived?aimY:n.HeadY+v.Y;
        if(!ClearSegment(p.Map,n.HeadX,n.HeadY,x,y,n.Z)){Links=Links.Remove(n.Id);return 0;}
        var trace=SourceMissileSteps.Advance(n.HeadX,n.HeadY,x-n.HeadX,y-n.HeadY,n.Trace);BaseCreature victim=null;
        foreach(var cell in trace.Cells)
        {
            foreach(var t in SkillCombat.NearbySnapshot<BaseCreature>(p.Map,Point(cell.X/2.0,cell.Y/2.0,n.Z),3).Where(t=>MiasmaWorld.Enemy(p,t,n.Z)).OrderBy(t=>t.Serial.Value))
            {if(CombatHitboxes.SourceTouches(2,CreatureProfiles.For(t).Traits.Size??2,cell.X-t.X*2,cell.Y-t.Y*2)){victim=t;break;}}
            if(victim!=null){break;}
        }
        ulong seed=n.Seed;int loss=0;
        if(victim!=null)
        {
            TaintedFireBallMissiles.RequireOrdinaryRecipient(p,victim);x=victim.X;y=victim.Y;arrived=true;
            // Commit attachment before any damage/death/reaction callback.
            Links=Links.SetItem(n.Id,n with{HeadX=x,HeadY=y,TargetX=x,TargetY=y,Target=victim.Serial.Value,Attached=true,Trace=trace,NextUtc=n.NextUtc.AddMilliseconds(40)});
            int raw=SourceSpellPacketRoll.Range(ref seed,MiasmaBoltBirths.Enhanced(n.Birth.Numbers.DirectMinimum256,n.Birth.MagicMastery),MiasmaBoltBirths.Enhanced(n.Birth.Numbers.DirectMaximum256,n.Birth.MagicMastery));
            Links=Links.SetItem(n.Id,Links[n.Id]with{Seed=seed});SorceressEffects.MissileHit(p,victim,"miasmachains");
            if(!ClassAvoidance.Missile(victim,reaction:true))
            {loss=MiasmaWorld.Deliver(p,victim,DruidMoltenBoulder.Potion(n.Birth.Modifiers,new(0,0,0,0,0,Magic:n.Birth.Slow.Damage256(raw)/256.0)),true,n.NextUtc);}
        }
        else{Links=Links.SetItem(n.Id,n with{HeadX=x,HeadY=y,TargetX=aimX,TargetY=aimY,Attached=arrived,Trace=trace,NextUtc=n.NextUtc.AddMilliseconds(40)});}
        return loss;
    }
    private static (double X,double Y) End(MiasmaChainState n,PlayerMobile p)
    {
        double x=n.HeadX,y=n.HeadY,length=MiasmaWorld.LinkLength(p.X,p.Y,x,y);
        if(length>n.Birth.Numbers.LinkLength){double fraction=n.Birth.Numbers.LinkLength/length;x=p.X+(x-p.X)*fraction;y=p.Y+(y-p.Y)*fraction;}return(x,y);
    }
    internal static void Tick(DateTime at)
    {
        if(World.Loading||!Approved){return;}
        foreach(uint owner in Links.Values.Select(n=>n.Owner).Concat(Makers.Values.Select(n=>n.Owner)).Distinct().OrderBy(n=>n).ToArray())
        {
            var result=TickPrepared(at,onlyOwner:owner);
            if(result.Fault!=null){Log.Warning("Miasma Chain stopped an unsupported link for {Owner}: {Reason}",owner,result.Fault);}
        }
    }
    internal static MiasmaChainAdvance TickPrepared(DateTime at,bool? actions=null,uint? onlyOwner=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Chain clock must be UTC");}int updates=0,direct=0,stamps=0,clouds=0,damage=0;
        while(true)
        {
            var n=Links.Values.Where(n=>(!onlyOwner.HasValue||n.Owner==onlyOwner)&&n.NextUtc<=at).OrderBy(n=>n.NextUtc).ThenBy(n=>n.Id).FirstOrDefault();if(n==null){break;}
            var p=Owner(n.Owner);if(p==null||at>=n.UntilUtc){Links=Links.Remove(n.Id);continue;}
            if(!MiasmaWorld.Fighting(p,n.Map,actions)||Math.Abs(p.Z-n.Z)>=16)
            {
                long frames=(at-n.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
                var stamp=n.NextStampUtc;if(stamp<=at){stamp=stamp.AddMilliseconds(((at-stamp).Ticks/TimeSpan.FromMilliseconds(500).Ticks+1)*500);}
                Links=Links.SetItem(n.Id,n with{Maker=false,NextUtc=n.NextUtc.AddMilliseconds(frames*40),NextStampUtc=stamp});continue;
            }
            try
            {
                updates++;
                if(n.Maker){Links=Links.SetItem(n.Id,n with{Maker=false,NextUtc=n.NextUtc.AddMilliseconds(40)});}
                else{int loss=Head(n,p);if(loss>0){direct++;}damage=(int)Math.Min(int.MaxValue,(long)damage+loss);}
            }
            catch(NotSupportedException ex){Links=Links.Remove(n.Id);return new(updates,direct,stamps,clouds,damage,ex.Message);}
        }
        foreach(var n in Snapshot().Where(n=>(!onlyOwner.HasValue||n.Owner==onlyOwner)&&!n.Maker&&n.NextStampUtc<=at))
        {
            var p=Owner(n.Owner);if(p==null){Links=Links.Remove(n.Id);continue;}if(!MiasmaWorld.Fighting(p,n.Map,actions)||Math.Abs(p.Z-n.Z)>=16){continue;}
            var end=End(n,p);if(!ClearSegment(p.Map,p.X,p.Y,end.X,end.Y,n.Z)){Links=Links.Remove(n.Id);continue;}
            long id=MiasmaWorld.NextIdentity;ulong seed=n.Seed;ulong child=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));
            var stamp=new MiasmaChainStamp(id,n.Id,n.Owner,n.Map,n.Z,p.X,p.Y,end.X,end.Y,n.Birth,child,at,at.AddMilliseconds(40));Validate(stamp);
            MiasmaWorld.CommitIdentity(id);Makers=Makers.Add(id,stamp);long skipped=(at-n.NextStampUtc).Ticks/TimeSpan.FromMilliseconds(500).Ticks+1;
            Links=Links.SetItem(n.Id,n with{Seed=seed,NextStampUtc=n.NextStampUtc.AddMilliseconds(skipped*500)});stamps++;
            Effects.SendLocationEffect(Point(end.X,end.Y,n.Z),p.Map,0x37C4,2,10,0x48D,0); // Identified linked-end placeholder.
        }
        foreach(var n in Stamps().Where(n=>(!onlyOwner.HasValue||n.Owner==onlyOwner)&&n.NextUtc<=at))
        {
            var p=Owner(n.Owner);Makers=Makers.Remove(n.Id);
            if(p==null||at>=n.NextUtc.AddMilliseconds(n.Birth.Numbers.Shape.LifetimeFrames*40L)){continue;}
            MiasmaWorld.ChainCloud(n);clouds++;
        }
        return new(updates,direct,stamps,clouds,damage);
    }
    private static void Position(int map,int z,double x,double y)
    {
        if(map<0||map>=Map.Maps.Length||Map.Maps[map]==null||Map.Maps[map]==Map.Internal||z is <-128 or >127
            ||!double.IsFinite(x)||!double.IsFinite(y)||x<0||y<0||x>=Map.Maps[map].Width||y>=Map.Maps[map].Height)
        {throw new InvalidDataException("Invalid Chain position");}
    }
    internal static void Validate(MiasmaChainState n)
    {
        if(n==null||n.Id<=0||!((Serial)n.Owner).IsMobile||n.Target!=0&&!((Serial)n.Target).IsMobile||n.Seed==0||n.Trace?.Cells is not{Length:>=1 and <=10}
            ||n.CreatedUtc.Kind!=DateTimeKind.Utc||n.CreatedUtc<=DateTime.UnixEpoch||n.NextUtc.Kind!=DateTimeKind.Utc||n.NextStampUtc.Kind!=DateTimeKind.Utc||n.UntilUtc.Kind!=DateTimeKind.Utc
            ||n.NextUtc<n.CreatedUtc||n.NextUtc>n.UntilUtc||n.NextStampUtc<n.CreatedUtc||n.NextStampUtc>n.UntilUtc||n.Maker&&n.Attached)
        {throw new InvalidDataException("Invalid Chain state");}
        MiasmaChainBirths.Validate(n.Birth);Position(n.Map,n.Z,n.TargetX,n.TargetY);Position(n.Map,n.Z,n.HeadX,n.HeadY);
        if(n.UntilUtc!=n.CreatedUtc.AddMilliseconds(40+n.Birth.Numbers.LinkFrames*40L)){throw new InvalidDataException("Invalid Chain expiry");}
    }
    internal static void Validate(MiasmaChainStamp n)
    {
        if(n==null||n.Id<=0||n.Cast<=0||n.Cast>=n.Id||!((Serial)n.Owner).IsMobile||n.Seed==0||n.CreatedUtc.Kind!=DateTimeKind.Utc||n.CreatedUtc<=DateTime.UnixEpoch||n.NextUtc.Kind!=DateTimeKind.Utc||n.NextUtc!=n.CreatedUtc.AddMilliseconds(40))
        {throw new InvalidDataException("Invalid Chain cloud maker");}
        MiasmaChainBirths.Validate(n.Birth);Position(n.Map,n.Z,n.X,n.Y);Position(n.Map,n.Z,n.EndX,n.EndY);
        if(MiasmaWorld.LinkLength(n.X,n.Y,n.EndX,n.EndY)>n.Birth.Numbers.LinkLength+.0001){throw new InvalidDataException("Invalid Chain stamp length");}
    }
    internal static void ValidateSnapshot(MiasmaChainState[] links,MiasmaChainStamp[] makers,long next,IEnumerable<long> nodeIds)
    {
        var ids=nodeIds.ToHashSet();foreach(var n in links){Validate(n);if(n.Id>next||!ids.Add(n.Id)){throw new InvalidDataException("Duplicate/future Chain ID");}}
        foreach(var n in makers){Validate(n);if(n.Id>next||!ids.Add(n.Id)){throw new InvalidDataException("Duplicate/future Chain maker ID");}}
    }
    internal static void RestoreValidated(MiasmaChainState[] links,MiasmaChainStamp[] makers)
    {Links=links.ToImmutableDictionary(n=>n.Id);Makers=makers.ToImmutableDictionary(n=>n.Id);}
}
