// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class AmazonMelee
{
    internal sealed record Prepared(CombatAbility Ability,Serial Target,bool Hit,bool Basic,PreparedWeaponImpact Impact,double ElementRoll,int Lightning256,long EffectAt,int Rank)
    {internal PlayerWeaponContactPrepared Contact{get;init;}}
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> LightningRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static Random WearRoll=Random.Shared;
    public static bool Handles(CombatAbility ability)=>ability is CombatAbility.PowerStrike or CombatAbility.ChargedStrike or CombatAbility.LightningStrike;
    // Literal shared start6 reads calc1, which is also Charged Strike's bolt
    // count. Current executable corroboration of this small ED overlap is open.
    internal static int PhysicalBonus(CombatAbility ability,SkillValues values,bool basic)=>!basic && ability is(CombatAbility.ChargedStrike or CombatAbility.LightningStrike)?(int)values.Eval("calc1"):0;
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
        if(ability==CombatAbility.ChargedStrike){ChargedStrikeBolts.RequireRoute();}
        if(ability==CombatAbility.LightningStrike){ChainLightning.RequireLightningStrikeRoute();}
        var values=SkillMechanics.Values(owner,ability);bool basic=!NecromancerEffects.CanPay(owner,values.Mana256);
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(_,_,id,_)=>AmazonJab.ValidTarget(owner,id),requireMana:!basic)){return false;}
        var session=ClassCombat.For(owner);var target=World.FindMobile(selected);double roll=0;int lightning=0,cold=0;
        PlayerWeaponContactPrepared contact;
        using(WeaponHands.UseMelee(owner,CombatTiming.Equipped(owner)))
        {
            if(target is not BaseCreature creature){session.PendingAbility=CombatAbility.None;return false;}
            try
            {
                // Released2.6 fixes Power Strike's older start6 zero-AR bug.
                contact=PlayerWeaponContact.PrepareOrdinary(owner,creature,
                    basic||ability==CombatAbility.LightningStrike?0:(int)values.Formula("toht"),PhysicalBonus(ability,values,basic),
                    ()=>AmazonJab.ValidTarget(owner,selected),HitRoll,ElementRoll,PhysicalRoll,PoisonRoll,Effects,allowNative:true,suppressWear:true,
                    captured:()=>
                    {
                        lightning=basic?0:Lightning(owner,values); // Skill lightning precedes weapon allocation/RNG.
                        roll=ElementRoll();var damage=GearEffects.Damage(owner,roll,creature,PhysicalBonus(ability,values,basic),PhysicalRoll,PoisonRoll,
                            ()=>WeaponPoisonPackets.Melee(WeaponPoisonPackets.AddVenom(BowLaunch.RawPoison(owner,CombatTiming.Equipped(owner)),Venoms.State(owner)),
                                NamedElements.MasteryPercent(owner,"pois"),128,PoisonRoll,overrideFrames:Venoms.State(owner)?.Frames??0));
                        var impact=GearEffects.PrepareWeaponImpact(owner,creature,roll,Effects,damage,new(0,0,0,lightning/256.0,0,ColdMs:0));
                        var poison=PoisonJavelins.PreparePoison(owner,creature,damage.SourcePoison.Rate256,damage.SourcePoison.Frames);
                        if(damage.Cold>0)
                        {
                            if(damage.ColdMs%40!=0){throw new NotSupportedException("Amazon thrust cold duration has no exact source-frame projection");}
                            cold=ColdStates.LengthFrames(damage.ColdMs/40,NamedElements.Resistance(owner,creature,"cold",creature.ColdResistance),
                                ItemDurationDefenses.CannotFreeze(creature),ItemDurationDefenses.HalfFreeze(creature));
                        }
                        return impact with{LifeDamage256=checked(NamedElements.To256(impact.Parts.Total)+poison.Rate256),AllocatedPoison=poison};
                    });
                contact=contact with{ColdFrames=cold,AttemptScoped=true,RangeCheck=_=>AmazonJab.ValidTarget(owner,selected),
                    WeaponWear=()=>AmazonJab.Wear(CombatTiming.Equipped(owner),WearRoll)};
            }
            catch(NotSupportedException e){session.PendingAbility=CombatAbility.None;owner.SendMessage(0x22,e.Message);return false;}
        }
        if(!basic && !NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        session.AmazonAttack=new(ability,selected,(contact.Flags&SourceMeleeResult.Successful)!=0,basic,contact.Impact,roll,lightning,session.EffectAt,values.Rank){Contact=contact};return true;
    }
    public static bool Release(PlayerMobile owner,CombatAbility ability,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.AmazonAttack;
        if(prepared==null || now<prepared.EffectAt){return false;}session.AmazonAttack=null;
        if(prepared.Ability!=ability || session.PendingTarget!=prepared.Target || !AmazonCombat.Available(owner,ability)
            || !AmazonJab.ValidTarget(owner,prepared.Target) || World.FindMobile(prepared.Target) is not{} target){return false;}
        using var mode=WeaponHands.UseMelee(owner,CombatTiming.Equipped(owner));owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified native weapon sound/pose placeholders.
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(target is BaseCreature creature&&prepared.Contact is{} contact)
        {
            try{PlayerWeaponContact.Release(owner,creature,contact,TimedEffects.Now,true);}
            catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
            if(prepared.Hit&&!prepared.Basic){target.FixedEffect(0x374A,10,5,0x481,0);} // Identified weapon lightning-contact placeholder.
        }
        // Source do11 commits melee first, then emits at the resulting target
        // coordinates even on a miss or a lethal strike. The retained target
        // object supplies coordinates after death; no replacement is acquired.
        if(ability==CombatAbility.ChargedStrike && !prepared.Basic)
        {ChargedStrikeBolts.Cast(owner,target.Location,SkillMechanics.Values(owner,ability,prepared.Rank));}
        if(ability==CombatAbility.LightningStrike&&!prepared.Basic)
        {ChainLightning.CastLightningStrike(owner,target,target.Location,SkillMechanics.Values(owner,ability,prepared.Rank),TimedEffects.Now);}
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        var session=ClassCombat.Existing(owner);if(session==null){return;}session.AmazonAttack=null;
        if(Handles(session.PendingAbility)){session.PendingAbility=CombatAbility.None;}
    }
}
