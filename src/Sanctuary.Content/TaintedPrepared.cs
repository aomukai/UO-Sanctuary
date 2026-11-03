// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record TaintedSuppliers(int Rank, int LifePercent, int AttackSpeed, int VelocityPercent,
    int FireMasteryPercent, int DefenseBonus, int PhysicalSunder, int PhysicalResistBonus, int FireSunder,
    int PetMaximum, int Mana256, int DelayFrames, int ResistFireRank, int FireBallRank, int BloodBoilRank);
internal sealed record TaintedBirth(int OwnerLevel, int PetLevel, MonsterDifficulty Difficulty,
    int BaseLife256, int MaximumLife256, int BaseAttack, int LevelAttack, int BaseArmor, int LevelArmor,
    TaintedSuppliers Suppliers)
{
    internal int ProjectedAttack => checked(BaseAttack + LevelAttack);
    internal int ProjectedArmor => checked(BaseArmor + LevelArmor + Suppliers.DefenseBonus);
}
internal sealed record TaintedSnapshot(TaintedBirth Birth, int MasteryRank, int OathRank, int HardMastery, int HardBloodBoil);
internal sealed record TaintedFireBallPacket(int Minimum256, int Maximum256, int Radius);
internal sealed record TaintedFireResistance(int Rank, int Percent, int Radius, int PeriodFrames);

// Numerical capture only. The common do56/GenericPet controller, actual aura,
// projectile release and extended passive ownership still require integration.
// Nonzero inherited sunder fields are recorded, not installed or combined.
internal static class TaintedPrepared
{
    internal static TaintedSuppliers Capture(SkillValues values, Func<string, int> accrued)
    {
        ArgumentNullException.ThrowIfNull(values); ArgumentNullException.ThrowIfNull(accrued);
        var row = SkillMechanics.For(CombatAbility.SummonTainted);
        if (values.Ability != row.Ability || values.Rank is < 1 or > 255 || row.SourceId != 376
            || row.Number("srvdofunc") != 56 || row.Text("summon") != "warbighead" || row.Text("pettype") != "demon"
            || row.Text("summode") != "S1" || row.Text("aurastate") != "warlockdemonground"
            || row.Text("aurastat1") != "attackrate" || row.Text("passivestat1") != "velocitypercent"
            || row.Text("passivestat2") != "passive_fire_mastery" || row.Text("passivestat3") != "armorclass"
            || row.Text("passivestat4") != "item_pierce_damage_immunity" || row.Text("passivestat6") != "damageresist"
            || row.Text("passivestat7") != "item_pierce_fire_immunity"
            || row.Text("sumskill1") != "Tainted Resist Fire" || row.Text("sumskill2") != "Tainted Fire Ball"
            || row.Text("sumskill3") != "Blood Boil")
        { throw new NotSupportedException("Unsupported Tainted summon supplier route"); }
        int Eval(string field) => checked((int)values.Eval(field));
        return new(values.Rank, Eval("calc1"), Eval("aurastatcalc1"), Eval("passivecalc1"),
            Eval("passivecalc2"), Eval("passivecalc3"), checked((int)values.StatEval("passivecalc4", accrued)),
            Eval("passivecalc6"), checked((int)values.StatEval("passivecalc7", accrued)), Eval("petmax"),
            values.Mana256, row.Number("localdelay"), Eval("sumsk1calc"), Eval("sumsk2calc"), Eval("sumsk3calc"));
    }

    internal static TaintedBirth Birth(SkillValues values, MonsterDifficulty difficulty, int ownerLevel, Func<string, int> accrued)
    {
        if (ownerLevel is < 1 or > 99 || !Enum.IsDefined(difficulty))
        { throw new ArgumentOutOfRangeException(nameof(ownerLevel)); }
        var suppliers = Capture(values, accrued);
        int level = Math.Min(ownerLevel, values.Rank + 3 * ownerLevel / 4);
        int life = checked(NecromancerSummons.Source("warbighead", "minHP", difficulty) * 256);
        return new(ownerLevel, level, difficulty, life, checked(life + SourcePercentage.Calculate(life, suppliers.LifePercent, 100)),
            NecromancerSummons.Source("warbighead", "A1TH", difficulty), NecromancerSummons.Source("level-" + level, "L-TH", difficulty),
            NecromancerSummons.Source("warbighead", "AC", difficulty), NecromancerSummons.Source("level-" + level, "L-AC", difficulty), suppliers);
    }

    internal static TaintedSnapshot Capture(PlayerMobile owner)
    {
        if (owner is not { Alive: true, Deleted: false }
            || ClassCreation.Profiles.TryGet(owner.Serial, out var profile) != true || profile.Class != DiabloClass.Warlock
            || ClassCreation.Profiles.GetRank(owner.Serial, "warlock.summontainted") < 1)
        { throw new InvalidOperationException("Tainted needs a live learned Warlock owner"); }
        return new(Birth(SkillMechanics.Values(owner, CombatAbility.SummonTainted), SummonDifficulties.For(owner.Map), profile.Level,
            stat => stat switch
            {
                "item_pierce_damage_immunity" => ClassGear.Bonus(owner, "pierce-immunity-damage"),
                "item_pierce_fire_immunity" => ClassGear.Bonus(owner, "pierce-immunity-fire"),
                _ => throw new NotSupportedException("Unmapped Tainted inherited stat: " + stat)
            }), SkillMechanics.Rank(owner, CombatAbility.DemonicMastery), SkillMechanics.Rank(owner, CombatAbility.BloodOath),
            ClassCreation.Profiles.GetRank(owner.Serial, "warlock.demonicmastery"), ClassCreation.Profiles.GetRank(owner.Serial, "warlock.bloodboil"));
    }

