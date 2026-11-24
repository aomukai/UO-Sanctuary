// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Sanctuary;

internal static class TalentDetails
{
    internal static string For(SkillDefinition skill)
    {
        if (skill == null)
        {
            return "";
        }
        var ability = skill.Ability;
        bool implemented = NecromancerCombat.Implemented(ability) || SorceressCombat.Implemented(ability)
            || PaladinCombat.Implemented(ability) || BarbarianCombat.Implemented(ability) || AmazonCombat.Implemented(ability)
            || AssassinCombat.Implemented(ability) || DruidCombat.Implemented(ability) || WarlockCombat.Implemented(ability);
        if (!implemented)
        {
            return "Coming later.";
        }

        string detail = skill.Passive ? "Passive"
            : ability == CombatAbility.BindDemon ? "Hold to bind • 9 mana per second"
            : ability == CombatAbility.SummonTainted ? "Stats apply to newly summoned succubi."
            : PaladinCombat.IsAura(ability) ? ability == CombatAbility.Prayer ? "Right-slot aura • Costs mana to heal"
                : "Right-slot aura • No mana cost"
            : "";
        string milestones = ability switch
        {
            CombatAbility.RaiseSkeleton => "5 hard points · Sword\n10 hard points · Shield and Thorns (34 physical)\n20 hard points · Armour and Extra Fast",
            CombatAbility.RaiseSkeletalMage => "5 hard points · Hat\n10 hard points · Staff. Cold, fire and lightning mages can cast Glacial Spike, Meteor or Chain Lightning\n20 hard points · Robe. Cold gains Frozen Armor, fire casts Enchant, lightning can cast Static Field",
            CombatAbility.ClayGolem => "10 hard points   Stone Skin\n20 hard points   Magic Resistant",
            CombatAbility.BloodGolem => "10 hard points   Open Wounds on every eligible hit\n20 hard points   5% chance to cast Life Tap on hit",
            CombatAbility.IronGolem => "10 hard points   Extra Strong\n20 hard points   Spectral Hit",
            CombatAbility.FireGolem => "10 hard points   Cannot Be Frozen\n20 hard points   At least 100% cold resistance",
            CombatAbility.SummonSpiritWolf => "10 hard points   Open Wounds on every eligible hit\n20 hard points   At least 100% cold resistance",
            CombatAbility.SummonDireWolf => "10 hard points   Extra Fast\n20 hard points   Extra Strong",
            CombatAbility.SummonGrizzly => "10 hard points   Can stun on hit\n20 hard points   10% chance of Crushing Blow",
            _ => ""
        };
        if (milestones.Length == 0)
        {
            return detail;
        }
        return $"{detail}\nNew summon bonuses\n{milestones}".TrimStart();
    }

