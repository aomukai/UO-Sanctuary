// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public sealed record HydraHeadSource(uint Actor,HydraCreationStats Birth,HydraControlState Control,DateTime NextUtc,
    Point2D? CachedAim,DateTime? RetireUtc=null,string Fault=null);
public sealed record HydraBoltSource(uint Actor,SourceStepTrace Steps,SlowMissilePayload Slow,ulong Seed,uint LastCollision,int VelocityFp8);

internal static class PlayerHydras
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerHydras));private static readonly HashSet<string> Warned=new();
    internal static void Gap(string reason){if(Warned.Add(reason)){Log.Warning("Hydra unsupported context: {Reason}",reason);}}
    internal static bool ValidAim(PlayerMobile owner,Serial selected,Point3D point,out Point3D aim)=>PlayerFrozenOrbs.ValidAim(owner,selected,point,out aim);
    internal static bool Release(PlayerMobile owner,Serial selected,Point3D point,SkillValues values)
    {
        if(!ValidAim(owner,selected,point,out var aim)||!Hydras.CanPlace(owner,aim)){return false;}
        var birth=HydraCreation.Prepare(values,ClassStats.Level(owner),name=>HydraCreation.Accrued(owner,name));
        if(birth.Passive("item_pierce_fire_immunity")!=0){Gap("Captured fire sunder requires its NPC immunity consumer");return false;}
        if(NecromancerEffects.ManaBalance256(owner)<values.Mana256){return false;}
        if(!Hydras.Commit(owner,aim,birth,TimedEffects.Now)){return false;}
        if(!NecromancerEffects.Pay(owner,values.Mana256)){throw new InvalidOperationException("Hydra mana changed during synchronous creation");}
        owner.PlaySound(0x1E5);return true; // Identified cast placeholder; no local delay.
    }
    internal static PreparedMonsterDamageResult Deliver(HydraBody body,BaseCreature target,int raw,DateTime at)
    {
        if(body.Head?.Source is not{} source||World.FindMobile((Serial)body.Head.Owner) is not PlayerMobile owner
            ||body.Map!=target.Map||!body.TargetAllowed(owner,target)||!target.CanBeDamaged())
        {throw new NotSupportedException("Detached Hydra emitter or unqualified recipient");}
        return NpcFireSpellHit.Deliver(body,owner,target,raw,at,"Hydra");
    }
}

