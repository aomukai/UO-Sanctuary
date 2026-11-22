// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class MountMerchant : ConversationNpc
{
    internal static readonly Point3D Britain = new(1516, 1547, 25);
    internal static readonly Point3D Yew = new(562, 1113, 0);
    [SerializableField(0, setter: "private")] private bool _trader;

    public new static void Configure() => EventSink.ServerStarted += Install;

    private static void Install()
    {
        foreach (bool trader in new[] { false, true })
        {
            var merchant = World.Mobiles.Values.OfType<MountMerchant>().FirstOrDefault(npc => npc.Trader == trader)
                ?? new MountMerchant(trader);
            merchant.MoveToWorld(trader ? Yew : Britain, Map.Trammel);
        }
    }

    [Constructible]
    public MountMerchant(bool trader)
    {
        Trader = trader;
        Name = trader ? "Brennan" : "Rowena";
        Title = trader ? "the mount trader" : "the horse groom";
        Female = !trader;
        Body = Female ? 0x191 : 0x190;
        Hue = 0x83EA;
        HairItemID = Female ? 0x203C : 0x2048;
        HairHue = 0x0462;
        CantWalk = true;
        Direction = Direction.South;
        AddItem(new FancyShirt(0x0432));
        AddItem(new HalfApron(trader ? 0x0482 : 0x0591));
        AddItem(new LongPants(0x0455));
        AddItem(new Boots(0x03E5));
    }

    internal override ushort Portrait => (ushort)(Trader ? NpcPortrait.YewMountTrader : NpcPortrait.BritainHorseMerchant);
    internal override IReadOnlyList<ScriptedDialogue.Node> Script =>
    [
        new(Trader
            ? "Good day. I sell horses and keep mounts here on behalf of their owners until a buyer comes along."
            : "Good day. A steady horse will make short work of the roads. I ask five thousand gold for one.",
            Trader ? _trading : _horses)
    ];

    private static readonly ScriptedDialogue.Reply _buy = new("Buy horse · 5,000 gold", 0, (owner, merchant) =>
        owner.SendGump(new MountConfirmation(owner, "Buy a horse for 5,000 gold?", "Buy Horse", () =>
        {
            if (MountTravel.Nearby(owner, merchant))
                MountTravel.BuyHorse(owner, merchant);
        })));
    private static readonly ScriptedDialogue.Reply[] _horses = [_buy];
    private static readonly ScriptedDialogue.Reply[] _trading =
    [
        _buy,
        new("Mounts for sale", 0, (owner, actor) => MountMarketGump.Display(owner, (MountMerchant)actor)),
        new("List a mount", 0, (owner, actor) => MountMarketGump.OfferListing(owner, (MountMerchant)actor)),
        new("Collect proceeds", 0, (owner, actor) => MountMarketGump.Collect(owner, (MountMerchant)actor))
    ];
}

