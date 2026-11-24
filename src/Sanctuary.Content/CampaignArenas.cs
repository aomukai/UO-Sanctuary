// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Engines.PartySystem;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;

namespace Sanctuary;

internal static class CampaignArenas
{
    private static readonly TimeSpan IdleReset = TimeSpan.FromSeconds(20);

    internal sealed class Arena(string dungeon, Point3D door, Point3D origin, DungeonProfile profile, int bossLevel,
        MonsterDifficulty difficulty, CampaignProgress victory, int floor, Point3D? formerDoor = null)
    {
        internal readonly string Dungeon = dungeon;
        internal Point3D Door = door;
        internal readonly Point3D? FormerDoor = formerDoor;
        internal readonly Point3D Origin = origin;
        internal readonly DungeonProfile Profile = profile;
        internal readonly int BossLevel = bossLevel;
        internal readonly MonsterDifficulty Difficulty = difficulty;
        internal readonly CampaignProgress Victory = victory;
        internal readonly int Floor = floor;
        internal string Name => Victory switch
        {
            CampaignProgress.SkeletonKing => "Leoric",
            CampaignProgress.Lazarus => "Lazarus",
            _ => "Diablo"
        };
        internal CampaignBoss Boss;
        internal bool Cleared;
        internal DateTime? EmptySince;
        internal bool IsDiablo => Victory == CampaignProgress.Diablo;
        internal bool IsLazarus => Victory == CampaignProgress.Lazarus;
        internal int Width => IsLazarus ? WorldDecorationGraphics.MausoleumSize : IsDiablo ? DiabloCave.Size : LeoricThroneRoom.Width;
        internal int Height => IsLazarus ? Width : IsDiablo ? DiabloCave.Size : LeoricThroneRoom.Height;
        internal Rectangle2D Bounds => new(Origin.X, Origin.Y, Width, Height);
        internal Point3D Entry => IsLazarus ? Offset(41, 61) : IsDiablo ? Offset(5, Height / 2) : Offset(15, 35);
        internal Point3D Center => IsLazarus ? Offset(44, 44) : IsDiablo ? Offset(DiabloCave.Midpoint, Height / 2) : Offset(15, Height / 2);
        internal Point3D BossPost => IsLazarus ? Offset(WorldDecorationGraphics.PrelateX, WorldDecorationGraphics.PrelateY)
            : IsDiablo ? Center : Offset(15, 7);
        internal Point3D Offset(int x, int y) => new(Origin.X + x, Origin.Y + y, Origin.Z + (IsLazarus ? LazarusMausoleum.Elevation(x, y) : 0));
    }

    internal static readonly Arena[] All =
    [
        // Each profile range also accepts the previous base level so old bosses can load before replacement.
        new("Hythloth", new(6104, 33, 27), LeoricThroneRoom.Origin,
            new("campaign-skeleton-king", "crypt-sentinel", "King Leoric", MonsterArchetype.Tank, "Britannia campaign", 32, 37,
                CampaignBossAppearance.Leoric, 0x48D, new(30, 25, 10, 55, 50, 15)), 40,
            MonsterDifficulty.Normal, CampaignProgress.SkeletonKing, WorldDecorationGraphics.ThroneRoomFloor,
            formerDoor: new Point3D(6099, 158, -22)),
        new("Deceit", new(5276, 667, 6), new(WorldDecorationGraphics.MausoleumX, WorldDecorationGraphics.MausoleumY, 0),
            new("campaign-lazarus", "cinder-hierophant", "Lazarus", MonsterArchetype.Caster, "Britannia campaign", 52, 52,
                CampaignBossAppearance.Lazarus, 0x44B, new(30, 45, 65, 30, 50, 60)), 55,
            MonsterDifficulty.Nightmare, CampaignProgress.Lazarus, 0x051C, formerDoor: new Point3D(5305, 654, 0)),
        new("Destard", new(5190, 1010, 0), DiabloCave.Origin,
            new("campaign-diablo", "cinder-hierophant", "Diablo", MonsterArchetype.Caster, "Britannia campaign", 62, 72,
                CampaignBossAppearance.Diablo, 0x165, new(70, 70, 85, 45, 65, 60)), 65,
            MonsterDifficulty.Hell, CampaignProgress.Diablo, 0x051D, formerDoor: new Point3D(5150, 810, -8))
    ];

