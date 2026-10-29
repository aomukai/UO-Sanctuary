// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal sealed record GenericPetScoreSkill(byte Flags36,byte Flags39,short Quarter532,short Bonus730,byte Element644,
    short State162,short Class272,sbyte OwnerCompare274,int Calc276);
internal sealed record GenericPetScoreUnit(GenericPetDecisionUnit Position,int ClassId,int UnitId,byte Selector445,bool HasStats,
    bool HasMonsterData=false,bool HasMonsterRow=false,bool SnapshotOverride=false,byte SnapshotByte8=0);
internal sealed record GenericPetScoreContext(byte GameSelector,byte GameByte260,int Pet,int Owner,int Frame100,int Frame104,int Frame108);
internal sealed record GenericPetScoreServices(Func<byte,int,GenericPetScoreSkill> Skill,Func<int,int,int> Flag4Value,
    Func<int,GenericPetScoreUnit> Unit,Func<byte,int,bool> StatCost,Func<int,int,int> StatValue,Func<int,int,int,int> Distance,
    Func<byte,int> StateCount,Func<byte,int,ushort?> StateGroup,Func<int,int,bool> HasState,
    Func<int,int,int> OwnerValue0,Func<int,int> OwnerValue1,Func<byte,int,int,int,int,int> Calculate,
    Func<int,byte,byte> MonsterByte242,Func<int,bool> Collision0);
