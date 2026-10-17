// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;

namespace Sanctuary;

public enum MonsterArchetype { Tank, Melee, Ranged, Caster }
public readonly record struct StatBand(int Min, int Max)
{
    public int Roll(Random random) => random.Next(Min, checked(Max + 1));
    public bool Valid => Min > 0 && Max >= Min;
}
public sealed record MonsterLevel(MonsterArchetype Archetype, string SourceBand, int Level,
    string Reference, StatBand Life, StatBand AttackRating, StatBand Defense, int MinDamage, int MaxDamage,
    int AttackMs, int EffectMs, int RecoveryMs, int MoveMs, int Range, string DamageType, int CriticalChance, int Experience)
{
    public MonsterRoll Roll(Random random) => new(this, Life.Roll(random), AttackRating.Roll(random), Defense.Roll(random));
}
public sealed record MonsterRoll(MonsterLevel Basis, int Life, int AttackRating, int Defense);

public static class MonsterBalance
{
    private static readonly Dictionary<(MonsterArchetype, int), MonsterLevel> _levels = new();
    public static IReadOnlyCollection<MonsterLevel> All => _levels.Values;
    public static MonsterLevel For(MonsterArchetype archetype, int level)
        => _levels.TryGetValue((archetype, level), out var result) ? result
            : throw new ArgumentOutOfRangeException(nameof(level), "A defined archetype and level 1–99 are required.");

    public static void Load()
    {
        _levels.Clear();
        foreach (var row in BalanceCatalog.Read<MonsterLevel>("monsters"))
        {
            if (!Enum.IsDefined(row.Archetype) || row.SourceBand is not ("Normal" or "Nightmare" or "Hell") || row.Level is < 1 or > 99
                || !row.Life.Valid || !row.AttackRating.Valid || !row.Defense.Valid
                || row.MinDamage < 1 || row.MaxDamage < row.MinDamage || row.Experience < 1
                || row.AttackMs < 40 || row.EffectMs < 40 || row.EffectMs >= row.AttackMs
                || row.RecoveryMs < 0 || row.MoveMs < 40 || row.Range is < 1 or > 18
                || row.CriticalChance is < 0 or > 100 || row.DamageType is not ("Physical" or "Fire"))
            {
                throw new InvalidDataException("Invalid normal monster archetype row.");
            }
            _levels.Add((row.Archetype, row.Level), row);
        }
        if (_levels.Count != 4 * 99)
        {
            throw new InvalidDataException("Players-1 monster archetypes must cover levels 1–99.");
        }
        foreach (var row in _levels.Values)
        {
            if (row.Level == 99)
            {
                continue;
            }
            var next = For(row.Archetype, row.Level + 1);
            if (row.Life.Max >= next.Life.Min || row.AttackRating.Max > next.AttackRating.Min
                || row.Defense.Max > next.Defense.Min || row.MinDamage > next.MinDamage || row.MaxDamage > next.MaxDamage
                || row.Experience > next.Experience || row.AttackMs != next.AttackMs || row.RecoveryMs != next.RecoveryMs)
            {
                throw new InvalidDataException("Monster variance crossed an adjacent level boundary.");
            }
        }
    }
}
