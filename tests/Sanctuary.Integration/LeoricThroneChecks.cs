using System;
using System.Linq;
using Server;
using Server.Commands;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class LeoricThroneChecks
{
    private static Point3D _return;

    public static void Configure() => ConsoleInputHandler.RegisterCommand("leoric-throne-check", "Exercise Hythloth's throne room",
        stage => Core.LoopContext.Post(() =>
        {
            try { Check(stage); LogFactory.GetLogger(typeof(LeoricThroneChecks)).Information("leoric-throne-check: passed"); }
            catch (Exception ex) { LogFactory.GetLogger(typeof(LeoricThroneChecks)).Error(ex, "leoric-throne-check: failed"); }
        }));

    private static void Check(string stage)
    {
        var hall = CampaignArenas.All[0];
        var map = Map.Trammel;
        var avatar = World.Mobiles.Values.OfType<PlayerMobile>().Single(p => p.NetState?.Running == true);
        void Require(bool passed, string detail) { if (!passed) throw new InvalidOperationException(detail); }

        if (stage == "enter")
        {
            _return = avatar.Location;
            CampaignArenaGate gate = null;
            foreach (var candidate in map.GetItemsAt<CampaignArenaGate>(hall.Door))
            {
                Require(gate == null, "duplicate Hythloth gate");
                gate = candidate;
            }
            Require(gate != null, "missing Hythloth gate");
            var oldEntry = new Point3D(6894, 3812, 0);
            var oldFloor = new CampaignArenaTile(0x51B, 0);
            oldFloor.MoveToWorld(oldEntry, map);
            var token = new Gold(1);
            token.MoveToWorld(oldEntry, map);
            gate.PointDest = oldEntry;
            DiabloCave.RetireArena(map, hall, new Rectangle2D(6890, 3800, 33, 25), oldEntry);
            Require(oldFloor.Deleted && token.Location == hall.Entry && gate.PointDest == hall.Entry, "old hall migration");
            token.Delete();
            int Furnishings()
            {
                int total = 0;
                foreach (var tile in map.GetItemsInBounds<CampaignArenaTile>(hall.Bounds)) total++;
                return total;
            }
            int before = Furnishings();
            // An existing court may have all five native half-banners when the hall is rebuilt.
            foreach (var (x, y, graphic) in new[] { (6, 1, 0x426), (13, 1, 0x426), (19, 1, 0x426), (1, 9, 0x42B), (1, 25, 0x42F) })
                new CampaignArenaTile(graphic, 0).MoveToWorld(new Point3D(hall.Origin.X + x, hall.Origin.Y + y, 12), map);
            var raisedThrone = new CampaignArenaTile(LeoricThroneRoom.Throne, 0);
            var thronePost = hall.Offset(15, 3);
            var oldPost = new CampaignArenaTile(0x00DF, 0);
            oldPost.MoveToWorld(thronePost, map);
            raisedThrone.MoveToWorld(new Point3D(thronePost.X, thronePost.Y, thronePost.Z + 1), map);
            LeoricThroneRoom.Build(map, hall);
            int thrones = 0;
            foreach (var tile in map.GetItemsAt<CampaignArenaTile>(thronePost))
                if (tile.ItemID == LeoricThroneRoom.Throne)
                {
                    Require(tile.Location == thronePost, "throne above the floor");
                    thrones++;
                }
            Require(oldPost.Deleted && raisedThrone.Deleted && thrones == 1, "raised throne migration");
            Require(!map.CanFit(thronePost.X, thronePost.Y, thronePost.Z, 16, requireSurface: true), "walkable throne");
            int standards = 0;
            foreach (var tile in map.GetItemsInBounds<CampaignArenaTile>(hall.Bounds))
            {
                if (tile.ItemID is >= 59995 and <= 59998) ++standards;
                Require(tile.ItemID is not (0x426 or 0x42B or 0x42F), "old half-banner in court");
            }
            Require(standards == 5, "expected five complete wall standards");
            Require(before == Furnishings(), "duplicate furnishings on rebuild");
            avatar.MoveToWorld(hall.Door, map);
            avatar.Blessed = true;
            gate.DoTeleport(avatar);
            Require(avatar.InRange(hall.Entry, 1) && avatar.Region.Name == "Leoric" && avatar.Region.GoLocation == hall.Entry, "gate or GM arrival");
            Require(hall.Boss is { Alive: true } && hall.Boss.Basis.Level == 40 && hall.Boss.Location == hall.BossPost, "king's post");
        }
        else if (stage == "walk")
        {
            avatar.Direction = Direction.North;
            for (int i = 0; i < 24; i++) Require(avatar.Move(Direction.North), $"processional aisle blocked at {avatar.Location}");
            var kings = World.Mobiles.Values.OfType<CampaignBoss>().Where(b => b.Map == map && hall.Bounds.Contains(b.Location)).ToArray();
            Require(kings.Length == 1 && kings[0] == hall.Boss, "duplicate king inside throne room");
            Require(hall.Boss.RepelledByRefuge(hall.Entry) && !hall.Boss.RepelledByRefuge(avatar.Location), "fight or refuge boundary");
            Require(avatar.X == hall.Origin.X + 15 && avatar.Y == hall.Origin.Y + 11, "did not reach the court");
        }
        else if (stage == "leave") avatar.MoveToWorld(_return, map);
        else throw new ArgumentException(stage);
    }
}
