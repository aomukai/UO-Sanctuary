// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

// Solo expansion XP: D2 level-difference adjustment, then Experience.txt /1024.
public static class MonsterExperience
{
    private static readonly int[] _ratios =
    [976, 928, 880, 832, 784, 736, 688, 640, 592, 544, 496, 448, 400, 352, 304,
        256, 192, 144, 108, 81, 61, 46, 35, 26, 20, 15, 11, 8, 6];

    public static int Ratio1024(int playerLevel)
    {
        if (playerLevel is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(playerLevel));
        }
        return playerLevel == 99 ? 0 : playerLevel < 70 ? 1024 : _ratios[playerLevel - 70];
    }

    public static int Award(int baseExperience, int monsterLevel, int playerLevel)
    {
        if (baseExperience < 0 || monsterLevel is < 1 or > 99 || playerLevel is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(baseExperience));
        }
        long xp = baseExperience;
        int difference = monsterLevel - playerLevel;
        if (difference < -5)
        {
            int percent = difference switch { -6 => 81, -7 => 62, -8 => 43, -9 => 24, _ => 5 };
            xp = xp * percent / 100;
        }
        else if (difference > 0 && playerLevel >= 25)
        {
            xp = xp * playerLevel / monsterLevel;
        }
        else if (difference > 5)
        {
            int percent = difference switch { 6 => 88, 7 => 68, 8 => 36, 9 => 15, _ => 2 };
            xp = xp * percent / 100;
        }
        return (int)(xp * Ratio1024(playerLevel) / 1024);
    }
}
