// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NecromancerArmyStep(int MinimumRank,int Mages,int Revives);
internal sealed record NecromancerArmyPolicy(string ReferenceVersion,NecromancerArmyStep[] Steps);

// Approved Sanctuary roster policy. Counts use current effective rank;
// inherited combat stats and hard-point abilities retain their birth snapshot.
internal static class NecromancerArmyLimits
{
    private static readonly Lazy<NecromancerArmyStep[]> Steps=new(()=>
    {
        var policy=BalanceCatalog.ReadObject<NecromancerArmyPolicy>("necromancer-army-limits");
        if(policy is not{ReferenceVersion:"3.3.93847",Steps.Length:7}
            ||!policy.Steps.Select(s=>(s.MinimumRank,s.Mages,s.Revives)).SequenceEqual(new[]{(1,1,1),(2,2,1),(3,3,1),(10,4,2),(20,5,3),(30,6,4),(40,7,5)}))
        {throw new InvalidDataException("Invalid approved Necromancer army progression");}
        return policy.Steps;
    });
    internal static int ForRank(CombatAbility kind,int rank)
    {
        if(kind is not(CombatAbility.RaiseSkeletalMage or CombatAbility.Revive)){throw new ArgumentOutOfRangeException(nameof(kind));}
        if(rank<=0){return 0;}
        var step=Steps.Value.Last(s=>s.MinimumRank<=rank);
        return kind==CombatAbility.RaiseSkeletalMage?step.Mages:step.Revives;
    }
    internal static int For(PlayerMobile owner,CombatAbility kind)=>ForRank(kind,SkillMechanics.Rank(owner,kind));
    internal static void Enforce(PlayerMobile owner)
    {
        if(World.Loading||owner is not{Alive:true,Deleted:false}
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Necromancer){return;}
        NecromancerSkeletons.EnforceMages(owner);ReviveNativeActivation.Enforce(owner);
    }
}
