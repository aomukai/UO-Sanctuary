// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class AyilaInn : ConversationNpc
{
    internal static readonly Point3D Desk = new(1594, 1586, 20);
    internal static readonly Point3D Reunion = new(1613, 1588, 0);
    internal static readonly Guid LegacyVendorSpawn = new("360bf7a9-be1c-453a-b74a-3d05f91508eb");
    internal const int CallFee = 500;

    private static readonly ScriptedDialogue.Reply[] _topics =
    [
        new("Call Companion", 0, (player, speaker) => ((AyilaInn)speaker).ShowRoster(player)),
        new("Dismiss Companion", 0, (player, speaker) => ((AyilaInn)speaker).OfferDismissal(player))
    ];
    private static readonly ScriptedDialogue.Node[] _script =
    [
        new("Good day. I am Ayila. If you have left a travelling companion in my care, I would be delighted to send for them. Should your present companion require a room, that too can be arranged.", _topics)
    ];

    public new static void Configure() => EventSink.ServerStarted += Install;

    private static void Install()
    {
        foreach (var spawner in World.Items.Values.OfType<BaseSpawner>().Where(item => item.Guid == LegacyVendorSpawn).ToArray())
            spawner.Delete();

        var ayila = World.Mobiles.Values.OfType<AyilaInn>().FirstOrDefault(actor => !actor.Deleted) ?? new AyilaInn();
        if (ayila.Map != Map.Trammel || ayila.Location != Desk)
            ayila.MoveToWorld(Desk, Map.Trammel);
    }

    [Constructible]
    public AyilaInn()
    {
        Name = "Ayila";
        Title = "the innkeeper";
        Female = true;
        Body = 0x191;
        Hue = 0x83EF;
        HairItemID = 0x2046;
        HairHue = 0x045D;
        CantWalk = true;
        Direction = Direction.South;
        Blessed = true;
        AddItem(new FancyDress(0x04D3));
        AddItem(new Cloak(0x045E));
        AddItem(new GoldNecklace());
        AddItem(new GoldEarrings());
        AddItem(new Shoes(0x0455));
    }

    internal override ushort Portrait => PortraitProtocol.AyilaPortrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _script;

    private void SayTo(PlayerMobile player, string line) =>
        ScriptedDialogue.Show(player, this, [new(line, _topics)]);

    private void ShowRoster(PlayerMobile player, int offset = 0)
    {
        var companions = MercenaryCare.ParkedCompanions(player).OrderBy(companion => companion.Name).ToArray();
        if (companions.Length == 0)
        {
            SayTo(player, "I am afraid none of your companions are in residence at present.");
            return;
        }

        bool paged = companions.Length > PortraitProtocol.MaxReplies;
        int pageSize = paged ? PortraitProtocol.MaxReplies - 2 : PortraitProtocol.MaxReplies;
        offset = Math.Min(offset, (companions.Length - 1) / pageSize * pageSize);
        var replies = new List<ScriptedDialogue.Reply>(PortraitProtocol.MaxReplies);
        foreach (var companion in companions.Skip(offset).Take(pageSize))
            replies.Add(new(companion.Name, 0, (owner, _) => Call(owner, companion)));
        if (paged && offset > 0)
            replies.Add(new("Previous", 0, (owner, _) => ShowRoster(owner, offset - pageSize)));
        if (paged && offset + pageSize < companions.Length)
            replies.Add(new("Next", 0, (owner, _) => ShowRoster(owner, offset + pageSize)));

        ScriptedDialogue.Show(player, this,
        [
            new($"Whom shall I send for? The charge is {CallFee:N0} gold, payable when you call them.", [.. replies])
        ]);
    }

    private void OfferDismissal(PlayerMobile player)
    {
        if (MercenaryCare.ActiveStoryCompanion(player) is not { } companion)
        {
            SayTo(player, "You appear to be travelling alone at present. Do let me know if that changes.");
            return;
        }

        ScriptedDialogue.Show(player, this,
        [
            new($"Shall I make up a room for {companion.Name}? I can send for them at your convenience.",
                [new($"Leave {companion.Name} Here", 0, (owner, _) => Dismiss(owner, companion)), new("Keep Travelling", 0, (owner, _) => BeginConversation(owner))])
        ]);
    }

    private void Dismiss(PlayerMobile player, BaseCreature companion)
    {
        if (!ScriptedDialogue.CanTalk(player, this))
            return;

        if (!MercenaryCare.ParkAtInn(player, companion))
        {
            SayTo(player, "I am afraid I cannot receive your companion just now.");
            return;
        }

        ScriptedDialogue.SendStoryState(player);
        SayTo(player, $"Very good. I shall see that {companion.Name} is made comfortable.");
    }

    private void Call(PlayerMobile player, BaseCreature companion)
    {
        if (!ScriptedDialogue.CanTalk(player, this))
            return;
        if (MercenaryCare.HasActiveCompanion(player))
        {
            SayTo(player, "One travelling companion at a time, if you please. I would be happy to arrange a room for the one accompanying you.");
            return;
        }
        if (!SanctuaryBanking.CanPay(player, CallFee))
        {
            SayTo(player, $"I am afraid the {CallFee:N0} gold must be paid in full before I send anyone down.");
            return;
        }

        var map = Map.Trammel;
        var eye = new Point3D(Reunion.X, Reunion.Y, Reunion.Z + 14);
        if (!map.CanSpawnMobile(Reunion) || !PersistentMinions.FindPosition(map, Reunion,
            point => point.Z == Reunion.Z && Utility.InRange(Reunion, point, PortraitProtocol.NpcInteractionRange)
                && map.LineOfSight(eye, new Point3D(point.X, point.Y, point.Z + 14)), out var alongside))
        {
            SayTo(player, "The room I had in mind is occupied. If you would be so kind as to try again in a moment.");
            return;
        }
        if (!MercenaryCare.CallFromInn(player, companion, map, Reunion, alongside, CallFee))
        {
            SayTo(player, "I am afraid I cannot send for that companion at present.");
            return;
        }

        ScriptedDialogue.SendStoryState(player);
        if (companion.IsDeadPet)
            player.SendMessage("Your companion's spirit has returned. A healer can restore them to life.");
        else if (ScriptedDialogue.CanTalk(player, companion))
            ScriptedDialogue.Show(player, companion, [new(ReunionLine(companion), [])]);
    }

    private static string ReunionLine(BaseCreature companion) => companion switch
    {
        Iolo => "It is good to see thee again, my friend. Quite a tale thou hast brought back. Give me a moment for my crossbow, and we shall be on our way.",
        Wolfgang => "I think I've got it. I should have taken notes, but I was listening. Just let me fetch my shield and I'm ready.",
        Nightwind => "I am glad thou hast come. My bow is close at hand.",
        Merydwen => "I leave you alone for a little while and you come back with a story like that. Right, let me get my bow.",
        _ => "I'm glad to see thee again. Thank thee for bringing me up to date. Shall we set out?"
    };
}
