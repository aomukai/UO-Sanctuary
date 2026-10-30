// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record ReviveSourceSkill(byte PetType,int PetTypeCount);
internal sealed record ReviveSourceUnit(int Kind,int ClassId,int Id,int Level,int MaximumLife,int Room,int X,int Y,int Pattern,int Size,bool RightSkill,int NodeIndex);
internal readonly record struct ReviveSourceAction(int Kind,int Unit,int A=0,int B=0,int C=0,int D=0);
internal abstract class ReviveSourceServices
{
    internal abstract ReviveSourceSkill Skill(int id);
    internal abstract int Target();
    internal abstract ReviveSourceUnit Unit(int handle);
    internal abstract bool? Species(int cls);
    internal abstract bool Dead(int target);
    internal abstract bool Consumable(int target);
    internal abstract int RevivedAi(int target);
    internal abstract int FreeCoordinates(int target,SourceGridPoint point);
    internal abstract (int Minimum,int Maximum) BaseLife(int target);
    internal abstract int Roll(int target,int bound);
    internal abstract int PetMaximum(int caster,int skill,int rank);
    internal abstract int Duration(int caster,int skill,int rank);
    internal abstract int Frame{get;}
    internal abstract void Act(ReviveSourceAction action);
    internal abstract void MarkRevived(int target);
}

// Complete older start21/release58/owner sequencing. Mandatory services own
// geometry,mode entry,stats/passives,AI,registration and original-unit lifetime.
// This is not a factory for a replacement skeleton or a learned spell route.
internal static class ReviveSourcePolicy
{
    // Trace/action kinds6..24 are shared with the independent source harness.
    internal const int SelfEvents=6,SelfDamage=7,ResetCollision=8,Flag=9,Callback=10,Neutral=11,ResetSpecial=12,
        DeleteEvent=13,RightAttack=14,Move=15,SetStat=16,Passives=17,OwnerData=18,OwnerRoute=19,
        Event=20,AiFlag=21,OwnerRefresh=22,State=23,UniqueMod=24,Register=25,UpdateAi=26,Finalize=27;
    private static bool Eligible(int target,ReviveSourceServices s)
    {
        if(target==0||s.Unit(target).Kind!=1){return false;}
        return s.Species(s.Unit(target).ClassId)==true&&s.Dead(target)&&s.Consumable(target)&&s.RevivedAi(target)!=0;
    }
    internal static bool Start(ReviveSourceServices s)
    {ArgumentNullException.ThrowIfNull(s);return Eligible(s.Target(),s);}
    internal static bool Release(int caster,int skill,int rank,ReviveSourceServices s)
    {
        if(skill!=95){throw new NotSupportedException("Unsupported skill routed to learned Revive policy");}
        if(ReviveSpecies.Lifetime is not{MaximumAgeFrames:null}){throw new NotSupportedException("Approved no-expiration policy is required");}
        return ReleaseCore(caster,skill,rank,s,false);
    }
    // Explicit reference lane for differential tests,never a learned route.
    internal static bool ReferenceRelease(int caster,int skill,int rank,ReviveSourceServices s)=>ReleaseCore(caster,skill,rank,s,true);
    private static bool ReleaseCore(int caster,int skill,int rank,ReviveSourceServices s,bool sourceExpiry)
    {
        ArgumentNullException.ThrowIfNull(s);var row=s.Skill(skill);if(row==null){return false;}
        int type=row.PetType;if(type>=row.PetTypeCount){type=0;}
        int target=s.Target();if(!Eligible(target,s)){return false;}
        // The complete source later dereferences the caster's node index.
        // A null caster is outside this valid-call composition.
        if(caster==0){throw new NotSupportedException("Revive release requires an actual caster");}
        var owner=s.Unit(caster);
        if(owner.Kind==0&&owner.ClassId==3)
        {
            int damage=owner.MaximumLife/8;
            s.Act(new(SelfEvents,caster,4,0x1000,damage,damage));s.Act(new(SelfDamage,caster,4,0x1000,damage,damage));
        }
        var u=s.Unit(target);var point=new SourceGridPoint(u.X,u.Y);
        s.Act(new(ResetCollision,target,u.Room,u.X,u.Y,u.Pattern));
        int room=s.FreeCoordinates(target,point);
        if(room!=0)
        {
            foreach(int flag in new[]{4,8,2,0x04000000,0x20000}){s.Act(new(Flag,target,flag,1));}
            s.Act(new(Callback,target));s.Act(new(Neutral,target,1,1));s.Act(new(ResetSpecial,target));
            s.Act(new(DeleteEvent,target,8));if(s.Unit(target).RightSkill){s.Act(new(RightAttack,target,0,-1));}
            s.Act(new(Move,target,room,point.X,point.Y));
        }
        var hp=s.BaseLife(target);int life=unchecked((s.Roll(target,unchecked(hp.Maximum-hp.Minimum+1))+hp.Minimum)<<8);
        s.Act(new(SetStat,target,7,life));s.Act(new(SetStat,target,6,life));
        int casterLevel=s.Unit(caster).Level,targetLevel=s.Unit(target).Level;
        if(targetLevel!=0&&casterLevel<targetLevel)
        {
            int scaled=Math.Max(1,SourcePercentage.Calculate(life,casterLevel,targetLevel));
            s.Act(new(SetStat,target,7,scaled));s.Act(new(SetStat,target,6,scaled));s.Act(new(SetStat,target,12,casterLevel));
        }
        s.Act(new(Passives,target,skill,rank,0));Owner(caster,target,skill,rank,s,sourceExpiry);
        s.Act(new(Register,target,caster,type,s.PetMaximum(caster,skill,rank)));
        s.Act(new(UpdateAi,target,s.Unit(caster).NodeIndex));s.Act(new(Finalize,target));return true;
    }
    internal static void ReferenceOwner(int caster,int target,int skill,int rank,ReviveSourceServices s)=>Owner(caster,target,skill,rank,s,true);
    private static void Owner(int caster,int target,int skill,int rank,ReviveSourceServices s,bool sourceExpiry)
    {
        var row=s.Skill(skill);if(skill==0){return;}
        if(row==null){throw new NotSupportedException("Owner conversion dereferences missing skill metadata");}
        int kind=6,id=-1;if(caster!=0){var owner=s.Unit(caster);kind=owner.Kind;id=owner.Id;}
        s.Act(new(OwnerData,target,id,kind));s.Act(new(OwnerRoute,target,caster));s.Act(new(DeleteEvent,target,2));
        s.Act(new(Event,target,2,unchecked(s.Frame+15)));s.Act(new(AiFlag,target,2,0));s.Act(new(OwnerRefresh,target));
        if(target!=0){s.MarkRevived(target);}s.Act(new(State,target,96,1));
        int duration=s.Duration(caster,skill,rank);
        if(sourceExpiry&&duration>0)
        {s.Act(new(UniqueMod,target,21,0));s.Act(new(Event,target,7,unchecked(duration+s.Frame)));}
    }
}