    internal static DungeonProfile Profile(string id) => All.FirstOrDefault(a => a.Profile.Id == id)?.Profile;
    internal static Arena At(Map map, Point3D point) => map == Map.Trammel
        ? All.FirstOrDefault(a => a.Bounds.Contains(point) && Math.Abs(a.Origin.Z - point.Z) < 16)
        : null;

    internal static void Install()
    {
        var map = Map.Trammel;
        foreach (var arena in All)
        {
            arena.Door = TownMarkets.Floor(map, arena.Door, [], false);
            if (arena.IsDiablo)
            {
                DiabloCave.Build(map, arena);
                DiabloCave.RetireOldArena(map, arena);
            }
            else if (arena.IsLazarus)
            {
                LazarusMausoleum.Build(map, arena);
                DiabloCave.RetireArena(map, arena, new Rectangle2D(6935, 3800, 33, 25), new Point3D(6939, 3812, 0));
            }
            else
            {
                LeoricThroneRoom.Build(map, arena);
                DiabloCave.RetireArena(map, arena, new Rectangle2D(6890, 3800, 33, 25), new Point3D(6894, 3812, 0));
            }
            var bounds = new Rectangle3D(arena.Origin.X, arena.Origin.Y, -16, arena.Width, arena.Height, 48);
            DungeonRegion region = arena.IsDiablo
                ? new DiabloCaveRegion(arena.Name, map, bounds)
                : arena.IsLazarus ? new MausoleumRegion(arena, map, bounds) : new LeoricThroneRegion(arena.Name, map, bounds);
            region.Entrance = arena.Door;
            region.GoLocation = arena.Entry;
            region.Register();
            PlaceGate(arena, map);

            foreach (var boss in World.Mobiles.Values.OfType<CampaignBoss>()
                         .Where(b => b.Map == map && arena.Bounds.Contains(b.Location)).ToArray())
            {
                if (arena.Boss == null && boss.Alive && boss.Dungeon.Id == arena.Profile.Id
                    && (!arena.IsLazarus || boss is ArchbishopLazarus) && boss.Basis.Level == arena.BossLevel && boss.Difficulty == arena.Difficulty)
                {
                    boss.ApplyAppearance(arena);
                    boss.Home = boss is ArchbishopLazarus { Stop: >= 0 } ? boss.Location : arena.BossPost;
                    if (boss is ArchbishopLazarus { Stop: < 0 } && boss.Location != boss.Home)
                        boss.MoveToWorld(boss.Home, map);
                    boss.HomeMap = map;
                    if (arena.IsDiablo)
                        boss.RangeHome = DiabloCave.LeashRadius;
                    arena.Boss = boss;
                }
                else
                    boss.Delete();
            }
        }

        Timer.DelayCall(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4), Maintain);
    }

    private static void PlaceGate(Arena arena, Map map)
    {
        CampaignArenaGate FindAt(Point3D point)
        {
            foreach (var candidate in map.GetItemsAt<CampaignArenaGate>(point))
                if (candidate.Location == point && candidate.MapDest == map && At(map, candidate.PointDest) == arena)
                    return candidate;
            return null;
        }

        var gate = FindAt(arena.Door);
        if (arena.FormerDoor is { } formerDoor)
        {
            var previous = FindAt(TownMarkets.Floor(map, formerDoor, [], false));
            if (gate == null)
                gate = previous;
            else if (previous != null && previous != gate)
                previous.Delete();
        }

        gate ??= new CampaignArenaGate(arena.Entry);
        gate.PointDest = arena.Entry;
        gate.Name = $"passage to the {arena.Profile.Name} arena";
        if (gate.Map != map || gate.Location != arena.Door)
            gate.MoveToWorld(arena.Door, map);
    }

    internal static bool VisibleTo(PlayerMobile player, CampaignArenaGate gate) =>
        At(gate.Map, gate.PointDest) is { Cleared: false } arena && HasPrerequisites(player, arena)
        && !BritanniaCampaign.Has(player, arena.Victory);

    internal static bool HasPrerequisites(PlayerMobile player, Arena arena) => arena.Victory switch
    {
        CampaignProgress.Lazarus => BritanniaCampaign.Has(player, CampaignProgress.SkeletonKing),
        CampaignProgress.Diablo => BritanniaCampaign.Has(player, CampaignProgress.SkeletonKing | CampaignProgress.Lazarus),
        _ => true
    };

    private static void RefreshGate(Arena arena)
    {
        var map = Map.Trammel;
        foreach (var gate in map.GetItemsAt<CampaignArenaGate>(arena.Door))
            foreach (var viewer in map.GetMobilesInRange<PlayerMobile>(arena.Door, 18))
                if (viewer.NetState is { Running: true } connection)
                {
                    if (viewer.CanSee(gate))
                        viewer.SendEverything();
                    else
                        connection.SendRemoveEntity(gate.Serial);
                }
    }

    private static bool Occupied(Arena arena)
    {
        foreach (var mobile in Map.Trammel.GetMobilesInRange<PlayerMobile>(arena.Center, arena.Width))
            if (mobile is { Alive: true, NetState.Running: true } && At(mobile.Map, mobile.Location) == arena)
                return true;
        return false;
    }

    private static void Maintain()
    {
        foreach (var arena in All)
        {
            if (Occupied(arena))
            {
                arena.EmptySince = null;
                EnsureBoss(arena);
            }
            else if (arena.EmptySince == null)
                arena.EmptySince = Core.Now;
            else if (Core.Now - arena.EmptySince.Value >= IdleReset)
            {
                arena.Boss?.Delete();
                arena.Boss = null;
                if (arena.Cleared)
                {
                    arena.Cleared = false;
                    RefreshGate(arena);
                }
            }
        }
    }

    internal static void EnsureBoss(Arena arena)
    {
        if (arena.Cleared || arena.Boss is { Alive: true, Deleted: false })
            return;
        var boss = arena.IsLazarus ? new ArchbishopLazarus(arena) : new CampaignBoss(arena);
        boss.Home = arena.BossPost;
        boss.HomeMap = Map.Trammel;
        boss.RangeHome = arena.IsDiablo ? DiabloCave.LeashRadius : arena.IsLazarus ? arena.Width : 10;
        arena.Boss = boss;
        boss.MoveToWorld(arena.BossPost, Map.Trammel);
        if (boss is ArchbishopLazarus lazarus) lazarus.PlaceBodyguards();
    }

    internal static void Defeated(CampaignBoss boss)
    {
        if (At(boss.Map, boss.Location) is not { } arena || arena.Boss != boss)
            return;
        arena.Cleared = true;
        RefreshGate(arena);
        var credited = new HashSet<PlayerMobile>();
        if (ClassLoot.Killer(boss) is { } killer)
            credited.Add(killer);
        foreach (var hit in boss.DamageEntries)
            if (hit.DamageGiven > 0 && (hit.Damager as PlayerMobile
                ?? (hit.Damager as BaseCreature)?.GetDamageMaster(boss) as PlayerMobile) is { } contributor)
                credited.Add(contributor);
        foreach (var player in credited.ToArray())
            foreach (var member in BritanniaCampaign.NearbyParty(player, boss.Location, arena.Width))
                credited.Add(member);
        foreach (var player in credited)
            if (At(player.Map, player.Location) == arena)
                BritanniaCampaign.Defeated(player, arena.Victory, arena.Profile.Name);
    }

    internal static bool AllowDeath(PlayerMobile player)
    {
        if (At(player.Map, player.Location) == null || Party.Get(player) is { Active: true })
            return true;
        bool ghostTown = BritanniaCampaign.TownsEmpty(player);
        var inn = ghostTown ? CampaignActors.HavenInn : CampaignActors.BritainInn;
        if (!BritanniaCampaign.Sail(player, inn))
            return true;
        TimedEffects.Clear(player);
        player.CurePoison(player);
        TownPortals.Close(player.Serial);
        player.Hits = player.HitsMax;
        player.Mana = player.ManaMax;
        player.Stam = player.StamMax;
        player.SendMessage(ghostTown
            ? "You wake at the Bountiful Harvest Inn. The battle still lingers in your mind, as vivid as any dream."
            : "You wake at the Britain inn. The battle still lingers in your mind, as vivid as any dream.");
        return false;
    }
}

