// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// Forty source bytes. Opaque fields retain their position/value without assigning
// undocumented meanings; Target is a detached identity supplied by the adapter.
internal readonly record struct GenericPetCombatCandidate(int Target,long Opaque8,int Rating,int SkillId,int Opaque24,int Opaque28,int Counter,
    byte Byte36,byte BaseModeFlag,byte Byte38,byte Byte39);
internal sealed class GenericPetCombatSelection
{
    internal readonly int[] Targets;
    internal long Word40,Word48;
    internal GenericPetCombatSelection(int[] targets,long word40,long word48)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if(targets.Length!=5){throw new ArgumentException("Current search returns five target slots",nameof(targets));}
        Targets=(int[])targets.Clone();Word40=word40;Word48=word48;
    }
    internal bool Contains(int target)=>Array.IndexOf(Targets,target)>=0;
}
internal sealed record GenericPetCombatContext(byte GameTableSelector,GenericPetDecisionUnit Pet);
// Providers explicitly bind the caller's game/pet/owner. These are unresolved
// current helpers,not implementations of targeting,movement,rating or attacks.
internal sealed record GenericPetCombatServices(Func<List<GenericPetCombatCandidate>> Gather,Func<byte,int> BaseModeSkill,
    Func<IReadOnlyList<GenericPetCombatCandidate>,int> Range,
    Func<IReadOnlyList<GenericPetCombatCandidate>,int,int,GenericPetCombatSelection> Search,
    Func<GenericPetCombatCandidate,GenericPetCombatSelection,GenericPetCombatCandidate> RateRemembered,
    Func<int> UnitProperty,Func<int> Room,Func<int,int,int,int,int,bool> Blocked,
    Action ResetForMove,Func<int,int,int,int,int,bool> Move,Action MoveFinished,
    Action<IList<GenericPetCombatCandidate>> Prepare,
    Func<GenericPetCombatCandidate,GenericPetCombatSelection,GenericPetCombatCandidate> Rate,
    Action<IList<GenericPetCombatCandidate>> Order,
    Func<IList<GenericPetCombatCandidate>,int?,int,bool> Act,
    Func<IList<GenericPetCombatCandidate>,int?,bool> Alternative);
internal static class GenericPetCombat
{
    internal static bool Try(GenericPetCombatContext c,GenericPetDecisionFrame f,GenericPetCombatServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(c.Pet);ArgumentNullException.ThrowIfNull(f);ArgumentNullException.ThrowIfNull(f.Memory);ArgumentNullException.ThrowIfNull(f.Parameters);ArgumentNullException.ThrowIfNull(s);
        var rows=s.Gather();ArgumentNullException.ThrowIfNull(rows);var memory=f.Memory;
        int oldTarget=memory.Target;int? remembered=null;
        if(memory.Value0>-1&&memory.Value1<25)
        {
            // The current body enters a fatal engine path for other selectors.
            // Detached callers receive an explicit unsupported-context error.
            if(c.GameTableSelector is <1 or >3){throw new NotSupportedException("Current GenericPet skill-table selector requires1..3");}
            int baseSkill=s.BaseModeSkill(c.GameTableSelector);
            for(int i=0;i<rows.Count;i++)
            {
                bool match=memory.Value0==baseSkill?rows[i].BaseModeFlag!=0:rows[i].SkillId==memory.Value0;
                if(!match){continue;}
                remembered=i;rows[i]=rows[i] with{Counter=memory.Value1};break;
            }
        }
        memory.Target=0;memory.Value0=-1;memory.Value1=0;
        if(oldTarget!=0)
        {
            IReadOnlyList<GenericPetCombatCandidate> subset=remembered.HasValue?new[]{rows[remembered.Value]}:rows;
            int range=s.Range(subset);
            if(range!=0)
            {
                memory.Value2=unchecked(memory.Value2+1);int bias=oldTarget;
                if(memory.Value2>5){memory.Value2=0;bias=0;}
                if(!s.Search(subset,range,bias).Contains(oldTarget)){oldTarget=0;remembered=null;}
            }
            else{oldTarget=0;remembered=null;}
        }
        if(oldTarget!=0)
        {
            if(remembered.HasValue)
            {
                int i=remembered.Value;rows[i]=rows[i] with{Target=oldTarget,Rating=int.MaxValue};
            }
            else
            {
                // The second search supplies opaque rating context; it does not
                // replace the already-accepted old target or request a new range.
                var selection=s.Search(rows,0,0);
                for(int i=0;i<rows.Count;i++){rows[i]=s.RateRemembered(rows[i] with{Target=oldTarget},selection);}
            }
        }
        else
        {
            remembered=null;int property=s.UnitProperty();var(x,y)=c.Pet.Coordinates();int room=s.Room();
            if(s.Blocked(room,x,y,property,64))
            {
                s.ResetForMove();
                if(s.Move(x,y,5,0,4)){s.MoveFinished();return true;}
            }
            int range=s.Range(rows);var selection=s.Search(rows,range,0);s.Prepare(rows);
            for(int i=0;i<rows.Count;i++){rows[i]=s.Rate(rows[i],selection);}
        }
        if(f.Parameters.ChooseType==1){return s.Alternative(rows,remembered);}
        if(f.Parameters.ChooseType!=0){return false;}
        s.Order(rows);
        for(int i=0;i<rows.Count;i++)
        {
            if(rows[i].Rating==int.MinValue){break;}
            // Remembered is a source vector slot. Ordering may change its
            // contents;do not capture a detached pre-order candidate object.
            if(s.Act(rows,remembered,i)){return true;}
        }
        return false;
    }
}
