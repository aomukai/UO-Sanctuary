// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class Scholar : BaseVendor, IScriptedSpeaker
{
    internal const int IdentificationFee = 50;
    private const int MaxItems = 99;
    private sealed record Quote(Scholar Vendor, NetState Connection, Dictionary<Serial, GearState> Items, long Expires);
    private static readonly Dictionary<Serial, Quote> _quotes = new();
    private readonly List<SBInfo> _stock = new();

    private static readonly ScriptedDialogue.Node[] _dialogue =
    [
        new($"Bring me what thou hast found. I can reveal its properties for {IdentificationFee} gold apiece. If thou wouldst do the work thyself, I have scrolls for the road.",
            [new("Identify", 0, (player, speaker) => ((Scholar)speaker).OpenIdentification(player)),
                new("Buy", 0, (player, speaker) => ((Scholar)speaker).VendorBuy(player))])
    ];

    [Constructible]
    public Scholar() : base("the Scholar")
    {
        Name = "Elara";
        Female = true;
        Body = 0x191;
        FacialHairItemID = 0;
        HairItemID = 0x203B;
        HairHue = 0x0481;
        CantWalk = true;
    }

    protected override List<SBInfo> SBInfos => _stock;
    public override void InitSBInfo() { }
    public override bool IsActiveBuyer => false;
    public override bool CanTeach => false;

    ushort IScriptedSpeaker.Portrait => (ushort)NpcPortrait.Elara;
    string IScriptedSpeaker.DialogueName => $"{Name} {Title}";
    void IScriptedSpeaker.BeginConversation(PlayerMobile player) => ScriptedDialogue.Show(player, this, _dialogue);

    public new static void Configure() => EventSink.Disconnected += player => _quotes.Remove(player.Serial);

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player)
            ScriptedDialogue.Open(player, this);
    }

    internal void OpenIdentification(PlayerMobile player)
    {
        _quotes.Remove(player.Serial);
        if (!TownMarkets.Trading(this, player) || player.NetState is not { Running: true } connection)
            return;

        var entries = new HashSet<SellItemState>(new SellItemStateComparer());
        var quoted = new Dictionary<Serial, GearState>();
        foreach (var item in player.Backpack.FindItems())
        {
            if (Identification.TargetGear(player, item) is not { } state)
                continue;

            quoted.Add(item.Serial, state);
            entries.Add(new SellItemState(item, IdentificationFee, ClassGear.Base(item)?.Name));
            if (entries.Count == MaxItems)
                break;
        }
        if (entries.Count == 0)
        {
            player.SendMessage("There is nothing in thy pack that needs identifying.");
            return;
        }

        _quotes[player.Serial] = new(this, connection, quoted, Core.TickCount + 120_000);
        ScriptedDialogue.SendIdentifyShop(player, Serial);
        connection.SendVendorSellList(Serial, entries);
        foreach (var entry in entries)
            connection.Send(entry.Item.PropertyList.Buffer);
    }

    public override bool OnSellItems(Mobile seller, List<SellItemResponse> items)
    {
        if (seller is not PlayerMobile player || !_quotes.TryGetValue(player.Serial, out var quote) || quote.Vendor != this)
            return false;

        _quotes.Remove(player.Serial);
        if (quote.Connection != player.NetState || items is not { Count: > 0 and <= MaxItems })
            return false;
        if (!TownMarkets.Trading(this, player) || Core.TickCount >= quote.Expires)
        {
            player.SendMessage("Speak with Elara again for a fresh appraisal.");
            return false;
        }

        var selected = new Dictionary<Item, GearState>();
        foreach (var request in items)
        {
            var item = request.Item;
            if (item == null || request.Amount != 1 || !quote.Items.TryGetValue(item.Serial, out var state)
                || !ReferenceEquals(state, Identification.TargetGear(player, item)) || !selected.TryAdd(item, state))
            {
                player.SendMessage("The items have changed. Ask Elara for a new appraisal.");
                return false;
            }
        }

        int cost = selected.Count * IdentificationFee;
        if (!Banker.PayPurchase(player, cost, out _))
        {
            player.SendMessage($"Thou needest {cost} gold for this appraisal.");
            return false;
        }

        foreach (var (item, state) in selected)
            Identification.RevealGear(item, state);
        player.SendMessage($"Elara identified {selected.Count} {(selected.Count == 1 ? "item" : "items")} for {cost} gold.");
        return true;
    }
}
