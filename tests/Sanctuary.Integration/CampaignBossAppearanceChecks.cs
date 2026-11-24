using System;
using System.Linq;
using Server;
using Server.Commands;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

// Disposable-world console fixture. A connected client can compare the native daemon
// death with the imported bodies at the same zoom and check their actual ground pivots.
public static class CampaignBossAppearanceChecks
{
    public static void Configure() => ConsoleInputHandler.RegisterCommand("boss-model-check", "Inspect campaign boss artwork",
        stage => Core.LoopContext.Post(() =>
        {
            try { Check(stage); LogFactory.GetLogger(typeof(CampaignBossAppearanceChecks)).Information("boss-model-check: passed"); }
            catch (Exception ex) { LogFactory.GetLogger(typeof(CampaignBossAppearanceChecks)).Error(ex, "boss-model-check: failed"); }
        }));

    private static void Check(string stage)
    {
        var viewer = World.Mobiles.Values.OfType<PlayerMobile>().Single(p => p.NetState?.Running == true);
        viewer.Hidden = viewer.Blessed = true;
        var map = Map.Trammel;
        if (stage == "daemon")
        {
            var location = viewer.Location;
            var daemon = new Daemon { CantWalk = true, Direction = Direction.Down };
            daemon.MoveToWorld(new Point3D(location.X - 2, location.Y - 2, location.Z), viewer.Map);
            daemon.Kill();
            if (daemon.Corpse is not Corpse corpse || corpse.Map != viewer.Map)
                throw new InvalidOperationException("Missing native daemon corpse");
            return;
        }

        var arena = CampaignArenas.All.Single(a => a.Name.Equals(stage.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
        var post = arena.BossPost;
        viewer.MoveToWorld(new Point3D(post.X + 3, post.Y + 3, post.Z), map);
        CampaignArenas.EnsureBoss(arena);
        var boss = arena.Boss;
        int expected = arena.IsDiablo ? CampaignBossAppearance.Diablo : arena.IsLazarus ? CampaignBossAppearance.Lazarus : CampaignBossAppearance.Leoric;
        if (boss.Body != expected || boss.Hue != 0 || !boss.Body.IsMonster)
            throw new InvalidOperationException($"Unexpected appearance {boss.Body}/{boss.Hue}/{boss.Body.Type}");
        // Keep autonomous idle turns from changing the subject during a camera take.
        boss.AIObject.AITimer.Stop();
        if (stage.EndsWith(" turn"))
        {
            boss.Direction = Direction.Up;
            return;
        }
        var action = stage.Split(' ').Last();
        if (action is "walk" or "walk-right" or "walk-front")
        {
            // Exercise the movement packets that drive a step, including the pauses between them.
            var start = boss.Location;
            var direction = action switch
            {
                "walk-right" => Direction.Right,
                "walk-front" => Direction.Down,
                _ => Direction.Left
            };
            int distance = action == "walk-front" ? 2 : 5;
            boss.Direction = direction;
            int steps = 0;
            Timer.StartTimer(TimeSpan.FromMilliseconds(450), TimeSpan.FromMilliseconds(340), distance, () =>
            {
                boss.CantWalk = false;
                bool moved = boss.Move(direction);
                boss.CantWalk = true;
                if (moved) steps++;
                if (steps == distance && boss.Location != start)
                    LogFactory.GetLogger(typeof(CampaignBossAppearanceChecks)).Information($"boss-model-check: walked {distance} tiles {direction}");
            });
            return;
        }
        boss.CantWalk = true;
        boss.Direction = Direction.Down;
        if (stage.EndsWith(" strike"))
            boss.Animate(arena.IsLazarus ? 12 : 4, 6, 1, true, false, 0);
        else if (stage.EndsWith(" death"))
        {
            boss.Direction = Direction.Down;
            boss.Kill();
            if (boss.Corpse?.Map != map) throw new InvalidOperationException("Missing boss corpse");
        }
        else if (arena.IsLazarus)
        {
            // Applying the appearance to an old save must preserve the ward and current health.
            int health = boss.Hits;
            boss.Body = 0x190;
            boss.AddItem(new Robe(0x497));
            boss.AddItem(new Sandals());
            boss.HairItemID = 0x203C;
            boss.ApplyAppearance(arena);
            if (!boss.Blessed || boss.Hits != health || boss.Body != expected || boss.HairItemID != 0 ||
                boss.FindItemOnLayer(Layer.OuterTorso) != null || boss.FindItemOnLayer(Layer.Shoes) != null)
                throw new InvalidOperationException("Legacy Lazarus appearance migration");
        }
    }
}
