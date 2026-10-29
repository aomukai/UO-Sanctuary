// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal sealed record GrimWardSkillRow(int MediumStart,int SmallStart,int LargeStart);
internal sealed record GrimWardSourceUnit(int Id,int Kind,int ClassId,int X,int Y,int Skill,int Rank);
internal sealed record GrimWardMissileRow(int Child,int RangeOverride,int Period,int DoFunction);
internal sealed record GrimWardSpawn(uint Flags,int Missile,int Owner,int Skill,int Rank,int X,int Y,int Range=0);
internal sealed record GrimWardLifecycleServices(Func<int,GrimWardSkillRow> SkillRow,
    Func<int,GrimWardSourceUnit> Target,Func<int,bool> CorpseEligible,Func<int,int,int,bool> Room,
    Func<int,int,int,int,uint,int,(bool Success,int X,int Y)> FreeCoordinates,
    Func<int,(bool Large,bool Small)?> Size,Func<int> MissileCount,Func<GrimWardSpawn,int> Create,
    Action<int> MarkNoDraw,Action<int> MarkNoSelect,Action<int> Refresh,
    Func<int,GrimWardMissileRow> MissileRow,Func<int,int> Owner,Func<int,int,int,int> OwnerDuration,
    Action<int,int,int,int> Dispatch,Func<int,int> Collision);
internal static class GrimWardLifecycle
{
    // Only the reviewed lifecycle. Generic curse dispatch keeps the body
    // missile as its context;modern owner/synergy/fear resolution is unresolved.
    // No learned handler,loot-container hiding or world scheduler is supplied.
    internal static bool Place(int caster,int skill,int rank,GrimWardLifecycleServices s)
    {
        ArgumentNullException.ThrowIfNull(s);var row=s.SkillRow(skill);if(row==null){return false;}
        var corpse=s.Target(caster);if(corpse==null||!s.CorpseEligible(corpse.Id)){return false;}
        if(!s.Room(corpse.Id,corpse.X,corpse.Y)){return false;}
        var point=s.FreeCoordinates(corpse.Id,corpse.X,corpse.Y,2,0x1000,1);
        if(!point.Success||corpse.Kind!=1){return false;}
        var size=s.Size(corpse.ClassId);if(!size.HasValue){return false;}
        int missile=size.Value.Large?row.LargeStart:size.Value.Small?row.SmallStart:row.MediumStart;
        if(missile<0||missile>=s.MissileCount()){return false;}
        // Source intentionally does not inspect the allocation result before
        // marking the corpse unavailable. Visual no-draw differs from retained
        // loot access in Sanctuary and must be bound separately.
        s.Create(new(1,missile,caster,skill,rank,point.X,point.Y));
        s.MarkNoDraw(corpse.Id);s.MarkNoSelect(corpse.Id);s.Refresh(corpse.Id);return true;
    }
    internal static int StartHit(GrimWardSourceUnit start,GrimWardLifecycleServices s)
    {
        ArgumentNullException.ThrowIfNull(s);if(start==null){return 1;}
        var row=s.MissileRow(start.ClassId);if(row==null||row.Child<0){return 1;}
        int owner=s.Owner(start.Id);if(owner==0||s.SkillRow(start.Skill)==null){return 1;}
        int range=row.RangeOverride>0?row.RangeOverride:Math.Max(s.OwnerDuration(owner,start.Skill,start.Rank),5);
        s.Create(new(32769,row.Child,owner,start.Skill,start.Rank,start.X,start.Y,range));return 1;
    }
    internal static int Elapsed(int total,int current)=>unchecked(total-current);
    internal static int Tick(GrimWardSourceUnit body,int elapsed,GrimWardLifecycleServices s)
    {
        ArgumentNullException.ThrowIfNull(s);if(body==null){return 2;}
        var row=s.MissileRow(body.ClassId);if(row==null){return 2;}
        if(elapsed%Math.Max(row.Period,1)!=0){return s.Collision(body.Id);}
        if(body.Skill<=0||body.Rank<=0){return 2;}
        s.Dispatch(body.Id,row.DoFunction,body.Skill,body.Rank);return s.Collision(body.Id);
    }
}
