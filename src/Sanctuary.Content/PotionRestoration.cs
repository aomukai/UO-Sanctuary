// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Items;

namespace Sanctuary;

public static class PotionRestoration
{
    public static void Clear(Mobile player) => TimedEffects.ClearRestoration(player);
    public static bool CanUse(PlayerMobile player, RestorationPotion potion) => PotionBelts.Ready(player)
        && potion is { Deleted: false, Movable: true, Amount: > 0 } && player.Holding != potion
        && (PotionBelts.Active(player, potion) || player.Backpack != null && potion.IsChildOf(player.Backpack));
    public static int Amount(PlayerMobile player, RestorationBase basis)
    {
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        int halves = profile.Class switch
        {
            DiabloClass.Amazon or DiabloClass.Assassin or DiabloClass.Paladin => 3,
            DiabloClass.Barbarian => basis.Kind == RestorationKind.Healing ? 4 : 2,
            _ => basis.Kind == RestorationKind.Mana ? 4 : 2
        };
        return basis.Amount * halves / 2;
    }
    // Arreat Summit's Vitality/Energy probabilities, expressed as a fraction.
    public static double DoubleChance(int stat) => stat <= 2 ? 0 : stat > 200 ? 1 - 101.0 / stat
        : stat % 2 == 0 ? (stat - 2) / 400.0 : (stat - 1.0) * (stat - 1) / (400.0 * stat);
    public static bool Drink(PlayerMobile player, RestorationPotion potion) => Drink(player, potion, null);
    internal static bool Drink(PlayerMobile player, RestorationPotion potion, Random random)
    {
        if (!CanUse(player, potion)) { return false; }
        TimedEffects.Settle(player, TimedEffects.Now);
        if (!CanUse(player, potion)) { return false; }
        var row = PotionBeltCatalog.Potion(potion.Kind, potion.Tier);
        bool life = row.Kind is RestorationKind.Healing or RestorationKind.InstantHealing or RestorationKind.Rejuvenation;
        bool mana = row.Kind is RestorationKind.Mana or RestorationKind.InstantMana or RestorationKind.Rejuvenation;
        if (row.Kind != RestorationKind.CombatBuff && (!life || SkillHealth.Full(player)) && (!mana || player.Mana >= player.ManaMax)) { return false; }
        if (row.Kind == RestorationKind.CombatBuff)
        {
            if (!CombatPotions.Apply(player, row.Tier)) { return false; }
        }
        else if (row.DurationMs > 0)
        {
            int amount = Amount(player, row);
            int stat = life ? ClassStats.Vitality(player) : player.Int;
            if ((random?.NextDouble() ?? Utility.RandomDouble()) < DoubleChance(stat)) { amount *= 2; }
            if (!TimedEffects.AddRestoration(player, life, amount, row.DurationMs)) { return false; }
        }
        else
        {
            if (life) { SkillHealth.Heal(player, (int)Math.Min((long)player.HitsMax * row.Amount / 100 * 256, int.MaxValue)); }
            if (mana) { player.Mana = (int)Math.Min(player.ManaMax, player.Mana + (long)player.ManaMax * row.Amount / 100); }
            TimedEffects.DiscardFullRestoration(player);
        }
        int column = potion.X;
        bool inBelt = PotionBelts.Active(player, potion);
        var origin = potion.Origin;
        potion.Consume(1);
        if ((random?.NextDouble() ?? Utility.RandomDouble()) < 0.20)
        {
            var bottle = ItemOrigins.Create(origin, () => new Bottle());
            if (player.Backpack == null || !player.Backpack.TryDropItem(player, bottle, false))
            {
                bottle.Delete();
            }
        }
        if (inBelt)
        {
            PotionBelts.Compact(player, column);
        }
        player.RevealingAction();
        player.PlaySound(0x2D6);
        // No drinking animation, free-hand/cast gate, or combat action deadline changes.
        return true;
    }
}
