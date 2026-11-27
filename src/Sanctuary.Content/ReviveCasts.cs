// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// Learned casting composes the retained original, not a replacement NPC or
// an independent spell repertoire. World spell conversion is a separate task.
internal static class ReviveCasts
{
    internal static bool RegularMonster(BaseCreature actor)
        => actor is not (BaseVendor or BaseHire or BaseChampion or Harrower or DemonKnight or ConversationNpc or IClassCompanion
            or GraveyardUndead{Raised:true} or EncounterCreature{Grade:EncounterGrade.Champion or EncounterGrade.SuperUnique})
            &&!actor.IsParagon&&CreatureProfiles.For(actor).Traits.Boss!=true&&PaladinConversion.SourceRule(actor)?.Boss!=true;

    internal static ReviveAiStatus NativeStatus(BaseCreature actor,out string issue)
    {
        issue=NamedCombat.NativeQualityIssue(actor,"Revive")??(actor is BaseHire?"Hirelings cannot be revived."
            :actor is EncounterCreature{Grade:EncounterGrade.Champion}?"Paragon corpses are unavailable for Revive."
            :PaladinConversion.SourceRule(actor)?.Base=="imp1"?"Attached-Imp eligibility has no native adapter."
            :null);
        // These are native actor facts, not inferred from body, hue or age.
        // Unclassified native elite/attachment systems refuse above.
        return new(actor.CanBeDamaged(),MonsterUninterruptibles.Active(actor),
            actor is EncounterCreature{Grade:EncounterGrade.Unique},
            actor is EncounterCreature{Grade:EncounterGrade.SuperUnique},false,true);
    }
    internal static bool Plan(PlayerMobile owner,Corpse corpse,out ReviveNativeSetup setup,out ReviveAiStatus status,out string issue,bool preparedNative=false)
    {
        setup=null;status=default;issue="That corpse is unavailable for Revive.";
        if(!CorpseSkillRules.UsableForRevive(owner,corpse,SkeletonPets.Range))
        {
            if(owner is{Alive:true,Deleted:false,Map:not null}&&owner.Map!=Map.Internal
                &&corpse is{Deleted:false,Parent:null}&&corpse.Map==owner.Map&&owner.CanSee(corpse)&&owner.InLOS(corpse))
            {
                if(owner.GetDistanceToSqrt(corpse)>SkeletonPets.Range||Math.Abs(corpse.Z-owner.Z)>=16)
                {issue="Move closer to the corpse to cast Revive.";}
                else if(CorpseSkillRules.Shattered(corpse))
                {issue="Cold damage shattered this corpse. It cannot be revived.";}
            }
            return false;
        }
        if(corpse.Owner is BaseCreature source&&!RegularMonster(source))
        {issue="Bosses and paragons cannot be revived.";return false;}
        try
        {
            var saved=ReviveOriginals.State(corpse);var actor=ReviveOriginals.Find(corpse);
            if(actor==null||saved?.BeforeDeathJson==null)
            {issue=saved?.BeforeDeathIssue??"The original monster has no retained pre-death state.";return false;}
            var captured=ReviveBeforeDeath.Decode(saved.BeforeDeathJson);
            if(captured.AiStatus is not{} facts){issue="This older corpse has no captured Revive eligibility.";return false;}
            status=facts;
            if(actor.Guild!=null||actor.Spawner!=null)
            {issue="This monster still has unsupported guild or spawner ownership.";return false;}
            if(!owner.Map.CanSpawnMobile(corpse.Location))
            {issue="There is no safe space to revive this monster at its corpse.";return false;}
            setup=SourceSetup(owner,corpse,actor,saved,captured,status,preparedNative);
            int rank=SkillMechanics.Rank(owner,CombatAbility.Revive);
            if(rank<=0){issue="You have not learned Revive.";return false;}
            int life=Math.Clamp(captured.MaximumHits,1,65000);
            setup??=new(rank,life,life,Math.Min(captured.Mana,actor.ManaMax),Math.Min(captured.Stamina,actor.StamMax),NativeCombat:true);
            issue=null;return true;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException or ArgumentException)
        {setup=null;issue=e.Message;return false;}
    }
    private static ReviveNativeSetup SourceSetup(PlayerMobile owner,Corpse corpse,BaseCreature actor,ReviveOriginalState saved,
        ReviveBeforeDeathState captured,ReviveAiStatus status,bool preparedNative)
    {
        if(saved.NativeOnly||captured.EligibilityIssue!=null||!CreatureProfiles.Variant(actor).Enabled){return null;}
        if(NativeCorpseSources.ForSource(CorpseSkillRules.Profiles.Find(corpse)?.Source)is{} native
            &&(native.ReviveAiSource==null||!NativeReviveRules.Approved&&!preparedNative)){return null;}
        try
        {
            var unit=SourceCorpseIdentity.For(corpse);
            if(!unit.SpeciesAllowsRevive||!unit.AllowsRevivedAi(status)){return null;}
            var setup=ReviveLife.Setup(owner,corpse,captured.Mana,captured.Stamina) with{RegenerationRate=ReviveRegeneration.Rate(unit.Snapshot.Source)};
            if(ReviveBeforeDeath.Equipment(captured).Length==0){return setup;}
            setup=ReviveSwings.Prepare(setup,saved);
            ReviveEquipmentDefenses.RequireBody(actor);ReviveEquipmentIncoming.RequireBody(actor);
            _=ReviveEquipmentOffense.Preflight(actor,owner,setup,saved);
            return setup;
        }
        catch(Exception e)when(e is NotSupportedException or OverflowException or ArgumentException)
        {return null;}
    }
    internal static bool Release(PlayerMobile owner,Corpse corpse,bool preparedNative=false)
    {
        if(!Plan(owner,corpse,out var setup,out var status,out var issue,preparedNative))
        {owner.SendMessage(0x22,issue);return false;}
        int mana=SkillMechanics.Values(owner,CombatAbility.Revive).Mana256;
        if(!NecromancerEffects.CanPay(owner,mana)){return false;}
        if(!ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,status,setup,out var actor,out issue))
        {owner.SendMessage(0x22,issue);return false;}
        if(!NecromancerEffects.Pay(owner,mana))
        {throw new InvalidOperationException("Revive mana changed during original-actor activation");}
        UndeadSummonVisuals.Arrive(actor);
        return true;
    }
}
