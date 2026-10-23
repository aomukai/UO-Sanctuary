// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonMelee
{
    internal sealed record Prepared(CombatAbility Ability,Serial Target,bool Hit,bool Basic,PreparedWeaponImpact Impact,double ElementRoll,int Lightning256,long EffectAt);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> LightningRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static Random WearRoll=Random.Shared;
    public static bool Handles(CombatAbility ability)=>ability==CombatAbility.PowerStrike;
    internal static ActionTiming Timing(string code,int advance)
    {
        if(code is not("1HT" or "2HT")){return new(1,1);}advance=Math.Max(1,advance);
        // Source Amazon A1 starts at frame bonus2; cached legacy AnimData has
        // 1HT15/event9 and2HT18/event11. Current-build animation acceptance is open.
        int end=code=="1HT"?13:16,action=code=="1HT"?7:9;
        return new((end*256+advance-1)/advance,(action*256+advance-1)/advance);
    }
    internal static ActionTiming Timing(PlayerMobile owner)=>Timing(AmazonJab.Code(owner),CombatTiming.WeaponSequenceAdvance(owner));
    internal static int EnhanceEndpoint(int value,int mastery)=>(int)Math.Clamp((long)value+(long)value*mastery/100,0,int.MaxValue);
    internal static (int Min,int Max) LightningRange(SkillValues values,int mastery)=>
        (EnhanceEndpoint(values.Damage256(false),mastery),EnhanceEndpoint(values.Damage256(true),mastery));
    internal static int Lightning(PlayerMobile owner,SkillValues values)
    {
        var range=LightningRange(values,NamedElements.MasteryPercent(owner,"ltng"));int span=Math.Max(1,range.Max-range.Min);
        return range.Min+Math.Clamp(LightningRoll(span),0,span-1);
    }
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
    {
        if(!Handles(ability) || !AmazonCombat.Available(owner,ability) || !AmazonJab.ValidTarget(owner,selected)){return false;}
        var values=SkillMechanics.Values(owner,ability);bool basic=!NecromancerEffects.CanPay(owner,values.Mana256);
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(_,_,id,_)=>AmazonJab.ValidTarget(owner,id),requireMana:!basic)){return false;}
        var session=ClassCombat.For(owner);var target=World.FindMobile(selected);bool hit;double roll=0;int lightning=0;PreparedWeaponImpact packet=default;
        using(WeaponHands.UseMelee(owner,CombatTiming.Equipped(owner)))
        {
            // Released2.6 fixes the older start6 zero-AR bug; use the pinned bonus.
            hit=ClassStats.CheckHit(owner,target,basic?0:(int)values.Formula("toht"),HitRoll)==true;
            if(hit)
            {
                lightning=basic?0:Lightning(owner,values); // Skill lightning precedes weapon allocation/RNG.
                roll=ElementRoll();var damage=GearEffects.Damage(owner,roll,target,0,PhysicalRoll);
                packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,damage,new(0,0,0,lightning/256.0,0,ColdMs:0));
                packet=packet with{LifeDamage256=NamedElements.To256(packet.Parts.Total)};
            }
        }
        if(!basic && !NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        session.AmazonAttack=new(ability,selected,hit,basic,packet,roll,lightning,session.EffectAt);return true;
    }
    public static bool Release(PlayerMobile owner,CombatAbility ability,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.AmazonAttack;
        if(prepared==null || now<prepared.EffectAt){return false;}session.AmazonAttack=null;
        if(prepared.Ability!=ability || session.PendingTarget!=prepared.Target || !AmazonCombat.Available(owner,ability)
            || !AmazonJab.ValidTarget(owner,prepared.Target) || World.FindMobile(prepared.Target) is not{} target){return false;}
        using var mode=WeaponHands.UseMelee(owner,CombatTiming.Equipped(owner));owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified native weapon sound/pose placeholders.
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(!prepared.Hit){return true;}
        var weapon=CombatTiming.Equipped(owner);GearEffects.Strike(owner,target,prepared.ElementRoll,Effects,wearWeapon:false,preparedImpact:prepared.Impact);
        AmazonJab.Wear(weapon,WearRoll);
        if(!prepared.Basic){target.FixedEffect(0x374A,10,5,0x481,0);} // Identified Power Strike lightning-contact placeholder.
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        var session=ClassCombat.Existing(owner);if(session==null){return;}session.AmazonAttack=null;
        if(Handles(session.PendingAbility)){session.PendingAbility=CombatAbility.None;}
    }
}
