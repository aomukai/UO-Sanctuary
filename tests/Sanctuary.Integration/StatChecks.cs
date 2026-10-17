using System;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;
using Server.Misc;

namespace Sanctuary.Integration;

public static class StatChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    public static void Check()
    {
        var profiles = ClassCreation.Profiles;
        foreach (DiabloClass characterClass in Enum.GetValues<DiabloClass>())
        {
            var player = new PlayerMobile { Body = 0x190, Player = true, AccessLevel = AccessLevel.Player };
            try
            {
                var basis = ClassStats.For(characterClass);
                profiles.Assign(player, characterClass);
                Require(ClassStats.Vitality(player) == basis.Vitality && profiles.AvailableStatPoints(player.Serial) == 0,
                    "Starting attributes or points are wrong");
                Require(!profiles.TrySpendStat(player, CharacterStat.Vitality), "Level 1 spent an unearned point");
                profiles.AdvanceLevel(player, 3);
                var snapshot = ClassStats.Snapshot(player);
                Require(snapshot.Length == StatProtocol.Values && snapshot[(int)StatField.Points] == 10
                    && snapshot[(int)StatField.TalentPoints] == 2,
                    "Progression notification snapshot omitted earned stat or talent points");
                Require(profiles.AvailableStatPoints(player.Serial) == 10 && player.Hits == player.HitsMax, "Skipped level rewards are wrong");
                player.Hits -= 10;
                for (int i = 0; i < 4; i++)
                {
                    Require(profiles.TrySpendStat(player, CharacterStat.Vitality), "Could not spend Vitality");
                }
                Require(player.HitsMax == basis.Life + basis.LifeLevel4 * 2 / 4 + basis.LifeVitality4
                    && player.Hits == player.HitsMax - 10, "Vitality scaling healed prior damage or lost fractions");
                for (int i = 0; i < 4; i++)
                {
                    profiles.TrySpendStat(player, CharacterStat.Energy);
                }
                Require(player.ManaMax == basis.Mana + (basis.ManaLevel4 * 2 + basis.ManaEnergy4 * 4) / 4,
                    "Class Energy scaling is wrong");
                var rating = ClassStats.AttackRating(player);
                profiles.TrySpendStat(player, CharacterStat.Dexterity);
                Require(ClassStats.AttackRating(player) == rating + 5, "Dexterity did not improve Attack Rating");
                profiles.TrySpendStat(player, CharacterStat.Strength);
                Require(profiles.AvailableStatPoints(player.Serial) == 0 && !profiles.TrySpendStat(player, CharacterStat.Strength),
                    "Point spending overdrew the budget");
                Require(!profiles.TrySpendStat(player, (CharacterStat)255), "Invalid attribute accepted");
                profiles.AdvanceLevel(player, 99);
                while (profiles.AvailableStatPoints(player.Serial) > 0)
                {
                    profiles.TrySpendStat(player, CharacterStat.Strength);
                }
                Require(player.Str > 150, "UO's 150 stat cap still applies to class characters");
                var stats = (player.RawStr, player.RawDex, player.RawInt);
                player.StrLock = player.DexLock = player.IntLock = StatLockType.Up;
                SkillCheck.GainStat(player, Stat.Str);
                SkillCheck.GainStat(player, Stat.Dex);
                SkillCheck.GainStat(player, Stat.Int);
                Require(stats == (player.RawStr, player.RawDex, player.RawInt), "UO skill use awarded unearned attributes");
            }
            finally
            {
                player.Delete();
                profiles.PruneDeleted();
            }
        }
        Require(ClassStats.HitChance(1, 100000, 1, 99) == 0.05
            && ClassStats.HitChance(100000, 0, 99, 1) == 0.95, "Hit chance caps are wrong");
        Require(Math.Abs(ClassStats.HitChance(100, 100, 1, 1) - 0.5) < 0.001,
            "Equal ratings/levels do not give 50% hit chance");
        var account = new Account("stats-save", Guid.NewGuid().ToString("N"));
        var saved = new PlayerMobile { Body = 0x190, Name = "StatsSave", Player = true };
        account[0] = saved;
        profiles.Assign(saved, DiabloClass.Assassin);
        profiles.AdvanceLevel(saved, 3);
        for (int i = 0; i < 4; i++)
        {
            profiles.TrySpendStat(saved, CharacterStat.Energy);
            profiles.TrySpendStat(saved, CharacterStat.Vitality);
        }
        var dagger = new Dagger();
        var bow = new Bow();
        try
        {
            saved.EquipItem(dagger);
            var target = new Skeleton();
            try
            {
                using (var misses = new CombatRandom(0.99))
                {
                    Require(ClassStats.CheckHit(saved, target) == false, "Physical hit check ignored the miss roll");
                }
                using (var hits = new CombatRandom(0))
                {
                    Require(ClassStats.CheckHit(saved, target) == true, "Physical hit check ignored the hit roll");
                }
            }
            finally
            {
                target.Delete();
            }
            var original = ClassStats.DamageRange(saved);
            saved.Str += 100;
            Require(ClassStats.DamageRange(saved).Min > original.Min, "Strength did not increase dagger damage");
            saved.RemoveItem(dagger);
            saved.Str = 100;
            saved.EquipItem(bow);
            var bowDamage = ClassStats.DamageRange(saved);
            saved.Str += 100;
            Require(ClassStats.DamageRange(saved) == bowDamage, "Strength increased bow damage");
            saved.Dex += 100;
            Require(ClassStats.DamageRange(saved).Min > bowDamage.Min, "Dexterity did not increase bow damage");
            ClassStats.ApplyBase(saved, new CharacterProfile(DiabloClass.Assassin, 3, 0), profiles.Stats(saved.Serial));
            Require(saved.ManaMax == 35 && saved.HitsMax == 66 && saved.StamMax == 102,
                "Assassin quarter points were rounded before addition");
        }
        finally
        {
            dagger.Delete();
            bow.Delete();
        }
    }
    public static void Reload()
    {
        var player = (PlayerMobile)Accounts.GetAccount("stats-save")?[0];
        Require(player != null && ClassCreation.Profiles.AvailableStatPoints(player.Serial) == 2,
            "Unspent stat points did not survive restart");
        Require(ClassCreation.Profiles.Stats(player.Serial) == new StatAllocation(0, 0, 4, 4)
            && player.Int == 29 && ClassStats.Vitality(player) == 24
            && player.ManaMax == 35 && player.HitsMax == 66 && player.StamMax == 102,
            "Stat allocations or class resource pools changed on reload");
    }
}
