// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Sanctuary;

public static class PotionBelts
{
    public static void Configure() => CommandSystem.Register("PotionBeltTest", AccessLevel.GameMaster, TestKit);
    public static int Rows(PlayerMobile player) => PotionBeltCatalog.Belt(player.FindItemOnLayer(Layer.MiddleTorso))?.Rows ?? 1;
    public static PotionBeltStorage Storage(PlayerMobile player, bool create = false)
    {
        foreach (var item in player.Items)
        {
            if (item is PotionBeltStorage storage && !storage.Deleted)
            {
                return storage;
            }
        }
        if (!create || !player.Alive || !ClassCombat.Enabled(player))
        {
            return null;
        }
        var result = new PotionBeltStorage();
        player.AddItem(result);
        return result;
    }
    public static RestorationPotion Cell(PlayerMobile player, int cell) => cell is >= 0 and < BeltProtocol.Cells
        ? Storage(player)?.Items?.OfType<RestorationPotion>().FirstOrDefault(p => p.X == cell % 4 && p.Y == cell / 4) : null;
    public static bool Active(PlayerMobile player, RestorationPotion potion) => potion.Parent == Storage(player)
        && potion.X is >= 0 and < 4 && potion.Y >= 0 && potion.Y < Rows(player);
    public static bool Ready(PlayerMobile player) => !player.Deleted && player.Alive && ClassCombat.Enabled(player);
    private static bool InBackpack(PlayerMobile player, Item item) => player.Backpack != null && item.IsChildOf(player.Backpack);
    public static bool Load(PlayerMobile player, int cell, RestorationPotion potion)
    {
        if (!Ready(player) || player.Holding != null || cell < 0 || cell >= Rows(player) * 4 || Cell(player, cell) != null
            || potion is not { Deleted: false, Movable: true } || !InBackpack(player, potion) || potion.Amount < 1)
        {
            return false;
        }
        RestorationPotion unit = potion;
        if (potion.Amount > 1)
        {
            unit = new RestorationPotion(potion.Kind, potion.Tier);
            potion.Dupe(unit); // Includes the source origin; never creates an extra unit.
            unit.Amount = 1;
            potion.Amount--;
        }
        unit.Stackable = false;
        Storage(player, true).AddItem(unit);
        unit.Location = new Point3D(cell % 4, cell / 4, 0);
        return true;
    }
    public static bool Remove(PlayerMobile player, int cell)
    {
        if (!Ready(player) || player.Holding != null || Cell(player, cell) is not { } potion || player.Backpack == null)
        {
            return false;
        }
        potion.Stackable = true;
        if (!player.Backpack.TryDropItem(player, potion, true))
        {
            potion.Stackable = false;
            return false;
        }
        Compact(player, cell % 4);
        return true;
    }
    public static void Compact(PlayerMobile player, int column)
    {
        int next = 0;
        // Inactive overflow never refills an active column: that would bypass capacity.
        for (int row = 0; row < Rows(player); row++)
        {
            if (Cell(player, row * 4 + column) is { } potion)
            {
                potion.Location = new Point3D(column, next++, 0);
            }
        }
    }
    public static void Reconcile(PlayerMobile player)
    {
        if (!Ready(player) || player.Holding != null)
        {
            return;
        }
        for (int cell = Rows(player) * 4; cell < BeltProtocol.Cells; cell++)
        {
            // A full pack leaves clearly marked, unusable overflow until it can be returned.
            if (Cell(player, cell) is { } potion && player.Backpack != null)
            {
                potion.Stackable = true;
                if (!player.Backpack.TryDropItem(player, potion, false))
                {
                    potion.Stackable = false;
                }
            }
        }
    }
    public static bool Use(PlayerMobile player, int cell)
    {
        if (!Ready(player) || cell < 0 || cell >= Rows(player) * 4 || Cell(player, cell) is not { } potion)
        {
            return false;
        }
        bool result = PotionRestoration.Drink(player, potion);
        if (result)
        {
            Compact(player, cell % 4);
        }
        return result;
    }
    public static void Fill(PlayerMobile player)
    {
        if (!Ready(player) || player.Holding != null || player.Backpack == null)
        {
            return;
        }
        var potions = new System.Collections.Generic.List<RestorationPotion>();
        foreach (var potion in player.Backpack.FindItemsByType<RestorationPotion>(true))
        {
            potions.Add(potion);
        }
        for (int column = 0; column < 4; column++)
        {
            var kind = Cell(player, column)?.Kind ?? (column < 2 ? RestorationKind.Healing : RestorationKind.Mana);
            for (int row = 0; row < Rows(player); row++)
            {
                int cell = row * 4 + column;
                if (Cell(player, cell) != null)
                {
                    continue;
                }
                var source = potions.FirstOrDefault(p => !p.Deleted && p.Amount > 0 && p.Kind == kind && InBackpack(player, p));
                if (source == null)
                {
                    break;
                }
                Load(player, cell, source);
            }
        }
    }
    public static void Target(PlayerMobile player, int cell)
    {
        if (Ready(player) && player.Target == null && cell >= 0 && cell < Rows(player) * 4 && Cell(player, cell) == null)
        {
            player.SendMessage("Choose a healing or mana potion in your backpack.");
            player.Target = new LoadTarget(cell);
        }
    }
    private sealed class LoadTarget(int cell) : Target(-1, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from is PlayerMobile player)
            {
                if (targeted is not RestorationPotion potion || !Load(player, cell, potion))
                {
                    player.SendMessage("Load a restoration potion from your own backpack into an empty active slot.");
                }
                PotionBeltNetwork.Send(player);
            }
        }
    }
    public static void OnDeath(PlayerMobile player, Container corpse)
    {
        PotionRestoration.Clear(player);
        if (Storage(player)?.Items == null || corpse == null)
        {
            return;
        }
        foreach (var potion in Storage(player).Items.ToArray())
        {
            potion.Stackable = true;
            corpse.DropItem(potion);
        }
    }
    private static void TestKit(CommandEventArgs args)
    {
        if (args.Mobile is not PlayerMobile player || !Ready(player) || player.Backpack == null)
        {
            return;
        }
        // Explicit GM-only comparison kit; never grant items on login or rewrite a live loadout.
        foreach (var item in new Item[] { new PotionSash(), new PotionLightBelt(), new PotionBelt(), new PotionHeavyBelt(), new PotionPlatedBelt(),
            new RestorationPotion(RestorationKind.Healing, 1) { Amount = 20 }, new RestorationPotion(RestorationKind.Mana, 1) { Amount = 20 } })
        {
            if (!player.Backpack.TryDropItem(player, item, true))
            {
                item.Delete();
            }
        }
        player.SendMessage("Belt comparison kit added. Equip a tabard, then use Fill in the potion panel. Heavy belts retain their strength requirements.");
    }
}
