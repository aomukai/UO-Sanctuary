// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public static class DragonTails
{
    internal sealed record Prepared(Serial Target,int Flags,PreparedKickPacket Kick,PlayerWeaponContactPrepared Contact,long EffectAt);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static readonly CombatAbility[] ProgressiveOrder=[CombatAbility.TigerStrike,CombatAbility.CobraStrike,CombatAbility.PhoenixStrike,CombatAbility.FistsOfFire,CombatAbility.BladesOfIce,CombatAbility.ClawsOfThunder];
    public static void Initialize()=>ClassMapHooks.LocationChanged+=mobile=>
    {
        if(mobile is PlayerMobile owner&&ClassCombat.Existing(owner) is{} session&&session.TailAttack!=null
            &&(owner.Location!=session.ActionOrigin||owner.Map!=session.ActionMap)){Cancel(owner);}
    };
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{} session){return;}session.TailAttack=null;
        if(session.PendingAbility==CombatAbility.DragonTail){session.PendingAbility=CombatAbility.None;}
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is{} session&&session.TailAttack!=null&&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=session.PendingWeapon){Cancel(owner);}
    }
    internal static bool ValidTarget(PlayerMobile owner,Serial selected)
        =>World.FindMobile(selected) is BaseCreature target&&ClassCombat.IsHostile(owner,target)
            &&PlayerMeleeGeometry.Within(owner,target,1)&&SpellHelper.CheckTown(owner.Location,owner)&&SpellHelper.CheckTown(target.Location,owner);
    private static Prepared Allocate(PlayerMobile owner,BaseCreature target,long effectAt)
    {
        var pair=WeaponHands.Pair(owner);using var primary=pair is{} hands?WeaponHands.Use(owner,hands.Primary):null;
        var weapon=CombatTiming.Equipped(owner);using var melee=weapon!=null?WeaponHands.UseMelee(owner,weapon):null;
        int flags=DragonTailPreparation.ResultFlags(owner,target,()=>PlayerMeleeGeometry.Within(owner,target,1),HitRoll);
        PreparedKickPacket kick=null;PreparedWeaponImpact impact=default;int cold=0,freeze=0;
        if((flags&1)!=0)
        {
            WeaponStartEffects.PrepareDamage(owner,target);
            kick=DragonKickPackets.Prepare(owner,target,0,ElementRoll(),PhysicalRoll,PoisonRoll,Effects,orderedPoison:true);impact=kick.Impact;
            if(kick.SoftHit){flags|=0x4000;}
            int resistance=NamedElements.Resistance(owner,target,"cold",target.ColdResistance);
            bool cannot=ItemDurationDefenses.CannotFreeze(target),half=ItemDurationDefenses.HalfFreeze(target);
            cold=ColdStates.LengthFrames(kick.ColdFrames??0,resistance,cannot,half);
            int mode=FreezeStates.Mode(1,false,CreatureProfiles.For(target).Traits.Boss==true,NamedCombat.Elite(target),false,
                PaladinHolyFreeze.SourceRule(target)==null?null:ColdStates.Effect(target));
            int length=ColdStates.LengthFrames(kick.FreezeFrames,resistance,cannot,half);
            if(mode==2){freeze=length;}else if(mode==1){cold=Math.Max(cold,length);}
            // Refuse unverified old signed-overflow contexts before payment.
            _=DragonKickPackets.TailFire256(NamedElements.To256(impact.Parts.Physical),SkillMechanics.Values(owner,CombatAbility.DragonTail),NamedElements.MasteryPercent(owner,"fire"));
        }
        var contact=new PlayerWeaponContactPrepared(flags,impact,kick?.Drain??default,cold,CreatureHitRecovery.PlayerHitClass(owner),Effects,
            RangeCheck:bonus=>PlayerMeleeGeometry.Within(owner,target,bonus),FreezeFrames:freeze);
        return new(target.Serial,flags,kick,contact,effectAt);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!AssassinCombat.Available(owner,CombatAbility.DragonTail)){return false;}
        try
        {
            if(!ValidTarget(owner,selected)||World.FindMobile(selected) is not BaseCreature target){return false;}
            var pair=WeaponHands.Pair(owner);
            using(var primary=pair is{} hands?WeaponHands.Use(owner,hands.Primary):null)
            {
                var weapon=CombatTiming.Equipped(owner);using var melee=weapon!=null?WeaponHands.UseMelee(owner,weapon):null;
                PlayerWeaponContact.RequireOrdinary(owner,target,true);_=DragonKickDamage.Capture(owner,0);
                if(!DragonKickPackets.TailRecipient(owner,target,out string reason)){throw new NotSupportedException(reason);}
            }
            var values=SkillMechanics.Values(owner,CombatAbility.DragonTail);
            if(!NecromancerEffects.CanPay(owner,values.Mana256))
            {return ClassCombat.Perform(owner,CombatAbility.BasicAttack,target,point,now);}
            if(!SkillCombat.Begin(owner,CombatAbility.DragonTail,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id),timing:DragonTailPreparation.Timing(owner))){return false;}
            var session=ClassCombat.For(owner);session.TailAttack=Allocate(owner,target,session.EffectAt);return true;
        }
        catch(NotSupportedException e){Cancel(owner);owner.SendMessage(0x22,e.Message);return false;}
    }
    private static void Progressive(PlayerMobile owner,BaseCreature target,Point3D center,Map map)
    {
        var states=AssassinCharges.Finisher(owner);
        foreach(var ability in ProgressiveOrder)
        {
            var state=states.FirstOrDefault(x=>x.Ability==ability);if(state==null){continue;}
            switch(ability)
            {
                case CombatAbility.PhoenixStrike:MartialPhoenix.AfterParent(owner,target,center,map,state,TimedEffects.Now);break;
                case CombatAbility.FistsOfFire:FistsFireRelease.AfterParent(owner,center,map,FistsFireRelease.Capture(owner,state,rollInline:false),TimedEffects.Now);break;
                case CombatAbility.BladesOfIce:MartialCold.AfterParent(owner,target,center,map,MartialCold.Capture(owner,state,rollInline:false),TimedEffects.Now);break;
                case CombatAbility.ClawsOfThunder:MartialLightning.AfterParent(owner,center,map,MartialLightning.Capture(owner,state,rollInline:false),TimedEffects.Now);break;
            }
            AssassinCharges.Consume(owner,state,SkillMechanics.For(CombatAbility.DragonTail).Number("prgchargesconsumed"));
        }
    }
    public static bool Release(PlayerMobile owner,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.TailAttack;
        if(prepared==null||now<prepared.EffectAt){return false;}session.TailAttack=null;
        if(prepared.Target!=selected||owner is not{Alive:true,Deleted:false}||SkillHealth.Balance256(owner)<=0
            ||!AssassinCombat.Available(owner,CombatAbility.DragonTail)){return false;}
        var target=World.FindMobile(selected) as BaseCreature;
        try
        {
            var pair=WeaponHands.Pair(owner);using var primary=pair is{} hands?WeaponHands.Use(owner,hands.Primary):null;
            var weapon=CombatTiming.Equipped(owner);using var melee=weapon!=null?WeaponHands.UseMelee(owner,weapon):null;
            if(target is{Alive:true,Deleted:false}){PlayerWeaponContact.RequireOrdinary(owner,target,true);}
            var values=SkillMechanics.Values(owner,CombatAbility.DragonTail);
            if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
            owner.PlaySound(0x238); // Identified native kick/contact sound placeholder.
            if(target==null||target.Deleted||target.Map!=owner.Map){return true;}
            var center=target.Location;var map=target.Map;
            int copied=prepared.Kick==null?0:NamedElements.To256(prepared.Kick.Impact.Parts.Physical);
            // Do50 copies the allocated physical lane before progressive work.
            // Progressive uses bonus0; DrainItemDurability independently uses1.
            if((prepared.Flags&1)!=0&&target is{Alive:true,Deleted:false}&&ClassCombat.IsHostile(owner,target)&&!SpellHelper.IsTown(owner.Location,owner)&&!SpellHelper.IsTown(target.Location,target)&&PlayerMeleeGeometry.Within(owner,target,0))
            {Progressive(owner,target,center,map);}
            if(target is{Alive:true,Deleted:false}){PlayerWeaponContact.Release(owner,target,prepared.Contact,TimedEffects.Now,true);}
            if((prepared.Flags&1)!=0&&owner is{Alive:true,Deleted:false}&&SkillHealth.Balance256(owner)>0)
            {
                if(target is{Deleted:false}&&target.Map==map){center=target.Location;}
                DragonKickPackets.TailBurst(owner,map,center,copied,SkillMechanics.Values(owner,CombatAbility.DragonTail));
            }
            return true;
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
    }
}
