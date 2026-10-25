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

internal sealed record SentryBoltState(long Id,uint Actor,uint Owner,int Map,int Z,int Rank,int OriginX,int OriginY,
    Point2D[] Path,double X,double Y,int Next,int Frame,int Frames,int VelocityFp8,int Minimum256,int Maximum256,int Pierce,
    SlowMissilePayload Slow,ulong Seed,SourceStepTrace Steps,DateTime NextUtc,string Fault=null)
{public string MotionAdapter{get;init;}="native-shared-chargedbolt-v1";}
internal sealed record SentryBoltSave(SentryBoltState[] Bolts,long NextId);
public sealed class SentryBoltProfiles:GenericPersistence
{
    private SentryBoltSave _snapshot;
    public SentryBoltProfiles():base("SanctuarySentryBolts",158){}
    public override void Serialize(){_snapshot=AssassinSentryBolts.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown sentry bolt version");}AssassinSentryBolts.Restore(JsonSerializer.Deserialize<SentryBoltSave>(reader.ReadString()));}
}

// Real trap-owned projectiles. Path/heading/seed use the source-compared shared
// helpers. Advance is the existing native ChargedBolts movement adapter, not a
// claim that the ambiguous reconstructed missile movement body was ported.
public static class AssassinSentryBolts
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AssassinSentryBolts));
    private static readonly Dictionary<long,SentryBoltState> Bolts=new();
    private static long _nextId;
    public static void Configure()=>_ = new SentryBoltProfiles();
    internal static DateTime NextFrame=>Bolts.Values.Select(x=>x.NextUtc).DefaultIfEmpty(DateTime.MaxValue).Min();
    internal static SentryBoltState State(long id)=>Bolts.GetValueOrDefault(id);
    internal static void Dispatch(AssassinSentryBody body,uint targetSerial)
    {
        int Field(string name)=>NecromancerSummons.Source("sentrychargedbolt",name);
        if(Field("Size")!=1 || Field("CollideType")!=3 || Field("CollideKill")!=1 || Field("LastCollide")!=1
            || Field("NextHit")!=0 || Field("ToHit")!=0 || Field("pSrvHitFunc")!=0 || Field("pSrvDmgFunc")!=0
            || Field("SrcDamage")!=0 || Field("Accel")!=0 || Field("Vel")!=12 || Field("VelLev")!=0 || Field("ResultFlags")!=4 || NecromancerSummons.Source("chargeboltsentry","Crit")!=0)
        {throw new NotSupportedException("Unsupported sentry bolt collision/damage/critical table route");}
        var context=body.Prepared;
        AssassinSentryLaunch.Dispatch(true,true,AssassinSentryCreation.Bolts(context.Creation),Field("Range"),_=>
        {
            // The source re-reads target coordinates for every ordinal. A dead
            // but still present target is not rejected by that getter.
            Point2D? present=null;
            if(World.FindMobile((Serial)targetSerial) is{Deleted:false} target && target!=body)
            {
                if(target.Map!=body.Map){throw new NotSupportedException("Cross-map sentry pending target is not mapped");}
                present=new(target.X*2,target.Y*2);
            }
            return AssassinSentryAim.Resolve(context.CachedAim,present);
        },spawn=>{Create(body,spawn);return true;});
    }
    internal static long Create(AssassinSentryBody body,SentryBoltSpawn spawn)
    {
        var c=body.Prepared;
        if(body is not{Alive:true,Deleted:false} || c.ControllerFault!=null || spawn.Frames is <1 or >77 || spawn.Distance!=spawn.Frames)
        {throw new InvalidOperationException("Invalid live sentry bolt supplier");}
        var slow=AmazonSlowMissiles.Capture(body,NecromancerSummons.Source("sentrychargedbolt","CanSlow")!=0);
        int velocity=SlowMissilePayload.VelocityFp8(NecromancerSummons.Source("sentrychargedbolt","Vel"),NecromancerSummons.Source("sentrychargedbolt","VelLev"),c.Creation.Rank,0x21,0,true,slow.VelocityPercent);
        var ends=AssassinSentryCreation.Lightning(c.Creation);int heading=ChargedBolts.Heading(spawn.Aim.X-body.X*2,spawn.Aim.Y-body.Y*2);
        var path=ChargedBolts.Path(spawn.Seed,heading,spawn.Distance);ulong seed=SourceRandom.InitLow(unchecked((int)spawn.Seed));
        for(int i=1;i<path.Length;i++){SourceRandom.Roll(ref seed);}
        long id=checked(++_nextId);
        var bolt=new SentryBoltState(id,body.Serial.Value,c.Owner,c.Map,c.Position.Z,c.Creation.Rank,body.X,body.Y,path,body.X,body.Y,1,0,spawn.Frames,
            velocity,slow.Damage256(ends.Min),slow.Damage256(ends.Max),c.Creation.Passive("passive_ltng_pierce"),slow,seed,
            new([new(body.X*2,body.Y*2)]),c.NextFrameUtc.AddMilliseconds(40));Validate(bolt);Bolts.Add(id,bolt);return id;
    }
    private static void Validate(SentryBoltState s)
    {
        if(s==null || s.Id<=0 || !((Serial)s.Actor).IsMobile || !((Serial)s.Owner).IsMobile || s.Rank is <1 or >255
            || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal || s.Z is <-128 or >127
            || s.Frames is <1 or >77 || s.Frame<0 || s.Frame>=s.Frames || s.Path==null || s.Path.Length!=(s.Frames>>1)+1 || s.Next<1 || s.Next>s.Path.Length
            || !double.IsFinite(s.X) || !double.IsFinite(s.Y) || s.X<0 || s.Y<0 || s.X>=Map.Maps[s.Map].Width || s.Y>=Map.Maps[s.Map].Height
            || s.OriginX<=0 || s.OriginY<=0 || s.OriginX>=Map.Maps[s.Map].Width || s.OriginY>=Map.Maps[s.Map].Height
            || s.VelocityFp8<0 || s.Minimum256<0 || s.Maximum256<s.Minimum256 || s.Slow.VelocityPercent is <0 or >100 || s.Slow.DamagePercent is <0 or >100
            || s.Steps?.Cells is not{Length:>=1 and <=10} || s.NextUtc.Kind!=DateTimeKind.Utc || s.MotionAdapter!="native-shared-chargedbolt-v1")
        {throw new InvalidDataException("Invalid saved sentry bolt");}
        if(s.Steps.Cells.Any(c=>c.X is <0 or >65535 || c.Y is <0 or >65535)){throw new InvalidDataException("Invalid sentry retained source cell");}
        if(s.Path[0]!=new Point2D(0,0)){throw new InvalidDataException("Invalid sentry path origin");}
        for(int i=1;i<s.Path.Length;i++)
        {int dx=Math.Abs(s.Path[i].X-s.Path[i-1].X),dy=Math.Abs(s.Path[i].Y-s.Path[i-1].Y);if(dx>1 || dy>1 || dx+dy==0){throw new InvalidDataException("Invalid sentry waypoint");}}
    }
    internal static void TickFrame(DateTime at,bool? allowActions=null)
    {
        foreach(var state in Bolts.Values.Where(x=>x.NextUtc==at).OrderByDescending(x=>x.Id).ToArray())
        {
            try{Advance(state,allowActions);}
            catch(Exception e){Bolts[state.Id]=state with{Fault=e.Message,Frame=state.Frame+1,NextUtc=state.NextUtc.AddMilliseconds(40)};
                if(state.Frame+1>=state.Frames){Bolts.Remove(state.Id);}Log.Error(e,"Sentry bolt suspended: {Id}",state.Id);}
        }
    }
    private static void Advance(SentryBoltState s,bool? allowActions)
    {
        int frame=s.Frame+1;var next=s with{Frame=frame,NextUtc=s.NextUtc.AddMilliseconds(40)};
        var body=World.FindMobile((Serial)s.Actor) as AssassinSentryBody;
        bool live=body is{Alive:true,Deleted:false} && body.Prepared.Owner==s.Owner && body.Map?.MapID==s.Map;
        bool fighting=live && body.Prepared.ControllerFault==null && (allowActions??AssassinTrapRoster.Fighting(s.Owner));
        if(!fighting || s.Fault!=null)
        {if(frame>=s.Frames){Bolts.Remove(s.Id);}else{Bolts[s.Id]=next;}return;}
        var moved=ChargedBolts.Advance(s.Path,s.OriginX,s.OriginY,s.X,s.Y,s.Next,SkillGeometry.Tiles(s.VelocityFp8/4096.0));
        next=next with{X=moved.X,Y=moved.Y,Next=moved.Next};
        // Generic source update moves before decrement/expiry; no contact at0.
        if(frame>=s.Frames || moved.Next>=s.Path.Length || moved.X<0 || moved.Y<0 || moved.X>=body.Map.Width || moved.Y>=body.Map.Height)
        {Bolts.Remove(s.Id);return;}
        next=next with{Steps=SourceMissileSteps.Advance(s.X,s.Y,moved.X-s.X,moved.Y-s.Y,s.Steps)};
        if(World.FindMobile((Serial)s.Owner) is not PlayerMobile owner){throw new NotSupportedException("Missing sentry allegiance owner");}
        foreach(var cell in next.Steps.Cells)
        {
            if(cell.X<=0 || cell.Y<=0 || cell.X>=body.Map.Width*2 || cell.Y>=body.Map.Height*2)
            {Bolts.Remove(s.Id);return;}
            BaseCreature target=null;
            foreach(var candidate in body.Map.GetMobilesInRange<BaseCreature>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z),2))
            {
                if(Math.Abs(candidate.Z-s.Z)>=16 || !body.TrapTarget(owner,candidate) || SpellHelper.IsTown(candidate.Location,candidate)){continue;}
                int? size=CreatureProfiles.For(candidate).Traits.Size;
                if(size is not(>=1 and <=3)){throw new NotSupportedException("Sentry collider has no mapped source size");}
                if(CombatHitboxes.SourceTouches(1,size.Value,cell.X-candidate.X*2,cell.Y-candidate.Y*2)){target=candidate;break;}
            }
            // Source valid-collider priority precedes barrier-only removal.
            if(target!=null){Impact(body,target,next);Bolts.Remove(s.Id);return;}
            if(!body.Map.LineOfSight(new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z+12),new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z+12)))
            {Bolts.Remove(s.Id);return;}
        }
        Bolts[s.Id]=next;
        if(frame%2==0){Effects.SendLocationEffect(new Point3D((int)Math.Round(next.X),(int)Math.Round(next.Y),s.Z),body.Map,0x3818,3,10,0x482,0);} // Placeholder bolt.
    }
    internal static int Impact(AssassinSentryBody body,BaseCreature target,SentryBoltState s)
        =>Impact(body,target,"sentrychargedbolt",s.Minimum256,s.Maximum256,s.Pierce,s.Seed).WholeLoss;
    // Both source-zero NPC lightning routes share block/resistance/recovery,
    // but each projectile owns its RNG and its own continuation policy.
    internal static (int WholeLoss,ulong Seed) Impact(AssassinSentryBody body,BaseCreature target,string missile,int minimum256,int maximum256,int pierce,ulong seed)
    {
        SorceressEffects.MissileHit(body,target,missile);
        ulong stream=seed;int raw=WeaponMissileDamage.RollValue(minimum256,maximum256,0,bound=>(int)SourceRandom.Limited(ref stream,bound));
        if(!SourceBlocking.TryBlock(target,false,out bool blocked)){throw new NotSupportedException("Sentry spell-block supplier is not mapped");}
        if(blocked){return(0,stream);}
        body.DoHarmful(target,true);
        var result=NamedElements.Resolve256(body,target,raw,"ltng",enhance:false,pierceOverride:pierce);
        if(result.Absorbed256>0){SkillHealth.Heal(target,result.Absorbed256);}
        int loss=SkillHealth.Damage(body,target,result.Damage256);CreatureHitRecovery.Apply(target,body,loss);CombatDamage.Finish(target,body);return(loss,stream);
    }
    internal static SentryBoltSave Snapshot()=>new(Bolts.Values.OrderBy(x=>x.Id).ToArray(),_nextId);
    internal static void Restore(SentryBoltSave saved)
    {
        if(saved?.Bolts==null || saved.NextId<0){throw new InvalidDataException("Missing sentry bolts");}
        Bolts.Clear();_nextId=saved.NextId;
        foreach(var row in saved.Bolts){Validate(row);if(row.Id>_nextId || !Bolts.TryAdd(row.Id,row)){throw new InvalidDataException("Duplicate sentry bolt");}}
    }
}
