// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class WardrobeSwitch
{
    public static bool Commit(PlayerMobile player, Item wardrobe, long token, CharacterProfile quoted,
        ProfessionState profession, DiabloClass selectedClass, SkillName craft, string quote = null)
    {
        if (!TownRecognition.SessionValid(player, token) || !WardrobeAccess.Ready(player, wardrobe)
            || !ClassCreation.Profiles.TryGet(player.Serial, out var active) || active != quoted
            || !DiabloClasses.TryFromWire((int)selectedClass, out _) || !TradeSkills.IsCraft((int)craft)
            || !ReferenceEquals(ClassProfessions.Profiles.Find(player), profession)
            || quote != null && quote != WardrobeTransaction.Quote(player)) { return false; }
        TimedEffects.Settle(player, TimedEffects.Now);
        PersistentMinions.Profiles.Sweep(System.DateTime.UtcNow);
        if (!WardrobeAccess.Ready(player, wardrobe)) { return false; }
        int missing = 0, packed = 0;
        bool changedClass = selectedClass != active.Class;
        if (changedClass && !WardrobeTransaction.ChangeClass(player, selectedClass, out missing, out packed)) { return false; }
        bool changedCraft = profession is not { Confirmed: true } || profession.Craft != craft;
        if (changedCraft) { ClassProfessions.Profiles.Select(player, craft); }
        if (changedClass || changedCraft) { ClassProfessions.InvalidateChoices(player); }
        var pair = SkillBindings.Instance.Get(player); var session = ClassCombat.For(player);
        session.Left = pair.Left; session.Right = pair.Right;
        player.CloseGump<TalentGump>(); player.CloseGump<BindingGump>();
        ClassGear.Changed(player); StatNetwork.Send(player); SkillBindingNetwork.Send(player);
        ClassCombat.SendState(player, session); PotionBeltNetwork.Send(player); EarnedTitles.Changed(player);
        player.SendMessage(changedClass || changedCraft
            ? $"Class and craft selected. Switching is free. Missing equipment skipped: {missing}; equipment placed in backpack: {packed}."
            : "Your class and craft are already selected. Nothing changed.");
        TownRecognition.EndSession(player);
        player.CloseGump<WardrobeGump>(); player.CloseGump<WardrobeGearGump>();
        player.CloseGump<RecognitionGump>(); player.CloseGump<RespecGump>();
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
    private readonly string _quote;
    private bool _used;
    public override bool Singleton => true;
    internal WardrobeGump(PlayerMobile player, Item wardrobe) : base(35, 35)
    {
        _owner = player.Serial; _wardrobe = wardrobe; _connection = player.NetState;
        ClassCreation.Profiles.TryGet(player.Serial, out _profile);
        _profession = ClassProfessions.Profiles.Find(player);
        _token = TownRecognition.BeginSession(player); _female = player.Female;
        _quote = WardrobeTransaction.Quote(player);
        _respec = TownRecognition.Profiles.Find(player).Earned == RecognitionHonor.Both;
    }
    public static void DisplayTo(PlayerMobile player, Item wardrobe)
    {
        if (!WardrobeAccess.Ready(player, wardrobe))
        { player.SendMessage("Wardrobe switching requires earned recognition, a nearby wardrobe, and being alive and out of combat with no pending action."); return; }
        if (player.NetState != null)
        {
            player.CloseGump<RecognitionGump>(); player.CloseGump<RespecGump>(); player.CloseGump<WardrobeGearGump>();
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
            int level = 1;
            foreach (var history in ClassCreation.Profiles.Histories(_owner))
            { if (history.Profile.Class == characterClass) { level = history.Profile.Level; } }
            b.AddRadio(22, y, 210, 211, characterClass == _profile.Class, id);
            b.AddLabel(48, y, 0x481, $"{DiabloClasses.DisplayName(characterClass, _female)} — level {level}");
        }
        b.AddButton(22, 275, 4005, 4007, 5); b.AddLabel(57, 275, 0x481, "Earned titles");
        b.AddGroup(2);
        int row = 0;
        foreach (byte craft in TradeSkills.Crafting)
        {
            int y = 85 + row++ * 22;
            b.AddRadio(295, y, 210, 211, (SkillName)craft == _profession.Craft, 1000 + craft);
            b.AddLabel(321, y, 0x481, ClassProfessions.Name((SkillName)craft));
        }
        b.AddLabel(22, 315, 0x47E, "If you wish to share an item with another class, take it off now.");
        b.AddLabel(22, 338, 0x47E, "Equipped gear is stored for the class you leave.");
        b.AddButton(22, 365, 4005, 4007, 1); b.AddLabel(57, 365, 0x481, "Swap");
        b.AddButton(285, 365, 4005, 4007, 3); b.AddLabel(320, 365, 0x481, "Recognition / awards");
        if (_respec)
        { b.AddButton(22, 400, 4005, 4007, 2); b.AddLabel(57, 400, 0x481, "Reset points — 5,000 gold"); }
        else { b.AddLabel(22, 400, 0x47E, "Both ceremonies unlock resets."); }
        b.AddButton(285, 400, 4005, 4007, 4); b.AddLabel(320, 400, 0x481, "Retrieve stored gear");
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
                if (DiabloClasses.TryFromWire(id, out _) && selectedClass == -1) { selectedClass = id; }
                else if (id >= 1000 && TradeSkills.IsCraft(id - 1000) && craft == -1) { craft = id - 1000; }
                else { selectedClass = -1; break; }
            }
            if (selectedClass == -1 || craft == -1 || !WardrobeSwitch.Commit(player, _wardrobe, _token, _profile,
                    _profession, (DiabloClass)selectedClass, (SkillName)craft, _quote))
            { player.SendMessage("The selection could not be applied. Reopen the wardrobe and select exactly one class and craft."); }
        }
        else if (info.ButtonID == 2) { ClassRespec.DisplayTo(player, _wardrobe); return; }
        else if (info.ButtonID == 5) { EarnedTitleGump.DisplayTo(player); return; }
        else if (info.ButtonID == 4) { WardrobeGearGump.DisplayTo(player, _wardrobe); return; }
        else if (info.ButtonID == 3)
        { TownRecognition.Open(player, TownRecognition.NearbyOffice(player), _wardrobe); return; }
        TownRecognition.EndSession(player);
    }
}
