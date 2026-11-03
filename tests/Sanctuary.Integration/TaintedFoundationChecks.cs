// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class TaintedFoundationChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(TaintedFoundationChecks));
    private sealed record Saved(uint Owner, uint Helm, bool Female, int Mana, TaintedSnapshot Snapshot, TaintedFireBallPacket Packet);
    private static string Manifest => Path.Join(Core.BaseDirectory, "tainted-foundation-fixtures.json");
    private static bool _creating;
    private static void FreezeMana()
    {
        var previous = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = p => _creating || p.Name == "TaintedFoundationFixture"
            ? TimeSpan.FromHours(1) : previous?.Invoke(p) ?? Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)] public static void Initialize() => FreezeMana();
    public static void Configure()
    {
        FreezeMana(); Register("tainted-foundation-check", Check); Register("tainted-foundation-reload-check", Reload);
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Tainted captured suppliers and own NPC spell packets", _ => Core.LoopContext.Post(() =>
    { try { action(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    private static SkillValues Values(int rank, int mastery = 0, int oath = 0, int hardMastery = 0, int boil = 0) =>
        new(SkillMechanics.For(CombatAbility.SummonTainted), key => key == "Summon Tainted" ? rank : key == "Demonic Mastery" ? mastery : key == "Blood Oath" ? oath : 0,
            key => key == "Demonic Mastery" ? hardMastery : key == "Blood Boil" ? boil : 0, 47);
    private static int Curve(int rank, int minimum, int maximum) => rank <= 0 ? 0 : minimum + (maximum - minimum) * Math.Min(100, 110 * rank / (rank + 6)) / 100;
    private static TaintedSuppliers Suppliers(int rank, int mastery = 0, int oath = 0, int hard = 0, int boil = 0) =>
        TaintedPrepared.Capture(Values(rank, mastery, oath, hard, boil), stat => stat == "item_pierce_damage_immunity" ? 7 : 13);
    private static void Model()
    {
        int formulas = 0, packets = 0, bases = 0, contexts = 0;
        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "tainted-formula-source-cases.json"))))
        {
            foreach (var row in doc.RootElement.EnumerateArray())
            { Require(Suppliers(row[1].GetInt32(), row[2].GetInt32()).FireMasteryPercent == row[4].GetInt32(), "Tainted exact Fog EOF formula"); formulas++; }
        }
        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "tainted-fireball-source-cases.json"))))
        {
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var damage = TaintedNpcSkills.FireBall(Suppliers(row[0].GetInt32(), row[2].GetInt32(), boil: row[1].GetInt32()));
                Require(damage == new TaintedFireBallPacket(row[4].GetInt32(), row[5].GetInt32(), 4), "Tainted complete-source fire packet/own NPC synergy/mastery order"); packets++;
            }
        }
        for (int rank = 1; rank <= 255; rank++)
        foreach (int mastery in new[] { 0, 1, 17, 20, 255 })
        foreach (int oath in new[] { 0, 1, 6, 20 })
        foreach (int hard in new[] { 0, 4, 5, 9, 10, 20 })
        {
            int boil = rank % 21;
            var actual = Suppliers(rank, mastery, oath, hard, boil);
            var expected = new TaintedSuppliers(rank, 35 * (rank - 1) + (oath > 0 ? 50 + 35 * (oath - 1) : 0),
                Math.Min(10 + rank - 1, 25) + (mastery > 0 ? Math.Min(5 + mastery - 1, 25) : 0), Curve(mastery, 5, 39),
                10 * mastery, 75 + 20 * (rank - 1), 7, Curve(oath, 1, 12), 13,
                hard >= 10 ? 3 : hard >= 5 ? 2 : 1, (rank + 49) * 128, 15, Math.Min(rank, 30), rank, boil);
            Require(actual == expected, "Tainted rank/stat scopes or hard-only shared demon cap");
            var aura = TaintedNpcSkills.ResistFire(actual);
            Require(aura == new TaintedFireResistance(Math.Min(rank, 30), Curve(Math.Min(rank, 30), 15, 70), 16 + 2 * (Math.Min(rank, 30) - 1), 50),
                "Tainted NPC fire aura own curve/radius/period; no player passive maximum resistance");
            foreach (var (difficulty, life, armor, attack) in new[] { (MonsterDifficulty.Normal, 127, 75, 77), (MonsterDifficulty.Nightmare, 180, 90, 110), (MonsterDifficulty.Hell, 290, 190, 110) })
            {
                var birth = TaintedPrepared.Birth(Values(rank, mastery, oath, hard, boil), difficulty, 47, stat => stat == "item_pierce_damage_immunity" ? 7 : 13);
                Require(birth.PetLevel == Math.Min(47, rank + 35) && birth.BaseLife256 == life * 256 && birth.BaseArmor == armor && birth.BaseAttack == attack
                    && birth.MaximumLife256 == life * 256 + (int)((long)life * 256 * actual.LifePercent / 100) && birth.Suppliers == actual,
                    "Tainted own difficulty bases and fractional life");
            }
            contexts++;
        }
        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "goatman-creation-source-probe.json"))))
        {
            foreach (var row in doc.RootElement.GetProperty("base").EnumerateArray())
            {
                if (row[3].GetInt32() != 1) { continue; }
                var birth = TaintedPrepared.Birth(Values(row[1].GetInt32()), (MonsterDifficulty)row[2].GetInt32(), row[0].GetInt32(), _ => 0);
                Require(birth.PetLevel == row[5].GetInt32() && birth.LevelArmor == row[6].GetInt32() && birth.LevelAttack == row[7].GetInt32(), "Shared complete-source summon base stats"); bases++;
            }
        }
        Require(formulas == 3570 && packets == 6120 && contexts == 30600 && bases == 3825, "Tainted source matrix coverage");
        Require(TaintedNpcSkills.FireBall(Suppliers(1)) == new TaintedFireBallPacket(1280, 1792, 4), "Tainted rank1 Fire Ball 5..7, not Sorceress Fire Ball");
        Require(Suppliers(255, 255, hard: 4).PetMaximum == 1 && Suppliers(1, hard: 10).PetMaximum == 3, "Soft ranks changed shared demon cap");
        foreach (int rank in new[] { 0, 256 })
        { try { Suppliers(rank); throw new InvalidOperationException("Invalid summon rank accepted"); } catch (NotSupportedException) { } }
        try { Values(1).Formula(SkillMechanics.For(CombatAbility.SummonTainted).Text("passivecalc2")); throw new InvalidOperationException("General formula parser repaired malformed input"); }
        catch (InvalidDataException) { }
        Require(!WarlockCombat.Implemented(CombatAbility.SummonTainted), "Unfinished Tainted learned cast enabled");
        // The original four Goatman rows still reach their own existing loader.
        Require(GoatmanNpcSkills.Source(416).SourceKey == "Goatman Stun" && GoatmanNpcSkills.Source(419).SourceKey == "Goatman Cleave", "NPC catalogue extension changed Goatman rows");
        Log.Information("Tainted:3570Fog/6120NPC fire packets/3825shared base controls,30600rank contexts;no actor,aura,learned cast or modern AI claimed");
    }
    private static void Train(PlayerMobile owner, string id, int rank = 1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Warlock, id, out var talent), "Tainted talent");
        foreach (string pre in talent.Prerequisites) { if (ClassCreation.Profiles.GetRank(owner.Serial, pre) == 0) { Train(owner, pre); } }
        while (ClassCreation.Profiles.GetRank(owner.Serial, id) < rank) { Require(ClassCreation.Profiles.TryTrain(owner.Serial, id, out _), "Train " + id); }
    }
    private static void Check()
    {
        Model(); var saved = new Saved[2]; int index = 0;
        foreach (bool female in new[] { false, true })
        {
            PlayerMobile owner; _creating = true;
            try { owner = ClassSkillChecks.Player(DiabloClass.Warlock); } finally { _creating = false; }
            owner.Name = "TaintedFoundationFixture"; owner.Female = female; owner.Body = female ? 0x191 : 0x190;
            FixtureSpace.Corridor(owner, 6200, 26, 3700);
            if (owner.Backpack == null) { owner.AddItem(new Backpack()); }
            while (owner.RawStr < 50) { Require(ClassCreation.Profiles.TrySpendStat(owner, CharacterStat.Strength), "Tainted helmet Strength"); }
            Train(owner, "warlock.summontainted"); Train(owner, "warlock.demonicmastery", 4);
            Train(owner, "warlock.bloodoath", 2); Train(owner, "warlock.bloodboil", 5);
            var before = TaintedPrepared.Capture(owner);
            var helm = NamedGear.Create(NamedGear.Catalog.Items.Single(x => x.SourceKey == "Tarnhelm").Id, 99, new Random(173), true);
            owner.AddToBackpack(helm); Require(owner.EquipItem(helm), "Actual Tainted Tarnhelm equip"); ClassGear.Refresh(owner);
            var soft = TaintedPrepared.Capture(owner);
            Require(soft.Birth.Suppliers is { Rank: 2, FireMasteryPercent: 50, PetMaximum: 1, BloodBoilRank: 5, ResistFireRank: 2 }
                && before.Birth.Suppliers is { Rank: 1, FireMasteryPercent: 40, PetMaximum: 1, BloodBoilRank: 5 }, "Actual hard/effective NPC ranks");
            Train(owner, "warlock.demonicmastery", 5); Require(TaintedPrepared.Capture(owner).Birth.Suppliers.PetMaximum == 2, "Actual hard5 shared cap");
            Train(owner, "warlock.demonicmastery", 9); Require(TaintedPrepared.Capture(owner).Birth.Suppliers.PetMaximum == 2, "Actual soft10 must not raise cap");
            Train(owner, "warlock.demonicmastery", 10); var snapshot = TaintedPrepared.Capture(owner);
            Require(snapshot.Birth.Suppliers is { PetMaximum: 3, FireMasteryPercent: 110, BloodBoilRank: 5 } && snapshot.MasteryRank == 11, "Actual hard10 cap/soft mastery");
            foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
            { var rescaled = TaintedPrepared.Rescale(snapshot, difficulty); Require(TaintedPrepared.Rescale(rescaled, snapshot.Birth.Difficulty) == snapshot, "Captured Tainted difficulty roundtrip"); }
            var corrupt = snapshot with { Birth = snapshot.Birth with { MaximumLife256 = snapshot.Birth.MaximumLife256 + 1 } };
            try { TaintedPrepared.Rescale(corrupt, MonsterDifficulty.Hell); throw new InvalidOperationException("Corrupt birth accepted"); } catch (InvalidDataException) { }
            Require(JsonSerializer.Deserialize<TaintedSnapshot>(JsonSerializer.Serialize(snapshot)) == snapshot, "Captured DTO JSON roundtrip");
            owner.AddToBackpack(helm); ClassGear.Refresh(owner);
            Require(TaintedPrepared.Capture(owner).Birth.Suppliers.Rank == 1 && snapshot.Birth.Suppliers.Rank == 2
                && TaintedPrepared.Rescale(snapshot, snapshot.Birth.Difficulty) == snapshot, "Gear change recaptured existing birth");
            Require(owner.EquipItem(helm), "Original Tainted helm restore"); ClassGear.Refresh(owner);
            NecromancerEffects.SetMana256(owner, 5000 + index);
            Require(!SkillBindings.Instance.Assign(owner, false, CombatAbility.SummonTainted)
                && !SkillBindings.Instance.Assign(owner, true, CombatAbility.SummonTainted)
                && !SkillBindings.Instance.AssignQuick(owner, 0, CombatAbility.SummonTainted), "Unfinished learned input accepted");
            saved[index++] = new(owner.Serial.Value, helm.Serial.Value, female, NecromancerEffects.ManaBalance256(owner), snapshot, TaintedNpcSkills.FireBall(snapshot.Birth.Suppliers));
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Model();
        foreach (var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var owner = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var helm = World.FindItem((Serial)saved.Helm);
            ClassGear.Refresh(owner);
            Require(owner.Female == saved.Female && helm.Parent == owner && NecromancerEffects.ManaBalance256(owner) == saved.Mana
                && TaintedPrepared.Capture(owner) == saved.Snapshot && TaintedNpcSkills.FireBall(saved.Snapshot.Birth.Suppliers) == saved.Packet,
                "Original Warlock/Witch/helmet/resource and captured Tainted suppliers changed across reload");
            Require(!SkillBindings.Instance.AssignQuick(owner, 0, CombatAbility.SummonTainted), "Reload enabled unfinished learned summon");
        }
    }
}
