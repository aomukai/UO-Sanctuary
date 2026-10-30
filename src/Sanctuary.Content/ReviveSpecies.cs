// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

public sealed record ReviveSpeciesRule(int Id,string Name,bool Allowed);
public sealed record ReviveLifetimePolicy(int? MaximumAgeFrames,bool RemoveOnReviveDeath,bool RemoveOnNecromancerDeath,bool RemoveOnReplacement);
public static class ReviveSpecies
{
    private static ReviveSpeciesRule[] Rules;
    internal static ReviveLifetimePolicy Lifetime{get;private set;}
    public static void Configure()
    {
        var rows=BalanceCatalog.Read<ReviveSpeciesRule>("revive-species-rules");
        if(rows.Length!=623){throw new InvalidDataException("Incomplete current Revive species rules");}
        for(int i=0;i<rows.Length;i++)
        {if(rows[i] is not{} row||row.Id!=i||string.IsNullOrWhiteSpace(row.Name)){throw new InvalidDataException("Invalid Revive secondary rule");}}
        var policies=BalanceCatalog.Read<ReviveLifetimePolicy>("revive-policy");
        if(policies.Length!=1||policies[0] is not{MaximumAgeFrames:null,RemoveOnReviveDeath:true,RemoveOnNecromancerDeath:true,RemoveOnReplacement:true})
        {throw new InvalidDataException("Revive must retain the approved no-expiration lifetime policy");}
        Rules=(ReviveSpeciesRule[])rows.Clone();Lifetime=policies[0];
    }
    internal static ReviveSpeciesRule Row(int secondary)=>secondary>=0&&secondary<Rules.Length?Rules[secondary]:null;
    // This uses the owned pinned catalog. It does not assign a runtime table
    // selector or replace dead/consumable/revived-AI eligibility checks.
    internal static bool? ForClass(int sourceClass)
    {
        if(GenericPetSecondaryTables.Table.Row(sourceClass) is not{} secondary){return null;}
        var rule=Row(secondary.Id);
        if(rule==null||rule.Name!=secondary.Name){throw new InvalidDataException("Revive rule and secondary metadata disagree");}
        return rule.Allowed;
    }
}
