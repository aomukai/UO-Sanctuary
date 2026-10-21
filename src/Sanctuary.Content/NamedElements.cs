// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class NamedElements
{
    private static int Bonus(Mobile owner, string code) => owner is PlayerMobile player && ClassCombat.Enabled(player)
        ? ClassGear.Bonus(player, code) : 0;
    public static int MagicResistance(Mobile target) => target is PlayerMobile
        ? Math.Clamp(Bonus(target, "res-mag"), -100, 75)
        : target is BaseCreature creature ? CreatureProfiles.For(creature).Resistances.Magic ?? 0 : 0;
    public static bool Attack(Mobile owner) => Bonus(owner, "extra-fire") != 0 || Bonus(owner, "extra-cold") != 0
        || Bonus(owner, "extra-ltng") != 0 || Bonus(owner, "extra-pois") != 0 || Bonus(owner, "extra-mag") != 0 || Bonus(owner, "pierce-mag") != 0
        || Bonus(owner, "pierce-fire") != 0 || Bonus(owner, "pierce-cold") != 0
        || Bonus(owner, "pierce-ltng") != 0 || Bonus(owner, "pierce-pois") != 0;
    public static bool Defense(Mobile target) => Bonus(target, "abs-fire") != 0 || Bonus(target, "abs-fire%") != 0
        || Bonus(target, "abs-cold") != 0 || Bonus(target, "abs-cold%") != 0
        || Bonus(target, "abs-ltng") != 0 || Bonus(target, "abs-ltng%") != 0;
    public static int Resistance(Mobile source, Mobile target, string element, int resistance)
    {
        // Ordinary item pierce cannot break a monster's immunity.
        if (target is not BaseCreature || resistance < 100)
        { resistance -= Math.Max(0, Bonus(source, "pierce-" + element)); }
        return Math.Clamp(resistance, -100, 200);
    }
    public static double Damage(Mobile source, Mobile target, double damage, string element, int? resistance = null)
    {
        if (damage <= 0) { return 0; }
        damage *= Math.Max(0, 100 + Bonus(source, "extra-" + element)) / 100.0;
        int resist = resistance ?? element switch
        {
            "fire" => target.FireResistance, "cold" => target.ColdResistance, "ltng" => target.EnergyResistance,
            "pois" => target.PoisonResistance, "mag" => MagicResistance(target), _ => target.PhysicalResistance
        };
        resist = Resistance(source, target, element, resist);
        int reduction = element == "pois" ? 0 : element == "physical"
            ? GemCombat.PhysicalReduction(target) : GemCombat.Reduction(target);
        double value = Math.Max(0, damage - reduction) * Math.Clamp(100 - resist, 0, 200) / 100.0;
        if (element is "fire" or "cold" or "ltng")
        {
            double percent = value * Math.Clamp(Bonus(target, "abs-" + element + "%"), 0, 40) / 100.0;
            double flat = Math.Min(Math.Max(0, value - percent), Math.Max(0, Bonus(target, "abs-" + element)));
            double absorbed = percent + flat;
            if (absorbed > 0 && target.Alive && !target.Deleted)
            { target.Hits = (int)Math.Min(target.HitsMax, target.Hits + Math.Floor(absorbed)); }
            value -= absorbed;
        }
        return Math.Max(0, value);
    }
}
