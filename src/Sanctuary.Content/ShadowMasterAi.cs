// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record ShadowMasterMemory(int Repeat=0,int Skill=0,int Rank=1);
internal sealed record ShadowMasterContext(int Approach,int MeleeBonus,int ProgressiveBonus,int RandomPick,int Leash,int IgnoreRange,
    int AttackChance,bool Combat,int TargetDistance,ShadowMasterUnit Target);
internal sealed record ShadowMasterSkill(int Id,int AiType,int Aura,int TargetAura,int Element,int Required,int Bonus,
    bool Progressive,int Missile,int MissileA,int Do);
internal abstract class ShadowMasterServices
{
    internal abstract ShadowMasterUnit Self{get;}
    internal abstract bool HasSkills{get;}
    internal abstract ShadowMasterUnit Owner(ShadowMasterUnit unit);
    internal abstract void DeleteThink();
    internal abstract int Squared(ShadowMasterUnit a,ShadowMasterUnit b);
    internal abstract bool CommonMove(ShadowMasterUnit target,ShadowMasterUnit owner,bool combat,int flags,int leash);
    internal abstract ShadowMasterUnit Target(ShadowMasterUnit unit);
    internal abstract bool TrySkill(int id,bool combat,ShadowMasterUnit target,int x,int y);
    internal abstract IEnumerable<int> Skills();
    internal abstract ShadowMasterSkill Row(int id);
    internal abstract bool State(ShadowMasterUnit unit,int state);
    internal abstract int? Group(int state);
    internal abstract int StateCount{get;}
    internal abstract int Percentage();
    internal abstract int? Left();
    internal abstract void SetLeft(int id);
    internal abstract bool Dead(ShadowMasterUnit target);
    internal abstract bool Enemy(ShadowMasterUnit target);
    internal abstract void Idle(int frames);
    internal abstract ShadowMasterScan Scan();
    internal abstract ShadowMasterUnit Unit(int id);
    internal abstract bool Special(ShadowMasterUnit target);
    internal abstract int LifePercent();
    internal abstract bool Progressive();
    internal abstract bool ClearMissileLine(ShadowMasterUnit target);
    internal abstract uint Raw();
    internal abstract void Run(ShadowMasterUnit target);
    internal abstract bool Retreat(ShadowMasterUnit target,int flags,int mode);
    internal abstract int Resistance(ShadowMasterUnit target,int stat);
    internal abstract int Rank(int id);
    internal abstract int Limited(int maximum);
    internal abstract int? Charges(int id);
    internal abstract int? MissileRange(int id);
    internal abstract int? Drain(ShadowMasterUnit target);
    internal abstract bool Melee(ShadowMasterUnit target);
}

