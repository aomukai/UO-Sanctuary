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

public sealed record HolyBoltMotion(int VelocityFp8,ulong Seed,SourceStepTrace Steps,string Fault=null);

// Holy Bolt/FoH's saved do1 consumer. Hit7's ally/species/piercing
// contract is distinct from enemy-only Sorceress/Necromancer consumers.
internal static class HolyBoltFlights
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HolyBoltFlights));
    private static readonly HashSet<long> Restored=new();
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    public static void Configure()=>EventSink.WorldLoad+=RestoreReferences;
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void Loaded(IEnumerable<long> ids){Restored.Clear();foreach(long id in ids){Restored.Add(id);}}
    internal static void Forget(long id)=>Restored.Remove(id);
    internal static void Cancel(Mobile actor)
    {if(actor!=null){foreach(var f in HolyBolts.Snapshot().Where(f=>f.Owner==actor.Serial.Value&&f.Motion!=null)){HolyBolts.Remove(f.Id);}}}
    internal static void RestoreReferences()
    {
        // UO may reuse a deleted actor's serial after boot. Resolve references
        // once all saved actors exist,before newly spawned actors can alias it.
        foreach(var f in HolyBolts.Snapshot().Where(f=>f.Motion!=null&&f.LastCollision!=0&&World.FindMobile((Serial)f.LastCollision)==null))
        {HolyBolts.Update(f with{LastCollision=0});Log.Information("Holy Bolt cleared absent saved collider:{Flight}/{Collider}",f.Id,f.LastCollision);}
    }
    internal static void Validate(HolyBoltFlight f)
    {
        int basis=f?.Ability==CombatAbility.FistOfTheHeavens?12:20;double maximum=basis*.0234375;
        if(f?.Motion is not{} m||f.Ability is not (CombatAbility.HolyBolt or CombatAbility.FistOfTheHeavens)||f.Captured is not{Modifiers:not null} c||f.Id<=0||!((Serial)f.Owner).IsMobile||f.Rank is <1 or >255
            ||f.Map<0||f.Map>=Map.Maps.Length||Map.Maps[f.Map]==null||Map.Maps[f.Map]==Map.Internal||f.Z is <-128 or >127
            ||!double.IsFinite(f.X+f.Y+f.Vx+f.Vy)||f.X<=0||f.Y<=0||f.X>=Map.Maps[f.Map].Width||f.Y>=Map.Maps[f.Map].Height
            ||Math.Abs(f.Vx)>maximum||Math.Abs(f.Vy)>maximum||f.Frame is <0 or >=50||f.Min256<0||f.Max256<f.Min256
            ||f.LastCollision!=0&&!((Serial)f.LastCollision).IsMobile||f.NextUtc.Kind!=DateTimeKind.Utc||f.UntilUtc.Kind!=DateTimeKind.Utc
            ||f.NextUtc>DateTime.MaxValue.AddMilliseconds(-2000)||c.Modifiers.Potion!=0||c.Slow.VelocityPercent is <0 or >100||c.Slow.DamagePercent is <0 or >100
            ||m.VelocityFp8!=SlowMissilePayload.VelocityFp8(basis,0,f.Rank,33,0,true,c.Slow.VelocityPercent)
            ||m.Steps?.Cells is not{Length:>=1 and <=10}||m.Steps.Cells.Any(p=>p.X<0||p.Y<0||p.X>=Map.Maps[f.Map].Width*2||p.Y>=Map.Maps[f.Map].Height*2))
        {throw new InvalidDataException("Invalid saved Holy Bolt source flight");}
    }
    internal static ulong Impact(PlayerMobile owner,Mobile target,HolyBoltFlight flight,DateTime at)
    {
        ulong seed=flight.Motion.Seed;
        int Sample(int min,int max)=>min+(max>min?(int)SourceRandom.Limited(ref seed,max-min):0);
        if(ClassCombat.Friendly(owner,target))
        {
            var row=SkillMechanics.For(flight.Ability);
            if(target is PlayerMobile?row.Number("TargetAlly")==0:row.Number("TargetPet")==0){return seed;}
            var heal=HolyBolts.Healing(owner,flight.Rank);SkillHealth.Heal(target,Sample(heal.Min,heal.Max));
            target.FixedEffect(0x376A,10,10,0x481,0);return seed; // Identified healing placeholder.
        }
        if(!HolyBolts.FullDamage(target)){return seed;}
        if(!HolyBoltDamage.Supported(owner,target,out string reason)){throw new NotSupportedException(reason);}
        string missile=HolyBolts.Missile(flight.Ability);
        SorceressEffects.MissileHit(owner,target,missile);if(ClassAvoidance.Missile(target,missile)){return seed;}
        HolyBoltDamage.Deliver(owner,(BaseCreature)target,Sample(flight.Min256,flight.Max256),flight.Captured,at,flight.Ability);return seed;
    }
    private static void Advance(HolyBoltFlight f,DateTime at,bool? actions)
    {
        if(World.FindMobile((Serial)f.Owner) is not PlayerMobile{Alive:true,Deleted:false} owner
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Paladin
            ||owner.Map!=Map.Internal&&owner.Map?.MapID!=f.Map)
        {HolyBolts.Remove(f.Id);return;}
        bool loaded=Restored.Remove(f.Id);
        int elapsed=loaded?(int)Math.Min(50-f.Frame,(at-f.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1):1;
        var next=f with{Frame=f.Frame+elapsed,NextUtc=f.NextUtc.AddMilliseconds(elapsed*40L)};
        // Reuse the persistent-spell host policy:loaded/offline periods age
        // harmlessly. Exact current D2R owner-removal policy remains unverified.
        if(loaded||f.Motion.Fault!=null||owner.Map==Map.Internal||!(actions??owner.NetState is{Running:true}))
        {if(next.Frame>=50){HolyBolts.Remove(f.Id);}else{HolyBolts.Update(next);}return;}
        var map=Map.Maps[f.Map];bool barrier=false,removed=false;
        if(SpellHelper.IsTown(new Point3D((int)Math.Round(f.X),(int)Math.Round(f.Y),f.Z),owner)){HolyBolts.Remove(f.Id);return;}
        int Move()
        {
            double x=f.X+f.Vx,y=f.Y+f.Vy;
            next=next with{X=x,Y=y,Motion=f.Motion with{Steps=SourceMissileSteps.Advance(f.X,f.Y,f.Vx,f.Vy,f.Motion.Steps)}};
            barrier=x<=0||y<=0||x>=map.Width||y>=map.Height;return barrier?2:1;
        }
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var target in map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,f.Z),3))
            {
                if(target.Serial.Value==f.LastCollision||!HolyBolts.Candidate(owner,target)||Math.Abs(target.Z-f.Z)>=16
                    ||SpellHelper.IsTown(target.Location,owner)||ClassCombat.IsHostile(owner,target)&&!owner.CanBeHarmful(target,false)){continue;}
                int size=target is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??2:2;
                if(CombatHitboxes.SourceTouches(1,size,cell.X-target.X*2,cell.Y-target.Y*2)){return target;}
            }
            return null;
        }
        bool Wall(SourceMissileCell cell)
        {bool blocked=DruidFirestorm.CellBlocked(map,f.Z,f.X,f.Y,cell.X,cell.Y);if(blocked){barrier=true;}return blocked;}
        int Hit(uint serial,bool terminal)
        {
            if(terminal){removed=true;return 2;}
            if(World.FindMobile((Serial)serial) is{} target)
            {ulong seed=Impact(owner,target,f,f.NextUtc);next=next with{LastCollision=serial,Motion=next.Motion with{Seed=seed}};}
            return 1; // Modern sHitPar3=0:unit collisions never consume the flight.
        }
        var result=BasicMissileUpdate.Run(new(true,true,f.Motion.VelocityFp8,50-f.Frame,2,50-NecromancerSummons.Source(HolyBolts.Missile(f.Ability),"Activate"),true),
            new(Move,_=>{},()=>true,()=>true,()=>2,()=>next.Motion.Steps.Cells,
                cell=>Wall(cell)?4:Find(cell)!=null?1:0,cell=>Wall(cell)?0:Find(cell)?.Serial.Value??0,Hit));
        if(removed||barrier||result.Result==2){HolyBolts.Remove(f.Id);return;}
        if(HolyBolts.State(f.Id)!=null){HolyBolts.Update(next);}
        if(next.Frame%2==0){Effects.SendLocationEffect(new((int)Math.Round(next.X),(int)Math.Round(next.Y),f.Z),map,0x36E4,2,10,0x481,0);} // Identified holy travel placeholder.
    }
    internal static void TickFlight(long id,DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Holy Bolt clock must be UTC");}
        var f=HolyBolts.State(id);if(f?.Motion==null||f.NextUtc>at){return;}
        try{Advance(f,at,actions);}
        catch(NotSupportedException e)
        {
            Log.Warning("Holy Bolt flight suspended:{Id};{Reason}",id,e.Message);
            if(HolyBolts.State(id)==null){return;}
            if(f.Frame+1>=50){HolyBolts.Remove(id);}else{HolyBolts.Update(f with{Frame=f.Frame+1,NextUtc=f.NextUtc.AddMilliseconds(40),Motion=f.Motion with{Fault=e.Message}});}
        }
    }
    internal static void Tick(DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Holy Bolt clock must be UTC");}
        while(HolyBolts.Due(at) is{} f){TickFlight(f.Id,at,actions);}
    }
}
