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

public static class GemActivationChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(GemActivationChecks));
    private sealed record Saved(uint Serial, string State, int Level);
    private static string Manifest => Path.Join(Core.BaseDirectory, "gem-activation.json");
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    public static void Configure() { Register("gem-activation-check", Check); Register("gem-activation-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Permanent gems and actual effect consumers", _ => Core.LoopContext.Post(() =>
    { try { action(); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    private static Saved Snapshot(Item item) => new(item.Serial.Value, JsonSerializer.Serialize(ClassGear.State(item)), ClassGear.RequiredLevel(item));
    internal static SocketGem Gem(PlayerMobile p, string family, GemQuality quality = GemQuality.Perfect, int amount = 1)
    { var gem = ItemOrigins.Create(ItemOrigin.Drop, () => new SocketGem(family, quality, amount)); p.Backpack.DropItem(gem); return gem; }
    internal static void Install(PlayerMobile p, SocketGem gem, Item host)
    {
        int amount = gem.Amount, gold = p.Backpack.GetAmount(typeof(Gold)); var q = GemSockets.Quote(p, gem, host);
        Require(q != null && GemSockets.Commit(q) && !GemSockets.Commit(q) && (amount == 1 ? gem.Deleted : gem.Amount == amount - 1)
            && p.Backpack.GetAmount(typeof(Gold)) == gold, "Gem insertion did not consume exactly one without fees");
    }
    private static void Check()
    {
        var p = ImbuingChecks.Player(SkillName.Alchemy); p.Backpack.MaxItems = 0;
        var saved = new List<Saved>();
        foreach (var family in SocketGems.Settings.Families)
        {
            foreach (var quality in Enum.GetValues<GemQuality>())
            {
                foreach (var type in new[] { typeof(Longsword), typeof(MetalKiteShield), typeof(PlateHelm), typeof(PlateChest) })
                {
                    var host = RuneSocketChecks.Host(p, type); Install(p, Gem(p, family.Id, quality), host);
                    var state = ClassGear.State(host); var gem = state.Gems.Single();
                    var effects = GemSockets.Effects(host, gem);
                    Require(JsonSerializer.Serialize(effects) == JsonSerializer.Serialize(ClassGear.Slot(host) switch
                    { GearSlot.Weapon => family.Effects[(int)quality].Weapon, GearSlot.Shield => family.Effects[(int)quality].Shield, _ => family.Effects[(int)quality].Armor }), "Wrong host effects");
                    p.AddItem(host); ClassGear.Refresh(p);
                    foreach (var e in effects) { Require(ClassGear.Bonus(p, e.Code) == e.Minimum, "Bonus missing/duplicated: " + e.Code); }
                    Actual(p, host, effects);
                    p.Backpack.DropItem(host); ClassGear.Refresh(p);
                    foreach (var e in effects) { Require(ClassGear.Bonus(p, e.Code) == 0, "Unequipped gem remained active"); }
                    var copy = type.CreateInstance<Item>(); host.Dupe(copy); p.Backpack.DropItem(copy);
                    Require(Snapshot(host).State == Snapshot(copy).State && !ReferenceEquals(state.Gems, ClassGear.State(copy).Gems)
                        && !ReferenceEquals(gem.Effects.Weapon, ClassGear.State(copy).Gems[0].Effects.Weapon), "Copy lost or shared gem arrays");
                    saved.Add(Snapshot(host)); saved.Add(Snapshot(copy));
                }
            }
        }
        var storage = ImbuingChecks.Player(SkillName.Tinkering); storage.Backpack.MaxItems = 0;
        foreach (var row in saved)
        {
            var item = World.FindItem((Serial)row.Serial); storage.Backpack.DropItem(item);
            Require(Snapshot(item) == row, "Trade changed frozen gem identity");
        }
        Mixed(p); Guards(p); Combat(p); RepairSalvage(p); PersistenceMigration(p, saved);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        _logger.Information("Gem activation coverage: 180 family/quality/host cases, 360 saved originals/copies, mixed sockets, actual stats/combat/regen, stale guards and migration passed");
    }
    private static void Actual(PlayerMobile p, Item host, GemEffect[] effects)
    {
        foreach (var e in effects)
        {
            int b = e.Minimum;
            switch (e.Code)
            {
                case "str": Require(p.Str >= p.RawStr + b, "Strength did not apply"); break;
                case "dex": Require(p.Dex >= p.RawDex + b, "Dexterity did not apply"); break;
                case "hp": Require(AosAttributes.GetValue(p, AosAttribute.BonusHits) == b, "Life pool consumer missing"); break;
                case "mana": Require(AosAttributes.GetValue(p, AosAttribute.BonusMana) == b, "Mana pool consumer missing"); break;
                case "att": Require(ClassCreation.Profiles.TryGet(p.Serial, out var profile) && ClassStats.AttackRating(p) == Math.Max(1, 5 * p.Dex - 35 + ClassStats.For(profile.Class).AttackBonus + b), "Attack rating consumer missing"); break;
                case "ac": Require(ClassGear.Defense(host) >= ClassGear.State(host).Defense + b, "Shield defense missing"); break;
                case "mag%": Require(GearDrops.MagicFind(p) == b, "Magic Find consumer missing"); break;
                case "res-all": Require(p.FireResistance == b && p.ColdResistance == b && p.PoisonResistance == b && p.EnergyResistance == b, "All resistance wrong"); break;
                case "res-fire": Require(p.FireResistance == b, "Fire resistance wrong"); break;
                case "res-cold": Require(p.ColdResistance == b, "Cold resistance wrong"); break;
                case "res-pois": Require(p.PoisonResistance == b, "Poison resistance wrong"); break;
                case "res-ltng": Require(p.EnergyResistance == b, "Lightning resistance wrong"); break;
                case "replenish-life": Require(GearEffects.RegenerationRate(p, TimeSpan.FromSeconds(10), "life-per-five").TotalSeconds < 10, "Life regeneration inert"); break;
                case "mana-regen%": Require(Math.Abs(GearEffects.RegenerationRate(p, TimeSpan.FromSeconds(2), "mana-per-five").TotalSeconds - 2.0 * 100 / (100 + b)) < .00001, "Mana regeneration wrong"); break;
                case "fire-damage": Require(GearEffects.Damage(p, 0).Fire == b && GearEffects.Damage(p, 1).Fire == e.Maximum, "Fire damage range wrong"); break;
                case "cold-damage": Require(GearEffects.Damage(p, 0).Cold == b && GearEffects.Damage(p, 1).ColdMs == e.DurationMs, "Cold range/duration wrong"); break;
                case "lightning-damage": Require(GearEffects.Damage(p, 0).Lightning == b && GearEffects.Damage(p, 1).Lightning == e.Maximum, "Lightning damage wrong"); break;
                case "poison-damage": Require(Math.Abs(GearEffects.Damage(p, 0).Poison - b) < .00001 && GearEffects.Damage(p, 0).PoisonMs == e.DurationMs, "Poison damage/duration wrong"); break;
                case "magic-reduction": Require(GemCombat.Component(p, 100, 0, true) == 100 - b, "MDR inert"); break;
            }
        }
    }
    private static void Mixed(PlayerMobile p)
    {
        var host = RuneSocketChecks.Host(p); var stack = Gem(p, "Ruby", amount: 3);
        Install(p, stack, host); Install(p, stack, host); Require(stack.Amount == 1, "Stack consumed incorrectly");
        RuneSockets.Commit(RuneSockets.Quote(p, RuneSocketChecks.Rune(p), host));
        Install(p, stack, host); RuneSockets.Commit(RuneSockets.Quote(p, RuneSocketChecks.Rune(p), host));
        Require(RuneSockets.Filled(ClassGear.State(host)) == 5 && ClassGear.RequiredLevel(host) == 70, "Mixed level/capacity wrong");
        var extra = Gem(p, "Amber"); Require(GemSockets.Quote(p, extra, host) == null && RuneSockets.Quote(p, RuneSocketChecks.Rune(p), host) == null, "Mixed full host accepted");
        p.AddItem(host); Require(HitRecovery.Fhr(p) == 20 && GearEffects.Damage(p, 0).Fire == 45, "Mixed bonuses wrong");
        ((BaseWeapon)host).HitPoints = 0; Require(ClassGear.Bonus(p, "balance1") == 0 && GearEffects.Damage(p, 0).Fire == 0, "Broken gem active");
        host.Delete(); extra.Delete();
    }
    private static void Guards(PlayerMobile p)
    {
        var other = ImbuingChecks.Player(SkillName.Tinkering); var host = RuneSocketChecks.Host(p); var gem = Gem(p, "Sapphire", amount: 2);
        void Reject(Action change, Action restore, string why)
        { var q = GemSockets.Quote(p, gem, host); Require(q != null, "No quote: " + why); change(); Require(!GemSockets.Commit(q), "Accepted " + why); restore(); }
        Reject(() => gem.Amount++, () => gem.Amount--, "changed stack");
        Reject(() => other.Backpack.DropItem(gem), () => p.Backpack.DropItem(gem), "foreign gem");
        Reject(() => other.Backpack.DropItem(host), () => p.Backpack.DropItem(host), "foreign host");
        Reject(() => p.Frozen = true, () => p.Frozen = false, "frozen player");
        var state = ClassGear.State(host);
        Reject(() => ClassGear.Profiles.Store(host, state with { Identified = false }), () => ClassGear.Profiles.Store(host, state), "hidden host");
        var q = GemSockets.Quote(p, gem, host); var runeQ = RuneSockets.Quote(p, RuneSocketChecks.Rune(p), host);
        Require(!GemSockets.Commit(q) && RuneSockets.Commit(runeQ), "Shared rune/gem session allowed stale commit");
        q = GemSockets.Quote(p, gem, host); RuneSockets.End(p, q.Session); Require(!GemSockets.Commit(q), "Cancelled gem consumed");
        p.Backpack.MaxItems = p.Backpack.TotalItems; Install(p, gem, host); p.Backpack.MaxItems = 0;
        var vendor = ItemOrigins.Create(ItemOrigin.Vendor, () => new Longsword()); p.Backpack.DropItem(vendor); ClassGear.Profiles.Ensure(vendor);
        Require(GemSockets.Quote(p, gem, vendor) == null, "Vendor host accepted");
        host.Delete(); gem.Delete(); vendor.Delete();
    }
    private static void Combat(PlayerMobile p)
    {
        var host = RuneSocketChecks.Host(p); Install(p, Gem(p, "Ruby"), host); Install(p, Gem(p, "Sapphire"), host); Install(p, Gem(p, "Citrine"), host);
        p.AddItem(host);
        var target = new Zombie(); target.MoveToWorld(p.Location, p.Map); CreatureProfiles.Initialize(target); target.HitsMaxSeed = 10000; target.Hits = target.HitsMax;
        var d = GearEffects.Damage(p, 0); int before = target.Hits;
        int expect = (int)Math.Floor(d.Physical * (100 - target.PhysicalResistance) / 100 + d.Fire * (100 - target.FireResistance) / 100
            + d.Cold * (100 - target.ColdResistance) / 100 + d.Lightning * (100 - target.EnergyResistance) / 100);
        GearEffects.Strike(p, target, 0); Require(before - target.Hits == expect && GearEffects.ChillPercent(target) > 0, "Real elemental strike/chill wrong");
        host.Delete(); GearEffects.Clear(target);
        host = RuneSocketChecks.Host(p); Install(p, Gem(p, "Emerald"), host); Install(p, Gem(p, "Emerald"), host); p.AddItem(host);
        d = GearEffects.Damage(p, 0); Require(Math.Abs(d.Poison - 200) < .00001 && d.PoisonMs == 7000, "Repeated emerald duration stacked");
        long now = Core.TickCount; before = target.Hits; GearEffects.ApplyPoison(p, target, d.Poison, now, d.PoisonMs); GearEffects.Tick(now + 7000);
        Require(before - target.Hits == (int)(200 * (100 - target.PoisonResistance) / 100.0), "Actual poison total wrong"); host.Delete(); GearEffects.Clear(target);
        host = RuneSocketChecks.Host(p); Install(p, Gem(p, "Diamond"), host); p.AddItem(host);
        Require(GearEffects.Damage(p, 0, target).Physical > GearEffects.Damage(p, 0).Physical, "Undead damage inert"); host.Delete();
        host = RuneSocketChecks.Host(p); Install(p, Gem(p, "Tourmaline"), host); p.AddItem(host); p.Hits = p.HitsMax - 50; p.Mana = p.ManaMax - 50;
        int hp = p.Hits, mana = p.Mana; GemCombat.Leech(p, target, 100);
        int drain = GemCombat.Drain(target);
        Require(p.Hits == hp + 4 * drain / 100 && p.Mana == mana + 3 * drain / 100, "Leech actual pools wrong");
        p.Hits = p.HitsMax - 50; p.Mana = p.ManaMax - 50; hp = p.Hits; mana = p.Mana;
        int hp256=SkillHealth.Balance256(p),mp256=NecromancerEffects.ManaBalance256(p);
        var packet=GearEffects.PrepareWeaponImpact(p,target,0,new Random(173),GearEffects.Damage(p,0,target),projectileCritical:false);
        int physical256=Math.Min(NamedElements.To256(packet.Parts.Physical),SkillHealth.Balance256(target));
        GearEffects.Strike(p,target,0,preparedImpact:packet);
        int leech(int percent)=>(int)((long)physical256*(percent*64)/100*drain/100/64);
        Require(SkillHealth.Balance256(p)==hp256+leech(4)&&NecromancerEffects.ManaBalance256(p)==mp256+leech(3),
            "Actual weapon strike did not leech once from clipped fractional physical damage");
        foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
        {
            var zombie = new Zombie(); MonsterVariants.Assign(zombie, difficulty);
            p.Hits = p.HitsMax - 50; p.Mana = p.ManaMax - 50; hp = p.Hits; mana = p.Mana;
            GemCombat.Leech(p, zombie, 300); int divisor = difficulty == MonsterDifficulty.Normal ? 1 : difficulty == MonsterDifficulty.Nightmare ? 2 : 3;
            Require(p.Hits == hp + 300 * 4 * GemCombat.Drain(zombie) / (10000 * divisor) && p.Mana == mana + 300 * 3 * GemCombat.Drain(zombie) / (10000 * divisor), "Difficulty leech wrong"); zombie.Delete();
        }
        var skeleton = new Skeleton(); CreatureProfiles.Initialize(skeleton); hp = p.Hits; mana = p.Mana;
        GemCombat.Leech(p, skeleton, 1000); Require(GemCombat.Drain(skeleton) == 0 && p.Hits == hp && p.Mana == mana, "Skeleton provided leech"); skeleton.Delete();
        host.Delete();
        host = RuneSocketChecks.Host(p, typeof(PlateHelm)); Install(p, Gem(p, "StarSapphire"), host); Install(p, Gem(p, "Amber"), host); p.AddItem(host); ClassGear.Refresh(p);
        p.Hits = p.HitsMax; hp = p.Hits; AOS.Damage(p, target, 20, 0, 100, 0, 0, 0);
        Require(hp - p.Hits == 15, "Native spell did not apply flat MDR");
        int returnBefore=SkillHealth.Balance256(target);p.Hits=p.HitsMax;AOS.Damage(p,target,10,false,100,0,0,0,0,weaponDamage:true);
        int returned256=20*256*(100-target.PhysicalResistance)/100;
        Require(returnBefore-SkillHealth.Balance256(target)==returned256,
            $"Native melee flat ATD:actual={returnBefore-SkillHealth.Balance256(target)} expected={returned256} resistance={target.PhysicalResistance}");
        before = target.Hits; AOS.Damage(p, target, 10, false, 100, 0, 0, 0, 0, archer: true, weaponDamage: true);
        Require(target.Hits == before, "Ranged attack triggered thorns");
        Require(GemCombat.Component(p, 5, 0, true) == 0 && GemCombat.Component(p, 100, 50, true) == 47.5
            && GemCombat.Component(p, 100, 50, false) == 50, "MDR ordering/zero or physical exclusion wrong");
        host.Delete(); target.Delete();
    }
    private static void RepairSalvage(PlayerMobile p)
    {
        var host = (BaseWeapon)RuneSocketChecks.Host(p); Install(p, Gem(p, "Ruby"), host);
        Require(RuneSockets.Commit(RuneSockets.Quote(p, RuneSocketChecks.Rune(p), host)), "Mixed repair setup failed");
        ClassProfessions.Profiles.Select(p, SkillName.Blacksmith); p.Skills.Blacksmith.Cap = 120; p.Skills.Blacksmith.Base = 120;
        CraftFacilities.Register(new(SkillName.Blacksmith,p.Map.Name,p.X+1,p.Y,p.Z,0xFAF));
        new Static(0xFAF).MoveToWorld(new Point3D(p.X+1,p.Y-1,p.Z), p.Map); new Static(0xFB1).MoveToWorld(new Point3D(p.X+1,p.Y,p.Z), p.Map);
        var tool = new RepairHammer(); p.Backpack.DropItem(tool); host.HitPoints = 0; var quote = EquipmentRepairs.Quote(p, host, tool);
        Require(quote != null, "Broken gem host not repairable");
        foreach (var cost in quote.Costs) { var resource = cost.Type.CreateInstance<Item>(); resource.Amount = cost.Amount; p.Backpack.DropItem(resource); }
        string state = Snapshot(host).State;
        Require(EquipmentRepairs.Commit(quote, out _, new SocketChecks.Rolls(99, 99)) && Snapshot(host).State == state, "Repair changed gem identity");
        p.AddItem(host); Require(GearEffects.Damage(p, 0).Fire == 15 && HitRecovery.Fhr(p) == 10, "Repair did not restore mixed effects"); p.Backpack.DropItem(host);
        int gems = p.Backpack.Items.OfType<SocketGem>().Count(); RuneSocketChecks.Salvage(p, host);
        Require(p.Backpack.Items.OfType<SocketGem>().Count() == gems, "Salvage recovered gems"); host.Delete(); tool.Delete();
    }
    private static void PersistenceMigration(PlayerMobile p, List<Saved> saved)
    {
        Require(saved.Count == 360, "Incomplete family/quality/host coverage");
        var host = RuneSocketChecks.Host(p); var rune = RuneSocketChecks.Rune(p);
        Require(RuneSockets.Commit(RuneSockets.Quote(p, rune, host)), "No rune migration fixture");
        string state = Snapshot(host).State; byte[] current = ItemStateChecks.Serialize();
        ClassGear.Profiles.Deserialize(new BufferReader(ItemStateChecks.VersionZero(4)));
        Require(Snapshot(host).State == state && ClassGear.State(host).Runes.Length == 1 && ClassGear.State(host).Gems.Length == 0,
            "Version4 migration changed existing rune or fields");
        ClassGear.Profiles.Deserialize(new BufferReader(current)); host.Delete();
        foreach (var row in saved) { Require(Snapshot(World.FindItem((Serial)row.Serial)) == row, "Version5 roundtrip lost gem"); }
    }
    private static void Reload()
    {
        foreach (var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        { Require(World.FindItem((Serial)row.Serial) is { } item && Snapshot(item) == row, "Gem identity/effects changed after reload"); }
    }
}