[SerializationGenerator(0)]
public partial class CampaignArenaTile : Static
{
    internal CampaignArenaTile(int graphic, int hue) : base(graphic) => Hue = hue;

    public override int GetUpdateRange(Mobile viewer) =>
        WorldDecorationGraphics.IsDiabloCaveTerrain(Map?.MapIndex ?? -1, X, Y, (ushort)ItemID)
            ? WorldDecorationGraphics.DiabloTerrainRange : base.GetUpdateRange(viewer);
}

[SerializationGenerator(0)]
public partial class CampaignArenaGate : Teleporter
{
    private const int UnderlevelWarningThreshold = 10;

    internal CampaignArenaGate(Point3D destination) : base(destination, Map.Trammel)
    {
        ItemID = 0xDDA;
        Visible = true;
    }

    public override void DoTeleport(Mobile mobile)
    {
        if (mobile is not PlayerMobile { Alive: true } player || CampaignArenas.At(Map, PointDest) is not { } arena
            || !CampaignArenas.VisibleTo(player, this))
            return;
        var companions = BritanniaCampaign.NearbyParty(player, Location, 4)
            .Where(member => CampaignArenas.HasPrerequisites(member, arena)).ToArray();
        if (!BritanniaCampaign.Sail(player, PointDest))
        {
            player.SendMessage("The passage is crowded. Try again in a moment.");
            return;
        }
        foreach (var companion in companions)
            BritanniaCampaign.Sail(companion, PointDest);
        CampaignArenas.EnsureBoss(arena);
    }

