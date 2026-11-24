// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RockThrowChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(RockThrowChecks));

    public static void Configure() => ConsoleInputHandler.RegisterCommand(
        "rock-impact-check", "Check rock impacts when damage removes a mobile from the sector",
        args => Core.LoopContext.Post(() =>
        {
            try
            {
                Check(args.Trim() == "granite");
            }
            catch (Exception error)
            {
                Log.Error(error, "rock-impact-check: failed");
            }
        }));

    private static void Check(bool granite)
    {
        var target = new Target();
        var second = new PlayerMobile { Body = 0x190, Player = true };
        var bystander = new PlayerMobile { Body = 0x190, Player = true, Blessed = true };
        EncounterCreature source = granite ? new HavenEncounter(HavenEncounter.GraniteElemental, 8) : new BoulderEttin(18);
        try
        {
            foreach (var player in new PlayerMobile[] { target, second, bystander })
            {
                ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
                ClassCreation.Profiles.AdvanceLevel(player, 30);
                player.Hits = player.HitsMax;
            }
            FixtureSpace.Corridor(target, 5440, 14);
            second.MoveToWorld(target.Location, target.Map);
            bystander.MoveToWorld(new Point3D(target.X + 1, target.Y, target.Z), target.Map);
            source.MoveToWorld(new Point3D(target.X + 5, target.Y, target.Z), target.Map);
            source.AIObject.AITimer.Stop();
            target.RemoveOnHit = bystander;
            int targetHits = target.Hits, secondHits = second.Hits;
            if (!source.BeginFirebolt(target))
                throw new InvalidOperationException("Rock throw did not start.");

            Timer.StartTimer(TimeSpan.FromSeconds(2), () =>
            {
                try
                {
                    if (!bystander.Deleted || target.Hits >= targetHits || second.Hits >= secondHits)
                        throw new InvalidOperationException("The hit did not remove the bystander and damage both targets.");
                    Log.Information("rock-impact-check: passed ({Thrower})", source.Name);
                }
                catch (Exception error)
                {
                    Log.Error(error, "rock-impact-check: failed");
                }
                finally
                {
                    Cleanup();
                }
            });
        }
        catch
        {
            Cleanup();
            throw;
        }

        void Cleanup()
        {
            source.Delete();
            target.Delete();
            second.Delete();
            bystander.Delete();
        }
    }

    private sealed class Target : PlayerMobile
    {
        public Target() { Body = 0x190; Player = true; }
        public Target(Serial serial) : base(serial) { }
        internal Mobile RemoveOnHit { get; set; }

        public override void OnDamage(int amount, Mobile from, bool willKill)
        {
            base.OnDamage(amount, from, willKill);
            RemoveOnHit?.Delete();
        }
    }
}
