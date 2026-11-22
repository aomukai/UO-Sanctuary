// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

public static class VesperTownhall
{
    internal const string Office = "Trammel/Vesper";
    internal const string SignName = "Vesper Townhall";
    internal static readonly Point3D SignPost = new(2848, 929, 0);
    internal static readonly Point3D RegistrarPost = new(2845, 928, 0);
    internal static readonly Point3D WardrobePost = new(2840, 928, 0);
    internal static readonly Point3D MayorPost = new(2840, 931, 0);

    public static void Configure() => EventSink.ServerStarted += Install;

    private static void Install()
    {
        var map = Map.Trammel;
        const int signGraphic = 3025;
        bool foundSign = false;
        foreach (var sign in map.GetItemsAt<BaseSign>(SignPost))
        {
            if (sign.Location != SignPost || sign.ItemID != signGraphic)
                continue;
            if (sign.Name != SignName)
                sign.Name = SignName;
            foundSign = true;
        }
        if (!foundSign)
            new Sign(signGraphic) { Name = SignName }.MoveToWorld(SignPost, map);

        var mayor = World.Mobiles.Values.OfType<MayorOswin>().FirstOrDefault(mobile => !mobile.Deleted) ?? new MayorOswin();
        if (mayor.Map != map || mayor.Location != MayorPost)
            mayor.MoveToWorld(MayorPost, map);
    }
}

[SerializationGenerator(0)]
public partial class MayorOswin : ConversationNpc
{
    private static readonly ScriptedDialogue.Reply[] _topics = [new("Vesper", 1), new("The Coast", 2)];
    private static readonly ScriptedDialogue.Node[] _welcome =
    [
        new("Welcome to Vesper, traveller. Ours is the largest town in these parts. Fishing keeps us fed, and trade keeps the quays busy. Take care near the coast. Lately our crews have seen sea serpents and krakens close to shore.", _topics),
        new("Come down to the quays at dawn, and thou shalt see half the town at work. The fishing crews bring in their catch while the merchants load ships bound for every port in Britannia.", _topics),
        new("The crews know these waters well, and even they are wary now. Sea serpents have come into the shallows, and some swear they saw a kraken. Give the water's edge a wide berth.", _topics)
    ];

    [Constructible]
    public MayorOswin()
    {
        Name = "Oswin";
        Title = "the mayor of Vesper";
        Body = 0x190;
        Hue = 0x83EA;
        HairItemID = 0x2048;
        HairHue = 0x0454;
        CantWalk = true;
        Direction = Direction.East;
        AddItem(new FancyShirt(0x03E5));
        AddItem(new Doublet(0x0591));
        AddItem(new LongPants(0x0455));
        AddItem(new Boots(0x0455));
    }

    internal override ushort Portrait => PortraitProtocol.VesperMayorPortrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _welcome;
}
