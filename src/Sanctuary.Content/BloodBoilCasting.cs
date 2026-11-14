// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class BloodBoilCasting
{
    internal sealed class Pending
    {
        internal bool Released,Cancelled;
        internal BloodBoilRelease Result;
    }
    internal static bool Actor(PlayerMobile owner)=>owner is{Alive:true,Deleted:false,NetState:{Running:true},Map:not null}
        &&owner.Map!=Map.Internal&&!MinionSafeAreas.Contains(owner)&&!SpellHelper.IsTown(owner.Location,owner)
        &&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Warlock
        &&SkillMechanics.Rank(owner,CombatAbility.BloodBoil)>0;
    internal static Pending BeginPrepared(PlayerMobile owner,long now)
    {
        if(!Actor(owner)||!SkillCombat.BeginPrepared(owner,CombatAbility.BloodBoil,Serial.Zero,owner.Location,now,
            (who,_,id,point)=>BloodBoilPrepared.Roster(who).Any(BloodBoilPrepared.HasLife),(who,_)=>Actor(who),CombatTiming.For(owner,CombatAbility.BloodBoil)))
        {return null;}
        var pending=new Pending();ClassCombat.For(owner).BloodBoil=pending;return pending;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        var s=ClassCombat.Existing(owner);if(s?.BloodBoil is not{} action){return;}action.Cancelled=true;s.BloodBoil=null;
        if(s.PendingAbility==CombatAbility.BloodBoil){s.PendingAbility=CombatAbility.None;}
    }
    internal static bool ResolvePrepared(PlayerMobile owner,long now)
    {
        var s=ClassCombat.For(owner);if(s.BloodBoil is not{} action||action.Released||action.Cancelled){return false;}
        if(!Actor(owner)||!ShapeRestrictions.Allowed(owner,CombatAbility.BloodBoil)||owner.Frozen||owner.Paralyzed||owner.Spell!=null||!AlliedOverlap.Allowed(owner)
            ||s.PendingAbility!=CombatAbility.BloodBoil||owner.Location!=s.ActionOrigin||owner.Map!=s.ActionMap
            ||(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=s.PendingWeapon)
        {Cancel(owner);return false;}
        if(now<s.EffectAt){return false;}action.Released=true;s.BloodBoil=null;s.PendingAbility=CombatAbility.None;
        action.Result=BloodBoilPrepared.ReleasePrepared(owner,TimedEffects.Now);return action.Result.Released&&action.Result.Fault==null;
    }
}