internal sealed class MountMarketGump(PlayerMobile owner, MountMerchant trader, int page,
    (BaseMount Mount, MountRecord Record)[] offers) : DynamicGump(75, 50)
{
    private const int PageLength = 7;
    public override bool Singleton => true;

    internal static void Display(PlayerMobile owner, MountMerchant trader, int page = 0)
    {
        var listings = MountTravel.Profiles.Mounts
            .Where(pair => pair.Value.Price > 0)
            .OrderBy(pair => pair.Value.Price).ThenBy(pair => pair.Key.Value)
            .Select(pair => (Mount: World.FindMobile(pair.Key) as BaseMount, Record: pair.Value))
            .Where(pair => pair.Mount is { Deleted: false, IsBonded: false, IsDeadPet: false }).ToArray();
        page = Math.Clamp(page, 0, Math.Max(0, (listings.Length - 1) / PageLength));
        owner.SendGump(new MountMarketGump(owner, trader, page, listings));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage();
        b.AddBackground(0, 0, 535, 337, 5054);
        b.AddLabel(24, 20, 0x481, "Yew Mount Exchange");
        if (offers.Length == 0)
            b.AddLabel(24, 62, 0x481, "There are no mounts for sale.");
        int count = Math.Min(PageLength, offers.Length - page * PageLength);
        for (int index = 0; index < count; index++)
        {
            var (mount, listing) = offers[page * PageLength + index];
            int y = 63 + index * 31;
            string verb = listing.Owner == owner.Serial ? "Withdraw" : "Buy";
            b.AddButton(25, y, 4005, 4007, 100 + index);
            b.AddLabel(60, y, 0x481, $"{verb} {mount.Name} · {listing.Price:N0} gold");
            b.AddLabel(374, y, 0x47E, $"{mount.HitsMax} life");
        }
        if (page > 0)
        {
            b.AddButton(25, 293, 4005, 4007, 1);
            b.AddLabel(60, 293, 0x481, "Previous");
        }
        if ((page + 1) * PageLength < offers.Length)
        {
            b.AddButton(401, 293, 4005, 4007, 2);
            b.AddLabel(436, 293, 0x481, "Next");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != owner || !MountTravel.Nearby(owner, trader))
            return;
        if (info.ButtonID is 1 or 2)
            Display(owner, trader, page + (info.ButtonID == 1 ? -1 : 1));
        else if (info.ButtonID is >= 100 and < 100 + PageLength
            && page * PageLength + info.ButtonID - 100 is var index && index < offers.Length)
        {
            var (mount, quote) = offers[index];
            bool withdraw = quote.Owner == owner.Serial;
            owner.SendGump(new MountConfirmation(owner, withdraw ? $"Withdraw {mount.Name} from sale?"
                : $"Buy {mount.Name} for {quote.Price:N0} gold?", withdraw ? "Withdraw" : "Buy Mount",
                () => TakeListing(owner, trader, mount, quote)));
        }
    }

    private static void TakeListing(PlayerMobile owner, MountMerchant trader, BaseMount mount, MountRecord quote)
    {
        if (!MountTravel.Nearby(owner, trader) || mount is not { Deleted: false, IsBonded: false, IsDeadPet: false }
            || MountTravel.Find(mount) != quote)
            return;
        if (MountTravel.Active(owner) != null)
        {
            owner.SendMessage("Stable or list your active mount first.");
            return;
        }
        if (!MountTravel.FindLanding(owner, out var spot))
            return;
        if (quote.Owner != owner.Serial)
        {
            long proceeds = MountTravel.Profiles.Proceeds.GetValueOrDefault(quote.Owner);
            if (proceeds > long.MaxValue - quote.Price || !SanctuaryBanking.TryPay(owner, quote.Price))
            {
                owner.SendMessage("You cannot pay for this mount.");
                return;
            }
            if (!mount.SetControlMaster(owner))
            {
                Banker.Deposit(owner, quote.Price);
                return;
            }
            mount.Owners.Clear();
            mount.Owners.Add(owner);
            MountTravel.Profiles.Proceeds[quote.Owner] = proceeds + quote.Price;
        }
        MountTravel.Retrieve(owner, mount, quote with { Owner = owner.Serial, Price = 0, BondAt = DateTime.MinValue }, spot);
    }

    internal static void OfferListing(PlayerMobile owner, MountMerchant trader)
    {
        var mount = MountTravel.Active(owner);
        if (mount == null || (mount.Rider != owner && !MountTravel.Nearby(owner, mount)))
            owner.SendMessage("Bring your mount to the trader.");
        else if (mount.IsBonded || mount.IsDeadPet)
            owner.SendMessage("Bonded mounts cannot be sold.");
        else
            owner.SendGump(new ListingPrice(owner, trader, mount));
    }

    internal static void Collect(PlayerMobile owner, MountMerchant trader)
    {
        var proceeds = MountTravel.Profiles.Proceeds;
        int gold = (int)Math.Min(int.MaxValue, proceeds.GetValueOrDefault(owner.Serial));
        if (!MountTravel.Nearby(owner, trader) || gold <= 0)
        {
            owner.SendMessage("You have no sale proceeds waiting.");
            return;
        }
        if (!Banker.Deposit(owner, gold))
        {
            owner.SendMessage("Your bank could not accept the payment.");
            return;
        }
        if (proceeds[owner.Serial] == gold)
            proceeds.Remove(owner.Serial);
        else
            proceeds[owner.Serial] -= gold;
        owner.SendMessage($"{gold:N0} gold from mount sales was deposited in your bank.");
    }

    private sealed class ListingPrice(PlayerMobile owner, MountMerchant trader, BaseMount mount) : DynamicGump(100, 75)
    {
        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder b)
        {
            b.AddPage();
            b.AddBackground(0, 0, 425, 155, 5054);
            b.AddLabel(22, 17, 0x481, $"List {mount.Name} for sale");
            b.AddLabel(22, 59, 0x481, "Price in gold");
            b.AddBackground(126, 54, 178, 29, 3000);
            b.AddTextEntry(132, 59, 166, 23, 0, 0, "5000");
            b.AddButton(22, 106, 4005, 4007, 1);
            b.AddLabel(57, 106, 0x481, "List Mount");
            b.AddButton(287, 106, 4017, 4019, 0);
            b.AddLabel(322, 106, 0x481, "Cancel");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != owner || info.ButtonID != 1 || !MountTravel.Nearby(owner, trader)
                || mount.IsBonded || mount.IsDeadPet || (mount.Rider != owner && !MountTravel.Nearby(owner, mount))
                || MountTravel.Find(mount) is not { } record || record.Owner != owner.Serial || !MountTravel.IsActive(record))
                return;
            if (!int.TryParse(info.GetTextEntry(0), out int price) || price <= 0)
            {
                owner.SendMessage("Enter a sale price greater than zero.");
                return;
            }
            MountTravel.Store(mount, record with { Price = price, BondAt = DateTime.MinValue });
            owner.SendMessage($"Your mount is listed for {price:N0} gold.");
        }
    }
}
