// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using System.Runtime.CompilerServices;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;

namespace Sanctuary;

internal sealed class BlackthornKeepClosure(Map map, Region parent)
    : BaseRegion("Blackthorn's old castle", map, parent, parent.Priority + 1, _bridge, _keep)
{
    internal const string Warning = "The old castle is off-limits to visitors. It's fallen into disrepair, and it isn't safe inside.";
    internal static readonly Point3D BridgeApproach = new(1523, 1506, 5);
    private static readonly Point3D[] _guardPosts = [new(1520, 1506, 5), new(1526, 1506, 5)];
    private static readonly Rectangle3D _bridge = new(1517, 1505, MinZ, 12, 1, MaxZ - MinZ);
    private static readonly Rectangle3D _keep = new(1521, 1410, MinZ, 19, 34, MaxZ - MinZ);
    private readonly ConditionalWeakTable<PlayerMobile, WarningCooldown> _warnings = new();

    private sealed class WarningCooldown
    {
        internal long Until;
    }

    internal static void Install()
    {
        var map = Map.Trammel;
        var parent = Region.Find(BridgeApproach, map);
        new BlackthornKeepClosure(map, parent).Register();

        foreach (var player in World.Mobiles.Values.OfType<PlayerMobile>())
        {
            if (player.Deleted || player.AccessLevel != AccessLevel.Player)
                continue;
            if (player.Map == Map.Internal && player.LogoutMap == map && _keep.Contains(player.LogoutLocation))
                player.LogoutLocation = BridgeApproach;
            else if (player.Map == map && _keep.Contains(player.Location))
                player.MoveToWorld(BridgeApproach, map);
        }

        foreach (var door in World.Items.Values.OfType<MetalDoor>().Where(door => !door.Deleted && door.Map == map))
        {
            var closed = door.Open
                ? new Point3D(door.X - door.Offset.X, door.Y - door.Offset.Y, door.Z - door.Offset.Z)
                : door.Location;
            if (closed is { X: 1523 or 1524, Y: 1439, Z: 15 })
            {
                door.Open = false;
                door.Locked = true;
            }
        }

        for (int i = 0; i < _guardPosts.Length; i++)
        {
            var post = _guardPosts[i];
            if (!World.Mobiles.Values.OfType<BlackthornBridgeGuard>()
                .Any(guard => !guard.Deleted && guard.Map == map && guard.Location == post))
                new BlackthornBridgeGuard(i == 1).MoveToWorld(post, map);
        }
    }

    public override bool OnMoveInto(Mobile mobile, Direction direction, Point3D next, Point3D previous)
    {
        if (mobile is not PlayerMobile { AccessLevel: AccessLevel.Player } player
            || _bridge.Contains(next) && previous.Y < next.Y)
            return base.OnMoveInto(mobile, direction, next, previous);

        var cooldown = _warnings.GetValue(player, _ => new WarningCooldown());
        if (Core.TickCount >= cooldown.Until)
        {
            cooldown.Until = Core.TickCount + 3000;
            foreach (var guard in Map.GetMobilesInRange<BlackthornBridgeGuard>(player.Location, 6))
            {
                if (!player.CanSee(guard) || !player.InLOS(guard))
                    continue;
                guard.Warn(player);
                return false;
            }
            player.SendMessage(Warning);
        }
        return false;
    }

    public override bool CheckTravel(Mobile mobile, Point3D destination, TravelCheckType travel, out TextDefinition message)
    {
        if (mobile is PlayerMobile { AccessLevel: AccessLevel.Player } && Contains(destination)
            && travel is TravelCheckType.RecallTo or TravelCheckType.GateTo or TravelCheckType.TeleportTo)
        {
            message = Warning;
            return false;
        }
        return base.CheckTravel(mobile, destination, travel, out message);
    }
}

[SerializationGenerator(0)]
public partial class BlackthornBridgeGuard : BaseCreature
{
    internal BlackthornBridgeGuard(bool female) : base(AIType.AI_Vendor, FightMode.None, 10, 1)
    {
        Name = female ? "Ada" : "Rowan";
        Title = "the Britain city guard";
        Female = female;
        Body = female ? 0x191 : 0x190;
        Hue = 0x83EF;
        CantWalk = Blessed = true;
        Direction = Direction.South;
        AddItem(new RingmailChest());
        AddItem(new Bascinet());
        AddItem(new Halberd());
        AddItem(new Boots());
    }

    public override bool IsInvulnerable => true;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && player.Map == Map && player.InRange(this, 3)
            && player.CanSee(this) && player.InLOS(this))
            Warn(player);
    }

    internal void Warn(PlayerMobile player)
    {
        if (player.NetState is { Running: true } connection)
            PrivateOverheadMessage(MessageType.Regular, 0x59, false, BlackthornKeepClosure.Warning, connection);
    }
}
