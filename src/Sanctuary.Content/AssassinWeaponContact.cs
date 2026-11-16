// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Human charge builders and common Attack finishers share the ordinary NPC
// contact consumer. Sequence/payment/selected-hand ownership stays in callers.
internal static class AssassinWeaponContact
{
    internal static bool Require(PlayerMobile owner,Mobile target)
    {
        if(target is not BaseCreature creature){return true;}
        try{PlayerWeaponContact.RequireOrdinary(owner,creature,true);return true;}
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
    }
    internal static bool Builder(PlayerMobile owner,BaseCreature target,SkillValues values)
    {
        try
        {
            var prepared=PlayerWeaponContact.PrepareOrdinary(owner,target,(int)values.Formula("toht"),0,
                ()=>TigerStrikes.ValidTarget(owner,target.Serial),TigerStrikes.HitRoll,TigerStrikes.ElementRoll,
                TigerStrikes.PhysicalRoll,TigerStrikes.PoisonRoll,TigerStrikes.Effects,true) with{AttemptScoped=true};
            // do34 gains AFTER allocation, BEFORE damage/durability events,
            // even against elemental/physical immunity. A miss still drains
            // its own attempt packet without gaining or refreshing charges.
            if((prepared.Flags&SourceMeleeResult.Successful)!=0){AssassinCharges.Gain(owner,values);}
            var result=PlayerWeaponContact.Release(owner,target,prepared,TimedEffects.Now,true);
            return result.Commit!=null&&(result.Packet.ResultFlags&1)!=0;
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
    }
    internal static bool Basic(PlayerMobile owner,BaseCreature target)
    {
        try
        {
            var states=AssassinCharges.Finisher(owner);var tiger=states.FirstOrDefault(x=>x.Ability==CombatAbility.TigerStrike);
            int ed=AssassinCharges.TigerPercent(tiger,SkillMechanics.Rank(owner,CombatAbility.TigerStrike));
            var drain=WeaponLeech.Capture(owner,AssassinCharges.CobraDrain(states.FirstOrDefault(x=>x.Ability==CombatAbility.CobraStrike),SkillMechanics.Rank(owner,CombatAbility.CobraStrike)));
            FistsFirePacket fire=null;ThunderPacket lightning=null;IcePacket ice=null;
            int cold=0,freeze=0;
            var prepared=PlayerWeaponContact.PrepareOrdinary(owner,target,states.Sum(x=>x.AttackPercent),ed,
                ()=>TigerStrikes.ValidTarget(owner,target.Serial),TigerStrikes.HitRoll,TigerStrikes.ElementRoll,
                TigerStrikes.PhysicalRoll,TigerStrikes.PoisonRoll,TigerStrikes.Effects,true,captured:()=>
                {
                    fire=FistsFireRelease.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.FistsOfFire));
                    lightning=MartialLightning.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.ClawsOfThunder));
                    ice=MartialCold.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.BladesOfIce));
                    double roll=TigerStrikes.ElementRoll();var venom=Venoms.State(owner);
                    var raw=GearEffects.Damage(owner,roll,target,ed,TigerStrikes.PhysicalRoll,TigerStrikes.PoisonRoll,
                        ()=>WeaponPoisonPackets.Melee(WeaponPoisonPackets.AddVenom(BowLaunch.RawPoison(owner,CombatTiming.Equipped(owner)),venom),NamedElements.MasteryPercent(owner,"pois"),128,TigerStrikes.PoisonRoll,overrideFrames:venom?.Frames??0));
                    var extra=new GearDamage(0,(fire?.Inline256??0)/256.0,(ice?.Inline256??0)/256.0,(lightning?.Inline256??0)/256.0,0);
                    var impact=GearEffects.PrepareWeaponImpact(owner,target,roll,TigerStrikes.Effects,raw,extra,physicalToFirePercent:fire?.ConversionPercent??0);
                    var poison=PoisonJavelins.PreparePoison(owner,target,raw.SourcePoison.Rate256,raw.SourcePoison.Frames);
                    impact=impact with{LifeDamage256=checked(NamedElements.To256(impact.Parts.Total)+poison.Rate256),AllocatedPoison=poison};
                    if(raw.ColdMs%40!=0){throw new NotSupportedException("Common finisher cold duration has no source-frame projection");}
                    // The progressive cold writer replaces ordinary item length.
                    int rawCold=ice?.Frames??raw.ColdMs/40;
                    (cold,freeze)=DragonTails.ColdLengths(owner,target,rawCold,ice?.Count==3?rawCold/SkillMechanics.For(CombatAbility.BladesOfIce).Number("Param5"):0);
                    return impact;
                },capturedDrain:drain) with{AttemptScoped=true};
            bool hit=(prepared.Flags&SourceMeleeResult.Successful)!=0;
            prepared=prepared with{ColdFrames=cold,FreezeFrames=freeze,HitFlags=hit?2u:0u,
                Flags=prepared.Flags|((fire!=null||lightning!=null||ice!=null)?0x4000:0)};
            var center=target.Location;var map=target.Map;
            if(hit){foreach(var state in states){AssassinCharges.Consume(owner,state,CommonMeleeSkills.Attack.Number("prgchargesconsumed"));}}
            var result=PlayerWeaponContact.Release(owner,target,prepared,TimedEffects.Now,true);
            if(hit)
            {
                FistsFireRelease.AfterParent(owner,center,map,fire,TimedEffects.Now);
                MartialLightning.AfterParent(owner,center,map,lightning,TimedEffects.Now);
                MartialCold.AfterParent(owner,target,center,map,ice,TimedEffects.Now);
                MartialPhoenix.AfterParent(owner,target,center,map,states.FirstOrDefault(x=>x.Ability==CombatAbility.PhoenixStrike),TimedEffects.Now);
            }
            return result.Commit!=null&&(result.Packet.ResultFlags&1)!=0;
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
    }
}