    internal static string Preview(PlayerMobile player, SkillDefinition skill, IReadOnlyDictionary<string, int> draft,
        IReadOnlyDictionary<string, int> next)
    {
        if (skill == null)
        {
            return "";
        }

        var current = Values(player, skill, draft);
        var increased = next == null ? null : Values(player, skill, next);
        var lines = new List<string>();

        void Add(string label, Func<SkillValues, string> measure, bool availableAtZero = false)
        {
            string Read(SkillValues v) => v.Rank > 0 || availableAtZero ? measure(v) : "—";
            lines.Add($"{label}\t{Read(current)}" + (increased == null ? "" : $"\t{Read(increased)}"));
        }

        int Hard(IReadOnlyDictionary<string, int> pending) =>
            ClassCreation.Profiles.GetRank(player.Serial, skill.TalentId) + pending.GetValueOrDefault(skill.TalentId);
        if (current.Rank != Hard(draft) || increased != null && increased.Rank != Hard(next))
            Add("Effective", v => v.Rank.ToString(), true);
        var ability = skill.Ability;
        if (ability is CombatAbility.RaiseSkeleton or CombatAbility.SkeletonMastery)
        {
            var summon = SkillMechanics.For(CombatAbility.RaiseSkeleton);
            int Rank(SkillValues v, string key) => (int)v.Formula($"skill('{key}'.lvl)");
            int SummonRank(SkillValues v) => Rank(v, summon.SourceKey);
            int level = ClassStats.Level(player);
            var difficulty = SummonDifficulties.For(player);
            var births = new Dictionary<(int, int), SkeletonBirthStats>();
            SkeletonBirthStats Birth(SkillValues v)
            {
                int rank = SummonRank(v), mastery = Rank(v, "Skeleton Mastery");
                if (!births.TryGetValue((rank, mastery), out var birth))
                {
                    births[(rank, mastery)] = birth = NecromancerSkeletonBirth.Warrior(level, rank, mastery, difficulty);
                }
                return birth;
            }
            Add("Skeletons", v => SummonRank(v) == 0 ? "0"
                : NecromancerSkeletons.Limit(player, CombatAbility.RaiseSkeleton, SummonRank(v)).ToString(), true);
            Add("Life each", v => SummonRank(v) == 0 ? "—" : $"{Birth(v).Life256 / 256.0:0.#}", true);
            Add("Damage", v =>
            {
                if (SummonRank(v) == 0)
                    return "—";
                var damage = NecromancerSkeletonBirth.Damage(Birth(v), difficulty);
                return $"{damage.Min}–{damage.Max}";
            }, true);
            Add("Attack", v => SummonRank(v) == 0 ? "—" : Birth(v).Attack.ToString(), true);
            Add("Defense", v => SummonRank(v) == 0 ? "—" : Birth(v).Armor.ToString(), true);
            if (ability == CombatAbility.RaiseSkeleton)
                Add("Mana", v => (5 + v.Rank).ToString());
            return string.Join('\n', lines);
        }

        string passive = ability switch
        {
            CombatAbility.CriticalStrike => "Crit chance",
            CombatAbility.Penetrate => "Attack",
            CombatAbility.Pierce => "Pierce chance",
            CombatAbility.Warmth => "Mana regen",
            CombatAbility.FireMastery => "Fire damage",
            CombatAbility.LightningMastery => "Lightning",
            CombatAbility.ColdMastery => "Cold pierce",
            CombatAbility.IncreasedStamina => "Stamina",
            CombatAbility.IncreasedSpeed => "Move speed",
            CombatAbility.IronSkin => "Defense",
            CombatAbility.NaturalResistance => "Elem. resist",
            _ => null
        };
        if (passive != null)
            Add(passive, v => $"{v.Eval("passivecalc1")}%");
        if (ability is CombatAbility.RaiseSkeletalMage or CombatAbility.Revive)
            Add("Summon limit", v => NecromancerArmyLimits.ForRank(ability, v.Rank).ToString());
        if (ability == CombatAbility.SummonTainted)
        {
            var currentBirth = current.Rank > 0 ? TaintedPrepared.Birth(player, current) : null;
            var nextBirth = increased is { Rank: > 0 } ? TaintedPrepared.Birth(player, increased) : null;
            TaintedBirth Birth(SkillValues values) => values == current ? currentBirth : nextBirth;

            Add("Life", v => ((Birth(v).MaximumLife256 + 255) / 256).ToString());
            Add("Fireball dmg", v =>
            {
                var damage = TaintedNpcSkills.FireBall(Birth(v).Suppliers);
                return $"{damage.Minimum256 / 256.0:0.#}–{damage.Maximum256 / 256.0:0.#}";
            });
            Add("Defense", v => Birth(v).ProjectedArmor.ToString());
            Add("Fire resist aura", v => $"+{TaintedNpcSkills.ResistFire(Birth(v).Suppliers).Percent}%");
        }
        if (ability is CombatAbility.FrozenArmor or CombatAbility.ShiverArmor or CombatAbility.ChillingArmor
            or CombatAbility.Shout or CombatAbility.Defiance)
            Add("Defense", v => $"{v.Eval("aurastatcalc1")}%");
        if (ability is CombatAbility.AmplifyDamage or CombatAbility.Decrepify)
            Add("Phys. resist", v => $"-{Math.Abs(v.Param(5))}%");
        if (ability == CombatAbility.LowerResist)
            Add("Elem. resist", v => $"-{v.Diminishing(5, 6)}%");
        if (ability == CombatAbility.LifeTap)
            Add("Life return", v => $"{v.Linear(5, 6)}%");
        if (ability is CombatAbility.Might or CombatAbility.Concentration)
            Add("Phys. dmg", v => $"{v.Eval("aurastatcalc1")}%");
        if (ability == CombatAbility.BlessedAim)
            Add("Attack", v => $"{v.Eval("aurastatcalc1")}%");

        if (ability == CombatAbility.EnergyShield)
            Add("Absorbed", v => $"{v.Eval("calc1")}%");
        if (ability is CombatAbility.FrozenArmor or CombatAbility.ShiverArmor or CombatAbility.ChillingArmor
            or CombatAbility.Shout or CombatAbility.EnergyShield)
            Add("Duration", v => $"{v.Eval("auralencalc") / 25.0:0.#}s");
        if (ability is CombatAbility.FireBolt or CombatAbility.Fireball or CombatAbility.Meteor or CombatAbility.FireWall
            or CombatAbility.IceBolt or CombatAbility.IceBlast or CombatAbility.GlacialSpike or CombatAbility.FrostNova
            or CombatAbility.ChargedBolt or CombatAbility.Lightning or CombatAbility.ChainLightning or CombatAbility.Nova
            or CombatAbility.FireBlast or CombatAbility.FlameWave)
            Add("Base dmg", v => $"{v.Damage(false):0.#}–{v.Damage(true):0.#}");
        if (!skill.Passive && ability is not (CombatAbility.Teeth or CombatAbility.BindDemon)
            && (skill.Number("mana") > 0 || skill.Number("lvlmana") > 0))
        {
            Add(ability == CombatAbility.Prayer ? "Pulse mana" : "Base mana", v => $"{v.Mana256 / 256.0:0.##}");
        }
        return string.Join('\n', lines);
    }

    private static SkillValues Values(PlayerMobile player, SkillDefinition skill, IReadOnlyDictionary<string, int> draft)
    {
        int Hard(string key)
        {
            var definition = SkillMechanics.Source(key);
            return ClassCreation.Profiles.GetRank(player.Serial, definition.TalentId) + draft.GetValueOrDefault(definition.TalentId);
        }
        int Effective(string key)
        {
            var definition = SkillMechanics.Source(key);
            return definition.Ability == CombatAbility.Teeth ? ClassCombat.TeethRank(player, Hard(key))
                : NamedSkills.Rank(player, definition.TalentId, Hard(key));
        }
        return new SkillValues(skill, Effective, Hard, ClassStats.Level(player));
    }
}
