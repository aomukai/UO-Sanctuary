// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal static class CompanionCharacterStats
{
    private static readonly CombatAbility[] _iolo = [CombatAbility.MagicArrow, CombatAbility.ColdArrow, CombatAbility.GuidedArrow];
    private static readonly CombatAbility[] _wolfgang = [CombatAbility.IronSkin, CombatAbility.Shout, CombatAbility.SwordMastery, CombatAbility.HolyShield];
    private static readonly CombatAbility[] _celeste = [CombatAbility.HolyShield, CombatAbility.Charge, CombatAbility.Smite, CombatAbility.Conviction, CombatAbility.Zeal];
    private static readonly CombatAbility[] _merydwen = [CombatAbility.GuidedArrow, CombatAbility.InnerSight, CombatAbility.Strafe, CombatAbility.Dodge, CombatAbility.Avoid, CombatAbility.Evade];
    private static readonly CombatAbility[] _nightwind = [CombatAbility.GuidedArrow, CombatAbility.MultipleShot, CombatAbility.Dodge, CombatAbility.Avoid, CombatAbility.Evade];

    internal static int[] Snapshot(BaseCreature companion, IClassCompanion build, int level)
    {
        var weapon = companion.Weapon as BaseWeapon;
        var mastery = companion is Wolfgang ? Wolfgang.SwordMastery(weapon, level) : null;
        var (minimum, maximum, factor) = weapon is null or Fists || ClassGear.Broken(weapon)
            ? (0, 0, 0)
            : CompanionBuild.WeaponDamage(companion, weapon, level, (int)(mastery?.Eval("passivecalc2") ?? 0));
        int attack = CompanionBuild.Bonus(companion, "att%") + (int)(mastery?.Eval("passivecalc1") ?? 0);

        return
        [
            level, companion.Str + CompanionBuild.Bonus(companion, "str"), companion.Dex + CompanionBuild.Bonus(companion, "dex"),
            companion.Hits, companion.HitsMax, minimum * factor / 100, maximum * factor / 100,
            ClassStats.AttackRating(companion, attack), ClassStats.Defense(companion),
            CompanionBuild.AttackSpeedBonus(companion), CompanionBuild.Recovery(companion, build.CombatClass),
            companion.FireResistance, companion.ColdResistance, companion.PoisonResistance, companion.EnergyResistance,
            companion switch { Wolfgang wolfgang => wolfgang.BlockChance, Celeste celeste => celeste.BlockChance, _ => 0 },
            companion is Merydwen ? AmazonAvoidance.CompanionChance(CombatAbility.Dodge, level) : 0,
            companion is Merydwen ? AmazonAvoidance.CompanionChance(CombatAbility.Avoid, level) : 0,
            companion is Merydwen ? AmazonAvoidance.CompanionChance(CombatAbility.Evade, level) : 0
        ];
    }

    internal static CompanionSkillStat[] Skills(BaseCreature companion, int level)
    {
        var abilities = companion switch
        {
            Iolo => _iolo,
            Wolfgang => _wolfgang,
            Celeste { GearRecovered: true } => _celeste,
            Nightwind => _nightwind,
            Merydwen => _merydwen,
            _ => []
        };
        var result = new CompanionSkillStat[abilities.Length];
        for (int i = 0; i < abilities.Length; i++)
        {
            var ability = abilities[i];
            var values = companion is Iolo ? Iolo.ShotValues(ability, level) : CompanionBuild.Values(ability, level);
            result[i] = SkillStat(ability, values, level);
        }
        return result;
    }

    private static CompanionSkillStat SkillStat(CombatAbility ability, SkillValues values, int level)
    {
        int rank = values.Rank;
        if (rank == 0)
            return new(ability, 0, Iolo.ShotUnlockLevel(ability));

        int Eval(string field) => (int)values.Eval(field);
        int Duration() => Eval("auralencalc") * ActionTiming.FrameMs;
        return ability switch
        {
            CombatAbility.IronSkin => new(ability, rank, Eval("passivecalc1")),
            CombatAbility.Shout => new(ability, rank, Eval("aurastatcalc1"), Duration()),
            CombatAbility.SwordMastery => new(ability, rank, Eval("passivecalc1"), Eval("passivecalc2"), Eval("passivecalc3")),
            CombatAbility.HolyShield => new(ability, rank, HolyShields.DefensePercent(values), HolyShields.BlockBonus(values)),
            CombatAbility.Charge => new(ability, rank, (int)values.Formula("toht"), Eval("calc1")),
            CombatAbility.Smite => new(ability, rank, Eval("calc1"), Eval("calc2") * ActionTiming.FrameMs),
            CombatAbility.Conviction => new(ability, rank, Eval("aurastatcalc1"), Eval("aurastatcalc2")),
            CombatAbility.Zeal => new(ability, rank, Eval("calc1"), (int)values.Formula("toht"), Eval("calc2")),
            CombatAbility.GuidedArrow => new(ability, rank, Eval("calc1")),
            CombatAbility.InnerSight => new(ability, rank, -Eval("aurastatcalc1"), Duration()),
            CombatAbility.Strafe => new(ability, rank, Eval("calc2"), AmazonStrafe.Count(values, 2, false), Eval("calc1")),
            CombatAbility.MultipleShot => new(ability, rank, 1 + rank),
            CombatAbility.Dodge or CombatAbility.Avoid or CombatAbility.Evade => new(ability, rank, AmazonAvoidance.CompanionChance(ability, level)),
            CombatAbility.MagicArrow => new(ability, rank, Iolo.ArrowConversion(ability, rank), values.PhysicalDamage256(false) / 256, values.PhysicalDamage256(true) / 256),
            CombatAbility.ColdArrow => new(ability, rank, Iolo.ArrowConversion(ability, rank), values.Damage256(false) / 256, values.Damage256(true) / 256),
            _ => new(ability, rank)
        };
    }

    internal static void SendVitals(NetState net, BaseCreature companion)
    {
        const int length = CompanionStatProtocol.VitalsLength;
        var writer = new SpanWriter(stackalloc byte[length]);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write(CompanionStatProtocol.SubCommand);
        writer.Write(CompanionStatProtocol.VitalsVersion);
        writer.Write(companion.Serial.Value);
        writer.Write(companion is IScriptedSpeaker speaker ? speaker.Portrait : (ushort)0);
        writer.Write(companion.Alive && !companion.IsDeadPet ? companion.Hits : 0);
        writer.Write(companion.HitsMax);
        writer.Write(companion.Mana);
        writer.Write(companion.ManaMax);
        writer.Write(companion.Stam);
        writer.Write(companion.StamMax);
        net.Send(writer.Span);
    }

    internal static void Send(NetState net, BaseCreature companion, IClassCompanion build, int level, byte version)
    {
        bool includeSkills = version == CompanionStatProtocol.Version;
        var skills = includeSkills ? Skills(companion, level) : [];
        int length = CompanionStatProtocol.SnapshotLength + (includeSkills ? 1 + skills.Length * CompanionStatProtocol.SkillLength : 0);
        var writer = new SpanWriter(stackalloc byte[length]);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write(CompanionStatProtocol.SubCommand);
        writer.Write(version);
        writer.Write(companion.Serial.Value);
        writer.Write((byte)build.EquipmentStyle);
        foreach (int value in Snapshot(companion, build, level))
            writer.Write(value);
        if (includeSkills)
        {
            writer.Write((byte)skills.Length);
            foreach (var skill in skills)
            {
                writer.Write((byte)skill.Ability);
                writer.Write((byte)skill.Rank);
                writer.Write(skill.First);
                writer.Write(skill.Second);
                writer.Write(skill.Third);
            }
        }
        net.Send(writer.Span);
    }
}
