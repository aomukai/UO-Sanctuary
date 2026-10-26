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

internal sealed record FirestormEmitter(long Id,uint Owner,int Map,int Rank,int Z,int OriginX,int OriginY,
    double X,double Y,double Vx,double Vy,int VelocityFp8,bool Charged,int Ordinal,Point2D[] Path,int Next,
    int CellX,int CellY,int Frame,DateTime NextUtc)
{public string MotionAdapter{get;init;}="native-shared-q12-charged-v1";public bool CellChanged{get;init;}}
internal sealed record FirestormModifiers(int Potion,int FireMastery,int ColdMastery,int LightningMastery,int PoisonMastery);
internal sealed record FirestormPatch(long Id,uint Owner,int Map,int Rank,int X,int Y,int Z,int Min256,int Max256,
    FirestormModifiers Modifiers,ulong Seed,DateTime UntilUtc,DateTime NextUtc);
internal sealed record FirestormSave(FirestormEmitter[] Emitters,FirestormPatch[] Patches,long NextId);
public sealed class FirestormProfiles:GenericPersistence
{
    public FirestormProfiles():base("SanctuaryFirestorm",165){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(DruidFirestorm.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Firestorm version");}DruidFirestorm.Restore(JsonSerializer.Deserialize<FirestormSave>(reader.ReadString()));}
}

// Player225 ground/target SC cast and independent emitter/field consumers.
// Motion/room order retain the declared
// shared Sanctuary adapters; the older do117 positive-count guard is not ported.
public static class DruidFirestorm
{
    private sealed record Pool(ImmutableDictionary<long,FirestormEmitter> Emitters,ImmutableDictionary<long,FirestormPatch> Patches,long NextId);
    private static Pool _pool=new(ImmutableDictionary<long,FirestormEmitter>.Empty,ImmutableDictionary<long,FirestormPatch>.Empty,0);
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static FirestormProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static PlayerMobile Owner(uint serial)=>World.FindMobile((Serial)serial) is PlayerMobile{Alive:true,Deleted:false} p
        && ClassCreation.Profiles.TryGet(p.Serial,out var profile) && profile.Class==DiabloClass.Druid?p:null;
    private static bool Fighting(PlayerMobile p,int map,bool? actions)=>p!=null && p.Map!=null && p.Map!=Map.Internal && p.Map.MapID==map
        && (actions??p.NetState is{Running:true});
    internal static FirestormSave Snapshot()
    {var pool=_pool;return new(pool.Emitters.Values.OrderBy(x=>x.Id).ToArray(),pool.Patches.Values.OrderBy(x=>x.Id).ToArray(),pool.NextId);}
    internal static void Cancel(Mobile owner)
    {
        if(owner==null){return;}
        _pool=_pool with{Emitters=_pool.Emitters.RemoveRange(_pool.Emitters.Where(x=>x.Value.Owner==owner.Serial.Value).Select(x=>x.Key)),
            Patches=_pool.Patches.RemoveRange(_pool.Patches.Where(x=>x.Value.Owner==owner.Serial.Value).Select(x=>x.Key))};
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!DruidGroundCasting.Aim(owner,selected,ref point,false)){return false;}
        return SkillCombat.Begin(owner,CombatAbility.Firestorm,selected,point,now,(p,_,_,aim)=>DruidGroundCasting.Ground(p,aim));
    }
    public static bool Release(PlayerMobile owner)
    {
        var session=ClassCombat.For(owner);var point=session.PendingPoint;
        if(!DruidCombat.Available(owner,CombatAbility.Firestorm) || !FireGround.Ready(owner,CombatAbility.Firestorm)
            || !DruidGroundCasting.Aim(owner,session.PendingTarget,ref point,true)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Firestorm);FirestormEmitter[] prepared;
        try{prepared=Prepare(owner,point,values,TimedEffects.Now);}
        catch(NotSupportedException){owner.SendMessage(0x22,"Firestorm cannot be used in this context yet.");return false;}
        if(prepared.Length==0 || !NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        Commit(prepared);FireGround.Delay(owner,CombatAbility.Firestorm);
        owner.PlaySound(0x1E5); // Identified cast-audio placeholder.
        return true;
    }
    private static long[] Commit(FirestormEmitter[] prepared)
    {
        if(prepared.Length==0){return [];}
        var emitters=_pool.Emitters;foreach(var state in prepared){emitters=emitters.Add(state.Id,state);}
        _pool=_pool with{NextId=prepared[^1].Id,Emitters=emitters};return prepared.Select(x=>x.Id).ToArray();
    }
    internal static long[] Create(PlayerMobile owner,Point3D aim,SkillValues values,DateTime at)=>Commit(Prepare(owner,aim,values,at));
    private static FirestormEmitter[] Prepare(PlayerMobile owner,Point3D aim,SkillValues values,DateTime at)
    {
        if(owner==null || Owner(owner.Serial.Value)!=owner || owner.Map==null || owner.Map==Map.Internal || at.Kind!=DateTimeKind.Utc
            || values.Ability!=CombatAbility.Firestorm || values.Rank is <1 or >255)
        {throw new NotSupportedException("Unsupported Firestorm supplier");}
        int Field(string key)=>NecromancerSummons.Source("firestormmaker",key);
        if(Field("pSrvDoFunc")!=23 || Field("Range")!=40 || Field("Vel")!=8 || Field("VelLev")!=0 || Field("Param1")!=0
            || Field("CollideType")!=8 || Field("Size")!=2 || Field("Accel")!=0 || Field("Town")!=0 || Field("SrcTown")!=0 || values.Eval("calc1")!=3)
        {throw new NotSupportedException("Unsupported Firestorm maker route");}
        int ax=aim.X*2,ay=aim.Y*2,dx=ax-owner.X*2,dy=ay-owner.Y*2;
        if(ax<=0 || ay<=0 || ax>65535 || ay>65535 || Math.Abs(dx)>=100 || Math.Abs(dy)>=100){return [];}
        // Generic factory shifts an exact self aim before the charged callback.
        if(dx==0 && dy==0){ax++;ay++;dx=dy=1;}
        var slow=AmazonSlowMissiles.Capture(owner,true);
        int velocity=SlowMissilePayload.VelocityFp8(8,0,values.Rank,33,0,true,slow.VelocityPercent);
        if(velocity==0){throw new NotSupportedException("Zero-velocity Firestorm path flags are not established");}
        var plans=DruidEmitterPatterns.Launch(3,true,new(owner.X*2,owner.Y*2,ax,ay,true),458,values.Rank);
        var prepared=new FirestormEmitter[plans.Length];int index=0;
        foreach(var plan in plans)
        {
            var init=DruidEmitterPatterns.InitializeCharged(40,plan.AimX,plan.Ordinal);
            var path=plan.Charged?ChargedBolts.Path(init.Seed,ChargedBolts.Heading(dx,dy),init.Distance):[];
            var vector=MissileVectors.VelocityFp8(dx,dy,velocity);long id=checked(_pool.NextId+index+1);
            var state=new FirestormEmitter(id,owner.Serial.Value,owner.Map.MapID,values.Rank,owner.Z,owner.X,owner.Y,
                owner.X,owner.Y,vector.X,vector.Y,velocity,plan.Charged,plan.Ordinal,path,1,owner.X*2,owner.Y*2,0,at.AddMilliseconds(40));
            Validate(state);prepared[index++]=state;
        }
        ValidateFieldRoute();return prepared;
    }
    internal static FirestormModifiers Modifiers(PlayerMobile owner)=>new(CombatPotions.Bonus(owner,"magical-penetration"),
        NamedElements.MasteryPercent(owner,"fire"),NamedElements.MasteryPercent(owner,"cold"),NamedElements.MasteryPercent(owner,"ltng"),NamedElements.MasteryPercent(owner,"pois"));
    internal static (int Min,int Max) Endpoints(SkillValues values,int mastery,SlowMissilePayload slow)
    {
        int Enhance(int raw)=>slow.Damage256((int)Math.Clamp((long)raw+(long)raw*mastery/100,0,int.MaxValue));
        return(Enhance(values.Damage256(false)),Enhance(values.Damage256(true)));
    }
    internal static GearDamage Packet(FirestormModifiers mods,int raw256)
    {
        double fire=raw256/256.0,added=fire*mods.Potion/100;
        return new(0,fire+added*Math.Max(0,100+mods.FireMastery)/100,added*Math.Max(0,100+mods.ColdMastery)/100,
            added*Math.Max(0,100+mods.LightningMastery)/100,added*Math.Max(0,100+mods.PoisonMastery)/100);
    }
    private static void ValidateFieldRoute()
    {
        int Field(string key)=>NecromancerSummons.Source("firestorm",key);
        if(Field("pSrvDoFunc")!=1 || Field("pSrvDmgFunc")!=0 || Field("pSrvHitFunc")!=0 || Field("Range")!=15 || Field("Size")!=2
            || Field("Vel")!=0 || Field("NextHit")!=0 || Field("NextDelay")!=0 || Field("CollideType")!=3 || Field("DamageRate")!=41 || Field("SoftHit")!=1 || Field("Town")!=0 || Field("SrcTown")!=0)
        {throw new NotSupportedException("Unsupported Firestorm field route");}
    }
    private static void AddPatch(PlayerMobile owner,FirestormEmitter maker,int x,int y,DateTime at)
    {
        ValidateFieldRoute();
        var values=SkillMechanics.Values(owner,CombatAbility.Firestorm,maker.Rank);var mods=Modifiers(owner);
        var ends=Endpoints(values,mods.FireMastery,AmazonSlowMissiles.Capture(owner,true));long id=checked(_pool.NextId+1);
        var patch=new FirestormPatch(id,maker.Owner,maker.Map,maker.Rank,x,y,maker.Z,ends.Min,ends.Max,mods,
            SourceRandom.InitLow(SeedLow()),at.AddMilliseconds(15*40),at.AddMilliseconds(40));Validate(patch);
        _pool=_pool with{NextId=id,Patches=_pool.Patches.Add(id,patch)};
    }
    private static bool CellBlocked(Map map,int z,double x,double y,int cx,int cy)
    {
        if(cx<=0 || cy<=0 || cx>=map.Width*2 || cy>=map.Height*2){return true;}
        return !map.LineOfSight(new Point3D((int)Math.Round(x),(int)Math.Round(y),z+12),new Point3D((cx+1)/2,(cy+1)/2,z+12));
    }
    internal static void Tick(DateTime at,bool? actions=null)
    {
        while(true)
        {
            DateTime next=_pool.Emitters.Values.Select(x=>x.NextUtc).Concat(_pool.Patches.Values.Select(x=>x.NextUtc)).DefaultIfEmpty(DateTime.MaxValue).Min();
            if(next>at){return;}
            foreach(var maker in _pool.Emitters.Values.Where(x=>x.NextUtc==next).OrderByDescending(x=>x.Id).ToArray()){Advance(maker,actions);}
            foreach(var patch in _pool.Patches.Values.Where(x=>x.NextUtc==next).OrderByDescending(x=>x.Id).ToArray()){Burn(patch,actions);}
        }
    }
    private static void Advance(FirestormEmitter s,bool? actions)
    {
        var owner=Owner(s.Owner);int frame=s.Frame+1;var next=s with{Frame=frame,NextUtc=s.NextUtc.AddMilliseconds(40)};
        if(owner==null || owner.Map!=Map.Internal && owner.Map?.MapID!=s.Map){_pool=_pool with{Emitters=_pool.Emitters.Remove(s.Id)};return;}
        // Both current rows have Town=0/SrcTown=0. Their own location
        // ends the missile in town; the caster entering town does not.
        if(owner.Map!=Map.Internal && SpellHelper.IsTown(new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z),owner))
        {_pool=_pool with{Emitters=_pool.Emitters.Remove(s.Id)};return;}
        if(!Fighting(owner,s.Map,actions))
        { _pool=_pool with{Emitters=frame>=40?_pool.Emitters.Remove(s.Id):_pool.Emitters.SetItem(s.Id,next)};return;}
        // do23 sees the flag left by the preceding generic movement. Its
        // child is created before this callback moves/decrements again.
        if(s.CellChanged){AddPatch(owner,s,s.CellX,s.CellY,s.NextUtc);}
        double x=s.X+s.Vx,y=s.Y+s.Vy;int waypoint=s.Next;
        if(s.Charged)
        {var moved=ChargedBolts.Advance(s.Path,s.OriginX,s.OriginY,s.X,s.Y,s.Next,SkillGeometry.Tiles(s.VelocityFp8/4096.0));x=moved.X;y=moved.Y;waypoint=moved.Next;}
        int cx=(int)Math.Floor(x*2+.5),cy=(int)Math.Floor(y*2+.5);
        var steps=SourceMissileSteps.Crossings((long)Math.Round(s.X*131072+32768),(long)Math.Round(s.Y*131072+32768),
            (int)Math.Round((x-s.X)*131072),(int)Math.Round((y-s.Y)*131072));
        bool blocked=steps.Cells.Any(cell=>CellBlocked(owner.Map,s.Z,s.X,s.Y,cell.X,cell.Y));
        if(frame>=40 || blocked || s.Charged && waypoint>=s.Path.Length || x<0 || y<0 || x>=owner.Map.Width || y>=owner.Map.Height)
        {_pool=_pool with{Emitters=_pool.Emitters.Remove(s.Id)};return;}
        next=next with{X=x,Y=y,Next=waypoint,CellX=cx,CellY=cy,CellChanged=cx!=s.CellX || cy!=s.CellY};_pool=_pool with{Emitters=_pool.Emitters.SetItem(s.Id,next)};
        if(frame%4==0){Effects.SendLocationEffect(new((int)Math.Round(x),(int)Math.Round(y),s.Z),owner.Map,0x398C,3,10,0x489,0);} // Identified emitter placeholder.
    }
    private static void Burn(FirestormPatch s,bool? actions)
    {
        var owner=Owner(s.Owner);
        if(owner==null || s.NextUtc>=s.UntilUtc || owner.Map!=Map.Internal && owner.Map?.MapID!=s.Map)
        {_pool=_pool with{Patches=_pool.Patches.Remove(s.Id)};return;}
        if(owner.Map!=Map.Internal && SpellHelper.IsTown(new Point3D((s.X+1)/2,(s.Y+1)/2,s.Z),owner))
        {_pool=_pool with{Patches=_pool.Patches.Remove(s.Id)};return;}
        var next=s with{NextUtc=s.NextUtc.AddMilliseconds(40)};_pool=_pool with{Patches=_pool.Patches.SetItem(s.Id,next)};
        if(!Fighting(owner,s.Map,actions)){return;}
        var point=new Point3D((s.X+1)/2,(s.Y+1)/2,s.Z);
        if(!owner.Map.CanFit(point,1,false,false,false)){_pool=_pool with{Patches=_pool.Patches.Remove(s.Id)};return;}
        var projected=new FirePatch(s.Id,s.Owner,CombatAbility.Firestorm,s.Map,s.X/2.0,s.Y/2.0,s.Z,s.Min256,s.Max256,41,s.UntilUtc,s.NextUtc,2){SkillRank=s.Rank};
        if(ImmolationArrows.Recipient(owner,projected) is{} target)
        {
            ulong seed=s.Seed;int raw=WeaponMissileDamage.RollValue(s.Min256,s.Max256,0,bound=>(int)SourceRandom.Limited(ref seed,bound));
            _pool=_pool with{Patches=_pool.Patches.SetItem(s.Id,next with{Seed=seed})};
            if(!ClassAvoidance.Missile(target))
            {
                owner.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(owner,target,Packet(s.Modifiers,raw),damageRate:41);
                SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total)); // No GETHIT, weapon procs, drain or extra19/128 callback.
            }
        }
        if((s.NextUtc.Ticks/TimeSpan.TicksPerMillisecond/40)%5==0){Effects.SendLocationEffect(point,owner.Map,0x398C,3,10,0x489,0);} // Identified burning-patch placeholder.
    }
    private static void Common(long id,uint owner,int map,int rank,int z,DateTime at)
    {
        if(id<=0 || !((Serial)owner).IsMobile || rank is <1 or >255 || map<0 || map>=Map.Maps.Length || Map.Maps[map]==null || Map.Maps[map]==Map.Internal
            || z is <-128 or >127 || at.Kind!=DateTimeKind.Utc){throw new InvalidDataException("Invalid Firestorm identity");}
    }
    private static void Validate(FirestormEmitter s)
    {
        if(s==null){throw new InvalidDataException("Missing Firestorm emitter");}Common(s.Id,s.Owner,s.Map,s.Rank,s.Z,s.NextUtc);
        if(s.Frame is <0 or >=40 || s.Ordinal is <0 or >1 || s.VelocityFp8 is <1 or >1536 || s.MotionAdapter!="native-shared-q12-charged-v1"
            || !double.IsFinite(s.X+s.Y+s.Vx+s.Vy) || Math.Abs(s.Vx)>1 || Math.Abs(s.Vy)>1 || s.X<0 || s.Y<0 || s.X>=Map.Maps[s.Map].Width || s.Y>=Map.Maps[s.Map].Height
            || s.OriginX<=0 || s.OriginY<=0 || s.OriginX>=Map.Maps[s.Map].Width || s.OriginY>=Map.Maps[s.Map].Height || s.Path==null || s.Path.Length!=(s.Charged?21:0) || s.Next<1 || s.Charged && s.Next>s.Path.Length
            || s.CellX!=(int)Math.Floor(s.X*2+.5) || s.CellY!=(int)Math.Floor(s.Y*2+.5))
        {throw new InvalidDataException("Invalid Firestorm emitter path");}
        if(s.Charged && (s.Path[0]!=new Point2D(0,0) || s.Path.Zip(s.Path.Skip(1)).Any(pair=>Math.Abs(pair.First.X-pair.Second.X)>1 || Math.Abs(pair.First.Y-pair.Second.Y)>1 || pair.First==pair.Second)))
        {throw new InvalidDataException("Invalid Firestorm charged waypoints");}
    }
    private static void Validate(FirestormPatch s)
    {
        if(s==null){throw new InvalidDataException("Missing Firestorm field");}Common(s.Id,s.Owner,s.Map,s.Rank,s.Z,s.NextUtc);
        if(s.X<=0 || s.Y<=0 || s.X>=Map.Maps[s.Map].Width*2 || s.Y>=Map.Maps[s.Map].Height*2 || s.Min256<0 || s.Max256<s.Min256
            || s.Modifiers==null || s.Modifiers.Potion<0 || s.UntilUtc.Kind!=DateTimeKind.Utc || s.NextUtc>s.UntilUtc || s.UntilUtc-s.NextUtc>TimeSpan.FromMilliseconds(14*40))
        {throw new InvalidDataException("Invalid Firestorm field state");}
    }
    internal static void Restore(FirestormSave saved)
    {
        if(saved?.Emitters==null || saved.Patches==null || saved.NextId<0){throw new InvalidDataException("Missing Firestorm state");}
        var next=new Pool(ImmutableDictionary<long,FirestormEmitter>.Empty,ImmutableDictionary<long,FirestormPatch>.Empty,saved.NextId);
        foreach(var row in saved.Emitters){Validate(row);if(row.Id>next.NextId || next.Emitters.ContainsKey(row.Id)){throw new InvalidDataException("Duplicate Firestorm emitter");}next=next with{Emitters=next.Emitters.Add(row.Id,row with{Path=row.Path.ToArray()})};}
        foreach(var row in saved.Patches){Validate(row);if(row.Id>next.NextId || next.Emitters.ContainsKey(row.Id) || next.Patches.ContainsKey(row.Id)){throw new InvalidDataException("Duplicate Firestorm field");}next=next with{Patches=next.Patches.Add(row.Id,row)};}
        _pool=next;
    }
}
