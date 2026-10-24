// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record ShockShard(long Id,long Cast,uint Owner,int Map,int Rank,double X,double Y,int Z,double Vx,double Vy,
    int Remaining,bool Ground,DateTime NextUtc,int Min256=0,int Max256=0,int Potion=0,
    int FireMastery=0,int ColdMastery=0,int LightningMastery=0,int PoisonMastery=0);
public sealed record ShockWebSave(ShockShard[] Shards,WeaponFlightLease[] Leases,long NextId,long NextCast);
public sealed class ShockWebProfiles:GenericPersistence
{
    private ShockWebSave _snapshot;
    public ShockWebProfiles():base("SanctuaryShockWeb",154){}
    public override void Serialize(){_snapshot=ShockWebs.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Shock Web version");}ShockWebs.Restore(JsonSerializer.Deserialize<ShockWebSave>(reader.ReadString()));}
}

public static class ShockWebs
{
    private static readonly Dictionary<long,ShockShard> _shards=new();
    private static readonly Dictionary<(long,uint),WeaponFlightLease> _leases=new();
    private static long _nextId,_nextCast;
    internal static Func<int,int> Roll=Utility.Random;
    public static ShockWebProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static int Count(SkillValues values)=>(int)values.Eval("prgcalc1");
    internal static Point2D[] Scatter(int casterX,int casterY,int targetX,int targetY,int count,int range)
    {
        ulong seed=SourceRandom.InitLow(targetX);var result=new List<Point2D>();
        if(count<=1 || range<2){return [new(targetX,targetY)];}
        for(int i=0;i<count;i++)
        {
            int x=targetX+(int)SourceRandom.Limited(ref seed,2*range)-range,y=targetY+(int)SourceRandom.Limited(ref seed,2*range)-range;
            if((long)(x-casterX)*(x-casterX)+(long)(y-casterY)*(y-casterY)>=4){result.Add(new(x,y));}
        }
        return result.ToArray();
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!LobProjectiles.Aim(owner,selected,ref point)){return false;}
        return SkillCombat.Begin(owner,CombatAbility.ShockWeb,selected,point,now,(p,_,_,at)=>LobProjectiles.Valid(p,at),timing:LobProjectiles.Timing(owner));
    }
    public static bool Release(PlayerMobile owner,Serial selected)
    {
        var point=ClassCombat.For(owner).PendingPoint;
        if(!AssassinCombat.Available(owner,CombatAbility.ShockWeb) || !LobProjectiles.Aim(owner,selected,ref point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.ShockWeb);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        long cast=++_nextCast;DateTime at=TimedEffects.Now.AddMilliseconds(40);
        foreach(var aim in Scatter(owner.X*2,owner.Y*2,point.X*2,point.Y*2,Count(values),(int)values.Eval("aurarangecalc")))
        {
            int dx=aim.X-owner.X*2,dy=aim.Y-owner.Y*2;
            if(Math.Abs(dx)>=100 || Math.Abs(dy)>=100 || aim.X<=0 || aim.Y<=0 || aim.X>=owner.Map.Width*2 || aim.Y>=owner.Map.Height*2){continue;}
            var v=MissileVectors.VelocityFp8(dx,dy,LobProjectiles.Velocity);long id=++_nextId;
            _shards.Add(id,new(id,cast,owner.Serial.Value,owner.Map.MapID,values.Rank,owner.X,owner.Y,owner.Z,v.X,v.Y,LobProjectiles.Lifetime(dx,dy),false,at));
        }
        // Source do43 returns success even if its near-caster exclusions emitted
        // no bomblets. One cast payment and the skill-local delay still apply.
        FireGround.Delay(owner,CombatAbility.ShockWeb);owner.PlaySound(0x23B); // Identified throw-audio placeholder.
        return true;
    }
    internal static bool Ready(long cast,uint target,DateTime at)=>!_leases.TryGetValue((cast,target),out var lease) || lease.UntilUtc<=at;
    internal static BaseCreature Recipient(PlayerMobile owner,ShockShard shard)
    {
        var point=new Point3D((int)Math.Round(shard.X),(int)Math.Round(shard.Y),shard.Z);
        // Same source Size2 footprint as the poison clouds. Here NHD is part of
        // the finder, so an ineligible first actor cannot hide the next actor.
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(point,3))
        {
            int size=CreatureProfiles.For(target).Traits.Size??2;
            if(Math.Abs(target.Z-shard.Z)<16 && CombatHitboxes.SourceTouches(2,size,(int)(shard.X*2)-target.X*2,(int)(shard.Y*2)-target.Y*2)
                && ClassCombat.IsHostile(owner,target) && Ready(shard.Cast,target.Serial.Value,shard.NextUtc)
                && !SpellHelper.IsTown(target.Location,target)){return target;}
        }
        return null;
    }
    internal static GearDamage Packet(ShockShard s,int damage256)
    {
        double lightning=damage256/256.0,added=lightning*s.Potion/100;
        return new(0,added*(100+s.FireMastery)/100,added*(100+s.ColdMastery)/100,
            lightning+added*(100+s.LightningMastery)/100,added*(100+s.PoisonMastery)/100);
    }
    private static void Hit(PlayerMobile owner,BaseCreature target,ShockShard shard)
    {
        // Current origin/cast scope follows the existing weapon-projectile
        // lease model and released2.7 rule. Exact repeated-throw scope remains
        // explicitly tracked for current-game corroboration.
        _leases[(shard.Cast,target.Serial.Value)]=new(shard.Cast,target.Serial.Value,shard.NextUtc.AddMilliseconds(NecromancerSummons.Source("shock field on ground","NextDelay")*40L));
        var raw=Packet(shard,WeaponMissileDamage.RollValue(shard.Min256,shard.Max256,0,Roll));
        if(ClassAvoidance.Missile(target)){return;}
        owner.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(owner,target,raw);
        SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total)); // SoftHit: no ordinary get-hit/recovery request.
    }
    private static ShockShard Land(PlayerMobile owner,ShockShard shard)
    {
        if(!LobProjectiles.CanLand(owner,shard.X,shard.Y)){return null;}
        var values=SkillMechanics.Values(owner,CombatAbility.ShockWeb,shard.Rank);
        int Enhance(int damage)=>(int)Math.Clamp((long)damage+(long)damage*NamedElements.MasteryPercent(owner,"ltng")/100,0,int.MaxValue);
        return shard with{X=Math.Floor(shard.X*2+.5)/2,Y=Math.Floor(shard.Y*2+.5)/2,Vx=0,Vy=0,Remaining=NecromancerSummons.Source("shock field on ground","Range"),Ground=true,NextUtc=shard.NextUtc.AddMilliseconds(40),
            Min256=Enhance(values.Damage256(false)),Max256=Enhance(values.Damage256(true)),Potion=CombatPotions.Bonus(owner,"magical-penetration"),
            FireMastery=NamedElements.MasteryPercent(owner,"fire"),ColdMastery=NamedElements.MasteryPercent(owner,"cold"),
            LightningMastery=NamedElements.MasteryPercent(owner,"ltng"),PoisonMastery=NamedElements.MasteryPercent(owner,"pois")};
    }
    internal static void Tick(DateTime at)
    {
        var queue=new PriorityQueue<ShockShard,(DateTime,long)>();
        foreach(var s in _shards.Values){if(s.NextUtc<=at){queue.Enqueue(s,(s.NextUtc,s.Id));}}
        while(queue.TryDequeue(out var due,out _))
        {
            if(!_shards.TryGetValue(due.Id,out var s) || s.NextUtc!=due.NextUtc){continue;}
            var owner=World.FindMobile((Serial)s.Owner) as PlayerMobile;
            if(owner is not{Deleted:false} || owner.Map==null || ClassCreation.Profiles.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Assassin
                || owner.Map!=Map.Internal && owner.Map.MapID!=s.Map){_shards.Remove(s.Id);continue;}
            if(owner.Map==Map.Internal)
            {if(s.NextUtc.AddMilliseconds((s.Remaining+(s.Ground?0:90))*40L)<=at){_shards.Remove(s.Id);}continue;}
            ShockShard next;
            if(s.Ground)
            {
                if(s.Remaining<=1){_shards.Remove(s.Id);continue;}
                var recipient=Recipient(owner,s);
                if(recipient!=null){Hit(owner,recipient,s);}
                else if(LobProjectiles.Barrier(owner.Map,new((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z))){_shards.Remove(s.Id);continue;}
                next=s with{Remaining=s.Remaining-1,NextUtc=s.NextUtc.AddMilliseconds(40)};
                if(s.Remaining%5==0){Effects.SendLocationEffect(new((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z),owner.Map,0x374A,2,10,0x481,0);} // Identified electric-field placeholder.
            }
            else
            {
                var move=LobProjectiles.Advance(owner.Map,s.X,s.Y,s.Z,s.Vx,s.Vy,s.Remaining);var moved=s with{X=move.X,Y=move.Y};
                next=move.Landed?Land(owner,moved):moved with{Remaining=s.Remaining-1,NextUtc=s.NextUtc.AddMilliseconds(40)};
                if(next!=null){Effects.SendLocationEffect(new((int)Math.Round(next.X),(int)Math.Round(next.Y),next.Z),owner.Map,0x36E4,2,10,0x481,0);} // Identified caltrop/lob placeholder.
            }
            if(next==null){_shards.Remove(s.Id);continue;}_shards[s.Id]=next;
            if(next.NextUtc<=at){queue.Enqueue(next,(next.NextUtc,next.Id));}
        }
        var active=_shards.Values.Select(s=>s.Cast).ToHashSet();
        foreach(var key in _leases.Where(x=>!active.Contains(x.Key.Item1) || x.Value.UntilUtc<=at).Select(x=>x.Key).ToArray()){_leases.Remove(key);}
    }
    internal static ShockWebSave Snapshot()=>new(_shards.Values.OrderBy(s=>s.Id).ToArray(),_leases.Values.OrderBy(s=>s.Cast).ThenBy(s=>s.Target).ToArray(),_nextId,_nextCast);
    internal static void Restore(ShockWebSave saved)
    {
        if(saved?.Shards==null || saved.Leases==null || saved.NextId<0 || saved.NextCast<0){throw new InvalidDataException("Missing Shock Web state");}
        _shards.Clear();_leases.Clear();_nextId=saved.NextId;_nextCast=saved.NextCast;
        foreach(var s in saved.Shards)
        {
            if(s.Id<=0 || s.Cast<=0 || !((Serial)s.Owner).IsMobile || s.Rank is <1 or >255 || s.Remaining<1 || s.Remaining>(s.Ground?90:264)
                || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || !double.IsFinite(s.X+s.Y+s.Vx+s.Vy)
                || s.X<0 || s.Y<0 || s.X>=Map.Maps[s.Map].Width || s.Y>=Map.Maps[s.Map].Height || s.Min256<0 || s.Max256<s.Min256
                || s.Potion<0 || Math.Abs(s.Vx)>1 || Math.Abs(s.Vy)>1 || s.NextUtc.Kind!=DateTimeKind.Utc
                || s.Ground && (s.Vx!=0 || s.Vy!=0) || !s.Ground && (s.Min256!=0 || s.Max256!=0 || s.Potion!=0)
                || !_shards.TryAdd(s.Id,s)){throw new InvalidDataException("Invalid saved Shock Web");}
            _nextId=Math.Max(_nextId,s.Id);_nextCast=Math.Max(_nextCast,s.Cast);
        }
        foreach(var lease in saved.Leases)
        {if(lease.Cast<=0 || !((Serial)lease.Target).IsMobile || lease.UntilUtc.Kind!=DateTimeKind.Utc || !_leases.TryAdd((lease.Cast,lease.Target),lease)){throw new InvalidDataException("Invalid Shock Web hit-delay lease");}_nextCast=Math.Max(_nextCast,lease.Cast);}
    }
}