public static partial class Hydras
{
    private static readonly Dictionary<long,HydraHead> _retired=new();
    private static readonly HashSet<long> _loadedHeads=new(),_loadedBolts=new();
    internal static HydraHead Head(long id)=>_heads.GetValueOrDefault(id)??_retired.GetValueOrDefault(id);
    internal static bool Commit(PlayerMobile owner,Point3D point,HydraCreationStats birth,DateTime at)
    {
        HydraCreation.Validate(birth);Route();
        if(owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal||at.Kind!=DateTimeKind.Utc
            ||birth.Passive("item_pierce_fire_immunity")!=0){throw new NotSupportedException("Unsupported Hydra world creation context");}
        var ends=HydraCreation.Fire(birth,SlowMissilePayload.Normal);long group=checked(++_nextId);bool any=false;
        foreach(var place in Places(owner,point))
        {
            long id=checked(++_nextId);var body=new HydraBody();body.Attach(id,owner);
            try{body.MoveToWorld(new Point3D((int)Math.Round(place.X),(int)Math.Round(place.Y),point.Z),owner.Map);}
            catch{body.Delete();throw;}
            if(body.Deleted){continue;} // Failed allocation/placement never evicts an old head.
            ulong seed=SourceRandom.InitLow(Blazes.SeedLow());
            var head=new HydraHead(id,group,place.Index,owner.Serial.Value,birth.Rank,owner.Map.MapID,place.X,place.Y,point.Z,
                ends.Min,ends.Max,birth.Passive("passive_fire_pierce"),seed,at.AddMilliseconds(10000),at.AddMilliseconds(1000),default,0)
                {Source=new(body.Serial.Value,birth,HydraControl.Start(250,seed),at.AddMilliseconds(40),new(0,0))};
            var oldest=_heads.Values.Where(x=>x.Owner==owner.Serial.Value).OrderBy(x=>x.Id).ToArray();
            foreach(var old in oldest.Take(Math.Max(0,oldest.Length+1-birth.MaximumHeads))){Retire(old,at);}
            _heads.Add(id,head);any=true;
        }
        return any;
    }
    internal static void Route()
    {
        int M(string f)=>NecromancerSummons.Source("hydra",f);
        if(M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=0||M("Range")!=30||M("Vel")!=16||M("MaxVel")!=16||M("VelLev")!=0||M("InitSteps")!=2
            ||M("Size")!=1||M("CollideType")!=3||M("CollideKill")!=1||M("LastCollide")!=1||M("NextHit")!=0||M("ToHit")!=0
            ||M("GetHit")!=1||M("ReturnFire")!=1||M("CanSlow")!=1||M("CanDestroy")!=1||M("SrcDamage")!=0||M("pSrvDmgFunc")!=0||M("ResultFlags")!=4||M("HitFlags")!=0)
        {throw new NotSupportedException("Unsupported pinned Hydra bolt route");}
    }
    // Native emitter lease: stop AI immediately, retain source identity for
    // already-fired bolts and the ten-frame death presentation, then delete.
    // This bounds host objects; exact D2R final GUID/room removal is unproved.
    private static void Retire(HydraHead head,DateTime at)
    {
        _heads.Remove(head.Id);_loadedHeads.Remove(head.Id);
        if(head.Source==null){return;}
        var control=head.Source.Control with{Mode=0,Target=0,NextThink=0,ActionAt=0,EndAt=0};
        _retired[head.Id]=head with{Target=0,ShotUtc=default,Source=head.Source with{Control=control,RetireUtc=at.AddMilliseconds(400)}};
    }
    private static void RemoveSource(HydraHead head)
    {
        _heads.Remove(head.Id);_retired.Remove(head.Id);_loadedHeads.Remove(head.Id);
        if(head.Source!=null){World.FindMobile((Serial)head.Source.Actor)?.Delete();}
    }
    internal static void Forget(HydraBody body,long id)
    {
        if(Head(id)?.Source?.Actor==body.Serial.Value){_heads.Remove(id);_retired.Remove(id);_loadedHeads.Remove(id);}
        // Explicit native deletion cancels dependent flights. Do not retain a
        // dangling saved creator or substitute the player's attacking identity.
        foreach(var b in _bolts.Values.Where(x=>x.Source?.Actor==body.Serial.Value).ToArray()){_bolts.Remove(b.Id);_loadedBolts.Remove(b.Id);}
    }
    private static HydraHead Project(HydraHead head,HydraHeadSource source)
    {
        var c=source.Control;var origin=source.NextUtc.AddMilliseconds(-(c.Frame+1)*40L);
        return head with{Seed=c.Seed,Target=c.Target,DecisionUtc=c.NextThink==0?default:origin.AddMilliseconds(c.NextThink*40L),
            ShotUtc=c.ActionAt==0?default:origin.AddMilliseconds(c.ActionAt*40L),Source=source};
    }
    internal static void Advance(PlayerMobile owner,DateTime at,bool actions)=>TickSource(at,actions,owner.Serial.Value);
    private static void TickSource(DateTime at,bool? actions,uint? onlyOwner=null)
    {
        var queue=new PriorityQueue<(long Id,bool Head),(DateTime At,long Id)>();
        void EnqueueHead(HydraHead head){if(head?.Source is{} s&&s.NextUtc<=at&&(!onlyOwner.HasValue||head.Owner==onlyOwner)){queue.Enqueue((head.Id,true),(s.NextUtc,head.Id));}}
        void EnqueueBolt(HydraBolt bolt){if(bolt?.Source!=null&&bolt.NextUtc<=at&&(!onlyOwner.HasValue||bolt.Owner==onlyOwner)){queue.Enqueue((bolt.Id,false),(bolt.NextUtc,bolt.Id));}}
        foreach(var h in _heads.Values){EnqueueHead(h);}foreach(var b in _bolts.Values){EnqueueBolt(b);}
        while(queue.TryDequeue(out var due,out var priority))
        {
            if(due.Head)
            {
                if(!_heads.TryGetValue(due.Id,out var head)||head.Source.NextUtc!=priority.At){continue;}
                try{if(StepHead(head,at,actions) is{} child){EnqueueBolt(child);}}
                catch(NotSupportedException e){PlayerHydras.Gap(e.Message);Retire(head with{Source=head.Source with{Fault=e.Message}},priority.At);}
                EnqueueHead(_heads.GetValueOrDefault(due.Id));
            }
            else
            {
                if(!_bolts.TryGetValue(due.Id,out var bolt)||bolt.NextUtc!=priority.At){continue;}
                try{StepBolt(bolt,at,actions);}
                catch(NotSupportedException e){PlayerHydras.Gap(e.Message);_bolts.Remove(due.Id);}
                EnqueueBolt(_bolts.GetValueOrDefault(due.Id));
            }
        }
        foreach(var h in _retired.Values.Where(x=>!onlyOwner.HasValue||x.Owner==onlyOwner).ToArray())
        {
            if(PlayerBlizzards.Owner(h.Owner)==null||h.Source.RetireUtc<=at&&!_bolts.Values.Any(x=>x.Head==h.Id))
            {RemoveSource(h);}
        }
    }
    private static HydraBolt StepHead(HydraHead head,DateTime at,bool? actions)
    {
        var source=head.Source;var owner=PlayerBlizzards.Owner(head.Owner);
        if(owner==null||World.FindMobile((Serial)source.Actor) is not HydraBody{Deleted:false} body||body.SummonMaster!=owner||body.Map?.MapID!=head.Map)
        {RemoveSource(head);return null;}
        bool loaded=_loadedHeads.Remove(head.Id),fighting=actions??AssassinTrapRoster.Fighting(head.Owner);
        if(loaded||!fighting)
        {
            int skip=(int)Math.Min(1_000_000,Math.Max(1,(at-source.NextUtc).TotalMilliseconds/40+1));var c=HydraControl.Inactive(source.Control,skip);
            var next=Project(head,source with{Control=c,NextUtc=source.NextUtc.AddMilliseconds(skip*40L)});
            if(c.Mode==0){Retire(next,at);}else{_heads[head.Id]=next;}return null;
        }
        if(SpellHelper.IsTown(body.Location,body)){Retire(head,source.NextUtc);return null;}
        Point2D? cached=source.CachedAim;HydraBolt emitted=null;
        SentryTarget? Acquire()
        {
            // Native nearest-target/range adapter; full source AI acquisition
            // ordering/room traversal is explicitly not certified here.
            BaseCreature first=null;int nearest=25;
            foreach(var target in body.Map.GetMobilesInRange<BaseCreature>(body.Location,13))
            {
                if(!body.TargetAllowed(owner,target)||!body.CanSee(target)||!body.InLOS(target)||SpellHelper.IsTown(target.Location,body)){continue;}
                int size=CreatureProfiles.For(target).Traits.Size??throw new NotSupportedException("Hydra target has no source size mapping");
                int distance=AssassinSentryTargets.Distance(new((int)(head.X*2),(int)(head.Y*2)),new(target.Serial.Value,target.X*2,target.Y*2,size));
                if(distance<nearest){nearest=distance;first=target;}
            }
            return first==null?null:new(first.Serial.Value,nearest);
        }
        var control=HydraControl.Advance(source.Control,Acquire,id=>
        {
            if(World.FindMobile((Serial)id) is not{Deleted:false} target||target.Map!=body.Map){return false;}
            cached=AssassinSentryAim.Begin(cached,new(target.X*2,target.Y*2));body.Direction=body.GetDirectionTo(target);return true;
        },id=>emitted=ShootSource(head,body,id,cached,source.NextUtc));
        var updated=Project(head,source with{Control=control,CachedAim=cached,NextUtc=source.NextUtc.AddMilliseconds(40)});
        if(control.Mode==0){Retire(updated,source.NextUtc);}else{_heads[head.Id]=updated;}
        return emitted;
    }
    private static HydraBolt ShootSource(HydraHead head,HydraBody body,uint targetId,Point2D? cached,DateTime at)
    {
        Point2D? present=null;if(World.FindMobile((Serial)targetId) is{Deleted:false} target)
        {if(target.Map!=body.Map){throw new NotSupportedException("Hydra pending target changed maps");}present=new(target.X*2,target.Y*2);}
        var aim=AssassinSentryAim.Resolve(cached,present);int dx=aim.X-(int)(head.X*2),dy=aim.Y-(int)(head.Y*2);if(dx==0&&dy==0){dx=dy=1;}
        var slow=AmazonSlowMissiles.Capture(body,true);int velocity=SlowMissilePayload.VelocityFp8(16,0,head.Rank,33,0,true,slow.VelocityPercent);
        if(velocity==0){throw new NotSupportedException("Hydra zero-velocity path flags need their adapter");}
        var v=MissileVectors.VelocityFp8(dx,dy,velocity);var ends=HydraCreation.Fire(head.Source.Birth,slow);
        var bolt=new HydraBolt(checked(++_nextId),head.Id,head.Owner,head.Map,head.X,head.Y,head.Z,v.X,v.Y,0,ends.Min,ends.Max,head.Pierce,at.AddMilliseconds(1200),at.AddMilliseconds(40))
            {Source=new(body.Serial.Value,new([new((int)(head.X*2),(int)(head.Y*2))]),slow,SourceRandom.InitLow(Blazes.SeedLow()),body.Serial.Value,velocity)};
        _bolts.Add(bolt.Id,bolt);return bolt;
    }
    private static void StepBolt(HydraBolt bolt,DateTime at,bool? actions)
    {
        var s=bolt.Source;var head=Head(bolt.Head);var owner=PlayerBlizzards.Owner(bolt.Owner);
        var body=World.FindMobile((Serial)s.Actor) as HydraBody;
        if(owner==null){_bolts.Remove(bolt.Id);return;}
        bool loaded=_loadedBolts.Remove(bolt.Id);bool fighting=(actions??AssassinTrapRoster.Fighting(bolt.Owner))&&head?.Source?.Actor==s.Actor&&body is{Deleted:false}&&body.Map?.MapID==bolt.Map;
        if(loaded||!fighting)
        {
            int skip=(int)Math.Min(30-bolt.Frame,Math.Max(1,(at-bolt.NextUtc).TotalMilliseconds/40+1));
            if(bolt.Frame+skip>=30){_bolts.Remove(bolt.Id);}else{_bolts[bolt.Id]=bolt with{Frame=bolt.Frame+skip,NextUtc=bolt.NextUtc.AddMilliseconds(skip*40L)};}return;
        }
        var next=bolt;bool removed=false,barrier=false;int remaining=30-bolt.Frame;
        int Move()
        {
            next=next with{X=bolt.X+bolt.Vx,Y=bolt.Y+bolt.Vy,Source=s with{Steps=SourceMissileSteps.Advance(bolt.X,bolt.Y,bolt.Vx,bolt.Vy,s.Steps)}};
            barrier=next.X<=0||next.Y<=0||next.X>=body.Map.Width||next.Y>=body.Map.Height;return barrier?2:1;
        }
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var t in body.Map.GetMobilesInRange<BaseCreature>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,bolt.Z),3))
            {
                if(t.Serial.Value==s.LastCollision||Math.Abs(t.Z-bolt.Z)>=16||!body.TargetAllowed(owner,t)||SpellHelper.IsTown(t.Location,body)){continue;}
                int size=CreatureProfiles.For(t).Traits.Size??throw new NotSupportedException("Hydra collider has no source size mapping");
                if(CombatHitboxes.SourceTouches(1,size,cell.X-t.X*2,cell.Y-t.Y*2)){return t;}
            }
            return null;
        }
        bool Wall(SourceMissileCell cell)=>DruidFirestorm.CellBlocked(body.Map,bolt.Z,bolt.X,bolt.Y,cell.X,cell.Y);
        int Hit(uint target,bool terminal)
        {
            removed=true;if(!terminal&&World.FindMobile((Serial)target) is BaseCreature t)
            {
                next=next with{Source=next.Source with{LastCollision=target}};SorceressEffects.MissileHit(body,t,"hydra");
                if(!SourceBlocking.TryBlock(t,false,out bool blocked)){throw new NotSupportedException("Hydra recipient spell-block supplier is not mapped");}
                ulong seed=next.Source.Seed;int raw=SorceressSpellMissiles.Roll(bolt.Min256,bolt.Max256,seed,out seed);next=next with{Source=next.Source with{Seed=seed}};
                if(!blocked&&!ClassAvoidance.Missile(t,"hydra")){PlayerHydras.Deliver(body,t,raw,bolt.NextUtc);}
            }
            return 2;
        }
        var result=BasicMissileUpdate.Run(new(true,true,s.VelocityFp8,remaining,2,30,true),new(Move,r=>{remaining=r;next=next with{Frame=30-r};},()=>true,()=>true,()=>64,()=>next.Source.Steps.Cells,
            cell=>Wall(cell)?4:Find(cell)!=null?1:0,cell=>Find(cell)?.Serial.Value??0,Hit));
        if(removed||barrier||result.Result==2){_bolts.Remove(bolt.Id);return;}
        _bolts[bolt.Id]=next with{NextUtc=bolt.NextUtc.AddMilliseconds(40)};
        if(next.Frame%2==0){Effects.SendLocationEffect(new Point3D((int)Math.Round(next.X),(int)Math.Round(next.Y),next.Z),body.Map,0x36D4,3,10,0x489,0);} // Identified bolt placeholder.
    }
    private static bool Position(uint owner,int map,double x,double y,int z)=>((Serial)owner).IsMobile&&map>=0&&map<Map.Maps.Length&&Map.Maps[map]!=null&&Map.Maps[map]!=Map.Internal
        &&double.IsFinite(x+y)&&x>0&&y>0&&x<Map.Maps[map].Width&&y<Map.Maps[map].Height&&z is >=-128 and <=127;
    private static void ValidateSource(HydraHead h)
    {
        if(h.Source is not{} s){return;}
        HydraCreation.Validate(s.Birth);HydraControl.Validate(s.Control);var ends=HydraCreation.Fire(s.Birth,SlowMissilePayload.Normal);
        if(!Position(h.Owner,h.Map,h.X,h.Y,h.Z)||!((Serial)s.Actor).IsMobile||s.NextUtc.Kind!=DateTimeKind.Utc||h.UntilUtc.Kind!=DateTimeKind.Utc
            ||h.Rank!=s.Birth.Rank||h.Min256!=ends.Min||h.Max256!=ends.Max||h.Pierce!=s.Birth.Passive("passive_fire_pierce")||h.Seed!=s.Control.Seed
            ||s.Birth.Passive("item_pierce_fire_immunity")!=0||h.X*2!=Math.Floor(h.X*2)||h.Y*2!=Math.Floor(h.Y*2)
            ||s.CachedAim is{} aim&&(aim.X is <0 or >65535||aim.Y is <0 or >65535)||s.RetireUtc is{} retired&&(retired.Kind!=DateTimeKind.Utc||s.Control.Mode!=0))
        {throw new InvalidDataException("Invalid saved Hydra source head");}
    }
    private static void ValidateSource(HydraBolt b,HydraHead head)
    {
        if(b.Source is not{} s){return;}
        int velocity=SlowMissilePayload.VelocityFp8(16,0,head?.Rank??1,33,0,true,s.Slow.VelocityPercent);
        if(!Position(b.Owner,b.Map,b.X,b.Y,b.Z)||!((Serial)s.Actor).IsMobile||head?.Source?.Actor!=s.Actor||head.Owner!=b.Owner||head.Map!=b.Map
            ||s.Slow.VelocityPercent is <0 or >100||s.Slow.DamagePercent is <0 or >100||s.VelocityFp8!=velocity||velocity==0
            ||Math.Abs(b.Vx)>.375||Math.Abs(b.Vy)>.375||s.Steps?.Cells is not{Length:>=1 and <=10}||s.Steps.Cells.Any(c=>c.X is <0 or >65535||c.Y is <0 or >65535)
            ||b.NextUtc.Kind!=DateTimeKind.Utc||b.UntilUtc.Kind!=DateTimeKind.Utc||b.UntilUtc-b.NextUtc!=TimeSpan.FromMilliseconds((29-b.Frame)*40))
        {throw new InvalidDataException("Invalid saved Hydra source bolt");}
        var ends=HydraCreation.Fire(head.Source.Birth,s.Slow);
        if(b.Min256!=ends.Min||b.Max256!=ends.Max||b.Pierce!=head.Pierce){throw new InvalidDataException("Hydra bolt differs from captured head stats");}
    }
    internal static void ValidateWorld()
    {
        foreach(var h in _heads.Values.Concat(_retired.Values).Where(x=>x.Source!=null))
        {
            if(World.FindMobile((Serial)h.Source.Actor) is not HydraBody{Deleted:false} b||b.Head?.Id!=h.Id||b.SummonMaster?.Serial.Value!=h.Owner||b.Map?.MapID!=h.Map
                ||b.Location!=new Point3D((int)Math.Round(h.X),(int)Math.Round(h.Y),h.Z))
            {throw new InvalidDataException("Saved Hydra actor/owner/location graph mismatch");}
        }
    }
}
