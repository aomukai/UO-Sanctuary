// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;
// Raw source fields; descriptor tail bytes are explicit caller scratch,
// not initialized skill data or persistent actor state. World metadata remains
// supplied by services until the real actor adapter is verified.
internal sealed record GenericPetGatherEntry(int Token,short SkillId,int Raw16,int Raw68,int Raw76);
internal sealed record GenericPetGatherContext(bool HasPet,int ClassId,byte UnitByte445,IReadOnlyList<GenericPetGatherEntry> Skills)
{internal byte Selector=>HasPet?UnitByte445:(byte)0;}
internal sealed record GenericPetSkillAiInfo(byte Flags36,byte Flags39,byte AiType728);
internal sealed class GenericPetGatherState(int value108)
{internal int Value108=value108;}
internal sealed record GenericPetGatherServices(Func<GenericPetGatherEntry,bool,int> SkillLevel,
    Func<byte,int,GenericPetSkillAiInfo> SkillInfo,Func<int,int> Flag4Value,
    Func<byte,int,int,short?> MonsterSkill,Func<byte,int> SkillCount,
    Func<bool,int,ushort> ScratchTail);
internal static class GenericPetGather
{
    private static void AddSkill(GenericPetGatherContext c,GenericPetParameters p,GenericPetGatherState state,GenericPetGatherServices s,List<GenericPetCombatCandidate> result,GenericPetGatherEntry entry)
    {
        int level=s.SkillLevel(entry,p.AllowSkillBonuses);if(level<=0){return;}
        int skill=entry?.SkillId??0,raw16=entry?.Raw16??0;byte type=0;
        var info=s.SkillInfo(c.Selector,skill);
        if(info!=null)
        {type=info.AiType728;if((info.Flags36&4)!=0){state.Value108=unchecked(state.Value108+s.Flag4Value(skill));}}
        ushort tail=s.ScratchTail(false,entry?.Token??-1);
        result.Add(new(0,0,int.MinValue,skill,level,raw16,0,type,0,(byte)tail,(byte)(tail>>8)));
    }
    internal static List<GenericPetCombatCandidate> Build(GenericPetGatherContext c,GenericPetParameters p,GenericPetGatherState state,GenericPetGatherServices s,List<GenericPetCombatCandidate> result=null)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(p);ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(s);
        result??=new List<GenericPetCombatCandidate>();result.Clear();byte mode=unchecked((byte)p.ModeAiType);
        if(mode!=0)
        {ushort tail=s.ScratchTail(true,-1);result.Add(new(0,0,int.MinValue,-1,0,p.ModeAttack,0,mode,1,(byte)tail,(byte)(tail>>8)));}
        if(p.GatherType is 1 or 2)
        {
            if(!c.HasPet){throw new NotSupportedException("Current skill-list gather requires a pet");}
            if(c.Skills!=null)
            {foreach(var entry in c.Skills){if(entry.Raw76==0&&(p.GatherType==2||entry.Raw68==-1)){AddSkill(c,p,state,s,result,entry);}}}
            return result;
        }
        if(p.GatherType!=0){return result;}
        for(int slot=0;slot<8;slot++)
        {
            short? skill=s.MonsterSkill(c.Selector,c.HasPet?c.ClassId:-1,slot);
            if(!skill.HasValue){throw new NotSupportedException("No current monster skill-row metadata");}
            if(skill.Value<=-1){continue;}
            if(c.Selector is <1 or >3){throw new NotSupportedException("Current gather skill-table selector requires 1..3");}
            if(skill.Value>=s.SkillCount(c.Selector)){continue;}
            if(!c.HasPet){throw new NotSupportedException("Current monster skill gather requires a pet");}
            if(c.Skills==null){continue;}
            foreach(var entry in c.Skills)
            {
                if(entry.SkillId!=skill.Value||entry.Raw68!=-1){continue;}
                // Mode 0 does not apply the mode 1/2 Raw76 filter, and repeated
                // monster slots may deliberately append the same skill again.
                AddSkill(c,p,state,s,result,entry);break;
            }
        }
        return result;
    }
    // This is a search category/flag mask, not a radius. Keep the legacy
    // orchestration's Range service name until its caller API is migrated.
    internal static int SearchMask(GenericPetGatherContext c,ushort flags,IReadOnlyList<GenericPetCombatCandidate> rows,GenericPetGatherServices s)
    {
        int mask=0;
        foreach(var row in rows)
        {
            int value=0;
            if(row.BaseModeFlag==0&&s.SkillInfo(c.Selector,row.SkillId) is{} info&&(info.Flags39&1)!=0){value=8;}
            value=row.Byte36 switch
            {
                2=>value|((flags&2)!=0?32:0),
                3 or 11=>value|4,
                4 or 12=>value|1,
                5=>value|2,
                7=>value|16,
                13=>value|17,
                _=>0
            };
            mask|=value;
        }
        return mask;
    }
}
