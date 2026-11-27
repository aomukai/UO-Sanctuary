// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class ShameOne : Spawner
{
    [Constructible]
    public ShameOne() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(120)) =>
        ShamePopulation.ConfigurePool(this);
}

[SerializationGenerator(0)]
public partial class ShameTwo : Spawner
{
    [Constructible]
    public ShameTwo() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(120)) =>
        ShamePopulation.ConfigurePool(this);
}

[SerializationGenerator(0)]
public partial class ShameThree : Spawner
{
    [Constructible]
    public ShameThree() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(120)) =>
        ShamePopulation.ConfigurePool(this);
}

[SerializationGenerator(0)]
public partial class ShameFour : Spawner
{
    [Constructible]
    public ShameFour() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(120)) =>
        ShamePopulation.ConfigurePool(this);
}

[SerializationGenerator(0)]
public partial class ShameMage : Spawner
{
    [Constructible]
    public ShameMage() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(120)) =>
        ShamePopulation.ConfigurePool(this);
}

public static class ShamePopulation
{
    internal static readonly Point3D MainBoat = new(5749, 60, -5);
    internal static readonly Point3D Deck = new(5750, 60, -2);
    internal static readonly Point3D BoardingShore = new(5752, 60, 0);
    internal static readonly Point3D Wheel = new(5749, 57, -2);

    private sealed record Pool(ShameRole[] Roles, Point3D[] LegacySites);

    private static readonly Pool _one = new(
        [ShameRole.Silicon, ShameRole.Chrysoberyl, ShameRole.Corundum, ShameRole.Tungsten],
        [new(5395, 117, 0), new(5404, 93, 10), new(5425, 58, 10), new(5387, 41, 20),
         new(5394, 11, 30), new(5468, 109, 35), new(5474, 20, 0), new(5477, 67, 20)]);

    private static readonly Pool _two = new(
        [ShameRole.Titanium, ShameRole.Moissanite, ShameRole.Pirate, ShameRole.Tidesinger],
        [new(5535, 22, 0), new(5549, 60, 0), new(5568, 36, 0), new(5575, 16, 10),
         new(5603, 93, 0), new(5603, 116, 0), new(5615, 18, 10)]);

    private static readonly Pool _three = new(
        [ShameRole.Stishovite, ShameRole.Diamond, ShameRole.Stormcaller, ShameRole.Corsair],
        [new(5411, 163, 0), new(5412, 201, 0), new(5394, 230, 10), new(5477, 183, 0),
         new(5505, 180, 0), new(5527, 210, 0), new(5571, 158, -10), new(5593, 141, 10)]);

    private static readonly Pool _four = new(
        [ShameRole.Wind, ShameRole.Fire, ShameRole.Water, ShameRole.Earth],
        [new(5649, 101, 10), new(5661, 16, -10), new(5684, 117, 0), new(5721, 12, 10), new(5724, 118, 0),
         new(5760, 99, 0), new(5804, 12, 0), new(5818, 80, 0), new(5845, 57, 0), new(5856, 107, 10), new(5875, 44, 0)]);

    private static readonly Pool _mage = new([ShameRole.CrazedResearcher, ShameRole.FanaticExperimenter], []);

    public static void Configure() => EventSink.ServerStarted += Install;

    private static Pool PoolFor(Spawner spawner) => spawner switch
    {
        ShameOne => _one,
        ShameTwo => _two,
        ShameThree => _three,
        ShameFour => _four,
        ShameMage => _mage,
        _ => null
    };

    internal static void ConfigurePool(Spawner spawner)
    {
        spawner.Name = spawner.GetType().Name;
        spawner.HomeRange = 4;
        spawner.WalkingRange = 4;
        spawner.SpawnLocationIsHome = true;
        var roles = PoolFor(spawner).Roles;
        foreach (var role in roles)
            spawner.AddEntry(nameof(ShameEncounter), probability: 100 / roles.Length, dotimer: false,
                parameters: $"{role} {Paragon.SpawnChance}");
    }

    internal static bool OnMainDeck(Point3D location, Map map) => map == Map.Trammel
        && Math.Abs(location.Z - Deck.Z) <= 1
        && location.X is >= 5748 and <= 5750 && location.Y is >= 57 and <= 63;

