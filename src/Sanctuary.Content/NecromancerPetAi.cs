// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct NecromancerPetAiState(int Parameter0,int Parameter1,int RememberedOwner);
internal readonly record struct NecromancerPetAiTarget(int Id,int Distance,bool Combat);
internal enum NecromancerPetAiKind
{
    DeleteThink,Owner,LookupPlayer,Distance,OwnerTarget,Primary,PetIgnore,MissileBarrier,ValidTarget,
    Percentage,CommonMove,Town,Idle,PetMove,Wander,Attack,Velocity,Approach,Skill,Reposition
}
internal readonly record struct NecromancerPetAiRequest(NecromancerPetAiKind Kind,int A=0,int B=0,int C=0,int D=0);
internal sealed record NecromancerPetAiContext(NecromancerPetAiState State,int Velocity,int Run,int SkillMode,int Skill);
internal sealed record NecromancerPetAiServices(Action<NecromancerPetAiRequest> Request,Func<int> Owner,
    Func<int,int> LookupPlayer,Func<int,int> Distance,Func<int,NecromancerPetAiTarget> OwnerTarget,
    Func<bool,int,NecromancerPetAiTarget> Primary,Func<int,bool> PetIgnore,Func<int,bool> MissileBarrier,
    Func<int,bool> ValidTarget,Func<int> Percentage,Func<int,bool,int,int,bool> CommonMove,
    Func<bool> Town,Func<int,bool,int,int,bool> PetMove);

// Complete pinned older NecroPet AI67, including its ordinary-melee helper.
// Query services deliberately stay ordered: the primary lookup's combat flag
// survives replacement by an owner target. No native AI, target lookup, seed,
// action clock or current-executable equivalence is implied by this policy.
internal static class NecromancerPetAi
{
    internal static NecromancerPetAiState Think(NecromancerPetAiContext c,NecromancerPetAiServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        if(s.Request==null||s.Owner==null||s.LookupPlayer==null||s.Distance==null
            ||s.OwnerTarget==null||s.Primary==null||s.PetIgnore==null||s.MissileBarrier==null||s.ValidTarget==null
            ||s.Percentage==null||s.CommonMove==null||s.Town==null||s.PetMove==null)
        {throw new ArgumentException("Unsupported Necromancer pet AI context/services");}
        void A(NecromancerPetAiKind kind,int a=0,int b=0,int d=0,int e=0)=>s.Request(new(kind,a,b,d,e));
        static int Id(int id)=>id>=0?id:throw new NotSupportedException("Pet AI query returned a negative identity");
        static NecromancerPetAiTarget Target(NecromancerPetAiTarget target)
        {
            Id(target.Id);
            if(target.Distance<0){throw new NotSupportedException("Pet AI query returned a negative distance");}
            return target;
        }
        int Distance(int target)
        {
            int value=s.Distance(target);
            return value>=0?value:throw new NotSupportedException("Pet AI query returned a negative distance");
        }
        int Roll()
        {
            int roll=s.Percentage();
            return roll is >=0 and <100?roll:throw new NotSupportedException("Pet AI percentage must be0..99");
        }
        A(NecromancerPetAiKind.DeleteThink);
        int owner=Id(s.Owner());
        if(owner==0)
        {
            if(c.State.RememberedOwner!=0&&Id(s.LookupPlayer(c.State.RememberedOwner))!=0)
            {s.PetMove(3,false,0,0);}
            else{A(NecromancerPetAiKind.Idle,10);}
            return c.State;
        }
        var state=c.State with{RememberedOwner=owner};
        bool ranged=c.State.Parameter0!=0;
        int ownerDistance=Distance(owner);
        if(!ranged)
        {
            if(ownerDistance>50){s.PetMove(3,false,0,0);return state;}
            int velocity=c.Velocity<=0?100:Math.Min(checked(100*c.Run)/c.Velocity-100,100);
            if(ownerDistance>28){s.PetMove(0,false,velocity,0);return state;}
        }
        var potential=Target(s.OwnerTarget(owner));
        int candidate=potential.Id;
        if(candidate!=0&&s.PetIgnore(candidate)){candidate=0;}
        var primary=Target(s.Primary(ranged,ranged?0:24));
        int target=primary.Id;
        if(target==0||primary.Distance>(ranged?15:6))
        {
            target=0;
            if(candidate!=0&&(!ranged||!s.MissileBarrier(candidate))&&Distance(candidate)<(ranged?20:36))
            {target=candidate;}
        }
        if(!ranged&&!s.ValidTarget(target)){target=0;}
        bool closer=Roll()<15;
        if(s.CommonMove(target,primary.Combat,closer?1:0,closer?8:7)){return state;}
        if(target==0||s.Town())
        {A(NecromancerPetAiKind.Wander,owner,4);return state;}
        if(ranged)
        {
            if(Roll()<80){A(NecromancerPetAiKind.Skill,c.SkillMode,c.Skill,target);}
            else if(Roll()>=75){A(NecromancerPetAiKind.Reposition,target,3);}
            else{A(NecromancerPetAiKind.Idle,10);}
        }
        else if(primary.Combat)
        {
            if(Roll()<80){A(NecromancerPetAiKind.Attack,4,target);}
            else{A(NecromancerPetAiKind.Idle,10);}
        }
        else
        {A(NecromancerPetAiKind.Velocity,0,0,12);A(NecromancerPetAiKind.Approach,target,7);}
        return state;
    }
}
