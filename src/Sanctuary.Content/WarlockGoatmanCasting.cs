// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public static class WarlockGoatmanCasting
{
    public static void Initialize()=>ClassMapHooks.LocationChanged+=LocationChanged;
    private static void LocationChanged(Mobile mobile)
    {
        if(World.Loading||mobile is not PlayerMobile owner){return;}var s=ClassCombat.For(owner);
        if(s.PendingAbility==CombatAbility.SummonGoatman&&(owner.Map!=s.ActionMap||owner.Location!=s.ActionOrigin)){ClassCombat.Stop(owner);}
    }
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D aim,bool release,out Point3D point)
    {
        point=default;if(owner is not{Alive:true,Deleted:false}||MinionSafeAreas.Contains(owner)){return false;}
        return DruidSpiritWolfCasting.Destination(owner,selected,aim,release,out point)&&!SpellHelper.IsTown(point,owner);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
        =>Destination(owner,selected,point,false,out _)&&SkillCombat.Begin(owner,CombatAbility.SummonGoatman,selected,point,now,
            (p,ability,id,at)=>Destination(p,id,at,false,out _));
    public static bool Release(PlayerMobile owner)
    {
        var action=ClassCombat.For(owner);
        if(!WarlockCombat.Available(owner,CombatAbility.SummonGoatman)||!Destination(owner,action.PendingTarget,action.PendingPoint,true,out var point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.SummonGoatman);if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        WarlockGoatmanBody body;
        try{body=WarlockGoatmanWorld.Create(owner,point,TimedEffects.Now);}
        catch(NotSupportedException e){Server.Logging.LogFactory.GetLogger(typeof(WarlockGoatmanCasting)).Error(e,"Goatman summon refused unsupported context");return false;}
        // Commit payment before pool replacement or automatic admission. The
        // server loop cannot tick the new,untracked body between these steps.
        if(!NecromancerEffects.Pay(owner,values.Mana256)){DeathMarkWorld.Remove(body);body.Delete();return false;}
        WarlockGoatmanWorld.Admit(body);WarlockGoatmanWorld.Enforce(owner);FireGround.Delay(owner,CombatAbility.SummonGoatman);
        owner.PlaySound(0x1E5); // Identified shared summon sound placeholder.
        return true;
    }
}
