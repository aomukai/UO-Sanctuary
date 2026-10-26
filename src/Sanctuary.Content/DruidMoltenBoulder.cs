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

internal sealed record BoulderMissile(long Id,uint Owner,int Map,int Rank,int Missile,int Z,
    double X,double Y,double Vx,double Vy,int CellX,int CellY,bool CellChanged,int Remaining,
    int AimX,int AimY,int Velocity256,int PhysicalMin256,int PhysicalMax256,int FireMin256,int FireMax256,
    FirestormModifiers Modifiers,ulong Seed,int Animation256,DateTime NextUtc)
{
    public string Adapter{get;init;}="native-shared-q12-boulder-v1";
    public SourceStepTrace Steps{get;init;}
}
internal sealed record BoulderSave(BoulderMissile[] Missiles,long NextId);
internal readonly record struct BoulderEndpoints(int PhysicalMin,int PhysicalMax,int FireMin,int FireMax);
public sealed class MoltenBoulderProfiles:GenericPersistence
{
    public MoltenBoulderProfiles():base("SanctuaryMoltenBoulder",166){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(DruidMoltenBoulder.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Molten Boulder version");}
        DruidMoltenBoulder.Restore(JsonSerializer.Deserialize<BoulderSave>(reader.ReadString()));
    }
}

// Source229 native SC cast, emergence, rolling and independently captured fire.
// Native geometry/state adapters and current executable limits remain explicit.
public static class DruidMoltenBoulder
{
    private sealed record Pool(ImmutableDictionary<long,BoulderMissile> Missiles,long NextId);
    private static Pool _pool=new(ImmutableDictionary<long,BoulderMissile>.Empty,0);
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<BaseCreature,KnockbackSourceRule> Model=SourceKnockbacks.Rule;
    // The shared native Leap controller is the currently represented
    // uninterruptible player mode. Additional engine states remain explicit
    // source/native acceptance work rather than inferred from Frozen.
    internal static Func<Mobile,bool> Uninterruptible=BarbarianLeap.Uninterruptible;
    public static MoltenBoulderProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static BoulderSave Snapshot(){var pool=_pool;return new(pool.Missiles.Values.OrderBy(x=>x.Id).Select(x=>x with{Steps=new(x.Steps.Cells.ToArray())}).ToArray(),pool.NextId);}
    internal static void Cancel(Mobile owner)
    {if(owner!=null){_pool=_pool with{Missiles=_pool.Missiles.RemoveRange(_pool.Missiles.Where(x=>x.Value.Owner==owner.Serial.Value).Select(x=>x.Key))};}}
    private static PlayerMobile Owner(uint serial)=>World.FindMobile((Serial)serial) is PlayerMobile{Alive:true,Deleted:false} p
        && ClassCreation.Profiles.TryGet(p.Serial,out var profile) && profile.Class==DiabloClass.Druid?p:null;
    private static bool Fighting(PlayerMobile owner,int map,bool? actions)=>owner?.Map!=null && owner.Map!=Map.Internal
        && owner.Map.MapID==map && (actions??owner.NetState is{Running:true});
    private static string Key(int missile)=>missile switch{453=>"moltenboulderemerge",452=>"moltenboulder",455=>"moltenboulderfirepath",_=>throw new NotSupportedException("Unmapped Boulder missile")};
    private static void Routes()
    {
        foreach(int id in new[]{453,452,455})
        {
            string key=Key(id);int F(string name)=>NecromancerSummons.Source(key,name);
            if(F("pSrvDoFunc")!=(id==453?1:id==452?6:5) || F("pSrvHitFunc")!=(id==453?48:id==452?47:0)
                || F("pSrvDmgFunc")!=(id==452?14:0) || F("Range")!=(id==453?5:id==452?50:37)
                || F("Vel")!=(id==453?3:id==452?8:0) || F("CollideType")!=8 || F("NextHit")!=0 || F("ToHit")!=0
                || F("CollideFriend")!=0 || F("Pierce")!=0 || F("LastCollide")!=(id==453?1:0)
                || F("AlwaysExplode")!=(id==455?0:1) || F("Town")!=0 || F("SrcTown")!=0)
            {throw new NotSupportedException("Unsupported Boulder row context");}
        }
    }
    internal static BoulderEndpoints Endpoints(SkillValues values,int missile,int mastery,SlowMissilePayload slow)
    {
        if(missile==453){return default;}
        if(missile==452)
        {
            var fire=DruidFirestorm.Endpoints(values,mastery,slow);
            return new(values.PhysicalDamage256(false),values.PhysicalDamage256(true),fire.Min,fire.Max);
        }
        if(missile!=455){throw new NotSupportedException("Unmapped Boulder damage supplier");}
        int synergy=NecromancerSummons.Formula("moltenboulderfirepath","EDmgSymPerCalc",values);
        return new(0,0,slow.Damage256(FireGround.MissileDamage256("moltenboulderfirepath",values.Rank,synergy,false)),
            slow.Damage256(FireGround.MissileDamage256("moltenboulderfirepath",values.Rank,synergy,true)));
    }
    internal static long Create(PlayerMobile owner,Point3D aim,int rank,DateTime at)
    {
        Routes();
        if(owner==null || Owner(owner.Serial.Value)!=owner || owner.Map==null || owner.Map==Map.Internal
            || rank is <1 or >255 || at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Unsupported Boulder supplier");}
        var request=new DruidEmitterRequest(33,453,owner.X*2,owner.Y*2,aim.X*2,aim.Y*2,false,0,0,rank);
        return Add(owner,request,owner.Z,at);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!DruidGroundCasting.Aim(owner,selected,ref point,false)){return false;}
        return SkillCombat.Begin(owner,CombatAbility.MoltenBoulder,selected,point,now,(p,_,_,aim)=>DruidGroundCasting.Ground(p,aim));
    }
    public static bool Release(PlayerMobile owner)
    {
        var session=ClassCombat.For(owner);var point=session.PendingPoint;
        if(!DruidCombat.Available(owner,CombatAbility.MoltenBoulder) || !FireGround.Ready(owner,CombatAbility.MoltenBoulder)
            || !DruidGroundCasting.Aim(owner,session.PendingTarget,ref point,true)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.MoltenBoulder);BoulderMissile prepared;
        try
        {
            Routes();
            prepared=Prepare(owner,new(33,453,owner.X*2,owner.Y*2,point.X*2,point.Y*2,false,0,0,values.Rank),owner.Z,TimedEffects.Now);
        }
        catch(NotSupportedException){owner.SendMessage(0x22,"Molten Boulder cannot be used in this context yet.");return false;}
        if(prepared==null || !NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        Commit(prepared);FireGround.Delay(owner,CombatAbility.MoltenBoulder);owner.PlaySound(0x1E5); // Identified cast-audio placeholder.
        return true;
    }
    private static long Add(PlayerMobile owner,DruidEmitterRequest request,int z,DateTime at)
    {
        var prepared=Prepare(owner,request,z,at);return prepared==null?0:Commit(prepared);
    }
    private static long Commit(BoulderMissile state)
    {
        _pool=_pool with{Missiles=_pool.Missiles.Add(state.Id,state),NextId=state.Id};return state.Id;
    }
    private static BoulderMissile Prepare(PlayerMobile owner,DruidEmitterRequest request,int z,DateTime at)
    {
        string key=Key(request.Missile);int missile=request.Missile;
        int velocity=SlowMissilePayload.VelocityFp8(NecromancerSummons.Source(key,"Vel"),0,request.Rank,request.Flags,0,true,AmazonSlowMissiles.Capture(owner,true).VelocityPercent);
        int dx=(request.Flags&32)!=0?request.AimX-request.X:0,dy=(request.Flags&32)!=0?request.AimY-request.Y:0;
        if(velocity!=0 && (Math.Abs(dx)>=100 || Math.Abs(dy)>=100)){return null;}
        if(request.X<=0 || request.Y<=0 || request.X>=owner.Map.Width*2 || request.Y>=owner.Map.Height*2){return null;}
        var point=new Point3D((request.X+1)/2,(request.Y+1)/2,z);
        if(SpellHelper.IsTown(point,owner)){return null;}
        if(velocity!=0 && dx==0 && dy==0){dx=dy=1;}
        var vector=velocity==0?(X:0.0,Y:0.0):MissileVectors.VelocityFp8(dx,dy,velocity);
        var values=SkillMechanics.Values(owner,CombatAbility.MoltenBoulder,request.Rank);var mods=DruidFirestorm.Modifiers(owner);
        var slow=AmazonSlowMissiles.Capture(owner,true);
        // The shared Slow Missiles payload scales elemental endpoints, not
        // physical damage. Skill fire mastery and unmastered field fire differ.
        var ends=Endpoints(values,missile,mods.FireMastery,slow);
        long id=checked(_pool.NextId+1);
        var state=new BoulderMissile(id,owner.Serial.Value,owner.Map.MapID,request.Rank,missile,z,request.X/2.0,request.Y/2.0,
            vector.X,vector.Y,request.X,request.Y,false,request.RangeOverride??NecromancerSummons.Source(key,"Range"),
            request.X+dx,request.Y+dy,velocity,ends.PhysicalMin,ends.PhysicalMax,ends.FireMin,ends.FireMax,mods,SourceRandom.InitLow(SeedLow()),0,at.AddMilliseconds(40))
            {Steps=new([new(request.X,request.Y)])};
        Validate(state);return state;
    }
    private static void Remove(long id)=>_pool=_pool with{Missiles=_pool.Missiles.Remove(id)};
    internal static void Tick(DateTime at,bool? actions=null)
    {
        while(true)
        {
            var next=_pool.Missiles.Values.Select(x=>x.NextUtc).DefaultIfEmpty(DateTime.MaxValue).Min();if(next>at){return;}
            foreach(var state in _pool.Missiles.Values.Where(x=>x.NextUtc==next).OrderByDescending(x=>x.Id).ToArray())
            {if(_pool.Missiles.ContainsKey(state.Id)){Advance(state,actions);}}
        }
    }
    private static void Advance(BoulderMissile s,bool? actions)
    {
        var owner=Owner(s.Owner);int remaining=s.Remaining-1;
        if(owner==null || owner.Map!=Map.Internal && owner.Map?.MapID!=s.Map){Remove(s.Id);return;}
        if(owner.Map!=Map.Internal && SpellHelper.IsTown(new Point3D((s.CellX+1)/2,(s.CellY+1)/2,s.Z),owner)){Remove(s.Id);return;}
        var next=s with{Remaining=remaining,NextUtc=s.NextUtc.AddMilliseconds(40)};
        if(!Fighting(owner,s.Map,actions))
        {if(remaining<=0){Remove(s.Id);}else{_pool=_pool with{Missiles=_pool.Missiles.SetItem(s.Id,next)};}return;}
        if(s.Missile==452 && s.CellChanged)
        {foreach(var child in MoltenBoulderPackets.Trail(true,true,true,true,455,8,s.CellX,s.CellY,s.Rank,1).Requests){Add(owner,child,s.Z,s.NextUtc);}}
        if(s.Missile==455)
        {
            // Explicit persisted server sequence frame. No invented animation
            // increment: the complete do5 callback owns only these mutations.
            var anim=MoltenBoulderPackets.Animate(true,true,s.Remaining,s.Animation256,s.Seed,12,36,1);
            next=next with{Animation256=anim.Animation256,Seed=anim.Seed};
        }
        double x=s.X+s.Vx,y=s.Y+s.Vy;
        var steps=SourceMissileSteps.Advance(s.X,s.Y,s.Vx,s.Vy,s.Steps);
        bool blocked=x<=0 || y<=0 || x>=owner.Map.Width || y>=owner.Map.Height;
        foreach(var cell in steps.Cells)
        {
            var point=new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z);
            if(cell.X<=0 || cell.Y<=0 || cell.X>=owner.Map.Width*2 || cell.Y>=owner.Map.Height*2
                || !owner.Map.LineOfSight(new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z+12),new Point3D(point.X,point.Y,s.Z+12)))
            {blocked=true;break;}
        }
        // The shared native adapter keeps the previous valid position on a
        // wall. Exact D2 saved-step/room movement remains an acceptance limit.
        if(!blocked)
        {
            int cx=(int)Math.Floor(x*2+.5),cy=(int)Math.Floor(y*2+.5);
            next=next with{X=x,Y=y,CellX=cx,CellY=cy,CellChanged=cx!=s.CellX || cy!=s.CellY,Steps=steps};
        }
        bool terminal=blocked || remaining<=0;
        if(!terminal && (s.Remaining&3)==0)
        {Effects.SendLocationEffect(new((next.CellX+1)/2,(next.CellY+1)/2,s.Z),owner.Map,s.Missile==455?0x398C:0x36D4,3,10,0x489,0);} // Identified field/travel placeholders.
        Mobile target=null;
        if(!terminal)
        {
            // Current native first-collider adapter. Ground remains a single
            // collider per frame; rolling repeats on later frames if it stays.
            foreach(var cell in next.Steps.Cells)
            {
                var patch=new FirePatch(s.Id,s.Owner,CombatAbility.MoltenBoulder,s.Map,cell.X/2.0,cell.Y/2.0,s.Z,0,0,0,s.NextUtc,s.NextUtc,NecromancerSummons.Source(Key(s.Missile),"Size"));
                target=ImmolationArrows.Recipient(owner,patch);if(target!=null){break;}
            }
        }
        if(target==null && !terminal)
        {_pool=_pool with{Missiles=_pool.Missiles.SetItem(s.Id,next)};return;}
        if(s.Missile==452 && target is BaseCreature creature && Model(creature) is not{Small:not null,Large:not null})
        {Remove(s.Id);owner.SendMessage(0x22,"Molten Boulder cannot affect that creature yet.");return;}
        bool large=target is BaseCreature c && Model(c)?.Large==true;
        var contact=new BoulderContactInput(true,true,true,target==null?null:target is BaseCreature?1:0,true,large,true,0,7,1,455,
            next.CellX,next.CellY,s.Rank,2,5,0,0,true);
        var route=MoltenBoulderPackets.Route(s.Missile,target!=null,false,true,true,terminal,contact,next.CellX,next.CellY,s.AimX,s.AimY,s.Rank);
        ulong seed=next.Seed;
        if(route.Contact?.Area is{} area)
        {
            var raw=Roll(next,ref seed);
            foreach(var victim in SkillCombat.Targets(owner,new Point3D((next.CellX+1)/2,(next.CellY+1)/2,s.Z),SkillGeometry.Tiles(area.Radius)))
            {Impact(owner,victim,next,raw,false,ref seed);}
        }
        if(route.DirectDamage && target!=null)
        {var raw=Roll(next,ref seed);Impact(owner,target,next,raw,s.Missile==452,ref seed);}
        foreach(var request in route.Requests){Add(owner,request,s.Z,s.NextUtc);}
        if(route.Result==2){Remove(s.Id);}else{_pool=_pool with{Missiles=_pool.Missiles.SetItem(s.Id,next with{Seed=seed})};}
        // All visible art here is explicitly provisional; mechanical children
        // retain their independent lifetimes when this marker ends.
        if(s.Missile!=455){Effects.SendLocationEffect(new((next.CellX+1)/2,(next.CellY+1)/2,s.Z),owner.Map,0x36BD,3,10,0x489,0);}
    }
    private static GearDamage Roll(BoulderMissile s,ref ulong seed)
    {
        ulong state=seed;
        int physical=WeaponMissileDamage.RollValue(s.PhysicalMin256,s.PhysicalMax256,0,bound=>(int)SourceRandom.Limited(ref state,bound));
        int fire=WeaponMissileDamage.RollValue(s.FireMin256,s.FireMax256,0,bound=>(int)SourceRandom.Limited(ref state,bound));seed=state;
        return new(physical/256.0,fire/256.0,0,0,0);
    }
    private static GearDamage Potion(FirestormModifiers mods,GearDamage raw)
    {
        double added=raw.Total*mods.Potion/100;
        return raw with{Fire=raw.Fire+added*Math.Max(0,100+mods.FireMastery)/100,
            Cold=added*Math.Max(0,100+mods.ColdMastery)/100,Lightning=added*Math.Max(0,100+mods.LightningMastery)/100,
            Poison=added*Math.Max(0,100+mods.PoisonMastery)/100};
    }
    private static void Impact(PlayerMobile owner,Mobile target,BoulderMissile s,GearDamage raw,bool knockback,ref ulong seed)
    {
        var kind=knockback?BoulderImpactKind.Rolling:s.Missile==455?BoulderImpactKind.Ground:BoulderImpactKind.Area;
        var model=target is BaseCreature creature?Model(creature):null;bool uninterruptible=Uninterruptible(target);
        var initial=MoltenBoulderPackets.ImpactFlags(kind,model?.Small==true,model?.Large==true,uninterruptible,seed);seed=initial.Seed;
        int defense=ClassAvoidance.Missile(target,knockback)?2:0;
        if(defense==0 && kind==BoulderImpactKind.Rolling && raw.Physical>0 && target is BaseCreature c)
        {
            // Copied area wrapper passes block-check0 even with physical
            // damage. Only direct rolling hits check a monster's shield.
            if(!SourceBlocking.TryBlock(c,true,out bool blocked)){return;}
            if(blocked){defense=1;}
        }
        var plan=MoltenBoulderPackets.ResolveImpact(kind,initial,defense,model?.Knockback==true,uninterruptible);
        owner.DoHarmful(target,true);
        int loss=0;
        if(plan.Damage)
        {
            var parts=SkillCombat.MitigatePrepared(owner,target,Potion(s.Modifiers,raw),damageRate:s.Missile==455?41:0);
            loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
        }
        if(!uninterruptible && target is BaseCreature survivor && s.Missile!=455)
        {
            // A blocked hit may still request KB. The source mode handler
            // chooses knockback before its block reaction, after health events.
            if(!(plan.Push && SourceKnockbacks.Begin(owner,survivor)) && plan.Damage){CreatureHitRecovery.Apply(survivor,owner,loss);}
        }
    }
    private static void Validate(BoulderMissile s)
    {
        if(s==null || s.Id<=0 || !((Serial)s.Owner).IsMobile || s.Rank is <1 or >255 || s.Missile is not(452 or 453 or 455)
            || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal || s.Z is <-128 or >127
            || !double.IsFinite(s.X+s.Y+s.Vx+s.Vy) || s.X<=0 || s.Y<=0 || s.X>=Map.Maps[s.Map].Width || s.Y>=Map.Maps[s.Map].Height
            || Math.Abs(s.Vx)>1 || Math.Abs(s.Vy)>1 || s.CellX!=(int)Math.Floor(s.X*2+.5) || s.CellY!=(int)Math.Floor(s.Y*2+.5)
            || s.Remaining<1 || s.Remaining>(s.Missile==453?5:s.Missile==452?50:37) || s.Velocity256<0 || s.Velocity256>(s.Missile==453?576:s.Missile==452?1536:0)
            || s.PhysicalMin256<0 || s.PhysicalMax256<s.PhysicalMin256 || s.FireMin256<0 || s.FireMax256<s.FireMin256
            || s.Modifiers==null || s.Modifiers.Potion<0 || s.Animation256<0 || s.Animation256>36*256 || s.NextUtc.Kind!=DateTimeKind.Utc
            || s.Adapter!="native-shared-q12-boulder-v1" || s.Steps?.Cells is not{Length:>=1 and <=10}
            || s.Steps.Cells.Any(c=>c.X<=0 || c.Y<=0 || c.X>=Map.Maps[s.Map].Width*2 || c.Y>=Map.Maps[s.Map].Height*2))
        {throw new InvalidDataException("Invalid Molten Boulder state");}
    }
    internal static void Restore(BoulderSave saved)
    {
        if(saved?.Missiles==null || saved.NextId<0){throw new InvalidDataException("Missing Molten Boulder state");}
        var pool=new Pool(ImmutableDictionary<long,BoulderMissile>.Empty,saved.NextId);
        foreach(var state in saved.Missiles)
        {Validate(state);if(state.Id>pool.NextId || pool.Missiles.ContainsKey(state.Id)){throw new InvalidDataException("Duplicate Molten Boulder identity");}pool=pool with{Missiles=pool.Missiles.Add(state.Id,state with{Steps=new(state.Steps.Cells.ToArray())})};}
        _pool=pool;
    }
}
