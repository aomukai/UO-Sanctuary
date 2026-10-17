using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class GearChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(GearChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "gear-checks.json");
    private sealed record Saved(uint[] Items, string[] States);
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static void Rejected(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
    private static GearState RollUntil(Item item, string code)
    {
        for (int seed = 0; seed < 2000; seed++)
        {
            var state = ClassGear.Profiles.FromMonster(item, 99, GearRarity.Magic, new Random(seed));
            if (state.Roll.Affixes.Any(a => a.Modifiers.Any(m => m.Code == code)))
            {
                return state;
            }
        }
        throw new InvalidOperationException($"No legal {code} affix found for {item.GetType().Name}");
    }
    public static void Check()
    {
        CheckLegacyStarter();
        var types = typeof(Dagger).Assembly.GetTypes();
        int checkedBases = 0;
        foreach (var definition in EquipmentBalance.All.Where(d => d.NormalDrop))
        {
            var type = types.Single(t => t.Name == definition.Type);
            var ctor = type.GetConstructors().First(c => c.GetParameters().All(p => p.HasDefaultValue));
            var item = (Item)ctor.Invoke(ctor.GetParameters().Select(p => p.DefaultValue).ToArray());
            try
            {
                var state = ClassGear.Profiles.Convert(item, 1, GearRarity.Normal, new Random(12));
                Require(state.Roll.Rarity == GearRarity.Normal && state.Roll.Affixes.Length == 0,
                    "A normal base received intrinsic UO magical properties");
                if (ClassGear.Slot(item) == GearSlot.Cosmetic)
                {
                    Require(!definition.AllowsAffixes && ClassGear.Defense(item) == 0
                        && ClassGear.Stat(item, ClassGearStat.StrengthRequirement) == 0,
                        $"Cosmetic base contributes stats: {definition.Type}");
                    Rejected(() => ClassGear.Profiles.Convert(item, 99, GearRarity.Magic), "Cosmetic equipment accepted magic rarity");
                    Rejected(() => ClassGear.Profiles.Convert(item, 99, GearRarity.Rare), "Cosmetic equipment accepted rare rarity");
                }
                else
                {
                    Require(state.BaseCode != null && ClassGear.Base(item).Tier == "Normal",
                        $"Normal conversion missing: {definition.Type}");
                    if (item is BaseWeapon weapon)
                    {
                        Require(weapon.MinDamage == ClassGear.Damage(item).Min && weapon.MaxDamage == ClassGear.Damage(item).Max
                            && weapon.MaxDamage > 0, $"Weapon still uses UO damage: {definition.Type}");
                        weapon.Attributes.BonusStr = 1000;
                        weapon.Attributes.WeaponDamage = 1000;
                        Require(weapon.Attributes.BonusStr == 0 && weapon.Attributes.WeaponDamage == 0,
                            "Native UO weapon attributes remain active on white gear");
                    }
                }
                using var list = new ObjectPropertyList(item);
                item.GetProperties(list);
                list.Terminate();
                checkedBases++;
            }
            finally
            {
                item.Delete();
            }
        }
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "ConvertedGear" };
        var account = new Account("gear-save", Guid.NewGuid().ToString("N"));
        account[0] = player;
        player.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        ClassCreation.Profiles.AdvanceLevel(player, 99);
        player.Str = player.Dex = 200;
        var ordinary = new Dagger();
        var starter = new Dagger { LootType = LootType.Blessed };
        var gorget = new PlateGorget();
        var arms = new PlateArms();
        var legs = new PlateLegs();
        var gloves = new LeatherGloves();
        var boots = new Boots();
        var ring = new GoldRing();
        try
        {
            Require(ordinary.MinDamage == 1 && ordinary.MaxDamage == 4 && ordinary.StrRequirement == 0,
                "A converted dagger is not the D2 normal 1–4 base");
            Require(player.EquipItem(ordinary) && ClassCombat.TeethRank(player) == 0,
                "An ordinary looted dagger still grants Teeth");
            ordinary.Name = ClassEquipment.NecromancerDaggerName;
            ClassGear.Profiles.BeginStarterMigration(player.Serial);
            ClassEquipment.Prepare(player);
            Require(!ClassEquipment.IsStarter(ordinary), "Renaming an ordinary dagger forged a starter grant");
            player.RemoveItem(ordinary);
            ClassGear.Profiles.MarkStarter(starter);
            starter.Name = "Renamed real starter";
            Require(player.EquipItem(starter) && ClassCombat.TeethRank(player) == 1,
                "Explicit starter identity did not grant Teeth after rename");
            using (var list = new ObjectPropertyList(ordinary))
            {
                ordinary.GetProperties(list);
                list.Terminate();
                Require(list.Buffer.AsSpan().IndexOf(Encoding.Unicode.GetBytes(ClassEquipment.TeethProperty)) < 0,
                    "An ordinary dagger advertises the starter skill grant");
            }
            int strength = player.Str;
            int defense = ClassStats.Defense(player);
            gorget.Attributes.BonusStr = 1000;
            gorget.PhysicalBonus = 1000;
            Require(player.EquipItem(gorget) && player.EquipItem(arms) && player.EquipItem(legs),
                "Could not equip cosmetic fixtures");
            ClassGear.Refresh(player);
            Require(player.Str == strength && ClassStats.Defense(player) == defense && player.PhysicalResistance == 0
                && EquipmentBalance.RunPenalty(player) == 0 && gorget.ArmorRating == 0,
                "Extra UO layers increased power or added armour penalties");
            gorget.Delete(); // Necklaces share the native UO neck layer.
            CheckEffects(player);
            var gloveState = RollUntil(gloves, "swing2");
            Require(player.EquipItem(gloves), "Could not equip legal magic gloves");
            ClassGear.Refresh(player);
            int ias = ClassGear.Bonus(gloves, "swing2");
            Require(ias > 0 && CombatTiming.Ias(player) == ias && gloves.Attributes.WeaponSpeed == ias,
                "A legal glove IAS roll did not affect the equipped attack speed");
            foreach (var affix in AffixBalance.All.Where(a => a.Modifiers.Any(m => m.Code.StartsWith("swing", StringComparison.Ordinal))))
            {
                Require(!AffixBalance.Eligible(affix, "boot", 99, GearRarity.Magic), "IAS is legal on boots");
            }
            var bootState = RollUntil(boots, "move3");
            Require(player.EquipItem(boots), "Could not equip legal speed boots");
            ClassGear.Refresh(player);
            Require(ClassGear.RunBonus(player) > 0, "A legal boot speed roll did not affect running");
            var ringState = RollUntil(ring, "cast1");
            Require(player.EquipItem(ring), "Could not equip a legal FCR ring");
            ClassGear.Refresh(player);
            Require(CombatTiming.Fcr(player) > 0 && starter.Attributes.CastSpeed == 0,
                "Legal FCR was missing or leaked onto the starter weapon");
            var rare = new Dagger();
            var rareState = ClassGear.Profiles.FromMonster(rare, 40, GearRarity.Rare, new Random(44));
            Require(rareState.Roll.ItemLevel == 40
                && rareState.Roll.AffixLevel == AffixBalance.Level(40, ClassGear.Base(rare).QualityLevel)
                && rareState.Roll.Affixes.All(a => a.Definition.Rare && a.Definition.Level <= rareState.Roll.AffixLevel),
                "Monster level did not determine legal rare stat levels");
            player.Backpack.DropItem(rare);
            Rejected(() => ClassGear.Profiles.FromMonster(ordinary, 0, GearRarity.Magic), "Invalid monster level accepted");
            var persisted = new Item[] { starter, rare, gloves, boots, ring };
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(persisted.Select(i => i.Serial.Value).ToArray(),
                persisted.Select(i => JsonSerializer.Serialize(ClassGear.State(i))).ToArray())));
            CheckNoDrops(player);
        }
        finally
        {
            ordinary.Delete();
            gorget.Delete();
            arms.Delete();
            legs.Delete();
        }
        _logger.Information("gear-check: passed; normal bases={Count}; cosmetic slots, starter identity, legal IAS/FCR/FRW, levels and no drops", checkedBases);
    }
    private static void CheckLegacyStarter()
    {
        var player = new PlayerMobile { Player = true, Body = 0x190 };
        player.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        var starter = new Dagger { Name = ClassEquipment.NecromancerDaggerName, LootType = LootType.Blessed };
        var ordinary = new Dagger { Name = ClassEquipment.NecromancerDaggerName };
        player.Backpack.DropItem(starter);
        player.Backpack.DropItem(ordinary);
        try
        {
            ClassEquipment.Prepare(player);
            Require(ClassEquipment.IsStarter(starter) && !ClassEquipment.IsStarter(ordinary),
                "Legacy migration lost the blessed starter or granted Teeth to renamed loot");
            var later = new Dagger { Name = ClassEquipment.NecromancerDaggerName, LootType = LootType.Blessed };
            player.Backpack.DropItem(later);
            ClassEquipment.Prepare(player);
            Require(!ClassEquipment.IsStarter(later), "Starter migration could be repeated");
        }
        finally
        {
            player.Delete();
        }
    }
    private static void CheckEffects(PlayerMobile player)
    {
        var amulet = new GoldNecklace();
        var ring = new GoldRing();
        var heavy = new PlateChest();
        int strength = player.Str, life = player.HitsMax;
        try
        {
            RollUntil(amulet, "str");
            Require(player.EquipItem(amulet), "Could not equip attribute amulet");
            ClassGear.Refresh(player);
            Require(player.Str == strength + ClassGear.Bonus(amulet, "str"),
                "Legal Strength roll was ignored or applied twice");
            amulet.Delete();
            ClassGear.Refresh(player);
            Require(player.Str == strength, "Unequipped Strength roll remained active");
            RollUntil(ring, "hp");
            Require(player.EquipItem(ring), "Could not equip life ring");
            ClassGear.Refresh(player);
            Require(player.HitsMax == life + ClassGear.Bonus(ring, "hp"), "Legal life roll did not affect maximum life");
            ring.Delete();
            ClassGear.Refresh(player);
            Require(player.HitsMax == life, "Unequipped life roll remained active");
            player.Str = 15;
            Require(ClassGear.CanEquip(heavy, player) == false, "D2 base Strength requirement was not enforced");
            player.Str = strength;
            var low = new PlayerMobile { Player = true, Body = 0x190 };
            try
            {
                ClassCreation.Profiles.Assign(low, DiabloClass.Necromancer);
                RollUntil(amulet = new GoldNecklace(), "str");
                Require(ClassGear.RequiredLevel(amulet) > 1 && ClassGear.CanEquip(amulet, low) == false,
                    "Affix character-level requirement was not enforced");
            }
            finally
            {
                low.Delete();
            }
        }
        finally
        {
            amulet.Delete();
            ring.Delete();
            heavy.Delete();
            player.Str = strength;
            ClassGear.Refresh(player);
        }
    }
    private static void CheckNoDrops(PlayerMobile player)
    {
        var point = CombatPractice.FindFloor(Map.Trammel, 1440, 1695);
        foreach (var creature in new BaseCreature[] { new GraveyardZombie(), new GraveyardSkeleton(), new Skeleton(), new Zombie() })
        {
            creature.MoveToWorld(point, Map.Trammel);
            creature.AIObject.AITimer.Stop();
            var generated = new Gold(50);
            creature.PackItem(generated);
            Require(generated.Deleted, "A monster accepted generated death loot");
            creature.AddItem(new Backpack());
            creature.Backpack.DropItem(new Gold(100));
            creature.Kill();
            Require(creature.Corpse is Corpse corpse && !ClassLoot.HasContents(corpse),
                "An existing monster inventory produced death drops");
            creature.Corpse?.Delete();
            creature.Delete();
        }
        var pet = new Cow { Controlled = true, ControlMaster = player };
        pet.MoveToWorld(point, Map.Trammel);
        var retained = new Gold(23);
        pet.PackItem(retained);
        pet.Kill();
        Require(!retained.Deleted && retained.IsChildOf(pet.Corpse), "Disabling monster drops destroyed pet-owned items");
        pet.Corpse?.Delete();
        pet.Delete();
    }
    public static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        for (int i = 0; i < saved.Items.Length; i++)
        {
            var item = World.FindItem((Serial)saved.Items[i]);
            Require(item != null && JsonSerializer.Serialize(ClassGear.State(item)) == saved.States[i],
                "Saved base/affix rolls or starter identity changed on reload");
            GearProfiles.Validate(item, ClassGear.State(item));
        }
        var player = (PlayerMobile)Accounts.GetAccount("gear-save")[0];
        Require(ClassCombat.TeethRank(player) == 1 && CombatTiming.Ias(player) > 0 && CombatTiming.Fcr(player) > 0,
            "Saved equipped gear effects did not return after load");
        _logger.Information("gear-reload-check: passed");
    }
}
