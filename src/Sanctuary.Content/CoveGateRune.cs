// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class CoveGateRune : RecallRune
{
    internal static readonly Point3D Gates = new(2285, 1210, 0);
    internal const string Label = "Cove Gates";

    [Constructible]
    public CoveGateRune()
    {
        Description = Label;
        Target = Gates;
        TargetMap = Map.Trammel;
        Marked = true;
        MountedTravel = RuneMountTravel.Allowed;
        LootType = LootType.Blessed;
    }

    internal static void Award(PlayerMobile player)
    {
        var profiles = ClassCreation.Profiles;
        if (!profiles.TryGet(player.Serial, out _) || player.Backpack is not { } pack
            || (profiles.Discoveries(player.Serial) & StoryDiscoveries.CoveChief) != 0)
            return;

        Runebook available = null;
        foreach (var book in pack.FindItemsByType<Runebook>())
        {
            if (ContainsGates(book) || book.Entries.Count < 16
                && (available == null || book.Description == "Iolo's Runebook"))
                available = book;
            if (ContainsGates(book))
                break;
        }

        if (available != null)
        {
            File(available);
            player.SendMessage("The rune for Cove Gates has been recorded in thy runebook.");
        }
        else
        {
            pack.DropItem(new CoveGateRune());
            player.SendMessage("A rune marked for Cove Gates has been placed in thy pack.");
        }
        ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.CoveChief);
    }

    internal static void FileCarriedRune(PlayerMobile player, Runebook book)
    {
        if (book.Entries.Count >= 16 || player.Backpack is not { } pack)
            return;
        foreach (var rune in pack.FindItemsByType<CoveGateRune>())
        {
            if (!rune.Marked || rune.TargetMap != Map.Trammel || rune.Target != Gates)
                continue;
            File(book);
            rune.Delete();
            player.SendMessage("The rune for Cove Gates has been recorded in thy runebook.");
            break;
        }
    }

    private static bool ContainsGates(Runebook book) =>
        book.Entries.Any(entry => entry.Map == Map.Trammel && entry.Location == Gates);

    private static void File(Runebook book)
    {
        if (ContainsGates(book))
            return;
        book.Entries.Add(new RunebookEntry(book, Gates, Map.Trammel, Label, mountedTravel: RuneMountTravel.Allowed));
        book.MarkDirty();
        book.InvalidateProperties();
    }
}
