// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record HolyBoltFlight(long Id,uint Owner,int Map,int Rank,double X,double Y,int Z,double Vx,double Vy,
    int Frame,int Min256,int Max256,uint LastCollision,DateTime UntilUtc,DateTime NextUtc,CombatAbility Ability=CombatAbility.HolyBolt)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public HolyBoltCapture Captured{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public HolyBoltMotion Motion{get;init;}
}
public sealed class HolyBoltProfiles:GenericPersistence
{
    private HolyBoltFlight[] _snapshot;
    public HolyBoltProfiles():base("SanctuaryHolyBolts",123){}
    public override void Serialize(){_snapshot=HolyBolts.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown holy bolt version");}
        HolyBolts.Restore(JsonSerializer.Deserialize<HolyBoltFlight[]>(reader.ReadString()));
    }
}
public static class HolyBolts
{
    private static readonly Dictionary<long,HolyBoltFlight> _flights=new();
    private static readonly List<long> _keys=new();
    private static long _next;
    internal static Func<int,int> Roll=static range=>Utility.Random(range);
    public static HolyBoltProfiles Profiles{get;private set;}
    public static int Frames=>NecromancerSummons.Source("holybolt","Range");
    public static string Missile(CombatAbility ability)=>ability==CombatAbility.HolyBolt?"holybolt":"fistoftheheavensbolt";
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),Tick);
    public static bool Candidate(PlayerMobile owner,Mobile target)=>target!=owner && target is{Alive:true,Deleted:false,Blessed:false,IsDeadBondedPet:false}
        && target.Map==owner.Map && target is not BaseVendor && (ClassCombat.Friendly(owner,target) || ClassCombat.IsHostile(owner,target));
    public static bool FullDamage(Mobile target)=>GemCombat.Undead(target) || NamedCombat.Demon(target);
    public static (int Min,int Max) Healing(PlayerMobile owner,int rank)
    {
        // Legacy hit callback evaluates with the stored missile rank and CURRENT Prayer hard points.
        var values=SkillMechanics.Values(owner,CombatAbility.HolyBolt,rank);
        return ((int)Math.Clamp(values.Eval("calc1")*256,0,int.MaxValue),(int)Math.Clamp(values.Eval("calc2")*256,0,int.MaxValue));
    }
    public static (int Min,int Max) SecondaryDamage(PlayerMobile owner,SkillValues values)
    {
        const string missile="fistoftheheavensbolt";
        int Damage(bool maximum)
        {
            string prefix=maximum?"Max":"Min";
            long value=(long)SkillValues.Tier(NecromancerSummons.Source(missile,maximum?"EMax":"EMin"),values.Rank,
                tier=>NecromancerSummons.Source(missile,prefix+"ELev"+tier))<<NecromancerSummons.Source(missile,"HitShift",8);
            value=value*(100+NecromancerSummons.Formula(missile,"EDmgSymPerCalc",values))/100;
            // ApplyMastery=0: factory-time hard synergy, no item mastery or
            // launch potion scalar. Approved T29 additions occur at impact.
            return (int)Math.Clamp(value,0,int.MaxValue);
        }
        return(Damage(false),Damage(true));
    }
    public static void CastSecondary(PlayerMobile owner,Point3D origin,Point3D aim,SkillValues values,DateTime at)
    {
        HolyBoltDamage.Route(values);int dx=aim.X-origin.X,dy=aim.Y-origin.Y;if(dx==0&&dy==0){dx=dy=1;}
        var slow=AmazonSlowMissiles.Capture(owner,true);var mods=DruidFirestorm.Modifiers(owner) with{Potion=0};
        var damage=SecondaryDamage(owner,values);int speed=SlowMissilePayload.VelocityFp8(12,0,values.Rank,33,0,true,slow.VelocityPercent);
        var velocity=MissileVectors.VelocityFp8(dx,dy,speed);long id=checked(_next+1);
        var f=new HolyBoltFlight(id,owner.Serial.Value,owner.Map.MapID,values.Rank,origin.X,origin.Y,origin.Z,velocity.X,velocity.Y,
            0,slow.Damage256(damage.Min),slow.Damage256(damage.Max),0,at.AddMilliseconds(Frames*40),at.AddMilliseconds(40),CombatAbility.FistOfTheHeavens)
            {Captured=new(mods,slow),Motion=new(speed,SourceRandom.InitLow(HolyBoltFlights.SeedLow()),new([new(origin.X*2,origin.Y*2)]))};
        HolyBoltFlights.Validate(f);_flights.Add(id,f);_next=id;
    }
    internal static void CastSecondaryLegacy(PlayerMobile owner,Point3D origin,Point3D aim,SkillValues values,DateTime at)
    {
        int dx=aim.X-origin.X,dy=aim.Y-origin.Y;if(dx==0 && dy==0){dx=dy=1;}
        var velocity=MissileVectors.Velocity(dx,dy,NecromancerSummons.Source("fistoftheheavensbolt","Vel"));
        var raw=SecondaryDamage(owner,values);int bonus=CombatPotions.Bonus(owner,"magical-penetration");
        var damage=(Min:(int)Math.Clamp((long)raw.Min*(100+bonus)/100,0,int.MaxValue),Max:(int)Math.Clamp((long)raw.Max*(100+bonus)/100,0,int.MaxValue));long id=++_next;
        _flights.Add(id,new(id,owner.Serial.Value,owner.Map.MapID,values.Rank,origin.X,origin.Y,origin.Z,velocity.X,velocity.Y,
            0,damage.Min,damage.Max,0,at.AddMilliseconds(NecromancerSummons.Source("fistoftheheavensbolt","Range")*40),at,CombatAbility.FistOfTheHeavens));
    }
    private static int Sample(int min,int max)=>min+(max>min?Roll(max-min):0);
    public static void Cast(PlayerMobile owner,Point3D aim,SkillValues values)
    {
        HolyBoltDamage.Route(values);
        int dx=aim.X-owner.X,dy=aim.Y-owner.Y;if(dx==0 && dy==0){dx=dy=1;}
        var slow=AmazonSlowMissiles.Capture(owner,true);
        var modifiers=DruidFirestorm.Modifiers(owner) with{Potion=0};
        var ends=DruidFirestorm.Endpoints(values,NamedElements.MasteryPercent(owner,"mag"),slow);
        int speed=SlowMissilePayload.VelocityFp8(NecromancerSummons.Source("holybolt","Vel"),0,values.Rank,33,0,true,slow.VelocityPercent);
        var velocity=MissileVectors.VelocityFp8(dx,dy,speed);
        var now=TimedEffects.Now;long id=++_next;
        var flight=new HolyBoltFlight(id,owner.Serial.Value,owner.Map.MapID,values.Rank,owner.X,owner.Y,owner.Z,velocity.X,velocity.Y,
            0,ends.Min,ends.Max,0,now.AddMilliseconds(Frames*40),now.AddMilliseconds(40)){Captured=new(modifiers,slow),
            Motion=new(speed,SourceRandom.InitLow(HolyBoltFlights.SeedLow()),new([new(owner.X*2,owner.Y*2)]))};
        HolyBoltFlights.Validate(flight);_flights.Add(id,flight);
    }
    internal static void Impact(PlayerMobile owner,Mobile target,HolyBoltFlight flight)
    {
        if(ClassCombat.Friendly(owner,target))
        {
            var skill=SkillMechanics.For(flight.Ability);
            // Missile heal flag alone is insufficient: FoH's parent skill has neither TargetAlly nor TargetPet.
            if(target is PlayerMobile?skill.Number("TargetAlly")==0:skill.Number("TargetPet")==0){return;}
            var heal=Healing(owner,flight.Rank);SkillHealth.Heal(target,Sample(heal.Min,heal.Max));
            target.FixedEffect(0x376A,10,10,0x481,0);return; // Placeholder healing overlay; no damage or harmful event.
        }
        if(!FullDamage(target)){return;} // Pinned sHitPar2=3, dParam1=0: normal beasts take zero damage and do not stop the bolt.
        if(flight.Captured is{} captured)
        {
            if(!HolyBoltDamage.Supported(owner,target,out string reason))
            {owner.SendMessage(0x22,reason);return;}
            string capturedMissile=Missile(flight.Ability);SorceressEffects.MissileHit(owner,target,capturedMissile);
            if(ClassAvoidance.Missile(target,capturedMissile)){return;}
            HolyBoltDamage.Deliver(owner,(BaseCreature)target,Sample(flight.Min256,flight.Max256),captured,TimedEffects.Now,flight.Ability);return;
        }
        string missile=Missile(flight.Ability);SorceressEffects.MissileHit(owner,target,missile);
        int holy=NecromancerSummons.Source(missile,"Holy");
        SkillCombat.HolyImpact(owner,target,Sample(flight.Min256,flight.Max256),(holy&1)!=0 && GemCombat.Undead(target) || (holy&2)!=0 && NamedCombat.Demon(target));
    }
    private static PlayerMobile Owner(HolyBoltFlight flight)=>World.FindMobile((Serial)flight.Owner) is PlayerMobile {Alive:true,Deleted:false} owner
        && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Paladin
        && (owner.Map==Map.Internal || owner.Map.MapID==flight.Map && !SpellHelper.IsTown(owner.Location,owner))?owner:null;
    private static void Tick()
    {
        var now=TimedEffects.Now;_keys.Clear();_keys.AddRange(_flights.Keys);
        foreach(long id in _keys)
        {
            if(!_flights.TryGetValue(id,out var flight)){continue;}
            if(flight.Motion!=null){continue;}
            var owner=Owner(flight);
            if(owner==null || owner.Map==Map.Internal && flight.UntilUtc<=now){_flights.Remove(id);continue;}
            if(owner.Map==Map.Internal){continue;}
            // Bound catch-up by the source frame count, including due frames before absolute expiry.
            while(_flights.TryGetValue(id,out flight) && flight.NextUtc<=now && flight.NextUtc<flight.UntilUtc){Step(owner,flight);}
            if(_flights.TryGetValue(id,out flight) && flight.UntilUtc<=now){_flights.Remove(id);}
        }
    }
    private static void Step(PlayerMobile owner,HolyBoltFlight flight)
    {
        int frame=flight.Frame+1;
        string missile=Missile(flight.Ability);
        if(frame>=NecromancerSummons.Source(missile,"Range")){_flights.Remove(flight.Id);return;} // SrvDo1 decrements before collision.
        double x=flight.X+flight.Vx,y=flight.Y+flight.Vy;
        var from=new Point3D((int)Math.Round(flight.X),(int)Math.Round(flight.Y),flight.Z+12);
        var to=new Point3D((int)Math.Round(x),(int)Math.Round(y),flight.Z+12);
        if(x<0 || y<0 || x>=owner.Map.Width || y>=owner.Map.Height || !owner.Map.LineOfSight(from,to))
        {_flights.Remove(flight.Id);return;}
        Mobile first=null;double nearest=double.MaxValue;
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(new Point3D(to.X,to.Y,flight.Z),2))
        {
            if(frame>=NecromancerSummons.Source(missile,"Activate") && target.Serial.Value!=flight.LastCollision && Candidate(owner,target) && !SpellHelper.IsTown(target.Location,target)
                && CombatHitboxes.Intersect(flight.X,flight.Y,x,y,flight.Z,target.Location,out double entry)
                && (entry<nearest || entry==nearest && target.Serial.Value<(first?.Serial.Value ?? uint.MaxValue)))
            {first=target;nearest=entry;}
        }
        _flights[flight.Id]=flight with{X=x,Y=y,Frame=frame,NextUtc=flight.NextUtc.AddMilliseconds(40),LastCollision=first?.Serial.Value ?? flight.LastCollision};
        Effects.SendLocationEffect(new(to.X,to.Y,flight.Z),owner.Map,0x36E4,3,10,0x481,0); // Placeholder holy bolt; no impact consumes this piercing missile.
        if(first!=null){Impact(owner,first,flight);}
    }
    private static HolyBoltFlight Copy(HolyBoltFlight f)=>f.Motion is{} m?f with{Motion=m with{Steps=new(m.Steps.Cells.ToArray())}}:f;
    internal static HolyBoltFlight State(long id)=>_flights.TryGetValue(id,out var f)?Copy(f):null;
    internal static HolyBoltFlight Due(DateTime at)=>_flights.Values.Where(f=>f.Motion!=null&&f.NextUtc<=at).OrderBy(f=>f.NextUtc).ThenByDescending(f=>f.Id).FirstOrDefault();
    internal static void Remove(long id){_flights.Remove(id);HolyBoltFlights.Forget(id);}
    internal static void Update(HolyBoltFlight flight)
    {HolyBoltFlights.Validate(flight);if(_flights.ContainsKey(flight.Id)){_flights[flight.Id]=Copy(flight);}}
    public static HolyBoltFlight[] Snapshot()=>_flights.Values.Select(Copy).ToArray();
    public static void Restore(HolyBoltFlight[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing holy bolts");}
        var next=new Dictionary<long,HolyBoltFlight>();long nextId=0;
        foreach(var flight in saved)
        {
            if(flight==null||flight.Ability is not (CombatAbility.HolyBolt or CombatAbility.FistOfTheHeavens)
                || flight.Id<=0 || flight.Owner==0 || flight.Rank<1 || flight.Frame<0 || flight.Frame>=NecromancerSummons.Source(Missile(flight.Ability),"Range") || flight.Min256<0 || flight.Max256<flight.Min256
                || !double.IsFinite(flight.X+flight.Y+flight.Vx+flight.Vy) || flight.Motion==null&&flight.Vx==0&&flight.Vy==0
                || flight.Captured is{} captured&&(flight.Rank>255||captured.Modifiers==null||captured.Modifiers.Potion!=0
                    ||captured.Slow.VelocityPercent is <0 or >100||captured.Slow.DamagePercent is <0 or >100)
                || !next.TryAdd(flight.Id,CopyChecked(flight)))
            {throw new InvalidDataException("Invalid holy bolt");}
            nextId=Math.Max(nextId,flight.Id);
        }
        _flights.Clear();foreach(var pair in next){_flights.Add(pair.Key,pair.Value);}_next=nextId;
        HolyBoltFlights.Loaded(next.Values.Where(f=>f.Motion!=null).Select(f=>f.Id));
    }
    private static HolyBoltFlight CopyChecked(HolyBoltFlight f){if(f.Motion!=null){HolyBoltFlights.Validate(f);}return Copy(f);}
}
