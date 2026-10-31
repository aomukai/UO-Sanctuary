// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Shared pettype=golem roster. Storage/stat-body factories are deliberately
// outside this roster until a successful creation transaction commits them.
internal static class NecromancerGolems
{
    internal static bool Ability(CombatAbility ability)=>ability is CombatAbility.ClayGolem or CombatAbility.BloodGolem or CombatAbility.IronGolem or CombatAbility.FireGolem;
    internal static bool Member(SkeletonGuardian pet)=>pet is IronGolemBody{Prepared:{RosterMember:true}}
        ||pet is NecromancerMinion minion&&Ability(minion.Kind);
    internal static SkeletonGuardian[] For(PlayerMobile owner)=>owner==null?[]:(owner.AllFollowers??[]).OfType<SkeletonGuardian>()
        .Where(p=>p is{Alive:true,Deleted:false,IsDeadBondedPet:false}&&p.ControlMaster==owner&&Member(p)).ToArray();
    internal static int Limit(PlayerMobile owner)
    {
        foreach(var ability in new[]{CombatAbility.ClayGolem,CombatAbility.BloodGolem,CombatAbility.IronGolem,CombatAbility.FireGolem})
        {
            var row=SkillMechanics.For(ability);
            if(row.Text("pettype")!="golem"||SkillMechanics.Values(owner,ability,1).Eval("petmax")!=1)
            {throw new NotSupportedException("Unmapped Necromancer shared golem cap");}
        }
        return 1;
    }
    private static void Remove(SkeletonGuardian pet,bool replacement)
    {
        // Retain the existing Fire Golem replacement hook. Ordinary cap
        // refresh retains its prior delete-only behavior; event parity is separate.
        if(replacement&&pet is NecromancerMinion minion){minion.Explode();}
        pet.Delete(); // IronGolemBody consumes its bound original here.
    }
    internal static void Commit(PlayerMobile owner,SkeletonGuardian created)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Necromancer
            ||created is not{Alive:true,Deleted:false}||created.ControlMaster!=owner||created.Map==null||created.Map==Map.Internal||created.Map!=owner.Map
            ||created is not IronGolemBody{Prepared:not null}&&!Member(created))
        {throw new ArgumentException("Golem roster commit requires a completed owned world candidate");}
        Limit(owner); // Resolve the supported cap before any roster mutation.
        if(created is IronGolemBody iron){iron.JoinGolemRoster();}
        BattleCommand.Track(created);
        foreach(var old in For(owner).Where(p=>p!=created).OrderBy(BattleCommand.PetOrder).ThenBy(p=>p.Serial.Value).ToArray())
        {Remove(old,true);}
    }
    internal static void Enforce(PlayerMobile owner)
    {
        if(owner==null||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Necromancer){return;}
        var ordered=For(owner).OrderBy(BattleCommand.PetOrder).ThenBy(p=>p.Serial.Value).ToArray();
        // Historical untracked entries have order0: their original FIFO is
        // unknown,so deterministic serial order precedes recorded new births.
        int remove=Math.Max(0,ordered.Length-Limit(owner));
        foreach(var old in ordered.Take(remove)){Remove(old,false);}
    }
}