    internal static TaintedSnapshot Rescale(TaintedSnapshot snapshot, MonsterDifficulty difficulty)
    {
        if (snapshot?.Birth?.Suppliers == null || !Enum.IsDefined(difficulty)
            || snapshot.MasteryRank is < 0 or > 255 || snapshot.OathRank is < 0 or > 255
            || snapshot.HardMastery is < 0 or > 20 || snapshot.HardBloodBoil is < 0 or > 20)
        { throw new ArgumentException("Invalid captured Tainted ranks"); }
        var original = snapshot.Birth;
        var values = new SkillValues(SkillMechanics.For(CombatAbility.SummonTainted),
            key => key == "Summon Tainted" ? original.Suppliers.Rank : key == "Demonic Mastery" ? snapshot.MasteryRank
                : key == "Blood Oath" ? snapshot.OathRank : 0,
            key => key == "Demonic Mastery" ? snapshot.HardMastery : key == "Blood Boil" ? snapshot.HardBloodBoil : 0, original.OwnerLevel);
        int Accrued(string stat) => stat switch
        {
            "item_pierce_damage_immunity" => original.Suppliers.PhysicalSunder,
            "item_pierce_fire_immunity" => original.Suppliers.FireSunder,
            _ => throw new NotSupportedException("Unmapped captured Tainted stat")
        };
        if (Birth(values, original.Difficulty, original.OwnerLevel, Accrued) != original)
        { throw new InvalidDataException("Inconsistent captured Tainted birth"); }
        return snapshot with { Birth = Birth(values, difficulty, original.OwnerLevel, Accrued) };
    }
}

internal static class TaintedNpcSkills
{
    private static readonly Lazy<SkillDefinition[]> Rows = new(() =>
    {
        var rows = BalanceCatalog.Read<SkillDefinition>("warlock-npc-skills")
            .Where(r => r.Ability == CombatAbility.SummonTainted).ToArray();
        if (rows.Length != 2 || rows[0].SourceId != 420 || rows[1].SourceId != 422
            || rows.Any(r => r.CharacterClass != DiabloClass.Warlock || r.Left || r.Right)
            || rows[0].SourceKey != "Tainted Resist Fire" || rows[0].Number("srvdofunc") != 65
            || rows[0].Text("aurastat1") != "fireresist" || rows[0].Number("perdelay") != 50
            || rows[0].Number("aurafilter") != 73731 || rows[0].Text("aurastate") != "resistfire"
            || rows[0].Text("auratargetstate") != "resistfire" || rows[0].Number("immediate") != 1
            || rows[0].Number("mana") != 0 || rows[0].Number("minmana") != 0 || rows[0].Number("lvlmana") != 0
            || rows[0].Text("aurastat2") != "0" || rows[0].Text("passivestate") != "passive_resistfire"
            || rows[1].SourceKey != "Tainted Fire Ball" || rows[1].Text("srvmissile") != "taintedfireball"
            || rows[1].Text("EType") != "fire" || rows[1].Number("SrcDam") != 0)
        { throw new InvalidDataException("Invalid Tainted NPC source rows"); }
        return rows;
    });

    private static SkillValues Values(int index, int rank, int hardBloodBoil)
    {
        if (rank is < 1 or > 255 || hardBloodBoil is < 0 or > 20) { throw new ArgumentOutOfRangeException(); }
        var row = Rows.Value[index];
        // This is the NPC's assigned Blood Boil rank from sumsk3calc, not
        // effective player Blood Boil, Sorceress synergies or owner equipment.
        return new(row, key => key == row.SourceKey ? rank : 0, key => key == "Blood Boil" ? hardBloodBoil : 0, 1);
    }

    internal static TaintedFireBallPacket FireBall(TaintedSuppliers suppliers)
    {
        ArgumentNullException.ThrowIfNull(suppliers);
        if (suppliers.FireMasteryPercent is < 0 or > 2550) { throw new ArgumentOutOfRangeException(nameof(suppliers)); }
        var values = Values(1, suppliers.FireBallRank, suppliers.BloodBoilRank);
        int Damage(bool maximum)
        {
            int basis = values.Damage256(maximum);
            // Elemental mastery uses DATATBLS_ApplyRatio, not the separate
            // monster percentage helper's large-value early division.
            return checked((int)(basis + (long)basis * suppliers.FireMasteryPercent / 100));
        }
        return new(Damage(false), Damage(true), checked((int)values.Eval("calc1")));
    }

    internal static TaintedFireResistance ResistFire(TaintedSuppliers suppliers)
    {
        ArgumentNullException.ThrowIfNull(suppliers);
        var values = Values(0, suppliers.ResistFireRank, 0);
        return new(values.Rank, checked((int)values.Eval("aurastatcalc1")), checked((int)values.Eval("aurarangecalc")), Rows.Value[0].Number("perdelay"));
    }
}
