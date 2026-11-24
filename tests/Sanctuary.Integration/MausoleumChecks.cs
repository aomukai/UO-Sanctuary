using System;
using System.Linq;
using Server;
using Server.Commands;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

// Run on the disposable client shard. The connected Avatar exercises the actual
// region entry and dialogue reply before the phase transitions are advanced.
public static class MausoleumChecks
{
    private static int _lifeBeforeShot;
    private static Point3D _startingLocation;
    private static Point3D _firePost;

    public static void Configure() => ConsoleInputHandler.RegisterCommand("mausoleum-check", "Exercise the Deceit finale",
        args => Core.LoopContext.Post(() =>
        {
            try { Check(args); LogFactory.GetLogger(typeof(MausoleumChecks)).Information("mausoleum-check: passed"); }
            catch (Exception ex) { LogFactory.GetLogger(typeof(MausoleumChecks)).Error(ex, "mausoleum-check: failed"); }
        }));

    private static void Check(string stage)
    {
        var hall = ArchbishopLazarus.Hall;
        var map = Map.Trammel;

        void Require(bool ok, string detail) { if (!ok) throw new InvalidOperationException(detail); }

        if (stage == "reload")
        {
            var restored = hall.Boss as ArchbishopLazarus;
            Require(restored is { Challenged: true, Stop: 0, FallenGuards: 3, Blessed: false } &&
                restored.Home == restored.Location, "active circle lost on reload");
            var followers = World.Mobiles.Values.OfType<MausoleumDemon>().Where(d => d.Alive).ToArray();
            Require(followers.Select(d => d.Role).Order().SequenceEqual(new[] { 3, 4, 5, 6 }), "saved guardians");
            restored.Delete();
            Require(followers.All(d => d.Deleted), "orphan guardians after reset");
            return;
        }
        var player = World.Mobiles.Values.OfType<PlayerMobile>().Single(p => p.NetState?.Running == true);
        if (stage == "enter")
        {
            var oldLeft = new CampaignArenaTile(0x2A9B, 0);
            var oldRight = new CampaignArenaTile(0x2A9A, 0);
            oldLeft.MoveToWorld(new Point3D(hall.Origin.X+45, hall.Origin.Y+46, 0), map);
            oldRight.MoveToWorld(new Point3D(hall.Origin.X+46, hall.Origin.Y+46, 0), map);
            // Recreate a floor and a dropped item at their old save height.
            var landing = hall.Offset(44, 42);
            CampaignArenaTile oldFloor = null;
            foreach (var tile in map.GetItemsAt<CampaignArenaTile>(landing))
                if (tile.ItemID == hall.Floor) oldFloor = tile;
            oldFloor.Z = 0;
            var dropped = new Server.Items.Gold(1);
            dropped.MoveToWorld(new Point3D(landing.X, landing.Y, 0), map);
            // The formerly broadside stair at this location must come back down.
            var nave = hall.Offset(44, 52);
            CampaignArenaTile oldStep = null;
            foreach (var tile in map.GetItemsAt<CampaignArenaTile>(nave))
                if (tile.ItemID == hall.Floor) oldStep = tile;
            oldStep.Z = 4;
            var offering = new Server.Items.Gold(1);
            offering.MoveToWorld(oldStep.Location, map);
            var oldAnchor = new CampaignArenaTile(WorldDecorationGraphics.MausoleumAltar, 0);
            oldAnchor.MoveToWorld(hall.Offset(46, 44), map);
            var oldBlocker = new CampaignArenaTile(WorldDecorationGraphics.MausoleumAltarBlocker, 0);
            oldBlocker.MoveToWorld(hall.Offset(46, 43), map);
            var coinAtAltar = new Server.Items.Gold(1);
            coinAtAltar.MoveToWorld(hall.Offset(44, 44), map);
            var coldPier = hall.Offset(20,20);
            CampaignArenaTile pierStone = null;
            foreach (var tile in map.GetItemsAt<CampaignArenaTile>(coldPier))
                if (tile.ItemID == 0x027E) pierStone = tile;
            Require(pierStone != null, "missing cold pier");
            pierStone.Delete();
            var coinAtPier = new Server.Items.Gold(1);
            coinAtPier.MoveToWorld(coldPier, map);
            LazarusMausoleum.Build(map, hall);
            Require(coinAtPier.Location == hall.Offset(15,20), "loose item buried under fourth pier");
            coinAtPier.Delete();
            Require(coinAtAltar.Location == hall.Offset(44,45), "loose item stuck under aligned altar");
            coinAtAltar.Delete();
            Require(nave.Z == 0 && oldStep.Location == nave && offering.Location == nave,
                "old stair or its contents left suspended");
            offering.Delete();
            Require(oldAnchor.Deleted && oldBlocker.Deleted, "old altar footprint remained");
            Require(oldFloor.Location == landing && dropped.Location == landing, "flat-floor save migration");
            dropped.Delete();
            Require(oldLeft.Deleted && oldRight.Deleted, "old altar remained after rebuild");
            var altar = hall.Offset(45, 45);
            int count = 0;
            foreach (var tile in map.GetItemsAt<CampaignArenaTile>(altar))
                if (tile.ItemID == WorldDecorationGraphics.MausoleumAltar) ++count;
            Require(count == 1, "altar missing or duplicated");
            for (int x = 42; x <= 47; x++)
            for (int y = 42; y <= 47; y++)
            {
                var p = hall.Offset(x, y);
                Require(map.CanFit(p.X, p.Y, p.Z, 16, requireSurface: true)
                    != WorldDecorationGraphics.InAltarFootprint(x,y), $"altar footprint at {p}");
            }
            Require(LazarusMausoleum.Piers.Length == 4, "expected four columns");
            foreach (var pier in LazarusMausoleum.Piers)
            {
                var a = hall.Offset(pier.X - 6, pier.Y);
                var b = hall.Offset(pier.X + 6, pier.Y);
                Require(!EncounterProfiles.ClearSight(map, a, b), $"sight through pier at {pier}");
                Require(EncounterProfiles.ClearSight(map, hall.Offset(pier.X - 6, pier.Y + 6),
                    hall.Offset(pier.X + 6, pier.Y + 6)), "passage beside pier");
            }
            for (int i = 0; i < 5; i++)
            {
                var p = LazarusMausoleum.Circle(hall, i);
                foreach (var origin in new[] { hall.Center, LazarusMausoleum.Circle(hall, (i + 4) % 5) })
                {
                    int dx = p.X - origin.X, dy = p.Y - origin.Y;
                    Require(Math.Abs(22 * (dx - dy)) > 1026 || Math.Abs(22 * (dx + dy)) > 606,
                        $"seal {i} visible from {origin} at 1920 × 1080 and 1× zoom");
                    Require(Math.Abs(22 * (dx - dy)) > 866 || Math.Abs(22 * (dx + dy)) > 666,
                        $"seal {i} visible from {origin} at 1600 × 1200 and 1× zoom");
                }
                Require(map.CanFit(p.X, p.Y, 0, 16, requireSurface: true), $"circle {i} obstructed");
            }
            _startingLocation = player.Location;
            player.Blessed = true;
            player.MoveToWorld(hall.Offset(40, 53), map);
            Require(player.Z == 0, "chancel approach");
            foreach (var direction in new[] { Direction.North, Direction.South })
            {
                player.Direction = direction;
                for (int n = 0; n < 6; n++)
                {
                    Require(player.Move(direction), $"chancel stair blocked at {player.Location}");
                    Require(player.Z == hall.Offset(player.X-hall.Origin.X, player.Y-hall.Origin.Y).Z,
                        $"walked under a chancel tread at {player.Location}");
                }
                Require(player.Z == (direction == Direction.North ? 6 : 0), "incorrect stair destination");
            }
            Require(hall.Offset(55,44).Z > 0 && hall.Offset(44,55).Z == 0, "long dimension must run east–west");
            Require(hall.Entry.Z == 0, "entrance must meet the nave floor");
            player.MoveToWorld(hall.Entry, map);
            return;
        }
        var boss = (ArchbishopLazarus)hall.Boss;
        var guards = World.Mobiles.Values.OfType<MausoleumDemon>().Where(d => d.IsBodyguard && d.Alive).OrderBy(d => d.Role).ToArray();
        if (stage == "greeting")
        {
            Require(!boss.CanBeDamaged() && guards.Length == 2 && guards.All(d => d.Frozen && d.Blessed), "altar greeting");
            Require(boss.Location == hall.BossPost && guards[0].Location == hall.Offset(42, 42)
                && guards[1].Location == hall.Offset(48, 44), "altar tableau");
            Require(!player.CanBeHarmful(boss, false), "boss targetable before fight");
            var access = player.AccessLevel;
            player.AccessLevel = AccessLevel.GameMaster;
            player.Hidden = true;
            boss.Challenge(player);
            player.AccessLevel = access;
            player.Hidden = false;
            Require(!boss.Challenged, "hidden GM released the court");
            foreach (var guard in guards)
            {
                var post = guard.Home;
                guard.MoveToWorld(hall.Entry, map);
                guard.Frozen = guard.CantWalk = false;
                guard.Direction = Direction.South;
                Require(!guard.Move(Direction.South), "waiting guard wandered without a freeze flag");
                boss.PlaceBodyguards();
                Require(guard.Location == post && guard.CantWalk, "waiting guard failed to return to post");
            }
        }
        else if (stage == "view")
        {
            player.MoveToWorld(hall.Offset(36, 49), map);
        }
        else if (stage == "guards")
        {
            Require(boss.Challenged && guards.All(d => !d.Frozen && !d.Blessed), "bodyguards did not engage after dialogue");
            foreach (var guard in guards)
            {
                guard.Direction = Direction.West;
                Require(!guard.CantWalk && guard.Move(Direction.West), "bodyguard could not move after the challenge");
            }
            guards[0].Kill();
            Require(boss.FallenGuards == 1 && boss.Blessed && boss.Stop == -1, "first death opened ward");
            guards[1].Kill();
            Require(boss.Stop == 0 && !boss.Blessed && boss.InRange(LazarusMausoleum.Circle(hall, 0), 1), $"first teleport: mask {boss.FallenGuards}, stop {boss.Stop}, blessed {boss.Blessed}, location {boss.Location}, player {player.Location} alive {player.Alive} hidden {player.Hidden}");
            player.MoveToWorld(hall.Offset(9, 53), map);
        }
        else if (stage == "circuit")
        {
            for (int i = 0; i < 5; i++)
            {
                var element = LazarusMausoleum.Circles[i].Element;
                if (boss.NextSummon != DateTime.MaxValue) boss.Advance(boss.NextSummon);
                Require(boss.Stop == i && boss.Warded(element), $"missing guardian for {i}");
                Require(NamedElements.Resistance(player, boss, "mag", 30) < 100, "unrelated magic ward");
                boss.Advance(boss.NextTravel);
            }
            Require(boss.Stop == 0 && boss.Warded(ResistanceType.Fire), "surviving guardian on second circuit");
            var fire = World.Mobiles.Values.OfType<MausoleumDemon>().Single(d => d.Role == 2);
            fire.Kill();
            Require(!boss.Warded(ResistanceType.Fire), "dead guardian sustained ward");
            // Let the spirits from the accelerated five-stop circuit retire before the cover test.
            player.MoveToWorld(_startingLocation, map);
        }
        else if (stage == "cold-cover")
        {
            foreach (var demon in World.Mobiles.Values.OfType<MausoleumDemon>()) demon.Frozen = true;
            _firePost = boss.Location;
            boss.MoveToWorld(LazarusMausoleum.Circle(hall, 1), map);
            player.MoveToWorld(hall.Offset(28, 28), map);
            player.Blessed = false;
            player.RawStr = 450;
            player.Hits = player.HitsMax;
            _lifeBeforeShot = player.Hits;
            new MausoleumSpirit(boss, player).Launch();
        }
        else if (stage == "far-column")
        {
            Require(player.Hits == _lifeBeforeShot && player.Alive, "Bone Spirit crossed the fourth column");
            boss.MoveToWorld(_firePost, map);
            player.Blessed = true;
            player.MoveToWorld(hall.Offset(10, 11), map);
        }
        else if (stage == "cover")
        {
            foreach (var demon in World.Mobiles.Values.OfType<MausoleumDemon>()) demon.Frozen = true;
            // Fire to (38,67) crosses the western pier at (26,60).
            player.MoveToWorld(hall.Offset(38, 67), map);
            player.Blessed = false;
            player.RawStr = 450;
            player.Hits = player.HitsMax;
            _lifeBeforeShot = player.Hits;
            new MausoleumSpirit(boss, player).Launch();
        }
        else if (stage == "open")
        {
            Require(player.Hits == _lifeBeforeShot && player.Alive, "Bone Spirit passed through cover");
            player.MoveToWorld(hall.Offset(30, 49), map);
            new MausoleumSpirit(boss, player).Launch();
        }
        else if (stage == "sidestep")
        {
            for (int i = 0; i < 4; ++i)
                Require(player.Move(Direction.South | Direction.Running), "failed to run on the mausoleum floor");
        }
        else if (stage == "impact")
        {
            Require(player.Hits < _lifeBeforeShot || CampaignArenas.At(player.Map, player.Location) != hall,
                "Bone Spirit missed the player after their sidestep");
        }
        else throw new ArgumentException(stage);
    }
}
