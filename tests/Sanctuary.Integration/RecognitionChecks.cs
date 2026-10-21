using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Accounting;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RecognitionChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RecognitionChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "recognition-checks.json");
    private sealed record Saved(uint Player, CharacterProfile Profile, RecognitionState Recognition, uint[] Awards, bool Pending, bool Respecced = false);
    private sealed record Office(string Id, uint Registrar, uint Wardrobe);
    private sealed record Record(Saved[] Players, Office[] Offices);
    public static void Configure()
    {
        Register("recognition-check", Check);
        Register("recognition-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable recognition and active-class respec checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool value, string message)
    { if (!value) { throw new InvalidOperationException(message); } }
    private static CharacterProfile Profile(PlayerMobile player)
    { ClassCreation.Profiles.TryGet(player.Serial, out var profile); return profile; }
    private static PlayerMobile Player(DiabloClass characterClass = DiabloClass.Necromancer)
    {
        var player = new PlayerMobile { Player = true, Body = 0x190, Name = "RecognitionCheck" };
        new Account("recognition-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))[0] = player;
        ClassCreation.Profiles.Assign(player, characterClass);
        player.AddItem(new Backpack());
        return player;
    }
    public static string Place(PlayerMobile player, int index = 0)
    {
        var hall = TownRecognition.Halls[index];
        var wardrobe = TownRecognition.PublicWardrobe(hall.Id);
        FixtureSpace.Place(player,wardrobe.Location, wardrobe.Map);
        Require(TownRecognition.AtOffice(player, hall.Id) && ClassRespec.SourceValid(player, wardrobe),
            $"Office is inaccessible at {hall.Id}/{wardrobe.Location}");
        return hall.Id;
    }
    private static Item[] Awards(PlayerMobile p) => p.Backpack.Items.Where(i => i is RecognitionCertificate or EarnedWardrobe).ToArray();
    private static void Delete(PlayerMobile p)
    { p.Delete(); ClassCreation.Profiles.PruneDeleted(); TownRecognition.Profiles.Prune(); }
    private static void Both(PlayerMobile p, string hall)
    {
        ClassCreation.Profiles.AdvanceLevel(p, 80);
        p.Skills[SkillName.Cartography].Base = 100;
        Require(TownRecognition.Claim(p, hall, RecognitionHonor.Hero), "Hero ceremony failed");
        Require(!ClassRespec.Ready(p, TownRecognition.PublicWardrobe(hall)), "First ceremony unlocked respec");
        Require(TownRecognition.Claim(p, hall, RecognitionHonor.Pillar), "Pillar ceremony failed");
    }
    private static void Check()
    {
        Require(ClassProfessions.Enabled && TownRecognition.Halls.Length == 22, "Expected 22 enabled civic offices");
        var visit = Player();
        for (int i = 0; i < TownRecognition.Halls.Length; i++) { Place(visit, i); }
        Delete(visit);
        var before = TownRecognition.Halls.Select(h => new Office(h.Id, TownRecognition.Registrar(h.Id).Serial.Value,
            TownRecognition.PublicWardrobe(h.Id).Serial.Value)).ToArray();
        TownRecognition.Initialize();
        Require(before.All(o => TownRecognition.Registrar(o.Id).Serial.Value == o.Registrar
            && TownRecognition.PublicWardrobe(o.Id).Serial.Value == o.Wardrobe), "Repeated installation duplicated civic services");
        foreach (byte craft in TradeSkills.Crafting)
        {
            var p = Player();
            try
            {
                ClassCreation.Profiles.AdvanceLevel(p, 79);
                p.Skills[(SkillName)craft].Base = 99.9;
                foreach (byte gather in TradeSkills.Gathering) { p.Skills[(SkillName)gather].Base = 100; }
                p.AddSkillMod(new DefaultSkillMod((SkillName)craft, "recognition-threshold-check", true, 20));
                TownRecognition.Refresh(p);
                Require(TownRecognition.Profiles.Find(p).Eligible == 0 && !TownRecognition.HasAccess(p),
                    "Level79, gathering100 or bonus skill qualified");
                p.Skills[(SkillName)craft].Base = 100;
                Require(TownRecognition.Profiles.Find(p).Eligible == RecognitionHonor.Pillar, $"Craft {craft} did not qualify at 100 base");
                string hall = Place(p);
                Require(TownRecognition.Claim(p, hall, RecognitionHonor.Pillar), "Craft ceremony failed");
                Require(TownRecognition.HasAccess(p) && TownRecognition.HasHousingRights(p) && Awards(p).Length == 2,
                    "Craft honor did not grant permanent rights and first awards");
                p.Skills[(SkillName)craft].Base = 0;
                Require(TownRecognition.HasAccess(p), "Skill loss erased earned access");
                ClassCreation.Profiles.AdvanceLevel(p, 80);
                hall = Place(p, TownRecognition.Halls.Length - 1);
                Require(TownRecognition.Claim(p, hall, RecognitionHonor.Hero) && Awards(p).Length == 3,
                    "Second ceremony in another town/facet failed or duplicated furniture");
                Require(!TownRecognition.Claim(p, hall, RecognitionHonor.Hero) && Awards(p).Length == 3, "Repeated ceremony duplicated awards");
                foreach (var award in Awards(p)) { award.Delete(); }
                TownRecognition.Collect(p, hall);
                Require(Awards(p).Length == 0 && TownRecognition.HasAccess(p), "Lost awards were reissued or erased rights");
            }
            finally { Delete(p); }
        }
        var first = Player(); var other = Player();
        try
        {
            string site = Place(first);
            ClassCreation.Profiles.AdvanceLevel(first, 80);
            Require(!TownRecognition.Claim(first, "unknown", RecognitionHonor.Hero), "Unregistered office accepted ceremony");
            Require(TownRecognition.Claim(first, site, RecognitionHonor.Hero) && !TownRecognition.HasAccess(other), "Rights leaked to another character");
            first.Skills[SkillName.Cartography].Base = 100;
            first.MoveToWorld(new Point3D(first.X + 40, first.Y, first.Z), first.Map);
            Require(!TownRecognition.Claim(first, site, RecognitionHonor.Pillar), "Remote ceremony accepted");
            Place(first); first.Kill();
            Require(!TownRecognition.Claim(first, site, RecognitionHonor.Pillar), "Ghost claimed ceremony");
            first.Corpse?.Delete();
        }
        finally { Delete(first); Delete(other); }
        Respec();
        foreach (DiabloClass characterClass in Enum.GetValues<DiabloClass>())
        {
            var p = Player(characterClass);
            try
            {
                string site = Place(p); Both(p, site);
                p.Backpack.DropItem(new Gold(5000));
                var profiles = ClassCreation.Profiles;
                profiles.TrySpendStat(p, CharacterStat.Strength);
                profiles.TrySpendStat(p, CharacterStat.Energy);
                var active = Profile(p);
                Require(ClassRespec.Commit(p, TownRecognition.PublicWardrobe(site), active, profiles.Stats(p.Serial), profiles.AvailablePoints(p.Serial))
                    && Profile(p) == active && profiles.Stats(p.Serial) == default && p.RawStr == ClassStats.For(characterClass).Strength
                    && p.RawInt == ClassStats.For(characterClass).Energy && profiles.AvailableStatPoints(p.Serial) == 395,
                    $"Active-class respec failed for {characterClass}");
            }
            finally { Delete(p); }
        }
        var saved = new System.Collections.Generic.List<Saved>();
        foreach (int mode in new[] { 0, 1, 2 })
        {
            var p = Player(); string site = Place(p);
            p.Backpack.DropItem(new Bandage());
            if (mode > 0) { p.Backpack.MaxItems = mode; }
            Both(p, site);
            bool pending = TownRecognition.Pending(p);
            Require(pending == (mode > 0) && TownRecognition.HasAccess(p), "Full/partial pack lost rights or pending claim");
            var awards = Awards(p).Select(i => i.Serial.Value).ToArray();
            saved.Add(new(p.Serial.Value, Profile(p), TownRecognition.Profiles.Find(p), awards, pending));
        }
        var reset = Player(); string resetSite = Place(reset); Both(reset, resetSite);
        reset.Backpack.DropItem(new Gold(6000));
        ClassCreation.Profiles.TrySpendStat(reset, CharacterStat.Vitality);
        ClassCreation.Profiles.TryTrain(reset.Serial, "necromancer.raiseskeleton", out _);
        Require(ClassRespec.Commit(reset, TownRecognition.PublicWardrobe(resetSite), Profile(reset),
            ClassCreation.Profiles.Stats(reset.Serial), ClassCreation.Profiles.AvailablePoints(reset.Serial)), "Saved respec setup failed");
        saved.Add(new(reset.Serial.Value, Profile(reset), TownRecognition.Profiles.Find(reset),
            Awards(reset).Select(i => i.Serial.Value).ToArray(), false, true));
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Record(saved.ToArray(), before)));
    }
    private static void Respec()
    {
        var p = Player();
        var borrowed = Player();
        try
        {
            string site = Place(p); Both(p, site);
            var wardrobe = TownRecognition.PublicWardrobe(site);
            Require(ClassRespec.Ready(p, wardrobe), "Both ceremonies did not unlock respec");
            var profiles = ClassCreation.Profiles;
            for (int i = 0; i < 50; i++) { Require(profiles.TrySpendStat(p, CharacterStat.Strength), "Stat setup failed"); }
            Require(profiles.TrySpendStat(p, CharacterStat.Vitality) && profiles.TryTrain(p.Serial, "necromancer.raiseskeleton", out _), "Point setup failed");
            var weapon = new Longsword();
            for (int i = 0; i < 20; i++) { profiles.TrySpendStat(p, CharacterStat.Dexterity); }
            Require(p.EquipItem(weapon), "Could not equip stat-dependent respec weapon");
            var pet = new SkeletonGuardian(); pet.Initialize(p, 1, 0); pet.SetControlMaster(p); pet.MoveToWorld(p.Location, p.Map);
            p.Backpack.DropItem(new Gold(6000)); p.BankBox.DropItem(new Gold(10000));
            var profile = Profile(p); var stats = profiles.Stats(p.Serial); int points = profiles.AvailablePoints(p.Serial);
            p.Hits = 20; p.Mana = 10; p.Stam = 20;
            p.Backpack.MaxItems = p.Backpack.TotalItems;
            var trade = TradeSkills.Crafting.ToArray().Select(s => p.Skills[(SkillName)s].BaseFixedPoint).ToArray();
            ClassCombat.For(p).PendingAbility = CombatAbility.Teeth;
            Require(!ClassRespec.Commit(p, wardrobe, profile, stats, points) && p.Backpack.GetAmount(typeof(Gold)) == 6000, "Pending action respec charged");
            ClassCombat.Stop(p);
            var foreign = new EarnedWardrobe(borrowed);
            foreign.MoveToWorld(p.Location, p.Map);
            Require(!ClassRespec.Commit(p, foreign, profile, stats, points), "Foreign wardrobe allowed respec"); foreign.Delete();
            long token = TownRecognition.BeginSession(p);
            Require(ClassRespec.Commit(p, wardrobe, profile, stats, points), "Active class respec failed");
            Require(!TownRecognition.SessionValid(p, token), "Respec left its wardrobe session usable for a subsequent switch");
            Require(Profile(p) == profile && profiles.Stats(p.Serial) == default && profiles.AvailableStatPoints(p.Serial) == 395
                && profiles.AvailablePoints(p.Serial) == 79 && profiles.GetRank(p.Serial, "necromancer.raiseskeleton") == 0,
                "Respec changed class/level/XP or refunded the wrong points");
            Require(p.Backpack.GetAmount(typeof(Gold)) == 1000 && p.BankBox.GetAmount(typeof(Gold)) == 10000
                && p.Hits == 20 && p.Mana == 10 && p.Stam == 20 && pet.Deleted && !weapon.Deleted && weapon.Parent == p.Backpack,
                "Respec charged incorrectly, refilled pools, lost gear or retained summons");
            Require(trade.SequenceEqual(TradeSkills.Crafting.ToArray().Select(s => p.Skills[(SkillName)s].BaseFixedPoint)), "Respec reset crafting skills");
            Require(!ClassRespec.Commit(p, wardrobe, profile, stats, points) && p.Backpack.GetAmount(typeof(Gold)) == 1000, "Replayed respec charged twice");
            Require(profiles.TrySpendStat(p, CharacterStat.Strength), "Refunded point could not be spent");
            stats = profiles.Stats(p.Serial); points = profiles.AvailablePoints(p.Serial);
            Require(ClassRespec.Commit(p, wardrobe, profile, stats, points) && p.BankBox.GetAmount(typeof(Gold)) == 5000
                && p.Backpack.GetAmount(typeof(Gold)) == 1000, "Bank-funded reset did not charge exactly once");
            profiles.TrySpendStat(p, CharacterStat.Strength);
            var bankGold = new System.Collections.Generic.List<Item>();
            foreach (var item in p.BankBox.FindItemsByType(typeof(Gold), true)) { bankGold.Add(item); }
            foreach (var item in bankGold) { item.Delete(); }
            stats = profiles.Stats(p.Serial);
            Require(!ClassRespec.Commit(p, wardrobe, profile, stats, points) && profiles.Stats(p.Serial) == stats, "Insufficient gold reset points");
            p.Backpack.DropItem(new Gold(5000));
            ClassCreation.Profiles.AdvanceLevel(p, 81);
            Require(!ClassRespec.Commit(p, wardrobe, profile, stats, points), "Stale active-class quote accepted");
        }
        finally { Delete(p); Delete(borrowed); }
    }
    private static void Reload()
    {
        var record = JsonSerializer.Deserialize<Record>(File.ReadAllText(Manifest));
        foreach (var office in record.Offices)
        {
            Require(TownRecognition.Registrar(office.Id)?.Serial.Value == office.Registrar
                && TownRecognition.PublicWardrobe(office.Id)?.Serial.Value == office.Wardrobe, "Reload duplicated/replaced office services");
        }
        foreach (var saved in record.Players)
        {
            var p = (PlayerMobile)World.FindMobile((Serial)saved.Player);
            Require(Profile(p) == saved.Profile && TownRecognition.HasAccess(p)
                && TownRecognition.Profiles.Find(p).Earned == RecognitionHonor.Both, "Reload lost rights/progression");
            string site = Place(p);
            p.Backpack.MaxItems = 125;
            long token = TownRecognition.BeginSession(p);
            EventSink.InvokeDisconnected(p); EventSink.InvokeConnected(p);
            Require(!TownRecognition.SessionValid(p, token), "Reconnect retained a stale wardrobe session");
            TownRecognition.Collect(p, site); TownRecognition.Collect(p, site);
            Require(Awards(p).Length == 3 && !TownRecognition.Pending(p) && Profile(p) == saved.Profile
                && Awards(p).OfType<RecognitionCertificate>().Count() == 2 && Awards(p).OfType<EarnedWardrobe>().Count() == 1,
                "Pending/replayed reload collection lost or duplicated awards");
            Require(saved.Awards.All(serial => Awards(p).Any(i => i.Serial.Value == serial)), "Previously delivered award was replaced");
            Require(Awards(p).OfType<RecognitionCertificate>().All(i => i.Recipient == p.Serial.Value && i.RecipientName == p.Name && i.ItemID == 0xEA0)
                && Awards(p).OfType<EarnedWardrobe>().All(i => i.Recipient == p.Serial.Value && i.ItemID == 0xA97), "Award metadata/appearance did not persist");
            if (saved.Respecced)
            {
                Require(ClassCreation.Profiles.Stats(p.Serial) == default && ClassCreation.Profiles.AvailableStatPoints(p.Serial) == 395
                    && ClassCreation.Profiles.AvailablePoints(p.Serial) == 79 && p.Backpack.GetAmount(typeof(Gold)) == 1000
                    && p.Skills[SkillName.Cartography].BaseFixedPoint == 1000, "Reload lost paid reset/refunded points or recharged gold");
            }
        }
        TownRecognition.Initialize();
    }
}
