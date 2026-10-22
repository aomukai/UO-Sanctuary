using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.CharacterCreation;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ClassNameChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ClassNameChecks));
    private static readonly string[] _male = ["Gargarean", "Sorcerer", "Necromancer", "Paladin", "Barbarian", "Druid", "Assassin", "Warlock"];
    private static readonly string[] _female = ["Amazon", "Sorceress", "Necromancer", "Paladin", "Barbarian", "Druidess", "Assassin", "Witch"];
    private static string Manifest => Path.Join(Core.BaseDirectory, "class-name-checks.json");
    private sealed record Saved(uint Serial, byte Class, bool Female, CharacterProfile Profile, StatAllocation Allocation,
        string Talent, int[] Stats, ActionTiming Timing, uint Weapon);

    public static void Configure()
    {
        Register("class-names-check", Create);
        Register("class-names-reload-check", Reload);
        Register("class-names-ui-check", CheckUi);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable class-name/progression checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
    private static string Expected(byte id, bool female) => (female ? _female : _male)[id - 101];
    private static int[] Stats(PlayerMobile p) => [p.Str, p.Dex, p.Int, p.HitsMax, p.ManaMax, p.StamMax];

    private static void Create()
    {
        var saved = new List<Saved>();
        for (byte id = 101; id <= 108; id++)
        {
            Saved male = null;
            foreach (bool female in new[] { false, true })
            {
                Require(DiabloClasses.TryFromWire(id, out var identity) && (byte)identity == id, "Wire identity changed");
                string expected = Expected(id, female);
                Require(DiabloClasses.DisplayName(identity, female) == expected
                    && DiabloClasses.TryFromDisplayName(expected, out var parsed) && parsed == identity,
                    $"Display alias/identity mismatch for {id}/{female}");
                var account = new Account($"class-name-{id}-{female}", Guid.NewGuid().ToString("N"));
                var args = CreationChecks.Args(id, account, female);
                CharacterCreation.CharacterCreatedEvent(args);
                var player = args.Mobile as PlayerMobile;
                Require(!args.Rejected && player != null && player.Female == female
                    && ClassCreation.Profiles.TryGet(player.Serial, out var initial) && (byte)initial.Class == id
                    && player.Title == $"Level 1 {expected}", "Sex/class identity or initial title changed during real creation");
                FixtureSpace.Place(player,new Point3D(1450,1705,0),Map.Felucca);
                var profiles = ClassCreation.Profiles;
                Require(profiles.AwardExperience(player, 123) && profiles.AdvanceLevel(player, 12), "Could not establish earned progression");
                string talent = TalentCatalog.ForClass(identity).Trees.SelectMany(t => t.Skills)
                    .First(t => t.RequiredLevel == 1 && t.Prerequisites.Length == 0).Id;
                Require(profiles.TryTrain(player.Serial, talent, out _) && profiles.TrySpendStat(player, CharacterStat.Vitality),
                    "Alias changed talent/stat ownership");
                var dagger = new Dagger();
                player.Backpack.DropItem(dagger);
                Require(player.EquipItem(dagger), "Could not equip comparison weapon");
                profiles.TryGet(player.Serial, out var profile);
                var row = new Saved(player.Serial.Value, id, female, profile, profiles.Stats(player.Serial), talent,
                    Stats(player), CombatTiming.For(player, CombatAbility.BasicAttack), dagger.Serial.Value);
                Verify(row);
                if (!female) { male = row; }
                else { Require(row.Stats.SequenceEqual(male.Stats) && row.Timing == male.Timing, "Cosmetic sex alias changed stats/action timing"); }
                saved.Add(row);
            }
        }
        Require(!DiabloClasses.TryFromDisplayName("102", out _) && !DiabloClasses.TryFromDisplayName("Unknown", out _),
            "Display parser accepted a numeric/arbitrary title");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }

    private static void Verify(Saved row)
    {
        var player = World.FindMobile((Serial)row.Serial) as PlayerMobile;
        var profiles = ClassCreation.Profiles;
        Require(player != null && player.Female == row.Female && profiles.TryGet(player.Serial, out var profile)
            && profile == row.Profile && (byte)profile.Class == row.Class, "Saved sex/identity/XP/level changed");
        Require(profiles.Stats(player.Serial) == row.Allocation && profiles.GetRank(player.Serial, row.Talent) == 1
            && profiles.AvailablePoints(player.Serial) == 10 && profiles.AvailableStatPoints(player.Serial) == 54,
            "Saved allocation/talent keys or point budget changed");
        string expected = Expected(row.Class, row.Female);
        Require(player.Title == $"Level 12 {expected}" && Stats(player).SequenceEqual(row.Stats)
            && CombatTiming.For(player, CombatAbility.BasicAttack) == row.Timing, "Title/stats/timing changed on reload");
        var weapon = World.FindItem((Serial)row.Weapon) as Dagger;
        Require(weapon?.Parent == player, "Equipped identity changed");
        var properties = new ObjectPropertyList(weapon);
        CombatTiming.AddSpeedProperties(weapon, properties);
        Require(properties.HeaderArgs?.Contains($"({expected},", StringComparison.Ordinal) == true, "Speed tooltip did not use display name");
        Require(TalentStrings(player, row.Profile).Contains($"{expected} — Level 12"), "Compiled talent heading did not use display name");
    }
    private static void Reload()
    {
        foreach (var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest))) { Verify(row); }
    }

    // Decode the actual compiled server gump, rather than testing another label formatter.
    private static string[] TalentStrings(PlayerMobile player, CharacterProfile profile)
    {
        var gump = (TalentGump)Activator.CreateInstance(typeof(TalentGump), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [player, profile, 0, 0], null);
        var writer = new SpanWriter(new byte[65536]);
        gump.Compile(ref writer);
        byte[] packet = writer.Span.ToArray();
        int layoutLength = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(19));
        int offset = 19 + 4 + layoutLength;
        int count = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset));
        int packedLength = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset + 4));
        using var compressed = new MemoryStream(packet, offset + 12, packedLength - 4);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var expanded = new MemoryStream();
        zlib.CopyTo(expanded);
        byte[] data = expanded.ToArray();
        var strings = new string[count];
        offset = 0;
        for (int i = 0; i < count; i++)
        {
            int length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
            offset += 2;
            strings[i] = Encoding.BigEndianUnicode.GetString(data, offset, length * 2);
            offset += length * 2;
        }
        return strings;
    }

    private static void CheckUi()
    {
        var player = Accounts.GetAccount("classui")?[0] as PlayerMobile;
        Require(player != null, "No GUI-created character");
        Require(ClassCreation.Profiles.TryGet(player.Serial, out var profile), "No GUI-created class profile");
        bool female = Environment.GetEnvironmentVariable("SANCTUARY_TEST_CLASS_NAMES_FEMALE") == "1";
        byte id = female ? (byte)106 : (byte)102;
        Require((byte)profile.Class == id && player.Female == female && profile.Level == 1
            && player.Title == $"Level 1 {Expected(id, female)}", "GUI submitted wrong class/sex/title");
        Require((player.NetState == null || player.FindGump<TalentGump>() != null)
            && TalentStrings(player, profile).Contains($"{Expected(id, female)} — Level 1"),
            "GUI did not open the matching talent heading");
    }
}
