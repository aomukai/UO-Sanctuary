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

internal sealed record SorceressSpellFlight(long Id,uint Owner,int Map,int Z,CombatAbility Ability,int Rank,
    double X,double Y,double Vx,double Vy,int VelocityFp8,int Frame,int Frames,int Minimum256,int Maximum256,
    FirestormModifiers Modifiers,SlowMissilePayload Slow,ulong Seed,SourceStepTrace Steps,DateTime NextUtc,string Fault=null);
internal sealed record SorceressSpellSave(SorceressSpellFlight[] Flights,long NextId);

public sealed class SorceressSpellProfiles:GenericPersistence
{
    private SorceressSpellSave _snapshot;
    public SorceressSpellProfiles():base("SanctuarySorceressSpellMissiles",226){}
    public override void Serialize(){_snapshot=SorceressSpellMissiles.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Sorceress spell flight version");}SorceressSpellMissiles.Restore(JsonSerializer.Deserialize<SorceressSpellSave>(reader.ReadString()));}
}

// Player straight spells reuse the existing checked kernels,not a pet actor's
// ownership/AI or a weapon packet. Native room/cell order,initial dispatch and
// creator RNG allocation remain declared world adapters.
internal static class SorceressSpellMissiles
{
    private sealed record Pool(ImmutableDictionary<long,SorceressSpellFlight> Flights,long NextId);
    private static Pool _pool=new(ImmutableDictionary<long,SorceressSpellFlight>.Empty,0);
    private static readonly HashSet<long> Loaded=new();
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SorceressSpellMissiles));
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static void Configure()=>_ = new SorceressSpellProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));

    internal static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(values.Ability);int M(string key)=>NecromancerSummons.Source("firebolt",key);
        if(values.Ability!=CombatAbility.FireBolt||values.Rank is <1 or >255||row.SourceId!=36
            ||row.Number("srvstfunc")!=0||row.Number("srvdofunc")!=0||row.Text("srvmissile")!="firebolt"
            ||row.Text("EType")!="fire"||row.Number("SrcDam")!=0
            ||M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=0||M("pSrvDmgFunc")!=0||M("Vel")!=20||M("MaxVel")!=20
            ||M("VelLev")!=0||M("Accel")!=0||M("Range")!=50||M("Size")!=1||M("CollideType")!=3
            ||M("CollideKill")!=1||M("LastCollide")!=1||M("ToHit")!=0||M("NextHit")!=0||M("Pierce")!=0
            ||M("SrcDamage")!=0||M("CanSlow")!=1||M("InitSteps")!=1||M("Activate")!=0
            ||M("ReturnFire")!=1||M("GetHit")!=1||M("ResultFlags")!=4||M("DamageRate")!=0)
        {throw new NotSupportedException("Unmapped player Fire Bolt route");}
    }
    private static PlayerMobile Owner(uint id)=>World.FindMobile((Serial)id) is PlayerMobile{Alive:true,Deleted:false} p
        &&ClassCreation.Profiles.TryGet(p.Serial,out var profile)&&profile.Class==DiabloClass.Sorceress?p:null;
    internal static SorceressSpellFlight Prepare(PlayerMobile owner,Point3D point,SkillValues values,DateTime at)
    {
        Route(values);
        if(owner==null||Owner(owner.Serial.Value)!=owner||owner.Map==null||owner.Map==Map.Internal
            ||point.X<=0||point.Y<=0||point.X>=owner.Map.Width||point.Y>=owner.Map.Height||at.Kind!=DateTimeKind.Utc
            ||SpellHelper.IsTown(owner.Location,owner)||SpellHelper.IsTown(point,owner))
        {throw new NotSupportedException("Unsupported player Fire Bolt owner/aim");}
        var slow=AmazonSlowMissiles.Capture(owner,true);var mods=DruidFirestorm.Modifiers(owner);
        var ends=DruidFirestorm.Endpoints(values,mods.FireMastery,slow);
        // Default non-lob skill launch supplies33. No weapon AR/damage/ammo.
        int velocity=SlowMissilePayload.VelocityFp8(20,0,values.Rank,33,0,true,slow.VelocityPercent);
        int dx=point.X-owner.X,dy=point.Y-owner.Y;if(dx==0&&dy==0){dx=dy=1;} // Existing shared zero-vector projection.
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);
        var flight=new SorceressSpellFlight(checked(_pool.NextId+1),owner.Serial.Value,owner.Map.MapID,owner.Z,
            values.Ability,values.Rank,owner.X,owner.Y,vector.X,vector.Y,velocity,0,50,ends.Min,ends.Max,mods,slow,
            SourceRandom.InitLow(SeedLow()),new([new(owner.X*2,owner.Y*2)]),at.AddMilliseconds(40));
        Validate(flight);return flight;
    }
    internal static long Commit(SorceressSpellFlight flight)
    {
        Validate(flight);if(flight.Id!=_pool.NextId+1){throw new InvalidOperationException("Stale prepared Sorceress flight");}
        _pool=new(_pool.Flights.Add(flight.Id,Copy(flight)),flight.Id);return flight.Id;
    }
    internal static bool Cast(PlayerMobile owner,Point3D point,SkillValues values)
    {
        // Preparation publishes no flight;lost mana cannot emit a packet.
        var flight=Prepare(owner,point,values,TimedEffects.Now);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        Commit(flight);owner.PlaySound(0x1E5);return true; // Identified cast placeholder.
    }
    private static SorceressSpellFlight Copy(SorceressSpellFlight s)=>s with{Steps=new(s.Steps.Cells.ToArray())};
    internal static SorceressSpellSave Snapshot()
    {var pool=_pool;return new(pool.Flights.Values.OrderBy(s=>s.Id).Select(Copy).ToArray(),pool.NextId);}
    private static void Remove(long id){_pool=_pool with{Flights=_pool.Flights.Remove(id)};Loaded.Remove(id);}
    internal static void Cancel(Mobile owner)
    {if(owner!=null){foreach(long id in _pool.Flights.Values.Where(s=>s.Owner==owner.Serial.Value).Select(s=>s.Id).ToArray()){Remove(id);}}}

    internal static int Impact(PlayerMobile owner,Mobile target,SorceressSpellFlight flight,out ulong seed)
    {
        ulong stream=flight.Seed;int raw=WeaponMissileDamage.RollValue(flight.Minimum256,flight.Maximum256,0,n=>(int)SourceRandom.Limited(ref stream,n));seed=stream;
        SorceressEffects.MissileHit(owner,target,"firebolt");
        if(ClassAvoidance.Missile(target,"firebolt")){return 0;}
        owner.DoHarmful(target,true);
        // T29 checks potion eligibility at contact. Reuse the same captured
        // fire endpoints/four-element modifier consumer as Firestorm.
        var modifiers=flight.Modifiers with{Potion=CombatPotions.BonusAt(owner,"magical-penetration",flight.NextUtc)};
        var parts=SkillCombat.MitigatePrepared(owner,target,DruidFirestorm.Packet(modifiers,raw));
        int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
        if(target is BaseCreature creature){CreatureHitRecovery.Apply(creature,owner,loss);}
        CombatDamage.Finish(target,owner);return loss;
    }
    private static void Advance(SorceressSpellFlight flight,DateTime at,bool? actions)
    {
        var owner=Owner(flight.Owner);if(owner==null||owner.Map!=Map.Internal&&owner.Map?.MapID!=flight.Map){Remove(flight.Id);return;}
        bool loaded=Loaded.Remove(flight.Id);
        int elapsed=loaded?(int)Math.Min(flight.Frames-flight.Frame,(at-flight.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1):1;
        int frame=flight.Frame+elapsed;var next=flight with{Frame=frame,NextUtc=flight.NextUtc.AddMilliseconds(40L*elapsed)};
        // Existing persistent-spell host policy:offline/restart age harmlessly,
        // never replay past damage. This is not a modern owner-removal proof.
        if(loaded||flight.Fault!=null||owner.Map==Map.Internal||!(actions??owner.NetState is{Running:true}))
        {if(frame>=flight.Frames){Remove(flight.Id);}else{_pool=_pool with{Flights=_pool.Flights.SetItem(flight.Id,next)};}return;}
        var map=Map.Maps[flight.Map];bool barrier=false,removed=false;
        if(SpellHelper.IsTown(new Point3D((int)Math.Round(flight.X),(int)Math.Round(flight.Y),flight.Z),owner)){Remove(flight.Id);return;}
        int Move()
        {
            next=next with{X=flight.X+flight.Vx,Y=flight.Y+flight.Vy,Steps=SourceMissileSteps.Advance(flight.X,flight.Y,flight.Vx,flight.Vy,flight.Steps)};
            barrier=next.X<0||next.Y<0||next.X>=map.Width||next.Y>=map.Height;return barrier?2:1;
        }
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var candidate in map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,flight.Z),3))
            {
                if(candidate is not{Alive:true,Deleted:false,Blessed:false}||Math.Abs(candidate.Z-flight.Z)>=16
                    ||!ClassCombat.IsHostile(owner,candidate)||!owner.CanBeHarmful(candidate,false)||SpellHelper.IsTown(candidate.Location,owner)){continue;}
                int size=candidate is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??2:2;
                if(CombatHitboxes.SourceTouches(1,size,cell.X-candidate.X*2,cell.Y-candidate.Y*2)){return candidate;}
            }
            return null;
        }
        bool Wall(SourceMissileCell cell)
        {bool blocked=DruidFirestorm.CellBlocked(map,flight.Z,flight.X,flight.Y,cell.X,cell.Y);if(blocked){barrier=true;}return blocked;}
        int Hit(uint serial,bool terminal)
        {
            if(!terminal&&World.FindMobile((Serial)serial) is{} target)
            {Impact(owner,target,flight,out var seed);next=next with{Seed=seed};}
            removed=true;return 2; // CollideKill1; no lifetime hit-set or piercing.
        }
        var result=BasicMissileUpdate.Run(new(true,true,flight.VelocityFp8,flight.Frames-flight.Frame,2,flight.Frames,true),
            new(Move,_=>{},()=>true,()=>true,()=>2,()=>next.Steps.Cells,
                cell=>Wall(cell)?4:Find(cell)!=null?1:0,cell=>Wall(cell)?0:Find(cell)?.Serial.Value??0,Hit));
        if(removed||barrier||result.Result==2){Remove(flight.Id);return;}
        // A reactive consumer may have cleared the owner's flights.
        if(_pool.Flights.ContainsKey(flight.Id)){_pool=_pool with{Flights=_pool.Flights.SetItem(flight.Id,next)};}
        if(frame%2==0){Effects.SendLocationEffect(new((int)Math.Round(next.X),(int)Math.Round(next.Y),flight.Z),map,0x36D4,2,10,0x489,0);} // Identified shared Fire Bolt placeholder.
    }
    internal static void Tick(DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Sorceress flight clock must be UTC");}
        while(true)
        {
            var flight=_pool.Flights.Values.Where(s=>s.NextUtc<=at).OrderBy(s=>s.NextUtc).ThenByDescending(s=>s.Id).FirstOrDefault();if(flight==null){return;}
            try{Advance(flight,at,actions);}
            catch(NotSupportedException e)
            {
                Log.Error(e,"Sorceress flight suspended: {Id}",flight.Id);
                if(_pool.Flights.ContainsKey(flight.Id))
                {if(flight.Frame+1>=flight.Frames){Remove(flight.Id);}else{_pool=_pool with{Flights=_pool.Flights.SetItem(flight.Id,flight with{Frame=flight.Frame+1,NextUtc=flight.NextUtc.AddMilliseconds(40),Fault=e.Message})};}}
            }
        }
    }
    private static void Validate(SorceressSpellFlight s)
    {
        if(s==null||s.Id<=0||!((Serial)s.Owner).IsMobile||s.Ability!=CombatAbility.FireBolt||s.Rank is <1 or >255
            ||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal||s.Z is <-128 or >127
            ||!double.IsFinite(s.X+s.Y+s.Vx+s.Vy)||s.X<=0||s.Y<=0||s.X>=Map.Maps[s.Map].Width||s.Y>=Map.Maps[s.Map].Height
            ||Math.Abs(s.Vx)>0.5||Math.Abs(s.Vy)>0.5||s.VelocityFp8 is <0 or >3840||s.Frame<0||s.Frames!=50||s.Frame>=s.Frames
            ||s.Minimum256<0||s.Maximum256<s.Minimum256||s.Modifiers==null||s.Slow.VelocityPercent is <0 or >100||s.Slow.DamagePercent is <0 or >100
            ||s.NextUtc.Kind!=DateTimeKind.Utc||s.Steps?.Cells is not{Length:>=1 and <=10}
            ||s.Steps.Cells.Any(c=>c.X<0||c.Y<0||c.X>=Map.Maps[s.Map].Width*2||c.Y>=Map.Maps[s.Map].Height*2))
        {throw new InvalidDataException("Invalid saved Sorceress spell flight");}
    }
    internal static void Restore(SorceressSpellSave saved)
    {
        if(saved?.Flights==null||saved.NextId<0){throw new InvalidDataException("Invalid Sorceress flight save");}
        var flights=ImmutableDictionary<long,SorceressSpellFlight>.Empty;
        foreach(var flight in saved.Flights){Validate(flight);if(flight.Id>saved.NextId||flights.ContainsKey(flight.Id)){throw new InvalidDataException("Duplicate Sorceress flight");}flights=flights.Add(flight.Id,Copy(flight));}
        _pool=new(flights,saved.NextId);Loaded.Clear();foreach(var id in flights.Keys){Loaded.Add(id);}
    }
}