internal static class GenericPetTargetScoring
{
    private static void Selector(byte selector)
    {if(selector is <1 or >3){throw new NotSupportedException("Current target scoring state-table selector requires 1..3");}}
    private static long XY(int x,int y)=>unchecked((long)(((ulong)(uint)y<<32)|(uint)x));
    internal static GenericPetCombatCandidate Rate(GenericPetScoreContext c,GenericPetParameters p,long word40,long word48,
        GenericPetCombatCandidate row,GenericPetActionRandom random,GenericPetScoreServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(p);ArgumentNullException.ThrowIfNull(random);ArgumentNullException.ThrowIfNull(s);
        if(c.Pet==0){throw new NotSupportedException("Current target scorer requires a pet");}
        unchecked
        {
            var pet=s.Unit(c.Pet);int score=0;GenericPetScoreSkill skill=null;
            GenericPetCombatCandidate Reject()=>row with{Rating=int.MinValue};
            GenericPetCombatCandidate Finish(int value)=>row with{Rating=value+(int)SourceRandom.Limited(ref random.Seed,p.RandomBonus)};
            if(row.BaseModeFlag!=0){score=p.ModeBonus;}
            else
            {
                skill=s.Skill(c.GameSelector,row.SkillId);
                if(skill!=null)
                {
                    score=skill.Bonus730+skill.Quarter532/4+row.Opaque24;
                    if((skill.Flags36&4)!=0)
                    {if(s.Flag4Value(c.Pet,row.SkillId)>3){return Reject();}score+=p.ProgressiveBonus;}
                    if((skill.Flags36&8)!=0){score+=c.Frame108>0?2*c.Frame108:-10;}
                    if(row.Target!=0)
                    {
                        int stat=skill.Element644 switch{0=>36,1=>39,2=>41,3=>37,4 or 12=>43,5=>45,_=>-1},value=0;
                        if(stat>=0)
                        {
                            var target=s.Unit(row.Target);
                            if(target.HasStats&&s.StatCost(target.Selector445,stat)){value=s.StatValue(row.Target,stat);}
                        }
                        int absolute=value<0?-value:value;int adjustment=absolute>=100?20:absolute>=66?10:absolute>=33?5:0;
                        score+=value>0?-adjustment:adjustment;
                    }
                }
            }
            int distance=0;
            if(row.Target!=0){var(x,y)=s.Unit(row.Target).Position.Coordinates();distance=s.Distance(c.Pet,x,y);}
            switch(row.Byte36)
            {
                case 1:case 6:case 9:case 10:return Reject();
                case 2:
                {
                    if(skill==null){throw new NotSupportedException("Current type2 target scorer requires skill metadata");}
                    int state=skill.State162;if(state<0){return row;}Selector(c.GameSelector);
                    if(state>=s.StateCount(c.GameSelector)){return row;}
                    ushort? group=s.StateGroup(c.GameSelector,state);
                    if(group.HasValue&&group.Value!=0)
                    {
                        for(int i=0;;i++)
                        {
                            Selector(c.GameSelector);if(i>=s.StateCount(c.GameSelector)){break;}
                            var candidate=s.StateGroup(c.GameSelector,i);
                            if(candidate.HasValue&&candidate.Value==group.Value&&s.HasState(row.Target,i)){return row;}
                        }
                    }
                    else if(s.HasState(row.Target,state)){return row;}
                    return row with{Rating=int.MaxValue};
                }
                case 3:
                {
                    if(skill!=null&&c.Owner!=0&&s.Unit(c.Owner).Position.Kind==0)
                    {
                        int value=s.OwnerValue0(c.Owner,pet.UnitId);
                        if(skill.Class272==pet.ClassId||value==skill.OwnerCompare274){return Reject();}
                        int current=s.OwnerValue1(c.Owner),limit=s.Calculate(c.GameSelector,c.Owner,skill.Calc276,row.SkillId,row.Opaque24);
                        if(current>=limit){return Reject();}if(current>=limit/2){score-=10;}
                    }
                    uint a=(uint)((ulong)word48>>32),b=(uint)word48;
                    score+=a<3?-10:(int)(2*a-6);if(b>0){score-=10;}return Finish(score);
                }
                case 4:case 12:
                {
                    if(row.Byte36==12&&row.Target!=0)
                    {
                        var target=s.Unit(row.Target);
                        if(target.Position.Kind==1&&target.HasMonsterData&&target.HasMonsterRow)
                        {byte index=target.SnapshotOverride?target.SnapshotByte8:c.GameByte260;if(s.MonsterByte242(row.Target,index)<25){return Reject();}}
                    }
                    if(row.Byte36==12){score+=c.Frame100<50?10:c.Frame100<75?5:0;}
                    score+=p.MeleeBonus+(distance<p.MeleeMaxSquared/2?10:0);return Finish(score);
                }
                case 5:case 11:
                    if(distance<p.MeleeMaxSquared/2){score-=10;}
                    if((uint)word48>0){score-=5;}return Finish(score+p.RangedBonus);
                case 7:
                {
                    if(c.Frame100>66)
                    {
                        if(row.Target==0){return Reject();}
                        if(p.MeleeBonus>p.RangedBonus)
                        {if(distance<=p.MeleeMaxSquared/2){return Reject();}}
                        else
                        {
                            if(distance<=p.RangedMaxSquared){return Reject();}
                            var(tx,_)=s.Unit(row.Target).Position.Coordinates();var(px,py)=pet.Position.Coordinates();int delta=(tx-px)/2;
                            // Current code reuses the target X difference for Y.
                            row=row with{Opaque8=XY(px+delta,py+delta)};
                        }
                        return Finish(score+20);
                    }
                    if(c.Owner!=0){row=row with{Target=c.Owner};}
                    else
                    {
                        var(px,_)=pet.Position.Coordinates();int x=px+(int)SourceRandom.Limited(ref random.Seed,40)-20;
                        // The second current read is pet X again,not pet Y.
                        int y=px+(int)SourceRandom.Limited(ref random.Seed,40)-20;row=row with{Target=0,Opaque8=XY(x,y)};
                    }
                    return Finish(score+(c.Frame100>=33?5:15));
                }
                case 8:
                {
                    int value=c.Frame100,recipient=c.Pet;
                    if(value>66)
                    {if(skill==null||(skill.Flags39&4)==0||c.Owner==0){return Reject();}value=c.Frame104;if(value>66){return Reject();}recipient=c.Owner;}
                    row=row with{Target=recipient};return Finish(score+(value<33?40:20));
                }
                case 13:
                    if(s.Collision0(row.Target)){return Reject();}return Finish(score+(distance>p.MeleeMaxSquared?10:0));
                default:return row;
            }
        }
    }
}