    internal static void Install()
    {
        var map = Map.Trammel;
        RetireLegacyPopulation(map);
        InstallManifestCrew(map);

        Ensure(map, new(5402, 46, 30), () => new ShameClue(ShameClueKind.Miner));
        InstallMinerRemains(map);
        Ensure(map, new(5438, 185, 22), () => new ShameClue(ShameClueKind.Almanach));
        InstallStudyShelf(map);
        Ensure(map, new(5436, 185, 28), () => new ShameMineral());
        Ensure(map, new(5438, 184, 22), () => new ShameClue(ShameClueKind.Handling));

        Ensure(map, MainBoat, () => new ShameBoat(4));
        Ensure(map, new(5751, 60, -2), () => new ShameGangplank());
        ShameExpedition.Install(map, Wheel);
        ShameShipCrossing.Install(map);
    }

    private static void InstallMinerRemains(Map map)
    {
        var destination = new Point3D(5400, 45, 30);
        var old = new Point3D(5401, 45, 30);
        foreach (var remains in map.GetItemsAt<ShameMinerRemains>(old))
        {
            if (remains.Location != old)
                continue;
            remains.MoveToWorld(destination, map);
            return;
        }

        Ensure(map, destination, () => new ShameMinerRemains());
    }

    private static void InstallStudyShelf(Map map)
    {
        var destination = new Point3D(5573, 199, 0);
        Point3D[] placements = [destination, new(5572, 200, 0), new(5572, 201, 0)];
        using var shelves = PooledRefList<ShameShelf>.Create();
        foreach (var point in placements)
        {
            foreach (var shelf in map.GetItemsAt<ShameShelf>(point))
                if (shelf.Location == point)
                    shelves.Add(shelf);
        }

        if (shelves.Count == 0)
        {
            Ensure(map, destination, () => new ShameShelf());
            return;
        }

        var moved = shelves[0];
        if (moved.Location != destination)
            moved.MoveToWorld(destination, map);

        for (int i = 1; i < shelves.Count; i++)
        {
            var duplicate = shelves[i];
            foreach (var book in duplicate.Items.ToArray())
                moved.AddItem(book);
            duplicate.Delete();
        }
    }

    private static void RetireLegacyPopulation(Map map)
    {
        // The original installer used four 100-weight entries with no grade argument.
        // Its placement coordinates distinguish those spawners from saved manual copies.
        foreach (var spawner in World.Items.Values.OfType<Spawner>()
            .Where(spawner => PoolFor(spawner) != null).ToArray())
        {
            var pool = PoolFor(spawner);
            if (pool.LegacySites.Length == 0 || spawner.Count != pool.Roles.Length || spawner.Entries.Count != pool.Roles.Length
                || !pool.Roles.All(role => spawner.Entries.Any(entry => MatchesLegacyEntry(entry, role))))
                continue;

            if (spawner.Map == map && spawner.Name == spawner.GetType().Name && pool.LegacySites.Contains(spawner.Location))
            {
                spawner.Delete();
                continue;
            }

            spawner.Count = 1;
            foreach (var entry in spawner.Entries)
            {
                entry.SpawnedProbability = 100 / pool.Roles.Length;
                entry.Parameters = $"{entry.Parameters} {Paragon.SpawnChance}";
            }
            foreach (var excess in spawner.Spawned.Keys.Where(spawn => !spawn.Deleted)
                .OrderBy(spawn => spawn.Serial.Value).Skip(1).ToArray())
            {
                spawner.Remove(excess);
                excess.Delete();
            }
        }

        RetireHouseSpawner(new(5438, 186, 22), "Shame west house");
        RetireHouseSpawner(new(5572, 200, 0), "Shame east house");

        void RetireHouseSpawner(Point3D location, string name)
        {
            using var retired = PooledRefList<Spawner>.Create();
            foreach (var spawner in map.GetItemsAt<Spawner>(location))
                if (spawner.Location == location && spawner.GetType() == typeof(Spawner) && spawner.Name == name
                    && spawner.Count == 1 && spawner.Entries.Count == 1
                    && MatchesLegacyEntry(spawner.Entries[0], ShameRole.Researcher))
                    retired.Add(spawner);
            foreach (var spawner in retired)
                spawner.Delete();
        }
    }

