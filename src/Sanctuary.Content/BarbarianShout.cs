// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record ShoutState(uint Recipient,uint Creator,uint Supplier,int Rank,int DefensePercent,DateTime UntilUtc);
public sealed record ShoutRay(long Id,uint Owner,int Map,int Rank,int Ray,double X,double Y,int Z,int Frame,int Velocity,int AccelerationCounter,DateTime UntilUtc,DateTime NextUtc,CombatAbility Ability=CombatAbility.Shout,uint LastCollision=0);
public sealed record WarcryDelay(CombatAbility Ability,uint Recipient,DateTime UntilUtc);
public sealed record ShoutSave(ShoutState[] States,ShoutRay[] Rays,WarcryDelay[] Delays=null);
public sealed class ShoutProfiles:GenericPersistence
{
    public ShoutProfiles():base("SanctuaryBarbarianShout",140){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(BarbarianShout.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Shout version");}
        BarbarianShout.Restore(JsonSerializer.Deserialize<ShoutSave>(reader.ReadString()));
    }
}
public static class BarbarianShout
{
    // Supplementary source sub_6FD14170; do not substitute an ideal circular area.
    private static readonly int[] _x=[30,29,29,28,27,26,24,23,21,19,16,14,11,8,5,2,0,-2,-5,-8,-11,-14,-16,-19,-21,-23,-24,-26,-27,-28,-29,-29,-30,-29,-29,-28,-27,-26,-24,-23,-21,-19,-16,-14,-11,-8,-5,-2,0,2,5,8,11,14,16,19,21,23,24,26,27,28,29,29];
    private static readonly int[] _y=[0,2,5,8,11,14,16,19,21,23,24,26,27,28,29,29,30,29,29,28,27,26,24,23,21,19,16,14,11,8,5,2,0,-2,-5,-8,-11,-14,-16,-19,-21,-23,-24,-26,-27,-28,-29,-29,-30,-29,-29,-28,-27,-26,-24,-23,-21,-19,-16,-14,-11,-8,-5,-2];
    private static readonly Dictionary<uint,ShoutState> _states=new();
    private static readonly Dictionary<long,ShoutRay> _rays=new();
    private static readonly List<uint> _expired=new();
    private static readonly Dictionary<(CombatAbility,uint),DateTime> _delays=new();
    private static long _next;
    public static ShoutProfiles Profiles{get;private set;}
    public static int Frames=>NecromancerSummons.Source("shout","Range");
    internal static string Missile(CombatAbility ability)=>ability==CombatAbility.BattleOrders?"battleorders":"shout";
    internal static bool Delayed(CombatAbility ability,uint recipient,DateTime at)=>_delays.TryGetValue((ability,recipient),out var until) && until>at;
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Tick);
    private static bool Alive(Mobile target)=>target is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    public static ShoutState State(Mobile target)=>Alive(target) && _states.TryGetValue(target.Serial.Value,out var state) && TimedEffects.Now<state.UntilUtc?state:null;
    public static int Defense(Mobile target)=>State(target)?.DefensePercent??0;
    public static bool ValidCast(PlayerMobile owner)=>owner.Map!=null && owner.Map!=Map.Internal && SpellHelper.CheckTown(owner.Location,owner);
    // Retains approved shared native SC timing; exact modern event/FCR equivalence remains open.
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>SkillCombat.Begin(owner,CombatAbility.Shout,selected,point,now,(p,_,_,_)=>ValidCast(p));
    public static bool Release(PlayerMobile owner)
    {
        if(!BarbarianCombat.Available(owner,CombatAbility.Shout) || !ValidCast(owner)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Shout);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        Cast(owner,values,TimedEffects.Now);return true;
    }
    private static void Refresh(Mobile target)
    {
        if(target is PlayerMobile player){ClassStats.Refresh(player);StatNetwork.Send(player);}
    }
    internal static void Apply(PlayerMobile supplier,Mobile recipient,int rank,DateTime at)
    {
        if(!Alive(recipient)){return;}
        // Rank belongs to the launched missile; hard duration synergies are read at collision.
        var values=SkillMechanics.Values(supplier,CombatAbility.Shout,rank);
        uint creator=_states.TryGetValue(recipient.Serial.Value,out var old) && old.UntilUtc>at?old.Creator:supplier.Serial.Value;
        _states[recipient.Serial.Value]=new(recipient.Serial.Value,creator,supplier.Serial.Value,rank,(int)values.Eval("aurastatcalc1"),at.AddMilliseconds(values.Eval("auralencalc")*40));
        Refresh(recipient);
    }
    internal static void Cast(PlayerMobile owner,SkillValues values,DateTime at,CombatAbility ability=CombatAbility.Shout)
    {
        for(int ray=0;ray<64;ray++)
        {
            long id=++_next;
            _rays.Add(id,new(id,owner.Serial.Value,owner.Map.MapID,values.Rank,ray,owner.X,owner.Y,owner.Z,0,
                (NecromancerSummons.Source(Missile(ability),"Vel")<<8)*75/100,0,at.AddMilliseconds(NecromancerSummons.Source(Missile(ability),"Range")*40),at,ability,
                NecromancerSummons.Source(Missile(ability),"LastCollide")!=0?owner.Serial.Value:0));
        }
        if(ability==CombatAbility.BattleOrders){BattleOrders.Apply(owner,owner,values.Rank,at);}else{Apply(owner,owner,values.Rank,at);}
        owner.FixedEffect(0x375A,10,10,0x481,0);owner.PlaySound(0x1E5); // Identified UO placeholders; final cry/wave/state assets pending.
    }
    internal static bool Candidate(PlayerMobile owner,Mobile target)=>Alive(target) && !target.Blessed && target is not BaseVendor
        && target.Map==owner.Map && (target==owner || ClassCombat.Friendly(owner,target) || ClassCombat.IsHostile(owner,target));
    internal static (int Velocity,int Counter,double X,double Y) Advance(int ray,int velocity,int counter)
    {
        if(++counter>=5){velocity=Math.Max(0,velocity+NecromancerSummons.Source("shout","Accel"));counter=0;}
        var direction=MissileVectors.Direction(_x[ray],_y[ray]);
        long magnitude=(1024L*velocity)>>6;
        return(velocity,counter,SkillGeometry.Tiles(((magnitude*direction.X)>>12)/65536.0),SkillGeometry.Tiles(((magnitude*direction.Y)>>12)/65536.0));
    }
    private static void Step(PlayerMobile owner,ShoutRay ray,Map map)
    {
        string missile=Missile(ray.Ability);int frame=ray.Frame+1;
        if(frame>=NecromancerSummons.Source(missile,"Range")){_rays.Remove(ray.Id);return;} // SrvDo1 decrements Range before testing collision.
        var motion=Advance(ray.Ray,ray.Velocity,ray.AccelerationCounter);
        double x=ray.X+motion.X,y=ray.Y+motion.Y;
        var from=new Point3D((int)Math.Round(ray.X),(int)Math.Round(ray.Y),ray.Z+12);
        var to=new Point3D((int)Math.Round(x),(int)Math.Round(y),ray.Z+12);
        // UO LOS is the current shared approximation for source missile barriers; equivalence remains documented as unverified.
        if(x<0 || y<0 || x>=map.Width || y>=map.Height || !map.LineOfSight(from,to)){_rays.Remove(ray.Id);return;}
        Mobile first=null;double nearest=double.MaxValue;
        foreach(var target in map.GetMobilesInRange<Mobile>(new Point3D(to.X,to.Y,ray.Z),2))
        {
            if(Candidate(owner,target) && target.Serial.Value!=ray.LastCollision
                && (NecromancerSummons.Source(missile,"NextHit")==0 || !Delayed(ray.Ability,target.Serial.Value,ray.NextUtc))
                && CombatHitboxes.Intersect(ray.X,ray.Y,x,y,ray.Z,target.Location,out double entry)
                && (entry<nearest || entry==nearest && target.Serial.Value<(first?.Serial.Value??uint.MaxValue)))
            {first=target;nearest=entry;}
        }
        _rays[ray.Id]=ray with{X=x,Y=y,Frame=frame,Velocity=motion.Velocity,AccelerationCounter=motion.Counter,NextUtc=ray.NextUtc.AddMilliseconds(40),LastCollision=NecromancerSummons.Source(missile,"LastCollide")!=0?(first?.Serial.Value??ray.LastCollision):0};
        // LastCollide/NextHit belong to BO; Shout retains neither. The modern
        // skill-local delay is shared across its suppliers/casts pending exact scope capture.
        if(first!=null)
        {
            if(NecromancerSummons.Source(missile,"NextHit")!=0){_delays[(ray.Ability,first.Serial.Value)]=ray.NextUtc.AddMilliseconds(NecromancerSummons.Source(missile,"NextDelay")*40);}
            if(first==owner || ClassCombat.Friendly(owner,first))
            {if(ray.Ability==CombatAbility.BattleOrders){BattleOrders.Apply(owner,first,ray.Rank,ray.NextUtc);}else{Apply(owner,first,ray.Rank,ray.NextUtc);}}
        }
        if(ray.Ray%8==0){Effects.SendLocationEffect(new(to.X,to.Y,ray.Z),map,0x375A,2,10,0x481,0);} // Sparse placeholder travelling wave.
    }
    private static void Tick()
    {
        var now=TimedEffects.Now;
        _expired.Clear();foreach(var pair in _states)
        {if(pair.Value.UntilUtc<=now || !Alive(World.FindMobile((Serial)pair.Key))){_expired.Add(pair.Key);}}
        foreach(uint serial in _expired){Clear(World.FindMobile((Serial)serial));_states.Remove(serial);}
        // Catch up in scheduled frame order across rays/casts. A delayed tick must
        // not let an older ray refresh a recipient after a newer scheduled hit.
        var due=new PriorityQueue<(PlayerMobile Owner,Map Map,long Id),(DateTime At,long Id)>();
        var removed=new List<long>();
        foreach(var ray in _rays.Values)
        {
            if(World.FindMobile((Serial)ray.Owner) is not PlayerMobile{Deleted:false} owner || Map.Maps[ray.Map] is not{} map)
            {removed.Add(ray.Id);continue;}
            // Internalized/offline owners can resume a saved ray only within its absolute lifetime.
            if(owner.Map==Map.Internal){if(ray.UntilUtc<=now){removed.Add(ray.Id);}continue;}
            if(owner.Map!=map || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Barbarian)
            {removed.Add(ray.Id);continue;}
            if(ray.NextUtc<=now && ray.NextUtc<ray.UntilUtc){due.Enqueue((owner,map,ray.Id),(ray.NextUtc,ray.Id));}
            else if(ray.UntilUtc<=now){removed.Add(ray.Id);}
        }
        foreach(long id in removed){_rays.Remove(id);}
        while(due.TryDequeue(out var item,out _))
        {
            if(!_rays.TryGetValue(item.Id,out var ray)){continue;}
            Step(item.Owner,ray,item.Map);
            if(!_rays.TryGetValue(item.Id,out ray)){continue;}
            if(ray.NextUtc<=now && ray.NextUtc<ray.UntilUtc){due.Enqueue(item,(ray.NextUtc,ray.Id));}
            else if(ray.UntilUtc<=now){_rays.Remove(item.Id);}
        }
        var expiredDelays=new List<(CombatAbility,uint)>();foreach(var pair in _delays){if(pair.Value<=now){expiredDelays.Add(pair.Key);}}
        foreach(var key in expiredDelays){_delays.Remove(key);}
    }

