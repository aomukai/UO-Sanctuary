// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CampaignExitChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(CampaignExitChecks));

    public static void Configure() =>
        ConsoleInputHandler.RegisterCommand("campaign-exit-check", "Check the three campaign victory gates", _ =>
            Core.LoopContext.Post(Check));

    private static void Check()
    {
        var avatar = new PlayerMobile { Body = 0x190, Player = true };
        var pet = new Dog();
        var map = Map.Trammel;
        var landing = CampaignDungeonRoutes.HythlothSurfaceLanding;
        var arenas = CampaignArenas.All;
        try
        {
            Require(pet.SetControlMaster(avatar), "Could not bind test follower");
            foreach (var arena in arenas)
            {
                Require(arena.ExitGate == null && !arena.Cleared, $"Early exit gate in {arena.Name}");
                CampaignArenas.EnsureBoss(arena);
                var boss = arena.Boss;
                var point = arena.ExitPoint;
                Require(boss.Alive && map.CanFit(point, 16, checkMobiles: false), $"Unusable gate spot in {arena.Name}");
                avatar.MoveToWorld(point, map);
                if (boss is ArchbishopLazarus)
                {
                    boss.Blessed = false; // His ritual removes invulnerability before the fight.
                    boss.MoveToWorld(LazarusMausoleum.Circle(arena, 0), map);
                }
                boss.Kill();
                var gate = arena.ExitGate;
                Require(arena.Cleared && gate is { Visible: true, Movable: false, ItemID: 0xDDA }
                    && gate.Location == point && gate.PointDest == landing && gate.MapDest == map,
                    $"Victory did not open the red exit in {arena.Name}");
                CampaignArenas.Defeated(boss);
                Require(arena.ExitGate == gate, $"Duplicate exit in {arena.Name}");

                pet.MoveToWorld(point, map);
                pet.IssueOrder(OrderType.Follow, null, avatar);
                if (arena.IsDiablo)
                {
                    var approach = arena.Offset(DiabloCave.Midpoint - 5, DiabloCave.Midpoint + 3);
                    avatar.MoveToWorld(approach, map);
                    pet.MoveToWorld(approach, map);
                    avatar.Direction = Direction.East;
                    avatar.Move(Direction.East);
                }
                else
                    gate.OnDoubleClick(avatar);
                Require(avatar.Map == map && avatar.Location == landing, $"Wrong destination from {arena.Name}");
                Require(pet.Map == map && pet.InRange(landing, 2), $"Follower remained in {arena.Name}");
                arena.EmptySince = Core.Now.AddMinutes(-1);
            }

            var exits = arenas.Select(arena => arena.ExitGate).ToArray();
            Timer.DelayCall(TimeSpan.FromSeconds(5), () =>
            {
                try
                {
                    Require(exits.All(gate => gate.Deleted) && arenas.All(arena => !arena.Cleared && arena.ExitGate == null),
                        "An exit gate remained after its room reset");
                    var throne = arenas[0];
                    avatar.MoveToWorld(throne.ExitPoint, map);
                    pet.MoveToWorld(throne.ExitPoint, map);
                    CampaignArenas.RestoreExit(avatar);
                    Require(throne.ExitGate == null, "An uncredited character opened an exit on return");
                    BritanniaCampaign.Record(avatar, throne.Victory);
                    CampaignArenas.RestoreExit(avatar);
                    Require(throne.Cleared && throne.ExitGate is { Deleted: false }, "A returning victor could not reopen the exit");
                    throne.ExitGate.OnDoubleClick(avatar);
                    Require(avatar.Location == landing, "The restored exit did not return to Hythloth");
                    Log.Information("campaign-exit-check: passed");
                }
                catch (Exception error)
                {
                    Log.Error(error, "campaign-exit-check: failed");
                }
                finally
                {
                    pet.Delete();
                    avatar.Delete();
                }
            });
        }
        catch (Exception error)
        {
            pet.Delete();
            avatar.Delete();
            Log.Error(error, "campaign-exit-check: failed");
        }
    }

    private static void Require(bool passed, string detail)
    {
        if (!passed)
            throw new InvalidOperationException(detail);
    }
}
