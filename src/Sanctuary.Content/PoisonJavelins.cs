// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record PoisonCloud(long Id,uint Owner,int Map,double X,double Y,int Z,int Rank,WeaponMissileDamage Damage,
    int Frame,uint LastCollision,DateTime NextUtc);

public static class PoisonJavelins
{
    private static readonly Dictionary<long,PoisonCloud> _clouds=new();
    private static long _next;
    internal static ActionTiming Timing(int advance)=>AmazonJavelins.Timing(advance);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>AmazonJavelins.Begin(owner,CombatAbility.PoisonJavelin,selected,point,now);
    public static bool Release(PlayerMobile owner)=>AmazonJavelins.Release(owner,CombatAbility.PoisonJavelin);
    internal static void EquipmentChanged(PlayerMobile owner)=>AmazonJavelins.EquipmentChanged(owner);
    internal static PreparedPoison PreparePoison(PlayerMobile owner,BaseCreature target,int rate256,int frames)
    {
        // Read the recipient's item poison-length pool, including bound NPC
        // equipment. Per-monster difficulty already prepared ordinary ResPo;
        // the source player-only global penalty does not belong in this lane.
        int resistance=NamedElements.Resistance(owner,target,"pois",target.PoisonResistance,pierceOverride:0);
        return PoisonPackets.Mitigate(rate256,frames,false,resistance,ItemDurationDefenses.PoisonLength(target),Math.Max(0,ClassGear.Bonus(owner,"pierce-pois")),0,0);
    }
    internal static bool Poison(PlayerMobile owner,BaseCreature target,int rate256,int frames,DateTime at)
        =>PreparePoison(owner,target,rate256,frames).Commit(owner,target,at);
    internal static PoisonCloud Trail(PlayerMobile owner,WeaponFlight parent)
    {
        if(parent.Ability!=CombatAbility.PoisonJavelin || !parent.PathChanged){return null;}
        long id=++_next;var cloud=new PoisonCloud(id,owner.Serial.Value,parent.Map,
            Math.Floor(parent.X*2+.5)/2,Math.Floor(parent.Y*2+.5)/2,parent.Z,parent.SkillRank,
            PoisonJavelinPackets.Cloud(owner,parent.SkillRank),0,0,parent.NextUtc.AddMilliseconds(40));
        _clouds.Add(id,cloud);return cloud;
    }
    internal static Mobile Recipient(PlayerMobile owner,PoisonCloud cloud)
    {
        var point=new Point3D((int)Math.Round(cloud.X),(int)Math.Round(cloud.Y),cloud.Z);
        // Native sector order projects the source adjacent-room linked lists.
        // The complete source finder excludes the prior collider before selection.
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(point,3))
        {
            if(target.Serial.Value==cloud.LastCollision){continue;}
            int size=target is BaseCreature c?CreatureProfiles.For(c).Traits.Size??2:2;
            int dx=(int)(cloud.X*2)-target.X*2,dy=(int)(cloud.Y*2)-target.Y*2;
            if(Math.Abs(target.Z-cloud.Z)<16 && CombatHitboxes.SourceTouches(2,size,dx,dy)
                && ClassCombat.IsHostile(owner,target) && !SpellHelper.IsTown(target.Location,owner)){return target;}
        }
        return null;
    }
    internal static void Tick(DateTime at)
    {
        var queue=new PriorityQueue<PoisonCloud,(DateTime,long)>();
        foreach(var cloud in _clouds.Values){if(cloud.NextUtc<=at){queue.Enqueue(cloud,(cloud.NextUtc,cloud.Id));}}
        while(queue.TryDequeue(out var due,out _))
        {
            if(!_clouds.TryGetValue(due.Id,out var cloud) || cloud.NextUtc!=due.NextUtc){continue;}
            var owner=World.FindMobile((Serial)cloud.Owner) as PlayerMobile;
            if(owner is not{Alive:true,Deleted:false} || !ClassCombat.Enabled(owner)
                || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Amazon
                || owner.Map!=Map.Internal && (owner.Map.MapID!=cloud.Map || SpellHelper.IsTown(owner.Location,owner)))
            {_clouds.Remove(cloud.Id);continue;}
            if(owner.Map==Map.Internal)
            {if(cloud.NextUtc.AddMilliseconds((59-cloud.Frame)*40L)<=at){_clouds.Remove(cloud.Id);}continue;}
            var point=new Point3D((int)Math.Round(cloud.X),(int)Math.Round(cloud.Y),cloud.Z);
            if(!owner.Map.CanFit(point,1,false,false,false)){_clouds.Remove(cloud.Id);continue;} // Existing native field terrain adapter.
            int frame=cloud.Frame+1;
            if(frame>=NecromancerSummons.Source("poisonjavcloud","Range")){_clouds.Remove(cloud.Id);continue;}
            var next=cloud with{Frame=frame,NextUtc=cloud.NextUtc.AddMilliseconds(40)};_clouds[cloud.Id]=next;
            if(Recipient(owner,cloud) is BaseCreature target && target.Serial.Value!=cloud.LastCollision)
            {
                _clouds[cloud.Id]=next=next with{LastCollision=target.Serial.Value};
                int rate=WeaponMissileDamage.RollValue(cloud.Damage.Elements.PoisonMin,cloud.Damage.Elements.PoisonMax,0,WeaponMissiles.DamageRoll);
                owner.DoHarmful(target,true);Poison(owner,target,rate,cloud.Damage.Elements.PoisonImpactFrames,cloud.NextUtc);
            }
            if(frame%5==0){Effects.SendLocationEffect(new Point3D((int)Math.Round(cloud.X),(int)Math.Round(cloud.Y),cloud.Z),owner.Map,0x374A,2,10,0x3F,0);} // Identified green poison placeholder.
            if(next.NextUtc<=at){queue.Enqueue(next,(next.NextUtc,next.Id));}
        }
    }
    internal static void ClearClouds(Mobile owner)
    {foreach(long id in _clouds.Where(x=>x.Value.Owner==owner.Serial.Value).Select(x=>x.Key).ToArray()){_clouds.Remove(id);}}
    public static void Cancel(Mobile owner)=>WeaponMissiles.CancelPoison(owner);
    internal static PoisonCloud[] Snapshot()=>_clouds.Values.OrderBy(x=>x.Id).ToArray();
    internal static void Restore(PoisonCloud[] saved)
    {
        _clouds.Clear();_next=0;
        foreach(var cloud in saved)
        {
            var d=cloud.Damage;var e=d?.Elements;
            if(cloud.Id<=0 || !((Serial)cloud.Owner).IsMobile || cloud.Rank is <1 or >255 || cloud.Frame is <0 or >=60
                || !double.IsFinite(cloud.X) || !double.IsFinite(cloud.Y) || cloud.Map<0 || cloud.Map>=Map.Maps.Length || Map.Maps[cloud.Map]==null
                || cloud.X<0 || cloud.Y<0 || cloud.X>=Map.Maps[cloud.Map].Width || cloud.Y>=Map.Maps[cloud.Map].Height
                || cloud.LastCollision!=0 && !((Serial)cloud.LastCollision).IsMobile
                || e==null || d.Min256!=0 || d.Max256!=0 || d.Percent!=0 || d.Critical || d.PhysicalSupplement!=null || d.Conversion!=null || d.PhysicalToMagicPercent!=0
                || e!=new MissileSourceStats(PoisonMin:e.PoisonMin,PoisonMax:e.PoisonMax,PoisonFrames:e.PoisonFrames)
                || d.LightningJavelinPercent!=null || d.DemonPercent!=0 || d.UndeadPercent!=0 || d.FireMastery!=0 || d.ColdMastery!=0 || d.LightningMastery!=0 || d.PoisonMastery!=0 || d.MagicMastery!=0 || d.SkillAttackPercent!=0 || d.FreezeColdLength
                || e.PoisonMin<0 || e.PoisonMax<e.PoisonMin || e.PoisonFrames<=0 || !_clouds.TryAdd(cloud.Id,cloud))
            {throw new InvalidDataException("Invalid saved poison cloud");}
            _next=Math.Max(_next,cloud.Id);
            if(cloud.NextUtc.AddMilliseconds((59-cloud.Frame)*40L)<=TimedEffects.Now){_clouds.Remove(cloud.Id);}
        }
    }
}
