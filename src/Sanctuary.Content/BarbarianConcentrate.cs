// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class BarbarianConcentrate
{
    internal sealed record Mode(Serial Weapon,Point3D Origin,Map Map,long Until,int Defense);
    private static bool Valid(PlayerMobile owner,Mode state,long now)=>state!=null && now<state.Until
        && owner is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
        && BarbarianCombat.Available(owner,CombatAbility.Concentrate) && AlliedOverlap.Allowed(owner)
        && owner.Map==state.Map && owner.Location==state.Origin && (CombatTiming.Equipped(owner)?.Serial??Serial.Zero)==state.Weapon;
    public static bool Locked(PlayerMobile owner,long now)=>owner!=null && Valid(owner,ClassCombat.Existing(owner)?.Concentrate,now);
    public static int Defense(PlayerMobile owner)=>Locked(owner,Core.TickCount)?ClassCombat.Existing(owner).Concentrate.Defense:0;
    internal static (int Physical256,int Magic256) Split(int physical256,int percent)
    {
        int converted=(int)((long)Math.Max(0,physical256)*Math.Clamp(percent,0,100)/100);
        return(Math.Max(0,physical256-converted),converted);
    }
    internal static void Begin(PlayerMobile owner,SkillValues values)
    {
        var action=ClassCombat.For(owner);
        action.Concentrate=new(action.PendingWeapon,action.ActionOrigin,action.ActionMap,action.NextAction,(int)values.Eval("aurastatcalc1"));
        ClassStats.Refresh(owner);StatNetwork.Send(owner);
    }
    internal static void Impact(PlayerMobile owner,long now)
    {
        var action=ClassCombat.For(owner);
        if(!Valid(owner,action.Concentrate,now)){return;}
        action.Concentrate=action.Concentrate with{Defense=(int)SkillMechanics.Values(owner,CombatAbility.Concentrate).Eval("aurastatcalc1")};
        ClassStats.Refresh(owner);StatNetwork.Send(owner);
    }
    public static void Cancel(PlayerMobile owner)
    {
        var action=ClassCombat.For(owner);bool had=action.Concentrate!=null;action.Concentrate=null;
        if(action.PendingAbility==CombatAbility.Concentrate){action.PendingAbility=CombatAbility.None;action.BarbarianAttack=null;}
        if(had){ClassStats.Refresh(owner);StatNetwork.Send(owner);}
    }
    internal static void Tick(PlayerMobile owner,long now)
    {if(ClassCombat.For(owner).Concentrate is{} mode && !Valid(owner,mode,now)){Cancel(owner);}}
}
