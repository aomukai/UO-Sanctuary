// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record VolcanoActor(long Id,long Cast,uint Owner,int Map,int Rank,int Missile,int Z,
    double X,double Y,double Vx,double Vy,int Remaining,uint Memory,int PhysicalMin,int PhysicalMax,int FireMin,int FireMax,
    FirestormModifiers Modifiers,ulong Seed,uint LastTarget,DateTime NextUtc,DateTime UntilUtc)
{
    public string MotionAdapter{get;init;}="native-shared-q12-straight-v1";
}
internal sealed record VolcanoSave(VolcanoActor[] Actors,WeaponFlightLease[] Leases,long NextId)
{
    public string DelayScope{get;init;}="cast-target-provisional-2.7";
}
public sealed class VolcanoProfiles:GenericPersistence
{
    public VolcanoProfiles():base("SanctuaryVolcano",169){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(DruidVolcano.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Volcano version");}
        DruidVolcano.Restore(JsonSerializer.Deserialize<VolcanoSave>(reader.ReadString()));
    }
}

// Source244 vent479, noncolliding air481 and independently captured lava483.
// Modern cast/target hit-delay grouping and native geometry/motion/room/event
// order are explicit adapters, separate from the complete source route proofs.
public static class DruidVolcano
{
    private sealed record Pool(ImmutableDictionary<long,VolcanoActor> Actors,
        ImmutableDictionary<(long,uint),WeaponFlightLease> Leases,long NextId);
    private static Pool _pool=new(ImmutableDictionary<long,VolcanoActor>.Empty,ImmutableDictionary<(long,uint),WeaponFlightLease>.Empty,0);
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<Mobile,bool> Uninterruptible=BarbarianLeap.Uninterruptible;
    public static VolcanoProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static VolcanoSave Snapshot()
    {
        var pool=_pool;
        return new(pool.Actors.Values.OrderBy(x=>x.Id).ToArray(),pool.Leases.Values.OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray(),pool.NextId);
    }
    internal static void Cancel(Mobile owner)
    {
        if(owner==null){return;}
        _pool=_pool with{Actors=_pool.Actors.RemoveRange(_pool.Actors.Where(x=>x.Value.Owner==owner.Serial.Value).Select(x=>x.Key))};Prune(DateTime.MinValue);
    }
    private static PlayerMobile Owner(uint serial)=>World.FindMobile((Serial)serial) is PlayerMobile{Alive:true,Deleted:false} p
        && ClassCreation.Profiles.TryGet(p.Serial,out var profile) && profile.Class==DiabloClass.Druid?p:null;
    private static bool Fighting(PlayerMobile owner,int map,bool? actions)=>owner.Map!=null && owner.Map!=Map.Internal
        && owner.Map.MapID==map && (actions??owner.NetState is{Running:true});
    private static int Cell(double value)=>checked((int)Math.Floor(value*2+.5));
    private static Point3D Point(VolcanoActor actor)=>new((Cell(actor.X)+1)/2,(Cell(actor.Y)+1)/2,actor.Z);
    private static bool Inside(Map map,double x,double y)=>map!=null && map!=Map.Internal
        && double.IsFinite(x+y) && x>0 && y>0 && x<map.Width && y<map.Height;
    private static void Routes()
    {
        MissileFootprints.ValidateSources();var skill=SkillMechanics.For(CombatAbility.Volcano);
        if(skill.Number("srvdofunc")!=123 || skill.Number("localdelay")!=100 || skill.Number("SrcDam")!=0 || skill.Text("anim")!="SC")
        {throw new NotSupportedException("Unsupported Volcano skill route");}
        foreach(var (key,id,size,range,mode,collision,last,delay,hit,slow,knock) in new[]{
            ("volcano",479,3,150,3,1,0,10,0,1,1),("volcano debris 2",481,0,16,0,0,0,0,51,0,0),("volcano small fire",483,2,18,3,0,1,5,0,1,0)})
        {
            int F(string field)=>NecromancerSummons.Source(key,field);
            if(F("pSrvDoFunc")!=(id==479?28:1) || F("pSrvDmgFunc")!=0 || F("pSrvHitFunc")!=hit
                || F("Range")!=range || F("Size")!=size || F("CollideType")!=mode || F("Collision")!=collision
                || F("LastCollide")!=last || F("NextHit")!=(delay>0?1:0) || F("NextDelay")!=delay || F("KnockBack")!=knock
                || F("Vel")!=(id==481?14:0) || F("CanSlow")!=slow || F("SrcDamage")!=0 || F("SrcMissDmg")!=0
                || F("ToHit")!=0 || F("CollideKill")!=0 || F("Town")!=0 || F("SrcTown")!=(id==479?1:0))
            {throw new NotSupportedException("Unsupported Volcano missile route");}
        }
    }
    internal static bool Placement(Map map,Point3D point)
    {
        if(!Inside(map,point.X,point.Y)){return false;}
        // Source mask5/Size3 projects wall/barrier, not actor occupancy or0x40.
        for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
        {
            var cell=new Point3D((point.X*2+dx+1)/2,(point.Y*2+dy+1)/2,point.Z);
            if(!Inside(map,cell.X,cell.Y) || !map.CanFit(cell,1,false,false,true) || LobProjectiles.Barrier(map,cell)){return false;}
        }
        return true;
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!DruidGroundCasting.Aim(owner,selected,ref point,false)){return false;}
        return SkillCombat.Begin(owner,CombatAbility.Volcano,selected,point,now,(p,_,_,aim)=>DruidGroundCasting.Ground(p,aim)&&Placement(p.Map,aim));
    }
    public static bool Release(PlayerMobile owner)
    {
        var session=ClassCombat.For(owner);var point=session.PendingPoint;
        if(!DruidCombat.Available(owner,CombatAbility.Volcano) || !FireGround.Ready(owner,CombatAbility.Volcano)
            || !DruidGroundCasting.Aim(owner,session.PendingTarget,ref point,true)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Volcano);VolcanoActor vent;
        try{vent=Prepare(owner,point,values.Rank,TimedEffects.Now);}
        catch(NotSupportedException){owner.SendMessage(0x22,"Volcano cannot be used in this context yet.");return false;}
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        Add(vent);FireGround.Delay(owner,CombatAbility.Volcano);owner.PlaySound(0x1E5); // Identified cast-audio placeholder.
        return true;
    }
    internal static long Create(PlayerMobile owner,Point3D point,int rank,DateTime at)=>Add(Prepare(owner,point,rank,at));
    private static long Add(VolcanoActor actor)
    {
        Validate(actor);_pool=_pool with{Actors=_pool.Actors.Add(actor.Id,actor),NextId=actor.Id};return actor.Id;
    }
    private static VolcanoActor Prepare(PlayerMobile owner,Point3D point,int rank,DateTime at)
    {
        Routes();
        if(owner==null || Owner(owner.Serial.Value)!=owner || rank is <1 or >255 || at.Kind!=DateTimeKind.Utc
            || !Placement(owner.Map,point))
        {throw new NotSupportedException("Unsupported Volcano supplier/placement");}
        long id=checked(_pool.NextId+1);
        var vent=Capture(owner,id,id,479,rank,point.X,point.Y,point.Z,0,0,150,at);
        // Declared native RNG adapter: one caster draw becomes the emitter's
        // independent low-seed memory. The allocated actor owns another seed.
        return vent with{Memory=unchecked((uint)SeedLow())};
    }
    private static VolcanoActor Capture(PlayerMobile owner,long id,long cast,int missile,int rank,double x,double y,int z,double vx,double vy,int frames,DateTime at)
    {
        var mods=missile==481?new FirestormModifiers(0,0,0,0,0):DruidFirestorm.Modifiers(owner);
        int pmin=0,pmax=0,fmin=0,fmax=0;
        if(missile!=481)
        {
            var values=SkillMechanics.Values(owner,CombatAbility.Volcano,rank);
            pmin=values.PhysicalDamage256(false);pmax=values.PhysicalDamage256(true);
            (fmin,fmax)=DruidFirestorm.Endpoints(values,mods.FireMastery,AmazonSlowMissiles.Capture(owner,true));
        }
        return new(id,cast,owner.Serial.Value,owner.Map.MapID,rank,missile,z,x,y,vx,vy,frames,0,pmin,pmax,fmin,fmax,mods,
            SourceRandom.InitLow(SeedLow()),owner.Serial.Value,at.AddMilliseconds(40),at.AddMilliseconds(frames*40L));
    }
    internal static bool Ready(long cast,uint target,DateTime at)=>!_pool.Leases.TryGetValue((cast,target),out var lease)||lease.UntilUtc<=at;
    internal static BaseCreature Recipient(PlayerMobile owner,VolcanoActor actor)
    {
        int x=Cell(actor.X),y=Cell(actor.Y),size=actor.Missile==479?3:2;
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(Point(actor),2))
        {
            if((actor.Missile!=483 || target.Serial.Value!=actor.LastTarget) && Math.Abs(target.Z-actor.Z)<16
                && CombatHitboxes.SourceTouches(size,CreatureProfiles.For(target).Traits.Size??2,x-target.X*2,y-target.Y*2)
                && Ready(actor.Cast,target.Serial.Value,actor.NextUtc) && ClassCombat.IsHostile(owner,target)
                && !SpellHelper.IsTown(target.Location,owner)){return target;}
        }
        return null;
    }
    private static VolcanoActor Impact(PlayerMobile owner,BaseCreature target,VolcanoActor actor)
    {
        if(!SourceBlocking.TryRate(target,out _,out _)){return actor;}
        var lease=new WeaponFlightLease(actor.Cast,target.Serial.Value,actor.NextUtc.AddMilliseconds((actor.Missile==479?10:5)*40));
        _pool=_pool with{Leases=_pool.Leases.SetItem((lease.Cast,lease.Target),lease)};
        ulong seed=actor.Seed;
        int Roll(int min,int max)=>WeaponMissileDamage.RollValue(min,max,0,n=>(int)SourceRandom.Limited(ref seed,n));
        int physical=Roll(actor.PhysicalMin,actor.PhysicalMax),fire=Roll(actor.FireMin,actor.FireMax);bool guard=Uninterruptible(target);
        var flags=actor.Missile==479?DruidTwister.ReactionFlags(guard,seed):new BoulderKnockbackPlan(guard?1:5,seed);seed=flags.Seed;
        if(!SourceBlocking.TryBlock(target,physical!=0,out bool blocked)){return actor with{Seed=seed};}
        var reaction=MoltenBoulderPackets.ResolveImpact(BoulderImpactKind.Rolling,flags,blocked?1:0,SourceKnockbacks.Rule(target)?.Knockback==true,guard);
        owner.DoHarmful(target,true);int loss=0;
        if(reaction.Damage)
        {
            var raw=DruidMoltenBoulder.Potion(actor.Modifiers,new(physical/256.0,fire/256.0,0,0,0));
            var parts=SkillCombat.MitigatePrepared(owner,target,raw);loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
        }
        if(!guard && target is{Alive:true,Deleted:false})
        {if(!(reaction.Push&&SourceKnockbacks.Begin(owner,target))&&reaction.Damage){CreatureHitRecovery.Apply(target,owner,loss);}}
        target.FixedEffect(0x3709,3,4,0x489,0); // Identified contact placeholder.
        return actor with{LastTarget=target.Serial.Value,Seed=seed};
    }
    private static void Advance(VolcanoActor actor,bool? actions)
    {
        var owner=Owner(actor.Owner);bool active=owner!=null&&Fighting(owner,actor.Map,actions);
        if(owner==null){_pool=_pool with{Actors=_pool.Actors.Remove(actor.Id)};return;}
        var map=Map.Maps[actor.Map];var next=actor with{Remaining=actor.Remaining-1,NextUtc=actor.NextUtc.AddMilliseconds(40)};
        if(!active)
        {
            _pool=_pool with{Actors=next.Remaining==0?_pool.Actors.Remove(actor.Id):_pool.Actors.SetItem(actor.Id,next)};return;
        }
        if(actor.Missile==481)
        {
            // Mode0: neither actor interception nor wall/LOS tests. Native
            // map edge is the declared missing-room boundary, not an impact.
            double x=actor.X+actor.Vx,y=actor.Y+actor.Vy;
            if(!Inside(map,x,y)){_pool=_pool with{Actors=_pool.Actors.Remove(actor.Id)};return;}
            next=next with{X=x,Y=y};
            if(next.Remaining==0)
            {
                _pool=_pool with{Actors=_pool.Actors.Remove(actor.Id)};
                Add(Capture(owner,checked(_pool.NextId+1),actor.Cast,483,actor.Rank,Cell(x)/2.0,Cell(y)/2.0,actor.Z,0,0,18,actor.NextUtc));
                return;
            }
        }
        else
        {
            if(SpellHelper.IsTown(Point(actor),owner)){_pool=_pool with{Actors=_pool.Actors.Remove(actor.Id)};return;}
            if(actor.Missile==479)
            {
                var values=SkillMechanics.Values(owner,CombatAbility.Volcano,actor.Rank);
                var emit=VolcanoPatterns.Emit(Cell(actor.X),Cell(actor.Y),150-actor.Remaining,actor.Memory,actor.Rank,
                    Math.Max(1,(int)values.Eval("calc4")),Math.Max(1,(int)values.Eval("aurarangecalc")));
                if(emit!=null)
                {
                    next=next with{Memory=emit.Memory,Seed=emit.Seed};
                    var flight=VolcanoPatterns.Flight(Cell(actor.X),Cell(actor.Y),emit.X,emit.Y,actor.Rank);
                    if(flight!=null)
                    {Add(Capture(owner,checked(_pool.NextId+1),actor.Cast,481,actor.Rank,actor.X,actor.Y,actor.Z,flight.Vx,flight.Vy,flight.Remaining,actor.NextUtc));}
                }
            }
            // Emission precedes generic expiry; stationary actor contact does not.
            if(next.Remaining==0){_pool=_pool with{Actors=_pool.Actors.Remove(actor.Id)};return;}
            if(Recipient(owner,actor) is{} target){next=Impact(owner,target,next with{NextUtc=actor.NextUtc});}
            else if(LobProjectiles.Barrier(map,Point(actor))){_pool=_pool with{Actors=_pool.Actors.Remove(actor.Id)};return;}
            next=next with{NextUtc=actor.NextUtc.AddMilliseconds(40)};
        }
        _pool=_pool with{Actors=_pool.Actors.SetItem(actor.Id,next)};
        if(next.Remaining%5==0){Effects.SendLocationEffect(Point(next),map,actor.Missile==481?0x36D4:0x398C,3,10,0x489,0);} // Identified vent/air/lava placeholders.
    }
    internal static void Tick(DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Volcano clock must be UTC");}
        while(true)
        {
            var due=_pool.Actors.Values.Select(x=>x.NextUtc).DefaultIfEmpty(DateTime.MaxValue).Min();
            if(due>at){Prune(at);return;}
            foreach(var actor in _pool.Actors.Values.Where(x=>x.NextUtc==due).OrderByDescending(x=>x.Id).ToArray())
            {if(_pool.Actors.ContainsKey(actor.Id)){Advance(actor,actions);}}
        }
    }
    private static void Prune(DateTime at)
    {
        var casts=_pool.Actors.Values.Select(x=>x.Cast).ToHashSet();
        _pool=_pool with{Leases=_pool.Leases.RemoveRange(_pool.Leases.Where(x=>x.Value.UntilUtc<=at||!casts.Contains(x.Key.Item1)).Select(x=>x.Key))};
    }
    private static void Validate(VolcanoActor actor)
    {
        if(actor==null || actor.Id<=0 || actor.Cast<=0 || actor.Cast>actor.Id || !((Serial)actor.Owner).IsMobile
            || actor.Rank is <1 or >255 || actor.Map<0 || actor.Map>=Map.Maps.Length || !Inside(Map.Maps[actor.Map],actor.X,actor.Y)
            || actor.Z is < -128 or >127 || actor.Missile is not(479 or 481 or 483) || actor.Remaining<1
            || actor.Remaining>(actor.Missile==479?150:actor.Missile==481?27:18) || !double.IsFinite(actor.Vx+actor.Vy)
            || actor.Missile!=481 && (actor.Vx!=0||actor.Vy!=0||actor.X*2!=Cell(actor.X)||actor.Y*2!=Cell(actor.Y))
            || actor.Missile==481 && (actor.Vx==0&&actor.Vy==0||Math.Abs(actor.Vx)>.329||Math.Abs(actor.Vy)>.329)
            || actor.Missile==479 && actor.Id!=actor.Cast || actor.Missile!=479 && (actor.Id==actor.Cast||actor.Memory!=0)
            || actor.PhysicalMin<0 || actor.PhysicalMax<actor.PhysicalMin || actor.FireMin<0 || actor.FireMax<actor.FireMin
            || actor.Missile==481 && (actor.PhysicalMin!=0||actor.PhysicalMax!=0||actor.FireMin!=0||actor.FireMax!=0)
            || actor.Modifiers==null || actor.Modifiers.Potion<0 || actor.LastTarget!=0&&!((Serial)actor.LastTarget).IsMobile
            || actor.NextUtc.Kind!=DateTimeKind.Utc || actor.UntilUtc.Kind!=DateTimeKind.Utc
            || actor.UntilUtc-actor.NextUtc!=TimeSpan.FromMilliseconds((actor.Remaining-1)*40L)
            || actor.MotionAdapter!="native-shared-q12-straight-v1")
        {throw new InvalidDataException("Invalid Volcano actor");}
    }
    internal static void Restore(VolcanoSave saved)
    {
        if(saved?.Actors==null || saved.Leases==null || saved.NextId<0 || saved.DelayScope!="cast-target-provisional-2.7")
        {throw new InvalidDataException("Missing/unsupported Volcano state");}
        var actors=ImmutableDictionary<long,VolcanoActor>.Empty;
        foreach(var actor in saved.Actors)
        {
            Validate(actor);
            if(actor.Id>saved.NextId || actors.ContainsKey(actor.Id)){throw new InvalidDataException("Duplicate Volcano identity");}
            actors=actors.Add(actor.Id,actor);
        }
        foreach(var cast in actors.Values.GroupBy(x=>x.Cast))
        {
            var first=cast.First();
            if(actors.TryGetValue(cast.Key,out var root)&&root.Missile!=479 || cast.Any(x=>x.Owner!=first.Owner||x.Map!=first.Map||x.Rank!=first.Rank))
            {throw new InvalidDataException("Mismatched Volcano cast");}
        }
        var casts=actors.Values.Select(x=>x.Cast).ToHashSet();var leases=ImmutableDictionary<(long,uint),WeaponFlightLease>.Empty;
        foreach(var lease in saved.Leases)
        {
            if(lease==null || !casts.Contains(lease.Cast) || !((Serial)lease.Target).IsMobile || lease.UntilUtc.Kind!=DateTimeKind.Utc
                || lease.RetainWithoutFlight || leases.ContainsKey((lease.Cast,lease.Target)))
            {throw new InvalidDataException("Invalid Volcano delay lease");}
            leases=leases.Add((lease.Cast,lease.Target),lease);
        }
        _pool=new(actors,leases,saved.NextId);
    }
}
