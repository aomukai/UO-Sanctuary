// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public static class Identification
{
    public static CraftItem Recipe { get; private set; }
    [CallPriority(101)]
    public static void Initialize()
    {
        // Native Item Identification and identification wands set only their native
        // flags; Sanctuary's saved state is revealed by this consumable route.
        var nativeUse = SkillInfo.Table[(int)SkillName.ItemID].Callback;
        SkillInfo.Table[(int)SkillName.ItemID].Callback = from =>
        {
            if (from is PlayerMobile player && ClassCombat.Enabled(player))
            { from.SendMessage("Use an Identify scroll from your backpack."); return TimeSpan.FromSeconds(1); }
            return nativeUse?.Invoke(from) ?? TimeSpan.FromSeconds(1);
        };
        if (!ClassProfessions.Enabled) { return; }
        var system = DefInscription.CraftSystem;
        int index = system.AddCraft(typeof(IdentifyScroll), "Utility scrolls", "Identify scroll", 0, 50,
            typeof(BlankScroll), "Blank Scroll", 1, "You need a blank scroll.");
        system.AddRes(index, typeof(BlackPearl), "Black Pearl", 1, "You need black pearl.");
        Recipe = system.CraftItems.SearchFor(typeof(IdentifyScroll));
    }
    internal static bool ScrollReady(PlayerMobile p, IdentifyScroll scroll) => EquipmentEconomy.Ready(p)
        && scroll is { Deleted: false, Movable: true, Amount: 1, IsLockedDown: false, IsSecure: false }
        && scroll.IsChildOf(p.Backpack) && scroll.IsAccessibleTo(p) && scroll.VerifyMove(p);
    internal static bool Reveal(PlayerMobile p, IdentifyScroll scroll, Item item)
    {
        if (!ScrollReady(p, scroll) || item is not { Deleted: false, Amount: 1 }
            || !item.IsChildOf(p.Backpack) || !item.IsAccessibleTo(p)
            || !ClassGear.Converted(item) || ClassGear.State(item) is not { Identified: false } state) { return false; }
        // Store the same roll and existing sale offer. No generation or price roll.
        ClassGear.Profiles.Store(item, state with { Identified = true });
        scroll.Consume(); p.SendMessage("The item's properties have been revealed.");
        return true;
    }
    internal static void Use(PlayerMobile p, IdentifyScroll scroll)
    {
        if (!ScrollReady(p, scroll) || p.Target != null) { return; }
        p.SendMessage("Target unidentified equipment in your backpack. The scroll is used only on success.");
        p.Target = new IdentifyTarget(p.Serial, scroll.Serial, scroll.Parent, p.NetState);
    }
    private sealed class IdentifyTarget(Serial owner, Serial scroll, object parent, NetState connection) : Target(1, false, TargetFlags.None)
    {
        private bool _used;
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_used) { return; } _used = true;
            if (from is not PlayerMobile p || p.Serial != owner || !ReferenceEquals(p.NetState, connection)
                || World.FindItem(scroll) is not IdentifyScroll item || !ReferenceEquals(item.Parent, parent)
                || targeted is not Item gear || !Reveal(p, item, gear))
            { from.SendMessage("No scroll was used. Choose unidentified equipment in your own backpack."); }
        }
    }
}

[SerializationGenerator(0)]
public partial class IdentifyScroll : Item
{
    [Constructible]
    public IdentifyScroll() : base(0x1F2D) { Name = "Identify scroll"; Hue = 0x482; Weight = 0.1; }
    public override void OnDoubleClick(Mobile from)
    { if (from is PlayerMobile p) { Identification.Use(p, this); } }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Single use: identify one item in your backpack"}");
    }
}
