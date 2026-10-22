// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record HeavenStrike(long Id,uint Owner,uint Target,int Map,int Rank,int X,int Y,int Z,int Min256,int Max256,DateTime ImpactUtc,uint Corpse=0);
public sealed class HeavenFistProfiles:GenericPersistence
{
    public HeavenFistProfiles():base("SanctuaryHeavenFists",124){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(HeavenFists.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Fist of the Heavens version");}
        HeavenFists.Restore(JsonSerializer.Deserialize<HeavenStrike[]>(reader.ReadString()));
    }
}
public static class HeavenFists
{
    private static readonly Dictionary<long,HeavenStrike> _strikes=new();
    private static readonly List<long> _keys=new();
    private static long _next;
    internal static Func<int,int> Roll=static range=>Utility.Random(range);
    public static HeavenFistProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),Tick);
    public static Mobile Acquire(PlayerMobile owner,Point3D cursor)
    {
        if(!PaladinCombat.ValidCastPoint(owner,cursor)){return null;}
        int dx=cursor.X-owner.X,dy=cursor.Y-owner.Y;
        // A click turns the caster immediately; no separate turn-animation delay.
        // At the caster's own tile, a click supplies no facing evidence yet.
        if(dx==0 && dy==0){return null;}
        Mobile closest=null;long minimum=long.MaxValue;bool tied=false;
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,CombatProtocol.TeethRange))
        {
            int tx=target.X-owner.X,ty=target.Y-owner.Y;
            // Exact perpendicular boundary behavior is unverified and remains gated.
            if((long)dx*tx+(long)dy*ty<=0 || Math.Abs(target.Z-owner.Z)>=16
                || !PaladinCombat.ValidTarget(owner,CombatAbility.FistOfTheHeavens,target.Serial,target.Location)){continue;}
            long x=target.X-cursor.X,y=target.Y-cursor.Y,distance=x*x+y*y;
            if(distance<minimum){minimum=distance;closest=target;tied=false;}
            else if(distance==minimum){tied=true;}
        }
        // Exact tie resolution is unverified; do not invent native query/serial priority.
        return tied?null:closest;
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(selected==Serial.Zero)
        {
            if(Acquire(owner,point) is not{} target){return false;}
            selected=target.Serial;
        }
        if(!SkillCombat.Begin(owner,CombatAbility.FistOfTheHeavens,selected,point,now,PaladinCombat.ValidTarget)){return false;}
        var session=ClassCombat.For(owner);
        session.PendingLeft=session.HeldAbility==CombatAbility.FistOfTheHeavens && session.HeldAttack;
        return true;
    }
    public static void Cast(PlayerMobile owner,Mobile target,SkillValues values)
    {
        int Scale(int value)=>(int)Math.Clamp(NamedElements.Enhance(owner,value*(100+CombatPotions.Bonus(owner,"magical-penetration"))/100.0,"ltng"),0,int.MaxValue);
        long id=++_next;
        _strikes.Add(id,new(id,owner.Serial.Value,target.Serial.Value,owner.Map.MapID,values.Rank,target.X,target.Y,target.Z,
            Scale(values.Damage256(false)),Scale(values.Damage256(true)),TimedEffects.Now.AddMilliseconds(NecromancerSummons.Source("fistoftheheavensdelay","Range")*40)));
        target.FixedEffect(0x3709,10,10,0x481,0); // Placeholder sky-strike warning overlay.
    }
    private static void Tick()
    {
        var now=TimedEffects.Now;_keys.Clear();_keys.AddRange(_strikes.Keys);
        foreach(long id in _keys)
        {
            if(!_strikes.TryGetValue(id,out var strike)){continue;}
            var owner=World.FindMobile((Serial)strike.Owner) as PlayerMobile;
            if(owner is not{Alive:true,Deleted:false} || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Paladin
                || owner.Map!=Map.Internal && (owner.Map.MapID!=strike.Map || SpellHelper.IsTown(owner.Location,owner)))
            {_strikes.Remove(id);continue;}
            if(strike.ImpactUtc>now){continue;}
            _strikes.Remove(id);if(owner.Map==Map.Internal){continue;}
            Impact(owner,strike);
        }
    }
    internal static void Impact(PlayerMobile owner,HeavenStrike strike)
    {
        var primary=World.FindMobile((Serial)strike.Target);
        // D2 keeps a dead unit identity; native creatures delete on death. A captured corpse bridges that interval.
        if(primary==null && (strike.Corpse==0 || World.FindItem((Serial)strike.Corpse) is not Corpse{Deleted:false})){return;}
        if(primary!=null && ClassCombat.IsHostile(owner,primary) && !SpellHelper.IsTown(primary.Location,primary))
        {
            int damage=strike.Min256+(strike.Max256>strike.Min256?Roll(strike.Max256-strike.Min256):0);
            SkillCombat.LightningImpact(owner,primary,damage);
        }
        // A surviving converted/friendly primary or a dead target still permits the original-position burst.
        var values=SkillMechanics.Values(owner,CombatAbility.FistOfTheHeavens,strike.Rank);
        int count=Math.Max(1,(int)values.Eval("calc4"));double radius=SkillGeometry.Tiles(values.Eval("aurarangecalc"));
        var origin=new Point3D(strike.X,strike.Y,strike.Z);
        using var targets=PooledRefList<Mobile>.Create();
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(origin,(int)Math.Ceiling(radius)))
        {
            double dx=target.X-origin.X,dy=target.Y-origin.Y;
            if(ClassCombat.IsHostile(owner,target) && Math.Abs(target.Z-origin.Z)<16 && dx*dx+dy*dy<=radius*radius
                && !SpellHelper.IsTown(target.Location,target)){targets.Add(target);if(targets.Count>=count){break;}}
        }
        foreach(var target in targets){HolyBolts.CastSecondary(owner,origin,target.Location,values,strike.ImpactUtc);}
        Effects.SendLocationEffect(origin,owner.Map,0x3709,10,10,0x481,0); // Placeholder strike/burst. Native query order is provisional.
    }
    public static void CaptureDeath(BaseCreature target,Corpse corpse)
    {
        using var rows=PooledRefList<HeavenStrike>.Create();
        foreach(var strike in _strikes.Values){if(strike.Target==target.Serial.Value){rows.Add(strike);}}
        foreach(var strike in rows){_strikes[strike.Id]=strike with{Corpse=corpse.Serial.Value};}
    }
    public static HeavenStrike[] Snapshot()=>new List<HeavenStrike>(_strikes.Values).ToArray();
    public static void Restore(HeavenStrike[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Fist of the Heavens strikes");}
        _strikes.Clear();_next=0;
        foreach(var strike in saved)
        {
            if(strike.Id<=0 || strike.Owner==0 || strike.Target==0 || strike.Rank<1 || strike.Min256<0 || strike.Max256<strike.Min256 || !_strikes.TryAdd(strike.Id,strike))
            {throw new InvalidDataException("Invalid Fist of the Heavens strike");}
            _next=Math.Max(_next,strike.Id);
        }
    }
}
