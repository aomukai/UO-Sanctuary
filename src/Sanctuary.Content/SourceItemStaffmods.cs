// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record StaffmodSkill(int RequiredType);
internal abstract class ItemStaffmodServices
{
    internal abstract int ClassId{get;}
    internal abstract int SkillCount(int classId);
    internal abstract int FirstSkill(int classId);
    internal abstract int Format{get;}
    internal abstract int Quality{get;}
    internal abstract int ItemLevel{get;}
    internal abstract uint DropFlags{get;}
    internal abstract uint Random();
    internal abstract StaffmodSkill Skill(int id);
    internal abstract bool IsType(int type);
    internal abstract bool List();
    internal abstract void Write(int skill,int value);
}

// Complete older dispatcher and modern/legacy generators. The six-attempt
// modern loop deliberately writes its last candidate after exhaustion.
internal static class SourceItemStaffmods
{
    internal static void Apply(ItemStaffmodServices s)
    {
        int c=s.ClassId;if(c is<0 or>=7){return;}
        int count=s.SkillCount(c);if(count<=0){return;}
        int first=s.FirstSkill(c);
        if(s.Format!=0){Modern(s,s.ItemLevel,first,count,(s.DropFlags&0x20)!=0?s.ItemLevel:0);}
        else{Legacy(s,s.ItemLevel,first);}
    }
    internal static void Modern(ItemStaffmodServices s,int level,int first,int skillCount,int dropLevel)
    {
        _=skillCount;
        ModernCore(s,level,first,dropLevel,null);
    }
    // Explicit Sanctuary adaptation:current required-level tier lookup may
    // replace only the old fixed-five candidate mapping. All other count,
    // tier,value,restriction,retry,overwrite and RNG rules stay shared.
    internal static void ModernByTier(ItemStaffmodServices s,int level,int dropLevel,Func<int,uint,int> candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ModernCore(s,level,0,dropLevel,candidate);
    }
    private static void ModernCore(ItemStaffmodServices s,int level,int first,int dropLevel,Func<int,uint,int> candidate)
    {
        int roll=checked(dropLevel+(int)(s.Random()%100));
        int count=roll>90?3:roll>70?2:roll>30||dropLevel!=0?1:0;
        if(count==0){return;}
        int basis=level>36&&s.Format>=100?5:level>24?4:level>18?3:level>11?2:1;
        int[] selected=[-1,-1,-1];
        for(int j=0;j<count;j++)
        {
            roll=(int)(s.Random()%100);
            int tier=Math.Max(roll>80?basis+1:roll>30?basis:roll>10?basis-1:basis-2,1);
            if(s.Quality==1&&tier>=4){tier=4;}
            int offset=first+5*(tier-1),id=0;
            for(int i=0;i<6;i++)
            {
                uint draw=s.Random();id=candidate==null?offset+(int)(draw%5):candidate(tier,draw);var skill=s.Skill(id);
                if(skill==null||skill.RequiredType<=0||s.IsType(skill.RequiredType))
                {if(Array.IndexOf(selected,id)<0){selected[j]=id;break;}}
            }
            int value=1;
            if(s.Format<100||s.Quality!=1)
            {roll=checked(dropLevel/2+(int)(s.Random()%100));value=roll>=90?3:roll>=60?2:1;}
            if(s.List()){s.Write(id,value);}
        }
    }
    internal static void Legacy(ItemStaffmodServices s,int level,int first)
    {
        int roll=(int)(s.Random()%100),count=roll>90?3:roll>70?2:roll>30?1:0;
        if(count==0){return;}
        int basis=level>24?4:level>18?3:level>11?2:1;
        for(int i=0;i<count;i++)
        {
            roll=(int)(s.Random()%100);
            int tier=Math.Max(roll>80?basis+1:roll>30?basis:roll>10?basis-1:basis-2,1);
            // The source resets these within each iteration; moving them
            // outside this loop would silently change its duplicate rule.
            int[] selected=[-1,-1,-1];int id;
            do{do{id=first+5*(tier-1)+(int)(s.Random()%5);}while(id==73);}
            while(Array.IndexOf(selected,id)>=0);
            selected[i]=id;
            roll=(int)(s.Random()%100);int value=roll>=90?3:roll>=60?2:1;
            if(s.List()){s.Write(id,value);}
        }
    }
}
