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
    internal static ActionTiming Timing(int advance)
    {
        advance=Math.Max(1,advance);
        // Cached AMTH1HT:16 frames, missile event9, no TH starting bonus.
        // Retain the ordinary modern repeat-cycle minus-one adapter.
        return new(Math.Max(1,(16*256+advance-1)/advance-1),Math.Max(1,(9*256+advance-1)/advance));
    }
    private static bool Aim(PlayerMobile owner,Point3D point)=>owner.Map!=null && owner.Map!=Map.Internal
        && point.X>=0 && point.Y>=0 && point.X<owner.Map.Width && point.Y<owner.Map.Height
        && owner.InRange(point,CombatProtocol.SightRange)
        && Math.Abs(point.Z-owner.Z)<16 && owner.InLOS(new Point3D(point.X,point.Y,point.Z+12))
        && !SpellHelper.IsTown(owner.Location,owner) && !SpellHelper.IsTown(point,owner);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!AmazonCombat.Available(owner,CombatAbility.PoisonJavelin) || !NamedAmmunition.CanAttack(CombatTiming.Equipped(owner))){return false;}
        if(selected!=Serial.Zero)
        {var target=World.FindMobile(selected);if(!ClassCombat.IsHostile(owner,target)){return false;}point=target.Location;}
        if(!SkillCombat.Begin(owner,CombatAbility.PoisonJavelin,selected,point,now,(p,_,_,aim)=>Aim(p,aim))){return false;}
        var session=ClassCombat.For(owner);session.PendingLeft=session.HeldAbility==CombatAbility.PoisonJavelin && session.HeldAttack;
        // Source start4 succeeds before the generic wrapper consumes mana.
        if(!NecromancerEffects.Pay(owner,SkillMechanics.Values(owner,CombatAbility.PoisonJavelin).Mana256))
        {session.PendingAbility=CombatAbility.None;return false;}
        return true;
    }
    public static bool Release(PlayerMobile owner)
    {
        // The do wrapper does not repeat affordability/start checks. A last
        // javelin can have been removed after the paid start; emission still
        // follows the same-weapon/native action guard and consumes afterward.
        if(!AmazonCombat.Available(owner,CombatAbility.PoisonJavelin) || !FireGround.Ready(owner,CombatAbility.PoisonJavelin)){return false;}
        var session=ClassCombat.For(owner);var target=World.FindMobile(session.PendingTarget);
        var point=ClassCombat.IsHostile(owner,target)?target.Location:session.PendingPoint;
        if(!Aim(owner,point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.PoisonJavelin);
        var packet=BowLaunch.Capture(owner,CombatAbility.PoisonJavelin,NamedCombat.ProjectileCritical(owner,AmazonBow.LaunchRandom),preparedValues:values);
        int dx=(point.X-owner.X)*2,dy=(point.Y-owner.Y)*2;
        long id=WeaponMissiles.Launch(owner,CombatAbility.PoisonJavelin,new(dx,dy,0x21,0),packet,WeaponMissiles.NewCast(),
            NamedProjectiles.PierceBudget(WeaponMissiles.CurrentPierce(owner),AmazonBow.LaunchRandom),
            NecromancerSummons.Source("poisonjav","Vel"),NecromancerSummons.Source("poisonjav","Range"),0,"poisonjav",values.Rank);
        if(id<=0){return false;}
        NamedAmmunition.Consume(CombatTiming.Equipped(owner),TimedEffects.Now);FireGround.Delay(owner,CombatAbility.PoisonJavelin);
        owner.PlaySound(0x238); // Identified ordinary throw sound placeholder.
        return true;
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is{PendingAbility:CombatAbility.PoisonJavelin} session
            && (!PoisonJavelinPackets.WeaponAllowed(owner) || CombatTiming.Equipped(owner)?.Serial!=session.PendingWeapon))
        {session.PendingAbility=CombatAbility.None;}
    }
    internal static bool Poison(PlayerMobile owner,BaseCreature target,int rate256,int frames,DateTime at)
    {
        // Native wild monsters have no equipment poison-length stat or poison
        // shrine state. Their per-monster difficulty already prepared ResPo;
        // the source player-only global penalty does not belong in this lane.
        int resistance=NamedElements.Resistance(owner,target,"pois",target.PoisonResistance,pierceOverride:0);
        return PoisonPackets.Mitigate(rate256,frames,false,resistance,0,Math.Max(0,ClassGear.Bonus(owner,"pierce-pois")),0,0).Commit(owner,target,at);
    }
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
        // LastCollide is deliberately checked after choosing the first collider.
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(point,3))
        {
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
                || d.DemonPercent!=0 || d.UndeadPercent!=0 || d.FireMastery!=0 || d.ColdMastery!=0 || d.LightningMastery!=0 || d.PoisonMastery!=0 || d.MagicMastery!=0 || d.SkillAttackPercent!=0 || d.FreezeColdLength
                || e.PoisonMin<0 || e.PoisonMax<e.PoisonMin || e.PoisonFrames<=0 || !_clouds.TryAdd(cloud.Id,cloud))
            {throw new InvalidDataException("Invalid saved poison cloud");}
            _next=Math.Max(_next,cloud.Id);
            if(cloud.NextUtc.AddMilliseconds((59-cloud.Frame)*40L)<=TimedEffects.Now){_clouds.Remove(cloud.Id);}
        }
    }
}
