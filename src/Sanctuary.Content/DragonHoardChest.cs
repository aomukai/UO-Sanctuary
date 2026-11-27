// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class DragonHoardChest : WoodenChest
{
    [SerializableField(0, setter: "private")] private DateTime _refillAt;
    private Timer _refill;

    [Constructible]
    public DragonHoardChest() => Movable = false;

    public override string DefaultName => "a hoard chest";
    public override bool IsDecoContainer => false;

    public override void OnMapChange()
    {
        base.OnMapChange();
        ScheduleRefill();
    }

    public override void OnItemRemoved(Item item)
    {
        base.OnItemRemoved(item);
        if (Deleted || IsInternal || Items.Count > 0) { return; }

        RefillAt = Core.Now.AddMinutes(Utility.RandomMinMax(10, 30));
        ScheduleRefill();
    }

    public override bool CheckHold(Mobile from, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
    {
        if (from.AccessLevel >= AccessLevel.GameMaster)
            return base.CheckHold(from, item, message, checkItems, plusItems, plusWeight);
        if (message) { from.SendMessage("You cannot put items in the hoard chest."); }
        return false;
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        _refill?.Stop();
    }

    private bool IsInternal => Map == null || Map == Server.Map.Internal;

    [AfterDeserialization]
    private void ScheduleRefill()
    {
        _refill?.Stop();
        _refill = null;
        if (Deleted || IsInternal || Items.Count > 0) { return; }

        // A newly placed chest is stocked immediately. Once emptied, its deadline survives a restart.
        var delay = RefillAt > Core.Now ? RefillAt - Core.Now : TimeSpan.Zero;
        _refill = Timer.DelayCall(delay, Refill);
    }

    private void Refill()
    {
        _refill = null;
        if (Deleted || IsInternal || Items.Count > 0) { return; }

        DropItem(RollTreasure(Random.Shared));
        RefillAt = default;
    }

    private static Item RollTreasure(Random random) => random.Next(10) switch
    {
        < 2 => new Gold(random.Next(120, 401)),
        2 => AlchemyRestoratives.Drop("hp3"),
        3 => AlchemyRestoratives.Drop("mp3"),
        _ => EncounterTreasure.RollEquipment("Act 2 Uitem B", 30, 0, random)
    };
}
