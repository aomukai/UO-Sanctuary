// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record ShockWaveMissile(long Id,long Cast,uint Owner,int Map,int Rank,int Z,double X,double Y,
    double Vx,double Vy,int Remaining,uint LastCollision,int Min256,int Max256,FirestormModifiers Modifiers,
    ulong Seed,DateTime NextUtc,SourceStepTrace Steps,string Fault=null);
internal sealed record ShockWaveSave(ShockWaveMissile[] Missiles,WeaponFlightLease[] Leases,long NextId)
{public string DelayScope{get;init;}="cast-target-inferred-2.7";}
public sealed class ShockWaveProfiles:GenericPersistence
{
    public ShockWaveProfiles():base("SanctuaryShockWave",179){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(DruidShockWave.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Shock Wave version");}DruidShockWave.Restore(JsonSerializer.Deserialize<ShockWaveSave>(reader.ReadString()));}
}

// Prepared source-zero physical fan. Learned casting is enabled separately.
// Native Q12/terrain/room enumeration and modern per-execution delay grouping
// retain the documented adapters; unsupported owner contexts age harmlessly.
public static class DruidShockWave
{
    private sealed record Pool(ImmutableDictionary<long,ShockWaveMissile> Missiles,
        ImmutableDictionary<(long,uint),WeaponFlightLease> Leases,long NextId);
    private static Pool _pool=new(ImmutableDictionary<long,ShockWaveMissile>.Empty,ImmutableDictionary<(long,uint),WeaponFlightLease>.Empty,0);
    private static readonly HashSet<long> _loaded=new();
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<int,bool> Allocate=_=>true;
    public static ShockWaveProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static ShockWaveMissile Copy(ShockWaveMissile s)=>s with{Steps=new(s.Steps.Cells.ToArray())};
    internal static ShockWaveSave Snapshot()=>new(_pool.Missiles.Values.OrderBy(x=>x.Id).Select(Copy).ToArray(),
        _pool.Leases.Values.OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray(),_pool.NextId);
    private static void Routes()
    {
        var skill=SkillMechanics.For(CombatAbility.ShockWave);int F(string key)=>NecromancerSummons.Source("shockwave",key);
        if(skill.Number("srvdofunc")!=8||skill.Number("srvstfunc")!=0||skill.Number("SrcDam")!=0||skill.Text("anim")!="SC"
            ||skill.Text("srvmissilea")!="shockwave"||skill.Text("srvmissileb")!="shockwave"||skill.Number("mana")!=7
            ||skill.Number("Param1")!=40||skill.Number("Param2")!=15||skill.Number("Param8")!=10
            ||F("pSrvDoFunc")!=1||F("pSrvHitFunc")!=0||F("pSrvDmgFunc")!=7||F("Vel")!=20||F("VelLev")!=0||F("MaxVel")!=20
            ||F("Range")!=14||F("Activate")!=0||F("Size")!=1||F("CollideType")!=3||F("Collision")!=0||F("LastCollide")!=1
            ||F("NextHit")!=1||F("NextDelay")!=4||F("ToHit")!=0||F("Pierce")!=0||F("CollideKill")!=0||F("GetHit")!=1
            ||F("ResultFlags")!=4||F("HitClass")!=64||F("ReturnFire")!=1||F("CanSlow")!=1||F("SrcDamage")!=0||F("SrcMissDmg")!=0)
        {throw new NotSupportedException("Unsupported Shock Wave route");}
    }
    internal static long[] Create(PlayerMobile owner,Point3D aim,SkillValues values,DateTime at)
    {
        Routes();
        if(owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal||at.Kind!=DateTimeKind.Utc
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Druid
            ||values.Ability!=CombatAbility.ShockWave||values.Rank is <1 or >255||values.Eval("calc1")!=5||values.Eval("calc2")!=0||values.Eval("calc3")!=0)
        {throw new NotSupportedException("Unsupported Shock Wave supplier");}
        var rays=FanGeometry.Create((aim.X-owner.X)*2,(aim.Y-owner.Y)*2,5);
        var slow=AmazonSlowMissiles.Capture(owner,true);int velocity=SlowMissilePayload.VelocityFp8(20,0,values.Rank,2080,0,true,slow.VelocityPercent);
        if(velocity<=0){throw new NotSupportedException("Unmapped zero-velocity Shock Wave path");}
        if(slow.DamagePercent!=100){throw new NotSupportedException("Shock Wave modern Slow Missiles damage scaling is not established");}
        var mods=DruidFirestorm.Modifiers(owner);int minimum=values.PhysicalDamage256(false),maximum=values.PhysicalDamage256(true);
        long cast=checked(_pool.NextId+1);var next=_pool.Missiles;var ids=new List<long>();
        for(int i=0;i<rays.Length;i++)
        {
            var ray=rays[i];int dx=ray.X,dy=ray.Y;if(dx==0&&dy==0){dx=dy=1;}
            // Full source factory rejects either absolute aim delta>=100.
            // Do8 itself still succeeds when individual allocations fail.
            if(Math.Abs(dx)>=100||Math.Abs(dy)>=100||!Allocate(i)){continue;}
            var vector=MissileVectors.VelocityFp8(dx,dy,velocity);long id=cast+i;
            var wave=new ShockWaveMissile(id,cast,owner.Serial.Value,owner.Map.MapID,values.Rank,owner.Z,owner.X,owner.Y,
                vector.X,vector.Y,14,owner.Serial.Value,minimum,maximum,mods,SourceRandom.InitLow(SeedLow()),at.AddMilliseconds(40),new([new(owner.X*2,owner.Y*2)]));
            Validate(wave);next=next.Add(id,wave);ids.Add(id);
        }
        _pool=_pool with{Missiles=next,NextId=cast+4};return ids.ToArray();
    }
    internal static bool Ready(long cast,uint target,DateTime at)=>!_pool.Leases.TryGetValue((cast,target),out var lease)||lease.UntilUtc<=at;
    internal static BaseCreature Recipient(PlayerMobile owner,ShockWaveMissile wave,SourceMissileCell cell)
    {
        foreach(var target in Map.Maps[wave.Map].GetMobilesInRange<BaseCreature>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,wave.Z),3))
        {
            if(target.Serial.Value==wave.LastCollision||!Ready(wave.Cast,target.Serial.Value,wave.NextUtc)){continue;}
            int size=CreatureProfiles.For(target).Traits.Size??2;
            if(Math.Abs(target.Z-wave.Z)<16&&CombatHitboxes.SourceTouches(1,size,cell.X-target.X*2,cell.Y-target.Y*2)
                &&ClassCombat.IsHostile(owner,target)&&!SpellHelper.IsTown(target.Location,owner)){return target;}
        }
        return null;
    }
    internal static ShockWaveMissile Impact(PlayerMobile owner,BaseCreature target,ShockWaveMissile wave)
    {
        var lease=new WeaponFlightLease(wave.Cast,target.Serial.Value,wave.NextUtc.AddMilliseconds(160));
        _pool=_pool with{Leases=_pool.Leases.SetItem((lease.Cast,lease.Target),lease)};
        var next=wave with{LastCollision=target.Serial.Value};
        if(!SourceBlocking.TryRate(target,out _,out _)||!CombatStuns.Rules(target,out _,out _))
        {return next with{Fault="Unmapped Shock Wave recipient block/stun metadata"};}
        // HITBYMISSILE precedes fill/defense. The shared consumer checks ReturnFire.
        SorceressEffects.MissileHit(owner,target,"shockwave");
        ulong seed=wave.Seed;int raw=WeaponMissileDamage.RollValue(wave.Min256,wave.Max256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        next=next with{Seed=seed};
        if(ClassAvoidance.Missile(target,"shockwave")||!SourceBlocking.TryBlock(target,raw!=0,out bool blocked)||blocked){return next;}
        owner.DoHarmful(target,true);
        // No weapon/strength/charge ED,critical,leech,Enchant or strike procs.
        // DAMAGE_VS_MONTYPE item layers have no native producer/consumer yet;
        // the named-item gate retains that separately unsupported branch.
        var parts=SkillCombat.MitigatePrepared(owner,target,DruidMoltenBoulder.Potion(wave.Modifiers,new(raw/256.0,0,0,0,0)));
        int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
        // Complete ExecuteEvents commits HP first,even when immunity yields0.
        // Native synchronous death/reentrant context changes remain explicit.
        if(target is{Alive:true,Deleted:false}&&owner is{Alive:true,Deleted:false}&&owner.Map==target.Map)
        {
            CombatStuns.Apply(target,40+15*(wave.Rank-1),wave.NextUtc);
            CreatureHitRecovery.Apply(target,owner,loss);
        }
        else{next=next with{Fault="Shock Wave lethal/reentrant post-health stun chronology remains unverified"};}
        target.FixedEffect(0x37CC,3,4,0x481,0); // Identified contact placeholder.
        return next;
    }
    private static void Remove(long id){_pool=_pool with{Missiles=_pool.Missiles.Remove(id)};_loaded.Remove(id);}
    private static void Advance(ShockWaveMissile wave,DateTime at,bool? actions)
    {
        var owner=World.FindMobile((Serial)wave.Owner) as PlayerMobile;
        bool available=owner is{Alive:true,Deleted:false}&&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Druid
            &&owner.Map?.MapID==wave.Map&&(actions??owner.NetState is{Running:true});
        bool loaded=_loaded.Remove(wave.Id);
        int skipped=loaded?checked((int)Math.Min(wave.Remaining,(at-wave.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1)):1;
        int remaining=wave.Remaining-skipped;if(remaining<=0){Remove(wave.Id);return;}
        var next=wave with{Remaining=remaining,NextUtc=wave.NextUtc.AddMilliseconds(40L*skipped)};
        if(!available||loaded)
        {_pool=_pool with{Missiles=_pool.Missiles.SetItem(wave.Id,next with{Fault=!available?"Shock Wave unsupported owner/death/history/offline/facet context; clocks continue":null})};return;}
        var map=Map.Maps[wave.Map];double x=wave.X+wave.Vx,y=wave.Y+wave.Vy;
        if(x<=0||y<=0||x>=map.Width||y>=map.Height||SpellHelper.IsTown(new Point3D((int)Math.Round(wave.X),(int)Math.Round(wave.Y),wave.Z),owner)){Remove(wave.Id);return;}
        var steps=SourceMissileSteps.Advance(wave.X,wave.Y,wave.Vx,wave.Vy,wave.Steps);
        foreach(var cell in steps.Cells)
        {
            if(Recipient(owner,wave,cell) is{} target){next=Impact(owner,target,next with{NextUtc=wave.NextUtc});break;}
            if(DruidFirestorm.CellBlocked(map,wave.Z,wave.X,wave.Y,cell.X,cell.Y)){Remove(wave.Id);return;}
        }
        next=next with{X=x,Y=y,Steps=steps,NextUtc=wave.NextUtc.AddMilliseconds(40),Fault=next.Fault};
        _pool=_pool with{Missiles=_pool.Missiles.SetItem(wave.Id,next)};
        if(remaining%2==0){Effects.SendLocationEffect(new((int)Math.Round(x),(int)Math.Round(y),wave.Z),map,0x37CC,3,8,0x481,0);} // Identified wave placeholder.
    }
    internal static void Tick(DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shock Wave clock must be UTC");}
        while(true)
        {
            var wave=_pool.Missiles.Values.Where(x=>x.NextUtc<=at).OrderBy(x=>x.NextUtc).ThenByDescending(x=>x.Id).FirstOrDefault();
            if(wave==null){Prune(at);return;}
            Advance(wave,at,actions);
        }
    }
    private static void Prune(DateTime at)
    {
        var casts=_pool.Missiles.Values.Select(x=>x.Cast).ToHashSet();
        _pool=_pool with{Leases=_pool.Leases.RemoveRange(_pool.Leases.Where(x=>x.Value.UntilUtc<=at||!casts.Contains(x.Key.Item1)).Select(x=>x.Key))};
    }
    private static void Validate(ShockWaveMissile s)
    {
        if(s==null||s.Id<=0||s.Cast<=0||s.Id<s.Cast||s.Id>s.Cast+4||!((Serial)s.Owner).IsMobile||!((Serial)s.LastCollision).IsMobile||s.Rank is <1 or >255
            ||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal||s.Z is <-128 or >127
            ||!double.IsFinite(s.X+s.Y+s.Vx+s.Vy)||s.X<=0||s.Y<=0||s.X>=Map.Maps[s.Map].Width||s.Y>=Map.Maps[s.Map].Height
            ||s.Vx==0&&s.Vy==0||Math.Abs(s.Vx)>1||Math.Abs(s.Vy)>1||s.Remaining is <1 or >14
            ||s.Min256<0||s.Max256<s.Min256||s.Modifiers==null||s.Modifiers.Potion<0||s.NextUtc.Kind!=DateTimeKind.Utc
            ||s.Steps?.Cells is not{Length:>=1 and <=10}||s.Steps.Cells.Any(c=>c.X<=0||c.Y<=0||c.X>=Map.Maps[s.Map].Width*2||c.Y>=Map.Maps[s.Map].Height*2))
        {throw new InvalidDataException("Invalid Shock Wave missile");}
    }
    internal static void Restore(ShockWaveSave saved)
    {
        if(saved?.Missiles==null||saved.Leases==null||saved.NextId<0||saved.DelayScope!="cast-target-inferred-2.7"){throw new InvalidDataException("Invalid Shock Wave save");}
        var pool=new Pool(ImmutableDictionary<long,ShockWaveMissile>.Empty,ImmutableDictionary<(long,uint),WeaponFlightLease>.Empty,saved.NextId);
        foreach(var s in saved.Missiles){Validate(s);if(s.Id>saved.NextId||pool.Missiles.ContainsKey(s.Id)){throw new InvalidDataException("Duplicate Shock Wave identity");}pool=pool with{Missiles=pool.Missiles.Add(s.Id,Copy(s))};}
        foreach(var group in pool.Missiles.Values.GroupBy(x=>x.Cast))
        {var first=group.First();if(group.Any(x=>x.Owner!=first.Owner||x.Map!=first.Map||x.Rank!=first.Rank)){throw new InvalidDataException("Mixed Shock Wave cast");}}
        var casts=pool.Missiles.Values.Select(x=>x.Cast).ToHashSet();
        foreach(var lease in saved.Leases)
        {
            if(lease==null||!casts.Contains(lease.Cast)||!((Serial)lease.Target).IsMobile||lease.UntilUtc.Kind!=DateTimeKind.Utc||lease.RetainWithoutFlight||pool.Leases.ContainsKey((lease.Cast,lease.Target)))
            {throw new InvalidDataException("Invalid Shock Wave window");}
            pool=pool with{Leases=pool.Leases.Add((lease.Cast,lease.Target),lease)};
        }
        _pool=pool;_loaded.Clear();foreach(var s in saved.Missiles){_loaded.Add(s.Id);}
    }
}
