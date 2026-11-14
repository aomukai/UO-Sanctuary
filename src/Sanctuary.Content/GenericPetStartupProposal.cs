// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Approved normal-demon birth values. Native geometry and automatic AI remain
// separate qualified bindings; no load migration assigns these to old pets.
internal sealed record GenericPetStartupDraft(int SourceClass,int OwnerGame,int OwnerId,int OwnerKind,
    int State,int Function,int Mode10,int Field14,int CommandTarget,int CommandType,ushort Flags38,int TargetId3c,int TargetKind40,int Ai90,
    GenericPetSavedControl Words)
{
    internal const string Policy="proposed-normal-demon-startup-v1";
    internal GenericPetAiControl RuntimeForReview()=>new(State,Function,Field14,false,[]);
    internal GenericPetAiSavedTarget TargetForReview()=>new(TargetId3c,TargetKind40);
    internal GenericPetAiOverrideHeader HeaderForReview()=>new(State,Mode10);
}
internal static class GenericPetStartupProposal
{
    internal static bool Approved=>true;
    internal static GenericPetStartupDraft Draft(int sourceClass,int ownerGame,int ownerId)
    {
        if(ownerGame==0||ownerId<=0){throw new NotSupportedException("Startup draft requires an explicit owner/world");}
        var monster=GenericPetAiTables.Monster(sourceClass);
        if(monster?.Name is not("wargoatman"or"warbighead"or"warputriddefiler"))
        {throw new NotSupportedException("Startup proposal covers only the original three demon species");}
        var descriptor=GenericPetAiTables.Descriptor(new(false,monster.Ai));
        if(descriptor is not{Id:148,Type:0,Think:0x4b60c0}){throw new NotSupportedException("Startup proposal requires the qualified GenericPet default");}
        // Explicit design:normal state/default AI,no startup special override,
        // no early follow target or linked notification,unremembered skill.
        return new(sourceClass,ownerGame,ownerId,0,0,descriptor.Think,0,2,0,0,0,-1,1,0,new(-1,0,0));
    }
    internal static void RequireApproval()
    {if(!Approved){throw new NotSupportedException("Normal demon startup adaptation is not approved");}}
}