    public override bool OnMoveOver(Mobile mobile) => true;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile { Alive: true } player || !CanDoubleClick(player) || !CanTeleport(player)
            || CampaignArenas.At(Map, PointDest) is not { } arena)
            return;

        if (arena.BossLevel - ClassStats.Level(player) >= UnderlevelWarningThreshold)
        {
            if (player.NetState is not { Running: true } connection)
                return;
            player.SendGump(new ArenaLevelWarning(arena.BossLevel, accepted =>
            {
                if (accepted && player.Alive && player.NetState == connection && connection.Running
                    && CanDoubleClick(player) && CanTeleport(player))
                    StartTeleport(player);
            }));
            return;
        }

        player.CloseGump<ArenaLevelWarning>();
        StartTeleport(player);
    }
}

internal sealed class ArenaLevelWarning(int level, Action<bool> reply) : WarningGump(
    $"The recommended level of the encounter is lvl {level}. Proceed?", 380, 132, reply)
{
    public override bool Singleton => true;
}

[SerializationGenerator(1)]
public partial class CampaignBoss : EncounterCreature
{
    private DateTime _nextStrike;
    public override string CorpseName => $"{Name}'s corpse";
    [SerializableField(0)] private bool _destardFirst;
    [SerializableField(1)] private bool _destardFinal;
    [SerializableField(2)] private DestardChampion _destardChampion;

    // Version 0 had no fields on CampaignBoss itself.
    private void Deserialize(IGenericReader reader, int version) { }

    public override Mobile Combatant
    {
        get => base.Combatant;
        set => base.Combatant = _destardChampion is { } tank && tank.Protecting(this) ? tank : value;
    }

    internal override bool IsEnemyIgnoringLeash(Mobile mobile) =>
        mobile is DestardChampion dragon ? dragon == _destardChampion && dragon.Protecting(this) : base.IsEnemyIgnoringLeash(mobile);

    private void DestardSupport(CampaignArenas.Arena arena)
    {
        if (_destardChampion is { } dragon && !dragon.Protecting(this))
        {
            dragon.Delete();
            DestardChampion = null;
        }

        if (_destardChampion == null)
        {
            bool final = _destardFirst && !_destardFinal && 4L * Hits <= HitsMax;
            bool first = !_destardFirst && 4L * Hits <= 3L * HitsMax;
            if ((final || first) && DestardAid.HasPatron(this, arena, final) && DestardAid.Arrive(this, arena, final) is { } helper)
            {
                StopCombatActions();
                DestardChampion = helper;
                DestardFirst = true;
                DestardFinal = final;
            }
        }

        if (_destardChampion is { } tank)
        {
            Combatant = tank;
            FocusMob = tank;
        }
    }

