// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record ShadowWarriorMemory(int AllowedFrame=0,int Budget=0);
internal sealed record ShadowAiSkill(int Id,int Class,int Mode,int AiType,int Rank,int Mana,int Delay,
    bool Aura,bool TargetAura,bool Progressive,int Range);
internal sealed record ShadowWarriorContext(int Frame,int Target,int Distance,bool Combat,int OwnerDistance,int Rank,
    int MaxTarget,int MaxOwner,int AttackChance,int Decrement,int MinimumBudget,int MaximumBudget);
internal sealed record ShadowWarriorServices(Func<bool> Owner,Func<int?> Left,Func<int?> Right,Action<int,int> Mirror,
    Func<int,ShadowAiSkill> Skill,Func<ShadowAiSkill,bool> PetAllowed,Func<ShadowAiSkill,bool> SelfState,
    Func<ShadowAiSkill,bool> TargetState,Func<ShadowAiSkill,bool> ThreeCharges,Func<uint> Raw,
    Func<int,int> Limited,Func<int> Percentage,Func<int,bool,int,int,bool> CommonMove,
    Action<int> Idle,Action<int> RunTarget,Action<ShadowAiSkill,int> UseSkill,Action EnsureAttack);

// Complete older AI105 selection and eligibility policy. World queries,
// owned skills and actual actions are services; no generic NecroPet policy.
internal static class ShadowWarriorAi
{
    internal static ShadowWarriorMemory Think(ShadowWarriorContext c,ShadowWarriorMemory old,ShadowWarriorServices s)
    {
        if(!s.Owner()){s.Idle(100);return old;}
        int target=c.Target,budget=checked(old.Budget-1-c.Decrement),max=Math.Clamp(c.MaximumBudget,1,256);
        if(budget<0||budget>max<<6){budget=0;}
        var memory=old with{Budget=budget};
        if(c.Distance>c.MaxTarget||c.OwnerDistance>c.MaxOwner){target=0;}
        if(s.CommonMove(target,c.Combat,0,6)){return memory;}
        if(target==0){s.Idle(25);return memory;}
        int? right=s.Right(),left=s.Left();
        if(right.HasValue&&left.HasValue)
        {
            s.Mirror(left.Value,right.Value);var r=s.Skill(right.Value);var l=s.Skill(left.Value);
            if(r!=null&&l!=null)
            {
                var skill=(s.Raw()&1)==0?r:l;
                int chance=Math.Clamp(c.AttackChance-2*Math.Max(c.Rank,1),5,100);
                if(c.Combat&&s.Percentage()<chance){skill=s.Skill(0);}
                bool Eligible(ShadowAiSkill row)
                {
                    if(row==null||row.Class!=6||!s.PetAllowed(row)){return false;}
                    if(c.Combat?row.AiType is not(4 or 13):row.AiType is 4 or 13){return false;}
                    if(row.AiType==1&&row.Aura&&s.SelfState(row)){return false;}
                    if(c.Target==0&&row.AiType is 2 or 4 or 5 or 11 or 12 or 13){return false;}
                    if(row.AiType==2&&row.Aura&&s.SelfState(row)||row.TargetAura&&s.TargetState(row)){return false;}
                    if(row.Progressive&&row.Aura&&s.ThreeCharges(row)){return false;}
                    if(row.Id!=0)
                    {
                        if(s.Percentage()>100-160*row.Mana/100||memory.AllowedFrame>c.Frame){return false;}
                        int minimum=Math.Clamp(c.MinimumBudget,1,128),maximum=Math.Clamp(c.MaximumBudget,1,256);
                        int current=memory.Budget;if(current<minimum||current>32*maximum){current=minimum;}
                        memory=memory with{Budget=current};
                        if(s.Limited(current)>s.Percentage()){return false;}
                        memory=memory with{Budget=checked(current+row.Mana*(320-c.Rank)/(c.Rank+100))};
                    }
                    return true;
                }
                if(!Eligible(skill))
                {
                    skill=skill!=l?l:r;
                    if(!Eligible(skill)){skill=s.Skill(0);if(skill==null){s.EnsureAttack();skill=s.Skill(0);}}
                }
                if(skill!=null)
                {
                    if(skill.Range==1&&!c.Combat){s.RunTarget(target);return memory;}
                    s.UseSkill(skill,target);return memory with{AllowedFrame=checked(skill.Delay/3+c.Frame+18)};
                }
            }
        }
        s.Idle(25);return memory;
    }
}
