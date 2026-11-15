// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal enum MiasmaNodeKind{Bolt,Maker,Cloud}
// Common cloud payload keeps its producer and captured shape separate from
// Bolt-only flight/maker state. Chain can reuse this consumer later.
internal sealed record EntropyCloudLink(double EndX,double EndY);
internal sealed record EntropyCloudPacket(CombatAbility Ability,int Rank,int Minimum256,int Maximum256,EntropyCloudShape Shape,
    int MagicMastery,FirestormModifiers Modifiers,EntropyCloudLink Link=null,SlowMissilePayload? Slow=null);
internal sealed record MiasmaNode(long Id,long Cast,uint Owner,int Map,int Z,MiasmaNodeKind Kind,double X,double Y,double Vx,double Vy,
    int Remaining,MiasmaBoltBirth Birth,EntropyCloudPacket Cloud,ulong Seed,SourceStepTrace Trace,DateTime CreatedUtc,DateTime NextUtc,DateTime UntilUtc);
internal sealed record MiasmaWorldSave(MiasmaNode[] Nodes,WeaponFlightLease[] Leases,long NextId,MiasmaChainState[] Chains=null,MiasmaChainStamp[] ChainMakers=null);
internal readonly record struct MiasmaAdvance(int DirectDamaged,int CloudContacts,int CreatedClouds,int Damage256,string Fault=null);
public sealed class MiasmaWorldProfiles:GenericPersistence
{
    public MiasmaWorldProfiles():base("SanctuaryMiasmaWorld",254){}
    public override void Serialize(IGenericWriter w){w.Write(0);w.Write(JsonSerializer.Serialize(MiasmaWorld.Snapshot()));}
    public override void Deserialize(IGenericReader r)
    {if(r.ReadInt()!=0){throw new InvalidDataException("Unknown Miasma world version");}MiasmaWorld.Restore(JsonSerializer.Deserialize<MiasmaWorldSave>(r.ReadString()));}
}

