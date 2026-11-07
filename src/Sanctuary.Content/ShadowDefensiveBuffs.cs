// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

// The player and NPC paths share the existing table evaluator/capture. NPC
// values are real posted actor stat lists, not bonuses copied to its owner.
internal static class ShadowDefensiveBuffs
{
    internal static bool Handles(int id)=>id is 258 or 267 or 278;
    internal static int State(int id)=>id switch{258=>157,267=>159,278=>31,_=>throw new NotSupportedException("Unqualified Shadow buff: "+id)};
    internal static ShadowPassiveList Capture(int id,SkillValues values,DateTime at,int poisonMastery=0)
    {
        if(!Handles(id)||SkillMechanics.For(values.Ability).SourceId!=id||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Shadow buff needs its own intrinsic skill values and UTC release");}
        (int Stat,int Value)[] fields;DateTime until;
        if(id==278)
        {
            var venom=Venoms.Capture(0,values,poisonMastery,at);until=venom.UntilUtc;
            fields=[(57,venom.Minimum256),(58,venom.Maximum256),(101,venom.Frames)];
        }
        else
        {
            var buff=AssassinBuffs.Capture(0,values,at);until=buff.UntilUtc;
            fields=id==258?[(67,buff.Velocity),(68,buff.AttackRate),(69,buff.AttackRate)]
                :[(39,buff.ElementResistance),(43,buff.ElementResistance),(41,buff.ElementResistance),(45,buff.ElementResistance),
                    (109,buff.CurseResistance),(36,buff.PhysicalResistance),(181,buff.Fade)];
        }
        var data=fields.Concat(new[]{(350,id),(351,values.Rank)}).Where(f=>f.Item2!=0)
            .Select(f=>new EquipmentStatValue(EquipmentStatKey.Pack(f.Item1),f.Item2)).OrderBy(f=>f.Packed).ToArray();
        return new(id,State(id),values.Rank,data){UntilUtc=until,PoisonMastery=id==278?poisonMastery:null};
    }
    internal static void Validate(ShadowPassiveList list)
    {
        if(list==null||!Handles(list.Skill)||list.State!=State(list.Skill)||list.Level is<1 or>255||list.Values==null
            ||list.UntilUtc is not{Kind:DateTimeKind.Utc}||(list.Skill==278?list.PoisonMastery is not>=0:list.PoisonMastery.HasValue))
        {throw new InvalidDataException("Invalid timed Shadow buff");}
        var d=SkillMechanics.All.Single(s=>s.SourceId==list.Skill);var values=new SkillValues(d,key=>key==d.SourceKey?list.Level:0,_=>0,1);
        var expected=Capture(list.Skill,values,DateTime.UnixEpoch,list.PoisonMastery??0);
        if(!list.Values.SequenceEqual(expected.Values)){throw new InvalidDataException("Shadow buff escaped its captured source values");}
        foreach(var field in list.Values)
        {
            var cost=EquipmentStatValues.Definition(EquipmentStatKey.Stat(field.Packed));
            // Existing numeric writers handle the current resistance op2/6
            // dependents and attackrate's damage-related flag. These lists
            // are temporary flag2, not dynamic weapon-merge lists.
            if(cost.Flags!=(field.Stat is 57 or 58 or 68?4u:0u)||cost.Operation!=0||cost.HasOperations!=(field.Stat is 39 or 41 or 43 or 45 or 58)
                ||cost.BaseOfOther||cost.AppliesToItem||cost.Dependents.Length!=0)
            {throw new NotSupportedException("Defensive buff fields need their additional numeric operations");}
        }
    }
}
