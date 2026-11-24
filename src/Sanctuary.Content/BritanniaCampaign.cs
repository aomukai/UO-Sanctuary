// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Engines.PartySystem;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;

namespace Sanctuary;

[Flags]
internal enum CampaignProgress : ushort
{
    FireIslandPassage = 1,
    SkeletonKing = 2,
    BlackthornAudience = 4,
    Lazarus = 8,
    Diablo = 16,
    RoyalProclamation = 32,
    DaggerIslePassage = 64
}

public static class BritanniaCampaign
{
    private const CampaignProgress GhostTownVictories = CampaignProgress.SkeletonKing | CampaignProgress.Lazarus;
    private const CampaignProgress Victories = GhostTownVictories | CampaignProgress.Diablo;

    private static readonly HashSet<Serial> _inBritain = [];
    private static CampaignProfiles _profiles;

    internal enum Summons { None, British, Blackthorn, Victory }

    public static void Configure()
    {
        _profiles = new();
        EventSink.ServerStarted += Install;
        EventSink.Disconnected += mobile => _inBritain.Remove(mobile.Serial);
        var previous = ClassCombatHooks.DeathAllowed;
        ClassCombatHooks.DeathAllowed = player => previous?.Invoke(player) != false && CampaignArenas.AllowDeath(player);
        ClassMapHooks.BoatOperationAllowed = BoatAllowed;
        ClassMapHooks.CreatureSpawning = SpawnInDungeon;
        ClassMapHooks.StoryItemVisible = (viewer, item) => item is not CampaignArenaGate gate
            || CampaignArenas.VisibleTo(viewer, gate);
    }

    public static void Initialize() => ClassMapHooks.LocationChanged += OnMove;

    private static void Install()
    {
        CampaignArenas.Install();
        CampaignDungeonRoutes.Install();
        CampaignActors.Install();
        DestardCourt.Install();
    }

    internal static bool Has(PlayerMobile player, CampaignProgress progress) =>
        (_profiles.States.GetValueOrDefault(player.Serial) & progress) == progress;

    internal static bool Record(PlayerMobile player, CampaignProgress progress)
    {
        if (Has(player, progress))
            return false;
        _profiles.States[player.Serial] = _profiles.States.GetValueOrDefault(player.Serial) | progress;
        return true;
    }

    internal static bool Complete(PlayerMobile player) => Has(player, Victories);

    private static bool BoatAllowed(Mobile from)
    {
        if (from is not PlayerMobile { AccessLevel: AccessLevel.Player } player || Complete(player))
            return true;
        player.SendMessage("You can captain your own ship after completing the Britannia campaign.");
        return false;
    }

    internal static MonsterDifficulty? DungeonDifficulty(Map map, Point3D point, Region region)
    {
        if (map != Map.Trammel)
            return null;
        if (CampaignArenas.At(map, point) is { } arena)
            return arena.Difficulty;
        if (region == null)
            return null;
        if (region.IsPartOf("Destard"))
            return MonsterDifficulty.Hell;
        if (region.IsPartOf("Deceit"))
            return MonsterDifficulty.Nightmare;
        return region.IsPartOf("Hythloth") ? MonsterDifficulty.Normal : null;
    }

    private static void SpawnInDungeon(BaseCreature creature, Point3D point, Map map)
    {
        if (creature.Map != Map.Internal)
            return;
        var region = Region.Find(point, map);
        if (DungeonPlayerScaling.Contains(map, point, region))
            Timer.DelayCall(TimeSpan.Zero, () =>
            {
                if (!creature.Deleted && DungeonPlayerScaling.IsWildDungeonCreature(creature))
                    CreatureProfiles.Initialize(creature);
            });
        if (map != Map.Trammel)
            return;
        if (creature is not EncounterCreature && MonsterVariants.Difficulty(creature) == MonsterDifficulty.Normal
            && DungeonDifficulty(map, point, region) is { } difficulty && difficulty != MonsterDifficulty.Normal
            && MonsterVariants.Supported(creature.GetType(), difficulty))
            MonsterVariants.Assign(creature, difficulty);
        if (region.IsPartOf("Destard"))
            CreatureProfiles.ApplyDestardResistances(creature);
    }

    internal static bool TownsEmpty(PlayerMobile player) =>
        Has(player, GhostTownVictories) && !Has(player, CampaignProgress.Diablo);

    internal static Summons NextSummons(PlayerMobile player)
    {
        if (Complete(player))
            return Has(player, CampaignProgress.RoyalProclamation) ? Summons.None : Summons.Victory;
        if (!Has(player, CampaignProgress.FireIslandPassage))
            return !Has(player, CampaignProgress.SkeletonKing) && ClassCreation.Profiles.HasReached(player.Serial, 30)
                ? Summons.British : Summons.None;
        if (Has(player, CampaignProgress.SkeletonKing) && !Has(player, CampaignProgress.Lazarus)
            && !Has(player, CampaignProgress.BlackthornAudience))
            return Summons.Blackthorn;
        return Summons.None;
    }

