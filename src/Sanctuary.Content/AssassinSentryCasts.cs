// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Learned do45 adapter. Free-cell placement uses the existing native
// Decoy contract; the reconstructed general spawn-search routine is a stub.
// Radius-1/4 fallback and full current executable acceptance remain
// separate. In particular, failed placement never substitutes the caster cell.
internal static class AssassinSentryCasts
{
    internal static ActionTiming Timing(PlayerMobile owner)=>LobProjectiles.Timing(owner);
    internal static bool Supported(PlayerMobile owner,CombatAbility ability=CombatAbility.ChargedBoltSentry)
    {
        if(owner is not{Alive:true,Deleted:false} || ability is not(CombatAbility.ChargedBoltSentry or CombatAbility.LightningSentry or CombatAbility.WakeOfInferno or CombatAbility.DeathSentry) || !AssassinCombat.Available(owner,ability)){return false;}
        try{_=AssassinSentryCreation.Capture(owner,ability);return true;}
        catch(NotSupportedException){return false;}
    }
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D cached,out Point3D point)
    {
        point=cached;
        if(owner is not{Alive:true,Deleted:false} || owner.Map==null || owner.Map==Map.Internal){return false;}
        if(selected!=Serial.Zero)
        {
            // The source getter prefers even a dead-but-present unit. A gone
            // unit falls back to the retained action point. Cross-map targets
            // are unsupported; do not silently substitute their old position.
            if(!selected.IsMobile){return false;}
            if(World.FindMobile(selected) is{Deleted:false} target)
            {if(target.Map!=owner.Map){return false;}point=target.Location;}
        }
        return LobProjectiles.Valid(owner,point) && owner.InLOS(point) && owner.Map.CanSpawnMobile(point);
    }
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now,CombatAbility ability=CombatAbility.ChargedBoltSentry)
    {
        if(!Destination(owner,selected,point,out point)||!Supported(owner,ability)){return false;}
        return SkillCombat.Begin(owner,ability,selected,point,now,
            (p,ignoredAbility,id,where)=>Destination(p,id,where,out _),timing:Timing(owner));
    }
    internal static bool Release(PlayerMobile owner,Serial selected,DateTime? at=null,CombatAbility ability=CombatAbility.ChargedBoltSentry)
    {
        if(owner is not{Alive:true,Deleted:false} || !ClassCombat.Enabled(owner)
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Assassin
            || SkillMechanics.Rank(owner,ability) is <1 or >255
            || !Supported(owner,ability) || !Destination(owner,selected,ClassCombat.For(owner).PendingPoint,out var point))
        {return false;}
        var values=SkillMechanics.Values(owner,ability);
        // Reuse the existing no-start native resource policy (also Decoy).
        // Legacy wrappers ignore a failed debit if mana was lost in windup;
        // that edge is NOT established for3.3.93847. Keep it explicitly gated
        // rather than adopting a free trap. The current table supplies each cost.
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        var body=AssassinSentryBody.CreateEmerging(owner,point,at??TimedEffects.Now,
            SourceRandom.InitLow(BladeSentinelSummons.SeedLow()),ability);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){body.Delete();return false;}
        // No independent local/global delay is present in the pinned row.
        // Shared player action scheduling owns once-only release/recovery.
        owner.PlaySound(0x23B); // Identified assassin_summon audio placeholder.
        return true;
    }
}
