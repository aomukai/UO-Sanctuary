// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record BladeWarpFlight(long Id,uint Owner,int Map,int Rank,double X,double Y,int Z,
    Point3D Aim,double Vx,double Vy,int Remaining,DateTime NextUtc,int Minimum256,int Maximum256,FirestormModifiers Modifiers);
// Explicit user-authorized Sanctuary estimate for protected do166/hit42.
// Numeric inputs retain pinned 3.3.93847 rows;world/payment edges are documented
// in blade-warp-adaptation.json. Released travel is transient,never replayed on load.
internal static class BladeWarp
{
    private static readonly Dictionary<long,BladeWarpFlight> Flights=new();
    private static long _next;
    internal static Func<bool> Allocate=()=>true;
    internal static Func<int,int> Roll=Utility.Random;
    private const CombatAbility Ability=CombatAbility.BladeWarp;
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static BladeWarpFlight[] Snapshot()=>Flights.Values.OrderBy(x=>x.Id).ToArray();
    internal static void Clear(PlayerMobile owner)
    {foreach(long id in Flights.Values.Where(x=>x.Owner==owner.Serial.Value).Select(x=>x.Id).ToArray()){Flights.Remove(id);}}
    internal static bool Weapon(PlayerMobile p)=>PlayerMeleeGeometry.WarlockMelee(p)&&CombatTiming.Equipped(p) is BaseWeapon{HitPoints:>0};
    private static bool Owner(PlayerMobile p)=>p is{Alive:true,Deleted:false}&&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock;
    internal static bool Aim(PlayerMobile p,Serial selected,ref Point3D point)
    {
        if(!Owner(p)||!Weapon(p)||!LobProjectiles.Aim(p,selected,ref point)||point==p.Location||MinionSafeAreas.Contains(p)){return false;}
        return Teleportation.TravelAllowed(p,p.Map,point);
    }
    internal static bool Begin(PlayerMobile p,Serial selected,Point3D point,long now)
        =>Aim(p,selected,ref point)&&SkillCombat.Begin(p,Ability,selected,point,now,(owner,_,id,at)=>Aim(owner,id,ref at));
    internal static bool Release(PlayerMobile p,Serial selected,Point3D point)
    {
        if(!Aim(p,selected,ref point)||!FireGround.Ready(p,Ability)){return false;}
        var values=SkillMechanics.Values(p,Ability);var row=SkillMechanics.For(Ability);
        if(row.SourceId!=390||row.Number("srvdofunc")!=166||row.Text("srvmissilea")!="bladewarp"
            ||row.Text("anim")!="SC"||row.Number("requiresweapon")!=1||row.Parameter(1)!=4||values.Mana256!=3840
            ||row.Number("localdelay")!=20||row.Text("EType")!="mag"||row.Number("SrcDam")!=0)
        {throw new NotSupportedException("Unsupported Blade Warp table route");}
        if(!NecromancerEffects.CanPay(p,values.Mana256)||!Allocate()){return false;}
        int min=NamedElements.To256(NamedElements.Enhance(p,values.Damage256(false)/256.0,"mag"));
        int max=NamedElements.To256(NamedElements.Enhance(p,values.Damage256(true)/256.0,"mag"));
        var velocity=MissileVectors.Velocity((point.X-p.X)*2,(point.Y-p.Y)*2,36);
        var flight=new BladeWarpFlight(checked(_next+1),p.Serial.Value,p.Map.MapID,values.Rank,p.X,p.Y,p.Z,point,velocity.X,velocity.Y,20,
            TimedEffects.Now.AddMilliseconds(40),min,max,DruidFirestorm.Modifiers(p));
        if(!NecromancerEffects.Pay(p,values.Mana256)){return false;}
        _next=flight.Id;Flights.Add(flight.Id,flight);FireGround.Delay(p,Ability);p.PlaySound(0x23B); // Astral throw placeholder.
        return true;
    }
    private static Point3D Point(double x,double y,int z)=>new((int)Math.Round(x),(int)Math.Round(y),z);
    private static bool Enemy(PlayerMobile owner,Mobile target,int z)=>target is{Alive:true,Deleted:false}
        &&target.Map==owner.Map&&Math.Abs(target.Z-z)<16&&ClassCombat.IsHostile(owner,target)&&!SpellHelper.IsTown(target.Location,owner);
    internal static bool Landing(PlayerMobile p,Point3D center,out Point3D destination)
    {
        destination=default;var map=p.Map;if(!Owner(p)||map==null||map==Map.Internal||MinionSafeAreas.Contains(p)){return false;}
        // Exact impact tile first,then eight neighbors ordered by distance/X/Y.
        var offsets=(from x in Enumerable.Range(-1,3) from y in Enumerable.Range(-1,3) orderby x*x+y*y,x,y select new Point2D(x,y));
        foreach(var offset in offsets)
        {
            int x=center.X+offset.X,y=center.Y+offset.Y;
            if(x<0||y<0||x>=map.Width||y>=map.Height||!LeapTerrain.Ground(p,map,x,y,center.Z,out var at)||at==p.Location
                ||!Teleportation.Space(map,at,p)||!TreasureHunts.CanLand(p,map,at)||!Teleportation.TravelAllowed(p,map,at)
                ||SpellHelper.IsTown(at,p)||!map.LineOfSight(new Point3D(center.X,center.Y,center.Z+12),new Point3D(at.X,at.Y,at.Z+12))){continue;}
            destination=at;return true;
        }
        return false;
    }
    private static void Impact(PlayerMobile p,BladeWarpFlight flight,Point3D center)
    {
        // Claim removal before teleport/death/arrival callbacks;no replay/double explosion.
        if(!Flights.Remove(flight.Id)){return;}
        var map=p.Map;var raw=DruidMoltenBoulder.Potion(flight.Modifiers,new(0,0,0,0,0,
            Magic:WeaponMissileDamage.RollValue(flight.Minimum256,flight.Maximum256,0,Roll)/256.0));
        // One copied-area event. No weapon damage,accuracy,critical,leech or on-strike effects.
        foreach(var target in SkillCombat.Targets(p,center,2))
        {
            if(!Enemy(p,target,center.Z)||ClassAvoidance.Missile(target)){continue;}
            p.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(p,target,raw);
            int loss=SkillHealth.Damage(p,target,NamedElements.To256(parts.Total));
            if(target is BaseCreature npc){CreatureHitRecovery.Apply(npc,p,loss);}
        }
        Effects.SendLocationEffect(center,map,0x36BD,12,10,0x48D,0); // Blade Warp magic impact placeholder.
        if(Owner(p)&&p.Map==map&&Landing(p,center,out var destination)){Teleportation.CommitTransfer(p,destination,p.Location,map);}
    }
    private static void Advance(PlayerMobile p,BladeWarpFlight flight)
    {
        double dx=flight.Aim.X-flight.X,dy=flight.Aim.Y-flight.Y;
        bool arrived=dx*dx+dy*dy<=flight.Vx*flight.Vx+flight.Vy*flight.Vy;
        double vx=arrived?dx:flight.Vx,vy=arrived?dy:flight.Vy;
        var trace=SourceMissileSteps.Advance(flight.X,flight.Y,vx,vy,null);
        var last=Point(flight.X,flight.Y,flight.Z);
        foreach(var cell in trace?.Cells??[])
        {
            var point=Point(cell.X/2.0,cell.Y/2.0,flight.Z);
            if(point.X<0||point.Y<0||point.X>=p.Map.Width||point.Y>=p.Map.Height||LobProjectiles.Barrier(p.Map,point)
                ||!p.Map.LineOfSight(new Point3D(last.X,last.Y,last.Z+12),new Point3D(point.X,point.Y,point.Z+12)))
            {Impact(p,flight,last);return;}
            last=point;
            var targets=SkillCombat.NearbySnapshot<Mobile>(p.Map,point,2);targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));
            foreach(var target in targets)
            {
                if(!Enemy(p,target,flight.Z)){continue;}
                int size=target is BaseCreature b?CreatureProfiles.For(b).Traits.Size??2:2;
                if(CombatHitboxes.SourceTouches(1,size,cell.X-target.X*2,cell.Y-target.Y*2)){Impact(p,flight,point);return;}
            }
        }
        var next=flight with{X=flight.X+vx,Y=flight.Y+vy,Remaining=flight.Remaining-1,NextUtc=flight.NextUtc.AddMilliseconds(40)};
        if(arrived||next.Remaining<=0){Impact(p,flight,Point(next.X,next.Y,next.Z));return;}
        Flights[flight.Id]=next;Effects.SendLocationEffect(Point(next.X,next.Y,next.Z),p.Map,0x36E4,2,10,0x48D,0); // Astral blade placeholder.
    }
    internal static void Tick(DateTime now,bool? actions=null)
    {
        if(now.Kind!=DateTimeKind.Utc){throw new ArgumentException("Blade Warp requires UTC");}
        foreach(var initial in Flights.Values.OrderBy(x=>x.Id).ToArray())
        {
            while(Flights.TryGetValue(initial.Id,out var flight)&&flight.NextUtc<=now)
            {
                var p=World.FindMobile((Serial)flight.Owner) as PlayerMobile;
                if(!Owner(p)||p.Map==null||p.Map==Map.Internal||p.Map.MapID!=flight.Map||MinionSafeAreas.Contains(p)
                    ||!(actions??p.NetState is{Running:true}))
                {Flights.Remove(flight.Id);break;}
                Advance(p,flight);
            }
        }
    }
}