    private static void OnMove(Mobile mobile)
    {
        if (mobile is not PlayerMobile { Alive: true, Map: var map } player)
            return;
        if (map != Map.Trammel || !player.Region.IsPartOf("Britain"))
        {
            _inBritain.Remove(player.Serial);
            return;
        }
        if (!StatNetwork.SupportsCurrentProtocol(player) || !_inBritain.Add(player.Serial))
            return;
        CampaignActors.Intercept(player, NextSummons(player));
    }

    internal static void Defeated(PlayerMobile player, CampaignProgress victory, string boss)
    {
        if (!ClassCombat.Enabled(player))
            return;
        bool wasEmpty = TownsEmpty(player);
        if (!Record(player, victory))
            return;
        ScriptedDialogue.RecordDiscovery(player, victory switch
        {
            CampaignProgress.SkeletonKing => StoryDiscoveries.SkeletonKing,
            CampaignProgress.Lazarus => StoryDiscoveries.Lazarus,
            _ => StoryDiscoveries.Diablo
        });
        CampaignActors.ClearSummons(player);
        player.SendMessage($"You have defeated {boss}.");
        RefreshVisibility(player, wasEmpty);
    }

    private static void RefreshVisibility(PlayerMobile player, bool wasEmpty)
    {
        if (wasEmpty == TownsEmpty(player) || player.NetState is not { Running: true } connection || player.Map != Map.Trammel)
            return;
        foreach (var mobile in player.Map.GetMobilesInRange<Mobile>(player.Location, 24))
            if (mobile is BaseCreature creature && !creature.Controlled && !creature.Summoned)
                connection.SendRemoveEntity(mobile.Serial);
        player.SendEverything();
    }

    internal static bool MobileVisible(PlayerMobile viewer, Mobile mobile)
    {
        if (mobile is CampaignMessenger messenger)
            return messenger.Recipient == viewer.Serial;
        if (mobile is ICampaignRefugee)
            return TownsEmpty(viewer);
        if (mobile is Iolo { Recruited: false } && mobile.Map == Map.Trammel
            && mobile.InRange(NewHavenInn.IoloSeat, 2) && TownsEmpty(viewer))
            return true;
        if (!TownsEmpty(viewer) || mobile is Geoffrey || mobile.Map != Map.Trammel
            || mobile is not BaseCreature { Controlled: false, Summoned: false } npc
            || npc is Iolo { Recruited: true } or Merydwen { Recruited: true } or Wolfgang { Recruited: true })
            return true;
        if (npc.Region.IsPartOf("New Haven") || npc.Region.IsPartOf("Haven") || npc.Region.IsPartOf("Moonglow")
            || npc.Region.IsPartOf("Jhelom") || npc.Region.IsPartOf("Jhelom Islands") || npc.Region.IsPartOf("Heartwood"))
            return true;
        return npc.Region.GetRegion<TownRegion>() == null && !npc.Region.IsPartOf("Britain")
            && npc is not (BritainHarbourMaster or CampaignBlackthorn or AyilaInn or CampaignShamino);
    }

    internal static bool Sail(PlayerMobile player, Point3D destination)
    {
        var map = Map.Trammel;
        if (!Teleportation.Space(map, destination, player, allowAllies: true)
            && !PersistentMinions.FindPosition(map, destination, p => Math.Abs(p.Z - destination.Z) < 6, out destination))
            return false;
        ClassCombat.Stop(player);
        BaseCreature.TeleportPets(player, destination, map);
        player.MoveToWorld(destination, map);
        return true;
    }

    internal static IEnumerable<PlayerMobile> NearbyParty(PlayerMobile leader, Point3D point, int range)
    {
        if (Party.Get(leader) is not { Active: true } party)
            yield break;
        foreach (var member in party.Members)
            if (member.Mobile is PlayerMobile { Alive: true } player && player != leader && player.Map == leader.Map
                && player.InRange(point, range) && Math.Abs(player.Z - point.Z) < 16)
                yield return player;
    }

    private sealed class CampaignProfiles() : GenericPersistence("SanctuaryCampaign", 145)
    {
        internal readonly Dictionary<Serial, CampaignProgress> States = new();
        private KeyValuePair<Serial, CampaignProgress>[] _snapshot = [];

        public override void Serialize()
        {
            _snapshot = [.. States];
            base.Serialize();
        }

        public override void Serialize(IGenericWriter writer)
        {
            writer.Write(0);
            writer.Write(_snapshot.Length);
            foreach (var (player, progress) in _snapshot)
            {
                writer.Write(player.Value);
                writer.Write((ushort)progress);
            }
        }

        public override void Deserialize(IGenericReader reader)
        {
            if (reader.ReadInt() != 0)
                throw new InvalidDataException("Unsupported Britannia campaign save.");
            int count = reader.ReadInt();
            if (count is < 0 or > 1000000)
                throw new InvalidDataException("Invalid Britannia campaign player count.");
            for (int i = 0; i < count; i++)
            {
                var player = (Serial)reader.ReadUInt();
                var progress = (CampaignProgress)reader.ReadUShort();
                if (!player.IsMobile || !States.TryAdd(player, progress))
                    throw new InvalidDataException("Invalid Britannia campaign player identity.");
            }
        }

        public override void PostDeserialize()
        {
            foreach (var player in new List<Serial>(States.Keys))
                if (World.FindMobile(player) is not PlayerMobile { Deleted: false })
                    States.Remove(player);
        }
    }
}
