// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Mobiles;

namespace Sanctuary;

public static partial class TownRecognition
{
    public static RecognitionProfiles Profiles { get; private set; }
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static long _nextSession;
    public static long BeginSession(Mobile player) => _sessions[player.Serial] = ++_nextSession;
    public static bool SessionValid(Mobile player, long token) => _sessions.GetValueOrDefault(player.Serial) == token;
    public static void EndSession(Mobile player) => _sessions.Remove(player.Serial);
    public static void Configure()
    {
        Profiles = new RecognitionProfiles();
        ClassCombatHooks.SkillChanged += (player, skill, _) =>
        {
            if (TradeSkills.IsCraft((int)skill)) { Refresh(player); }
        };
        EventSink.Connected += from =>
        {
            if (from is not PlayerMobile player) { return; }
            bool invited = Refresh(player);
            var state = Profiles.Find(player);
            if (!invited && ClassProfessions.Affected(player) && state.Eligible != state.Earned) { Invite(player); }
            if (ClassProfessions.Affected(player) && Pending(player))
            { player.SendMessage("Recognition awards await collection at any supported town-hall office."); }
        };
        EventSink.Disconnected += EndSession;
        EventSink.WorldSave += Profiles.Prune;
        CommandSystem.Register("Recognition", AccessLevel.Player, args =>
        {
            if (args.Mobile is PlayerMobile player && ClassProfessions.Affected(player))
            { Open(player, NearbyOffice(player)); }
        });
    }
    public static string Title(RecognitionHonor honor) => honor switch
    {
        RecognitionHonor.Hero => "Hero of the People", RecognitionHonor.Pillar => "Pillar of Society", _ => "Recognition"
    };
    public static bool Refresh(Mobile from)
    {
        if (Profiles == null || World.Loading || from is not PlayerMobile { Deleted: false } player
            || !ClassProfessions.Affected(player)) { return false; }
        var state = Profiles.Find(player);
        var eligible = state.Eligible;
        if (ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Level >= 80)
        { eligible |= RecognitionHonor.Hero; }
        foreach (byte craft in TradeSkills.Crafting)
        {
            if (player.Skills[(SkillName)craft].BaseFixedPoint >= 1000) { eligible |= RecognitionHonor.Pillar; break; }
        }
        if (eligible == state.Eligible) { return false; }
        Profiles.Store(player, state with { Eligible = eligible });
        Invite(player);
        return true;
    }
    public static bool HasHousingRights(Mobile player) => HasAccess(player);
    public static bool HasAccess(Mobile player) => player is { Deleted: false } && Profiles != null && Profiles.Find(player).Earned != 0;
    public static void Open(PlayerMobile player, string site, Item wardrobe = null)
    {
        Refresh(player);
        RecognitionGump.DisplayTo(player, site, ClassRespec.SourceValid(player, wardrobe)
            ? wardrobe : ClassRespec.NearbyWardrobe(player));
    }
    public static bool Claim(PlayerMobile player, string site, RecognitionHonor honor)
    {
        if (!ClassProfessions.Affected(player) || !ClassProfessions.Ready(player) || !AtOffice(player, site)) { return false; }
        Refresh(player);
        var state = Profiles.Find(player);
        if (honor is not (RecognitionHonor.Hero or RecognitionHonor.Pillar)
            || (state.Eligible & honor) == 0 || (state.Earned & honor) != 0) { return false; }
        Profiles.Store(player, state with { Earned = state.Earned | honor });
        player.SendMessage($"{Title(honor)}: {player.Name}, your service is recognized. You have permanent wardrobe access and the right to acquire a home. A house must still be purchased.");
        Registrar(site)?.Say($"{player.Name}, we recognize you as {Title(honor)}. May your deeds inspire our people.");
        Collect(player, site);
        if (Profiles.Find(player).Earned == RecognitionHonor.Both)
        { player.SendMessage("Both honors are complete. Wardrobes now offer active-class point resets for 5,000 gold."); }
        return true;
    }
    public static bool Pending(Mobile player)
    {
        var state = Profiles.Find(player);
        return state.Earned != state.Certificates || state.Earned != 0 && !state.Wardrobe;
    }
    public static bool Collect(PlayerMobile player, string site)
    {
        if (!ClassProfessions.Affected(player) || !ClassProfessions.Ready(player) || !AtOffice(player, site)) { return false; }
        foreach (var honor in new[] { RecognitionHonor.Hero, RecognitionHonor.Pillar })
        {
            var state = Profiles.Find(player);
            if ((state.Earned & honor) == 0 || (state.Certificates & honor) != 0) { continue; }
            var item = new RecognitionCertificate(player, honor);
            Profiles.Store(player, state with { Certificates = state.Certificates | honor });
            bool delivered = false;
            try { delivered = player.Backpack.TryDropItem(player, item, false); }
            finally
            {
                if (!delivered)
                {
                    item.Delete();
                    var current = Profiles.Find(player);
                    Profiles.Store(player, current with { Certificates = current.Certificates & ~honor });
                }
            }
        }
        var after = Profiles.Find(player);
        if (after.Earned != 0 && !after.Wardrobe)
        {
            var item = new EarnedWardrobe(player);
            Profiles.Store(player, after with { Wardrobe = true });
            bool delivered = false;
            try { delivered = player.Backpack.TryDropItem(player, item, false); }
            finally
            {
                if (!delivered)
                {
                    item.Delete();
                    Profiles.Store(player, Profiles.Find(player) with { Wardrobe = false });
                }
            }
        }
        if (Pending(player)) { player.SendMessage("Some recognition awards await collection. Return with space and carrying capacity in your backpack."); }
        return true;
    }
}