// Whole older AI106/143 decision policy. Services retain actual owned skills,
// source rows,query results,RNG and actions. This prepares the distinct Master
// brain;it does not activate a world actor with unavailable skill consumers.
internal static class ShadowMasterAi
{
    internal static ShadowMasterMemory Think(ShadowMasterContext c,ShadowMasterMemory memory,ShadowMasterServices s)
    {
        if(!s.HasSkills){s.Idle(100);return memory;}
        var target=c.Target;var owner=s.Owner(s.Self);s.DeleteThink();
        if(owner!=null&&s.Squared(s.Self,owner)>c.Leash*c.Leash&&s.CommonMove(null,owner,c.Combat,0,6)){return memory;}
        ShadowMasterUnit temp;
        if(memory.Repeat<=0){temp=target;}
        else
        {
            temp=s.Target(s.Self);if(temp!=null){target=temp;}else{temp=target;}
            if(temp!=null)
            {memory=memory with{Repeat=memory.Repeat-1};if(s.TrySkill(memory.Skill,c.Combat,temp,0,0)){return memory;}}
            else{memory=memory with{Repeat=0,Skill=0};}
        }
        if(c.TargetDistance>c.IgnoreRange){target=null;temp=null;}
        bool SameGroup(int aura,int group)
        {
            bool same=false;
            for(int state=0;state<s.StateCount;state++)
            {if(state!=aura&&s.Group(state)==group&&s.State(s.Self,state)){same=true;}}
            return same;
        }
        if(temp==null)
        {
            foreach(int id in s.Skills())
            {
                var row=s.Row(id);if(row==null){continue;}
                if(row.AiType==1)
                {
                    if(row.Aura>0&&!s.State(s.Self,row.Aura))
                    {
                        int? group=s.Group(row.Aura);bool same=group is >0&&SameGroup(row.Aura,group.Value);
                        if((!same||s.Percentage()<4)&&s.Percentage()<60&&s.TrySkill(id,c.Combat,null,0,0)){return memory;}
                    }
                }
                else if(row.AiType==6&&s.Left()==null&&s.Percentage()<20){s.SetLeft(id);}
            }
        }
        ShadowMasterUnit ownerTarget=null;
        if(owner!=null)
        {
            ownerTarget=s.Target(owner);
            if(ownerTarget!=null)
            {if(!s.Dead(ownerTarget)&&s.Enemy(ownerTarget)){target=ownerTarget;}else{ownerTarget=null;}}
            if(s.Squared(s.Self,owner)>=144&&s.CommonMove(target,owner,c.Combat,0,6)){return memory;}
        }
        if(target==null){s.Idle(25);return memory;}
        int rank=Math.Max(memory.Rank,1),chance=Math.Clamp(c.AttackChance-2*rank,5,100);
        if(c.Combat&&s.Percentage()<chance&&s.TrySkill(0,c.Combat,target,0,0)){return memory;}
        var scan=s.Scan();
        if(!c.Combat)
        {
            if(ownerTarget!=null){target=ownerTarget;}
            else if(scan.NearestOwner!=0){target=s.Unit(scan.NearestOwner);}
            else if(scan.LastSpecial!=0)
            {var special=s.Unit(scan.LastSpecial);if(s.Squared(s.Self,special)<1024){target=special;}}
            if(!s.Special(target))
            {var master=s.Owner(target);if(master!=null&&!s.Dead(master)&&s.Squared(s.Self,master)<1024){target=master;}}
        }
        int life=s.LifePercent(),distance=s.Squared(s.Self,target);
        bool progressive=c.ProgressiveBonus>0&&s.Progressive(),clear=s.ClearMissileLine(target);
        if(scan.Near>3&&(s.Raw()&31)<2*scan.Near)
        {
            if(owner!=null&&s.Squared(s.Self,owner)>36){s.Run(owner);return memory;}
            if(s.Retreat(target,8,1)){return memory;}
        }
        int auraBonus=0;var candidates=new List<(int Id,ShadowMasterUnit Target)> { (0,target) };
        foreach(int id in s.Skills())
        {
            var row=s.Row(id);if(row==null){continue;}var skillTarget=target;int current=0;
            int? stat=row.Element switch{0=>0,1=>1,2=>2,4 or 12=>3,5=>4,3=>5,_=>null};
            int resist=stat.HasValue?s.Resistance(target,stat.Value):0;
            int score=resist/-10+(row.Required/4+s.Rank(id)+row.Bonus);
            switch(row.AiType)
            {
                case 1:
                    if(row.Aura<=0||s.State(s.Self,row.Aura))
                    {
                        if(scan.NearestSquared<=25){score-=6;}
                        int? group=s.Group(row.Aura);
                        score+=group==null||group==0||s.StateCount<=0?10:SameGroup(row.Aura,group.Value)?-10:10;
                        skillTarget=s.Self;current=score+s.Limited(c.RandomPick);
                    }
                    break;
                case 2:
                    if((row.Aura<=0||!s.State(s.Self,row.Aura))&&(row.TargetAura<=0||!s.State(target,row.TargetAura)))
                    {if(scan.NearestSquared<=25){score-=10;}current=score+s.Limited(c.RandomPick);}
                    break;
                case 3:
                    if(scan.Traps>5){score-=2*scan.Traps;}if(scan.NearestSquared<=25){score-=7;}if(scan.Within32<3){score-=10;}
                    current=score+s.Limited(c.RandomPick)+3*(scan.Within32-3);break;
                case 4:
                    if(distance>c.Approach*c.Approach){score-=10;}score+=c.MeleeBonus;if(c.Combat||distance<=25){score+=10;}
                    if(!row.Progressive){current=score+(c.ProgressiveBonus<=0||progressive?4*auraBonus+3:-10)+s.Limited(c.RandomPick);}
                    else
                    {int? charges=s.Charges(id);int count=charges??0;if(charges.HasValue){auraBonus+=count;}if(count<3){current=c.ProgressiveBonus+score+s.Limited(c.RandomPick);}}
                    break;
                case 5:case 11:
                    if(!clear){break;}
                    if(row.Missile<0&&s.MissileRange(row.MissileA)is{} range&&distance>=(range-1)*(range-1)){break;}
                    if(scan.NearestSquared<=25){score-=5;}if(distance<=25){score-=5;}if(progressive){score-=5;}
                    current=score+s.Limited(c.RandomPick)+(row.AiType==11?3*scan.Within32:0);break;
                case 6:
                    if(s.Left()==null?s.Percentage()<20:s.Percentage()<6){s.SetLeft(id);}break;
                case 7:case 8:
                    current=score+s.Limited(c.RandomPick);
                    if(life<=66)
                    {if(row.AiType==7){skillTarget=null;current+=life<45?20:10;}else{skillTarget=s.Self;current*=life<45?4:2;}}
                    else{current=0;}break;
                case 12:
                    if(target==null||target.Kind!=1||s.Drain(target)is >=25)
                    {
                        if(distance>c.Approach*c.Approach){score-=10;}score+=c.MeleeBonus;if(c.Combat||distance<=25){score+=10;}
                        int count=0;if(row.Progressive&&s.Charges(id)is{} charges){count=charges;auraBonus+=count;}
                        if(count<3){current=score+s.Limited(c.RandomPick);if(life<75){current+=8;}if(life<50){current+=12;}}
                    }
                    break;
                case 13:
                    score+=c.MeleeBonus;score+=c.ProgressiveBonus<=0||progressive?auraBonus:-5;
                    var nearest=s.Unit(scan.NearestOwner);
                    if((life<50||scan.Near>3)&&nearest!=null&&scan.NearOwner<4&&s.Squared(s.Self,nearest)>25)
                    {skillTarget=nearest;current=score+20+s.Limited(c.RandomPick);}
                    else if(distance>=25){if(distance>324){score+=10;}current=score+s.Limited(c.RandomPick);}
                    break;
            }
            // The original array appends every positive candidate to a new
            // zero slot. It does not sort scores or choose a weighted maximum.
            if(current>0)
            {if(candidates.Count>=48){throw new NotSupportedException("Master candidate array exceeds the qualified source capacity");}candidates.Add((id,skillTarget));}
        }
        for(int index=candidates.Count-1;index>=0;index--)
        {
            var pick=candidates[index];
            if((s.Raw()&3)!=0&&s.TrySkill(pick.Id,s.Melee(pick.Target),pick.Target,0,0))
            {if(s.Row(pick.Id)is{Do:19}){memory=memory with{Skill=pick.Id,Repeat=25};}return memory;}
        }
        if(target==null||!s.TrySkill(0,s.Melee(target),target,0,0)){s.Idle(15);}return memory;
    }
}
