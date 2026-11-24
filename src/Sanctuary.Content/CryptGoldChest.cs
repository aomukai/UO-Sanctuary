// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class CryptGoldChest : MetalGoldenChest
{
    public override string DefaultName => "a crypt treasure chest";
    public override bool IsDecoContainer => false;

    internal CryptGoldChest(int amount)
    {
        Movable = false;
        DropItem(new Gold(amount));
    }

    private bool CanReach(Mobile from) => from is PlayerMobile { Alive: true, Deleted: false }
        && !Deleted && Parent == null && from.Map == Map && from.InRange(this, 2)
        && Math.Abs(from.Z - Z) < 16 && from.CanSee(this) && from.InLOS(this);

    public override void OnDoubleClick(Mobile from)
    {
        if (CanReach(from))
            base.OnDoubleClick(from);
        else
            from.SendLocalizedMessage(500446); // That is too far away.
    }

    public override void DisplayTo(Mobile to)
    {
        if (to.NetState is { } connection && CanReach(to))
        {
            const int length = 5 + LootProtocol.ChestAnnouncementLength;
            Span<byte> data = stackalloc byte[length];
            var writer = new SpanWriter(data);
            writer.Write((byte)0xBF);
            writer.Write((ushort)length);
            writer.Write(LootProtocol.SubCommand);
            writer.Write(LootProtocol.Version);
            writer.Write(Serial.Value);
            connection.Send(data);
        }
        base.DisplayTo(to);
    }

    public override bool OnDragDrop(Mobile from, Item dropped) => false;
    public override bool OnDragDropInto(Mobile from, Item item, Point3D p) => false;

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
    {
        if (from.AccessLevel >= AccessLevel.GameMaster || CanReach(from) && Openers?.Contains(from) == true)
            return base.CheckLift(from, item, ref reject);
        reject = LRReason.CannotLift;
        return false;
    }

    internal bool TakeGold(PlayerMobile from, Gold gold)
    {
        if (gold.Parent != this || gold.Deleted || !gold.Movable || !CanReach(from)
            || Openers?.Contains(from) != true || !from.CanSee(gold) || !gold.IsAccessibleTo(from))
            return false;
        return ClassLoot.MoveLootToBackpack(from, this, gold);
    }
}
