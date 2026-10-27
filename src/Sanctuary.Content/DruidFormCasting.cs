// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public static class DruidFormCasting
{
    internal sealed record Pending(CombatAbility Ability,DruidPlayerForm Before);
    internal static Func<PlayerMobile,CombatAbility,DateTime,bool> Apply=DruidPlayerForms.ApplyPrepared;
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.Werewolf or CombatAbility.Werebear;
    internal static bool Transition(PlayerMobile owner,CombatAbility ability)
    {
        var before=DruidPlayerForms.State(owner);
        // Modern Param12direct switching is still unverified. This path is
        // explicitly unsupported; ordinary human entry and same-form return work.
        return Handles(ability)&&(before==null||before.Form.Ability==ability);
    }
    public static void Initialize()=>ClassMapHooks.LocationChanged+=Moved;
    private static void Moved(Mobile mobile)
    {
        if(World.Loading||mobile is not PlayerMobile owner){return;}
        var action=ClassCombat.Existing(owner);
        if(action?.Transform!=null&&(owner.Map!=action.ActionMap||owner.Location!=action.ActionOrigin)){ClassCombat.Stop(owner);}
    }
    internal static bool Begin(PlayerMobile owner,CombatAbility ability,long now)
    {
        if(owner==null||!Transition(owner,ability)){return false;}
        var before=DruidPlayerForms.State(owner);
        var timing=DruidFormCastClock.Calculate(before?.Form.Ability??CombatAbility.None,CombatTiming.Fcr(owner));
        if(!SkillCombat.Begin(owner,ability,Serial.Zero,owner.Location,now,
            (p,_,_,_)=>p.Map!=null&&p.Map!=Map.Internal&&Transition(p,ability),requireMana:before==null,timing:timing)){return false;}
        ClassCombat.For(owner).Transform=new(ability,before);return true;
    }
    internal static bool Release(PlayerMobile owner,CombatAbility ability,long now)
    {
        if(owner==null){return false;}
        var action=ClassCombat.For(owner);var pending=action.Transform;
        if(pending==null||pending.Ability!=ability||now<action.EffectAt){return false;}
        action.Transform=null;
        if(!ReferenceEquals(pending.Before,DruidPlayerForms.State(owner))
            ||owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
            ||owner.Map==null||owner.Map==Map.Internal||owner.Map!=action.ActionMap||owner.Location!=action.ActionOrigin
            ||!AlliedOverlap.Allowed(owner)||!DruidCombat.Available(owner,ability)||!FireGround.Ready(owner,ability)){return false;}
        var values=SkillMechanics.Values(owner,ability);var row=SkillMechanics.For(ability);
        if(row.Number("srvdofunc")!=116||row.Number("globaldelay")!=25||row.Number("localdelay")!=0
            ||row.Text("anim")!="SC"||row.Number("InTown")!=1||values.Mana256!=15*256)
        {throw new NotSupportedException("Unmapped learned form route");}
        if(pending.Before!=null)
        {
            if(!DruidPlayerForms.Clear(owner)){return false;}
        }
        else
        {
            // Current lost-mana behavior is unresolved. Refuse that branch
            // explicitly instead of importing the legacy ignored-debit quirk.
            if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
            if(!Apply(owner,ability,TimedEffects.Now)){return false;}
            if(!NecromancerEffects.Pay(owner,values.Mana256))
            {DruidPlayerForms.Clear(owner);throw new InvalidOperationException("Form payment changed inside one server turn");}
        }
        FireGround.DelayForms(owner);
        DruidFormInterruption.Capture(owner,now);
        owner.FixedEffect(0x373A,10,10);owner.PlaySound(0x1E5); // Identified transform/audio placeholders; human body remains.
        return true;
    }
}
