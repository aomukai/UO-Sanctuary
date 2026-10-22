// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Misc;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class EarnedTitleChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(EarnedTitleChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "earned-title-checks.json");
    private sealed record Saved(uint Owner, string State, string Display, uint Gatherer);
    private static void Require(bool value, string why) => GearDropChecks.Require(value, why);
    public static void Configure()
    { Register("earned-title-check", Check); Register("earned-title-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Earned cosmetic titles, thresholds, selection and persistence", _ => Core.LoopContext.Post(() =>
    { try { action(); _log.Information("{Command}: passed", name); } catch (Exception error) { _log.Error(error, "{Command}: failed", name); } }));
    private static PlayerMobile Player()
    {
        var p = new PlayerMobile { Player = true, Body = 0x190, Name = "Ada", Fame = 15000, Karma = -10000 };
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Necromancer); RecognitionChecks.Place(p); return p;
    }
    private static string State(PlayerMobile p) => JsonSerializer.Serialize(EarnedTitles.Profiles.Find(p.Serial));
    private static bool Select(PlayerMobile p, DiabloClass prefix, byte skill, byte tier) => EarnedTitles.Choose(p, EarnedTitles.Profiles.Find(p.Serial), prefix, skill, tier);
    private static void Swap(PlayerMobile p, DiabloClass c, SkillName craft)
    {
        ClassCreation.Profiles.TryGet(p.Serial, out var old); string site = RecognitionChecks.Place(p); p.Hits = p.HitsMax;
        Require(WardrobeSwitch.Commit(p, TownRecognition.PublicWardrobe(site), TownRecognition.BeginSession(p), old,
            ClassProfessions.Profiles.Find(p), c, craft), "Title fixture could not switch class/craft");
    }
    private static byte[] Encode(EarnedTitleProfiles profiles)
    { var w = new BufferWriter(new byte[65536], true); profiles.Serialize(w); return w.Buffer[..(int)w.Position]; }
    private static void Check()
    {
        var p = Player(); var empty = EarnedTitles.Profiles.Find(p.Serial);
        Require(empty.Classes == 0 && empty.Ranks.All(r => r == 0) && !Select(p, DiabloClass.Necromancer, 255, 0)
            && !Select(p, 0, (byte)SkillName.Blacksmith, 1), "Unearned title selected");
        ClassCreation.Profiles.AdvanceLevel(p, 98); Require(EarnedTitles.Profiles.Find(p.Serial).Classes == 0, "Class prefix unlocked early");
        ClassCreation.Profiles.AdvanceLevel(p, 99); Require(EarnedTitles.Profiles.Find(p.Serial).Classes == 4, "Level 99 did not unlock its prefix");
        Require(TownRecognition.Claim(p, RecognitionChecks.Place(p), RecognitionHonor.Hero), "Title fixture recognition failed");
        for (int i = 0; i < EarnedTitles.Skills.Length; i++)
        {
            var skill = p.Skills[EarnedTitles.Skills[i]]; skill.Cap = 120;
            foreach (int trained in new[] { 999, 1000, 1050, 1099, 1100, 1150, 1199, 1200 })
            {
                skill.BaseFixedPoint = trained;
                Require(EarnedTitles.Profiles.Find(p.Serial).Ranks[i] == EarnedTitles.Tier(trained), "Skill title threshold or gain hook is wrong");
            }
            skill.BaseFixedPoint = 999;
            Require(EarnedTitles.Profiles.Find(p.Serial).Ranks[i] == 3, "Losing skill erased an earned title");
        }
        var before = (p.RawStr, p.RawDex, p.RawInt, p.Hits, p.Mana, p.Stam, p.Fame, p.Karma, p.Kills, p.Title, p.Name, p.ShowFameTitle);
        Require(Select(p, DiabloClass.Necromancer, (byte)SkillName.Blacksmith, 3)
            && Titles.ComputeTitle(p, p) == "Deathspeaker Ada, Legendary Blacksmith Level 99 Necromancer"
            && before == (p.RawStr, p.RawDex, p.RawInt, p.Hits, p.Mana, p.Stam, p.Fame, p.Karma, p.Kills, p.Title, p.Name, p.ShowFameTitle),
            "Title selection changed gameplay/name data or legacy fame wording leaked");
        using (var list = new ObjectPropertyList(p))
        {
            p.GetProperties(list); list.Terminate();
            Require(list.Header == 1050045 && list.HeaderArgs.Contains("Deathspeaker") && list.HeaderArgs.Contains("Legendary Blacksmith")
                && !list.HeaderArgs.Contains("Lord"), "Name tooltip did not use the selected title slots");
        }
        var stale = EarnedTitles.Profiles.Find(p.Serial);
        Require(Select(p, 0, (byte)SkillName.Blacksmith, 1) && !EarnedTitles.Choose(p, stale, 0, 255, 0), "Stale title selection replaced newer preferences");
        Require(Select(p, DiabloClass.Necromancer, 255, 0) && EarnedTitles.Display(p)?.Name(p.Name) == "Deathspeaker Ada"
            && Select(p, 0, (byte)SkillName.Blacksmith, 2) && EarnedTitles.Display(p)?.Name(p.Name) == "Ada, Elder Blacksmith"
            && Select(p, 0, 255, 0) && EarnedTitles.Display(p)?.Name(p.Name) == "Ada", "Independent hide/rank choices failed");
        foreach (var c in Enum.GetValues<DiabloClass>())
        {
            Swap(p, c, SkillName.Tinkering); ClassCreation.Profiles.AdvanceLevel(p, 99);
            if (c == DiabloClass.Warlock)
            {
                Require(EarnedTitles.Prefix(c) == "", "Unapproved Warlock achievement prefix was invented");
                continue;
            }
            Require(Select(p, c, (byte)SkillName.Mining, 3) && EarnedTitles.Display(p)?.Prefix == EarnedTitles.Prefix(c), "Earned class prefix could not be selected");
            string title = EarnedTitles.Display(p)?.Name(p.Name); p.Female = !p.Female;
            Require(EarnedTitles.Display(p)?.Name(p.Name) == title, "Achievement titles changed their sex-neutral mapping");
        }
        Require(EarnedTitles.Profiles.Find(p.Serial).Classes == 255, "Not all eight class achievements were retained");
        var allClasses = Encode(EarnedTitles.Profiles);
        EarnedTitles.Profiles.Deserialize(new BufferReader(allClasses));
        Require(Encode(EarnedTitles.Profiles).SequenceEqual(allClasses), "Eight-class achievement mask did not round trip");
        Require(Select(p, DiabloClass.Sorceress, (byte)SkillName.Blacksmith, 3), "Final selection failed");
        string selected = State(p); Swap(p, DiabloClass.Necromancer, SkillName.Alchemy);
        Require(State(p) == selected && EarnedTitles.Display(p)?.Name(p.Name) == "Archmage Ada, Legendary Blacksmith", "Wardrobe tied selected titles to active class/craft");
        p.Name = "ACharacterNameWithThirtyLetters";
        Require(Titles.ComputeTitle(p, p).Length > 60, "Long-name title fixture did not cover the native packet limit");
        var gatherer = Player(); gatherer.Skills.Mining.Base = 100;
        Require(EarnedTitles.Profiles.Find(gatherer.Serial).Ranks[Array.IndexOf(EarnedTitles.Skills, SkillName.Mining)] == 1
            && !TownRecognition.HasAccess(gatherer) && TownRecognition.Profiles.Find(gatherer).Eligible == 0,
            "Gathering title granted civic/wardrobe rights");
        var legacy = new PlayerMobile { Name = "Legacy", Title = "the Scribe", Fame = 15000, Karma = 10000 };
        string old = Titles.ComputeTitle(legacy, legacy); EarnedTitles.Refresh(legacy);
        Require(EarnedTitles.Display(legacy) == null && Titles.ComputeTitle(legacy, legacy) == old, "Legacy character title formatting changed"); legacy.Delete();
        byte[] original = Encode(EarnedTitles.Profiles);
        for (int bad = 0; bad < 4; bad++)
        {
            var w = new BufferWriter(new byte[1024], true); w.Write(bad == 0 ? 1 : 0); w.Write(1); w.Write(bad == 1 ? 0u : p.Serial.Value);
            w.Write((byte)0); for (int i = 0; i < 13; i++) { w.Write((byte)(bad == 2 ? 4 : 0)); }
            w.Write((byte)(bad == 3 ? 103 : 0)); w.Write((byte)255); w.Write((byte)0);
            bool rejected = false; try { EarnedTitles.Profiles.Deserialize(new BufferReader(w.Buffer[..(int)w.Position])); } catch (InvalidDataException) { rejected = true; }
            Require(rejected && Encode(EarnedTitles.Profiles).SequenceEqual(original), "Corrupt title data replaced loaded authority");
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, State(p), Titles.ComputeTitle(p, p), gatherer.Serial.Value)));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var gatherer = (PlayerMobile)World.FindMobile((Serial)saved.Gatherer);
        Require(State(p) == saved.State && Titles.ComputeTitle(p, p) == saved.Display && EarnedTitles.Profiles.Find(p.Serial).Ranks.All(r => r == 3)
            && !TownRecognition.HasAccess(gatherer), "Reload lost earned titles, selections or gathering distinction");
        string before = State(p); Swap(p, DiabloClass.Barbarian, SkillName.Cooking); Swap(p, DiabloClass.Necromancer, SkillName.Alchemy);
        Require(State(p) == before, "Reloaded wardrobe replaced selected titles");
    }
}
