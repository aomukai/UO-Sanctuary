// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class DungeonEntranceRunes
{
    internal sealed record Entrance(string Label, Point3D Landing, StoryDiscoveries Claimed,
        RuneMountTravel MountedTravel = RuneMountTravel.DismountRequired)
    {
        internal bool WasClaimed(PlayerMobile player) =>
            (ClassCreation.Profiles.Discoveries(player.Serial) & Claimed) != 0;
    }

    internal static readonly Entrance Shame = new("Shame Entrance", CampaignDungeonRoutes.ShameSurfaceLanding, StoryDiscoveries.ShameEntranceRune);
    internal static readonly Entrance Covetous = new("Covetous Entrance", new(2499, 919, 0), StoryDiscoveries.CovetousEntranceRune);
    internal static readonly Entrance Deceit = new("Deceit Entrance", new(4111, 432, 5), StoryDiscoveries.DeceitEntranceRune);
    internal static readonly Entrance Despise = new("Despise Entrance", new(1298, 1080, 0), StoryDiscoveries.DespiseEntranceRune);
    internal static readonly Entrance Destard = new("Destard Entrance", new(1176, 2637, 0), StoryDiscoveries.DestardEntranceRune);
    internal static readonly Entrance Hythloth = new("Hythloth Entrance", new(4721, 3822, 0), StoryDiscoveries.HythlothEntranceRune);
    internal static readonly Entrance Wrong = new("Wrong Entrance", new(2043, 238, 10), StoryDiscoveries.WrongEntranceRune);
    internal static readonly Entrance Cove = new(CoveGateRune.Label, CoveGateRune.Gates, StoryDiscoveries.CoveChief, RuneMountTravel.Allowed);

    private static readonly Entrance[] _all = [Shame, Covetous, Deceit, Despise, Destard, Hythloth, Wrong, Cove];

    public static void Configure() => EventSink.Connected += mobile =>
    {
        if (mobile is PlayerMobile player)
            RestoreLegacyRunes(player);
    };

    internal static void Install()
    {
        var map = Map.Trammel;
        ShamePopulation.Ensure(map, new(5423, 1895, 0), () => new DungeonRuneCache(covetous: true));
        ShamePopulation.Ensure(map, new(5337, 544, 0), () => new DungeonRuneCache(covetous: false));

        // The bookcase is on the raised ledge beside the floor tile used to reach it.
        var floor = new Point3D(5985, 69, 22);
        var ledge = new Point3D(5985, 68, 28);
        var existing = FindBookcase(map, floor) ?? FindBookcase(map, ledge);
        if (existing is DungeonRuneBookcase)
            return;

        var shelf = new DungeonRuneBookcase
        {
            ItemID = existing?.ItemID ?? 0xA99,
            Hue = existing?.Hue ?? 0,
            Name = existing?.Name
        };
        shelf.MoveToWorld(existing?.Location ?? floor, map);
        if (existing != null)
        {
            foreach (var item in existing.Items.ToArray())
                shelf.AddItem(item);
            existing.Delete();
        }
    }

    private static FullBookcase FindBookcase(Map map, Point3D point)
    {
        foreach (var shelf in map.GetItemsAt<FullBookcase>(point))
            if (shelf.Location == point && !shelf.Movable)
                return shelf;
        return null;
    }

    internal static bool CanSearch(PlayerMobile player, Item item) =>
        player.Alive && item is { Deleted: false, Parent: null } && player.Map == item.Map
        && player.InRange(item, 2) && Math.Abs(player.Z - item.Z) < 16
        && player.CanSee(item) && player.InLOS(item);

    internal static bool Award(PlayerMobile player, Entrance entrance, bool announceRepeat = true,
        bool announceMissingBook = true)
    {
        var profiles = ClassCreation.Profiles;
        if (!profiles.TryGet(player.Serial, out _) || player.Backpack is not { } pack)
            return false;
        if (entrance.WasClaimed(player))
        {
            if (announceRepeat)
                player.SendMessage("Thou hast already found this rune.");
            return false;
        }

        var book = FindBook(pack, entrance, out bool alreadyFiled);
        if (book == null)
        {
            if (announceMissingBook)
                player.SendMessage("Thou wilt need a runebook to record this destination.");
            return false;
        }

        if (!alreadyFiled)
            File(player, book, entrance);
        else
            player.SendMessage($"{entrance.Label} is already marked in thy runebook.");

        if (entrance == Cove)
            ScriptedDialogue.RecordDiscovery(player, entrance.Claimed);
        else
            profiles.Discover(player.Serial, entrance.Claimed);
        return true;
    }

    internal static void RestoreLegacyRunes(PlayerMobile player)
    {
        var profiles = ClassCreation.Profiles;
        var discoveries = profiles.Discoveries(player.Serial);
        if ((discoveries & StoryDiscoveries.LegacyEntranceRunesFiled) != 0
            || player.Backpack is not { } pack || !profiles.TryGet(player.Serial, out _))
            return;

        var bank = player.FindBankNoCreate();
        foreach (var entrance in _all)
        {
            if ((discoveries & entrance.Claimed) == 0 && entrance != Cove)
                continue;

            List<RecallRune> legacy = [];
            FindLegacyRunes(pack, entrance, legacy);
            FindLegacyRunes(bank, entrance, legacy);
            if ((discoveries & entrance.Claimed) == 0 && legacy.Count == 0)
                continue;

            var book = FindBook(pack, entrance, out bool filedInPack);
            FindBook(bank, entrance, out bool filedInBank);
            if (!filedInPack && !filedInBank)
            {
                if (book == null)
                    return;
                File(player, book, entrance);
            }
            foreach (var rune in legacy)
                rune.Delete();

            if (entrance == Cove && (discoveries & entrance.Claimed) == 0)
                ScriptedDialogue.RecordDiscovery(player, entrance.Claimed);
        }

        profiles.Discover(player.Serial, StoryDiscoveries.LegacyEntranceRunesFiled);
    }

    private static void FindLegacyRunes(Container container, Entrance entrance, List<RecallRune> matches)
    {
        if (container == null)
            return;
        foreach (var rune in container.FindItemsByType<RecallRune>())
            if (rune.Marked && rune.TargetMap == Map.Trammel && rune.Target == entrance.Landing
                && (entrance == Cove ? rune is CoveGateRune : rune.GetType() == typeof(RecallRune) && rune.House == null
                    && rune.Description == entrance.Label && rune.MountedTravel == entrance.MountedTravel))
                matches.Add(rune);
    }

    private static Runebook FindBook(Container container, Entrance entrance, out bool alreadyFiled)
    {
        alreadyFiled = false;
        if (container == null)
            return null;

        Runebook available = null;
        foreach (var book in container.FindItemsByType<Runebook>())
        {
            if (book.Entries.Any(entry => entry.Map == Map.Trammel && entry.Location == entrance.Landing))
            {
                alreadyFiled = true;
                return book;
            }
            if (available == null || book.Description == "Iolo's Runebook")
                available = book;
        }
        return available;
    }

    private static void File(PlayerMobile player, Runebook book, Entrance entrance)
    {
        book.Entries.Add(new RunebookEntry(book, entrance.Landing, Map.Trammel, entrance.Label, mountedTravel: entrance.MountedTravel));
        book.MarkDirty();
        book.InvalidateProperties();
        player.SendMessage($"The rune for {entrance.Label} has been recorded in thy runebook.");
    }
}

[SerializationGenerator(0)]
public partial class DungeonRuneCache : Item
{
    [SerializableField(0, getter: "private", setter: "private")] private bool _covetous;

    internal DungeonRuneCache(bool covetous) : base(covetous ? 0x1170 : 0xE3C)
    {
        Covetous = covetous;
        Name = covetous ? "a gravestone" : "a crate";
        Movable = false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && DungeonEntranceRunes.CanSearch(player, this))
            DungeonEntranceRunes.Award(player, Covetous ? DungeonEntranceRunes.Covetous : DungeonEntranceRunes.Deceit);
    }
}

[SerializationGenerator(0)]
public partial class DungeonRuneBookcase : FullBookcase
{
    internal DungeonRuneBookcase() => Movable = false;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && DungeonEntranceRunes.CanSearch(player, this))
            DungeonEntranceRunes.Award(player, DungeonEntranceRunes.Hythloth, announceRepeat: false);
        base.OnDoubleClick(from);
    }
}
