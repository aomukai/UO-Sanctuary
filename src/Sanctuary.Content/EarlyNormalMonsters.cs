// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

public readonly record struct MonsterMelee(int MinDamage, int MaxDamage, int Frames, int Speed, int ActionFrame)
{
    // D2MOO SUnit::sub_6FCBCE70: 8-bit animation counters, 25 game frames/sec.
    public int DurationMs => (Frames * 256 + Speed - 1) / Speed * 40;
    public int EffectMs => (ActionFrame * 256 + Speed - 1) / Speed * 40;
}

public sealed record MonsterApproach(int Chance, int AwarenessRange, int RetryMs, bool Wander);

public sealed record NormalMonsterStats(int MaxLife, MonsterMelee Attack1, MonsterMelee Attack2,
    int AttackChance, int Attack1Chance, int AiDelayFrames, int StallFrames, int CriticalChance, int DamageRegen, int Experience, MonsterApproach Approach)
{
    public int AiDelayMs => AiDelayFrames * 40;
    public int StallMs => StallFrames * 40;
}

public static class EarlyNormalMonsters
{
    // Vanilla 1.13 MonStats/MonLvl/MonStats2 and legacy AnimData.d2.
    // Skeleton uses BaseW=1hs (SKA11HS/SKA21HS); zombie uses hth (ZMA1HTH/ZMA2HTH).
    // Full source records and derivation: docs/early-normal-monsters.md.
    public static readonly NormalMonsterStats Skeleton = new(11,
        new(1, 3, 16, 224, 10), new(1, 3, 16, 224, 9), 75, 75, 15, 15, 5, 2, 34, new(60, 0, 600, false));
    public static readonly NormalMonsterStats Zombie = new(12,
        new(1, 3, 16, 256, 9), new(2, 3, 16, 256, 9), 100, 20, 15, 0, 5, 2, 33, new(30, 10, 600, true));
}