    public override void OnDelete()
    {
        _destardChampion?.Delete();
        base.OnDelete();
    }


    internal CampaignBoss(CampaignArenas.Arena arena)
    {
        var profile = arena.Profile;
        const EncounterGrade grade = EncounterGrade.SuperUnique;
        int baseLevel = arena.BossLevel - EncounterProfiles.Level(0, grade);
        Assign(profile, baseLevel, arena.Difficulty, grade,
            ChampionKind.Champion, [], EliteAura.None, profile.Name, Random.Shared);
        ApplyAppearance(arena);
    }

    internal void ApplyAppearance(CampaignArenas.Arena arena)
    {
        if (arena.Victory == CampaignProgress.SkeletonKing && Name == "King Leoric, the Skeleton King")
            Name = arena.Profile.Name;
        if (arena.IsLazarus && Body == 0x190)
        {
            // The original human body wore these pieces to approximate the portrait.
            if (FindItemOnLayer(Layer.OuterTorso) is Robe { Hue: 0x497 } robe) robe.Delete();
            if (FindItemOnLayer(Layer.Shoes) is Sandals sandals) sandals.Delete();
            HairItemID = 0;
        }
        Body = arena.Profile.Body;
        Hue = 0;
    }

    protected override bool IsRefuge(Point3D point) => CampaignArenas.At(Map, point) is { } arena
        && (arena.Victory == CampaignProgress.SkeletonKing ? point.Y >= arena.Origin.Y + LeoricThroneRoom.VestibuleY : point.X < arena.Origin.X + 9);
    internal override int HomeLeashRadius => Dungeon.Id == "campaign-diablo" ? DiabloCave.LeashRadius : base.HomeLeashRadius;

    public override void OnThink()
    {
        if (CampaignArenas.At(Map, Location) is { Victory: CampaignProgress.Diablo } destard)
            DestardSupport(destard);
        base.OnThink();
        if (!Alive || Core.Now < _nextStrike || SpellInProgress || Combatant is not Mobile { Alive: true } target
            || !InRange(target, 9) || !InLOS(target) || IsRefuge(target.Location)
            || CampaignArenas.At(Map, Location) is not { } arena)
            return;

        _nextStrike = Core.Now + TimeSpan.FromSeconds(12);
        Animate(arena.IsDiablo ? 12 : 4, 6, 1, true, false, 0);
        var point = target.Location;
        var (radius, element, warning, art) = arena.Victory switch
        {
            CampaignProgress.SkeletonKing => (2, ResistanceType.Physical, "The Skeleton King raises his weapon to break the floor.", 0x376A),
            _ => (4, ResistanceType.Fire, "Heat gathers in the stone under Diablo's mark.", 0x3709)
        };
        PublicOverheadMessage(MessageType.Regular, 0x59, false, warning);
        Effects.SendLocationEffect(point, Map, art, 18, 9);
        Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
        {
            if (Deleted || !Alive || CampaignArenas.At(Map, Location) != arena)
                return;
            if (_destardChampion is { } tank && tank.Protecting(this) && tank.InRange(point, radius))
                EliteCombat.Hit(this, tank, (Basis.MinDamage + Basis.MaxDamage) / 2, element);

            foreach (var player in Map.GetMobilesInRange<PlayerMobile>(point, radius))
                if (player.Alive && Math.Abs(player.Z - point.Z) < 8 && CampaignArenas.At(player.Map, player.Location) == arena
                    && (!arena.IsDiablo || EncounterProfiles.ClearSight(Map, point, player.Location))
                    && !IsRefuge(player.Location) && CanBeHarmful(player, false))
                    EliteCombat.Hit(this, player, (Basis.MinDamage + Basis.MaxDamage) / 2, element);
        });
    }

    public override void OnDeath(Container corpse)
    {
        CampaignArenas.Defeated(this);
        base.OnDeath(corpse);
    }
}