    public static void Clear(Mobile recipient)
    {if(recipient!=null && _states.Remove(recipient.Serial.Value)){Refresh(recipient);}}
    public static ShoutSave Snapshot()
    {
        var delays=new List<WarcryDelay>();foreach(var pair in _delays){delays.Add(new(pair.Key.Item1,pair.Key.Item2,pair.Value));}
        return new(new List<ShoutState>(_states.Values).ToArray(),new List<ShoutRay>(_rays.Values).ToArray(),delays.ToArray());
    }
    public static void Restore(ShoutSave saved)
    {
        if(saved?.States==null || saved.Rays==null){throw new InvalidDataException("Missing Shout state");}
        _states.Clear();_rays.Clear();_delays.Clear();_next=0;
        foreach(var state in saved.States)
        {if(state.Recipient==0 || state.Creator==0 || state.Supplier==0 || state.Rank<1 || state.DefensePercent<0 || !_states.TryAdd(state.Recipient,state)){throw new InvalidDataException("Invalid Shout recipient");}}
        foreach(var ray in saved.Rays)
        {
            if(ray.Ability is not (CombatAbility.Shout or CombatAbility.BattleOrders) || ray.Id<=0 || ray.Owner==0 || ray.Rank<1 || ray.Ray is <0 or >=64 || ray.Map<0 || ray.Map>=Map.Maps.Length || Map.Maps[ray.Map]==null
                || ray.Frame<0 || ray.Frame>=NecromancerSummons.Source(Missile(ray.Ability),"Range") || ray.Velocity<0 || ray.AccelerationCounter is <0 or >=5 || !double.IsFinite(ray.X+ray.Y)
                || !_rays.TryAdd(ray.Id,ray)){throw new InvalidDataException("Invalid Shout ray");}
            _next=Math.Max(_next,ray.Id);
        }
        foreach(var delay in saved.Delays??[])
        {if(delay.Ability!=CombatAbility.BattleOrders || delay.Recipient==0 || !_delays.TryAdd((delay.Ability,delay.Recipient),delay.UntilUtc)){throw new InvalidDataException("Invalid warcry hit delay");}}
    }
}
