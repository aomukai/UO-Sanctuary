// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
using Server.Targeting;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class TreasureChart : MapItem
{
    [SerializableField(0, setter: "private")] private int _tier;
    [SerializableField(1, setter: "private")] private Map _facet;
    [SerializableField(2, setter: "private")] private Point2D _site;
    [SerializableField(3, setter: "private")] private int _seed;
    [SerializableField(4, setter: "private")] private Serial _decoderSerial;
    public Mobile Decoder { get => World.FindMobile(_decoderSerial); internal set { _decoderSerial = value?.Serial ?? Serial.Zero; InvalidateProperties(); } }
    [SerializableField(5, setter: "internal")] private bool _spent;
    [SerializableField(6, setter: "private")] private int _firstImmunity;
    [SerializableField(7, setter: "private")] private int _secondImmunity;
    [SerializableField(8, setter: "private")] private int _highResistance;
    [Constructible] public TreasureChart() : this(1, Map.Trammel) { }
    public TreasureChart(int tier, Map facet) : this(tier, facet, TreasureMap.GetRandomLocation(), Random.Shared.Next()) { }
    internal TreasureChart(int tier, Map facet, Point2D site, int seed) : base(facet)
    {
        _tier = tier; _facet = facet; _site = site; _seed = seed;
        (_firstImmunity, _secondImmunity, _highResistance) = TreasureHunts.Defenses(tier, seed);
        Name = $"{TreasureHunts.Tier(tier).Name} treasure map"; Weight = 0.1; Width = 300; Height = 300; Protected = true;
        int x = Math.Clamp(site.X - 300, 0, 4519), y = Math.Clamp(site.Y - 300, 0, 3495);
        Bounds = new(x, y, 600, 600); AddWorldPin(site.X, site.Y);
    }
    internal bool Ready(PlayerMobile owner) => ClassProfessions.Affected(owner) && ClassProfessions.Ready(owner)
        && RuneCrafting.Accessible(owner, this) && (_facet == Map.Felucca || _facet == Map.Trammel);
    internal bool Decode(PlayerMobile owner)
    {
        if (!Ready(owner) || _spent || Decoder != null || !ClassProfessions.Active(owner, SkillName.Cartography)) { return false; }
        double min = TreasureHunts.Tier(_tier).Cartography;
        if (owner.Skills.Cartography.Base < min) { owner.SendMessage($"This map requires {min:F1} Cartography."); return false; }
        if (!owner.CheckSkill(SkillName.Cartography, min, min + 60)) { owner.SendMessage("You fail to decipher the treasure map."); return false; }
        Decoder = owner; LootType = LootType.Blessed; InvalidateProperties(); owner.SendMessage("You decipher the map. Read it again to search for the entrance.");
        var row = TreasureHunts.Tier(_tier);
        owner.SendMessage($"{row.Difficulty} monsters, levels {row.MonsterMinimum}–{row.MonsterMaximum}. {TreasureHunts.DefenseText(_firstImmunity, _secondImmunity, _highResistance)}.");
        if (owner.NetState != null) { base.DisplayTo(owner); TreasureChartGump.DisplayTo(owner, this); } return true;
    }
    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile owner || !Ready(owner)) { return; }
        if (Decoder == null) { Decode(owner); }
        else if (owner.NetState != null) { base.DisplayTo(owner); TreasureChartGump.DisplayTo(owner, this); }
    }
    public override void DisplayTo(Mobile from)
    { if (from is PlayerMobile owner && Ready(owner) && Decoder != null && owner.NetState != null) { base.DisplayTo(owner); } }
    internal static int SearchTolerance(PlayerMobile owner)
    {
        int range = owner.Skills.Mining.Base switch { >= 100 => 4, >= 81 => 3, >= 51 => 2, _ => 1 };
        return ClassMapHooks.SearchRange?.Invoke(owner, range) ?? range;
    }
    internal bool FindEntrance(PlayerMobile owner, IPoint3D point)
    {
        if (!Ready(owner) || Spent || Decoder == null || owner.Map != Facet || !owner.InRange(point, 6)
            || !ClassProfessions.Active(owner, SkillName.Cartography) || !TreasureMap.HasDiggingTool(owner)) { return false; }
        if (!Utility.InRange(new Point2D(point.X, point.Y), Site, SearchTolerance(owner)))
        { owner.SendMessage("You find no entrance there."); return false; }
        var location = new Point3D(Site, owner.Map.GetAverageZ(Site.X, Site.Y));
        if (owner.X == location.X && owner.Y == location.Y || !owner.Map.CanFit(location.X, location.Y, location.Z, 16)
            || SpellHelper.CheckMulti(location, owner.Map) || Region.Find(location, owner.Map).IsPartOf<Server.Regions.HouseRegion>())
        { owner.SendMessage("The entrance is blocked. Your map is unchanged."); return false; }
        return TreasureHunts.Profiles.Begin(owner, this) != null;
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list); var row = TreasureHunts.Tier(_tier);
        list.Add($"{row.Name}\t{_facet?.Name}");
        list.Add($"{"Cartography"}\t{row.Cartography:F1}");
        list.Add($"{(_spent ? "Expedition already uncovered" : Decoder == null ? "Undeciphered treasure map" : "Deciphered; search for its dungeon entrance")}");
        if (Decoder != null) { list.Add($"{TreasureHunts.DefenseText(_firstImmunity, _secondImmunity, _highResistance)}"); }
        list.Add($"{row.KeyName}{" skeleton key opens its guarded chest"}");
    }
    public override void OnAfterDelete() { _decoderSerial = Serial.Zero; _facet = null; base.OnAfterDelete(); }
}
public sealed class TreasureChartGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly TreasureChart _chart;
    private readonly NetState _connection;
    private readonly object _parent;
    private bool _answered;
    public override bool Singleton => true;
    private TreasureChartGump(PlayerMobile owner, TreasureChart chart) : base(60, 45)
    { _owner = owner; _chart = chart; _connection = owner.NetState; _parent = chart.Parent; }
    public static void DisplayTo(PlayerMobile owner, TreasureChart chart)
    { if (owner.NetState != null && chart.Ready(owner) && chart.Decoder != null) { owner.SendGump(new TreasureChartGump(owner, chart)); } }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        var row = TreasureHunts.Tier(_chart.Tier); var session = TreasureHunts.Profiles.Find(_chart.Serial);
        b.AddPage(); b.AddBackground(0, 0, 560, 350, 5054); b.AddLabel(22, 20, 0x481, $"{row.Name} Treasure Expedition");
        b.AddLabel(22, 65, 0x47E, $"{_chart.Facet.Name}; Cartography {row.Cartography:F1}; {row.KeyName} key");
        b.AddLabel(22, 105, 0x47E, "8–12 spacious rooms; owner and pets; two hours to explore.");
        b.AddLabel(22, 140, 0x47E, $"{row.Difficulty} monsters, levels {row.MonsterMinimum}–{row.MonsterMaximum}");
        b.AddLabel(22, 165, 0x47E, $"{TreasureHunts.DefenseText(_chart.FirstImmunity, _chart.SecondImmunity, _chart.HighResistance)}");
        b.AddLabel(22, 205, 0x47E, session != null ? $"{(session.Active ? "Entrance ready" : "Uncovering entrance")}; expires {session.Expires:u}"
            : _chart.Spent ? "This map's expedition has ended." : "Stand near the marked site with a digging tool.");
        if (!_chart.Spent)
        { b.AddButton(22, 245, 4005, 4007, 1); b.AddLabel(57, 245, 0x481, "Search for entrance"); }
        b.AddButton(440, 290, 4017, 4019, 0); b.AddLabel(475, 290, 0x481, "Close");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_answered || sender != _connection || sender.Mobile != _owner) { return; } _answered = true;
        if (info.ButtonID != 1 || !_chart.Ready(_owner) || _chart.Parent != _parent || _chart.Spent
            || _owner.Target != null || _chart.Decoder == null) { return; }
        _owner.Target = new SearchTarget(_chart, _connection, _parent);
        _owner.SendMessage("Search the marked ground within six tiles. Your World map can widen the search tolerance.");
    }
    private sealed class SearchTarget(TreasureChart chart, NetState connection, object parent) : Target(6, true, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object target)
        {
            if (from is not PlayerMobile owner || owner.NetState != connection || chart.Parent != parent || !chart.Ready(owner)
                || chart.Decoder == null || chart.Spent || owner.Map != chart.Facet || target is not IPoint3D point
                || !ClassProfessions.Active(owner, SkillName.Cartography) || !TreasureMap.HasDiggingTool(owner))
            { from.SendMessage("Use your decoded map, Cartography and a digging tool near the correct site."); return; }
            chart.FindEntrance(owner, point);
        }
    }
}
[SerializationGenerator(0)]
public partial class ExpeditionPortal : Item
{
    [SerializableField(0, setter: "private")] private uint _chart;
    [SerializableField(1, setter: "private")] private bool _exit;
    internal Serial ChartSerial => (Serial)_chart;
    [Constructible] public ExpeditionPortal() : this(Serial.MinusOne, false) { }
    internal ExpeditionPortal(Serial chart, bool exit) : base(0xF6C)
    { _chart = chart.Value; _exit = exit; Name = exit ? "treasure dungeon exit" : "treasure dungeon entrance"; Movable = false; Hue = 0x482; }
    public override void OnDoubleClick(Mobile from)
    { if (from is PlayerMobile owner) { TreasureHunts.Profiles.Travel(owner, this); } }
}
[SerializationGenerator(0)]
public partial class ExpeditionChest : MetalChest
{
    [SerializableField(0, setter: "private")] private uint _chart;
    [SerializableField(1, setter: "internal")] private bool _unlocked;
    internal Serial ChartSerial => (Serial)_chart;
    [Constructible] public ExpeditionChest() : this(Serial.MinusOne.Value) { }
    internal ExpeditionChest(uint chart) { _chart = chart; Name = "guarded treasure chest"; Movable = false; Locked = false; TrapType = TrapType.None; }
    private bool Allowed(Mobile from) => from is PlayerMobile && ClassProfessions.Ready(from) && _unlocked
        && TreasureHunts.Session(this) is { Active: true } h && from.Serial == h.Owner && h.Expires > DateTime.UtcNow
        && from.Map == Map && from.InRange(this, 2) && from.InLOS(this);
    public override void OnDoubleClick(Mobile from)
    { if (Allowed(from)) { base.OnDoubleClick(from); } else { from.SendMessage("Defeat the guarding pack and use the matching skeleton key to open your chest."); } }
    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) => Allowed(from) && base.CheckLift(from, item, ref reject);
    public override bool CheckItemUse(Mobile from, Item item) => Allowed(from) && base.CheckItemUse(from, item);
    public override bool CheckHold(Mobile from, Item item, bool message, bool checkItems, int plusItems, int plusWeight) => false;
}
[SerializationGenerator(0)]
public partial class ExpeditionRecovery : Bag
{
    [SerializableField(0, setter: "private")] private uint _owner;
    [Constructible] public ExpeditionRecovery() : this(Serial.MinusOne.Value) { }
    internal ExpeditionRecovery(uint owner) { _owner = owner; Name = "recovered expedition belongings"; Movable = false; MaxItems = 0; }
    public override void OnDoubleClick(Mobile from)
    { if (from.Serial.Value == _owner) { if (Items.Count == 0) { Delete(); } else { base.OnDoubleClick(from); } } }
    public override int DefaultMaxWeight => 0;
    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) => from.Serial.Value == _owner && base.CheckLift(from, item, ref reject);
    public override bool CheckItemUse(Mobile from, Item item) => from.Serial.Value == _owner && base.CheckItemUse(from, item);
    public override bool CheckHold(Mobile from, Item item, bool message, bool checkItems, int plusItems, int plusWeight) => false;
}
