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
        PersistentMinions.Resume(player);
        var session = ClassCombat.For(player);
        session.RunningStep = (direction & Direction.Running) != 0;
        session.RunningUntil = Core.TickCount + player.ComputeMovementSpeed(direction, false);
        session.StepPosition = player.Location;
        session.StepMap = player.Map;
    }

    // The selected mode alone never removes defense while standing still. Use only
    // accepted steps, including their duration, so toggling cannot hide a running step.
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
