// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct RecyclerCorpse(int Id,int Distance,bool Melee);
internal sealed record RecyclerAiContext(CombatAbility Ability,bool HasOwner,int OwnerDistance,int Frame,int SkillId,
    bool HasSkillRow,bool HasInnateSkill,int AuraRange,int PrimaryTarget,bool PrimaryDead,bool Combat,
    bool LifeFull,bool ManaFull,MonsterDifficulty Difficulty=MonsterDifficulty.Normal);
internal sealed record RecyclerAiServices(Action<CreeperAiRequest> Request,Func<int,bool,int,int,bool> CommonMove,
    Func<int,bool,int,int,bool> PetMove,Func<int,RecyclerCorpse> FindCorpse,Func<int> Roll,Func<CreeperAiRequest,bool> UseSkill);

// Complete AI111 decision policy. Search/movement/geometry/dispatch are explicit
// services, shared with existing pet movement only when their contracts match.
internal static class DruidRecyclerAi
{
    internal static CreeperAiState Think(RecyclerAiContext c,CreeperAiState state,RecyclerAiServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(s);
        string key=DruidRecyclerStats.Source(c.Ability);
        if(c.OwnerDistance<0||c.Frame<state.LastSkillFrame||state.LastSkillFrame<0||c.SkillId is <-1 or >65535
            ||c.PrimaryTarget<0||c.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
            ||s.Request==null||s.CommonMove==null||s.PetMove==null||s.FindCorpse==null||s.Roll==null||s.UseSkill==null)
        {throw new ArgumentException("Unsupported recycler AI context/services");}
        int P(int index)=>NecromancerSummons.Source(key,"aip"+index,c.Difficulty);
        if(!c.HasOwner){return state;}
        if(c.OwnerDistance>=P(5)&&s.PetMove(3,false,0,6)){return state;}
        int primary=c.PrimaryDead?0:c.PrimaryTarget;bool combat=!c.PrimaryDead&&c.Combat;
        var corpse=c.SkillId>0&&c.HasSkillRow&&c.HasInnateSkill?s.FindCorpse(Math.Clamp(c.AuraRange,5,50)):default;
        if(corpse.Id<0||corpse.Distance<0){throw new ArgumentException("Unsupported recycler corpse scan");}
        int target=corpse.Distance<P(2)?corpse.Id:0;bool melee=target!=0&&corpse.Melee;
        if(s.CommonMove(target,melee,0,6)){return state;}
        bool use=c.Ability==CombatAbility.CarrionVine?!c.LifeFull:!c.ManaFull;
        if(target!=0&&melee&&use&&(long)state.LastSkillFrame+P(1)<c.Frame)
        {
            s.UseSkill(new(CreeperAiKind.Skill,8,c.SkillId,target));
            return state with{LastSkillFrame=c.Frame};
        }
        if(combat&&primary!=0)
        {
            int roll=s.Roll();if(roll is <0 or >99){throw new ArgumentException("Invalid recycler percentage roll");}
            if(roll<25){s.Request(new(CreeperAiKind.Escape,primary,P(4),0));return state;}
        }
        s.Request(target!=0?new(CreeperAiKind.Walk,target,7):new(CreeperAiKind.Idle,P(3)));
        return state;
    }
}
