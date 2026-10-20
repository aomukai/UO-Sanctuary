// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class WardrobeSwitch
{
    public static bool Commit(PlayerMobile player, Item wardrobe, long token, CharacterProfile quoted,
        ProfessionState profession, DiabloClass selectedClass, SkillName craft)
    {
        if (!TownRecognition.SessionValid(player, token) || !WardrobeAccess.Ready(player, wardrobe)
            || !ClassCreation.Profiles.TryGet(player.Serial, out var active) || active != quoted
            || selectedClass != active.Class || !TradeSkills.IsCraft((int)craft)
            || !ReferenceEquals(ClassProfessions.Profiles.Find(player), profession)) { return false; }
        if (profession is { Confirmed: true } && profession.Craft == craft)
        { player.SendMessage("Your class and craft are already selected. Nothing changed."); }
        else
        {
            ClassProfessions.Profiles.Select(player, craft);
            ClassProfessions.InvalidateChoices(player);
            player.SendMessage($"Your active craft is now {ClassProfessions.Name(craft)}. All trained skills are preserved. Switching is free.");
        }
        TownRecognition.EndSession(player);
        player.CloseGump<WardrobeGump>(); player.CloseGump<RecognitionGump>(); player.CloseGump<RespecGump>();
        return true;
    }
}

public sealed class WardrobeGump : DynamicGump
{
    private readonly Serial _owner;
    private readonly Item _wardrobe;
    private readonly NetState _connection;
    private readonly CharacterProfile _profile;
    private readonly ProfessionState _profession;
    private readonly long _token;
    private readonly bool _female;
    private readonly bool _respec;
    private bool _used;
    public override bool Singleton => true;
    internal WardrobeGump(PlayerMobile player, Item wardrobe) : base(35, 35)
    {
        _owner = player.Serial; _wardrobe = wardrobe; _connection = player.NetState;
        ClassCreation.Profiles.TryGet(player.Serial, out _profile);
        _profession = ClassProfessions.Profiles.Find(player);
        _token = TownRecognition.BeginSession(player); _female = player.Female;
        _respec = TownRecognition.Profiles.Find(player).Earned == RecognitionHonor.Both;
    }
    public static void DisplayTo(PlayerMobile player, Item wardrobe)
    {
        if (!WardrobeAccess.Ready(player, wardrobe))
        { player.SendMessage("Wardrobe switching requires earned recognition, a nearby wardrobe, and being alive and out of combat with no pending action."); return; }
        if (player.NetState != null)
        {
            player.CloseGump<RecognitionGump>(); player.CloseGump<RespecGump>();
            player.SendGump(new WardrobeGump(player, wardrobe));
        }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 435, 5054);
        b.AddLabel(22, 18, 0x481, "Wardrobe — choose class and craft");
        b.AddLabel(22, 48, 0x47E, "Select one class and one craft, then Swap. Cost: free.");
        b.AddGroup(1);
        for (int id = 101; id <= 107; id++)
        {
            int y = 85 + (id - 101) * 25;
            var characterClass = (DiabloClass)id;
            if (characterClass == _profile.Class)
            {
                b.AddRadio(22, y, 210, 211, true, id);
                b.AddLabel(48, y, 0x481, $"{DiabloClasses.DisplayName(characterClass, _female)} — level {_profile.Level}");
            }
            else { b.AddLabel(48, y, 0x3B2, DiabloClasses.DisplayName(characterClass, _female)); }
        }
        b.AddGroup(2);
        int row = 0;
        foreach (byte craft in TradeSkills.Crafting)
        {
            int y = 85 + row++ * 22;
            b.AddRadio(295, y, 210, 211, (SkillName)craft == _profession.Craft, 1000 + craft);
            b.AddLabel(321, y, 0x481, ClassProfessions.Name((SkillName)craft));
        }
        b.AddLabel(22, 315, 0x47E, "All trained trade skills stay saved; gathering stays active.");
        b.AddLabel(22, 338, 0x47E, "Class switching is not available yet.");
        b.AddButton(22, 365, 4005, 4007, 1); b.AddLabel(57, 365, 0x481, "Swap");
        b.AddButton(285, 365, 4005, 4007, 3); b.AddLabel(320, 365, 0x481, "Recognition / awards");
        if (_respec)
        { b.AddButton(22, 400, 4005, 4007, 2); b.AddLabel(57, 400, 0x481, "Reset active class points — 5,000 gold"); }
        else { b.AddLabel(22, 400, 0x47E, "Both ceremonies unlock active-class point resets."); }
        b.AddButton(510, 18, 4017, 4019, 0);
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile is not PlayerMobile player || player.Serial != _owner
            || !TownRecognition.SessionValid(player, _token)) { return; }
        _used = true;
        if (info.ButtonID == 1)
        {
            int selectedClass = -1, craft = -1;
            foreach (int id in info.Switches)
            {
                if (id == (int)_profile.Class && selectedClass == -1) { selectedClass = id; }
                else if (id >= 1000 && TradeSkills.IsCraft(id - 1000) && craft == -1) { craft = id - 1000; }
                else { selectedClass = -1; break; }
            }
            if (selectedClass == -1 || craft == -1 || !WardrobeSwitch.Commit(player, _wardrobe, _token, _profile,
                    _profession, (DiabloClass)selectedClass, (SkillName)craft))
            { player.SendMessage("The selection could not be applied. Reopen the wardrobe and select exactly one class and craft."); }
        }
        else if (info.ButtonID == 2) { ClassRespec.DisplayTo(player, _wardrobe); return; }
        else if (info.ButtonID == 3)
        { TownRecognition.Open(player, TownRecognition.NearbyOffice(player), _wardrobe); return; }
        TownRecognition.EndSession(player);
    }
}
