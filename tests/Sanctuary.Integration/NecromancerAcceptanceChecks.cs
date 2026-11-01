// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class NecromancerAcceptanceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerAcceptanceChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("necromancer-acceptance-save",
        "Prepare both barrier and Fire retirement fixtures before one real save",_=>Core.LoopContext.Post(()=>
        {
            try
            {
                BoneBarrierClockChecks.StageSave();
                BoneWallMakerChecks.StageSave();
                BoneBarrierBirthChecks.PrepareSave();
                FireGolemDeathChecks.PrepareSave();
                World.Save();
                Log.Information("necromancer-acceptance-save: passed");
            }
            catch(Exception e){Log.Error(e,"necromancer-acceptance-save: failed");}
        }));
}
