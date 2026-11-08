// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record ShadowMasterUnit(int Id,int Kind);
internal sealed record ShadowMasterMonster(int Base,bool Npc,bool Killable);
internal sealed record ShadowMasterScan(int Nearest=0,int NearestSquared=int.MaxValue,int Near=0,
    int NearestOwner=0,int OwnerSquared=int.MaxValue,int NearOwner=0,int Within32=0,int Traps=0,int LastSpecial=0);
internal sealed record ShadowMasterQueries(Func<ShadowMasterUnit,bool> Dead,Func<ShadowMasterUnit,bool> Aligned,
    Func<ShadowMasterUnit,ShadowMasterMonster> Monster,Func<ShadowMasterUnit,bool> Attackable,
    Func<ShadowMasterUnit,bool> Enemy,Func<int,ShadowMasterUnit,int> Squared,
    Func<ShadowMasterUnit,bool> Boss,Func<ShadowMasterUnit,bool> Prime,Func<ShadowMasterUnit,int,bool> Types);

// Complete AI106 callback policy. Enumeration,allegiance,source rows and actual
// squared source-cell distances are explicit world services. This is not AI105
// or an invented nearest-enemy replacement for the unfinished Master brain.
internal static class ShadowMasterTargets
{
    // Despite its older "IsTargetKillable" name,this helper accepts only
    // living killable non-NPC MONSTERS with boss/prime/elite classification.
    // Ordinary enemies remain eligible for the callback's other accumulators.
    internal static bool Special(int self,ShadowMasterUnit target,ShadowMasterQueries q)
    {
        if(target==null||target.Id==self||target.Kind!=1||q.Dead(target)){return false;}
        var row=q.Monster(target);if(row==null||row.Npc||!row.Killable){return false;}
        return q.Boss(target)||q.Prime(target)||q.Types(target,2|4|8);
    }
    internal static ShadowMasterScan Consider(int self,int owner,ShadowMasterScan s,ShadowMasterUnit target,ShadowMasterQueries q)
    {
        ArgumentNullException.ThrowIfNull(s);ArgumentNullException.ThrowIfNull(q);
        if(target?.Id==self||q.Dead(target)||target==null){return s;}
        if(target.Kind==1&&q.Aligned(target))
        {
            var row=q.Monster(target);
            if(row?.Base is 410 or 411 or 412 or 413 or 415 or 416){return s with{Traps=checked(s.Traps+1)};}
        }
        if(!q.Attackable(target)||!q.Enemy(target)){return s;}
        if(owner!=0)
        {
            int distance=q.Squared(owner,target);
            if(distance<=100){s=s with{NearOwner=checked(s.NearOwner+1)};}
            if(distance<s.OwnerSquared){s=s with{NearestOwner=target.Id,OwnerSquared=distance};}
        }
        int own=q.Squared(self,target);
        if(own<=1024)
        {
            s=s with{Within32=checked(s.Within32+1)};
            if(own<=100){s=s with{Near=checked(s.Near+1)};}
            if(own<s.NearestSquared){s=s with{Nearest=target.Id,NearestSquared=own};}
            if(Special(self,target,q)){s=s with{LastSpecial=target.Id};}
        }
        return s;
    }
    internal static ShadowMasterScan Scan(int self,int owner,IEnumerable<ShadowMasterUnit> candidates,ShadowMasterQueries q,ShadowMasterScan initial=null)
    {
        ArgumentNullException.ThrowIfNull(candidates);var s=initial??new();
        foreach(var target in candidates){s=Consider(self,owner,s,target,q);}return s;
    }
}

// Complete older sub_6FCECBA0 tactic admission. Actual movement/action and
// owned-skill lookup are supplied by the Master world bridge,not owner spells.
internal static class ShadowMasterTactics
{
    internal static int Use(int self,int owner,ShadowMasterUnit target,int id,bool combat,int x,int y,
        Func<int,ShadowAiSkill> lookup,Func<ShadowAiSkill,bool> melee,
        Func<ShadowMasterUnit,int,int> run,Func<ShadowAiSkill,ShadowMasterUnit,int,int,int> action)
    {
        if(target!=null&&(target.Id==owner||target.Id==self)){return 0;}
        var skill=lookup(id);if(skill==null){return 0;}
        return melee(skill)&&!combat?run(target,4):action(skill,target,x,y);
    }
}
