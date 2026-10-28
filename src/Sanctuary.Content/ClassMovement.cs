// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class ClassMovement
{
    public static void Moved(PlayerMobile player, Direction direction)
    {
        if (!ClassCombat.Enabled(player))
        {
            return;
        }
        if(player.Map!=null && player.Map!=Map.Internal){PetOwnerRoutes.Moved(player,TimedEffects.Now);}
        PersistentMinions.Resume(player);
        FireGround.Moved(player);
        var session = ClassCombat.For(player);
        session.FormAction=null;
        BladeFuryModes.Interrupt(player);
        if(AssassinCombat.Handles(session.PendingAbility) || DruidCombat.Handles(session.PendingAbility)){session.PendingAbility=CombatAbility.None;}
        // Any accepted external step changes the attack mode; voluntary Concentrate steps are gated.
        BarbarianConcentrate.Cancel(player);
        BarbarianBerserk.CancelMode(player);
        BarbarianDoubleThrow.Cancel(player);
        AmazonJab.Cancel(player);
        TigerStrikes.Cancel(player);
        DruidChargedAttacks.Cancel(player);DruidFireClaws.Cancel(player);DruidHunger.Cancel(player);DruidRabies.Cancel(player);
        AssassinClaws.Cancel(player);
        AmazonImpale.Cancel(player);
        AmazonFend.Cancel(player);
        AmazonMelee.Cancel(player);
        AmazonBow.Cancel(player);
        BarbarianWhirlwind.ExternalMove(player);
        // Bash/Stun are interruptible. Cancel on the accepted step itself,
        // so returning to the origin before the event cannot revive a paid packet.
        if(BarbarianMelee.Handles(session.PendingAbility)){session.PendingAbility=CombatAbility.None;session.BarbarianAttack=null;}
        if (session.PendingTeleport != null) { session.PendingAbility = CombatAbility.None; session.PendingTeleport = null; }
        session.RunningStep = (direction & Direction.Running) != 0;
        session.RunningUntil = Core.TickCount + player.ComputeMovementSpeed(direction, false);
        session.StepPosition = player.Location;
        session.StepMap = player.Map;
    }

    // The selected mode alone never removes defense while standing still. Use only
    // accepted steps, including their duration, so toggling cannot hide a running step.
    public static bool IsMoving(PlayerMobile player,long now)
    {
        if(!ClassCombat.Enabled(player)){return false;}
        var session=ClassCombat.For(player);
        return now-session.RunningUntil<0 && player.Map==session.StepMap && player.Location==session.StepPosition;
    }
    public static bool IsRunning(PlayerMobile player, long now)
    {
        if (!ClassCombat.Enabled(player))
        {
            return false;
        }
        var session = ClassCombat.For(player);
        return session.RunningStep && now - session.RunningUntil < 0
            && player.Map == session.StepMap && player.Location == session.StepPosition;
    }
}
