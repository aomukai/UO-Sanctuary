using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Server;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ResurrectionChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ResurrectionChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "healer-check.json");
    public static void Configure()
    {
        Register("resurrection-check", Check);
        Register("resurrection-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Healer and class resurrection checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", command);
            }
        }));
    private static void Require(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
    private static GraveyardHealer FindHealer()
    {
        GraveyardHealer found = null;
        foreach (var healer in Map.Felucca.GetMobilesInRange<GraveyardHealer>(new Point3D(1380, 1504, 0), 4))
        {
            Require(found == null, "Healer installation duplicated the NPC");
            found = healer;
        }
        Require(found is { IsInvulnerable: true, CantWalk: true, Price: 0 },
            $"No fixed, invulnerable, free graveyard healer; ground Z={Map.Felucca.GetAverageZ(1380, 1504)}");
        return found;
    }
    private static BaseSpawner FindSpawner()
    {
        BaseSpawner found = null;
        foreach (var spawner in Map.Felucca.GetItemsInRange<BaseSpawner>(new Point3D(1380, 1504, 0), 4))
        {
            if (spawner.Name == "Sanctuary Healer (graveyard)")
            {
                Require(found == null, "Healer spawner was duplicated");
                found = spawner;
            }
        }
        Require(found != null && found.Spawned.Count == 1, "Healer spawner does not maintain exactly one healer");
        return found;
    }
    private static void Check()
    {
        CorpseRecoveryChecks.Check();
        GraveyardPopulation.Install();
        var healer = FindHealer();
        var spawner = FindSpawner();
        GraveyardPopulation.Install();
        Require(FindHealer() == healer && FindSpawner() == spawner, "Repeated installation replaced the healer");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new[] { healer.Serial.Value, spawner.Serial.Value }));
        foreach (DiabloClass characterClass in Enum.GetValues<DiabloClass>())
        {
            var player = new PlayerMobile { Player = true, Body = 0x190, Name = "ResurrectionCheck" };
            var dagger = new Dagger();
            try
            {
                ClassCreation.Profiles.Assign(player, characterClass);
                var floor = CombatPractice.FindFloor(Map.Trammel, 1380, 1504);
                player.MoveToWorld(floor, Map.Trammel);
                player.AddItem(new Backpack());
                Require(player.EquipItem(dagger), "Could not equip resurrection test item");
                player.Kill();
                Require(!player.Alive && player.Location == floor && player.Corpse is Corpse
                    && dagger.IsChildOf(player.Corpse), "Death moved the ghost or lost corpse equipment");
                Require(!ClassCombat.IsHostile(player, healer), "Healer was targetable as an enemy");
                var corpse = player.Corpse;
                var state = (NetState)RuntimeHelpers.GetUninitializedObject(typeof(NetState));
                state.Mobile = player;
                var cancel = new RelayInfo(0, [], [], [], []);
                new ResurrectGump(healer).OnResponse(state, cancel);
                Require(!player.Alive, "Cancel resurrected the ghost");
                var accept = new RelayInfo(1, [], [], [], []);
                new ResurrectGump(healer, hitsScalar: 0.2).OnResponse(state, accept);
                Require(player.Alive && player.Hits == player.HitsMax && player.Location == floor,
                    "Resurrection did not restore full class life or moved the character");
                Require(player.Mana == 0 && player.Stam == player.StamMax,
                    "Full-health rule unexpectedly changed mana or stamina resurrection rules");
                Require(!corpse.Deleted && dagger.IsChildOf(corpse), "Resurrection auto-reclaimed or destroyed corpse items");
                player.Hits -= 5;
                int wounded = player.Hits;
                player.Resurrect();
                Require(player.Hits == wounded, "Calling resurrection on a living player healed them");
                player.Kill();
                player.Resurrect();
                Require(player.Alive && player.Hits == player.HitsMax, "Direct or shrine resurrection did not restore full life");
                corpse.Delete();
            }
            finally
            {
                player.Corpse?.Delete();
                player.Delete();
                dagger.Delete();
                ClassCreation.Profiles.PruneDeleted();
            }
        }
        var legacy = new PlayerMobile { Player = true, Body = 0x190, Name = "LegacyResurrection", Str = 100 };
        try
        {
            legacy.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 1380, 1504), Map.Trammel);
            legacy.Kill();
            legacy.Resurrect();
            Require(legacy.Alive && legacy.Hits == 10 && legacy.Hits < legacy.HitsMax,
                "Legacy character resurrection changed");
        }
        finally
        {
            legacy.Corpse?.Delete();
            legacy.Delete();
        }
    }
    private static void Reload()
    {
        var serials = JsonSerializer.Deserialize<uint[]>(File.ReadAllText(Manifest));
        var healer = FindHealer();
        var spawner = FindSpawner();
        Require(healer.Serial.Value == serials[0] && spawner.Serial.Value == serials[1],
            "Healer or spawner identity changed after restart");
        GraveyardPopulation.Install();
        Require(FindHealer() == healer && FindSpawner() == spawner, "Restart installation duplicated the healer");
    }
}
