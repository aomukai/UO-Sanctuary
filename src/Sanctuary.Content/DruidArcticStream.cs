// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record ArcticPacket(int Minimum256,int Maximum256,int ColdFrames,int Frames);
internal sealed record ArcticMissile(long Id,uint Owner,int Map,int Z,int Rank,double X,double Y,double Vx,double Vy,
    int Remaining,ArcticPacket Damage,uint LastCollision,ulong Seed,SourceStepTrace Steps,DateTime NextUtc,string Fault=null)
{
    public string MotionAdapter{get;init;}="native-shared-q12-sourcecells-v1";
}
internal sealed record ArcticStreamSave(ArcticMissile[] Missiles,long NextId);
public sealed class ArcticStreamProfiles:GenericPersistence
{
    public ArcticStreamProfiles():base("SanctuaryArcticStream",181){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(DruidArcticStream.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Arctic stream version");}DruidArcticStream.Restore(JsonSerializer.Deserialize<ArcticStreamSave>(reader.ReadString()));}
}

// Prepared player230 emissions; this does not register a learned channel.
// Source size/history/countdown and cold packets share existing checked kernels.
// Native room order/Q12/terrain remain adapters,not executable parity claims.
public static class DruidArcticStream
{
    private sealed record Pool(ImmutableDictionary<long,ArcticMissile> Missiles,long NextId);
    private static Pool _pool=new(ImmutableDictionary<long,ArcticMissile>.Empty,0);
    private static readonly HashSet<long> Loaded=new();
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidArcticStream));
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<bool> Allocate=()=>true;
    public static ArcticStreamProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static void Routes()
    {
        var s=SkillMechanics.For(CombatAbility.ArcticBlast);int F(string key)=>NecromancerSummons.Source("arcticblast1",key);
        if(s.Number("srvstfunc")!=11||s.Number("srvdofunc")!=19||s.Text("srvmissilea")!="arcticblast1"||s.Number("SrcDam")!=0
            ||s.Number("Param1")!=35||s.Number("Param2")!=2||s.Number("Param8")!=15||s.Text("calc1")!="ln12/2"||s.Text("EType")!="cold"
            ||F("pSrvDoFunc")!=1||F("pSrvHitFunc")!=0||F("pSrvDmgFunc")!=0||F("Size")!=1||F("CollideType")!=3||F("Collision")!=0
            ||F("LastCollide")!=1||F("NextHit")!=0||F("CollideKill")!=0||F("Pierce")!=0||F("ToHit")!=0||F("GetHit")!=1||F("ReturnFire")!=0
            ||F("CanSlow")!=0||F("Town")!=0||F("SrcTown")!=0||F("SrcDamage")!=0||F("SrcMissDmg")!=0||F("Vel")!=12||F("VelLev")!=0
            ||F("MaxVel")!=12||F("Accel")!=0||F("Activate")!=0||F("ResultFlags")!=4||F("HitClass")!=32||F("DamageRate")!=41)
        {throw new NotSupportedException("Unsupported Arctic stream route");}
    }
    internal static ArcticPacket Capture(SkillValues values,int mastery)
    {
        Routes();
        if(values.Ability!=CombatAbility.ArcticBlast||values.Rank is <1 or >255||mastery<0)
        {throw new NotSupportedException("Unsupported Arctic cold supplier");}
        int Enhance(int raw)=>checked(raw+(int)((long)raw*mastery/100));
        return new(Enhance(values.Damage256(false)),Enhance(values.Damage256(true)),values.DurationFrames,Math.Max(1,(int)values.Eval("calc1")));
    }
    // Aim is absolute source-subtile coordinates,as supplied by player do19.
    // No mana is taken here; the separately checked start wrapper owns payment.
    internal static long Create(PlayerMobile owner,Point2D aim,SkillValues values,DateTime at)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Druid
            ||owner.Map==null||owner.Map==Map.Internal||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Unsupported Arctic stream owner");}
        var packet=Capture(values,NamedElements.MasteryPercent(owner,"cold"));
        var slow=AmazonSlowMissiles.Capture(owner,false);
        if(slow.DamagePercent!=100||CombatPotions.Bonus(owner,"magical-penetration")!=0)
        {throw new NotSupportedException("Arctic modern Slow Missiles damage/potion ordering remains unverified");}
        if(aim.X is <=0 or >65535||aim.Y is <=0 or >65535){return 0;}
        int dx=aim.X-owner.X*2,dy=aim.Y-owner.Y*2;
        if(Math.Abs(dx)>=100||Math.Abs(dy)>=100){return 0;}
        if(dx==0&&dy==0){dx=dy=1;}
        if(!Allocate()){return 0;}
        int velocity=SlowMissilePayload.VelocityFp8(12,0,values.Rank,0x8020,0,false,slow.VelocityPercent);
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);long id=checked(_pool.NextId+1);
        var s=new ArcticMissile(id,owner.Serial.Value,owner.Map.MapID,owner.Z,values.Rank,owner.X,owner.Y,vector.X,vector.Y,
            packet.Frames,packet,owner.Serial.Value,SourceRandom.InitLow(SeedLow()),new([new(owner.X*2,owner.Y*2)]),at.AddMilliseconds(40));
        Validate(s);_pool=new(_pool.Missiles.Add(id,s),id);return id;
    }
    private static ArcticMissile Copy(ArcticMissile s)=>s with{Steps=new(s.Steps.Cells.ToArray())};
    internal static ArcticStreamSave Snapshot()
    {var pool=_pool;return new(pool.Missiles.Values.OrderBy(x=>x.Id).Select(Copy).ToArray(),pool.NextId);}
    private static void Remove(long id){_pool=_pool with{Missiles=_pool.Missiles.Remove(id)};Loaded.Remove(id);}
    internal static BaseCreature Recipient(PlayerMobile owner,ArcticMissile s,SourceMissileCell cell)
    {
        foreach(var target in Map.Maps[s.Map].GetMobilesInRange<BaseCreature>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z),3))
        {
            if(target.Serial.Value==s.LastCollision||Math.Abs(target.Z-s.Z)>=16||!ClassCombat.IsHostile(owner,target)||SpellHelper.IsTown(target.Location,target)){continue;}
            int? size=CreatureProfiles.For(target).Traits.Size;
            if(size is not(>=1 and <=3)){throw new NotSupportedException("Arctic collider has no mapped source size");}
            if(CombatHitboxes.SourceTouches(1,size.Value,cell.X-target.X*2,cell.Y-target.Y*2)){return target;}
        }
        return null;
    }
    internal static ArcticMissile Impact(PlayerMobile owner,BaseCreature target,ArcticMissile s)
    {
        // LastCollide remembers only the most recent recipient; it is not a
        // lifetime hit set or a shared per-cast delay. Cold has no physical block.
        ulong seed=s.Seed;int raw=WeaponMissileDamage.RollValue(s.Damage.Minimum256,s.Damage.Maximum256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        var next=s with{LastCollision=target.Serial.Value,Seed=seed};
        SorceressEffects.MissileHit(owner,target,"arcticblast1"); // ReturnFire0: no armor retaliation.
        if(ClassAvoidance.Missile(target,"arcticblast1")){return next;}
        owner.DoHarmful(target,true);
        double cold=SorceressEffects.Absorb(target,new(0,0,raw/256.0,0,0)).Cold;
        // Mastery was captured once at birth; item pierce/resistance are read
        // by the existing source-owner consumer at contact.
        var reduced=NamedElements.Resolve256(owner,target,NamedElements.To256(cold),"cold",enhance:false,damageRate:41);
        if(reduced.Absorbed256>0){SkillHealth.Heal(target,reduced.Absorbed256);}
        ColdStates.Apply(owner,target,s.Damage.ColdFrames,TimedEffects.TickAt(s.NextUtc));
        int loss=SkillHealth.Damage(owner,target,reduced.Damage256);CreatureHitRecovery.Apply(target,owner,loss);CombatDamage.Finish(target,owner);
        if(owner is not{Alive:true,Deleted:false}||owner.Map?.MapID!=s.Map)
        {next=next with{Fault="Arctic reentrant owner/death/facet chronology remains unverified"};}
        return next;
    }
    private static void Advance(ArcticMissile s,DateTime at,bool? actions)
    {
        bool loaded=Loaded.Remove(s.Id);
        int elapsed=loaded?(int)Math.Min(s.Remaining,(at-s.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1):1;
        int remaining=s.Remaining-elapsed;if(remaining<=0){Remove(s.Id);return;}
        var next=s with{Remaining=remaining,NextUtc=s.NextUtc.AddMilliseconds(40L*elapsed)};
        var owner=World.FindMobile((Serial)s.Owner) as PlayerMobile;
        bool fighting=owner is{Alive:true,Deleted:false}&&owner.Map?.MapID==s.Map&&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            &&profile.Class==DiabloClass.Druid&&(actions??owner.NetState is{Running:true});
        // Modern owner-removal/offline semantics are not inferred from trap AI.
        // Prepared unsupported contexts age harmlessly; restart never backfills.
        if(loaded||!fighting||s.Fault!=null){_pool=_pool with{Missiles=_pool.Missiles.SetItem(s.Id,next)};return;}
        var map=Map.Maps[s.Map];double x=s.X+s.Vx,y=s.Y+s.Vy;
        if(x<=0||y<=0||x>=map.Width||y>=map.Height||SpellHelper.IsTown(new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z),owner)){Remove(s.Id);return;}
        var steps=SourceMissileSteps.Advance(s.X,s.Y,s.Vx,s.Vy,s.Steps);
        foreach(var cell in steps.Cells)
        {
            if(Recipient(owner,s,cell) is{} target){next=Impact(owner,target,next with{NextUtc=s.NextUtc});break;}
            if(DruidFirestorm.CellBlocked(map,s.Z,s.X,s.Y,cell.X,cell.Y)){Remove(s.Id);return;}
        }
        _pool=_pool with{Missiles=_pool.Missiles.SetItem(s.Id,next with{X=x,Y=y,Steps=steps,NextUtc=s.NextUtc.AddMilliseconds(40)})};
        if(remaining%2==0){Effects.SendLocationEffect(new((int)Math.Round(x),(int)Math.Round(y),s.Z),map,0x374A,3,8,0x480,0);} // Identified cold-stream placeholder.
    }
    internal static void Tick(DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Arctic clock must be UTC");}
        while(true)
        {
            var s=_pool.Missiles.Values.Where(x=>x.NextUtc<=at).OrderBy(x=>x.NextUtc).ThenByDescending(x=>x.Id).FirstOrDefault();if(s==null){return;}
            try{Advance(s,at,actions);}
            catch(NotSupportedException e)
            {
                Log.Error(e,"Arctic emission suspended: {Id}",s.Id);
                if(s.Remaining==1){Remove(s.Id);}else{_pool=_pool with{Missiles=_pool.Missiles.SetItem(s.Id,s with{Remaining=s.Remaining-1,NextUtc=s.NextUtc.AddMilliseconds(40),Fault=e.Message})};}
            }
        }
    }
    private static void Validate(ArcticMissile s)
    {
        if(s==null||s.Id<=0||!((Serial)s.Owner).IsMobile||!((Serial)s.LastCollision).IsMobile||s.Rank is <1 or >255||s.MotionAdapter!="native-shared-q12-sourcecells-v1"
            ||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal||s.Z is <-128 or >127
            ||!double.IsFinite(s.X+s.Y+s.Vx+s.Vy)||s.X<=0||s.Y<=0||s.X>=Map.Maps[s.Map].Width||s.Y>=Map.Maps[s.Map].Height
            ||s.Vx==0&&s.Vy==0||Math.Abs(s.Vx)>1||Math.Abs(s.Vy)>1||s.Damage==null||s.Damage.Minimum256<0||s.Damage.Maximum256<s.Damage.Minimum256
            ||s.Damage.Frames!=(35+2*(s.Rank-1))/2||s.Damage.ColdFrames!=100+15*(s.Rank-1)||s.Remaining<1||s.Remaining>s.Damage.Frames
            ||s.NextUtc.Kind!=DateTimeKind.Utc||s.Steps?.Cells is not{Length:>=1 and <=10}||s.Steps.Cells.Any(c=>c.X<=0||c.Y<=0||c.X>=Map.Maps[s.Map].Width*2||c.Y>=Map.Maps[s.Map].Height*2))
        {throw new InvalidDataException("Invalid saved Arctic emission");}
    }
    internal static void Restore(ArcticStreamSave saved)
    {
        if(saved?.Missiles==null||saved.NextId<0){throw new InvalidDataException("Invalid Arctic stream save");}
        var next=ImmutableDictionary<long,ArcticMissile>.Empty;
        foreach(var s in saved.Missiles){Validate(s);if(s.Id>saved.NextId||next.ContainsKey(s.Id)){throw new InvalidDataException("Duplicate Arctic emission");}next=next.Add(s.Id,Copy(s));}
        _pool=new(next,saved.NextId);Loaded.Clear();foreach(var s in saved.Missiles){Loaded.Add(s.Id);}
    }
}
