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
    internal static ReviveAiStatus NativeStatus(BaseCreature actor,out string issue)
    {
        issue=actor.IsParagon?"Paragon Revive classification is not configured."
            :actor is BaseHire?"Hirelings cannot be revived."
            :PaladinConversion.SourceRule(actor)?.Base=="imp1"?"Attached-Imp eligibility has no native adapter."
            :null;
        // These are native actor facts, not inferred from body, hue or age.
        // Unclassified native elite/attachment systems refuse above.
        return new(actor.CanBeDamaged(),MonsterUninterruptibles.Active(actor),
            actor is EncounterCreature{Grade:EncounterGrade.Unique},
            actor is EncounterCreature{Grade:EncounterGrade.SuperUnique},false,true);
    }
    internal static bool Plan(PlayerMobile owner,Corpse corpse,out ReviveNativeSetup setup,out ReviveAiStatus status,out string issue)
    {
        setup=null;status=default;issue="That corpse is unavailable for Revive.";
        if(!CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)){return false;}
        try
        {
            var unit=SourceCorpseIdentity.For(corpse);
            var saved=ReviveOriginals.State(corpse);var actor=ReviveOriginals.Find(corpse);
            if(actor==null||saved?.BeforeDeathJson==null)
            {issue=saved?.BeforeDeathIssue??"The original monster has no retained pre-death state.";return false;}
            var captured=ReviveBeforeDeath.Decode(saved.BeforeDeathJson);
            if(captured.EligibilityIssue!=null){issue=captured.EligibilityIssue;return false;}
            if(captured.AiStatus is not{} facts){issue="This older corpse has no captured Revive eligibility.";return false;}
            status=facts;
            if(!unit.SpeciesAllowsRevive||!unit.AllowsRevivedAi(status))
            {issue="This monster is not eligible for Revive.";return false;}
            // ApplyStats=false can deliberately retain a known native or
            // project combat implementation. It is not an ability whitelist.
            if(!CreatureProfiles.Variant(actor).Enabled)
            {issue="This monster's combat classification is still a disabled proposal.";return false;}
            if(ReviveBeforeDeath.Equipment(captured).Length!=0)
            {issue="Preserving this monster's equipped combat effects is not implemented yet; its gear remains lootable.";return false;}
            if(actor.Guild!=null||actor.Spawner!=null)
            {issue="This monster still has unsupported guild or spawner ownership.";return false;}
            if(!owner.Map.CanSpawnMobile(corpse.Location))
            {issue="There is no safe space to revive this monster at its corpse.";return false;}
            // D2's release does not reset mana/stamina. Native death clears
            // them, so restore the resources captured before that cleanup.
            setup=ReviveLife.Setup(owner,corpse,captured.Mana,captured.Stamina);
            issue=null;return true;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException or ArgumentException)
        {issue=e.Message;return false;}
    }
    internal static bool Release(PlayerMobile owner,Corpse corpse)
    {
        if(!Plan(owner,corpse,out var setup,out var status,out var issue))
        {owner.SendMessage(0x22,issue);return false;}
        int mana=SkillMechanics.Values(owner,CombatAbility.Revive).Mana256;
        if(!NecromancerEffects.CanPay(owner,mana)){return false;}
        if(!ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,status,setup,out var actor,out issue))
        {owner.SendMessage(0x22,issue);return false;}
        if(!NecromancerEffects.Pay(owner,mana))
        {throw new InvalidOperationException("Revive mana changed during original-actor activation");}
        actor.FixedParticles(0x376A,9,20,5024,EffectLayer.Waist);
        owner.PlaySound(0x1E5); // Identified native summon/cast placeholders.
        return true;
    }
}
