// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
namespace Sanctuary;

internal sealed record CreeperHead(long Id,int Rank,double OriginX,double OriginY,double X,double Y,
    int Velocity256,Point2D[] Path,int Next,int Remaining){public int Z{get;init;}}
internal sealed record CreeperMat(long Id,int Rank,int X,int Y,int Total,int Remaining,uint LastCollision=0)
{public int Z{get;init;}public CreeperMatPoison Poison{get;init;}}
internal sealed record CreeperFields(CreeperHead[] Heads,CreeperMat[] Mats,long NextId=0,int Contacts=0,uint LastVictim=0)
{
    public string Profile{get;init;}="charged-motion-elapsed-age-contact-v2";
}

// Internal motion/contact engine. A supplied packet initializer/delivery pair
// adds poison; old capture-only fields keep their null packets without replay.
// Tick ordering, geometry and selection are explicit world adapter services.
internal static class DruidCreeperFields
{
    private static CreeperFields Current(CreeperFields s)=>s.Profile=="charged-motion-elapsed-age-contact-v2"?s:s with{Profile="charged-motion-elapsed-age-contact-v2"};
    internal static CreeperFields Empty()=>new([],[]);
    // Old snapshots already store countdowns, so migration changes only the
    // interpretation/profile. Do not recreate children or replay past hits.
    internal static CreeperFields Copy(CreeperFields s)=>s with{Profile="charged-motion-elapsed-age-contact-v2",
        Heads=s.Heads.Select(h=>h with{Path=[..h.Path]}).ToArray(),Mats=[..s.Mats]};
    internal static int Lifetime(int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        // Evaluate the CURRENT expression as an expression, never as legacy
        // wRange's integer prefix. Executable parser equivalence is unverified.
        var values=new SkillValues(SkillMechanics.For(CombatAbility.PoisonCreeper),_=>rank,_=>0,1);
        int frames=NecromancerSummons.Formula("plague vines trail","Range",values);
        if(frames!=100+rank*25){throw new NotSupportedException("Unsupported Creeper trail lifetime");}
        return frames;
    }
    private static void Route()
    {
        int H(string key)=>NecromancerSummons.Source("plague vines",key);
        int M(string key)=>NecromancerSummons.Source("plague vines trail",key);
        if(H("Range")!=20 || H("Vel")!=7 || H("VelLev")!=0 || H("Accel")!=0 || H("CollideType")!=0
            || H("CanSlow")!=1 || M("pSrvDoFunc")!=1 || M("CollideType")!=3 || M("Vel")!=0
            || M("Size")!=3 || M("NextHit")!=0 || M("NextDelay")!=0)
        {throw new NotSupportedException("Unsupported Creeper field route");}
    }
    internal static CreeperFields Launch(CreeperFields state,int rank,CreeperHeadRequest[] requests,int velocityPercent=100,int z=0)
    {
        Route();Validate(state);state=Current(state);ArgumentNullException.ThrowIfNull(requests);
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        if(requests.Length>24){throw new NotSupportedException("Too many Creeper heads");}
        if(requests.Length==0){return Copy(state);}
        int velocity=SlowMissilePayload.VelocityFp8(7,0,rank,3,0,true,velocityPercent);
        if(velocity==0){throw new NotSupportedException("Zero-speed Creeper charged path is not established");}
        var heads=new List<CreeperHead>(state.Heads);long id=state.NextId;
        foreach(var r in requests)
        {
            if(r.X<=0 || r.Y<=0 || r.X>=65535 || r.Y>=65535 || r.InitializerIndex is <0 or >23
                || Math.Abs(r.OffsetX)+Math.Abs(r.OffsetY)!=1){throw new NotSupportedException("Unsupported Creeper head request");}
            // Flags3 adds the cardinal delta to the explicit target-origin.
            // Charged init seeds from absolute aim X PLUS callback ordinal.
            var init=DruidEmitterPatterns.InitializeCharged(20,r.X+r.OffsetX,r.InitializerIndex);
            var path=ChargedBolts.Path(init.Seed,ChargedBolts.Heading(r.OffsetX,r.OffsetY),init.Distance);
            heads.Add(new(checked(++id),rank,r.X/2.0,r.Y/2.0,r.X/2.0,r.Y/2.0,velocity,path,1,init.Frames){Z=z});
        }
        var result=state with{Heads=heads.ToArray(),NextId=id};Validate(result);return result;
    }
    internal static CreeperFields Inactive(CreeperFields state,long frames)
    {
        Validate(state);state=Current(state);if(frames<0){throw new ArgumentOutOfRangeException(nameof(frames));}
        return state with{Heads=state.Heads.Where(h=>h.Remaining>frames).Select(h=>h with{Remaining=h.Remaining-(int)frames}).ToArray(),
            Mats=state.Mats.Where(m=>m.Remaining>frames).Select(m=>m with{Remaining=m.Remaining-(int)frames}).ToArray()};
    }
    internal static CreeperFields Step(CreeperFields state,bool actions,Func<double,double,int,bool> blocked,
        Func<CreeperMat,uint> recipient,Func<uint,bool> hostile,Func<CreeperHead,bool> createMat=null,bool hasOwner=true,
        Func<CreeperMat,CreeperMat> initializeMat=null,Func<CreeperMat,uint,CreeperMat> deliver=null)
    {
        Validate(state);state=Current(state);Route();ArgumentNullException.ThrowIfNull(blocked);ArgumentNullException.ThrowIfNull(recipient);ArgumentNullException.ThrowIfNull(hostile);
        var heads=new List<CreeperHead>();var mats=new List<CreeperMat>();var created=new List<CreeperMat>();
        long id=state.NextId;int contacts=state.Contacts;uint victim=state.LastVictim;
        // Existing fields run before children first advance on the next tick.
        // Global D2R unit-list order is not claimed by this local ordering.
        foreach(var mat in state.Mats.OrderByDescending(m=>m.Id))
        {
            int remaining=mat.Remaining-1;if(remaining<=0){continue;}
            var next=mat with{Remaining=remaining};
            if(actions)
            {
                uint target=recipient(next);
                var contact=DruidCreeperVines.Contact(mat.Total,mat.Total-remaining,mat.LastCollision,target,hasOwner,target!=0&&hostile(target),false);
                next=next with{LastCollision=contact.LastCollision};
                if(contact.Damage){contacts=checked(contacts+1);victim=target;next=deliver?.Invoke(next,target)??next;}
            }
            mats.Add(next);
        }
        foreach(var head in state.Heads.OrderByDescending(h=>h.Id))
        {
            int remaining=head.Remaining-1;
            if(!actions){if(remaining>0){heads.Add(head with{Remaining=remaining});}continue;}
            // Complete do26 emits before generic movement/decrement/removal.
            if(DruidCreeperVines.EmitsTrail(20-head.Remaining) && (createMat?.Invoke(head)??true))
            {
                int total=Lifetime(head.Rank);
                var mat=new CreeperMat(checked(++id),head.Rank,(int)Math.Floor(head.X*2+.5),(int)Math.Floor(head.Y*2+.5),total,total){Z=head.Z};
                created.Add(initializeMat?.Invoke(mat)??mat);
            }
            var moved=ChargedBolts.Advance(head.Path,0,0,head.X-head.OriginX,head.Y-head.OriginY,head.Next,
                SkillGeometry.Tiles(head.Velocity256/4096.0));
            double x=moved.X+head.OriginX,y=moved.Y+head.OriginY;
            var trace=SourceMissileSteps.Advance(head.X,head.Y,x-head.X,y-head.Y,new([]));
            bool wall=trace.Cells.Any(cell=>blocked(cell.X/2.0,cell.Y/2.0,head.Z));
            if(remaining>0 && !wall && moved.Next<head.Path.Length)
            {heads.Add(head with{X=x,Y=y,Next=moved.Next,Remaining=remaining});}
        }
        mats.AddRange(created);var result=state with{Heads=heads.ToArray(),Mats=mats.ToArray(),NextId=id,Contacts=contacts,LastVictim=victim};Validate(result);return result;
    }
    internal static void Validate(CreeperFields s)
    {
        if(s?.Heads==null || s.Mats==null || s.NextId<0 || s.Contacts<0 || s.Profile is not("charged-motion-current-range-contact-capture-v1" or "charged-motion-elapsed-age-contact-v2")
            || s.Heads.Length>512 || s.Mats.Length>4096 || (s.Contacts==0)!=(s.LastVictim==0))
        {throw new InvalidDataException("Invalid Creeper field state");}
        var ids=new HashSet<long>();
        bool Id(long id)=>id>0&&id<=s.NextId&&ids.Add(id);
        foreach(var h in s.Heads)
        {
            if(h==null || !Id(h.Id) || h.Rank is <1 or >255 || h.Remaining is <1 or >20 || h.Velocity256 is <1 or >1344
                || h.Path?.Length!=11 || h.Next<1 || h.Next>=h.Path.Length || h.Z is <-128 or >127 || !double.IsFinite(h.X+h.Y+h.OriginX+h.OriginY)
                || h.OriginX<=0 || h.OriginY<=0 || h.OriginX>=32767.5 || h.OriginY>=32767.5 || h.Path[0]!=new Point2D(0,0)
                || Math.Abs(h.X-h.OriginX)>10 || Math.Abs(h.Y-h.OriginY)>10
                || h.Path.Zip(h.Path.Skip(1)).Any(p=>Math.Abs((long)p.First.X-p.Second.X)>1||Math.Abs((long)p.First.Y-p.Second.Y)>1||p.First==p.Second))
            {throw new InvalidDataException("Invalid Creeper head");}
        }
        foreach(var m in s.Mats)
        {
            if(m==null || !Id(m.Id) || m.Rank is <1 or >255 || m.Total!=Lifetime(m.Rank) || m.Remaining<1 || m.Remaining>m.Total
                || m.X<=0 || m.Y<=0 || m.X>=65535 || m.Y>=65535 || m.Z is <-128 or >127){throw new InvalidDataException("Invalid Creeper mat");}
            if(m.Poison!=null){DruidCreeperPoison.Validate(m.Poison);}
        }
    }
}
