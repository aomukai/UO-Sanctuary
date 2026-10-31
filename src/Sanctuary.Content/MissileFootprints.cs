// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Server;

namespace Sanctuary;

internal readonly record struct MissileFootprint(string Source, int Map, int X, int Y, int Z, DateTime UntilUtc);

// Read-only projections of current native missile actors onto the source0x40
// collision layer. It is deliberately independent of the actor's damage/AI.
// Native room-list order, moving subcell rounding and global timer order remain
// the same declared adapters as the contributing projectile controllers.
internal static class MissileFootprints
{
    private sealed record Transient(MissileFootprint Footprint, Func<bool> Active);
    private static readonly List<Transient> Transients = new();
    private static readonly Dictionary<string, int> Sizes = new(StringComparer.Ordinal);
    private static readonly string[] Sources = ["armageddonfire", "blaze", "erruption crack 1", "erruption crack 2", "firewall",
        "fistsoffirefirewall", "hydra", "immolationfire", "meteorfire", "moltenboulderfirepath", "plaguejavcloud",
        "poisonexplosioncloud", "royalstrikemeteorfire", "shock field on ground", "volcano"];
    // These rows are numerical reference inputs, with no world producer yet.
    // Keep the closed inventory without registering imaginary footprints.
    private static readonly string[] ReferenceOnlySources=["miasmaboltcloud","miasmaboltdot"];
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), Prune);
    private static int Cell(double coordinate) => checked((int)Math.Floor(coordinate * 2 + .5));
    internal static void ValidateSources()
    {
        if (Sizes.Count != 0) { return; }
        var actual = BalanceCatalog.Read<SummonSource>("necromancer-summons").Where(r => r.Fields.TryGetValue("Collision", out var c)
            && c.ValueKind == JsonValueKind.Number && c.GetInt32() != 0).ToArray();
        if (!actual.Select(r => r.Key).Order().SequenceEqual(Sources.Concat(ReferenceOnlySources).Order()))
        { throw new NotSupportedException("Unmapped missile footprint producer"); }
        var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in actual)
        {
            if (row.Fields["Collision"].GetInt32() != 1 || !row.Fields.TryGetValue("Size", out var size)
                || !size.TryGetInt32(out int value) || value is < 1 or > 3)
            { throw new NotSupportedException("Unmapped missile footprint size/mask"); }
            if(ReferenceOnlySources.Contains(row.Key))
            {
                int expected=row.Key=="miasmaboltcloud"?1:2;
                if(value!=expected || !row.Fields.TryGetValue("pSrvDoFunc",out var route) || route.GetInt32()!=41)
                {throw new NotSupportedException("Unmapped reference-only missile footprint row");}
            }
            else{sizes.Add(row.Key, value);}
        }
        foreach (var entry in sizes) { Sizes.Add(entry.Key, entry.Value); }
    }
    private static void Prune() => Transients.RemoveAll(x => !x.Active() || x.Footprint.UntilUtc <= TimedEffects.Now);
    internal static void RegisterTransient(MissileFootprint footprint, Func<bool> active)
    {
        ValidateSources();
        if (footprint.Source != "poisonexplosioncloud" || active == null || footprint.UntilUtc.Kind != DateTimeKind.Utc)
        { throw new NotSupportedException("Unsupported transient missile footprint"); }
        Prune(); Transients.Add(new(footprint, active));
    }
    internal static bool Covers(MissileFootprint footprint, int map, int x, int y, int z, DateTime at)
    {
        ValidateSources();
        if (!Sizes.TryGetValue(footprint.Source, out int size)) { throw new NotSupportedException("Unknown missile footprint"); }
        return footprint.Map == map && Math.Abs(footprint.Z - z) < 16 && footprint.UntilUtc > at
            && CombatHitboxes.SourceTouches(1, size, x - footprint.X, y - footprint.Y);
    }
    internal static MissileFootprint[] Snapshot(bool includeFissure = true)
    {
        ValidateSources();
        var result = new List<MissileFootprint>();
        foreach (var p in FireGround.Snapshot().Patches)
        {
            string source = p.Ability switch
            {
                CombatAbility.Blaze => "blaze", CombatAbility.FireWall => "firewall", CombatAbility.Meteor => "meteorfire",
                CombatAbility.ImmolationArrow => "immolationfire", CombatAbility.FistsOfFire => "fistsoffirefirewall",
                CombatAbility.PhoenixStrike => "royalstrikemeteorfire", _ => throw new NotSupportedException("Unmapped fire-field footprint")
            };
            result.Add(new(source, p.Map, Cell(p.X), Cell(p.Y), p.Z, p.UntilUtc));
        }
        foreach (var p in DruidMoltenBoulder.Snapshot().Missiles.Where(x => x.Missile == 455))
        { result.Add(new("moltenboulderfirepath", p.Map, p.CellX, p.CellY, p.Z, p.NextUtc.AddMilliseconds((p.Remaining - 1) * 40L))); }
        foreach (var p in PlagueJavelins.Snapshot())
        { result.Add(new("plaguejavcloud", p.Map, Cell(p.X), Cell(p.Y), p.Z, p.NextUtc.AddMilliseconds((p.Frames - p.Frame - 1) * 40L))); }
        foreach(var p in NecromancerSpellMissiles.Snapshot().Flights.Where(x=>x.Ability==CombatAbility.PoisonExplosion))
        {result.Add(new("poisonexplosioncloud",p.Map,Cell(p.X),Cell(p.Y),p.Z,p.NextUtc.AddMilliseconds((p.Frames-p.Frame-1)*40L)));}
        foreach (var p in ShockWebs.Snapshot().Shards.Where(x => x.Ground))
        { result.Add(new("shock field on ground", p.Map, Cell(p.X), Cell(p.Y), p.Z, p.NextUtc.AddMilliseconds((p.Remaining - 1) * 40L))); }
        foreach (var p in Hydras.Snapshot().Bolts)
        { result.Add(new("hydra", p.Map, Cell(p.X), Cell(p.Y), p.Z, p.UntilUtc)); }
        foreach(var p in DruidArmageddon.Snapshot().Actors.Where(x=>x.Missile==623))
        { result.Add(new("armageddonfire",p.Map,p.X,p.Y,p.Z,p.UntilUtc)); }
        foreach(var p in DruidVolcano.Snapshot().Actors.Where(x=>x.Missile==479))
        { result.Add(new("volcano",p.Map,Cell(p.X),Cell(p.Y),p.Z,p.UntilUtc)); }
        foreach (var p in Transients.Where(x => x.Active())) { result.Add(p.Footprint); }
        if (includeFissure)
        {
            foreach (var p in DruidFissure.Snapshot().Cracks)
            { result.Add(new("erruption crack 1", p.Map, p.X, p.Y, p.Z, p.UntilUtc)); }
        }
        return result.ToArray();
    }
    internal static bool OtherBlocked(int map, int x, int y, int z, DateTime at)
        => Snapshot(false).Any(p => Covers(p, map, x, y, z, at));
}
