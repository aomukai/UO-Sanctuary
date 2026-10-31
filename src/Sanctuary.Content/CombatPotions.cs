// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class CombatPotions
{
    private static readonly string[] _ids = ["second-wind", "deep-wounds", "magical-penetration", "stealing", "protection"];
    public static void Configure()
    {
        ClassCombatHooks.FinalDamage = (target, damage) => Math.Max(0, damage - Bonus(target, "protection"));
        ClassGearHooks.SpellResistedDamage = SpellDamage;
        ClassGearHooks.DirectSpellDamage = (target, source, damage) => damage + (int)Math.Floor(AddedElements(source, target, damage));
    }
    public static string Description(int tier) => tier switch
    {
        1 => "+30% running speed; walking and turning unchanged",
        2 => "+10 minimum and maximum physical weapon damage before enemy resistance",
        3 => "Spells add 10% of their original damage as each fire/cold/lightning/poison component",
        4 => "+7% physical-hit life and mana steal; normal drain and difficulty rules",
        5 => "Subtract 10 once per hit or poison tick after mitigation, down to zero",
        _ => throw new ArgumentOutOfRangeException(nameof(tier))
    };
    public static bool Apply(PlayerMobile owner, int tier, DateTime? at = null)
    {
        if (tier is < 1 or > 5) { return false; }
        var row = PotionBeltCatalog.Potion(RestorationKind.CombatBuff, tier);
        if (!TimedEffects.ApplyBuff(owner, TimedEffectCategory.Potion, _ids[tier - 1], row.Amount, at: at)) { return false; }
        StatNetwork.Send(owner); return true;
    }
    public static int Bonus(PlayerMobile owner, string id)=>BonusAt(owner,id,TimedEffects.DamageTime);
    internal static int BonusAt(PlayerMobile owner,string id,DateTime? at)=>owner!=null&&ClassCombat.Enabled(owner)
        &&TimedEffects.Buff(owner,TimedEffectCategory.Potion,at) is{} buff&&buff.Id==id?buff.Magnitude:0;
    public static int RunBonus(PlayerMobile owner) => ClassGear.RunBonus(owner) + Bonus(owner, "second-wind");
    private static double ElementAmount(Mobile source, double damage) => source is PlayerMobile p
        ? Math.Max(0, damage) * Bonus(p, "magical-penetration") / 100.0 : 0;
    private static double AddedElements(Mobile source, Mobile target, double damage)
    {
        double added = ElementAmount(source, damage);
        return added <= 0 ? 0 : NamedElements.Damage(source, target, added, "fire")
            + NamedElements.Damage(source, target, added, "cold")
            + NamedElements.Damage(source, target, added, "ltng")
            + NamedElements.Damage(source, target, added, "pois");
    }
    public static double TeethDamage(PlayerMobile source, Mobile target, double damage, int magicResistance) =>
        NamedElements.Damage(source, target, damage, "mag", target is PlayerMobile ? NamedElements.MagicResistance(target) : magicResistance)
            + AddedElements(source, target, damage);
    public static int? SpellDamage(Mobile target, Mobile source, int damage, int physical, int fire, int cold, int poison, int lightning)
    {
        double added = ElementAmount(source, damage);
        if (added <= 0 && !NamedElements.Attack(source) && !NamedElements.Defense(target)) { return null; }
        // Combine each original/added element before applying that element's flat reduction and resistance.
        double total = GemCombat.Component(target, damage * physical / 100.0, target.PhysicalResistance, false)
            + NamedElements.Damage(source, target, damage * fire / 100.0 + added, "fire")
            + NamedElements.Damage(source, target, damage * cold / 100.0 + added, "cold")
            + NamedElements.Damage(source, target, damage * lightning / 100.0 + added, "ltng")
            + NamedElements.Damage(source, target, damage * poison / 100.0 + added, "pois");
        return Math.Max(0, (int)Math.Floor(total));
    }
}
