// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class ClassRespec
{
    public const int GoldCost = 5000;
    public static Item NearbyWardrobe(PlayerMobile player) => WardrobeAccess.NearbyWardrobe(player);
    public static bool SourceValid(PlayerMobile player, Item wardrobe) => WardrobeAccess.SourceValid(player, wardrobe);
    public static bool Ready(PlayerMobile player, Item wardrobe) => WardrobeAccess.Ready(player, wardrobe)
        && TownRecognition.Profiles.Find(player).Earned == RecognitionHonor.Both;
    public static void DisplayTo(PlayerMobile player, Item wardrobe)
    {
        if (!Ready(player, wardrobe))
        { player.SendMessage("Point resets require both ceremonies and a nearby wardrobe while alive and out of combat, with no pending action."); return; }
        var profiles = ClassCreation.Profiles;
        profiles.TryGet(player.Serial, out var active);
        if (profiles.Stats(player.Serial).Total == 0 && profiles.AvailablePoints(player.Serial) == active.Level - 1)
        { player.SendMessage("You have no allocated class points to reset."); return; }
        if (player.NetState != null) { player.CloseGump<WardrobeGearGump>(); player.CloseGump<RecognitionGump>(); player.CloseGump<WardrobeGump>(); player.SendGump(new RespecGump(player, wardrobe)); }
    }
    public static bool Commit(PlayerMobile player, Item wardrobe, CharacterProfile quoted, StatAllocation stats, int skillPoints)
    {
        var profiles = ClassCreation.Profiles;
        if (!Ready(player, wardrobe) || !profiles.TryGet(player.Serial, out var active) || active != quoted
            || profiles.Stats(player.Serial) != stats || profiles.AvailablePoints(player.Serial) != skillPoints
            || stats.Total == 0 && skillPoints == active.Level - 1) { return false; }
        if (player.Backpack.GetAmount(typeof(Gold)) >= GoldCost)
        {
            if (!player.Backpack.ConsumeTotal(typeof(Gold), GoldCost)) { return false; }
        }
        else if (!Banker.Withdraw(player, GoldCost))
        { player.SendMessage("A point reset costs 5,000 gold. Nothing was changed."); return false; }
        ClassCombat.Stop(player);
        PersistentMinions.Dismiss(player, activeOnly: true);
        profiles.ResetAllocatedPoints(player, active);
        TownRecognition.EndSession(player);
        player.CloseGump<WardrobeGearGump>(); player.CloseGump<RecognitionGump>(); player.CloseGump<RespecGump>(); player.CloseGump<WardrobeGump>();
        player.SendMessage("Your active class points are refunded. Your class, level, experience and trade skills are unchanged. Cost: 5,000 gold.");
        return true;
    }
}

public sealed class RespecGump : DynamicGump
{
    private readonly Serial _owner;
    private readonly Item _wardrobe;
    private readonly NetState _connection;
    private readonly CharacterProfile _profile;
    private readonly StatAllocation _stats;
    private readonly int _skillPoints;
    private bool _used;
    private readonly long _token;
    private readonly bool _female;
    public override bool Singleton => true;
    public RespecGump(PlayerMobile player, Item wardrobe) : base(45, 70)
    {
        _owner = player.Serial; _wardrobe = wardrobe; _connection = player.NetState;
        _token = TownRecognition.BeginSession(player); _female = player.Female;
        ClassCreation.Profiles.TryGet(player.Serial, out _profile);
        _stats = ClassCreation.Profiles.Stats(player.Serial);
        _skillPoints = ClassCreation.Profiles.AvailablePoints(player.Serial);
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 535, 320, 5054);
        b.AddLabel(22, 18, 0x481, "Reset active class points");
        b.AddLabel(22, 53, 0x47E, $"{DiabloClasses.DisplayName(_profile.Class, _female)} — level {_profile.Level}");
        b.AddLabel(22, 85, 0x481, "Cost: 5,000 gold; backpack first, otherwise bank");
        b.AddHtml(22, 125, 485, 110,
            "<BASEFONT COLOR=#FFFFFF>Return spent class skill and stat points. Keep your level, experience and trade skills. Your class summons are dismissed. Current life/mana/stamina are capped to their new maximums, without refilling.<BR>After resetting, the wardrobe closes. No class switch is performed.</BASEFONT>",
            background: false, scrollbar: false);
        b.AddButton(22, 265, 4005, 4007, 1); b.AddLabel(57, 265, 0x481, "Pay and reset");
        b.AddButton(350, 265, 4017, 4019, 0); b.AddLabel(385, 265, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile is not PlayerMobile player || player.Serial != _owner
            || !TownRecognition.SessionValid(player, _token))
        { return; }
        _used = true;
        if (info.ButtonID == 1 && !ClassRespec.Commit(player, _wardrobe, _profile, _stats, _skillPoints))
        { player.SendMessage("The reset could not be completed. Reopen the wardrobe and check your points, funds and access."); }
    }
}
