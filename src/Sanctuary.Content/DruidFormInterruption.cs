// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Released2.4 Werebear guarantee. This is scoped to the actual action, not
// permanent state immunity and not an inference from an old cooldown alone.
internal static class DruidFormInterruption
{
    internal sealed record Action(DruidPlayerForm Form,long Started,long Until,Map Map,Point3D Origin);
    public static void Initialize()=>ClassMapHooks.LocationChanged+=Moved;
    private static void Moved(Mobile mobile)
    {
        if(World.Loading||mobile is not PlayerMobile owner){return;}
        var session=ClassCombat.Existing(owner);
        if(session?.FormAction is{} action&&(owner.Map!=action.Map||owner.Location!=action.Origin)){session.FormAction=null;}
    }
    internal static void Capture(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var form=DruidPlayerForms.State(owner);
        session.FormAction=form is{Form.Ability:CombatAbility.Werebear,Form.Modifiers.Concentration:100}
            &&now<session.NextAction?new(form,now,session.NextAction,owner.Map,owner.Location):null;
    }
    internal static bool PreventRecovery(PlayerMobile owner,long now)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCombat.Enabled(owner)){return false;}
        var action=ClassCombat.Existing(owner)?.FormAction;
        return action!=null&&now>=action.Started&&now<action.Until&&action.Form.UntilUtc>TimedEffects.Now
            &&ReferenceEquals(action.Form,DruidPlayerForms.State(owner))&&owner.Map==action.Map&&owner.Location==action.Origin;
    }
}