// Approved697->736->705world projection. Hit41/do41are explicitly adapted,
// not claimed recovered;the shared forty-ms world clock owns progression.
internal static class MiasmaWorld
{
    internal static bool Approved=>true;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MiasmaWorld));
    private static ImmutableDictionary<long,MiasmaNode> Nodes=ImmutableDictionary<long,MiasmaNode>.Empty;
    private static ImmutableDictionary<(long,uint),WeaponFlightLease> Leases=ImmutableDictionary<(long,uint),WeaponFlightLease>.Empty;
    private static long _next;
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    public static void Configure()=>_=new MiasmaWorldProfiles();
    private static PlayerMobile Owner(uint id)=>World.FindMobile((Serial)id)is PlayerMobile{Alive:true,Deleted:false} p
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock?p:null;
    internal static bool Fighting(PlayerMobile p,int map,bool? actions=null)=>p is{Alive:true,Deleted:false,Map:not null}&&p.Map!=Map.Internal&&p.Map.MapID==map
        &&(actions??p.NetState is{Running:true})&&!MinionSafeAreas.Contains(p);
    internal static bool Aim(PlayerMobile p,Serial selected,ref Point3D point)=>Owner(p?.Serial.Value??0)==p&&p is{Map:not null}&&Fighting(p,p.Map.MapID)
        &&ShapeRestrictions.Allowed(p,CombatAbility.MiasmaBolt)&&LobProjectiles.Aim(p,selected,ref point);
    internal static MiasmaNode[] For(Mobile p)=>Nodes.Values.Where(n=>n.Owner==p.Serial.Value).OrderBy(n=>n.Id).ToArray();
    internal static long NextIdentity=>checked(_next+1);
    internal static void CommitIdentity(long id)
    {if(id!=NextIdentity){throw new InvalidOperationException("Miasma identity sequence changed");}_next=id;}
    private static void Remove(long id)=>Nodes=Nodes.Remove(id);
    private static void Prune()
    {var casts=Nodes.Values.Select(n=>n.Cast).ToHashSet();foreach(var k in Leases.Keys.Where(k=>!casts.Contains(k.Item1)).ToArray()){Leases=Leases.Remove(k);}}
    internal static void Clear(Mobile p)
    {if(p==null){return;}MiasmaChainWorld.Clear(p);foreach(var n in For(p)){Remove(n.Id);}Prune();}
    internal static bool Ready(long cast,uint target,DateTime at)=>!Leases.TryGetValue((cast,target),out var l)||l.UntilUtc<=at;
    private static Point3D Point(double x,double y,int z)=>new((int)Math.Round(x),(int)Math.Round(y),z);
    private static ulong Child(ref ulong seed)=>SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));
    internal static long ReleasePrepared(PlayerMobile p,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at<=DateTime.UnixEpoch){throw new ArgumentException("Miasma clock must be UTC");}
        if(!Aim(p,Serial.Zero,ref point)){return 0;}var birth=MiasmaBoltBirths.Capture(p);
        if(!NecromancerEffects.CanPay(p,birth.Numbers.Mana256)){return 0;}
        var plan=MiasmaLaunch.Prepare(new(p.X*2,p.Y*2),null,new(point.X*2,point.Y*2),null,birth.Rank,birth.Slow.VelocityPercent,true,true,()=>true);
        if(plan==null){return 0;}var v=MissileVectors.VelocityFp8(plan.AimX-plan.X,plan.AimY-plan.Y,plan.Velocity256);long id=checked(_next+1);
        var n=new MiasmaNode(id,id,p.Serial.Value,p.Map.MapID,p.Z,MiasmaNodeKind.Bolt,p.X,p.Y,v.X,v.Y,50,birth,null,
            SourceRandom.InitLow(SeedLow()),new([new(p.X*2,p.Y*2)]),at,at.AddMilliseconds(40),at.AddMilliseconds(2040));Validate(n);
        if(!NecromancerEffects.Pay(p,birth.Numbers.Mana256)){return 0;}Nodes=Nodes.Add(id,n);_next=id;p.PlaySound(0x1E5);return id; // Identified cast placeholder.
    }
    internal static bool Enemy(PlayerMobile p,BaseCreature t,int z)=>t is{Alive:true,Deleted:false,Blessed:false}&&t.Map==p.Map&&Math.Abs(t.Z-z)<16
        &&ClassCombat.IsHostile(p,t)&&!Server.Spells.SpellHelper.IsTown(t.Location,p);
    internal static int Deliver(PlayerMobile p,BaseCreature target,GearDamage raw,bool direct,DateTime at)
    {
        p.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(p,target,raw);int loss=SkillHealth.TypedDamage(p,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts));
        if(direct){CreatureHitRecovery.Apply(target,p,loss);}CombatDamage.Finish(target,p);
        if(target is{Alive:true,Deleted:false}){target.FixedEffect(0x375A,5,5,0x48D,0);}return loss; // Identified contact placeholder.
    }
    private static void Makers(MiasmaNode parent,double x,double y,DateTime at)
    {
        ulong seed=parent.Seed;
        foreach(var ray in MiasmaBoltBirths.Makers())
        {
            int speed=SlowMissilePayload.VelocityFp8(0,0,parent.Birth.Rank,(int)ray.Flags,128,false,100);
            var v=MissileVectors.VelocityFp8(ray.X,ray.Y,speed);long id=checked(++_next);
            var n=new MiasmaNode(id,parent.Cast,parent.Owner,parent.Map,parent.Z,MiasmaNodeKind.Maker,x,y,v.X,v.Y,3,parent.Birth,null,
                Child(ref seed),new([new((int)Math.Round(x*2),(int)Math.Round(y*2))]),at,at.AddMilliseconds(40),at.AddMilliseconds(160));Validate(n);Nodes=Nodes.Add(id,n);
        }
    }
    private static int Bolt(MiasmaNode n,PlayerMobile p)
    {
        var trace=SourceMissileSteps.Advance(n.X,n.Y,n.Vx,n.Vy,n.Trace);var last=Point(n.X,n.Y,n.Z);bool wall=false;double x=n.X+n.Vx,y=n.Y+n.Vy;
        foreach(var cell in trace.Cells)
        {
            var pt=Point(cell.X/2.0,cell.Y/2.0,n.Z);
            if(pt.X<0||pt.Y<0||pt.X>=p.Map.Width||pt.Y>=p.Map.Height||LobProjectiles.Barrier(p.Map,pt)
                ||!p.Map.LineOfSight(new Point3D(last.X,last.Y,last.Z+12),new Point3D(pt.X,pt.Y,pt.Z+12))){wall=true;x=last.X;y=last.Y;break;}last=pt;
        }
        // A retained half-subtile step may still round to the previous world
        // tile while the continuous endpoint has entered the next tile. Check
        // that endpoint too, before it can become next update's "last clear".
        var end=Point(x,y,n.Z);
        if(!wall&&(end.X<0||end.Y<0||end.X>=p.Map.Width||end.Y>=p.Map.Height||LobProjectiles.Barrier(p.Map,end)
            ||!p.Map.LineOfSight(new Point3D(last.X,last.Y,last.Z+12),new Point3D(end.X,end.Y,end.Z+12))))
        {wall=true;x=last.X;y=last.Y;}
        int remaining=n.Remaining;uint selected=0;double hitX=x,hitY=y;
        var result=BasicMissileUpdate.Run(new(true,true,1,n.Remaining,3,50,true),new(()=>wall?2:0,r=>remaining=r,()=>true,()=>true,()=>2,
            ()=>trace.Cells,_=>1,cell=>
            {
                var center=Point(cell.X/2.0,cell.Y/2.0,n.Z);
                foreach(var t in SkillCombat.NearbySnapshot<BaseCreature>(p.Map,center,3).Where(t=>Enemy(p,t,n.Z)).OrderBy(t=>t.Serial.Value))
                {int size=CreatureProfiles.For(t).Traits.Size??2;if(CombatHitboxes.SourceTouches(1,size,cell.X-t.X*2,cell.Y-t.Y*2)){selected=t.Serial.Value;hitX=cell.X/2.0;hitY=cell.Y/2.0;return selected;}}
                return 0;
            },(target,terrain)=>{selected=target;return 2;}));
        if(result.Result==2)
        {
            if(selected!=0&&World.FindMobile((Serial)selected)is BaseCreature victim){TaintedFireBallMissiles.RequireOrdinaryRecipient(p,victim);}
            Remove(n.Id);Makers(n,selected!=0?hitX:x,selected!=0?hitY:y,n.NextUtc);
            if(selected!=0&&World.FindMobile((Serial)selected)is BaseCreature t&&Enemy(p,t,n.Z))
            {
                SorceressEffects.MissileHit(p,t,"miasmabolt");ulong seed=n.Seed;var raw=MiasmaBoltBirths.Roll(n.Birth,false,ref seed);
                if(!ClassAvoidance.Missile(t,reaction:true)){return Deliver(p,t,raw,true,n.NextUtc);}
            }
            return 0;
        }
        Nodes=Nodes.SetItem(n.Id,n with{X=x,Y=y,Remaining=remaining,Trace=trace,NextUtc=n.NextUtc.AddMilliseconds(40)});
        Effects.SendLocationEffect(Point(x,y,n.Z),p.Map,0x36E4,2,10,0x48D,0);return 0; // Identified magic bolt placeholder.
    }
    private static void Cloud(MiasmaNode maker)
    {
        var b=maker.Birth;long id=checked(++_next);var packet=new EntropyCloudPacket(CombatAbility.MiasmaBolt,b.Rank,b.Numbers.CloudMinimum256,b.Numbers.CloudMaximum256,b.Shape,b.MagicMastery,b.Modifiers);
        var n=new MiasmaNode(id,maker.Cast,maker.Owner,maker.Map,maker.Z,MiasmaNodeKind.Cloud,maker.X,maker.Y,0,0,b.Shape.LifetimeFrames,null,packet,
            maker.Seed,new([new((int)Math.Round(maker.X*2),(int)Math.Round(maker.Y*2))]),maker.NextUtc,maker.NextUtc,maker.NextUtc.AddMilliseconds(b.Shape.LifetimeFrames*40L));Validate(n);Nodes=Nodes.Add(id,n);
    }
    internal static long ChainCloud(MiasmaChainStamp stamp)
    {
        var b=stamp.Birth;long id=NextIdentity;
        var packet=new EntropyCloudPacket(CombatAbility.MiasmaChain,b.Numbers.Rank,b.Numbers.CloudMinimum256,b.Numbers.CloudMaximum256,
            b.Numbers.Shape,b.MagicMastery,b.Modifiers,new(stamp.EndX,stamp.EndY),b.Slow);
        var n=new MiasmaNode(id,stamp.Cast,stamp.Owner,stamp.Map,stamp.Z,MiasmaNodeKind.Cloud,stamp.X,stamp.Y,0,0,b.Numbers.Shape.LifetimeFrames,
            null,packet,stamp.Seed,new([new((int)Math.Round(stamp.X*2),(int)Math.Round(stamp.Y*2))]),stamp.NextUtc,stamp.NextUtc,
            stamp.NextUtc.AddMilliseconds(b.Numbers.Shape.LifetimeFrames*40L));Validate(n);CommitIdentity(id);Nodes=Nodes.Add(id,n);return id;
    }
    internal static double LinkLength(double x,double y,double endX,double endY)
    {double dx=Math.Abs((endX-x)*2),dy=Math.Abs((endY-y)*2);return Math.Max(dx,dy)+Math.Min(dx,dy)/2;}
    private static (int X,int Y,Point3D Point) Nearest(MiasmaNode n,BaseCreature target)
    {
        double x=n.X,y=n.Y;
        if(n.Cloud.Link is{} line)
        {
            double dx=line.EndX-x,dy=line.EndY-y,length=dx*dx+dy*dy;
            double along=length==0?0:Math.Clamp(((target.X-x)*dx+(target.Y-y)*dy)/length,0,1);x+=along*dx;y+=along*dy;
        }
        return((int)Math.Round(x*2),(int)Math.Round(y*2),Point(x,y,n.Z));
    }
    private static bool InCloud(MiasmaNode n,PlayerMobile p,BaseCreature t)
    {var near=Nearest(n,t);return SourceDamageAreas.Recipient(p,t,n.Map,near.X,near.Y,n.Z,n.Cloud.Shape.Radius,true,near.Point);}
    private static (int Contacts,int Damage) Scan(MiasmaNode n,PlayerMobile p,DateTime at)
    {
        // Scan current occupants once when late;do not backfill missed damage.
        long frames=(at-n.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
        Nodes=Nodes.SetItem(n.Id,n with{NextUtc=n.NextUtc.AddMilliseconds(frames*40L)});
        if(!Fighting(p,n.Map)){return default;}var c=n.Cloud;var center=Point(n.X,n.Y,n.Z);int x=(int)Math.Round(n.X*2),y=(int)Math.Round(n.Y*2),contacts=0,damage=0;
        var query=c.Link is{} line?Point((n.X+line.EndX)/2,(n.Y+line.EndY)/2,n.Z):center;
        int range=(int)Math.Ceiling(SkillGeometry.Tiles(c.Shape.Radius)+(c.Link is{} path?Math.Max(Math.Abs(path.EndX-n.X),Math.Abs(path.EndY-n.Y))/2:0))+1;
        var targets=SkillCombat.NearbySnapshot<BaseCreature>(p.Map,query,range)
            .Where(t=>Enemy(p,t,n.Z)&&InCloud(n,p,t)&&Ready(n.Cast,t.Serial.Value,at)).OrderBy(t=>t.Serial.Value).ToArray();
        foreach(var t in targets){TaintedFireBallMissiles.RequireOrdinaryRecipient(p,t);}
        ulong seed=n.Seed;
        foreach(var t in targets)
        {
            if(!Nodes.ContainsKey(n.Id)||!Fighting(p,n.Map)||!Enemy(p,t,n.Z)||!Ready(n.Cast,t.Serial.Value,at)){continue;}
            Leases=Leases.SetItem((n.Cast,t.Serial.Value),new(n.Cast,t.Serial.Value,at.AddMilliseconds(1000)));
            int raw=SourceSpellPacketRoll.Range(ref seed,MiasmaBoltBirths.Enhanced(c.Minimum256,c.MagicMastery),MiasmaBoltBirths.Enhanced(c.Maximum256,c.MagicMastery));
            if(c.Slow is{} slow){raw=slow.Damage256(raw);}
            Nodes=Nodes.SetItem(n.Id,Nodes[n.Id]with{Seed=seed});contacts++;
            damage=(int)Math.Min(int.MaxValue,(long)damage+Deliver(p,t,DruidMoltenBoulder.Potion(c.Modifiers,new(0,0,0,0,0,Magic:raw/256.0)),c.Ability==CombatAbility.MiasmaChain,at));
        }
        Effects.SendLocationEffect(center,p.Map,0x374A,2,10,0x48D,0);return(contacts,damage); // Explicit magic-cloud placeholder,not poison damage.
    }
    internal static void Tick(DateTime at)
    {
        if(World.Loading||!Approved){return;}
        foreach(uint owner in Nodes.Values.Select(n=>n.Owner).Distinct().OrderBy(n=>n).ToArray())
        {
            var result=TickPrepared(at,onlyOwner:owner,allowPreparedChains:false);
            if(result.Fault!=null){Log.Warning("Miasma Bolt stopped an unsupported node for {Owner}: {Reason}",owner,result.Fault);}
        }
    }
    internal static MiasmaAdvance TickPrepared(DateTime at,bool? actions=null,uint? onlyOwner=null,bool allowPreparedChains=true)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Miasma clock must be UTC");}int direct=0,clouds=0,created=0,damage=0;
        while(true)
        {
            var n=Nodes.Values.Where(n=>(!onlyOwner.HasValue||n.Owner==onlyOwner)&&(allowPreparedChains||MiasmaChainWorld.Approved||n.Cloud?.Ability!=CombatAbility.MiasmaChain)&&n.NextUtc<=at).OrderBy(n=>n.NextUtc).ThenBy(n=>n.Id).FirstOrDefault();if(n==null){break;}
            var p=Owner(n.Owner);if(p==null||n.Kind==MiasmaNodeKind.Cloud&&at>=n.UntilUtc){Remove(n.Id);continue;}
            if(n.Kind!=MiasmaNodeKind.Cloud&&!Fighting(p,n.Map,actions)){Remove(n.Id);continue;}
            try
            {
                if(n.Kind==MiasmaNodeKind.Bolt){int loss=Bolt(n,p);if(loss>0){direct++;}damage=(int)Math.Min(int.MaxValue,(long)damage+loss);}
                else if(n.Kind==MiasmaNodeKind.Maker)
                {
                    int remaining=MiasmaBoltBirths.MakerUpdate(n.Remaining,()=>{Cloud(n);created++;});
                    if(remaining<=0){Remove(n.Id);}else
                    {
                        double x=n.X+n.Vx,y=n.Y+n.Vy;var point=Point(x,y,n.Z);var old=Point(n.X,n.Y,n.Z);
                        bool blocked=x<0||y<0||x>=p.Map.Width||y>=p.Map.Height||LobProjectiles.Barrier(p.Map,point)
                            ||!p.Map.LineOfSight(new Point3D(old.X,old.Y,old.Z+12),new Point3D(point.X,point.Y,point.Z+12));
                        Nodes=Nodes.SetItem(n.Id,n with{Remaining=remaining,X=blocked?n.X:x,Y=blocked?n.Y:y,NextUtc=n.NextUtc.AddMilliseconds(40)});
                    }
                }
                else
                {
                    if(!Fighting(p,n.Map,actions))
                    {long frames=(at-n.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;Nodes=Nodes.SetItem(n.Id,n with{NextUtc=n.NextUtc.AddMilliseconds(frames*40L)});continue;}
                    var result=Scan(n,p,at);clouds+=result.Contacts;damage=(int)Math.Min(int.MaxValue,(long)damage+result.Damage);
                }
            }
            catch(NotSupportedException ex){Remove(n.Id);Prune();return new(direct,clouds,created,damage,ex.Message);}
        }
        Prune();return new(direct,clouds,created,damage);
    }
    internal static MiasmaWorldSave Snapshot()=>new(Nodes.Values.OrderBy(n=>n.Id).ToArray(),Leases.Values.OrderBy(l=>l.Cast).ThenBy(l=>l.Target).ToArray(),_next,MiasmaChainWorld.Snapshot(),MiasmaChainWorld.Stamps());
    internal static void Validate(MiasmaNode n)
    {
        if(n==null||n.Id<=0||n.Cast<=0||n.Cast>n.Id||!((Serial)n.Owner).IsMobile||n.Map<0||n.Map>=Map.Maps.Length||Map.Maps[n.Map]==null||Map.Maps[n.Map]==Map.Internal
            ||n.Z is <-128 or >127||!Enum.IsDefined(n.Kind)||!double.IsFinite(n.X)||!double.IsFinite(n.Y)||n.X<0||n.Y<0||n.X>=Map.Maps[n.Map].Width||n.Y>=Map.Maps[n.Map].Height
            ||!double.IsFinite(n.Vx)||!double.IsFinite(n.Vy)||Math.Abs(n.Vx)>1||Math.Abs(n.Vy)>1||n.Seed==0||n.Trace?.Cells is not{Length:>=1 and <=10}
            ||n.CreatedUtc.Kind!=DateTimeKind.Utc||n.CreatedUtc<=DateTime.UnixEpoch||n.NextUtc.Kind!=DateTimeKind.Utc||n.UntilUtc.Kind!=DateTimeKind.Utc||n.NextUtc<n.CreatedUtc||n.UntilUtc<=n.CreatedUtc)
        {throw new InvalidDataException("Invalid Miasma node");}
        if(n.Kind==MiasmaNodeKind.Cloud)
        {
            var c=n.Cloud;
            if(n.Birth!=null||c==null||c.Ability is not(CombatAbility.MiasmaBolt or CombatAbility.MiasmaChain)||c.Rank is <1 or >255||c.Shape is not{} shape||shape.LifetimeFrames is <50 or >150
                ||shape.Radius is <2 or >4||shape.EffectiveRank is <0 or >255||shape.HardRank is <0 or >20
                ||shape.LifetimeFrames!=50+Math.Min(shape.EffectiveRank*5,100)||shape.Radius!=(shape.EffectiveRank>=10?4:shape.EffectiveRank>=5?3:2)||c.Ability==CombatAbility.MiasmaBolt&&(shape.ServerCalculation!=null||c.Link!=null||c.Slow!=null)
                ||c.Ability==CombatAbility.MiasmaChain&&(shape.ServerCalculation!=(shape.HardRank>=20?4:shape.HardRank>=10?3:2)||c.Link==null||c.Slow==null)
                ||c.Minimum256<0||c.Maximum256<c.Minimum256||c.Modifiers==null||c.MagicMastery<0||n.Vx!=0||n.Vy!=0
                ||n.Remaining!=c.Shape.LifetimeFrames||n.UntilUtc!=n.CreatedUtc.AddMilliseconds(n.Remaining*40L))
            {throw new InvalidDataException("Invalid Entropy cloud");}
            MiasmaBoltBirths.ValidatePacket(c.Minimum256,c.Maximum256,c.MagicMastery,c.Modifiers);
            if(c.Link is{} link&&(!double.IsFinite(link.EndX)||!double.IsFinite(link.EndY)||link.EndX<0||link.EndY<0
                ||link.EndX>=Map.Maps[n.Map].Width||link.EndY>=Map.Maps[n.Map].Height||LinkLength(n.X,n.Y,link.EndX,link.EndY)>40.0001)
                ||c.Slow is{} slow&&(slow.VelocityPercent is <0 or >100||slow.DamagePercent is <0 or >100))
            {throw new InvalidDataException("Invalid Entropy link/slow metadata");}
        }
        else
        {MiasmaBoltBirths.Validate(n.Birth);if(n.Cloud!=null||n.Remaining<1||n.Remaining>(n.Kind==MiasmaNodeKind.Bolt?50:3)){throw new InvalidDataException("Invalid Miasma flight/maker");}}
    }
    internal static void Restore(MiasmaWorldSave saved)
    {
        if(saved?.Nodes==null||saved.Leases==null||saved.NextId<0){throw new InvalidDataException("Missing Miasma snapshot");}
        var nodes=ImmutableDictionary<long,MiasmaNode>.Empty;var leases=ImmutableDictionary<(long,uint),WeaponFlightLease>.Empty;
        foreach(var n in saved.Nodes){Validate(n);if(n.Id>saved.NextId||nodes.ContainsKey(n.Id)){throw new InvalidDataException("Duplicate/future Miasma ID");}nodes=nodes.Add(n.Id,n);}
        foreach(var l in saved.Leases)
        {if(!nodes.Values.Any(n=>n.Cast==l.Cast)||!((Serial)l.Target).IsMobile||l.RetainWithoutFlight||l.UntilUtc.Kind!=DateTimeKind.Utc||leases.ContainsKey((l.Cast,l.Target))){throw new InvalidDataException("Invalid Miasma lease");}leases=leases.Add((l.Cast,l.Target),l);}
        var chains=saved.Chains??[];var makers=saved.ChainMakers??[];
        MiasmaChainWorld.ValidateSnapshot(chains,makers,saved.NextId,nodes.Keys);
        var casts=nodes.Values.Select(n=>(Cast:n.Cast,n.Owner,n.Map,Ability:n.Cloud?.Ability??CombatAbility.MiasmaBolt))
            .Concat(chains.Select(n=>(Cast:n.Id,n.Owner,n.Map,Ability:CombatAbility.MiasmaChain)))
            .Concat(makers.Select(n=>(Cast:n.Cast,n.Owner,n.Map,Ability:CombatAbility.MiasmaChain)));
        if(casts.GroupBy(n=>n.Cast).Any(g=>g.Select(n=>(n.Owner,n.Map,n.Ability)).Distinct().Count()!=1))
        {throw new InvalidDataException("Inconsistent Miasma cast ownership/producer");}
        Nodes=nodes;Leases=leases;_next=saved.NextId;MiasmaChainWorld.RestoreValidated(chains,makers);
    }
}
