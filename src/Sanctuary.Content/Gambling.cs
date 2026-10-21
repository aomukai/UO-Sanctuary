// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public enum GambleResult { Invalid, NoFunds, NoCapacity, Refunded, Purchased }

public static partial class Gambling
{
    public static GamblingProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new GamblingProfiles();
    [CallPriority(350)]
    public static void Initialize()
    {
        if (!ClassProfessions.Enabled) { return; }
        foreach (string code in _bases.Keys.ToArray())
        {
            var basis = _bases[code];
            var variants = new List<GambleVariant>();
            foreach (var variant in basis.Variants)
            {
                var item = ItemOrigins.Create(ItemOrigin.Vendor, () => EquipmentCrafting.Resolve(variant.Type).CreateInstance<Item>());
                try { variants.Add(variant with { Graphic = item.ItemID }); }
                finally { item.Delete(); }
            }
            _bases[code] = basis with { Variants = variants.ToArray() };
        }
        foreach (var site in Sites)
        {
            if (World.FindMobile(Profiles.At(site.Id)) is TavernGambler { Deleted: false }) { continue; }
            if (!Map.TryParse(site.Map, null, out var map) || map == Map.Internal || site.X >= map.Width || site.Y >= map.Height)
            { throw new InvalidDataException("Invalid tavern map/bounds: " + site.Id); }
            var floor = TownMarkets.Floor(map, new Point3D(site.X, site.Y, site.Z), new(), true);
            var gambler = new TavernGambler();
            gambler.MoveToWorld(floor, map);
            Profiles.Put(site.Id, gambler.Serial);
        }
    }
    public static int Level(PlayerMobile p) => ClassCreation.Profiles.TryGet(p.Serial, out var profile) ? profile.Level : 0;
    public static bool CanUse(PlayerMobile p, TavernGambler npc) => ClassProfessions.Enabled
        && p is { Alive: true, Deleted: false, Backpack: not null, Holding: null }
        && npc is { Alive: true, Deleted: false } && Level(p) is >= 1 and <= 99
        && AlliedOverlap.Allowed(p) && p.Map == npc.Map && p.InRange(npc, 3) && p.InLOS(npc) && npc.CheckVendorAccess(p);
    internal static GambleResult Buy(PlayerMobile p, TavernGambler npc, GambleOffer offer, int quotedLevel, Random random)
    {
        if (!CanUse(p, npc) || Level(p) != quotedLevel || !_bases.TryGetValue(offer.Code, out var basis)
            || Array.IndexOf(basis.Variants, offer.Variant) < 0 || offer.Price != Price(offer.Code, quotedLevel)
            || offer.ItemLevel < Math.Clamp(quotedLevel - 5, 5, 99) || offer.ItemLevel > Math.Clamp(quotedLevel + 4, 5, 99)
            || EquipmentBalance.Base(offer.Code).QualityLevel > offer.ItemLevel)
        { return GambleResult.Invalid; }
        int remaining = offer.Price;
        var consumes = new List<(Item Item, int Amount)>();
        foreach (var gold in p.Backpack.FindItemsByType(typeof(Gold), true))
        {
            int take = Math.Min(remaining, gold.Amount);
            if (take > 0) { consumes.Add((gold, take)); remaining -= take; }
        }
        bool bank = remaining > 0;
        if (bank && (offer.Price < 2000 || Banker.GetBalance(p) < offer.Price)) { return GambleResult.NoFunds; }
        if (bank) { consumes.Clear(); }
        var item = Generate(offer, random);
        if (item == null) { return GambleResult.Refunded; }
        bool committed = false;
        try
        {
            var delivery = MarketDelivery.Create(p, consumes, new() { item });
            if (delivery == null) { return GambleResult.NoCapacity; }
            if (bank && !Banker.Withdraw(p, offer.Price)) { return GambleResult.NoFunds; }
            delivery.Commit(); committed = true;
            p.PlaySound(0x32);
            return GambleResult.Purchased;
        }
        finally { if (!committed) { item.Delete(); } }
    }
}

public sealed class GamblingProfiles : GenericPersistence
{
    private Dictionary<string, Serial> _sites = new(StringComparer.Ordinal);
    public GamblingProfiles() : base("SanctuaryGambling", 136) { }
    public Serial At(string id) => _sites.GetValueOrDefault(id);
    public void Put(string id, Serial npc) => _sites[id] = npc;
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_sites.Count);
        foreach (var (id, npc) in _sites) { writer.Write(id); writer.Write(npc.Value); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown gambling persistence version."); }
        int count = reader.ReadInt();
        if (count is < 0 or > 1000) { throw new InvalidDataException("Invalid gambler count."); }
        var decoded = new Dictionary<string, Serial>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString(); var npc = (Serial)reader.ReadUInt();
            if (string.IsNullOrWhiteSpace(id) || !npc.IsMobile || !decoded.TryAdd(id, npc))
            { throw new InvalidDataException("Invalid gambler identity."); }
        }
        _sites = decoded;
    }
}

[SerializationGenerator(0)]
public partial class TavernGambler : BaseVendor
{
    private readonly List<SBInfo> _sbInfos = new();
    protected override List<SBInfo> SBInfos => _sbInfos;
    public override bool IsActiveBuyer => false;
    [Constructible]
    public TavernGambler() : base("the gambler") { CantWalk = true; }
    public override void InitSBInfo() { }
    public override void VendorBuy(Mobile from)
    { if (from is PlayerMobile player) { GamblingGump.DisplayTo(player, this); } }
    public override bool OnBuyItems(Mobile buyer, List<BuyItemResponse> list) => false;
}
