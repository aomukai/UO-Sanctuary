using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Server;
using Server.Items;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class NamedGearChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(NamedGearChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "named-gear-checks.json");
    private sealed record SavedItem(uint Serial, string State, string Name);
    private sealed record Saved(uint Owner, SavedItem[] Items);
    private sealed record SavedThrow(uint Item, int Remaining, int Maximum, DateTime Due, int Period, string State, int Durability);
    private sealed class FirstRoll(int quality = 0) : Random
    {
        public override int Next(int max) => max == 100000 ? quality : 0;
        public override int Next(int min, int max) => min;
    }
    public static void Configure()
    { Register("named-gear-check", Check); Register("named-gear-reload-check", Reload); }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable named gear eligibility, effect and persistence checks", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Command}: passed", command); } catch (Exception e) { _log.Error(e, "{Command}: failed", command); } }));
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static PlayerMobile Player()
    {
        var player = InsuranceChecks.Player();
        FixtureSpace.Place(player, new Point3D(5550, 1151, 0), Map.Trammel);
        return player;
    }
    private static string State(Item item) => JsonSerializer.Serialize(ClassGear.State(item));
    private static void Check()
    {
        var p = Player(); p.Name = "NamedGearCheck";
        p.Backpack.MaxItems = 2000;
        var saved = new List<SavedItem>();
        var report = new List<object>();
        var wearers = new Dictionary<string, PlayerMobile>();
        foreach (var definition in NamedGear.Catalog.Items)
        {
            bool ready = NamedGear.Available(definition.Id);
            report.Add(new { definition.Id, definition.Name, definition.Rarity, definition.BaseCode, definition.SetId,
                definition.SourceAvailability, Available = ready,
                Reasons = NamedGear.Unavailable.GetValueOrDefault(definition.Id) ?? [] });
            if (!ready) { continue; }
            var item = NamedGear.Create(definition.Id, 99, new Random(definition.SourceId + 731));
            p.Backpack.DropItem(item);
            var state = ClassGear.State(item);
            Require(state.NamedItemId == definition.Id && state.Roll.Affixes.Length == 0 && item.Origin == ItemOrigin.Drop,
                "Named factory lost identity/origin: " + definition.Id);
            Require(item.Name == definition.Name && state.Roll.RequiredLevel >= NamedGear.Catalog.SourceRequiredLevel(definition.Id),
                "Named identity/requirements changed: " + definition.Id);
            var basis = NamedGear.Catalog.Base(definition.BaseCode);
            string requiredClass = basis.RequiredClass ?? "nec";
            if (!wearers.TryGetValue(requiredClass, out var wearer))
            {
                wearer = new PlayerMobile { Body = 0x190, Player = true, Name = "NamedAppearance" };
                new Account("named-equip-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))[0] = wearer;
                wearer.AddItem(new Backpack());
                var characterClass = requiredClass switch
                { "ama" => DiabloClass.Amazon, "sor" => DiabloClass.Sorceress, "pal" => DiabloClass.Paladin,
                    "bar" => DiabloClass.Barbarian, "dru" => DiabloClass.Druid, "ass" => DiabloClass.Assassin, _ => DiabloClass.Necromancer };
                ClassCreation.Profiles.Assign(wearer, characterClass); ClassCreation.Profiles.AdvanceLevel(wearer, 99);
                FixtureSpace.Place(wearer, new Point3D(5550, 1151, 0), Map.Trammel);
                wearer.RawStr = wearer.RawDex = wearer.RawInt = 1000; wearers.Add(requiredClass, wearer);
            }
            EquipAppearance(item, wearer, definition.Id);
            p.Backpack.DropItem(item);
            foreach (string appearance in NamedGear.Appearances(definition.Id))
            {
                if (appearance == item.GetType().Name) { continue; }
                var alternate = NamedGear.Create(definition.Id, 99, new Random(183), true, appearance);
                EquipAppearance(alternate, wearer, definition.Id); alternate.Delete();
            }
            ClassGear.Profiles.Store(item, state);
            var copy = ItemOrigins.Create(ItemOrigin.Drop, () => item.GetType().CreateInstance<Item>());
            ClassGear.Profiles.Copy(item, copy);
            Require(State(copy) == State(item) && !ReferenceEquals(ClassGear.State(copy).NamedProperties, state.NamedProperties),
                "Named state copy changed or shared rolls: " + definition.Id);
            copy.Delete();
            Require(EquipmentEconomy.EssenceRange(state.Roll.Rarity) is { }, "No named salvage range");
            Require(EquipmentInsurance.Cost(item) > 0, "No named insurance price: " + definition.Id + " " + item.GetType().Name + " " + item.LootType);
            saved.Add(new(item.Serial.Value, State(item), item.Name));
        }
        Require(saved.Count > 0, "No named items available");
        SetEffects(); SkillRanks(); Insurance(); Upkeep(); Statuses(); SharedStatuses(); Maintenance(); DamageToMana(); Throwing(); Control(); StaminaDrain(); RestInPeace(); FireRanks(); Projectiles(); SetVisuals(); FadeVisual(); BloodVisual(); Combat(); Elements(); MagicPierce(); NamedLifecycleChecks.Check(); LegacyMigration(); Acquisition();
        File.WriteAllText(Path.Join(Core.BaseDirectory, "named-availability.json"), JsonSerializer.Serialize(report));
        string destination = Environment.GetEnvironmentVariable("SANCTUARY_NAMED_AVAILABILITY");
        if (!string.IsNullOrEmpty(destination)) { File.WriteAllText(destination, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }
        string audit = Environment.GetEnvironmentVariable("SANCTUARY_NAMED_AUDIT");
        if (!string.IsNullOrEmpty(audit))
        {
            string root = Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT")
                ?? throw new InvalidOperationException("Named runtime audit requires SANCTUARY_PROJECT_ROOT");
            string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            var inputs = new[] { "src/Sanctuary.Content", "shared", "data/Sanctuary/Balance" }
                .SelectMany(dir => Directory.GetFiles(Path.Join(root, dir), "*", SearchOption.TopDirectoryOnly))
                .Where(p => p.EndsWith(".cs", StringComparison.Ordinal) || p.EndsWith(".json", StringComparison.Ordinal))
                .Concat(Directory.GetFiles(Path.Join(root, "patches/ModernUO"), "*.patch"))
                .Concat(Directory.GetFiles(Path.Join(root, "patches/ClassicUO"), "*.patch"))
                .Concat(new[] { Path.Join(root, "tests/Sanctuary.Integration/NamedGearChecks.cs"), Path.Join(root, "tests/Sanctuary.Integration/NamedLifecycleChecks.cs"), Path.Join(root, "tests/Sanctuary.Integration/NamedVisualUiChecks.cs") })
                .OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => Path.GetRelativePath(root, p).Replace('\\', '/'), Hash);
            File.WriteAllText(audit, JsonSerializer.Serialize(new
            {
                SchemaVersion = 1, ContentAssemblySha256 = Hash(typeof(NamedGear).Assembly.Location),
                NativeAssemblySha256 = Hash(typeof(BaseWeapon).Assembly.Location),
                InputHashes = inputs, Definitions = report
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, saved.ToArray())));
        _log.Information("Named factory checked {Count} available definitions", saved.Count);
    }
    private static void EquipAppearance(Item item, PlayerMobile wearer, string id)
    {
        wearer.Backpack.DropItem(item);
        bool equipped = wearer.EquipItem(item);
        Require(equipped && ClassGear.Active(wearer, item),
            $"Named appearance could not equip actively: {id}/{item.GetType().Name}, equipped={equipped}, classAllowed={ClassGear.CanEquip(item, wearer)}, nativeAllowed={item.CanEquip(wearer)}, checkAllowed={wearer.CheckEquip(item)}, stats={wearer.Str}/{wearer.Dex}/{wearer.Int}, slot={ClassGear.Slot(item)}, broken={ClassGear.Broken(item)}, layer={item.Layer}, conflict={wearer.FindItemOnLayer(item.Layer)?.GetType().Name}");
        BaseArmor.ValidateMobile(wearer); BaseClothing.ValidateMobile(wearer);
        Require(item.Parent == wearer, "Native appearance validation removed named gear: " + id + "/" + item.GetType().Name);
        using var opl = new ObjectPropertyList(item); item.GetProperties(opl); opl.Terminate();
        string text = Encoding.Unicode.GetString(opl.Buffer.AsSpan(1));
        Require(!text.Contains("[Skill]", StringComparison.Ordinal) && !text.Contains("[Class]", StringComparison.Ordinal),
            "Named tooltip leaked unresolved skill/class placeholders: " + id);
        wearer.Backpack.DropItem(item);
    }
    private static Item Named(string sourceKey, PlayerMobile owner, bool identified = true)
    {
        var definition = NamedGear.Catalog.Items.Single(r => r.SourceKey == sourceKey);
        var item = NamedGear.Create(definition.Id, 99, new Random(317), identified);
        owner.Backpack.DropItem(item); return item;
    }
    private static void SetEffects()
    {
        var p = Player(); p.RawStr = 1000; p.RawDex = 1000;
        var items = new[] { "Civerb's Ward", "Civerb's Icon", "Civerb's Cudgel" }.Select(n => Named(n, p)).ToArray();
        var ward = items[0];
        int bonusMana = ClassGear.State(ward).NamedProperties.Single(r => r.Key == "a/1a").Value;
        int bonusPoison = ClassGear.State(ward).NamedProperties.Single(r => r.Key == "a/2a").Value;
        int mana = ClassGear.Bonus(ward, "mana"), poison = ClassGear.Bonus(ward, "res-pois");
        Require(p.EquipItem(ward), "Cannot equip Civerb shield");
        Require(ClassGear.Bonus(ward, "mana") == mana, "Inactive specific-piece bonus applied");
        Require(p.EquipItem(items[1]) && ClassGear.Bonus(ward, "mana") == mana + bonusMana,
            "Civerb's Icon did not activate its exact partner bonus");
        Require(p.EquipItem(items[2]) && ClassGear.Bonus(ward, "res-pois") == poison + bonusPoison,
            "Civerb's Cudgel did not activate its exact partner bonus");
        Require(NamedGear.SetBonus(p, "str") == 15 && NamedGear.SetBonus(p, "dmg-undead%") == 200,
            "Complete Civerb bonuses are missing or repeated per piece");
        var state = ClassGear.State(items[2]);
        ClassGear.Profiles.Store(items[2], state with { Identified = false });
        Require(ClassGear.Bonus(ward, "res-pois") == poison && NamedGear.SetBonus(p, "str") == 0,
            "Unidentified set piece enabled set bonuses or cache remained stale");
        ClassGear.Profiles.Store(items[2], state);
        var weapon = (BaseWeapon)items[2]; int hits = weapon.HitPoints; weapon.HitPoints = 0;
        Require(NamedGear.SetBonus(p, "str") == 0 && ClassGear.Bonus(ward, "res-pois") == poison,
            "Broken piece enabled its set bonuses");
        weapon.HitPoints = hits;
        Require(NamedGear.SetBonus(p, "str") == 15, "Repair did not restore set eligibility");
        p.Backpack.DropItem(items[1]);
        Require(ClassGear.Bonus(ward, "mana") == mana && NamedGear.SetBonus(p, "str") == 0,
            "Unequip retained set stats");
        foreach (var item in items) { item.Delete(); }
    }
    private static void SkillRanks()
    {
        var p = Player();
        Require(ClassCreation.Profiles.TryTrain(p.Serial, "necromancer.teeth", out _), "Cannot allocate base Teeth rank");
        int hard = ClassCreation.Profiles.GetRank(p.Serial, "necromancer.teeth");
        var ring = Named("The Stone of Jordan", p, false);
        Require(ring.Name != "The Stone of Jordan" && ClassGear.Bonus(ring, "allskills") == 0,
            "Unidentified item exposed its identity or effects");
        ClassGear.Profiles.Store(ring, ClassGear.State(ring) with { Identified = true });
        Require(ring.Name == "The Stone of Jordan" && p.EquipItem(ring), "Identification did not reveal/equip named item");
        Require(NamedSkills.Rank(p, "necromancer.teeth") == hard + 1
            && ClassCreation.Profiles.GetRank(p.Serial, "necromancer.teeth") == hard
            && NamedSkills.Rank(p, "necromancer.bonearmor") == 0, "Gear ranks spent points or granted unlearned skills");
        p.Backpack.DropItem(ring);
        Require(NamedSkills.Rank(p, "necromancer.teeth") == hard, "Unequip retained skill ranks");
        var state = ClassGear.State(ring);
        bool rejected = false;
        try { ClassGear.Profiles.Store(ring, state with { NamedProperties = [new("p/999", 1, 1)] }); }
        catch (InvalidDataException) { rejected = true; }
        Require(rejected && ReferenceEquals(ClassGear.State(ring), state), "Forged named roll accepted or changed saved state");
        ring.Delete();
    }
    private static void Insurance()
    {
        var p = Player(); var receiver = Player();
        foreach (var rarity in new[] { "Unique", "Set" })
        {
            var definition = NamedGear.Catalog.Items.First(d => d.Rarity == rarity && NamedGear.Available(d.Id));
            var item = NamedGear.Create(definition.Id, 99, new Random(714)); p.Backpack.DropItem(item);
            string state = State(item); int cost = EquipmentInsurance.Cost(item);
            Require(Banker.Deposit(p, cost) && EquipmentInsurance.Apply(p, [EquipmentInsurance.Quote(item)], [true])
                && Banker.GetBalance(p) == 0 && item.Insured, "Named insurance failed to prepay exact premium");
            receiver.Backpack.DropItem(item);
            Require(!item.Insured && !item.PaidInsurance && State(item) == state,
                "Named transfer retained policy credit or changed item rolls");
            item.Delete();
        }
    }
    private static void LegacyMigration()
    {
        var p = Player(); var item = InsuranceChecks.Gear(p);
        var state = ClassGear.State(item);
        var w = new BufferWriter(new byte[65536], true);
        w.Write(5); w.Write(0); w.Write(1); w.Write(item.Serial.Value);
        w.Write(state.BaseCode); w.Write(state.Defense); w.Write(false); w.Write((byte)GearRarity.Normal);
        w.Write(state.Roll.ItemLevel); w.Write(state.Roll.AffixLevel); w.Write(state.Roll.RequiredLevel); w.Write(0);
        w.Write((byte)state.Tier); w.Write((byte)state.Quality); w.Write(0); w.Write(0); w.Write(true); w.Write(0);
        w.Write(false); w.Write(0); w.Write(0); w.Write(-1);
        RuneSockets.Write(w, []); GemSockets.Write(w, []);
        var old = new GearProfiles(); old.Unregister();
        old.Deserialize(new BufferReader(w.Buffer[..(int)w.Position]));
        Require(JsonSerializer.Serialize(old.Find(item.Serial)) == State(item), "Version5 migration changed an ordinary item");
        item.Delete();
    }
    private static void Acquisition()
    {
        var item = GamblingChecks.Generate(GamblingChecks.Offer(), new FirstRoll());
        Require(item != null && ClassGear.State(item) is { NamedItemId: not null, Identified: true, Roll.Rarity: GearRarity.Unique },
            "Gambling failed to create an eligible source-weighted named item");
        item.Delete();
    }
    private static void Upkeep()
    {
        var p = Player();
        foreach (var rarity in new[] { GearRarity.Set, GearRarity.Unique })
        {
            var definition = NamedGear.Catalog.Items.First(d => d.Rarity == rarity.ToString() && NamedGear.Available(d.Id)
                && NamedGear.Catalog.Base(d.BaseCode).SourceTable == "weapons");
            var item = (BaseWeapon)NamedGear.Create(definition.Id, 99, new Random(77)); p.Backpack.DropItem(item);
            var skill = EquipmentRepairs.Skill(item).Value; p.Skills[skill].Cap = 120; p.Skills[skill].Base = 120;
            ClassProfessions.Profiles.Confirm(p, skill); RepairChecks.Workshop(p);
            BaseTool repair = skill == SkillName.Fletching ? new FletcherTools() : new RepairHammer();
            BaseTool salvage = skill == SkillName.Fletching ? new FletcherTools() : new SmithHammer();
            p.Backpack.DropItem(repair); p.Backpack.DropItem(salvage);
            string original = State(item);
            item.MaxHitPoints = 40; item.HitPoints = 20;
            var quote = EquipmentRepairs.Quote(p, item, repair, random: new FirstRoll());
            int factor = rarity == GearRarity.Set ? 2 : 3;
            Require(quote != null && quote.Costs.Single(c => c.Type == typeof(MagicEssence)).Amount
                == EquipmentRepairs.Tier(ClassGear.State(item).Tier).Min * factor,
                "Named repair multiplier differs from approved price: " + definition.Id + " " + item.GetType().Name + " " + skill);
            foreach (var cost in quote.Costs)
            { var resource = cost.Type.CreateInstance<Item>(); resource.Amount = cost.Amount; p.Backpack.DropItem(resource); }
            Require(EquipmentRepairs.Commit(quote, out _, new FirstRoll()) && item.HitPoints == item.MaxHitPoints && State(item) == original,
                "Named repair failed or rerolled the item");
            var range = EquipmentEconomy.EssenceRange(rarity);
            Require(range.ChancePercent == (rarity == GearRarity.Set ? 90 : 95) && range.Min == (rarity == GearRarity.Set ? 2 : 5)
                && range.Max == (rarity == GearRarity.Set ? 5 : 12), "Named salvage rates differ from approved values");
            var recovery = EquipmentEconomy.Quote(p, item, EssenceAction.Salvage, salvage);
            Require(recovery != null && EquipmentEconomy.Commit(p, recovery, out var result, new FirstRoll())
                && item.Deleted && result.Essence == range.Min, "Named salvage did not use its approved yield");
            repair.Delete(); salvage.Delete();
        }
        var host = NamedGear.Catalog.Items.First(d => NamedGear.Available(d.Id) && d.Properties.Any(a => a.Code == "sock"));
        var socketed = NamedGear.Create(host.Id, 99, new Random(87)); p.Backpack.DropItem(socketed);
        string id = ClassGear.State(socketed).NamedItemId;
        var rune = RuneSocketChecks.Rune(p);
        Require(RuneSockets.Commit(RuneSockets.Quote(p, rune, socketed)) && ClassGear.State(socketed).NamedItemId == id
            && ClassGear.State(socketed).Runes.Length == 1 && ClassGear.State(socketed).NamedSockets > 0,
            "Named sockets failed permanent rune insertion or lost provenance");
        socketed.Delete();
    }
    private static void Statuses()
    {
        var p = Player(); p.Hits = p.HitsMax - 50;
        var attacker = new TimedEffectDummy(); attacker.MoveToWorld(p.Location, p.Map);
        Require(TimedEffects.CanRegenerateHits(p) && p.CanRegenHits, "Clean fixture cannot regenerate");
        var now = TimedEffects.Now;
        Require(GearEffects.ApplyPoison(attacker, p, 10, Core.TickCount, 1000)
            && !TimedEffects.CanRegenerateHits(p) && !p.CanRegenHits, "Active poison did not suppress natural regeneration");
        TimedEffects.Profiles.Capture(); var writer = new BufferWriter(new byte[1048576], true);
        TimedEffects.Profiles.Serialize(writer); TimedEffects.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
        Require(!p.CanRegenHits, "Poison regeneration suppression vanished on reload");
        TimedEffects.Settle(p, now.AddMilliseconds(1100));
        Require(TimedEffects.CanRegenerateHits(p) && p.CanRegenHits, "Poison expiry retained regeneration suppression");
        TimedEffects.Clear(p);
        now = TimedEffects.Now; int before = p.Hits;
        Require(TimedEffects.Bleed(attacker, p, 32, now) && !p.CanRegenHits, "Bleed failed to suppress regeneration");
        TimedEffects.Settle(p, now.AddSeconds(4));
        int remaining = TimedEffects.Snapshot().Single(r => r.Owner == p.Serial.Value).BleedMs;
        Require(p.Hits == before - 16 && remaining == 4000,
            $"Bleed failed exact partial damage/remaining time: before={before}, after={p.Hits}, remaining={remaining}");
        TimedEffects.Settle(p, now.AddSeconds(8));
        Require(p.Hits == before - 32 && p.CanRegenHits, "Bleed expired incorrectly or kept regeneration disabled");
        TimedEffects.PreventHealing(p, TimedEffects.Now);
        Require(!p.CanRegenHits, "Prevent Heal did not disable regeneration");
        TimedEffects.ClearGear(p);
        Require(p.CanRegenHits, "Clearing gear statuses retained the regeneration block");
        Regeneration(attacker, p);
        attacker.Delete();
    }
    private static void Regeneration(Mobile attacker, PlayerMobile owner)
    {
        var native = new Zombie(); native.MoveToWorld(owner.Location, owner.Map); native.AIObject.AITimer.Stop();
        var poisonAt = TimedEffects.Now;
        Require(native.CanRegenHits && TimedEffects.Poison(attacker, native, .001, 1000, poisonAt)
            && !native.CanRegenHits, "Native creature regeneration bypassed poison");
        TimedEffects.Settle(native, poisonAt.AddMilliseconds(1100));
        Require(native.CanRegenHits, "Native creature regeneration stayed disabled after poison expiry");
        TimedEffects.Clear(native); native.Delete();
        var zombie = new GraveyardZombie(); zombie.SetHits(32);
        var pet = new SkeletonGuardian();
        foreach (var target in new BaseCreature[] { zombie, pet })
        {
            target.MoveToWorld(owner.Location, owner.Map); target.AIObject.AITimer.Stop();
            target.Hits = target.HitsMax / 2;
            int before = target.Hits;
            void Recover(long tick)
            {
                if (target is GraveyardUndead undead) { undead.RecoverLife(tick); }
                else { ((SkeletonGuardian)target).RecoverLife(tick); }
            }
            var now = TimedEffects.Now;
            Require(TimedEffects.Poison(attacker, target, .001, 60000, now), "Creature poison fixture failed");
            Recover(1000);
            for (int i = 1; i <= 20; i++) { Recover(1000 + 1000 * i); }
            Require(target.Hits == before, "Poison allowed custom creature regeneration");
            TimedEffects.Settle(target, now.AddSeconds(61));
            Recover(21001);
            Require(target.Hits == before, "Poison expiry banked suppressed regeneration");
            for (int i = 1; i <= 20; i++) { Recover(21001 + 1000 * i); }
            Require(target.Hits > before, "Custom creature regeneration did not resume after poison");
            TimedEffects.PreventHealing(target, TimedEffects.Now);
            before = target.Hits;
            for (int i = 1; i <= 20; i++) { Recover(41001 + 1000 * i); }
            Require(target.Hits == before, "Prevent Heal allowed custom creature regeneration");
            TimedEffects.Clear(target); target.Delete();
        }
    }
    private static void SharedStatuses()
    {
        var p = Player(); p.RawStr = p.RawDex = 1000;
        var mace = NamedGear.Create("unique/21", 99, new FirstRoll()); p.Backpack.DropItem(mace);
        Require(p.EquipItem(mace), "Cannot equip source slow weapon"); ClassGear.Refresh(p);
        var target = new Zombie(); target.MoveToWorld(new Point3D(p.X + 2, p.Y, p.Z), p.Map); target.AIObject.AITimer.Stop();
        var at = TimedEffects.Now;
        NamedCombat.AfterStrike(p, target, false, new FirstRoll());
        Require(TimedEffects.Slow(target, TimedEffects.Now) == 50, "Source slow weapon did not apply its on-hit state");
        double movement = ClassGearHooks.MovementDelay(target, 1);
        Require(movement == 2 && ClassGearHooks.AttackDelay(target, TimeSpan.FromSeconds(1)) == TimeSpan.FromSeconds(2),
            "Native movement/attack consumers missed item slow");
        TimedEffects.Slow(target, 100, at); Require(TimedEffects.Slow(target, at) == 90, "Normal target slow cap failed");
        Require(GearEffects.MovementSlow(target) == 75 && GearEffects.AttackSlow(target) == 85, "Combined slow speed floors failed");
        TimedEffects.Slow(target, 20, at.AddSeconds(1)); Require(TimedEffects.Slow(target, at.AddSeconds(1)) == 20,
            "Repeated source slow incorrectly stacked");
        TimedEffects.ReduceDefense(target, 5, at); TimedEffects.ReduceDefense(target, 7, at);
        Require(ClassStats.Defense(target) == Math.Max(0, CreatureProfiles.Defense(target) - 12), "Cumulative target defence loss was not consumed");
        TimedEffects.Profiles.Capture(); var writer = new BufferWriter(new byte[1048576], true);
        TimedEffects.Profiles.Serialize(writer); TimedEffects.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
        Require(TimedEffects.Slow(target, at.AddSeconds(2)) == 20 && TimedEffects.DefenseLoss(target) == Math.Min(12, CreatureProfiles.Defense(target)),
            "Slow/target defence loss disappeared on reload");
        TimedEffects.Settle(target, at.AddSeconds(32));
        Require(TimedEffects.Slow(target, at.AddSeconds(32)) == 0 && TimedEffects.DefenseLoss(target) > 0,
            "Slow expiry retained slowing or discarded permanent defence loss");
        TimedEffects.ReduceDefense(target, int.MaxValue, at.AddSeconds(33));
        Require(ClassStats.Defense(target) == 0, "Target defence reduction fell below zero or overflowed");
        TimedEffects.Clear(target); target.Delete(); p.Backpack.DropItem(mace); mace.Delete();
        var sword = NamedGear.Create("unique/162", 99, new FirstRoll()); p.Backpack.DropItem(sword);
        Require(p.EquipItem(sword), "Cannot equip source defence-reduction weapon"); ClassGear.Refresh(p);
        target = new Zombie(); target.MoveToWorld(new Point3D(p.X + 2, p.Y, p.Z), p.Map); target.AIObject.AITimer.Stop();
        NamedCombat.AfterStrike(p, target, false, new FirstRoll());
        Require(TimedEffects.DefenseLoss(target) == Math.Min(CreatureProfiles.Defense(target), -ClassGear.Bonus(p, "dmg-ac")),
            "Source weapon failed to reduce target defence on hit");
        TimedEffects.Clear(target); target.Delete(); p.Backpack.DropItem(sword); sword.Delete();
        var raven = NamedGear.Create("unique/275", 99, new FirstRoll()); p.Backpack.DropItem(raven);
        Require(p.EquipItem(raven), "Cannot equip nofreeze fixture"); ClassGear.Refresh(p);
        at = TimedEffects.Now; TimedEffects.Slow(p, 75, at);
        Require(TimedEffects.Slow(p, at) == 50 && GearEffects.MovementSlow(p) == 50
            && !GearEffects.ApplyChill(p, Core.TickCount), "Cannot Be Frozen incorrectly removed non-cold slow");
        TimedEffects.Clear(p); p.Backpack.DropItem(raven); raven.Delete();
    }
    private static void Maintenance()
    {
        var item = (BaseWeapon)NamedGear.Create("unique/134", 99, new FirstRoll(), false);
        int maximum = item.MaxHitPoints; item.HitPoints = maximum - 4;
        var now = TimedEffects.Now; int period = NamedMaintenance.PeriodMs(NamedMaintenance.Rate(ClassGear.State(item)));
        var due = NamedMaintenance.Clocks[item.Serial];
        NamedMaintenance.Settle(item, due.AddMilliseconds(-1)); Require(item.HitPoints == maximum - 4, "Self-repair ran before its source interval");
        NamedMaintenance.Settle(item, due); Require(item.HitPoints == maximum - 3, "Self-repair missed unidentified stored item");
        var writer = new BufferWriter(new byte[1048576], true); NamedMaintenance.Profiles.Capture();
        NamedMaintenance.Profiles.Serialize(writer); NamedMaintenance.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
        Require(NamedMaintenance.Clocks[item.Serial] == due.AddMilliseconds(period), "Self-repair clock reset on reload");
        NamedMaintenance.Settle(item, due.AddMilliseconds(period * 2));
        Require(item.HitPoints == maximum - 1 && item.MaxHitPoints == maximum, "Self-repair catch-up changed its rate or maximum durability");
        var owner = Player(); owner.Backpack.DropItem(item);
        var nextOwner = Player(); nextOwner.Backpack.DropItem(item);
        Require(NamedMaintenance.Clocks[item.Serial] == due.AddMilliseconds(period * 3), "Storage/trade reset self-repair clock");
        NamedMaintenance.Settle(item, due.AddDays(10)); Require(item.HitPoints == maximum, "Self-repair exceeded maximum or lost long elapsed catch-up");
        item.HitPoints = 0; NamedMaintenance.Settle(item, due.AddDays(20));
        Require(item.HitPoints == 0, "Source self-repair incorrectly revived a broken item");
        item.HitPoints = maximum - 1;
        Require(NamedMaintenance.Clocks[item.Serial] < now.AddMinutes(3), "Repaired item did not resume its repair timer");
        item.Delete(); NamedMaintenance.Tick(TimedEffects.Now);
        Require(!NamedMaintenance.Clocks.ContainsKey(item.Serial), "Self-repair retained a deleted item");
    }
    private static void DamageToMana()
    {
        var p = Player(); p.RawStr = p.RawDex = 1000;
        var definition = NamedGear.Catalog.Items.First(d => NamedGear.Available(d.Id) && d.Properties.Any(v => v.Code == "dmg-to-mana"));
        var item = NamedGear.Create(definition.Id, 99, new FirstRoll()); p.Backpack.DropItem(item);
        Require(p.EquipItem(item), "Cannot equip damage-to-mana source fixture"); ClassGear.Refresh(p);
        int percent = ClassGear.Bonus(p, "dmg-to-mana");
        var attacker = new TimedEffectDummy { Body = 3 }; attacker.Hits = attacker.HitsMax;
        attacker.MoveToWorld(new Point3D(p.X + 2, p.Y, p.Z), p.Map);
        p.Hits = p.HitsMax; p.Mana = 0; int before = p.Hits;
        AOS.Damage(p, attacker, 40, false, 100, 0, 0, 0, 0, weaponDamage: true);
        Require(p.Mana == (before - p.Hits) * percent / 100, "Damage-to-mana missed final mitigated melee loss or ran twice");
        p.Hits = p.HitsMax; p.Mana = 0; before = p.Hits;
        AOS.Damage(p, attacker, 40, false, 100, 0, 0, 0, 0, archer: true, weaponDamage: true);
        Require(p.Mana == (before - p.Hits) * percent / 100, "Damage-to-mana missed ranged weapon loss");
        p.Hits = p.HitsMax; p.Mana = 0;
        var at = TimedEffects.Now; GearEffects.ApplyPoison(attacker, p, 20, Core.TickCount, 1000);
        TimedEffects.Settle(p, at.AddSeconds(2));
        Require(p.Mana == 0, "Poison damage incorrectly regenerated mana from weapon-only item event"); TimedEffects.Clear(p);
        p.Hits = p.HitsMax; p.Mana = 0; before = p.Hits;
        TimedEffects.ApplyBuff(p, TimedEffectCategory.Potion, "protection", 5);
        AOS.Damage(p, attacker, 40, false, 100, 0, 0, 0, 0, weaponDamage: true);
        Require(p.Mana == (before - p.Hits) * percent / 100, "Damage-to-mana ignored final Protection mitigation");
        p.Mana = p.ManaMax - 1; GemCombat.WeaponHit(attacker, p, 1000, true);
        Require(p.Mana == p.ManaMax, "Damage-to-mana exceeded maximum mana");
        TimedEffects.Clear(p); attacker.Delete(); p.Backpack.DropItem(item); item.Delete();
    }
    private static void Throwing()
    {
        var owner = Player(); owner.RawStr = owner.RawDex = 1000;
        var scalper = (BaseWeapon)NamedGear.Create("unique/288", 99, new FirstRoll()); owner.Backpack.DropItem(scalper);
        Require(owner.EquipItem(scalper), "Cannot equip replenishing throwing weapon"); ClassGear.Refresh(owner);
        int maximum = NamedAmmunition.Maximum(scalper), durability = scalper.HitPoints;
        Require(maximum == 200 && NamedAmmunition.Remaining(scalper) == maximum, "Throwing source stack capacity was lost");
        var target = new Zombie(); target.MoveToWorld(new Point3D(owner.X + 1, owner.Y, owner.Z), owner.Map);
        target.AIObject.AITimer.Stop(); target.Hits = target.HitsMax;
        Require(ClassCombat.Perform(owner, CombatAbility.BasicAttack, target, target.Location, Core.TickCount), "Could not begin real throwing attack");
        Require(NamedAmmunition.Remaining(scalper) == maximum, "Windup consumed throwing quantity before release");
        Require(ClassCombat.ResolveAction(owner, ClassCombat.For(owner).EffectAt) && NamedAmmunition.Remaining(scalper) == maximum - 1,
            "Real attack release failed to consume exactly one throwing quantity");
        Require(scalper.HitPoints == durability, "Throwing incorrectly consumed native durability"); target.Delete();
        var now = TimedEffects.Now; var due = NamedAmmunition.Quantities[scalper.Serial].DueUtc;
        NamedAmmunition.Settle(scalper, due.AddMilliseconds(-1)); Require(NamedAmmunition.Remaining(scalper) == maximum - 1, "Quantity replenished too early");
        NamedAmmunition.Settle(scalper, due); Require(NamedAmmunition.Remaining(scalper) == maximum, "Quantity did not replenish at its source rate");
        for (int i = 0; i < maximum; i++) { Require(NamedAmmunition.Consume(scalper, now), "Quantity exhausted too early"); }
        Require(!NamedAmmunition.Consume(scalper, now) && !ClassCombat.Available(owner, CombatAbility.BasicAttack)
            && !scalper.Deleted && owner.Items.Contains(scalper), "Empty throwing weapon was deleted or allowed another throw");
        var writer = new BufferWriter(new byte[1048576], true); NamedAmmunition.Profiles.Capture();
        NamedAmmunition.Profiles.Serialize(writer); NamedAmmunition.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
        Require(NamedAmmunition.Remaining(scalper) == 0 && NamedAmmunition.Quantities[scalper.Serial].DueUtc == due,
            "Empty weapon quantity/clock changed on persistence reload");
        owner.Backpack.DropItem(scalper); var other = Player(); other.Backpack.DropItem(scalper);
        Require(NamedAmmunition.Remaining(scalper) == 0 && NamedAmmunition.Quantities[scalper.Serial].DueUtc == due,
            "Storage/trade reset quantity or replenishment clock");
        NamedAmmunition.Settle(scalper, due);
        Require(NamedAmmunition.Remaining(scalper) == 1 && NamedAmmunition.CanAttack(scalper), "Empty replenishing weapon did not become usable again");
        var titan = NamedGear.Create("unique/281", 99, new FirstRoll()); other.Backpack.DropItem(titan);
        Require(NamedAmmunition.Maximum(titan) == 180 && NamedAmmunition.Remaining(titan) == 180, "Titan's Revenge lost increased stack size");
        var gimmer = NamedGear.Create("unique/335", 99, new FirstRoll()); other.Backpack.DropItem(gimmer);
        Require(NamedAmmunition.Maximum(gimmer) == 330, "Gimmershred lost increased stack size");
        for (int i = 0; i < 330; i++) { Require(NamedAmmunition.Consume(gimmer, now), "Non-replenishing stack consumed incorrectly"); }
        NamedAmmunition.Settle(gimmer, now.AddDays(30));
        Require(NamedAmmunition.Remaining(gimmer) == 0 && !gimmer.Deleted, "Non-replenishing weapon acquired quantity or disappeared");
        var knife = NamedGear.Create("unique/291", 99, new FirstRoll()); other.Backpack.DropItem(knife);
        Require(knife is NamedThrowingKnife && CombatTiming.Weapon((BaseWeapon)knife).Family == AttackFamily.Throw
            && NamedAmmunition.Maximum(knife) == 240, "Deathbit did not receive its throwing-knife appearance and quantity consumer");
        var records = new List<SavedThrow>();
        foreach (var item in new[] { scalper, titan, gimmer, knife })
        {
            var quantity = NamedAmmunition.Quantities[item.Serial];
            records.Add(new(item.Serial.Value, quantity.Remaining, NamedAmmunition.Maximum(item), quantity.DueUtc,
                NamedMaintenance.PeriodMs(NamedAmmunition.Rate(item)), State(item), GearDetails.Read(item).Durability.Value));
        }
        File.WriteAllText(Path.Join(Core.BaseDirectory, "named-throwing-checks.json"), JsonSerializer.Serialize(records));
    }
    private static void ReloadThrowing()
    {
        var records = JsonSerializer.Deserialize<SavedThrow[]>(File.ReadAllText(Path.Join(Core.BaseDirectory, "named-throwing-checks.json")));
        var now = TimedEffects.Now;
        foreach (var row in records)
        {
            var item = World.FindItem((Serial)row.Item);
            Require(item is { Deleted: false } && State(item) == row.State && GearDetails.Read(item).Durability == row.Durability,
                "Throwing weapon identity, rolls or native durability changed across real restart");
            long replenished = row.Period > 0 && now >= row.Due ? (long)(now - row.Due).TotalMilliseconds / row.Period + 1 : 0;
            int expected = row.Remaining + (int)Math.Min(row.Maximum - row.Remaining, replenished);
            NamedAmmunition.Settle(item, now);
            Require(NamedAmmunition.Maximum(item) == row.Maximum && NamedAmmunition.Remaining(item) == expected,
                "Throwing quantity or elapsed replenishment failed real restart: " + row.Item);
        }
    }
    private static void Control()
    {
        Require(NamedControl.FreezeFrames(50, 50, 1, false, 0) == 125
            && NamedControl.FreezeFrames(50, 50, 1, true, 0) == 37
            && NamedControl.FreezeFrames(1, 99, 1, false, 0) == 0, "Source freeze chance/length/ranged penalty changed");
        Require(NamedControl.BlindLevel(50, 50, 1, false, 0) == 11
            && NamedControl.BlindLevel(50, 50, 1, true, 0) == 4, "Source blind level/ranged penalty changed");
        var p = Player(); p.RawStr = p.RawDex = 1000;
        var ground = CombatPractice.FindFloor(Map.Felucca, 1387, 1495);
        p.MoveToWorld(new Point3D(ground.X - 3, ground.Y, ground.Z), Map.Felucca);
        var axe = NamedGear.Create("unique/5", 99, new FirstRoll()); p.Backpack.DropItem(axe);
        Require(p.EquipItem(axe), "Cannot equip source freeze weapon"); ClassGear.Refresh(p);
        var target = new Zombie(); target.MoveToWorld(CombatPractice.FindFloor(p.Map, p.X + 3, p.Y), p.Map); target.AIObject.AITimer.Stop();
        target.Direction = Direction.East; Require(target.Move(Direction.East), "Freeze fixture has no open baseline movement tile");
        NamedCombat.AfterStrike(p, target, false, new FirstRoll());
        int frozen = NamedControl.FrozenRemaining(target);
        Require(frozen > 0 && !target.Frozen && !target.Paralyzed && CreatureHitRecovery.Remaining(target) >= frozen - 5,
            "Freeze did not own its independent AI/movement gate");
        Require(!target.Move(Direction.East) && !CombatDisplacement.TryStep(target, Direction.East), "Native movement/knockback bypassed freeze");
        var writer = new BufferWriter(new byte[1048576], true); TimedEffects.Profiles.Capture(); TimedEffects.Profiles.Serialize(writer);
        TimedEffects.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
        Require(NamedControl.FrozenRemaining(target) > 0, "Freeze disappeared after persistence reload");
        target.Frozen = true; TimedEffects.Settle(target, TimedEffects.Now.AddMinutes(1));
        Require(NamedControl.FrozenRemaining(target) == 0 && target.Frozen, "Freeze expiry cleared another system's root");
        target.Frozen = false; TimedEffects.Clear(target);
        p.Backpack.DropItem(axe); axe.Delete();
        var blind = NamedGear.Create("unique/1", 99, new FirstRoll()); p.Backpack.DropItem(blind);
        Require(p.EquipItem(blind), "Cannot equip source blind weapon"); ClassGear.Refresh(p);
        target.MoveToWorld(new Point3D(p.X + 3, p.Y, p.Z), p.Map); target.Combatant = p;
        NamedCombat.AfterStrike(p, target, false, new FirstRoll());
        Require(NamedControl.Blind(target) && !target.CanBeHarmful(p, false), "Blind did not restrict native distant targeting");
        var before = target.Location; Require(target.AIObject.Think() && target.Location == before && target.Combatant == null,
            "Blinded native AI continued pursuing a distant target");
        target.MoveToWorld(new Point3D(p.X + 1, p.Y, p.Z), p.Map);
        Require(target.CanBeHarmful(p, false), "Blind incorrectly prevented adjacent melee targeting");
        var lich = new GraveyardLich(); lich.MoveToWorld(new Point3D(p.X + 1, p.Y + 1, p.Z), p.Map); lich.AIObject.AITimer.Stop();
        Require(NamedControl.ApplyBlind(p, lich, 1, false, new FirstRoll()) && !lich.BeginFirebolt(p),
            "Blinded custom caster started a queued firebolt");
        target.MoveToWorld(new Point3D(p.X + 3, p.Y, p.Z), p.Map);
        writer = new BufferWriter(new byte[1048576], true); TimedEffects.Profiles.Capture(); TimedEffects.Profiles.Serialize(writer);
        TimedEffects.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
        Require(NamedControl.Blind(target) && !target.CanBeHarmful(p, false),
            "Blind targeting gate disappeared on reload");
        TimedEffects.Settle(target, TimedEffects.Now.AddMinutes(1)); Require(!NamedControl.Blind(target), "Blind did not expire");
        TimedEffects.Clear(lich); lich.Delete(); p.Backpack.DropItem(blind); blind.Delete();
        var fear = NamedGear.Create("unique/49", 99, new FirstRoll()); p.Backpack.DropItem(fear);
        Require(p.EquipItem(fear), "Cannot equip source flee weapon"); ClassGear.Refresh(p);
        NamedCombat.AfterStrike(p, target, false, new FirstRoll());
        Require(NamedControl.Fear(target) && !target.CanBeHarmful(p, false), "Flee failed source trigger or attack suppression");
        double distance = target.GetDistanceToSqrt(p);
        target.AIObject.NextMove = Core.TickCount; Require(target.AIObject.Think(), "Flee failed to override native AI");
        Require(target.GetDistanceToSqrt(p) > distance, "Flee did not move away through native movement validation");
        Require(!NamedControl.Blind(target), "Flee retained the replaced vision curse");
        TimedEffects.Settle(target, TimedEffects.Now.AddSeconds(2)); Require(!NamedControl.Fear(target), "Flee did not expire");
        var pet = new Zombie { Controlled = true, ControlMaster = p };
        Require(!NamedControl.ApplyBlind(p, pet, 1, false, new FirstRoll()) && !NamedControl.ApplyFear(p, pet, 128, new FirstRoll()),
            "AI curses affected a controlled companion"); pet.Delete();
        TimedEffects.Clear(target); target.Delete(); p.Backpack.DropItem(fear); fear.Delete();
    }
    private static void StaminaDrain()
    {
        var p = Player(); p.RawStr = p.RawDex = 1000;
        var boots = NamedGear.Create("unique/109", 99, new FirstRoll()); p.Backpack.DropItem(boots);
        Require(p.EquipItem(boots), "Cannot equip source reduced-stamina-drain boots"); ClassGear.Refresh(p);
        Require(ClassGear.Bonus(p, "stamdrain") == 50, "Source stamina drain reduction was not projected");
        p.Stam = p.StamMax;
        var method = typeof(Server.Misc.StaminaSystem).GetMethod("ProcessPlayerMovement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var movement = new MovementEventArgs(p, Direction.East | Direction.Running);
        for (int i = 0; i < 2 * (p.StepsMax + 1); i++) { method.Invoke(null, [0, movement]); }
        Require(p.Stam == p.StamMax - 1, "Actual native running drain did not consume the source percentage");
        TimedEffects.Clear(p); Require(NamedControl.RunningCost(p, 1) == 0, "Fractional running drain was rounded up");
        var writer = new BufferWriter(new byte[1048576], true); TimedEffects.Profiles.Capture(); TimedEffects.Profiles.Serialize(writer);
        TimedEffects.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
        Require(NamedControl.RunningCost(p, 1) == 1, "Fractional running drain was lost on reload");
        p.Backpack.DropItem(boots); boots.Delete(); TimedEffects.Clear(p);
    }
    private static void RestInPeace()
    {
        var p = Player(); p.RawStr = p.RawDex = 1000;
        var armor = NamedGear.Create("unique/311", 99, new FirstRoll()); p.Backpack.DropItem(armor);
        Require(p.EquipItem(armor), "Cannot equip source Rest in Peace armor"); ClassGear.Refresh(p);
        var target = new GraveyardSkeleton { Frozen = true }; target.MoveToWorld(new Point3D(p.X + 1, p.Y, p.Z), p.Map);
        p.DoHarmful(target); target.Damage(target.HitsMax + 1, p); CombatDamage.Finish(target, p);
        var corpse = target.Corpse as Corpse;
        Require(corpse is { SanctuaryRestInPeace: true }, "Actual player kill did not mark the corpse Rest in Peace");
        var gold = new Gold(17); corpse.DropItem(gold);
        Require(!SkeletonPets.Usable(p, corpse), "Rest in Peace allowed the player to consume its corpse");
        var lich = new GraveyardLich(); lich.MoveToWorld(new Point3D(p.X, p.Y + 1, p.Z), p.Map); lich.AIObject.AITimer.Stop();
        Require(!lich.CanRaise(corpse), "Rest in Peace allowed a monster to revive its corpse");
        new Server.Spells.Necromancy.AnimateDeadSpell(p).Target(corpse);
        Require(!corpse.Animated && !corpse.Deleted && gold.Parent == corpse && gold.Amount == 17, "Rest in Peace altered ordinary corpse loot");
        File.WriteAllText(Path.Join(Core.BaseDirectory, "named-corpse-checks.json"), JsonSerializer.Serialize(new[] { corpse.Serial.Value, gold.Serial.Value }));
        lich.Delete(); p.Backpack.DropItem(armor); armor.Delete();
    }
    private static void ReloadCorpse()
    {
        var serials = JsonSerializer.Deserialize<uint[]>(File.ReadAllText(Path.Join(Core.BaseDirectory, "named-corpse-checks.json")));
        var corpse = World.FindItem((Serial)serials[0]) as Corpse; var gold = World.FindItem((Serial)serials[1]);
        Require(corpse is { SanctuaryRestInPeace: true, Deleted: false, Animated: false } && gold?.Parent == corpse && gold.Amount == 17,
            "Rest in Peace flag/ordinary loot changed across real restart");
    }
    private static void FireRanks()
    {
        var p = new PlayerMobile { Body = 0x190, Player = true, Name = "NamedFireRanks" };
        new Account("named-fire-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))[0] = p;
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Sorceress); ClassCreation.Profiles.AdvanceLevel(p, 99);
        FixtureSpace.Place(p, new Point3D(5550, 1151, 0), Map.Trammel); p.RawStr = p.RawDex = 1000;
        var gloves = Named("Magefist", p); Require(p.EquipItem(gloves), "Cannot equip fire-skill rank fixture"); ClassGear.Refresh(p);
        Require(NamedSkills.Rank(p, "sorceress.firebolt") == 0, "Fire skills bonus granted an unlearned skill");
        Require(ClassCreation.Profiles.TryTrain(p.Serial, "sorceress.firebolt", out _), "Cannot learn fire rank fixture");
        Require(NamedSkills.Rank(p, "sorceress.firebolt") == 2 && NamedSkills.Rank(p, "sorceress.firemastery", 1) == 2
            && NamedSkills.Rank(p, "sorceress.icebolt", 1) == 1 && NamedSkills.Rank(p, "sorceress.teleport", 1) == 1,
            "Fire skill bonus missed source elemental tags or affected a non-fire skill");
        Require(ClassCreation.Profiles.GetRank(p.Serial, "sorceress.firebolt") == 1, "Fire skill gear spent hard points");
        p.Backpack.DropItem(gloves); Require(NamedSkills.Rank(p, "sorceress.firebolt") == 1, "Fire rank bonus survived unequip"); gloves.Delete();
    }
    private static void Projectiles()
    {
        using var random = new CombatRandom();
        Require(NamedProjectiles.PierceBudget(0, new FirstRoll()) == 0
            && NamedProjectiles.PierceBudget(100, new FirstRoll()) == 4, "Projectile pierce budget lost source cap/chance");
        var p = Player(); p.RawStr = p.RawDex = 1000;
        var map = Map.Trammel; bool placed = false;
        for (int x = 5430; x < 5490 && !placed; x++)
        {
            for (int y = 1130; y < 1190 && !placed; y++)
            {
                int z = map.GetAverageZ(x, y); bool clear = true;
                for (int i = 0; i <= 9; i++) { if (!map.CanSpawnMobile(x + i, y, z)) { clear = false; break; } }
                if (!clear) { continue; }
                p.MoveToWorld(new Point3D(x, y, z), map);
                if (p.InLOS(new Point3D(x + 9, y, z + 14))) { placed = true; }
            }
        }
        Require(placed, "No clear projectile fixture strip");
        var crossbow = (BaseWeapon)NamedGear.Create("unique/198", 99, new FirstRoll()); p.Backpack.DropItem(crossbow);
        Require(p.EquipItem(crossbow), "Cannot equip source piercing crossbow"); ClassGear.Refresh(p);
        Require(NamedProjectiles.Budget(p, new FirstRoll()) == 4, "Buriza did not supply its 100% pierce budget");
        var targets = new Zombie[6];
        for (int i = 0; i < targets.Length; i++)
        {
            targets[i] = new Zombie(); targets[i].SetHits(65000); targets[i].MoveToWorld(new Point3D(p.X + 2 + i, p.Y, p.Z), map);
            targets[i].AIObject.AITimer.Stop();
        }
        var friendly = new Zombie { Controlled = true, ControlMaster = p }; friendly.SetHits(65000);
        friendly.MoveToWorld(new Point3D(p.X + 3, p.Y, p.Z), map); friendly.AIObject.AITimer.Stop();
        var offRay = new Zombie(); offRay.SetHits(65000); offRay.MoveToWorld(new Point3D(p.X + 4, p.Y + 2, p.Z), map); offRay.AIObject.AITimer.Stop();
        int durability = crossbow.HitPoints;
        Require(ClassCombat.Perform(p, CombatAbility.BasicAttack, targets[0], targets[0].Location, Core.TickCount)
            && ClassCombat.ResolveAction(p, ClassCombat.For(p).EffectAt), "Could not release the real piercing projectile");
        Require(targets.Take(5).All(t => t.Hits < 65000) && targets[5].Hits == 65000
            && friendly.Hits == 65000 && offRay.Hits == 65000,
            "Pierce lost its ray, friendly exclusion, range or five-target cap");
        Require(crossbow.HitPoints == durability - 1, "One piercing shot multiplied weapon wear");
        var first = targets[0]; int before = first.Hits;
        NamedProjectiles.Continue(p, first, first.Location, 4, new FirstRoll());
        Require(first.Hits == before, "Piercing projectile repeated its first target");
        var wall = new Static(0x80); wall.MoveToWorld(new Point3D(p.X + 4, p.Y, p.Z), map);
        int far = targets[3].Hits;
        NamedProjectiles.Continue(p, first, first.Location, 4, new FirstRoll());
        Require(targets[3].Hits == far, "Piercing projectile crossed native terrain/LOS obstruction"); wall.Delete();
        foreach (var target in targets) { TimedEffects.Clear(target); target.Delete(); }
        friendly.Delete(); offRay.Delete(); p.Backpack.DropItem(crossbow); crossbow.Delete();
    }
    private static void SetVisuals()
    {
        var p = new PlayerMobile { Body = 0x190, Player = true, Name = "NamedSetAura" };
        new Account("named-aura-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))[0] = p;
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Druid); ClassCreation.Profiles.AdvanceLevel(p, 99);
        FixtureSpace.Place(p, new Point3D(5550, 1151, 0), Map.Trammel); p.RawStr = p.RawDex = 1000;
        var items = new List<Item>();
        foreach (int number in new[] { 66, 67, 68, 69 })
        {
            var item = NamedGear.Create("piece/" + number, 99, new FirstRoll()); p.Backpack.DropItem(item);
            Require(p.EquipItem(item), "Cannot equip full-set aura fixture"); items.Add(item); ClassGear.Refresh(p);
            Require(NamedSetVisuals.Active(p) == (items.Count == 4), "Partial set activated its complete-set aura");
        }
        var weapon = items.OfType<BaseWeapon>().Single(); int durability = weapon.HitPoints;
        weapon.HitPoints = 0; Require(!NamedSetVisuals.Active(p), "Broken set piece retained golden aura"); weapon.HitPoints = durability;
        var state = ClassGear.State(weapon); ClassGear.Profiles.Store(weapon, state with { Identified = false });
        Require(!NamedSetVisuals.Active(p), "Unidentified piece activated golden aura"); ClassGear.Profiles.Store(weapon, state);
        p.Backpack.DropItem(weapon); Require(!NamedSetVisuals.Active(p), "Stored set piece retained golden aura");
        Require(p.EquipItem(weapon), "Cannot restore full-set aura fixture"); ClassGear.Refresh(p);
        Require(NamedSetVisuals.Active(p) && p.Body == 0x190 && p.Hue == 0 && !p.Hidden, "Golden aura altered body/hue/visibility ownership");
        File.WriteAllText(Path.Join(Core.BaseDirectory, "named-aura-checks.json"), p.Serial.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    private static void FadeVisual()
    {
        var p = new PlayerMobile { Body = 0x190, Player = true, Name = "NamedSetFade" };
        new Account("named-fade-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))[0] = p;
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Assassin); ClassCreation.Profiles.AdvanceLevel(p, 99);
        FixtureSpace.Place(p, new Point3D(5550, 1151, 0), Map.Trammel); p.RawStr = p.RawDex = 1000;
        Item last = null;
        for (int i = 62; i <= 65; i++)
        {
            var item = NamedGear.Create("piece/" + i, 99, new FirstRoll()); p.Backpack.DropItem(item);
            Require(p.EquipItem(item), "Cannot equip Natalya fade fixture"); ClassGear.Refresh(p); last = item;
            Require(NamedSetVisuals.Faded(p) == (i == 65), "Partial Natalya set activated fade");
        }
        var observer = new Zombie(); observer.MoveToWorld(new Point3D(p.X + 1, p.Y, p.Z), p.Map); observer.AIObject.AITimer.Stop();
        Require(NamedSetVisuals.Opacity(p) == 128 && !p.Hidden && p.Body == 0x190 && observer.CanSee(p) && observer.CanBeHarmful(p, false),
            "Fade changed visibility, targeting or body ownership");
        byte[] packet = new byte[NamedVisualProtocol.Length]; NamedSetVisuals.WriteOpacity(packet, p.Serial, NamedSetVisuals.Opacity(p));
        Require(packet[0] == 0xBF && System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(1, 2)) == 11
            && System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(3, 2)) == NamedVisualProtocol.SubCommand
            && packet[5] == NamedVisualProtocol.Version && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(6, 4)) == p.Serial.Value
            && packet[10] == 128, "Fade packet lost its version, bounds, entity or opacity");
        var original = ClassGear.State(last); ClassGear.Profiles.Store(last, original with { Identified = false });
        Require(!NamedSetVisuals.Faded(p), "Unidentified Natalya piece retained fade"); ClassGear.Profiles.Store(last, original);
        p.Backpack.DropItem(last); Require(NamedSetVisuals.Opacity(p) == 255 && !p.Hidden, "Unequip did not remove fade");
        Require(p.EquipItem(last), "Cannot restore Natalya set"); ClassGear.Refresh(p);
        File.WriteAllText(Path.Join(Core.BaseDirectory, "named-fade-checks.json"), p.Serial.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        observer.Delete();
    }
    private static void ReloadFade()
    {
        uint serial = uint.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "named-fade-checks.json")), System.Globalization.CultureInfo.InvariantCulture);
        var p = World.FindMobile((Serial)serial) as PlayerMobile;
        Require(p != null && NamedSetVisuals.Opacity(p) == 128 && !p.Hidden && p.Body == 0x190,
            "Natalya fade did not derive correctly across real restart");
    }
    private static void BloodVisual()
    {
        var p = Player(); p.RawStr = p.RawDex = 1000;
        var shield = NamedGear.Create("unique/98", 99, new FirstRoll()); p.Backpack.DropItem(shield);
        var weapon = new Dagger(); p.Backpack.DropItem(weapon);
        Require(p.EquipItem(shield) && p.EquipItem(weapon), "Cannot equip cosmetic blood fixture"); ClassGear.Refresh(p);
        var target = new Zombie(); target.SetHits(10000); target.MoveToWorld(new Point3D(p.X + 2, p.Y, p.Z), p.Map); target.AIObject.AITimer.Stop();
        int before = 0; foreach (var blood in target.Map.GetItemsInRange<Blood>(target.Location, 0)) { before++; }
        GearEffects.Strike(p, target, 0, new FirstRoll());
        var decals = new List<Blood>(); foreach (var blood in target.Map.GetItemsInRange<Blood>(target.Location, 0)) { decals.Add(blood); }
        Require(decals.Count == before + 1, "Source extra-blood property did not create its native cosmetic decal");
        foreach (var blood in decals) { blood.Delete(); }
        TimedEffects.Clear(target); target.Delete(); p.Backpack.DropItem(shield); shield.Delete(); p.Backpack.DropItem(weapon); weapon.Delete();
    }
    private static void ReloadSetVisuals()
    {
        uint serial = uint.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "named-aura-checks.json")), System.Globalization.CultureInfo.InvariantCulture);
        var player = World.FindMobile((Serial)serial) as PlayerMobile;
        Require(player != null && NamedSetVisuals.Active(player) && player.Body == 0x190 && !player.Hidden,
            "Complete-set aura eligibility or appearance ownership failed real restart");
    }
    private static void Combat()
    {
        var classes = Enum.GetValues<DiabloClass>();
        foreach (var c in classes)
        {
            int expected = c switch
            { DiabloClass.Amazon or DiabloClass.Paladin or DiabloClass.Assassin => 5,
                DiabloClass.Barbarian => 7, DiabloClass.Sorceress => 9, _ => 11 };
            Require(HitRecovery.BlockFrames(c, 0) == expected && HitRecovery.BlockFrames(c, 100) < expected,
                "Block timing diverged from source human animation metadata: " + c);
        }
        var sword = new Longsword(); var dagger = new Dagger();
        Require(HitRecovery.BlockFrames(DiabloClass.Amazon, 0, sword) == 17
            && HitRecovery.BlockFrames(DiabloClass.Amazon, 0, dagger) == 5
            && HitRecovery.BlockFrames(DiabloClass.Paladin, 12) == 5
            && HitRecovery.BlockFrames(DiabloClass.Paladin, 13) == 4,
            "Weapon-specific blocking or its first breakpoint is wrong");
        sword.Delete(); dagger.Delete();
        var p = Player(); p.RawStr = p.RawDex = 1000;
        long now = Core.TickCount; var session = ClassCombat.For(p);
        session.PendingAbility = CombatAbility.Teeth; session.NextAction = now + 1000;
        long previousRecovery=session.RecoveryUntil;
        HitRecovery.Block(p,now);
        Require(session.PendingAbility==CombatAbility.Teeth && session.RecoveryUntil==previousRecovery && session.NextAction==now+1000,
            "Released2.4 block interrupted an active spell or changed its deadlines");
        session.PendingAbility=CombatAbility.None;
        HitRecovery.Block(p,now);
        Require(session.RecoveryUntil==previousRecovery && session.NextAction==now+1000,
            "Released2.4 block interrupted a post-event attack/skill animation");
        session.NextAction=now;
        HitRecovery.Block(p, now); long deadline = session.RecoveryUntil;
        HitRecovery.Block(p, now + 10);
        Require(deadline == now + 440 && session.RecoveryUntil == deadline
            && session.PendingAbility == CombatAbility.None && session.NextAction == now,
            "Idle blocking refreshed recovery or changed the action deadline");
        var definition = NamedGear.Catalog.Items.First(d => NamedGear.Available(d.Id)
            && d.Properties.Any(e => e.SourceEnabled && e.Code == "knock"));
        var weapon = NamedGear.Create(definition.Id, 99, new FirstRoll());
        p.Backpack.DropItem(weapon); Require(p.EquipItem(weapon), "Knockback gear could not be equipped");
        var point = CombatPractice.FindFloor(Map.Felucca, 1387, 1495);
        p.MoveToWorld(new Point3D(point.X - 1, point.Y, point.Z), Map.Felucca);
        var target = new Zombie(); target.MoveToWorld(point, p.Map); target.AIObject.AITimer.Stop(); target.Direction = Direction.North;
        var state = CreatureProfiles.Session(target); state.RecoveryUntil = Core.TickCount + 1000;
        long recovery = state.RecoveryUntil;
        Require(!target.Move(Direction.East), "Knockback fixture lacked voluntary recovery lock");
        NamedCombat.AfterStrike(p, target, true, new FirstRoll());
        Require(target.X == point.X + 1 && target.Y == point.Y && target.Direction == Direction.North
            && state.RecoveryUntil == recovery && CreatureHitRecovery.Remaining(target) > 0,
            "Knockback failed to displace during recovery or changed facing/deadline");
        var wall = new Static(0x80); wall.MoveToWorld(new Point3D(target.X + 1, target.Y, target.Z), target.Map);
        var stopped = target.Location;
        NamedCombat.AfterStrike(p, target, true, new FirstRoll());
        Require(target.Location == stopped && target.Direction == Direction.North && state.RecoveryUntil == recovery,
            "Knockback bypassed a wall or mutated recovery/facing on failure");
        wall.Delete(); TimedEffects.Clear(target); target.Delete(); weapon.Delete();
    }
    private static void Elements()
    {
        var p = Player(); p.RawStr = p.RawDex = 1000;
        var attacker = new TimedEffectDummy(); attacker.MoveToWorld(p.Location, p.Map);
        var raven = NamedGear.Create("unique/275", 99, new FirstRoll()); p.Backpack.DropItem(raven);
        Require(p.EquipItem(raven), "Raven Frost could not equip");
        p.Hits = p.HitsMax - 100; int before = p.Hits;
        AOS.Damage(p, attacker, 100, 0, 0, 100, 0, 0);
        Require(p.Hits == before - 60, "Cold absorb did not reduce and heal its share before damage");
        p.Hits = p.HitsMax;
        AOS.Damage(p, attacker, 100, 0, 0, 100, 0, 0);
        Require(p.Hits == p.HitsMax - 80, "Full-health absorb healed after damage instead of before it");
        p.Backpack.DropItem(raven); raven.Delete();
        var dwarf = NamedGear.Create("unique/274", 99, new FirstRoll()); p.Backpack.DropItem(dwarf);
        Require(p.EquipItem(dwarf), "Dwarf Star could not equip");
        int reduced = 100 - GemCombat.Reduction(p);
        p.Hits = p.HitsMax - 100;SkillHealth.Clear(p);int before256=p.Hits*256;
        AOS.Damage(p, attacker, 100, 0, 100, 0, 0, 0);
        int absorbed256=reduced*256*15/100;
        Require(before256-(p.Hits*256-SkillHealth.Remainder256(p))==reduced*256-2*absorbed256,
            "Fire absorb ran before flat magic reduction or twice through native mitigation");
        p.Backpack.DropItem(dwarf); dwarf.Delete();
        var helmDefinition = NamedGear.Catalog.Items.First(d => NamedGear.Available(d.Id)
            && NamedGear.Catalog.Base(d.BaseCode).ItemType == "circ"
            && d.Properties.Any(e => e.Code == "pierce-ltng") && d.Properties.Any(e => e.Code == "extra-ltng"));
        var helm = NamedGear.Create(helmDefinition.Id, 99, new FirstRoll()); p.Backpack.DropItem(helm);
        Require(p.EquipItem(helm), $"Pierce/elemental-damage named helm could not equip: {helm.GetType().Name}, allowed={ClassGear.CanEquip(helm, p)}, str={p.Str}, dex={p.Dex}, level={ClassStats.Level(p)}, req={ClassGear.RequiredLevel(helm)}");
        var target = new Zombie(); target.SetHits(10000); target.MoveToWorld(p.Location, p.Map); target.AIObject.AITimer.Stop();
        double expected = (100 + ClassGear.Bonus(p, "extra-ltng")) * (50 + ClassGear.Bonus(p, "pierce-ltng")) / 100.0;
        Require(Math.Abs(NamedElements.Damage(p, target, 100, "ltng", 50) - expected) < 1e-9
            && NamedElements.Damage(p, target, 100, "ltng", 100) == 0
            && NamedElements.Resistance(p, target, "ltng", -99) == -100,
            "Elemental enhancement/pierce order, immunity guard or negative resistance cap failed");
        var magicDefinition = NamedGear.Catalog.Items.First(d => NamedGear.Available(d.Id)
            && NamedGear.Catalog.Base(d.BaseCode).RequiredClass is null or "nec"
            && d.Properties.Any(e => e.Code == "dmg-mag"));
        var weapon = NamedGear.Create(magicDefinition.Id, 99, new FirstRoll()); p.Backpack.DropItem(weapon);
        Require(p.EquipItem(weapon), "Magic-damage named weapon could not equip: " + magicDefinition.Id);
        Require(GearEffects.Damage(p, 0, target).Magic > 0, "Named magic damage was lost from the weapon packet");
        target.Delete(); attacker.Delete(); weapon.Delete(); helm.Delete();
    }
    private static void MagicPierce()
    {
        var p = Player(); p.RawStr = p.RawDex = 1000;
        var item = NamedGear.Create("unique/418", 99, new FirstRoll()); p.Backpack.DropItem(item);
        Require(p.EquipItem(item), "Cannot equip source magic-pierce belt"); ClassGear.Refresh(p);
        var target = new TimedEffectDummy(); target.MoveToWorld(new Point3D(p.X + 2, p.Y, p.Z), p.Map);
        int pierce = ClassGear.Bonus(p, "pierce-mag");
        Require(pierce == 3 && NamedElements.Attack(p)
            && Math.Abs(CombatPotions.TeethDamage(p, target, 100, 50) - 53) < 1e-9,
            "Source magic pierce did not reach actual Teeth damage");
        var monster = new Zombie();
        Require(NamedElements.Damage(p, monster, 100, "mag", 100) == 0
            && NamedElements.Damage(p, monster, 100, "mag", -100) == 200,
            "Magic pierce broke immunity or exceeded the resistance floor");
        monster.Delete(); target.Delete(); p.Backpack.DropItem(item); item.Delete();
    }
    private static void Reload()
    {
        NamedLifecycleChecks.Reload(); ReloadThrowing(); ReloadCorpse(); ReloadSetVisuals(); ReloadFade();
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(World.FindMobile((Serial)saved.Owner) is PlayerMobile, "Named owner vanished");
        foreach (var row in saved.Items)
        {
            var item = World.FindItem((Serial)row.Serial);
            Require(item != null && !item.Deleted && State(item) == row.State && item.Name == row.Name,
                "Named state changed after real save/reload: " + row.Serial);
            ClassGear.Profiles.Store(item, ClassGear.State(item));
        }
    }
}
