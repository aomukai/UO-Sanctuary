// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record SourceCorpseUnit(int Id,int ClassId,Corpse Body,CorpseSkillRecord Snapshot)
{
    internal bool SpeciesAllowsRevive=>ReviveSpecies.ForClass(ClassId)??throw new NotSupportedException("Corpse species has no verified Revive rule");
    internal ReviveCreatureSnapshot OriginalCreature=>Snapshot.ReviveCreature??throw new NotSupportedException(Snapshot.ReviveSnapshotIssue??"Legacy corpse has no saved original creature descriptor");
    internal bool AllowsRevivedAi(ReviveAiStatus status)=>SourceAiEligibility.Corpse(this,status);
}
// Identity is independent of corpse usability. Reservation,shattering and RIP
// belong to the state/eligibility producer; none changes the original GUID.
internal static class SourceCorpseIdentity
{
    internal static SourceCorpseUnit For(Corpse corpse)
    {
        if(corpse is not{Deleted:false,OwnerWasBaseCreature:true})
        {throw new NotSupportedException("No live native monster corpse identity");}
        var row=CorpseSkillRules.Profiles.Find(corpse);
        if(row?.BaseLife is not{} life||!((Serial)life.OriginalMonsterSerial).IsMobile||life.OriginalMonsterSerial==0
            ||GenericPetSearchTables.SourceId(row.Source) is not{} cls)
        {throw new NotSupportedException("Historical monster identity/species is missing or unmapped");}
        if(corpse.Owner is{} owner&&owner.Serial.Value!=life.OriginalMonsterSerial)
        {throw new NotSupportedException("Native corpse owner conflicts with historical identity");}
        var original=World.FindMobile((Serial)life.OriginalMonsterSerial);
        if(original is PlayerMobile or BaseCreature{Alive:true,Deleted:false})
        {throw new NotSupportedException("Historical corpse identity collides with a live actor");}
        return new(checked((int)life.OriginalMonsterSerial),cls,corpse,row);
    }
    internal static SourceCorpseUnit Find(int original)
    {
        if(original<=0){return null;}SourceCorpseUnit found=null;
        foreach(var row in CorpseSkillRules.Profiles.ForOriginal((uint)original))
        {
            if(World.FindItem((Serial)row.Corpse) is not Corpse{Deleted:false} corpse){continue;}
            var current=For(corpse);
            if(current.Id!=original){throw new NotSupportedException("Corpse identity index disagrees with its saved row");}
            if(found!=null){throw new NotSupportedException("Multiple live native corpses share one source monster identity");}
            found=current;
        }
        return found;
    }
}
