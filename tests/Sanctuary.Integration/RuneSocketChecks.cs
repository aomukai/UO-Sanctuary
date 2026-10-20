using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RuneSocketChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RuneSocketChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "rune-socket-checks.json");
    private sealed record Saved(uint Serial, string State, int Requirement, string Name, ItemOrigin Origin);
    private sealed record SaveData(Saved[] Items, uint Player, int Fhr);
    public static void Configure() { Register("rune-socket-check", Check); Register("rune-socket-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Permanent rune content, actual stats, requirements, copy and persistence", _ => Core.LoopContext.Post(() =>
        { try { action(); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    private static void Require(bool value, string message) => RuneChecks.Require(value, message);
    private static Saved Snapshot(Item item) => new(item.Serial.Value, JsonSerializer.Serialize(ClassGear.State(item)), ClassGear.RequiredLevel(item), item.Name, item.Origin);
    internal static InscribedRune Rune(PlayerMobile owner, string stat = "hit-recovery")
    {
        var m = RuneChecks.Mould(owner, 1, stat); var rune = ItemOrigins.Create(ItemOrigin.Crafted, () => new InscribedRune(RunePattern.From(m), CraftQuality.Average));
        m.Delete(); owner.Backpack.DropItem(rune); return rune;
    }
    internal static Item Host(PlayerMobile owner, Type type = null, int sockets = 5)
    {
        var item = (type ?? typeof(Longsword)).CreateInstance<Item>(); owner.Backpack.DropItem(item);
        ClassGear.Profiles.Convert(item, 99, GearRarity.Normal, new Random(42));
        ClassGear.Profiles.Store(item, ClassGear.State(item) with { TinkerSockets = sockets }); return item;
    }
    private static void Install(PlayerMobile owner, InscribedRune rune, Item host)
    {
        int gold = owner.Backpack.GetAmount(typeof(Gold)); var quote = RuneSockets.Quote(owner, rune, host);
        Require(quote != null && RuneSockets.Commit(quote) && rune.Deleted && !RuneSockets.Commit(quote)
            && owner.Backpack.GetAmount(typeof(Gold)) == gold, "Insertion did not consume rune once without fees");
    }
    private static void Check()
    {
        var p = ImbuingChecks.Player(SkillName.Alchemy); var other = ImbuingChecks.Player(SkillName.Tinkering);
        p.Backpack.MaxItems = other.Backpack.MaxItems = 0; var saved = new List<Saved>();
        foreach (var stat in MouldRewards.Settings.Stats)
        {
            var host = Host(p); var rune = Rune(p, stat.Id); int value = rune.Percentage;
            Install(p, rune, host); Require(ClassGear.Bonus(host, stat.Code) == value, "Rune bonus missing or duplicated: " + stat.Code);
            p.AddItem(host); Require(ClassGear.Bonus(p, stat.Code) == value, "Equipped rune did not use normal bonus aggregation");
            other.Backpack.DropItem(host); Require(ClassGear.Bonus(p, stat.Code) == 0, "Unequipped rune still applied");
            var copy = host.GetType().CreateInstance<Item>(); host.Dupe(copy); other.Backpack.DropItem(copy);
            Require(JsonSerializer.Serialize(ClassGear.State(copy)) == JsonSerializer.Serialize(ClassGear.State(host))
                && !ReferenceEquals(ClassGear.State(copy).Runes, ClassGear.State(host).Runes), "Copy lost/shared installed content array");
            saved.Add(Snapshot(host)); saved.Add(Snapshot(copy));
        }
        foreach (var type in new[] { typeof(PlateHelm), typeof(PlateChest), typeof(MetalKiteShield) })
        {
            var host = Host(p, type); Install(p, Rune(p), host); p.AddItem(host);
            Require(HitRecovery.Fhr(p) == 10, "Non-weapon rune slot inactive"); other.Backpack.DropItem(host); saved.Add(Snapshot(host));
        }
        OffSlot(p);
        var full = Host(p); int ias = CombatTiming.Ias(p), fcr = CombatTiming.Fcr(p), movement = ClassGear.RunBonus(p);
        var low = new PlayerMobile { Body = 0x190, Player = true, Name = "SocketLevelChecks" }; low.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(low, DiabloClass.Necromancer); low.RawStr = low.RawDex = 1000; low.MoveToWorld(p.Location, p.Map);
        for (int count = 1; count <= 5; count++)
        {
            Install(p, Rune(p), full); int floor = 20 + count * 10;
            Require(ClassGear.RequiredLevel(full) == Math.Max(ClassGear.State(full).Roll.RequiredLevel, floor), "Filled socket level floor wrong");
            ClassCreation.Profiles.AdvanceLevel(low, floor - 1); Require(ClassGear.CanEquip(full, low) == false, "Underlevel equip permitted");
            ClassCreation.Profiles.AdvanceLevel(low, floor); Require(ClassGear.CanEquip(full, low) == true, "Eligible equip rejected");
        }
        low.Delete(); p.AddItem(full); Require(HitRecovery.Fhr(p) == 50, "Five FHR runes did not aggregate to 50");
        Require(CombatTiming.Ias(p) == ias && CombatTiming.Fcr(p) == fcr && ClassGear.RunBonus(p) == movement, "FHR changed attack/cast/movement timing");
        var session = ClassCombat.For(p); long now = Core.TickCount; session.RecoveryUntil = now; p.Hits = p.HitsMax;
        Require(HitRecovery.Apply(p, p.HitsMax, now) && session.RecoveryUntil - now == HitRecovery.Frames(DiabloClass.Necromancer, 50, full as BaseWeapon) * ActionTiming.FrameMs,
            "Actual hit recovery did not consume inserted FHR"); session.RecoveryUntil = now;
        var weapon = (BaseWeapon)full; int durability = weapon.HitPoints; weapon.HitPoints = 0;
        Require(HitRecovery.Fhr(p) == 0, "Broken host rune remained active"); weapon.HitPoints = durability;
        Require(HitRecovery.Fhr(p) == 50, "Restored durability did not restore rune bonuses");
        p.Backpack.DropItem(full); ClassProfessions.Profiles.Select(p, SkillName.Blacksmith);
        p.Skills.Blacksmith.Cap = 120; p.Skills.Blacksmith.Base = 120;
        CraftFacilities.Register(new(SkillName.Blacksmith,p.Map.Name,p.X+1,p.Y,p.Z,0xFAF));
        new Static(0xFAF).MoveToWorld(new Point3D(p.X+1,p.Y-1,p.Z), p.Map);
        new Static(0xFB1).MoveToWorld(new Point3D(p.X+1,p.Y,p.Z), p.Map);
        var hammer = new RepairHammer(); p.Backpack.DropItem(hammer); weapon.HitPoints--;
        var repair = EquipmentRepairs.Quote(p, full, hammer); Require(repair != null, "No filled-host repair quote");
        foreach (var cost in repair.Costs)
        { var resource = cost.Type.CreateInstance<Item>(); resource.Amount = cost.Amount; p.Backpack.DropItem(resource); }
        Require(EquipmentRepairs.Commit(repair, out _, new SocketChecks.Rolls(99, 99)), "Filled host repair rejected");
        p.AddItem(full); Require(HitRecovery.Fhr(p) == 50 && ClassGear.State(full).Runes.Length == 5, "Actual repair lost/doubled runes");
        p.Backpack.DropItem(full); var extra = Rune(p); Require(RuneSockets.Quote(p, extra, full) == null && !extra.Deleted, "Full socket contents replaced"); extra.Delete();
        Salvage(p, full); Rejections(p, other); saved.Add(Snapshot(full)); p.AddItem(full);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new SaveData(saved.ToArray(), p.Serial.Value, HitRecovery.Fhr(p))));
        _logger.Information("Rune socket coverage: all15 effects, four slots, global off-slot percent effects, five real FHR consumers, level30–70 gates, copy/reload, break/restore and stale/foreign guards passed");
    }
    private static void OffSlot(PlayerMobile p)
    {
        var weapon = (BaseWeapon)Host(p); var head = Host(p, typeof(PlateHelm)); p.AddItem(weapon); p.AddItem(head);
        var baseline = ClassGear.Damage(weapon); p.Backpack.DropItem(head); Install(p, Rune(p, "enhanced-damage"), head); p.AddItem(head);
        Require(ClassGear.Damage(weapon).Max > baseline.Max, "Damage rune in helmet was inert");
        p.Backpack.DropItem(weapon); Install(p, Rune(p, "enhanced-defense"), weapon); int defense = ClassGear.Defense(head); p.AddItem(weapon);
        Require(ClassGear.Defense(head) > defense, "Defense rune in weapon was inert"); weapon.Delete(); head.Delete();
    }
    private sealed class Yield : Random
    {
        public override int Next(int max) => 0;
        public override int Next(int min, int max) => min;
    }
    internal static void Salvage(PlayerMobile p, Item filled)
    {
        var tool = new SmithHammer(); p.Backpack.DropItem(tool); int uses = tool.UsesRemaining;
        var host = filled.GetType().CreateInstance<Item>(); filled.Dupe(host); p.Backpack.DropItem(host);
        var plain = filled.GetType().CreateInstance<Item>(); filled.Dupe(plain); p.Backpack.DropItem(plain);
        ClassGear.Profiles.Store(plain, ClassGear.State(plain) with { Runes = [], Gems = [] });
        int runes = p.Backpack.Items.OfType<InscribedRune>().Count();
        SalvageResult withRunes = null;
        var q = EquipmentEconomy.Quote(p, host, EssenceAction.Salvage, tool);
        Require(q != null && EquipmentEconomy.Commit(p, q, out withRunes, new Yield()) && host.Deleted, "Filled host salvage failed");
        q = EquipmentEconomy.Quote(p, plain, EssenceAction.Salvage, tool);
        Require(q != null && EquipmentEconomy.Commit(p, q, out var withoutRunes, new Yield()) && plain.Deleted
            && withRunes == withoutRunes && withRunes.Material > 0 && tool.UsesRemaining == uses
            && p.Backpack.Items.OfType<InscribedRune>().Count() == runes, "Rune contents changed salvage yield or were recovered");
    }
    private static void Rejections(PlayerMobile p, PlayerMobile other)
    {
        var host = Host(p); var rune = Rune(p);
        void Reject(Action mutate, Action restore, string why)
        { var q = RuneSockets.Quote(p, rune, host); Require(q != null, "No quote: " + why); mutate(); Require(!RuneSockets.Commit(q), "Accepted " + why); restore(); }
        Reject(() => other.Backpack.DropItem(rune), () => p.Backpack.DropItem(rune), "foreign rune");
        Reject(() => other.Backpack.DropItem(host), () => p.Backpack.DropItem(host), "foreign host");
        Reject(() => ((BaseWeapon)host).HitPoints--, () => ((BaseWeapon)host).HitPoints++, "host wear changed");
        Reject(() => p.Frozen = true, () => p.Frozen = false, "frozen owner");
        var state = ClassGear.State(host);
        Reject(() => ClassGear.Profiles.Store(host, state with { Identified = false }), () => ClassGear.Profiles.Store(host, state), "hidden host");
        var old = RuneSockets.Quote(p, rune, host); var current = RuneSockets.Quote(p, rune, host);
        Require(!RuneSockets.Commit(old), "Old insertion stayed valid"); RuneSockets.End(p, current.Session); Require(!RuneSockets.Commit(current), "Cancelled insertion consumed");
        Require(!rune.Deleted && ClassGear.State(host).Runes.Length == 0, "Rejected insertion changed state");
        p.Backpack.MaxItems = p.Backpack.TotalItems; Install(p, rune, host); p.Backpack.MaxItems = 0;
        var invalid = Rune(p); var noSocket = Host(p, sockets: 0); Require(RuneSockets.Quote(p, invalid, noSocket) == null, "Unsocketed host accepted");
        var vendor = ItemOrigins.Create(ItemOrigin.Vendor, () => new Longsword()); p.Backpack.DropItem(vendor); ClassGear.Profiles.Ensure(vendor);
        Require(RuneSockets.Quote(p, invalid, vendor) == null, "Vendor host accepted"); host.Delete(); noSocket.Delete(); vendor.Delete(); invalid.Delete();
    }
    private static void Reload()
    {
        var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(Manifest));
        foreach (var row in data.Items)
        { Require(World.FindItem((Serial)row.Serial) is { } item && Snapshot(item) == row, "Installed content/requirements changed on reload"); }
        Require(World.FindMobile((Serial)data.Player) is PlayerMobile p && HitRecovery.Fhr(p) == data.Fhr, "Reload changed actual FHR");
    }
}
