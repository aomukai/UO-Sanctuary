// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

internal sealed record DeathMarkCastContext(uint Caster,GenericPetDecisionUnit Position,bool HasSkillContext,int Skill,int Rank,int Selector,int Context12c,sbyte PetType,int PetTypeCount,short State,int StateCount);
internal sealed record DeathMarkTarget(uint Id,int Kind,bool IsCaster=false);
internal sealed class DeathMarkDirective
{
    internal int TargetType,TargetId,X,Y,Remaining;
    internal bool ClearControl;
}
internal sealed record DeathMarkStateRequest(uint Caster,DeathMarkTarget Target,int Skill,int Rank,int Duration,int State,int Stat=-1,int Value=0);
internal sealed record DeathMarkCastServices(Func<bool> SkillRecord,Func<bool> InitialRoom,Action MarkServerDo,Func<int,int> Formula,
    Func<Point2D?> Coordinates,Func<int?> CurrentLevel,Func<byte?> InitialLevelByte14,Func<Point2D,int,int> Placement,
    Action RefreshOwner,Func<DeathMarkTarget> Target,Func<DeathMarkTarget,bool> ValidTarget,Func<DeathMarkTarget,int?> TargetLevel,
    Action<int?,DeathMarkDirective> VisitPets,Func<DeathMarkStateRequest,bool> AllocateState,Action BindState,Action ApplyAura,Action<int,int> WriteStat);

// Complete captured current do155 composition. Pet traversal/callback44aa70,
// target validity,room metadata,formula and state allocation remain explicit
// services. This component alone admits neither learned casts nor pet warps.
internal static class DeathMarkCast
{
    private static bool Town(int? level)=>level is 1 or 40 or 75 or 103 or 109;
    internal static int Run(DeathMarkCastContext c,DeathMarkCastServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        bool row=s.SkillRecord();
        if(c.Caster==0){throw new NotSupportedException("Current do155 asserts a missing caster");}
        if(!row||!c.HasSkillContext||!s.InitialRoom()){return 0;}
        s.MarkServerDo();int maximum=s.Formula(0x194);if(maximum<=0){return 0;}
        var destination=s.Coordinates();if(destination==null||Town(s.CurrentLevel())){return 0;}
        int range1=s.Formula(0x84),range2=s.Formula(0x84);var(x,y)=c.Position.Coordinates();
        int distance=unchecked((destination.Value.X-x)*(destination.Value.X-x)+(destination.Value.Y-y)*(destination.Value.Y-y));
        if(distance>unchecked(range1*range2)){return 0;}
        var mode=s.InitialLevelByte14();if(mode is null or 0){return 0;}
        if(mode==2&&s.Placement(destination.Value,0x1804)!=0){return 0;}
        s.RefreshOwner();if(c.Position.Kind==5){throw new NotSupportedException("Current do155 asserts a tile caster");}
        var target=c.Position.HasPath?s.Target():null;
        if(target!=null&&(target.IsCaster||target.Kind is not(0 or 1)||!s.ValidTarget(target)||Town(s.TargetLevel(target)))){target=null;}
        var directive=new DeathMarkDirective{TargetType=target==null?0:target.Kind==0?1:2,TargetId=target==null?-1:unchecked((int)target.Id),
            X=destination.Value.X,Y=destination.Value.Y,Remaining=maximum,ClearControl=s.Formula(0x190)!=0};
        if(c.PetType>0)
        {
            if(c.Selector is <1 or >3){throw new NotSupportedException("Current do155 pet lookup asserts the source selector");}
        }
        int? type=c.PetType>0&&c.PetType<c.PetTypeCount?c.PetType:null;
        s.VisitPets(type,directive);
        if(target==null||c.State<0){return 1;}
        if(c.Selector is <1 or >3){throw new NotSupportedException("Current do155 state lookup asserts the source selector");}
        if(c.State>=c.StateCount||directive.Remaining>=maximum){return 1;}
        int duration=s.Formula(0x80);
        if(!s.AllocateState(new(c.Caster,target,c.Skill,c.Rank,duration,c.State))){return 0;}
        s.BindState();s.ApplyAura();s.WriteStat(350,c.Skill);s.WriteStat(351,c.Rank);s.WriteStat(202,c.Context12c);
        return 1;
    }
}
