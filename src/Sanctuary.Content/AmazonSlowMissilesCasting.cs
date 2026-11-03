// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Prepared SC windup only. Public learned routing remains gated on the
// separately recorded target/hostile-producer/current missile-order gaps.
internal static class AmazonSlowMissilesCasting
{
    internal sealed record Pending();
    internal static bool Eligible(PlayerMobile owner,CombatAbility ability)=>ability==CombatAbility.SlowMissiles
        &&AmazonSlowMissilesCast.Eligible(owner)&&ClassCombat.Enabled(owner)&&ShapeRestrictions.Allowed(owner,ability);
    public static void Initialize()=>ClassMapHooks.LocationChanged+=Moved;
    private static void Moved(Mobile actor)
    {
        if(World.Loading||actor is not PlayerMobile owner||ClassCombat.Existing(owner) is not{SlowMissilesCast:not null} action){return;}
        if(owner.Map!=action.ActionMap||owner.Location!=action.ActionOrigin){ClassCombat.Stop(owner);}
    }
    internal static bool BeginPrepared(PlayerMobile owner,long now)
    {
        if(!Eligible(owner,CombatAbility.SlowMissiles)||ClassCombat.For(owner).SlowMissilesCast!=null
            ||!AmazonCastClock.TryTiming(owner,out var timing)){return false;}
        if(!SkillCombat.BeginPrepared(owner,CombatAbility.SlowMissiles,Serial.Zero,owner.Location,now,
            (p,_,_,_)=>AmazonSlowMissilesCast.Eligible(p),Eligible,timing)){return false;}
        ClassCombat.For(owner).SlowMissilesCast=new();return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{SlowMissilesCast:not null} action){return;}
        action.SlowMissilesCast=null;
        if(action.PendingAbility==CombatAbility.SlowMissiles){action.PendingAbility=CombatAbility.None;}
    }
    internal static bool ResolvePrepared(PlayerMobile owner,long now)
    {
        if(ClassCombat.Existing(owner) is not{SlowMissilesCast:not null} action){return false;}
        if(action.PendingAbility!=CombatAbility.SlowMissiles||!Eligible(owner,CombatAbility.SlowMissiles)
            ||owner.Spell!=null||HitRecovery.Active(owner,now)||owner.Location!=action.ActionOrigin||owner.Map!=action.ActionMap
            ||(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=action.PendingWeapon)
        {Cancel(owner);return false;}
        if(now<action.EffectAt){return false;}
        // Retire the transient context before the shared payment/search. A
        // refused payment and duplicate event cannot re-enter the release.
        Cancel(owner);return AmazonSlowMissilesCast.Commit(owner,TimedEffects.Now);
    }
}
