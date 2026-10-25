// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record PlagueCloud(long Id,uint Owner,int Map,double X,double Y,int Z,double Vx,double Vy,int Rank,
    WeaponMissileDamage Damage,int Frame,int Frames,uint LastCollision,DateTime NextUtc,SourceStepTrace SourceSteps);

public static class PlagueJavelins
{
    private static readonly Dictionary<long,PlagueCloud> _clouds=new();
    private static long _next;
    internal static FanRay[] Pattern()
    {
        int[] xs=[0,1,2,2,2,2,2,1,0,-1,-2,-2,-2,-2,-2,-1],ys=[2,2,2,1,0,-1,-2,-2,-2,-2,-2,-1,0,1,2,2];
        var rays=new List<FanRay>();int main=Math.Max(1,NecromancerSummons.Source("plaguejavelin","sHitPar2")),sub=NecromancerSummons.Source("plaguejavelin","sHitPar1");
        for(int i=0;i<16;i+=main){rays.Add(new(xs[i],ys[i],0x1F,0));}
        if(sub>0){for(int i=0;i<15;i+=sub){rays.Add(new(xs[i+1],ys[i+1],0x1F,0));}}return rays.ToArray();
    }
    internal static void Burst(PlayerMobile owner,WeaponFlight parent,Mobile target,DateTime at)
    {
        if(parent.Ability!=CombatAbility.PlagueJavelin){return;}
        // Full hit02 uses the collider as origin, including on an AR miss.
        double x=target?.X??Math.Floor(parent.X*2+.5)/2,y=target?.Y??Math.Floor(parent.Y*2+.5)/2;int z=target?.Z??parent.Z;
        if(parent.Map<0 || parent.Map>=Map.Maps.Length || Map.Maps[parent.Map] is not{} map || x<0 || y<0 || x>=map.Width || y>=map.Height){return;}
        int frames=NecromancerSummons.Source("plaguejavcloud","Range")+NecromancerSummons.Source("plaguejavelin","sHitPar3")*(NecromancerSummons.Source("plaguejavcloud","SubStop")-NecromancerSummons.Source("plaguejavcloud","SubStart"));
        var pattern=Pattern();int inner=(16+Math.Max(1,NecromancerSummons.Source("plaguejavelin","sHitPar2"))-1)/Math.Max(1,NecromancerSummons.Source("plaguejavelin","sHitPar2"));
        foreach(var (ray,index) in pattern.Select((r,i)=>(r,i)))
        {
            // Flags0x10 keeps override Param<<7 in FP8; the shared vector
            // factory applies75percent. Param2/4 correspond to row velocities1/2.
            int supplied=NecromancerSummons.Source("plaguejavcloud",index<inner?"Param1":"Param2")<<7;
            int velocity=SlowMissilePayload.VelocityFp8(0,0,parent.SkillRank,0x1F,supplied,false,100);
            var v=MissileVectors.VelocityFp8(ray.X,ray.Y,velocity);long id=++_next;
            _clouds.Add(id,new(id,owner.Serial.Value,parent.Map,x,y,z,v.X,v.Y,parent.SkillRank,
                PoisonJavelinPackets.Cloud(owner,parent.SkillRank,CombatAbility.PlagueJavelin),0,frames,0,at.AddMilliseconds(40),new([new((int)(x*2),(int)(y*2))])));
        }
    }
    internal static Mobile Recipient(PlayerMobile owner,PlagueCloud cloud)
    {
        foreach(var cell in cloud.SourceSteps.Cells)
        {
            var point=new Point3D((int)Math.Round(cell.X/2.0),(int)Math.Round(cell.Y/2.0),cloud.Z);
            foreach(var target in owner.Map.GetMobilesInRange<Mobile>(point,3))
            {
                if(target.Serial.Value==cloud.LastCollision){continue;}
                int size=target is BaseCreature c?CreatureProfiles.For(c).Traits.Size??2:2;
                if(Math.Abs(target.Z-cloud.Z)<16 && CombatHitboxes.SourceTouches(2,size,cell.X-target.X*2,cell.Y-target.Y*2)
                    && ClassCombat.IsHostile(owner,target) && !SpellHelper.IsTown(target.Location,owner)){return target;}
            }
        }
        return null;
    }
    internal static void Tick(DateTime at)
    {
        var queue=new PriorityQueue<PlagueCloud,(DateTime,long)>();foreach(var c in _clouds.Values){if(c.NextUtc<=at){queue.Enqueue(c,(c.NextUtc,c.Id));}}
        while(queue.TryDequeue(out var due,out _))
        {
            if(!_clouds.TryGetValue(due.Id,out var cloud) || cloud.NextUtc!=due.NextUtc){continue;}
            var owner=World.FindMobile((Serial)cloud.Owner) as PlayerMobile;
            if(owner is not{Alive:true,Deleted:false} || !ClassCombat.Enabled(owner) || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Amazon
                || owner.Map!=Map.Internal && (owner.Map.MapID!=cloud.Map || SpellHelper.IsTown(owner.Location,owner))){_clouds.Remove(cloud.Id);continue;}
            if(owner.Map==Map.Internal){if(cloud.NextUtc.AddMilliseconds((cloud.Frames-cloud.Frame-1)*40L)<=at){_clouds.Remove(cloud.Id);}continue;}
            int frame=cloud.Frame+1;double x=cloud.X+cloud.Vx,y=cloud.Y+cloud.Vy;
            var point=new Point3D((int)Math.Round(x),(int)Math.Round(y),cloud.Z);
            if(frame>=cloud.Frames || x<0 || y<0 || x>=owner.Map.Width || y>=owner.Map.Height || !owner.Map.CanFit(point,1,false,false,false)
                || !owner.Map.LineOfSight(new Point3D((int)Math.Round(cloud.X),(int)Math.Round(cloud.Y),cloud.Z+12),new Point3D(point.X,point.Y,point.Z+12))){_clouds.Remove(cloud.Id);continue;}
            var next=cloud with{X=x,Y=y,Frame=frame,NextUtc=cloud.NextUtc.AddMilliseconds(40),SourceSteps=SourceMissileSteps.Advance(cloud.X,cloud.Y,cloud.Vx,cloud.Vy,cloud.SourceSteps)};_clouds[cloud.Id]=next;
            if(Recipient(owner,next) is BaseCreature target && target.Serial.Value!=cloud.LastCollision)
            {
                _clouds[cloud.Id]=next=next with{LastCollision=target.Serial.Value};int rate=WeaponMissileDamage.RollValue(cloud.Damage.Elements.PoisonMin,cloud.Damage.Elements.PoisonMax,0,WeaponMissiles.DamageRoll);
                owner.DoHarmful(target,true);PoisonJavelins.Poison(owner,target,rate,cloud.Damage.Elements.PoisonImpactFrames,cloud.NextUtc);
            }
            if(frame%5==0){Effects.SendLocationEffect(point,owner.Map,0x374A,2,10,0x3F,0);} // Identified poison placeholder.
            if(next.NextUtc<=at){queue.Enqueue(next,(next.NextUtc,next.Id));}
        }
    }
    internal static void Clear(Mobile owner){foreach(long id in _clouds.Where(x=>x.Value.Owner==owner.Serial.Value).Select(x=>x.Key).ToArray()){_clouds.Remove(id);}}
    internal static PlagueCloud[] Snapshot()=>_clouds.Values.OrderBy(x=>x.Id).ToArray();
    internal static void Restore(PlagueCloud[] saved)
    {
        _clouds.Clear();_next=0;
        foreach(var c in saved)
        {
            var d=c.Damage;var e=d?.Elements;
            if(c.Id<=0 || !((Serial)c.Owner).IsMobile || c.Rank is <1 or >255 || c.Frames!=87 || c.Frame<0 || c.Frame>=c.Frames || c.SourceSteps?.Cells is not{Length:>=1 and <=10}
                || Array.Exists(c.SourceSteps.Cells,v=>v.X is <0 or >65535 || v.Y is <0 or >65535) || !double.IsFinite(c.X) || !double.IsFinite(c.Y) || !double.IsFinite(c.Vx) || !double.IsFinite(c.Vy) || Math.Abs(c.Vx)>.05 || Math.Abs(c.Vy)>.05
                || c.Map<0 || c.Map>=Map.Maps.Length || Map.Maps[c.Map]==null || c.X<0 || c.Y<0 || c.X>=Map.Maps[c.Map].Width || c.Y>=Map.Maps[c.Map].Height || c.LastCollision!=0 && !((Serial)c.LastCollision).IsMobile
                || e==null || e!=new MissileSourceStats(PoisonMin:e.PoisonMin,PoisonMax:e.PoisonMax,PoisonFrames:e.PoisonFrames) || e.PoisonMin<0 || e.PoisonMax<e.PoisonMin || e.PoisonFrames<=0
                || d!=new WeaponMissileDamage(0,0,0,0,0,false,e) || !_clouds.TryAdd(c.Id,c)){throw new InvalidDataException("Invalid saved Plague Javelin cloud");}
            _next=Math.Max(_next,c.Id);if(c.NextUtc.AddMilliseconds((c.Frames-c.Frame-1)*40L)<=TimedEffects.Now){_clouds.Remove(c.Id);}
        }
    }
}
