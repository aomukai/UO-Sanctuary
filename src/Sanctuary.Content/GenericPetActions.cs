// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// The wrapper changes only target/X/Y; initialization and the remaining native
// request fields belong to the explicit mode service, carried opaquely here.
internal sealed record GenericPetModeRequest(int Target,int X,int Y,object Opaque);
internal sealed record GenericPetActionServices(Action<int,int,int> SetSkill,
    Func<int,GenericPetDecisionUnit> ResolveTarget,Func<int,int,int> Distance,
    Func<int,int,bool> Collision4,Func<int,bool> Collision0,Action ResetMove,
    Func<int,int,byte,int,int,bool> Move,Action MoveFinished,
    Func<byte,byte?> MonsterByte241,Func<int,bool,bool> Approach,
    Func<byte,int,int,int,int,bool,bool> ExecuteSkill,
    Func<int,GenericPetModeRequest> InitializeMode,Func<GenericPetModeRequest,bool> ExecuteMode,
    Func<byte,int,byte?> SkillByte37,Func<int,bool> TargetAccepted,
    Func<byte,int> BaseModeSkill);
internal sealed class GenericPetActionRandom(ulong seed)
{internal ulong Seed=seed;}
internal static class GenericPetActions
{
    internal static int Disposition(GenericPetCombatContext c,GenericPetDecisionFrame f,IList<GenericPetCombatCandidate> rows,int index,GenericPetActionServices s)
    {
        byte type=rows[index].Byte36;
        if(type is not(4 or 5 or 11 or 12)){return 1;}
        int target=rows[index].Target;
        if(target==0)
        {
            if(type is 5 or 11){throw new NotSupportedException("Current ranged descriptor requires a target");}
            return 1;
        }
        var unit=s.ResolveTarget(target)??throw new NotSupportedException("Missing current descriptor target metadata");var(x,y)=unit.Coordinates();int distance=s.Distance(x,y);
        if(type is 5 or 11)
        {
            if(distance<=f.Parameters.RangedMaxSquared&&!s.Collision4(rows[index].Target,4))
            {
                int minimum=f.Parameters.RangedMinSquared;
                if(distance>=minimum){return 1;}
                unit=s.ResolveTarget(rows[index].Target)??throw new NotSupportedException("Missing live ranged target metadata");(x,y)=unit.Coordinates();
                s.ResetMove();
                if(!s.Move(x,y,unchecked((byte)(minimum-distance)),0,4)){return 2;}
                s.MoveFinished();return 0;
            }
        }
        else if(distance<=16&&s.Collision0(rows[index].Target)){return 1;}
        byte? flags=s.MonsterByte241(c.GameTableSelector);
        bool hint=flags.HasValue&&(flags.Value&0x80)!=0&&distance>36;
        return s.Approach(rows[index].Target,hint)?0:2;
    }
    internal static bool Try(GenericPetCombatContext c,GenericPetDecisionFrame f,IList<GenericPetCombatCandidate> rows,int? remembered,int index,GenericPetActionServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(f);ArgumentNullException.ThrowIfNull(rows);ArgumentNullException.ThrowIfNull(s);
        if(rows[index].Byte36==6){s.SetSkill(0,rows[index].SkillId,-1);return false;}
        bool remember=false;int disposition=Disposition(c,f,rows,index,s);
        if(disposition==0){remember=true;}
        else if(disposition==1)
        {
            var row=rows[index];bool accepted;
            if(row.BaseModeFlag==0)
            {
                int x=unchecked((int)row.Opaque8),y=unchecked((int)(row.Opaque8>>32));
                accepted=s.ExecuteSkill(unchecked((byte)row.Opaque28),row.SkillId,row.Target,x,y,true);
            }
            else
            {
                int x=unchecked((int)row.Opaque8),y=unchecked((int)(row.Opaque8>>32));int mode=row.Opaque28;
                var request=s.InitializeMode(mode)??throw new NotSupportedException("Mode initialization did not provide a request");
                request=x>0||y>0?request with{X=x,Y=y}:request with{Target=row.Target};
                accepted=s.ExecuteMode(request);
            }
            // The source requests the skill row even after a failed non-base
            // execution. Base actions do not perform that lookup.
            byte? skillFlags=rows[index].BaseModeFlag==0?s.SkillByte37(c.GameTableSelector,rows[index].SkillId):null;
            if(!accepted){return false;}
            remember=skillFlags.HasValue&&(skillFlags.Value&8)!=0;
        }
        else{return false;}
        int target=rows[index].Target;
        if(target!=0&&s.TargetAccepted(target)&&!(rows[index].Byte36==2&&(f.Parameters.Flags&2)!=0)){f.Memory.Target=rows[index].Target;}
        if(remember)
        {
            f.Memory.Value1=remembered==index?unchecked(rows[index].Counter+1):0;
            if(rows[index].BaseModeFlag!=0)
            {
                if(c.GameTableSelector is <1 or >3){throw new NotSupportedException("Current action base-skill table requires1..3");}
                f.Memory.Value0=s.BaseModeSkill(c.GameTableSelector);
            }
            else{f.Memory.Value0=rows[index].SkillId;}
        }
        return true;
    }
    internal static bool Alternative(IList<GenericPetCombatCandidate> rows,int? remembered,GenericPetActionRandom random,Func<int?,int,bool> act)
    {
        ArgumentNullException.ThrowIfNull(rows);ArgumentNullException.ThrowIfNull(random);ArgumentNullException.ThrowIfNull(act);
        int total=0;int? fallback=null;
        for(int i=0;i<rows.Count;i++)
        {
            int rating=rows[i].Rating;
            if(rating==int.MinValue){continue;}
            if(rating==int.MaxValue){if(act(remembered,i)){return true;}continue;}
            total=unchecked(total+Math.Max(1,rating));rows[i]=rows[i] with{Rating=total};
            if(rows[i].BaseModeFlag!=0||!fallback.HasValue&&rows[i].SkillId==0){fallback=i;}
        }
        if(total>0)
        {
            uint value=SourceRandom.Roll(ref random.Seed);int ticket=(int)(value%(uint)total);
            // The current comparison accepts equality at cumulative boundaries.
            for(int i=0;i<rows.Count;i++)
            {
                int rating=rows[i].Rating;
                if(rating is int.MinValue or int.MaxValue){continue;}
                if(ticket>rating){continue;}
                if(act(remembered,i)){return true;}break;
            }
        }
        return fallback.HasValue&&act(remembered,fallback.Value);
    }
    internal static GenericPetCombatServices Bind(GenericPetCombatServices outer,GenericPetCombatContext c,GenericPetDecisionFrame f,GenericPetActionRandom random,GenericPetActionServices services)
        =>outer with{Act=(rows,old,index)=>Try(c,f,rows,old,index,services),Alternative=(rows,old)=>Alternative(rows,old,random,(remembered,index)=>Try(c,f,rows,remembered,index,services))};
}
