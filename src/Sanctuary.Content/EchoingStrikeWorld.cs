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

internal sealed record EchoingFlight(long Id,long Cast,int Ordinal,uint Owner,int Map,Point3D Origin,
    double X,double Y,double Vx,double Vy,int OutUpdates,bool Returning,double OutDistance,double ReturnRemaining,
    EchoingStrikePacket Packet,ulong Seed,SourceStepTrace Trace,DateTime CreatedUtc,DateTime NextUtc,DateTime UntilUtc);
internal sealed record EchoingWorldSave(EchoingFlight[] Flights,WeaponFlightLease[] Leases,long NextId);
internal readonly record struct EchoingAdvance(bool Advanced,int Contacts,int Damage256,string Fault=null);
public sealed class EchoingStrikeWorldProfiles:GenericPersistence
{
    public EchoingStrikeWorldProfiles():base("SanctuaryEchoingStrike",253){}
    public override void Serialize(IGenericWriter w){w.Write(0);w.Write(JsonSerializer.Serialize(EchoingStrikeWorld.Snapshot()));}
    public override void Deserialize(IGenericReader r)
    {if(r.ReadInt()!=0){throw new InvalidDataException("Unknown Echoing Strike version");}EchoingStrikeWorld.Restore(JsonSerializer.Deserialize<EchoingWorldSave>(r.ReadString()));}
}

