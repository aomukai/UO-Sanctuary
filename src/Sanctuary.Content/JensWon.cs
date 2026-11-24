// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class JensWon : ConversationNpc
{
    internal static readonly Point3D Shop = new(3555, 2456, 15);
    internal const int Price = 100;
    private static readonly TimeSpan _restock = TimeSpan.FromMinutes(5);
    private static readonly ScriptedDialogue.Node[] _inStock =
    [
        new("There are treasures to be found in the crypts below Haven. Take one of my shovels to the graveyard and try it for thyself. The more you buy, the more you earn!",
            [new("Buy shovel · 100 gold", 0, (player, speaker) => ((JensWon)speaker).Buy(player))])
    ];

    [SerializableField(0, setter: "private")]
    private DateTime _restockAt;

    [Constructible]
    public JensWon()
    {
        Name = "Jens Won";
        Title = "the shovel merchant";
        Body = 0x190;
        Hue = 0x83EA;
        HairItemID = 0x203B;
        HairHue = 0x0481;
        Direction = Direction.East;
        CantWalk = true;
        AddItem(new Doublet(0x0455));
        AddItem(new LongPants(0x0455));
        AddItem(new Boots(0x03E5));
    }

    public new static void Configure() => EventSink.ServerStarted += () =>
    {
        var merchant = World.Mobiles.Values.OfType<JensWon>().FirstOrDefault() ?? new JensWon();
        if (merchant.Map != Map.Trammel || merchant.Location != Shop)
            merchant.MoveToWorld(Shop, Map.Trammel);
    };

    internal override ushort Portrait => (ushort)NpcPortrait.JensWon;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => Core.Now >= _restockAt
        ? _inStock
        : [new($"Supply chain shortages. I'll have another shovel in {(_restockAt - Core.Now):m\\:ss}.", [])];

    internal bool Buy(PlayerMobile player)
    {
        if (!player.Alive || player.Map != Map || !player.InRange(this, PortraitProtocol.NpcInteractionRange)
            || !player.CanSee(this) || !player.InLOS(this) || player.Backpack is not { } pack)
            return false;
        if (Core.Now < _restockAt)
        {
            player.SendMessage("Supply chain shortages. I'll have another shovel within five minutes.");
            return false;
        }

        var shovel = new HavenCryptShovel();
        if (!pack.CheckHold(player, shovel, true) || !SanctuaryBanking.TryPay(player, Price))
        {
            shovel.Delete();
            player.SendMessage("Thou needest room in thy pack and 100 gold for the shovel.");
            return false;
        }

        pack.DropItem(shovel);
        RestockAt = Core.Now + _restock;
        player.SendMessage("Use the shovel in the graveyard east of here. It will take thee to a crypt.");
        return true;
    }
}