    private static bool MatchesLegacyEntry(SpawnerEntry entry, ShameRole role) =>
        entry.SpawnedName == nameof(ShameEncounter) && entry.SpawnedProbability == 100
        && entry.Parameters == role.ToString() && string.IsNullOrEmpty(entry.Properties);

    private static void InstallManifestCrew(Map map)
    {
        const string name = "Shame manifest crew";
        Ensure(map, new(5521, 17, 0), () =>
        {
            var spawner = new Spawner(3, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(5))
            {
                Name = name,
                HomeRange = 2,
                WalkingRange = 2,
                SpawnLocationIsHome = true
            };
            foreach (var role in new[] { ShameRole.Deckhand, ShameRole.Deckhand, ShameRole.Shipboy })
                spawner.AddEntry(nameof(ShameEncounter), dotimer: false, parameters: role.ToString());
            return spawner;
        }, spawner => spawner.Name == name);
    }

    internal static T Ensure<T>(Map map, Point3D point, Func<T> create, Func<T, bool> matches = null) where T : Item
    {
        foreach (var item in map.GetItemsAt<T>(point))
            if (item.Location == point && (matches == null || matches(item)))
                return item;
        var added = create();
        added.MoveToWorld(point, map);
        if (added is Spawner spawner)
            spawner.Respawn();
        return added;
    }
}

[SerializationGenerator(0)]
public partial class ShameBoat : BaseMulti
{
    internal ShameBoat(int itemID) : base(itemID)
    {
        Name = "a stranded pirate ship";
    }
}

[SerializationGenerator(0)]
public partial class ShameGangplank : Item
{
    internal ShameGangplank() : base(0x3ED4)
    {
        Movable = false;
        Name = "the pirate ship’s gangplank";
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile { Alive: true } player || player.Map != Map
            || !player.InRange(this, 2) || Math.Abs(player.Z - Z) >= 8)
            return;
        bool aboard = ShamePopulation.OnMainDeck(player.Location, player.Map);
        var target = aboard ? ShamePopulation.BoardingShore : ShamePopulation.Deck;
        if (!BritanniaCampaign.Sail(player, target))
            player.SendMessage("The gangplank is blocked.");
    }
}

[SerializationGenerator(0)]
public partial class ShameMinerRemains : Item
{
    internal ShameMinerRemains() : base(0x1B1D)
    {
        Name = "the remains of a miner";
        Movable = false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && DungeonEntranceRunes.CanSearch(player, this))
            DungeonEntranceRunes.Award(player, DungeonEntranceRunes.Shame);
    }
}

internal enum ShameClueKind { Miner, Manifest, Almanach, Handling }

[SerializationGenerator(0)]
public partial class ShameClue : BaseBook
{
    private static readonly string[] _titles =
        ["A Miner’s Note", "Cargo Manifest", "An Alchemical Almanach", "A Scribbled Note"];
    private static readonly string[] _textIds =
        ["shame.miners.note", "shame.pirates.manifest", "shame.research.almanach", "shame.research.handling_note"];
    private static readonly string[][] _lines =
    [
        ["We exposed a new seam today.", "By supper it had crept right", "back into the passage. No", "one will take the night shift.", "I heard it moving again."],
        ["Lower workings, Shame.", "Six chests of cut gemstones,", "promised for delivery afloat.", "Bring the shallow-draft boats.", "No samples have been seen.", "No advance to be paid."],
        ["Of moissanite: one shaving", "in the crucible brought my", "fire to three times its heat!", "Next trial: a whole splinter.", "I can scarcely wait."],
        ["Stable under observation.", "Further handling trials", "required."]
    ];

    internal ShameClue(ShameClueKind kind) : base(kind == ShameClueKind.Almanach ? 0xFF2 : 0x14ED,
        _titles[(int)kind], kind == ShameClueKind.Manifest ? "The quartermaster" : "Unknown", 1, false)
    {
        Name = _titles[(int)kind];
        Pages[0] = new BookPageInfo(_lines[(int)kind]);
        Movable = kind == ShameClueKind.Manifest;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !AuthoredWriting.CanRead(player, this))
            return;
        int index = Array.IndexOf(_titles, Title);
        if (index < 0 || !AuthoredWriting.Show(player, _textIds[index],
            index == (int)ShameClueKind.Almanach ? AuthoredTextKind.Book : AuthoredTextKind.Letter,
            Title, Author, AuthoredWriting.BookBody(this), _textIds[index] + ".body"))
            base.OnDoubleClick(player);
    }
}

