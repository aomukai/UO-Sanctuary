// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Cold-specific facade over the shared transient player sequence controller.
internal static class DruidArcticModes
{
    internal static PlayerInfernoModeCast State(Mobile owner)=>PlayerInfernoModes.State(owner);
    internal static bool Active(Mobile owner)=>PlayerInfernoModes.Active(owner);
    internal static void Cancel(Mobile owner)=>PlayerInfernoModes.Cancel(owner);
    internal static void Interrupt(Mobile owner)=>PlayerInfernoModes.Interrupt(owner);
    internal static bool BeginPrepared(PlayerMobile owner,Serial selected,Point3D point,DateTime at)
        =>PlayerInfernoModes.BeginPrepared(owner,CombatAbility.ArcticBlast,selected,point,at);
    internal static bool Advance(PlayerMobile owner,DateTime at)=>PlayerInfernoModes.Advance(owner,at);
    internal static bool StopIntent(PlayerMobile owner,DateTime at)=>PlayerInfernoModes.StopIntent(owner,at);
    internal static bool StartCallback(PlayerMobile owner,DateTime at)=>PlayerInfernoModes.StartCallback(owner,at);
    internal static bool Aim(PlayerMobile owner,Serial selected,Point3D point)=>PlayerInfernoModes.Aim(owner,selected,point);
}