// Complete proposed native-world projection. No automatic clock or learned
// route until the whole documented action/world rule is approved.
internal static class EchoingStrikeWorld
{
    internal static bool Approved=>false;
    private static ImmutableDictionary<long,EchoingFlight> Flights=ImmutableDictionary<long,EchoingFlight>.Empty;
    private static ImmutableDictionary<(long,uint),WeaponFlightLease> Leases=ImmutableDictionary<(long,uint),WeaponFlightLease>.Empty;
    private static long _next;
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    public static void Configure()=>_=new EchoingStrikeWorldProfiles();
    internal static bool Actor(PlayerMobile p)=>p is{Alive:true,Deleted:false,Map:not null}&&p.Map!=Map.Internal
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock&&!MinionSafeAreas.Contains(p);
    internal static bool Aim(PlayerMobile p,Serial selected,ref Point3D point)
        =>Actor(p)&&SkillMechanics.Rank(p,CombatAbility.EchoingStrike)>0&&PlayerMeleeGeometry.WarlockMelee(p)&&LobProjectiles.Aim(p,selected,ref point)&&point!=p.Location;
    internal static EchoingFlight[] For(PlayerMobile p)=>Flights.Values.Where(s=>s.Owner==p.Serial.Value).OrderBy(s=>s.Id).ToArray();
    internal static void Clear(Mobile p)
    {if(p!=null){foreach(var s in Flights.Values.Where(s=>s.Owner==p.Serial.Value).ToArray()){Remove(s.Id);}}}
    private static void Remove(long id)
    {Flights=Flights.Remove(id);foreach(var key in Leases.Keys.Where(k=>k.Item1==id).ToArray()){Leases=Leases.Remove(key);}}
    internal static bool Ready(long id,uint target,DateTime at)=>!Leases.TryGetValue((id,target),out var old)||old.UntilUtc<=at;
    private static Point3D Point(double x,double y,int z)=>new((int)Math.Round(x),(int)Math.Round(y),z);
    internal static long[] ReleasePrepared(PlayerMobile p,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at<=DateTime.UnixEpoch){throw new ArgumentException("Echoing clock must be UTC");}
        if(!Aim(p,Serial.Zero,ref point)){return [];}
        var input=EchoingStrikeInputs.Capture(p);if(!NecromancerEffects.CanPay(p,input.Skill.Mana256)){return [];}
        bool heal=ClassGear.Bonus(p,"noheal")>0;
        _=EchoingStrikePackets.Prepare(input,false,heal);
        if(input.Critical is{Passive:>0}or{Deadly:>0}or{Mastery:>0}){_=EchoingStrikePackets.Prepare(input,true,heal);}
        int dx=(point.X-p.X)*2,dy=(point.Y-p.Y)*2;
        var rays=input.Skill.TotalAttacks==1?new[]{new FanRay(dx,dy,32,0)}:FanGeometry.Create(dx,dy,input.Skill.TotalAttacks);
        ulong stream=SourceRandom.InitLow(SeedLow());long cast=checked(_next+1);var created=new List<EchoingFlight>();
        int velocity=SlowMissilePayload.VelocityFp8(24,0,input.Skill.Rank,32,0,true,input.Slow.VelocityPercent);
        for(int i=0;i<rays.Length;i++)
        {
            bool critical=NpcMissileCritical.Unsigned(input.Critical.Passive,input.Critical.Deadly,input.Critical.Mastery,()=>SourceRandom.Roll(ref stream));
            ulong seed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref stream)));var ray=rays[i];
            var v=MissileVectors.VelocityFp8(ray.X,ray.Y,velocity);
            var s=new EchoingFlight(checked(cast+i),cast,i,p.Serial.Value,p.Map.MapID,p.Location,p.X,p.Y,v.X,v.Y,0,false,0,0,
                EchoingStrikePackets.Prepare(input,critical,heal),seed,new([new(p.X*2,p.Y*2)]),at,at.AddMilliseconds(40),at.AddMilliseconds(1640));
            Validate(s);created.Add(s);
        }
        if(!NecromancerEffects.Pay(p,input.Skill.Mana256)){return [];}
        foreach(var s in created){Flights=Flights.Add(s.Id,s);}_next=created[^1].Id;
        p.PlaySound(0x23B); // Identified Echoing cast/throw audio placeholder.
        return created.Select(s=>s.Id).ToArray();
    }
    internal static void Validate(EchoingFlight s)
    {
        if(s==null||s.Id<=0||s.Cast<=0||s.Cast>s.Id||s.Ordinal is <0 or >4||s.Id-s.Cast!=s.Ordinal||!((Serial)s.Owner).IsMobile
            ||s.Map is <0 or >255||s.Packet?.Input.Owner!=s.Owner||s.Ordinal>=s.Packet.Input.Skill.TotalAttacks||s.Seed==0
            ||!double.IsFinite(s.X)||!double.IsFinite(s.Y)||!double.IsFinite(s.Vx)||!double.IsFinite(s.Vy)
            ||s.X is <0 or >65535||s.Y is <0 or >65535||Math.Abs(s.Vx)>1||Math.Abs(s.Vy)>1||s.OutUpdates is <0 or >20
            ||!double.IsFinite(s.OutDistance)||s.OutDistance is <0 or >30||!double.IsFinite(s.ReturnRemaining)||s.ReturnRemaining<0||s.ReturnRemaining>s.OutDistance+.000001
            ||!s.Returning&&s.ReturnRemaining!=0||s.Trace?.Cells is not{Length:>=1 and <=10}
            ||s.CreatedUtc.Kind!=DateTimeKind.Utc||s.CreatedUtc<=DateTime.UnixEpoch||s.NextUtc.Kind!=DateTimeKind.Utc||s.UntilUtc.Kind!=DateTimeKind.Utc
            ||s.UntilUtc!=s.CreatedUtc.AddMilliseconds(1640)||s.NextUtc<=s.CreatedUtc||s.NextUtc>=s.UntilUtc
            ||(s.NextUtc.Ticks-s.CreatedUtc.Ticks)%TimeSpan.FromMilliseconds(40).Ticks!=0)
        {throw new InvalidDataException("Invalid Echoing flight");}
        EchoingStrikePackets.Validate(s.Packet);
    }
    internal static EchoingWorldSave Snapshot()=>new(Flights.Values.OrderBy(s=>s.Id).ToArray(),Leases.Values.OrderBy(l=>l.Cast).ThenBy(l=>l.Target).ToArray(),_next);
    internal static void Restore(EchoingWorldSave saved)
    {
        if(saved?.Flights==null||saved.Leases==null||saved.NextId<0||saved.Flights.Length>1000000||saved.Leases.Length>1000000){throw new InvalidDataException("Missing Echoing snapshot");}
        var flights=ImmutableDictionary<long,EchoingFlight>.Empty;var leases=ImmutableDictionary<(long,uint),WeaponFlightLease>.Empty;
        foreach(var s in saved.Flights){Validate(s);if(s.Id>saved.NextId||flights.ContainsKey(s.Id)){throw new InvalidDataException("Duplicate/future Echoing ID");}flights=flights.Add(s.Id,s);}
        foreach(var l in saved.Leases)
        {
            if(!flights.TryGetValue(l.Cast,out var s)||!((Serial)l.Target).IsMobile||l.RetainWithoutFlight||l.UntilUtc.Kind!=DateTimeKind.Utc
                ||l.UntilUtc<s.CreatedUtc||l.UntilUtc>s.UntilUtc.AddMilliseconds(800)||leases.ContainsKey((l.Cast,l.Target)))
            {throw new InvalidDataException("Invalid Echoing recipient lease");}
            leases=leases.Add((l.Cast,l.Target),l);
        }
        Flights=flights;Leases=leases;_next=saved.NextId;
    }
    private static bool Enemy(PlayerMobile p,BaseCreature t,int z)=>t is{Alive:true,Deleted:false,Blessed:false}
        &&t.Map==p.Map&&Math.Abs(t.Z-z)<16&&ClassCombat.IsHostile(p,t)&&!Server.Spells.SpellHelper.IsTown(t.Location,p);
    private static EchoingAdvance Contact(PlayerMobile p,BaseCreature target,EchoingFlight s)
    {
        if(!Ready(s.Id,target.Serial.Value,s.NextUtc)){return default;}
        TaintedFireBallMissiles.RequireOrdinaryRecipient(p,target);
        if(!SourceBlocking.TryRate(target,out _,out _)){return new(false,0,0,"Echoing recipient block context is not mapped");}
        Leases=Leases.SetItem((s.Id,target.Serial.Value),new(s.Id,target.Serial.Value,s.NextUtc.AddMilliseconds(800)));
        SorceressEffects.MissileHit(p,target,"echoingstrike");
        // Owned706 has GetHit1. The shared explicit-context overload avoids
        // pretending this new missile is in the older summon lookup catalog.
        if(!Actor(p)||!Enemy(p,target,s.Origin.Z)||ClassAvoidance.Missile(target,reaction:true)){return new(true,1,0);}
        ulong seed=s.Seed;var raw=EchoingStrikePackets.Roll(s.Packet,target,ref seed,out int poisonRate);
        if(!Flights.TryGetValue(s.Id,out var live)){return default;}
        Flights=Flights.SetItem(s.Id,live with{Seed=seed});
        if(!SourceBlocking.TryBlock(target,raw.Physical>0,out bool blocked)){return new(false,0,0,"Echoing block supplier unavailable");}
        if(blocked){return new(true,1,0);}
        p.DoHarmful(target,true);
        // Explicit live Hex trigger boundary,after block and before damage;
        // keep shared group/reaction/ownership policy and no weapon procs.
        HexBaneEvents.QualifiedHexTrigger(p,target,true);HexSiphonEvents.QualifiedHexTrigger(p,target,true);HexPurgeEvents.QualifiedHexTrigger(p,target,true);
        var parts=SkillCombat.MitigatePrepared(p,target,raw);
        NecromancerEffects.LifeTap(p,target,NamedElements.To256(parts.Physical));WeaponLeech.Apply(p,target,NamedElements.To256(parts.Physical),s.Packet.BaseDamage.Elements);
        if(raw.Cold>0){ColdStates.Apply(p,target,s.Packet.BaseDamage.Elements.ColdFrames,TimedEffects.TickAt(s.NextUtc));}
        if(s.Packet.PreventHeal){TimedEffects.PreventHealing(target,s.NextUtc);}
        int before=SkillHealth.Balance256(target);int loss=SkillHealth.TypedDamage(p,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts));
        CreatureHitRecovery.Apply(target,p,loss);
        if(target is{Alive:true,Deleted:false})
        {if(poisonRate>0){PoisonJavelins.Poison(p,target,poisonRate,s.Packet.BaseDamage.Elements.PoisonImpactFrames,s.NextUtc);}target.FixedEffect(0x375A,5,5,0x48D,0);}
        CombatDamage.Finish(target,p);
        int after=target is{Alive:true,Deleted:false}?SkillHealth.Balance256(target):0;
        return new(true,1,Math.Max(0,before-after));
    }
    internal static EchoingAdvance AdvancePrepared(long id,DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Echoing clock must be UTC");}
        if(!Flights.TryGetValue(id,out var s)){return default;}
        var p=World.FindMobile((Serial)s.Owner)as PlayerMobile;
        if(at>=s.UntilUtc||!Actor(p)||p.Map.MapID!=s.Map||Math.Abs(p.Z-s.Origin.Z)>=16||!(actions??p.NetState is{Running:true}))
        {Remove(id);return default;}
        if(at<s.NextUtc){return default;}
        double vx=s.Vx,vy=s.Vy,step=Math.Sqrt(vx*vx+vy*vy);
        if(s.Returning)
        {
            double dx=p.X-s.X,dy=p.Y-s.Y,distance=Math.Sqrt(dx*dx+dy*dy);
            if(distance<.25||s.ReturnRemaining<=0||step==0){Remove(id);return default;}
            var v=MissileVectors.VelocityFp8((int)Math.Round(dx*1024),(int)Math.Round(dy*1024),
                SlowMissilePayload.VelocityFp8(24,0,s.Packet.Input.Skill.Rank,32,0,true,s.Packet.Input.Slow.VelocityPercent));
            vx=v.X;vy=v.Y;step=Math.Sqrt(vx*vx+vy*vy);double cap=Math.Min(distance,s.ReturnRemaining);
            if(step>cap){vx*=cap/step;vy*=cap/step;step=cap;}
        }
        var trace=SourceMissileSteps.Advance(s.X,s.Y,vx,vy,s.Trace);var last=Point(s.X,s.Y,s.Origin.Z);bool wall=false;var found=new Dictionary<uint,BaseCreature>();
        foreach(var cell in trace.Cells)
        {
            var point=Point(cell.X/2.0,cell.Y/2.0,s.Origin.Z);
            if(point.X<0||point.Y<0||point.X>=p.Map.Width||point.Y>=p.Map.Height||LobProjectiles.Barrier(p.Map,point)
                ||!p.Map.LineOfSight(new Point3D(last.X,last.Y,last.Z+12),new Point3D(point.X,point.Y,point.Z+12))){wall=true;break;}
            last=point;
            foreach(var t in SkillCombat.NearbySnapshot<BaseCreature>(p.Map,point,3))
            {
                if(!Enemy(p,t,s.Origin.Z)){continue;}int size=CreatureProfiles.For(t).Traits.Size??2;
                if(CombatHitboxes.SourceTouches(1,size,cell.X-t.X*2,cell.Y-t.Y*2)){found.TryAdd(t.Serial.Value,t);}
            }
        }
        try
        {
            foreach(var target in found.Values){TaintedFireBallMissiles.RequireOrdinaryRecipient(p,target);if(!SourceBlocking.TryRate(target,out _,out _)){throw new NotSupportedException("Echoing native block context is not mapped");}}
        }
        catch(NotSupportedException ex){Remove(id);return new(false,0,0,ex.Message);}
        if(wall)
        {
            if(s.Returning||s.OutDistance==0){Remove(id);return new(true,0,0);}
            // Last successful position is the return origin;never cross the wall.
            Flights=Flights.SetItem(id,s with{Returning=true,ReturnRemaining=s.OutDistance,NextUtc=s.NextUtc.AddMilliseconds(40)});
        }
        else
        {
            int updates=s.OutUpdates+(s.Returning?0:1);double outbound=s.OutDistance+(s.Returning?0:step);
            bool turn=!s.Returning&&updates==20;var next=s with{X=s.X+vx,Y=s.Y+vy,Vx=vx,Vy=vy,OutUpdates=updates,
                Returning=s.Returning||turn,OutDistance=outbound,ReturnRemaining=s.Returning?Math.Max(0,s.ReturnRemaining-step):turn?outbound:0,
                Trace=trace,NextUtc=s.NextUtc.AddMilliseconds(40)};
            Flights=Flights.SetItem(id,next);
        }
        int contacts=0,damage=0;
        foreach(var target in found.Values.OrderBy(t=>t.Serial.Value))
        {
            if(!Flights.TryGetValue(id,out var live)||!Actor(p)||p.Map.MapID!=s.Map){break;}
            if(!Enemy(p,target,s.Origin.Z)){continue;}
            var result=Contact(p,target,s with{Seed=live.Seed});if(result.Fault!=null){Remove(id);return new(true,contacts,damage,result.Fault);}
            contacts+=result.Contacts;damage=(int)Math.Min(int.MaxValue,(long)damage+result.Damage256);
        }
        if(Flights.TryGetValue(id,out var remaining))
        {
            if(remaining.NextUtc>=remaining.UntilUtc||remaining.Returning&&(remaining.ReturnRemaining<=.000001||Math.Abs(remaining.X-p.X)<.25&&Math.Abs(remaining.Y-p.Y)<.25)){Remove(id);}
            else{Effects.SendLocationEffect(Point(remaining.X,remaining.Y,s.Origin.Z),p.Map,0x36E4,2,10,0x48D,0);} // Identified moving-blade placeholder;visual720 adds no damage actor.
        }
        return new(true,contacts,damage);
    }
    internal static EchoingAdvance TickPrepared(DateTime at,bool? actions=null,uint? owner=null)
    {
        int contacts=0,damage=0;bool advanced=false;
        foreach(var initial in Flights.Values.Where(s=>owner==null||s.Owner==owner).OrderBy(s=>s.Id).ToArray())
        {
            if(at>=initial.UntilUtc){Remove(initial.Id);continue;}
            while(Flights.TryGetValue(initial.Id,out var s)&&s.NextUtc<=at)
            {
                EchoingAdvance result;
                try{result=AdvancePrepared(s.Id,s.NextUtc,actions);}
                catch(NotSupportedException ex){Remove(s.Id);return new(advanced,contacts,damage,ex.Message);}
                advanced|=result.Advanced;contacts+=result.Contacts;damage=(int)Math.Min(int.MaxValue,(long)damage+result.Damage256);
                if(result.Fault!=null){return new(advanced,contacts,damage,result.Fault);}
            }
        }
        return new(advanced,contacts,damage);
    }
}
