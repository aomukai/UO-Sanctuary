// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal enum CreeperAiKind{Idle,PetMove,CommonMove,Escape,Walk,Skill}
internal readonly record struct CreeperAiRequest(CreeperAiKind Kind,int A=0,int B=0,int C=0,int D=0,int E=0);
internal sealed record CreeperAiState(int Parameter0,int LastSkillFrame);
internal readonly record struct CreeperAiTarget(int Id,int Distance,bool Combat);
internal sealed record CreeperAiContext(bool HasOwner,int OwnerDistance,bool Town,int Frame,int SkillId,int SkillMode,
    MonsterDifficulty Difficulty=MonsterDifficulty.Normal);
internal sealed record CreeperAiServices(Action<CreeperAiRequest> Request,Func<int,bool,int,int,bool> CommonMove,
    Func<int,bool,int,int,bool> PetMove,Func<CreeperAiTarget> FindTarget,Func<int,bool> Poisoned,
    Func<int,int> PoisonResistance,Func<CreeperAiRequest,bool> UseSkill);

// Complete outer Vines policy. World target selection/visibility, movement,
// state and animation/skill execution remain explicit services. This does not
// enable the learned skill or assert modern executable/field equivalence.
internal static class DruidCreeperAi
{
    internal static CreeperAiState Initialize(CreeperAiState state)
        =>state is null?throw new ArgumentNullException(nameof(state)):state with{Parameter0=0};
    internal static CreeperAiState Think(CreeperAiContext c,CreeperAiState state,CreeperAiServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(s);
        if(c.OwnerDistance<0 || state.LastSkillFrame<0 || c.Frame<state.LastSkillFrame
            || c.SkillId is <-1 or >65535 || c.SkillMode is <0 or >15
            || c.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
            || s.Request==null || s.CommonMove==null || s.PetMove==null || s.FindTarget==null
            || s.Poisoned==null || s.PoisonResistance==null || s.UseSkill==null)
        {throw new ArgumentException("Unsupported Creeper AI context/services");}
        int P(int index)=>NecromancerSummons.Source("plaguepoppy","aip"+index,c.Difficulty);
        void Idle(int frames)=>s.Request(new(CreeperAiKind.Idle,frames));
        if(!c.HasOwner){Idle(25);return state;}
        if(c.OwnerDistance>=P(5) && s.PetMove(3,false,0,6)){return state;}
        if(c.Town)
        {if(!s.CommonMove(0,false,0,6)){Idle(P(3));}return state;}
        var target=s.FindTarget();
        if(target.Id<0 || target.Distance<0){throw new ArgumentException("Unsupported Creeper target scan");}
        // The source clears the far target pointer without clearing bCombat.
        int id=target.Distance>=P(2)?0:target.Id;
        if(s.CommonMove(id,target.Combat,0,6)){return state;}
        if(id!=0)
        {
            if(s.Poisoned(id) || s.PoisonResistance(id)==100)
            {s.Request(new(CreeperAiKind.Escape,id,P(4),0));return state;}
            if(!target.Combat)
            {s.Request(new(CreeperAiKind.Walk,id,7));return state;}
            if(c.SkillId>=0 && state.LastSkillFrame>int.MaxValue-P(1))
            {throw new NotSupportedException("Creeper AI game-frame wrap remains unverified");}
            if(c.SkillId>=0 && checked(state.LastSkillFrame+P(1))<c.Frame)
            {
                // The complete source ignores this return value and remembers
                // the attempt frame even when the mode/skill service fails.
                s.UseSkill(new(CreeperAiKind.Skill,c.SkillMode,c.SkillId,id,0,0));
                return state with{LastSkillFrame=c.Frame};
            }
        }
        Idle(P(3));return state;
    }
}
