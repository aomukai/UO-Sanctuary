// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// A finisher packet consumer of the shared claw sequence, not a second sequence
// controller. Policy is separately approved in dragon-claw-approved-action-rule.
internal static class DragonClaws
{
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static void Require(PlayerMobile owner,BaseCreature target)
    {
        if(!DragonClawPreparation.WeaponAllowed(owner)){throw new NotSupportedException("Dragon Claw requires two active claws");}
        var pair=WeaponHands.Pair(owner).Value;
        foreach(var weapon in new[]{pair.Primary,pair.Secondary})
        {
            using var hand=WeaponHands.Use(owner,weapon);using var melee=WeaponHands.UseMelee(owner,weapon);
            PlayerWeaponContact.RequireOrdinary(owner,target,true);
        }
        _=DragonClawPreparation.PhysicalPercent(owner);
    }
    internal static PlayerWeaponContactPrepared Prepare(PlayerMobile owner,BaseCreature target)
    {
        PlayerWeaponContact.RequireOrdinary(owner,target,true);
        int flags=DragonTailPreparation.ResultFlags(owner,target,()=>PlayerMeleeGeometry.Within(owner,target,1),HitRoll,CombatAbility.DragonClaw);
        PreparedWeaponImpact impact=default;PlayerWeaponDrain drain=default;int cold=0,freeze=0;
        if((flags&SourceMeleeResult.Successful)!=0)
        {
            WeaponStartEffects.PrepareDamage(owner,target);
            var states=AssassinCharges.Finisher(owner);var weapon=CombatTiming.Equipped(owner);
            int ed=checked(DragonClawPreparation.PhysicalPercent(owner)+AssassinCharges.TigerPercent(states.FirstOrDefault(x=>x.Ability==CombatAbility.TigerStrike),SkillMechanics.Rank(owner,CombatAbility.TigerStrike)));
            var fire=FistsFireRelease.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.FistsOfFire));
            var lightning=MartialLightning.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.ClawsOfThunder));
            var ice=MartialCold.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.BladesOfIce));
            double roll=ElementRoll();
            var damage=GearEffects.Damage(owner,roll,target,ed,PhysicalRoll,PoisonRoll,
                ()=>WeaponPoisonPackets.Melee(WeaponPoisonPackets.AddVenom(BowLaunch.RawPoison(owner,weapon),Venoms.State(owner)),NamedElements.MasteryPercent(owner,"pois"),128,PoisonRoll,overrideFrames:Venoms.State(owner)?.Frames??0));
            var extra=new GearDamage(0,(fire?.Inline256??0)/256.0,(ice?.Inline256??0)/256.0,(lightning?.Inline256??0)/256.0,0);
            impact=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,damage,extra,physicalToFirePercent:fire?.ConversionPercent??0);
            var poison=PoisonJavelins.PreparePoison(owner,target,damage.SourcePoison.Rate256,damage.SourcePoison.Frames);
            impact=impact with{LifeDamage256=checked(NamedElements.To256(impact.Parts.Total)+poison.Rate256),AllocatedPoison=poison};
            drain=WeaponLeech.Capture(owner,AssassinCharges.CobraDrain(states.FirstOrDefault(x=>x.Ability==CombatAbility.CobraStrike),SkillMechanics.Rank(owner,CombatAbility.CobraStrike)));
            if(damage.ColdMs%40!=0){throw new NotSupportedException("Dragon Claw cold duration has no exact source-frame projection");}
            int rawCold=checked(damage.ColdMs/40+(ice?.Frames??0));
            int rawFreeze=ice?.Count==3?rawCold/SkillMechanics.For(CombatAbility.BladesOfIce).Number("Param5"):0;
            (cold,freeze)=DragonTails.ColdLengths(owner,target,rawCold,rawFreeze);
            if(fire!=null||lightning!=null||ice!=null){flags|=0x4000;}
        }
        // Source Claw hit flags2 retain ordinary weapon critical; kick flags3
        // must not leak here. Selected primary/secondary scope stays with caller.
        return new(flags,impact,drain,cold,CreatureHitRecovery.PlayerHitClass(owner),Effects,
            RangeCheck:bonus=>PlayerMeleeGeometry.Within(owner,target,bonus),FreezeFrames:freeze,HitFlags:(flags&SourceMeleeResult.Successful)!=0?2u:0u);
    }
    internal static void Strike(PlayerMobile owner,BaseCreature target)
    {
        var weapon=CombatTiming.Equipped(owner);using var melee=WeaponHands.UseMelee(owner,weapon);
        var prepared=Prepare(owner,target);owner.PlaySound(0x238); // Identified claw/contact audio placeholder.
        if((prepared.Flags&SourceMeleeResult.Successful)!=0&&PlayerMeleeGeometry.Within(owner,target,0))
        {DragonTails.Progressive(owner,target,target.Location,target.Map,CombatAbility.DragonClaw);}
        if(!target.Deleted){PlayerWeaponContact.Release(owner,target,prepared,TimedEffects.Now,true);}
    }
}
