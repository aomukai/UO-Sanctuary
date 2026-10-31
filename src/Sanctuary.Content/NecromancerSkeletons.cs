// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Source pet lists append successful births and retire their oldest entries.
// Reuse the saved order ledger for both creation and cap refresh; historical
// entries without an order sort first,then by serial (original FIFO unknown).
internal static class NecromancerSkeletons
{
    internal static CombatAbility Kind(SkeletonGuardian pet)=>pet is RaisedSkeleton||pet.GetType()==typeof(SkeletonGuardian)?CombatAbility.RaiseSkeleton
        :pet is NecromancerMinion{IsMage:true}?CombatAbility.RaiseSkeletalMage:CombatAbility.None;
    internal static SkeletonGuardian[] For(PlayerMobile owner,CombatAbility kind)=>(owner.AllFollowers??[]).OfType<SkeletonGuardian>()
        .Where(p=>p is{Alive:true,Deleted:false,IsDeadBondedPet:false}&&p.ControlMaster==owner&&Kind(p)==kind)
        .OrderBy(BattleCommand.PetOrder).ThenBy(p=>p.Serial.Value).ToArray();
    internal static int Limit(PlayerMobile owner,CombatAbility kind,int? rank=null)
    {
        var row=SkillMechanics.For(kind);
        if(kind is not(CombatAbility.RaiseSkeleton or CombatAbility.RaiseSkeletalMage)
            ||row.Text("pettype")!=(kind==CombatAbility.RaiseSkeleton?"skeleton":"skeletonmage"))
        {throw new NotSupportedException("Unmapped skeleton summon family");}
        return Math.Max(1,checked((int)SkillMechanics.Values(owner,kind,rank).Eval("petmax")));
    }
    private static void Prune(PlayerMobile owner,CombatAbility kind,int maximum)
    {
        var ordered=For(owner,kind);
        foreach(var old in ordered.Take(Math.Max(0,ordered.Length-maximum))){old.Delete();}
    }
    internal static void Commit(PlayerMobile owner,SkeletonGuardian created)
    {
        var kind=Kind(created);
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Necromancer
            ||created is not{Alive:true,Deleted:false,IsDeadBondedPet:false}||created.ControlMaster!=owner||created.Rank<1
            ||created.Map==null||created.Map==Map.Internal||created.Map!=owner.Map||kind==CombatAbility.None)
        {throw new ArgumentException("Skeleton roster commit requires a completed owned world candidate");}
        int maximum=Limit(owner,kind,created.Rank);BattleCommand.Track(created);Prune(owner,kind,maximum);
    }
    internal static void Enforce(PlayerMobile owner)
    {
        if(owner==null||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Necromancer){return;}
        foreach(var kind in new[]{CombatAbility.RaiseSkeleton,CombatAbility.RaiseSkeletalMage}){Prune(owner,kind,Limit(owner,kind));}
    }
}
