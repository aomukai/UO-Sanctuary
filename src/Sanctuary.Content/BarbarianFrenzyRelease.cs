// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record FrenzyReleaseRow(short AuraState,ushort ResultFlags,uint HitFlags,uint HitClass,byte Element,byte SourceDamage);
internal sealed class FrenzyUsedSkill(int parameter){internal int Param1=parameter;}
internal sealed class FrenzyReleasePacket
{
    internal ushort ResultFlags;
    internal uint HitFlags,HitClass;
    internal int DamagePercent,ConversionPercent,Elemental;
    internal sbyte ConversionType;
}
internal sealed class FrenzyReleaseList(int charges,int expiry)
{
    internal int Charges=charges,Expiry=expiry,State=-1;
    internal bool RemoveCallback,Posted;
    internal int Velocity,AttackRate,OtherAnimationRate;
}
internal abstract class FrenzyStatServices
{
    internal abstract FrenzyReleaseRow Row(int skill);
    internal abstract FrenzyUsedSkill UsedSkill();
    internal abstract int StateCount{get;}
    internal abstract int GameFrame{get;}
    internal abstract int UnitKind{get;}
    internal abstract int UnitId{get;}
    // Slots0/3 are calc1/calc4;-1 is auralencalc. Aura writing is the
    // existing shared helper,which evaluates with the charge count as level.
    internal abstract int Formula(int slot,int skill,int rank);
    internal abstract FrenzyReleaseList List(int state);
    internal abstract FrenzyReleaseList AllocateList(int flags,int expiry,int kind,int id);
    internal abstract void SetRemoveCallback(FrenzyReleaseList list);
    internal abstract void PostList(FrenzyReleaseList list,int flag);
    internal abstract void ToggleState(int state,int flag);
    internal abstract void ScheduleRemoval(int frame,int zero,int zero2);
    internal abstract void ToggleGraphic(int state,int flag);
    internal abstract void WriteAura(FrenzyReleaseList list,FrenzyReleaseRow row,int skill,int level);
}
internal abstract class FrenzyReleaseServices:FrenzyStatServices
{
    internal abstract SourceGuidTarget Target();
    internal abstract void SetTarget(SourceGuidTarget target);
    internal abstract int ToHit(int skill,int rank);
    internal abstract int ResultFlags(SourceGuidTarget target,int toHit,int zero);
    internal abstract void RollElemental(FrenzyReleasePacket packet,int skill,int rank);
    internal abstract void AllocateCombat(SourceGuidTarget target,FrenzyReleasePacket packet,byte sourceDamage);
    internal abstract void Drain(SourceGuidTarget target,int zero);
    internal abstract void FreeCombat(SourceGuidTarget target);
    internal abstract void RefreshEquipment();
    internal abstract void RemoveBrokenWeapons();
    internal abstract int MeleeRange();
    internal abstract SourceGuidTarget Search(SourceGuidFindRequest request);
}

// Complete older do9/ApplyFrenzyStats/RollFrenzyDamage control flow. Actual
// stat lists,packet consumers,equipment and modern start67 remain services;
// this does not invent the initial success latch or a mana payment interval.
internal static class BarbarianFrenzyRelease
{
    internal static void ApplyStats(int skill,int rank,FrenzyStatServices s)
    {
        var row=s.Row(skill);if(row==null){return;}
        var used=s.UsedSkill();if(used==null||used.Param1==0||row.AuraState<0||row.AuraState>=s.StateCount){return;}
        int expiry=unchecked(s.GameFrame+s.Formula(-1,skill,rank));
        var list=s.List(row.AuraState);
        if(list==null)
        {
            list=s.AllocateList(2,expiry,s.UnitKind,s.UnitId);
            if(list!=null)
            {
                list.State=row.AuraState;s.SetRemoveCallback(list);list.Charges=0;
                s.PostList(list,1);s.ToggleState(row.AuraState,1);
            }
        }
        if(list==null){return;}
        list.Expiry=expiry;s.ScheduleRemoval(expiry,0,0);s.ToggleGraphic(row.AuraState,1);
        int count=unchecked(list.Charges+1);if(count>rank){count=rank;}
        list.Charges=count;s.WriteAura(list,row,skill,count);
    }
    internal static int Roll(SourceGuidTarget target,int skill,int rank,FrenzyReleaseServices s)
    {
        var row=s.Row(skill);if(row==null||target==null){return 0;}
        s.SetTarget(target);
        var packet=new FrenzyReleasePacket{ResultFlags=unchecked((ushort)s.ResultFlags(target,s.ToHit(skill,rank),0))};
        if((packet.ResultFlags&SourceMeleeResult.Successful)!=0)
        {
            packet.ResultFlags|=row.ResultFlags;packet.HitFlags|=row.HitFlags;
            if(row.HitClass!=0){packet.HitClass=row.HitClass;}
            packet.DamagePercent=s.Formula(0,skill,rank);
            if(row.Element!=0)
            {packet.ConversionPercent=s.Formula(3,skill,rank);if(packet.ConversionPercent>0){packet.ConversionType=unchecked((sbyte)row.Element);}}
            s.RollElemental(packet,skill,rank);
        }
        s.AllocateCombat(target,packet,row.SourceDamage==0?(byte)128:row.SourceDamage);s.Drain(target,0);
        // Look up the used skill after combat/equipment callbacks;do not
        // update an earlier captured skill object if those callbacks changed it.
        var used=s.UsedSkill();if(used!=null){used.Param1=(packet.ResultFlags&SourceMeleeResult.Successful)!=0?1:0;}
        return 1;
    }
    internal static int Execute(DoubleSwingPhaseUnit unit,int skill,int rank,FrenzyReleaseServices s)
    {
        var target=s.Target();if(target==null){return 0;}
        bool even=((unit.SequenceFrame>>8)%2)==0;
        if(!even){unit.Flags|=0x40;}
        s.Drain(target,0);if(even){s.FreeCombat(target);}
        s.RefreshEquipment();s.RemoveBrokenWeapons();ApplyStats(skill,rank,s);
        if(!even){target=s.Search(new(0,0,unchecked(s.MeleeRange()+4),0x20003,target.Id));}
        return Roll(target,skill,rank,s);
    }
}
