// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
namespace Sanctuary;

internal sealed record WarlockGroundRoute(CombatAbility Ability,int SkillId,int StartFunction,int DoFunction,int Missile,int Size,
    int MissileDo,int MissileHit,int HitDispatch,int Lifetime,string RadiusFormula);
internal static class WarlockGroundRoutes
{
    private static readonly Lazy<IReadOnlyDictionary<CombatAbility,WarlockGroundRoute>> Routes=new(()=>
    {
        var rows=BalanceCatalog.Read<WarlockGroundRoute>("warlock-ground-launch");
        if(rows.Length!=2||rows.Select(r=>r.Ability).Distinct().Count()!=2||rows.Any(r=>r.Ability is not(CombatAbility.Abyss or CombatAbility.Apocalypse)))
        {throw new InvalidDataException("Invalid prepared Warlock ground routes");}
        foreach(var r in rows)
        {
            var s=SkillMechanics.For(r.Ability);
            if(s.SourceId!=r.SkillId||s.Number("srvstfunc")!=r.StartFunction||s.Number("srvdofunc")!=r.DoFunction||r.Lifetime!=50
                ||r.Size!=(r.Ability==CombatAbility.Abyss?3:1)||r.Missile!=(r.Ability==CombatAbility.Abyss?709:722)
                ||r.MissileDo!=(r.Ability==CombatAbility.Abyss?39:1)||r.MissileHit!=(r.Ability==CombatAbility.Abyss?11:23)
                ||r.HitDispatch!=(r.Ability==CombatAbility.Abyss?0:163))
            {throw new NotSupportedException("Unverified Warlock ground route");}
        }
        return rows.ToDictionary(r=>r.Ability);
    });
    internal static WarlockGroundRoute For(CombatAbility ability)=>Routes.Value.TryGetValue(ability,out var r)?r:throw new NotSupportedException("Unmapped prepared ground route");
}
internal sealed record GroundMissileRequest(int Skill,int Rank,int Missile,Point2D Point)
{internal int Flags=>1;}
// Nonnull caster context. Coordinates resolve the current selected unit or
// first path point on each call. Room/placement/factory/network are mandatory;
// no new missile-do39 or skill-do163 damage behavior is supplied here.
internal abstract class GroundLaunchServices
{
    internal abstract bool HasSkill(int skill);
    internal abstract int SkillMissile();
    internal abstract int MissileCount{get;}
    internal abstract int ProgressiveMissile(int skill);
    internal abstract bool HasRoom();
    internal abstract Point2D Coordinates();
    internal abstract bool DestinationRoom(Point2D point);
    internal abstract bool Town();
    internal abstract int MissileSize(int missile);
    internal abstract bool Blocked(Point2D point,int size,int mask);
    internal abstract int Distance(Point2D point);
    internal abstract void SetDoFlag();
    internal abstract uint Random();
    internal abstract bool Create(GroundMissileRequest request);
    internal abstract void SetTargetMemory(uint seed);
    internal abstract void Notify(GroundMissileRequest request,uint seed);
}
internal abstract class GroundHitServices
{
    internal abstract bool Present();
    internal abstract bool HasRow();
    internal abstract int Skill();
    internal abstract int Rank();
    internal abstract bool HasOwner();
    internal abstract bool HasVictim();
    internal abstract int HitParameter();
    internal abstract void SetTarget();
    internal abstract void Dispatch(int function,int skill,int rank);
}
internal static class GroundMissileLaunch
{
    private static bool Nonzero(Point2D p)=>p.X!=0&&p.Y!=0;
    internal static bool Start14(GroundLaunchServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if(!services.HasRoom()){return false;}
        var point=services.Coordinates();
        return Nonzero(point)&&services.DestinationRoom(point)&&!services.Town();
    }
    private static bool Placement(GroundLaunchServices services,int missile)
    {
        int size=services.MissileSize(missile);
        if(size is <0 or >255){throw new NotSupportedException("Unmapped source placement size");}
        if(!services.HasRoom()){return false;}
        var point=services.Coordinates();
        return Nonzero(point)&&!services.Blocked(point,size,5);
    }
    internal static bool Do28(int skill,int rank,GroundLaunchServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if(!services.HasSkill(skill)){return false;}
        int missile=services.SkillMissile();
        if(missile<0||missile>=services.MissileCount){return false;}
        services.SetDoFlag();
        if(!Placement(services,missile)){return false;}
        var point=services.Coordinates();
        // The generic factory helper refuses only the both-zero coordinate
        // pair here;the preceding placement helper requires both nonzero.
        if((point.X==0&&point.Y==0)||services.Distance(point)>100){return false;}
        return services.Create(new(skill,rank,missile,point));
    }
    internal static bool Do123(int skill,int rank,GroundLaunchServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if(!services.HasSkill(skill)){return false;}
        int missile=services.ProgressiveMissile(skill);
        if(missile<=0){return false;}
        var point=services.Coordinates();
        if(!Nonzero(point)||!Placement(services,missile)){return false;}
        services.SetDoFlag();uint sample=services.Random();
        var request=new GroundMissileRequest(skill,rank,missile,point);
        if(!services.Create(request)){return false;}
        services.SetTargetMemory(sample);services.Notify(request,sample);return true;
    }
    internal static int Hit23(GroundHitServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if(!services.Present()||!services.HasRow()){return 1;}
        int skill=services.Skill(),rank=services.Rank();
        if(skill>0&&rank>0&&services.HasOwner())
        {
            if(services.HasVictim()){services.SetTarget();}
            services.Dispatch(services.HitParameter(),skill,rank);
        }
        return 1;
    }
}
