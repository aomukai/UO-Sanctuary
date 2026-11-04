// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class NecromancerLegacyUpgradeChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(NecromancerLegacyUpgradeChecks));
    private sealed record Seed(uint Owner, uint Target, uint Minion, int Armor, int OwnerLife, int OwnerMaximum,
        int Mana256, int MinionLife, int MinionMaximum, int Damage, byte Kind, int Rank, int Mastery, JsonElement OldState);
    private sealed record Progress(int Generation, int Remaining256);
    private static string PathFor(string name) => Path.Join(Core.BaseDirectory, name + ".json");
    public static void Configure() => ConsoleInputHandler.RegisterCommand("necro-legacy-upgrade-check", "Import the real earlier-build Necromancer save", _ => Core.LoopContext.Post(() =>
    {
        try { Check(); Log.Information("necro-legacy-upgrade-check: passed"); }
        catch (Exception e) { Log.Error(e, "necro-legacy-upgrade-check: failed"); }
    }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static void Check()
    {
        var seed = JsonSerializer.Deserialize<Seed>(File.ReadAllText(PathFor("necro-legacy-seed")));
        var prior = File.Exists(PathFor("necro-legacy-progress"))
            ? JsonSerializer.Deserialize<Progress>(File.ReadAllText(PathFor("necro-legacy-progress"))) : null;
        var owner = World.FindMobile((Serial)seed.Owner) as PlayerMobile;
        var minion = World.FindMobile((Serial)seed.Minion) as NecromancerMinion;
        Require(!seed.OldState.TryGetProperty("BoneArmorPool", out _), "Earlier writer unexpectedly serialized the new pool representation");
        Require(owner is { Alive:true, Deleted:false } && owner.Hits == seed.OwnerLife && owner.HitsMax == seed.OwnerMaximum
            && NecromancerEffects.ManaBalance256(owner) == seed.Mana256, "Original owner wounds, maximum or fractional mana changed across the actual binary upgrade");
        Require(minion is { Alive:true, Deleted:false } && minion.ControlMaster == owner && minion.Hits == seed.MinionLife
            && minion.HitsMax == seed.MinionMaximum && (byte)minion.Kind == seed.Kind && minion.Rank == seed.Rank && minion.Mastery == seed.Mastery,
            $"Original legacy summon ownership/resources/stats changed: life={minion?.Hits}/{minion?.HitsMax}, expected={seed.MinionLife}/{seed.MinionMaximum}, damage={minion?.CombatProfile.MinDamage}/{seed.Damage}");
        // The approved Clay replacement curve changes this old rank-1
        // damage from 2 to 3. Preserve identity, wounds and captured ranks,
        // rather than incorrectly freezing the superseded damage balance.
        Require(seed.Kind == (byte)CombatAbility.ClayGolem && seed.Rank == 1 && seed.Mastery == 1 && seed.Damage == 2
            && minion.CapturedValues.Formula("skill('Golem Mastery'.lvl)") == 1 && minion.CombatProfile.MinDamage == 3,
            "Approved Clay damage or original captured rank changed across the old save");
        Require(SkillBindings.Instance.GetQuick(owner.Serial, DiabloClass.Necromancer, 11) == CombatAbility.LowerResist,
            "Earlier F12 skill binding changed");
        int remaining = prior?.Remaining256 ?? seed.Armor * 256;
        var pool = NecromancerEffects.Profiles.Find(owner)?.BoneArmorPool;
        Require(prior == null ? pool == null : pool is { Rank:null, Maximum256:null } && pool.Remaining256 == remaining,
            "Legacy pool provenance or previously upgraded fractional capacity changed");
        Require(NecromancerEffects.Armor256(owner) == remaining, "Legacy remaining capacity was recaptured from current talents");
        Require(NecromancerEffects.AbsorbPhysical(owner, 0.25) == 0 && NecromancerEffects.Armor256(owner) == remaining - 64,
            "Actual earlier-build armor did not resume exact quarter-point absorption");
        pool = NecromancerEffects.Profiles.Find(owner).BoneArmorPool;
        Require(pool is { Rank:null, Maximum256:null } && pool.Remaining256 == remaining - 64
            && NecromancerEffects.ManaBalance256(owner) == seed.Mana256 && owner.Hits == seed.OwnerLife,
            "Upgrading the pool invented rank/maximum or changed owner resources");
        File.WriteAllText(PathFor("necro-legacy-progress"), JsonSerializer.Serialize(new Progress((prior?.Generation ?? 0) + 1, remaining - 64)));
    }
}
