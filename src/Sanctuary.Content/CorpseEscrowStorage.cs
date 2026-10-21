// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class CorpseEscrowStorage : Container
{
    [Constructible]
    public CorpseEscrowStorage() : base(0xE75)
    { Name = "Private corpse escrow"; Movable = Visible = false; }
    public override int DefaultMaxItems => 0;
    public override int DefaultMaxWeight => 0;
    public override double DefaultWeight => 0;
    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) => false;
    public override bool CheckContentDisplay(Mobile from) => false;
    public override void OnDoubleClick(Mobile from) { }
    public override bool OnDragDrop(Mobile from, Item dropped) => false;
    public override bool OnDragDropInto(Mobile from, Item item, Point3D point) => false;
}
