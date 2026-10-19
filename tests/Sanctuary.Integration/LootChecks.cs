using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class LootChecks
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
        var player = new PlayerMobile { Body = 0x190, Name = "LootCheck", Player = true, Str = 100 };
        var other = new PlayerMobile { Body = 0x190, Name = "OtherLoot", Player = true };
        var owner = new Skeleton { Frozen = true };
        var bird = new Chicken { Frozen = true };
        var corpse = new Corpse(owner, new List<Item>());
        var protectedCorpse = new Corpse(other, new List<Item>());
        var dagger = new Dagger();
        Corpse harvested = null;
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            player.AddItem(new Backpack());
            var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
            player.MoveToWorld(point, Map.Felucca);
            other.MoveToWorld(point, Map.Felucca);
            owner.MoveToWorld(point, Map.Felucca);
            corpse.Aggressors.Add(player);
            corpse.MoveToWorld(point, Map.Felucca);
            protectedCorpse.MoveToWorld(point, Map.Felucca);
            Require(ClassCombat.Enabled(player) && !ClassLoot.CanOpen(other, corpse), "Legacy character used class loot");
            Require(!ClassLoot.CanOpen(player, protectedCorpse), "AoE loot included a player corpse");
            Require(!ClassLoot.HasContents(corpse) && !corpse.Deleted, "Empty unharvested body was deleted");
            corpse.DropItem(dagger);
            corpse.MoveToWorld(new Point3D(point.X + 5, point.Y, point.Z), Map.Felucca);
            Require(ClassLoot.CanOpen(player, corpse) && ClassLoot.CanTake(player, corpse, dagger), "Five-tile loot reach failed");
            corpse.MoveToWorld(new Point3D(point.X + 6, point.Y, point.Z), Map.Felucca);
            Require(!ClassLoot.Take(player, dagger) && dagger.Parent == corpse, "Out-of-range item moved");
            corpse.MoveToWorld(new Point3D(point.X, point.Y, point.Z + 20), Map.Felucca);
            Require(!ClassLoot.Take(player, dagger), "Loot crossed floor height");
            corpse.MoveToWorld(point, Map.Felucca);
            var heavy = new Dagger { Weight = 1000 };
            corpse.DropItem(heavy);
            Require(!ClassLoot.Take(player, heavy) && heavy.Parent == corpse, "Overweight item left its corpse");
            heavy.Delete();
            var bag = new Bag();
            var nested = new Dagger();
            bag.DropItem(nested);
            corpse.DropItem(bag);
            Require(!ClassLoot.Take(player, nested), "Loot reached into an unopened nested container");
            Require(ClassLoot.Take(player, dagger) && dagger.Parent == player.Backpack, "Five-tile item transfer failed");
            Require(!ClassLoot.Take(player, dagger), "Repeated item request duplicated/moved a taken item");
            Require(ClassLoot.Take(player, bag) && !corpse.Deleted && !ClassLoot.HasContents(corpse), "Normal empty corpse did not survive");
            var fullItems = new Dagger();
            corpse.DropItem(fullItems);
            var limit = player.Backpack.MaxItems;
            player.Backpack.MaxItems = player.Backpack.TotalItems;
            Require(!ClassLoot.Take(player, fullItems) && fullItems.Parent == corpse, "Full item capacity lost corpse loot");
            player.Backpack.MaxItems = limit;
            fullItems.Delete();
            var gold = new Gold(25);
            corpse.DropItem(gold);
            owner.LastKiller = player;
            Require(gold.Parent == corpse && player.Backpack.GetAmount(typeof(Gold)) == 0, "Corpse gold moved automatically");
            Require(ClassLoot.Take(player, gold) && player.Backpack.GetAmount(typeof(Gold)) == 25, "Manual gold transfer failed");
            var full = new Dagger { Weight = 500 };
            player.Backpack.DropItem(full);
            var retainedGold = new Gold(25);
            corpse.DropItem(retainedGold);
            Require(!ClassLoot.Take(player, retainedGold), "Overweight gold was taken");
            Require(retainedGold.Parent == corpse && retainedGold.Amount == 25, "Failed manual gold transfer lost the stack");
            full.Delete();
            bird.MoveToWorld(point, Map.Felucca);
            bird.Hits = 1;
            bird.Damage(10, player);
            harvested = bird.Corpse as Corpse;
            Require(harvested != null && !harvested.Deleted, "Harvest test has no corpse");
            harvested.DropItem(new Dagger());
            harvested.Carve(player, dagger);
            Require(harvested.Carved && harvested.SanctuaryHarvested && !harvested.Deleted && ClassLoot.HasContents(harvested),
                "Harvest did not retain materials and equipment inside the corpse");
            var before = harvested.Items.Count;
            harvested.Carve(player, dagger);
            Require(harvested.Items.Count == before, "Repeated harvesting duplicated materials");
            var contents = harvested.Items.ToArray();
            foreach (var item in contents)
            {
                Require(ClassLoot.Take(player, item), $"Harvested item could not be looted: {item.GetType().Name}");
            }
            Require(harvested.Deleted, "Empty harvested body was not deleted");
            corpse.SanctuaryHarvested = true;
            Require(!corpse.Deleted && ClassLoot.HasContents(corpse), "Harvest flag deleted unclaimed equipment");
            Require(ClassLoot.Take(player, retainedGold) && corpse.Deleted, "Last harvested stack did not remove corpse");
        }
        finally
        {
            player.Delete();
            other.Delete();
            owner.Delete();
            bird.Delete();
            corpse.Delete();
            protectedCorpse.Delete();
            harvested?.Delete();
            dagger.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
}