[SerializationGenerator(0)]
public partial class ShameShelf : Item
{
    internal ShameShelf() : base(0xA98)
    {
        Movable = false;
        Name = "a bookshelf";
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile { Alive: true } player && player.Map == Map
            && player.InRange(this, 2) && Math.Abs(player.Z - Z) < 12 && player.InLOS(this))
        {
            var book = Items.OfType<BaseBook>().FirstOrDefault();
            if (book == null)
            {
                book = new BaseBook(0xFF1, "Teaching Stones to Think", "An unsigned hand", 2, false) { Movable = false };
                book.Pages[0] = new BookPageInfo("With but a whisper and the", "patience owed to all hard", "things, I persuaded the", "stones to rise as golems.", "They spoke before I did.");
                book.Pages[1] = new BookPageInfo("Does mineral flesh retain", "an impression of the dead?", "I shall not pretend to know", "whose voices I heard. More", "inquiry is required.");
                AddItem(book);
            }
            const string id = "shame.research.teaching_stones";
            if (!AuthoredWriting.Show(player, id, AuthoredTextKind.Book, book.Title, book.Author,
                AuthoredWriting.BookBody(book), id + ".body"))
                book.OnDoubleClick(player);
        }
    }
}

[SerializationGenerator(0)]
public partial class ShameMineral : Item
{
    [SerializableField(0)] private DateTime _detonatesAt;
    [SerializableField(1)] private DateTime _respawnsAt;
    private TimerExecutionToken _timer;

    internal ShameMineral() : base(0xF16)
    {
        Hue = 0x48F;
        Movable = false;
        Name = "a suspicious mineral - it sparkles!";
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile { Alive: true } player || !Visible || _detonatesAt != default || player.Map != Map
            || !player.InRange(this, 2) || Math.Abs(player.Z - Z) >= 12 || !player.InLOS(this))
            return;
        DetonatesAt = Core.Now.AddSeconds(5);
        Update();
    }

    [AfterDeserialization]
    private void RestoreTimer() => Timer.DelayCall(Update);

    private void Update()
    {
        _timer.Cancel();
        if (Deleted)
            return;
        if (_detonatesAt != default)
        {
            int remaining = (int)Math.Ceiling((_detonatesAt - Core.Now).TotalSeconds);
            if (remaining > 0)
            {
                PublicOverheadMessage(MessageType.Regular, 0x22, false, remaining.ToString());
                var next = _detonatesAt.AddSeconds(1 - remaining) - Core.Now;
                Timer.StartTimer(next > TimeSpan.Zero ? next : TimeSpan.Zero, Update, out _timer);
                return;
            }
            DetonatesAt = default;
            RespawnsAt = Core.Now.AddMinutes(10);
            var blast = EffectItem.Create(Location, Map, TimeSpan.FromSeconds(2));
            blast.PublicOverheadMessage(MessageType.Regular, 0x22, false, "BOOM!");
            Effects.SendLocationEffect(Location, Map, 0x36BD, 20);
            Effects.PlaySound(Location, Map, 0x207);
            Visible = false;
            using var victims = PooledRefList<Mobile>.Create();
            foreach (var target in Map.GetMobilesInRange<Mobile>(Location, 1))
                if (target is { Alive: true, Blessed: false } && Math.Abs(target.Z - Z) < 12)
                    victims.Add(target);
            foreach (var target in victims)
                SkillHealth.Damage(null, target, target.HitsMax * 128);
        }
        if (_respawnsAt == default)
            return;
        var delay = _respawnsAt - Core.Now;
        if (delay > TimeSpan.Zero)
            Timer.StartTimer(delay, Update, out _timer);
        else
        {
            RespawnsAt = default;
            Visible = true;
        }
    }

    public override void OnDelete()
    {
        _timer.Cancel();
        base.OnDelete();
    }
}
