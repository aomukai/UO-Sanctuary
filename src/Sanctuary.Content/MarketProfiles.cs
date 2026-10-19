// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public enum MarketRole { Smith, Tailor, Bowyer, Carpenter, Alchemist, Cook, Cartographer, Scribe, Tinker, Imbuer, Miner, Lumberjack, Fisher, Jeweller, Provisioner }
public sealed record MarketSite(string Id, string Town, string Map, MarketRole Role, int X, int Y, int Z, string Source, bool SharedWorkshop);
public sealed record MarketSettings(int OrdinaryItemLevel, int BritainItemLevel, int WhiteOffers, int MagicOffers,
    int RefreshMinutes, int WhitePriceMultiplier, int MagicPriceMultiplier, int VendorResalePercent,
    int NormalMin, int NormalMax, int ExceptionalMin, int ExceptionalMax, int EliteMin, int EliteMax);
public sealed record MarketSupply(MarketRole[] Roles, string Type, int Price, int SellPrice, RestorationKind? Kind, int Tier);
public sealed record MarketOffer(Serial Item, int Price, bool Equipment, string Supply);
public sealed class MarketState(Serial vendor, MarketRole role, string site = null)
{
    public Serial Vendor { get; } = vendor;
    public MarketRole Role { get; } = role;
    public string Site { get; } = site;
    public Serial Shelf { get; set; }
    public Serial Station { get; set; }
    public Serial Forge { get; set; }
    public DateTime NextRefresh { get; set; } = DateTime.MinValue;
    public List<MarketOffer> Offers { get; } = new();
}

public sealed class MarketProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, MarketState> _states = new();
    private readonly Dictionary<string, Serial> _sites = new(StringComparer.Ordinal);
    public MarketProfiles() : base("SanctuaryMarkets", 120) { }
    public IReadOnlyCollection<MarketState> All => _states.Values;
    public MarketState Find(Serial vendor) => _states.GetValueOrDefault(vendor);
    public BaseVendor At(string site) => _sites.TryGetValue(site, out var serial) ? World.FindMobile(serial) as BaseVendor : null;
    public MarketState Add(BaseVendor vendor, MarketRole role, string site = null)
    {
        var state = new MarketState(vendor.Serial, role, site);
        _states.Add(vendor.Serial, state);
        if (site != null) { _sites[site] = vendor.Serial; }
        return state;
    }
    public void Prune()
    {
        using var removed = Server.Collections.PooledRefList<Serial>.Create();
        foreach (var (serial, state) in _states)
        {
            if (World.FindMobile(serial) is not BaseVendor { Deleted: false }) { removed.Add(serial); }
        }
        foreach (var serial in removed)
        {
            if (_states.Remove(serial, out var state) && state.Site != null) { _sites.Remove(state.Site); }
        }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_states.Count);
        foreach (var state in _states.Values)
        {
            writer.Write(state.Vendor.Value); writer.Write((byte)state.Role); writer.Write(state.Site);
            writer.Write(state.Shelf.Value); writer.Write(state.Station.Value); writer.Write(state.Forge.Value); writer.Write(state.NextRefresh);
            writer.Write(state.Offers.Count);
            foreach (var offer in state.Offers)
            {
                writer.Write(offer.Item.Value); writer.Write(offer.Price); writer.Write(offer.Equipment); writer.Write(offer.Supply);
            }
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown market persistence version."); }
        int count = reader.ReadInt();
        if (count is < 0 or > 100000) { throw new InvalidDataException("Invalid market count."); }
        _states.Clear(); _sites.Clear();
        for (int i = 0; i < count; i++)
        {
            var state = new MarketState((Serial)reader.ReadUInt(), (MarketRole)reader.ReadByte(), reader.ReadString())
            { Shelf = (Serial)reader.ReadUInt(), Station = (Serial)reader.ReadUInt(), Forge = (Serial)reader.ReadUInt(), NextRefresh = reader.ReadDateTime() };
            if (!Enum.IsDefined(state.Role)) { throw new InvalidDataException("Invalid saved market role."); }
            int offers = reader.ReadInt();
            if (offers is < 0 or > 249) { throw new InvalidDataException("Invalid saved market offers."); }
            var seen = new HashSet<Serial>();
            for (int j = 0; j < offers; j++)
            {
                var offer = new MarketOffer((Serial)reader.ReadUInt(), reader.ReadInt(), reader.ReadBool(), reader.ReadString());
                if (offer.Price is < 1 or > 60000 || !seen.Add(offer.Item) || offer.Equipment != (offer.Supply == null))
                { throw new InvalidDataException("Invalid saved market offer."); }
                state.Offers.Add(offer);
            }
            _states.Add(state.Vendor, state);
            if (state.Site != null) { _sites.Add(state.Site, state.Vendor); }
        }
    }
    public override void PostDeserialize()
    {
        Prune();
        foreach (var state in _states.Values)
        {
            if (state.Shelf != Serial.Zero && World.FindItem(state.Shelf) is not MarketShelf)
            { throw new InvalidDataException("Saved market shelf is missing."); }
            foreach (var offer in state.Offers)
            {
                if (World.FindItem(offer.Item) is not { Deleted: false } item || item.Parent != World.FindItem(state.Shelf))
                { throw new InvalidDataException("Saved market item left its shelf."); }
            }
        }
    }
}

[SerializationGenerator(0)]
public partial class MarketShelf : Container
{
    [Constructible]
    public MarketShelf() : base(0xE75) { Movable = Visible = false; Name = "merchant stock"; }
    public override int DefaultMaxItems => 249;
    public override int DefaultMaxWeight => 0;
    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) => false;
    public override bool IsAccessibleTo(Mobile check) => false;
    public override void OnDoubleClick(Mobile from) { }
}

[SerializationGenerator(0)]
public partial class CookingSpices : Item
{
    [Constructible]
    public CookingSpices() : base(0xF88) { Name = "cooking spices"; Stackable = true; Weight = 0.1; Hue = 0x21; }
}
