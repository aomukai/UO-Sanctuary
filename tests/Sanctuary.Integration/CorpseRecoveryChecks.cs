using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CorpseRecoveryChecks
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
        foreach (int level in new[] { 1, 10, 20, 99 })
        {
            var player = new PlayerMobile { Body = 0x190, Player = true, Young = true, Name = "RecoveryCheck" };
            var other = new PlayerMobile { Body = 0x190, Player = true };
            var weapon = new Dagger();
            var bag = new Bag();
            var heavy = new Dagger { Weight = 1000 };
            Corpse corpse = null;
            try
            {
                ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
                ClassCreation.Profiles.Assign(other, DiabloClass.Barbarian);
                Require(!player.Young, "Class creation retained Young death protection");
                if (level > 1)
                {
                    ClassCreation.Profiles.AdvanceLevel(player, level);
                }
                var profile = GetProfile(player);
                player.AddItem(new Backpack());
                player.Backpack.DropItem(new Gold(600));
                bag.DropItem(new Gold(400));
                player.Backpack.DropItem(bag);
                player.BankBox.DropItem(new Gold(10000));
                Require(player.EquipItem(weapon), "Recovery setup could not equip original dagger");
                var floor = CombatPractice.FindFloor(Map.Trammel, 1367, 1504);
                player.MoveToWorld(floor, Map.Trammel);
                other.MoveToWorld(floor, Map.Trammel);
                player.Kill();
                corpse = player.Corpse as Corpse;
                Require(corpse != null && weapon.Parent == corpse && bag.Parent == corpse && player.Location == floor,
                    "Death protection retained gear or teleported the ghost");
                int expected = 800;
                Require(corpse.GetAmount(typeof(Gold)) == expected && player.BankBox.GetAmount(typeof(Gold)) == 10000,
                    "Death gold penalty did not use flat 20% or touched the bank");
                Require(GetProfile(player) == profile, "Death changed class XP, level or other progression");
                Require(!ClassDeath.Recover(player, corpse), "Ghost recovered its own corpse");
                player.Resurrect();
                Require(!ClassDeath.Recover(other, corpse) && weapon.Parent == corpse, "Another player recovered the corpse");
                other.MoveToWorld(new Point3D(floor.X+1,floor.Y,floor.Z),Map.Trammel);
                player.MoveToWorld(new Point3D(floor.X + 5, floor.Y, floor.Z), Map.Trammel);
                Require(!ClassDeath.Recover(player, corpse), "Corpse recovery worked remotely");
                player.MoveToWorld(floor, Map.Trammel);
                corpse.DropItem(heavy);
                int capacity = player.Backpack.MaxItems;
                player.Backpack.DropItem(new Bandage());
                player.Backpack.MaxItems = player.Backpack.TotalItems;
                Require(ClassDeath.Recover(player, corpse) && weapon.Parent == player && bag.Parent == corpse
                    && heavy.Parent == corpse && player.FindItemOnLayer(Layer.OuterTorso) is not DeathRobe,
                    "Full backpack blocked re-equipping, retained the temporary robe or lost leftover items");
                player.Backpack.MaxItems = capacity;
                Require(ClassDeath.Recover(player, corpse) && bag.Parent == player.Backpack && heavy.Parent == corpse,
                    "Repeated recovery did not collect what now fits");
                heavy.Weight = 1;
                Require(ClassDeath.Recover(player, corpse) && heavy.Parent == player.Backpack && !ClassLoot.HasContents(corpse),
                    "A final retry did not recover the remaining item");
                Require(player.Backpack.GetAmount(typeof(Gold)) == expected && player.FindItemOnLayer(Layer.OneHanded) == weapon,
                    "Recovered gold or original equipment was wrong");
                Require(ClassDeath.Recover(player, corpse) && player.Backpack.GetAmount(typeof(Gold)) == expected,
                    "Repeated empty-corpse clicks duplicated gold");
            }
            finally
            {
                corpse?.Delete();
                player.Delete();
                other.Delete();
                weapon.Delete();
                bag.Delete();
                heavy.Delete();
                ClassCreation.Profiles.PruneDeleted();
            }
        }
    }
    private static CharacterProfile GetProfile(PlayerMobile player)
    {
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        return profile;
    }
}
