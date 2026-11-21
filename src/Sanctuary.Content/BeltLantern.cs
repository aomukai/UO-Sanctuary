// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class BeltLantern : Lantern
{
    [Constructible]
    public BeltLantern()
    {
        Name = "Belt lantern";
        ItemID = UnlitItemID;
        Layer = Layer.Talisman;
    }

    public override int LitItemID => BeltLanternGraphics.Lit;
    public override int UnlitItemID => BeltLanternGraphics.Unlit;

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);
        if (parent is Mobile && !Burning)
            Ignite();
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.Alive)
            return;

        if (IsChildOf(from.Backpack))
        {
            if (from.FindItemOnLayer(Layer.Talisman) != null)
                from.SendMessage("Your belt lantern slot is occupied.");
            else
                from.EquipItem(this);
        }
        else if (Parent == from || Parent == null)
            base.OnDoubleClick(from);
    }
}
