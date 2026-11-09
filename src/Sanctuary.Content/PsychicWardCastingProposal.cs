// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Review uses the shared ordinary SC clock/session and existing do18 release.
// No learned registration or production resolver calls these methods yet.
internal static class PsychicWardCastingProposal
{
    internal static bool Eligible(PlayerMobile owner,CombatAbility ability)=>ability==CombatAbility.PsychicWard
        &&owner is{Alive:true,Deleted:false}&&owner.Map!=null&&owner.Map!=Map.Internal
        &&ClassCombat.Enabled(owner)&&ShapeRestrictions.Allowed(owner,ability)
        &&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Warlock
        &&SkillMechanics.Rank(owner,ability)>0&&SorceressEffects.Shield(owner)==null;
    internal static bool BeginReview(PlayerMobile owner,long now)
        =>Eligible(owner,CombatAbility.PsychicWard)&&SkillCombat.BeginPrepared(owner,CombatAbility.PsychicWard,Serial.Zero,owner.Location,now,
            (p,_,_,_)=>Eligible(p,CombatAbility.PsychicWard),Eligible,WarlockCastClock.Calculate(CombatTiming.Fcr(owner)));
    internal static bool ResolveReview(PlayerMobile owner,long now)
    {
        if(ClassCombat.Existing(owner) is not{PendingAbility:CombatAbility.PsychicWard} action){return false;}
        if(!Eligible(owner,CombatAbility.PsychicWard)||owner.Spell!=null||owner.Frozen||owner.Paralyzed||HitRecovery.Active(owner,now)
            ||owner.Location!=action.ActionOrigin||owner.Map!=action.ActionMap||(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=action.PendingWeapon)
        {action.PendingAbility=CombatAbility.None;return false;}
        if(now<action.EffectAt){return false;}
        action.PendingAbility=CombatAbility.None;
        return PsychicWard.CastPrepared(owner);
    }
}
