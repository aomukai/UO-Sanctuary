// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record SentryLightningState(long Id,uint Actor,uint Owner,int Map,int Z,int Rank,
    double X,double Y,double Vx,double Vy,int VelocityFp8,int Frame,int Frames,int Minimum256,int Maximum256,
    int Pierce,SlowMissilePayload Slow,ulong Seed,uint LastCollision,SourceStepTrace Steps,DateTime NextUtc,string Fault=null)
{
    public string MotionAdapter{get;init;}="native-shared-q12-sourcecells-v1";
    // Missing in version0 saves created by the original lightning-only pool.
    public CombatAbility Ability{get;init;}=CombatAbility.LightningSentry;
    public int HardWake{get;init;}
}
internal sealed record SentryLightningSave(SentryLightningState[] Bolts,long NextId);
public sealed class SentryLightningProfiles:GenericPersistence
{
    private SentryLightningSave _snapshot;
    public SentryLightningProfiles():base("SanctuarySentryLightning",163){}
    public override void Serialize(){_snapshot=AssassinSentryLightning.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown sentry lightning version");}AssassinSentryLightning.Restore(JsonSerializer.Deserialize<SentryLightningSave>(reader.ReadString()));}
}

// Shared straight stationary-trap flights. The lightning class/profile names
// retain their saved identity. NPC313 uses generic flags33; NPC311's custom
// do95 uses flags37 and replaces the initial range with20+hard Wake (cap255).
// Q12/cell/terrain geometry reuses existing native adapters; modern executable
// room enumeration, seed allocation and final retired-owner lifetime stay open.
public static class AssassinSentryLightning
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AssassinSentryLightning));
    private static readonly Dictionary<long,SentryLightningState> Bolts=new();
    private static long _nextId;
    // Native allocation stream. Each projectile retains its source LCG afterward.
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static void Configure()=>_ = new SentryLightningProfiles();
    internal static DateTime NextFrame=>Bolts.Values.Select(x=>x.NextUtc).DefaultIfEmpty(DateTime.MaxValue).Min();
    internal static SentryLightningState State(long id)=>Bolts.GetValueOrDefault(id);
    private static Point2D Aim(AssassinSentryBody body,uint targetSerial)
    {
        Point2D? targetPoint=null;
        if(World.FindMobile((Serial)targetSerial) is{Deleted:false} target && target!=body)
        {
            if(target.Map!=body.Map){throw new NotSupportedException("Cross-map sentry pending target is not mapped");}
            targetPoint=new(target.X*2,target.Y*2);
        }
        return AssassinSentryAim.Resolve(body.Prepared.CachedAim,targetPoint);
    }
    internal static void Dispatch(AssassinSentryBody body,uint targetSerial)=>Create(body,Aim(body,targetSerial));
    internal static long DispatchDeath(AssassinSentryBody body,uint targetSerial)=>CreateStraight(body,Aim(body,targetSerial),CombatAbility.DeathSentry);
    internal static InfernoEmission DispatchInferno(AssassinSentryBody body,uint targetSerial)
    {
        var aim=Aim(body,targetSerial);
        if(aim.X==0 || aim.Y==0){return InfernoEmission.NoCoordinates;}
        return CreateInferno(body,aim)>0?InfernoEmission.Success:InfernoEmission.AllocationFailed;
    }
    internal static long Create(AssassinSentryBody body,Point2D aim)
        =>CreateStraight(body,aim,CombatAbility.LightningSentry);
    internal static long CreateInferno(AssassinSentryBody body,Point2D aim)
        =>CreateStraight(body,aim,CombatAbility.WakeOfInferno);
    private static string Missile(CombatAbility ability)=>ability switch
    {CombatAbility.LightningSentry=>"sentrylightningbolt",CombatAbility.DeathSentry=>"sentrylightningbolt2",CombatAbility.WakeOfInferno=>"inferno sentry 1",_=>throw new NotSupportedException("Unsupported straight sentry missile")};
    private static long CreateStraight(AssassinSentryBody body,Point2D aim,CombatAbility ability)
    {
        if(body is not{Alive:true,Deleted:false} || body.Prepared is not{ControllerFault:null} c || c.Creation.Ability!=ability)
        {throw new InvalidOperationException("Invalid live straight sentry supplier");}
        bool inferno=ability==CombatAbility.WakeOfInferno;string missile=Missile(ability);
        int Field(string name)=>NecromancerSummons.Source(missile,name);
        if(Field("Size")!=(inferno?1:2) || Field("CollideType")!=3 || Field("LastCollide")!=1 || Field("NextHit")!=0
            || Field("CollideKill")!=0 || Field("Pierce")!=(ability==CombatAbility.DeathSentry?1:0) || Field("ToHit")!=0 || Field("pSrvDoFunc")!=1
            || Field("pSrvHitFunc")!=0 || Field("pSrvDmgFunc")!=0 || Field("SrcDamage")!=0 || Field("Accel")!=0
            || Field("Vel")!=(inferno?12:30) || Field("LevVel")!=0 || Field("ResultFlags")!=4 || Field("DamageRate")!=(inferno?82:0))
        {throw new NotSupportedException("Unsupported straight sentry missile route");}
        // Death missile641 has Pierce=1, but MISSILES_Create/MISSMODE only
        // allocate/use a pierce budget when its NPC owner has ITEM_PIERCE or
        // SKILL_PIERCE. The seven captured Death passives contain neither;
        // this supplier has zero chance. Do not inherit player pierce gear.
        // The generic wrapper rejects either zero absolute target coordinate.
        if(aim.X==0 || aim.Y==0){return 0;}
        if(aim.X is <0 or >65535 || aim.Y is <0 or >65535){throw new NotSupportedException("Sentry target is outside uint16 source coordinates");}
        var slow=AmazonSlowMissiles.Capture(body,Field("CanSlow")!=0);
        int velocity=SlowMissilePayload.VelocityFp8(Field("Vel"),Field("LevVel"),c.Creation.Rank,inferno?37:33,inferno?Field("Vel"):0,true,slow.VelocityPercent);
        if(velocity==0){throw new NotSupportedException("Zero-velocity sentry path flags are not mapped");}
        int dx=aim.X-body.X*2,dy=aim.Y-body.Y*2;
        if(velocity!=0 && (Math.Abs(dx)>=100 || Math.Abs(dy)>=100)){return 0;}
        if(velocity!=0 && dx==0 && dy==0){dx=dy=1;}
        var v=MissileVectors.VelocityFp8(dx,dy,velocity);var ends=inferno?AssassinSentryCreation.Fire(c.Creation):AssassinSentryCreation.Lightning(c.Creation);
        int hardWake=inferno?c.Creation.Skill("Wake of Fire Sentry"):0;
        int frames=inferno?Math.Clamp(20+hardWake,1,255):NecromancerSummons.Formula(missile,"Range",AssassinSentryCreation.Values(c.Creation));
        long id=checked(++_nextId);
        var state=new SentryLightningState(id,body.Serial.Value,c.Owner,c.Map,c.Position.Z,c.Creation.Rank,
            body.X,body.Y,v.X,v.Y,velocity,0,frames,slow.Damage256(ends.Min),slow.Damage256(ends.Max),
            c.Creation.Passive(inferno?"passive_fire_pierce":"passive_ltng_pierce"),slow,SourceRandom.InitLow(SeedLow()),0,
            new([new(body.X*2,body.Y*2)]),c.NextFrameUtc.AddMilliseconds(40)){Ability=ability,HardWake=hardWake};
        Validate(state);Bolts.Add(id,state);return id;
    }
    private static void Validate(SentryLightningState s)
    {
        if(s==null || s.Id<=0 || !((Serial)s.Actor).IsMobile || !((Serial)s.Owner).IsMobile || s.Rank is <1 or >255
            || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal || s.Z is <-128 or >127
            || s.Ability is not(CombatAbility.LightningSentry or CombatAbility.WakeOfInferno or CombatAbility.DeathSentry) || s.HardWake is <0 or >255
            || s.Ability is CombatAbility.LightningSentry or CombatAbility.DeathSentry && (s.HardWake!=0 || s.Frames!=10+2*s.Rank || s.VelocityFp8>5760)
            || s.Ability==CombatAbility.WakeOfInferno && (s.Frames!=Math.Clamp(20+s.HardWake,1,255) || s.VelocityFp8>2304)
            || s.Frame<0 || s.Frame>=s.Frames || s.VelocityFp8<1
            || !double.IsFinite(s.X) || !double.IsFinite(s.Y) || !double.IsFinite(s.Vx) || !double.IsFinite(s.Vy)
            || s.X<0 || s.Y<0 || s.X>=Map.Maps[s.Map].Width || s.Y>=Map.Maps[s.Map].Height
            || Math.Abs(s.Vx)>1 || Math.Abs(s.Vy)>1 || s.Minimum256<0 || s.Maximum256<s.Minimum256
            || s.Slow.VelocityPercent is <0 or >100 || s.Slow.DamagePercent is <0 or >100
            || s.Steps?.Cells is not{Length:>=1 and <=10} || s.Steps.Cells.Any(p=>p.X is <0 or >65535 || p.Y is <0 or >65535)
            || s.LastCollision!=0&&!((Serial)s.LastCollision).IsMobile || s.NextUtc.Kind!=DateTimeKind.Utc
            || s.MotionAdapter!="native-shared-q12-sourcecells-v1")
        {throw new InvalidDataException("Invalid saved sentry lightning");}
    }
    internal static void TickFrame(DateTime at,bool? actions=null)
    {
        foreach(var state in Bolts.Values.Where(x=>x.NextUtc==at).OrderByDescending(x=>x.Id).ToArray())
        {
            try{Advance(state,actions);}
            catch(Exception e)
            {
                if(state.Frame+1>=state.Frames){Bolts.Remove(state.Id);}
                else{Bolts[state.Id]=state with{Fault=e.Message,Frame=state.Frame+1,NextUtc=state.NextUtc.AddMilliseconds(40)};}
                Log.Error(e,"Sentry straight projectile suspended: {Id}",state.Id);
            }
        }
    }
    private static void Advance(SentryLightningState s,bool? actions)
    {
        int frame=s.Frame+1;var next=s with{Frame=frame,NextUtc=s.NextUtc.AddMilliseconds(40)};
        var body=World.FindMobile((Serial)s.Actor) as AssassinSentryBody;
        bool live=body is{Alive:true,Deleted:false}&&body.Prepared.Owner==s.Owner&&body.Map?.MapID==s.Map&&body.Prepared.Creation.Ability==s.Ability;
        // The source can still resolve released missiles from a registered
        // retired NPC. Holding the actor at the unresolved final-removal
        // boundary must not freeze its already-created Death Sentry bolts.
        bool retired=s.Ability==CombatAbility.DeathSentry && body?.Prepared is{DeathControl.Mode:0,ControllerFault:AssassinDeathSentryControl.RetirementFault};
        bool fighting=live&&(body.Prepared.ControllerFault==null||retired)&&(actions??AssassinTrapRoster.Fighting(s.Owner));
        if(!fighting || s.Fault!=null)
        {if(frame>=s.Frames){Bolts.Remove(s.Id);}else{Bolts[s.Id]=next;}return;}
        next=next with{X=s.X+s.Vx,Y=s.Y+s.Vy};
        // Complete do1 moves first, then decrements/ends before collision at0.
        if(frame>=s.Frames || next.X<0 || next.Y<0 || next.X>=body.Map.Width || next.Y>=body.Map.Height)
        {Bolts.Remove(s.Id);return;}
        next=next with{Steps=SourceMissileSteps.Advance(s.X,s.Y,s.Vx,s.Vy,s.Steps)};
        if(World.FindMobile((Serial)s.Owner) is not PlayerMobile owner){throw new NotSupportedException("Missing sentry allegiance owner");}
        foreach(var cell in next.Steps.Cells)
        {
            if(cell.X<=0 || cell.Y<=0 || cell.X>=body.Map.Width*2 || cell.Y>=body.Map.Height*2){Bolts.Remove(s.Id);return;}
            BaseCreature victim=null;
            foreach(var candidate in body.Map.GetMobilesInRange<BaseCreature>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z),3))
            {
                if(candidate.Serial.Value==s.LastCollision || Math.Abs(candidate.Z-s.Z)>=16 || !body.TrapTarget(owner,candidate)
                    || SpellHelper.IsTown(candidate.Location,candidate)){continue;}
                int? size=CreatureProfiles.For(candidate).Traits.Size;
                if(size is not(>=1 and <=3)){throw new NotSupportedException("Sentry collider has no mapped source size");}
                if(CombatHitboxes.SourceTouches(s.Ability==CombatAbility.WakeOfInferno?1:2,size.Value,cell.X-candidate.X*2,cell.Y-candidate.Y*2)){victim=candidate;break;}
            }
            if(victim!=null)
            {
                // Source router returns after one recipient this phase. The
                // non-killing projectile continues with most-recent history.
                bool inferno=s.Ability==CombatAbility.WakeOfInferno;
                var hit=AssassinSentryBolts.Impact(body,victim,Missile(s.Ability),s.Minimum256,s.Maximum256,s.Pierce,s.Seed,inferno?"fire":"ltng",inferno?82:0);
                Bolts[s.Id]=next with{LastCollision=victim.Serial.Value,Seed=hit.Seed};return;
            }
            if(!body.Map.LineOfSight(new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z+12),new((cell.X+1)/2,(cell.Y+1)/2,s.Z+12)))
            {Bolts.Remove(s.Id);return;}
        }
        Bolts[s.Id]=next;
        if(frame%2==0){bool inferno=s.Ability==CombatAbility.WakeOfInferno;Effects.SendLocationEffect(new((int)Math.Round(next.X),(int)Math.Round(next.Y),s.Z),body.Map,inferno?0x36D4:0x3818,3,10,inferno?0x489:0x482,0);} // Identified placeholders.
    }
    internal static SentryLightningSave Snapshot()=>new(Bolts.Values.OrderBy(x=>x.Id).ToArray(),_nextId);
    internal static void Restore(SentryLightningSave saved)
    {
        if(saved?.Bolts==null || saved.NextId<0){throw new InvalidDataException("Missing sentry lightning");}
        Bolts.Clear();_nextId=saved.NextId;
        foreach(var s in saved.Bolts){Validate(s);if(s.Id>_nextId || !Bolts.TryAdd(s.Id,s)){throw new InvalidDataException("Duplicate sentry lightning");}}
    }
}
