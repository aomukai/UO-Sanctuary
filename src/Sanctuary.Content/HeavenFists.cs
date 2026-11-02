// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Logging;
using System.Text.Json;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record HeavenStrike(long Id,uint Owner,uint Target,int Map,int Rank,int X,int Y,int Z,int Min256,int Max256,DateTime ImpactUtc,uint Corpse=0)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public HeavenCapture Captured{get;init;}
}
public sealed class HeavenFistProfiles:GenericPersistence
{
    private HeavenStrike[] _snapshot;
    public override void Serialize(){_snapshot=HeavenFists.Snapshot();base.Serialize();}
    public HeavenFistProfiles():base("SanctuaryHeavenFists",124){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Fist of the Heavens version");}
        HeavenFists.Restore(JsonSerializer.Deserialize<HeavenStrike[]>(reader.ReadString()));
    }
}
public static class HeavenFists
{
    private static readonly Dictionary<long,HeavenStrike> _strikes=new();
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HeavenFists));
    private static readonly List<long> _keys=new();
    private static long _next;
    internal static Func<int,int> Roll=static range=>Utility.Random(range);
    public static HeavenFistProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldLoad+=RestoreReferences;}
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
        if(!HeavenFistDamage.Supported(owner,World.FindMobile(selected),out string reason)){owner.SendMessage(0x22,reason);return false;}
        if(!SkillCombat.Begin(owner,CombatAbility.FistOfTheHeavens,selected,point,now,PaladinCombat.ValidTarget)){return false;}
        var session=ClassCombat.For(owner);
        session.PendingLeft=session.HeldAbility==CombatAbility.FistOfTheHeavens && session.HeldAttack;
        return true;
    }
    public static void Cast(PlayerMobile owner,Mobile target,SkillValues values)
        =>Commit(Prepare(owner,target,values,HeavenFistDamage.Prepare(owner,target,values)));
    internal static HeavenStrike Prepare(PlayerMobile owner,Mobile target,SkillValues values,PreparedHeavenDamage damage)
    {
        var f=new HeavenStrike(checked(_next+1),owner.Serial.Value,target.Serial.Value,owner.Map.MapID,values.Rank,target.X,target.Y,target.Z,
            damage.Minimum256,damage.Maximum256,TimedEffects.Now.AddMilliseconds(400)){Captured=damage.Capture};Validate(f);return f;
    }
    internal static void Commit(HeavenStrike f)
    {if(f.Id!=checked(_next+1)){throw new InvalidOperationException("Prepared FoH identity changed");}_strikes.Add(f.Id,f);_next=f.Id;
        World.FindMobile((Serial)f.Target)?.FixedEffect(0x3709,10,10,0x481,0);} // Identified sky warning placeholder.
    internal static void Cancel(Mobile owner)
    {if(owner!=null){foreach(var f in Snapshot().Where(f=>f.Owner==owner.Serial.Value&&f.Captured!=null)){_strikes.Remove(f.Id);}}}
    internal static void RestoreReferences()
    {
        var now=TimedEffects.Now;
        foreach(var f in Snapshot().Where(f=>f.Captured!=null))
        {
            if(f.ImpactUtc<=now){_strikes.Remove(f.Id);continue;} // Loaded overdue damage ages harmlessly.
            if(!f.Captured.TargetDead&&f.Corpse==0&&World.FindMobile((Serial)f.Target)==null)
            {_strikes[f.Id]=f with{Captured=f.Captured with{TargetLost=true}};Log.Warning("FoH lost saved primary identity:{Strike}/{Target}",f.Id,f.Target);}
        }
    }
    internal static void Tick()
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
            _strikes.Remove(id);if(owner.Map==Map.Internal||strike.Captured!=null&&owner.NetState is not{Running:true}){continue;}
            try{Impact(owner,strike);}catch(NotSupportedException e){Log.Warning("FoH strike unsupported:{Strike};{Reason}",strike.Id,e.Message);}
        }
    }
    internal static void Impact(PlayerMobile owner,HeavenStrike strike)
    {
        if(strike.Captured is{TargetLost:true}){throw new NotSupportedException("FoH primary identity was absent at world load");}
        var primary=strike.Captured is{TargetDead:true}?null:World.FindMobile((Serial)strike.Target);
        // D2 keeps a dead unit identity; native creatures delete on death. A captured corpse bridges that interval.
        if(primary==null && (strike.Corpse==0 || World.FindItem((Serial)strike.Corpse) is not Corpse{Deleted:false}))
        {if(strike.Captured!=null){throw new NotSupportedException("FoH missing/no-corpse dead-unit bridge needs its actor adapter");}return;}
        int prepared=0;
        if(primary!=null&&strike.Captured is{} captured)
        {
            if(ClassCombat.IsHostile(owner,primary)&&!HeavenFistDamage.Supported(owner,primary,out string reason)){throw new NotSupportedException(reason);}
            ulong seed=captured.Seed;prepared=HeavenFistDamage.Roll(strike.Min256,strike.Max256,ref seed); // Fill precedes block/dodge and alignment refusal.
        }
        if(primary!=null && ClassCombat.IsHostile(owner,primary) && !SpellHelper.IsTown(primary.Location,primary))
        {
            if(strike.Captured is{} c)
            {
                if(!HeavenFistDamage.Supported(owner,primary,out string reason)){throw new NotSupportedException(reason);}
                SorceressEffects.MissileHit(owner,primary,"fistoftheheavensdelay");
                if(!ClassAvoidance.Missile(primary,"fistoftheheavensdelay")){HeavenFistDamage.Deliver(owner,(BaseCreature)primary,prepared,c,strike.ImpactUtc);}
            }
            else{int damage=strike.Min256+(strike.Max256>strike.Min256?Roll(strike.Max256-strike.Min256):0);SkillCombat.LightningImpact(owner,primary,damage);}
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
        foreach(var target in targets)
        {if(strike.Captured==null){HolyBolts.CastSecondaryLegacy(owner,origin,target.Location,values,strike.ImpactUtc);}
            else{HolyBolts.CastSecondary(owner,origin,target.Location,values,strike.ImpactUtc);}}
        Effects.SendLocationEffect(origin,owner.Map,0x3709,10,10,0x481,0); // Placeholder strike/burst. Native query order is provisional.
    }
    public static void CaptureDeath(BaseCreature target,Corpse corpse)
    {
        using var rows=PooledRefList<HeavenStrike>.Create();
        foreach(var strike in _strikes.Values){if(strike.Target==target.Serial.Value){rows.Add(strike);}}
        foreach(var strike in rows){_strikes[strike.Id]=strike with{Corpse=corpse.Serial.Value,Captured=strike.Captured is{} c?c with{TargetDead=true}:null};}
    }
    public static HeavenStrike[] Snapshot()=>new List<HeavenStrike>(_strikes.Values).ToArray();
    private static void Validate(HeavenStrike f)
    {
        if(f==null||f.Id<=0||f.Owner==0||f.Target==0||f.Rank<1||f.Min256<0||f.Max256<f.Min256
            ||f.Captured is{} c&&(f.Rank>255||!((Serial)f.Owner).IsMobile||!((Serial)f.Target).IsMobile||f.Corpse!=0&&!((Serial)f.Corpse).IsItem
                ||f.Map<0||f.Map>=Map.Maps.Length||Map.Maps[f.Map]==null||Map.Maps[f.Map]==Map.Internal||f.X<=0||f.Y<=0||f.X>=Map.Maps[f.Map].Width||f.Y>=Map.Maps[f.Map].Height
                ||f.Z is <-128 or >127||f.ImpactUtc.Kind!=DateTimeKind.Utc||c.Modifiers==null||c.Modifiers.Potion!=0||c.Slow.VelocityPercent!=100||c.Slow.DamagePercent is <0 or >100))
        {throw new InvalidDataException("Invalid Fist of the Heavens strike");}
    }
    public static void Restore(HeavenStrike[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Fist of the Heavens strikes");}
        var next=new Dictionary<long,HeavenStrike>();long nextId=0;
        foreach(var f in saved){Validate(f);if(!next.TryAdd(f.Id,f)){throw new InvalidDataException("Duplicate Fist of the Heavens strike");}nextId=Math.Max(nextId,f.Id);}
        _strikes.Clear();foreach(var pair in next){_strikes.Add(pair.Key,pair.Value);}_next=nextId;
    }
}
