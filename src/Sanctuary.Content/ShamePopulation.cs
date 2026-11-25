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
    public ShameOne() : base(4, TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(120)) =>
        ShamePopulation.Configure(this, ShameRole.Silicon, ShameRole.Chrysoberyl, ShameRole.Corundum, ShameRole.Tungsten);
}

[SerializationGenerator(0)]
public partial class ShameTwo : Spawner
{
    [Constructible]
    public ShameTwo() : base(4, TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(120)) =>
        ShamePopulation.Configure(this, ShameRole.Titanium, ShameRole.Moissanite, ShameRole.Pirate, ShameRole.Tidesinger);
}

[SerializationGenerator(0)]
public partial class ShameThree : Spawner
{
    [Constructible]
    public ShameThree() : base(4, TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(120)) =>
        ShamePopulation.Configure(this, ShameRole.Stishovite, ShameRole.Diamond, ShameRole.Stormcaller, ShameRole.Corsair);
}

[SerializationGenerator(0)]
public partial class ShameFour : Spawner
{
    [Constructible]
    public ShameFour() : base(4, TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(120)) =>
        ShamePopulation.Configure(this, ShameRole.Wind, ShameRole.Fire, ShameRole.Water, ShameRole.Earth);
}

public static class ShamePopulation
{
    internal static readonly Point3D MainBoat = new(5749, 60, -5);
    internal static readonly Point3D Deck = new(5750, 60, -2);
    internal static readonly Point3D BoardingShore = new(5752, 60, 0);
    internal static readonly Point3D Wheel = new(5749, 57, -2);

    public static void Configure() => EventSink.ServerStarted += Install;

    internal static void Configure(Spawner spawner, params ShameRole[] roles)
    {
        if (spawner.GetType() != typeof(Spawner))
            spawner.Name = spawner.GetType().Name;
        spawner.HomeRange = 4;
        spawner.WalkingRange = 4;
        spawner.SpawnLocationIsHome = true;
        foreach (var role in roles)
            spawner.AddEntry(nameof(ShameEncounter), dotimer: false, parameters: role.ToString());
    }

    internal static bool OnMainDeck(Point3D location, Map map) => map == Map.Trammel
        && Math.Abs(location.Z - Deck.Z) <= 1
        && location.X is >= 5748 and <= 5750 && location.Y is >= 57 and <= 63;

    internal static void Install()
    {
        var map = Map.Trammel;
        Points<ShameOne>(map, () => new ShameOne(),
            new(5395, 117, 0), new(5404, 93, 10), new(5425, 58, 10), new(5387, 41, 20),
            new(5394, 11, 30), new(5468, 109, 35), new(5474, 20, 0), new(5477, 67, 20));
        Points<ShameTwo>(map, () => new ShameTwo(),
            new(5535, 22, 0), new(5549, 60, 0), new(5568, 36, 0), new(5575, 16, 10),
            new(5603, 93, 0), new(5603, 116, 0), new(5615, 18, 10));
        Points<ShameThree>(map, () => new ShameThree(),
            new(5411, 163, 0), new(5412, 201, 0), new(5394, 230, 10), new(5477, 183, 0),
            new(5505, 180, 0), new(5527, 210, 0), new(5571, 158, -10), new(5593, 141, 10));
        Points<ShameFour>(map, () => new ShameFour(),
            new(5649, 101, 10), new(5661, 16, -10), new(5684, 117, 0), new(5721, 12, 10), new(5724, 118, 0),
            new(5760, 99, 0), new(5804, 12, 0), new(5818, 80, 0), new(5845, 57, 0), new(5856, 107, 10), new(5875, 44, 0));

        PlacePack(map, "Shame manifest crew", new(5521, 17, 0), 2, ShameRole.Deckhand, ShameRole.Deckhand, ShameRole.Shipboy);
        PlacePack(map, "Shame west house", new(5438, 186, 22), 1, ShameRole.Researcher);
        PlacePack(map, "Shame east house", new(5572, 200, 0), 1, ShameRole.Researcher);

        Ensure(map, new(5402, 46, 30), () => new ShameClue(ShameClueKind.Miner));
        Ensure(map, new(5401, 45, 30), () => new ShameMinerRemains());
        Ensure(map, new(5438, 185, 22), () => new ShameClue(ShameClueKind.Almanach));
        Ensure(map, new(5572, 201, 0), () => new ShameShelf());
        Ensure(map, new(5436, 185, 28), () => new ShameMineral());
        Ensure(map, new(5438, 184, 22), () => new ShameClue(ShameClueKind.Handling));

        Ensure(map, MainBoat, () => new ShameBoat(4));
        Ensure(map, new(5549, 79, -5), () => new ShameBoat(4));
        Ensure(map, new(5751, 60, -2), () => new ShameGangplank());
        ShameExpedition.Install(map, Wheel);
    }

    private static void Points<T>(Map map, Func<T> create, params Point3D[] points) where T : Spawner
    {
        foreach (var point in points)
            Ensure(map, point, create);
    }

    private static void PlacePack(Map map, string name, Point3D point, int range, params ShameRole[] roles)
    {
        Ensure(map, point, () =>
        {
            var spawner = new Spawner(roles.Length, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(5)) { Name = name };
            Configure(spawner, roles);
            spawner.HomeRange = range;
            spawner.WalkingRange = range;
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
}

internal enum ShameClueKind { Miner, Manifest, Almanach, Handling }

[SerializationGenerator(0)]
public partial class ShameClue : BaseBook
{
    private static readonly string[] _titles =
        ["A Miner’s Note", "Cargo Manifest", "An Alchemical Almanach", "A Scribbled Note"];
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
        if (IsChildOf(from.Backpack) || from.Map == Map && from.InRange(this, 2) && Math.Abs(from.Z - Z) < 12 && from.InLOS(this))
            base.OnDoubleClick(from);
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
