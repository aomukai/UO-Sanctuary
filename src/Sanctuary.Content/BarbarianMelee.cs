// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class BarbarianMelee
{
    internal sealed record Prepared(CombatAbility Ability,Serial Target,bool Hit,PreparedWeaponImpact Impact,double ElementRoll,int StunFrames,long EffectAt)
    {internal PlayerWeaponContactPrepared Contact{get;init;}}
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    public static bool Handles(CombatAbility ability)=>ability is CombatAbility.Bash or CombatAbility.Stun or CombatAbility.Concentrate or CombatAbility.Berserk;
    public static bool ValidTarget(PlayerMobile owner,CombatAbility ability,Serial selected)
    {
        if(!Handles(ability) || !PaladinVengeance.ValidTarget(owner,selected) || World.FindMobile(selected) is not BaseCreature target){return false;}
        if(ability is CombatAbility.Concentrate or CombatAbility.Berserk){return true;}
        // Damage/control stay available for source-mapped species; do not invent
        // unknown native stun metadata or the special sand-leaper knockback path.
        if(ability==CombatAbility.Stun){return CombatStuns.Rules(target,out _,out _);}
        return SourceKnockbacks.Rule(target) is{} rule && (!rule.Knockback || !rule.GetHit || rule.Distance==5);
    }
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
    {
        if(!Handles(ability)||!BarbarianCombat.Available(owner,ability)||!ValidTarget(owner,ability,selected)){return false;}
        var target=(BaseCreature)World.FindMobile(selected);var values=SkillMechanics.Values(owner,ability);
        if(!NecromancerEffects.CanPay(owner,values.Mana256))
        {return ClassCombat.Perform(owner,CombatAbility.BasicAttack,target,point,now);}
        try{PlayerWeaponContact.RequireOrdinary(owner,target,true);}
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(_,skill,id,_)=>ValidTarget(owner,skill,id))){return false;}
        var session=ClassCombat.For(owner);var row=SkillMechanics.For(ability);
        int ar=(int)(row.SourceFields.ContainsKey("ToHitCalc")&&row.Text("ToHitCalc").Length>0?values.Eval("ToHitCalc"):values.Formula("toht"));
        // Start32/start39 allocate once. Accuracy,shield,setup,critical,typed
        // lanes,poison mitigation and leech stay captured across the windup.
        double roll=0;int bashFlat256=ability==CombatAbility.Bash?checked((int)values.Eval("calc2")*256):0;
        var contact=PlayerWeaponContact.PrepareOrdinary(owner,target,ar,(int)values.Eval("calc1"),
            ()=>ValidTarget(owner,ability,selected),HitRoll,()=>roll=ElementRoll(),PhysicalRoll,PoisonRoll,Effects,true,
            physicalToMagicPercent:ability is CombatAbility.Concentrate or CombatAbility.Berserk?(int)values.Eval("calc4"):0,
            physicalAfterCritical:ability==CombatAbility.Bash?physical=>checked(physical+bashFlat256):null);
        bool hit=(contact.Flags&SourceMeleeResult.Successful)!=0;var packet=contact.Impact;
        // Approved Sanctuary Bash rule: its flat bonus participates in typed
        // mitigation and life allocation,after weapon critical. Other start32
        // rows retain their original zero flat field; Berserk calc2 is duration.
        packet=packet with{LifeDamage256=packet.LifeDamage256??NamedElements.To256(packet.Parts.Total),
            Parts=packet.Parts with{Physical=packet.Parts.Physical+(ability is CombatAbility.Bash or CombatAbility.Berserk?0:values.Eval("calc2"))},
            SourceStunFrames=hit&&ability==CombatAbility.Stun?values.DurationFrames:packet.SourceStunFrames};
        contact=contact with{Impact=packet,Flags=contact.Flags|(hit?row.Number("ResultFlags"):0),HitClass=hit?row.Number("HitClass"):0,
            HitFlags=hit?(uint)row.Number("HitFlags"):0u,SkillKnockback=ability==CombatAbility.Bash,StunRoll=CombatStuns.Roll,ControlStun=ability==CombatAbility.Stun,AttemptScoped=true};
        if(!NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        if(ability==CombatAbility.Concentrate){BarbarianConcentrate.Begin(owner,values);}
        if(ability==CombatAbility.Berserk){BarbarianBerserk.Begin(owner,values,now);}
        session.BarbarianAttack=new(ability,selected,hit,packet,roll,ability==CombatAbility.Stun?values.DurationFrames:0,session.EffectAt){Contact=contact};
        return true;
    }
    public static bool Release(PlayerMobile owner,CombatAbility ability,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.BarbarianAttack;
        if(prepared==null || now<prepared.EffectAt){return false;}
        session.BarbarianAttack=null;
        if(prepared.Ability!=ability || prepared.Target!=selected || !BarbarianCombat.Available(owner,ability)){return false;}
        if(ability==CombatAbility.Berserk){BarbarianBerserk.Impact(owner,now);}
        if(!ValidTarget(owner,ability,selected)){return false;}
        if(ability==CombatAbility.Concentrate){BarbarianConcentrate.Impact(owner,now);}
        var target=(BaseCreature)World.FindMobile(selected);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing weapon sound/animation placeholders.
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(prepared.Contact==null){throw new NotSupportedException("Missing captured Barbarian contact");}
        // The outer scope owns the attempt. Even misses and shield blocks
        // release their captured attempt packet; never reroll at action time.
        try{PlayerWeaponContact.Release(owner,target,prepared.Contact with{AttemptScoped=true},TimedEffects.Now,true);}
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
        return true;
    }
}
