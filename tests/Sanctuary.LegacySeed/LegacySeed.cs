// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.LegacySeed;

// Compiled against the installed OLD content and native assemblies. This
// fixture changes ordinary public actor state; the old application writes
// its own actual world/profile save. It never constructs a legacy payload.
public static class LegacySeed
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(LegacySeed));
    public static void Configure() => ConsoleInputHandler.RegisterCommand("necro-legacy-seed", "Wound the earlier build's real Necromancer fixture", _ => Core.LoopContext.Post(() =>
    {
        try { Seed(); Log.Information("necro-legacy-seed: passed"); }
        catch (Exception e) { Log.Error(e, "necro-legacy-seed: failed"); }
    }));
    private static void Seed()
    {
        using var old = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "class-skills-checks.json")));
        var row = old.RootElement;
        var owner = (PlayerMobile)World.FindMobile((Serial)row.GetProperty("Owner").GetUInt32());
        var target = World.FindMobile((Serial)row.GetProperty("Target").GetUInt32());
        var minion = (NecromancerMinion)World.FindMobile((Serial)row.GetProperty("Minion").GetUInt32());
        owner.Name = "ColdArmorFixtureLegacyUpgrade";
        owner.Hits = owner.HitsMax - 73;
        owner.Mana = owner.ManaMax - 17;
        if (!NecromancerEffects.Pay(owner, 128)) { throw new InvalidOperationException("Old mana fixture could not pay"); }
        minion.Hits = Math.Max(1, minion.HitsMax - 13);
        minion.AIObject?.AITimer.Stop();
        minion.Combatant = null; minion.Internalize(); target.Internalize(); owner.Internalize();
        var state = NecromancerEffects.Profiles.Find(owner);
        if (NecromancerEffects.Armor(owner) != row.GetProperty("Armor").GetInt32() || minion.ControlMaster != owner)
        { throw new InvalidOperationException("Old fixture identity/armor changed before save"); }
        File.WriteAllText(Path.Join(Core.BaseDirectory, "necro-legacy-seed.json"), JsonSerializer.Serialize(new
        {
            Owner = owner.Serial.Value, Target = target.Serial.Value, Minion = minion.Serial.Value,
            Armor = NecromancerEffects.Armor(owner), OwnerLife = owner.Hits, OwnerMaximum = owner.HitsMax,
            Mana256 = NecromancerEffects.ManaBalance256(owner), MinionLife = minion.Hits, MinionMaximum = minion.HitsMax,
            Damage = minion.CombatProfile.MinDamage, Kind = (byte)minion.Kind, Rank = minion.Rank, Mastery = minion.Mastery,
            OldState = JsonSerializer.SerializeToElement(state)
        }));
    }
}
