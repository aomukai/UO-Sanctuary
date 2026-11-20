// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class ClassEquipment
{
    public const string NecromancerDaggerName = "Necromancer Starting Dagger";
    public const string TeethProperty = "Teeth +1 (Necromancer only)";

    public static void Configure()
    {
        ClassCombatHooks.WeaponSpeedProperties = CombatTiming.AddSpeedProperties;
        ClassCombatHooks.CreatureTooltipProperties = CreatureProfiles.AddImmunityProperties;
        ClassCombatHooks.DaggerProperties = (dagger, list) =>
        {
            if (StarterClass(dagger) is { } characterClass)
                list.Add($"{ClassAbilities.Label(StarterAbility(characterClass))} +1 ({DiabloClasses.DisplayName(characterClass, false)} only)");
        };
    }

    public static void ProtectNewbieGear(PlayerMobile mobile)
    {
        foreach (var item in mobile.Items)
        {
            if (item.Movable && item.Layer is not (Layer.Bank or Layer.Hair or Layer.FacialHair) && item is not Gold)
            {
                item.LootType = LootType.Blessed;
            }
        }
        if (mobile.Backpack != null)
        {
            foreach (var item in mobile.Backpack.FindItems(true))
            {
                if (item is not Gold)
                {
                    item.LootType = LootType.Blessed;
                }
            }
        }
    }

    private sealed record StarterMigration(uint Owner, string Name, byte Class, uint[] Items);
    public static void Initialize()
    {
        var file = Path.Join(Core.BaseDirectory, "Configuration", "sanctuary-starter-items.json");
        if (!File.Exists(file))
        {
            return;
        }
        var migration = JsonSerializer.Deserialize<StarterMigration>(File.ReadAllText(file));
        if (World.FindMobile((Serial)migration.Owner) is not PlayerMobile owner || !string.Equals(owner.Name, migration.Name, StringComparison.Ordinal)
            || !ClassCreation.Profiles.TryGet(owner.Serial, out var profile) || (byte)profile.Class != migration.Class)
        {
            return;
        }
        foreach (uint serial in migration.Items)
        {
            if (World.FindItem((Serial)serial) is not { Deleted: false } item || item is Gold)
            {
                continue;
            }
            item.LootType = LootType.Blessed;
            // Restore the already-dead character as if starter protection had applied at death.
            if (item.RootParent is Server.Items.Corpse corpse && corpse.Owner == owner && owner.Backpack != null)
            {
                owner.Backpack.DropItem(item);
            }
        }
    }

    public static CombatAbility StarterAbility(DiabloClass characterClass) => characterClass switch
    {
        DiabloClass.Amazon => CombatAbility.InnerSight,
        DiabloClass.Sorceress => CombatAbility.FireBolt,
        DiabloClass.Necromancer => CombatAbility.Teeth,
        DiabloClass.Paladin => CombatAbility.Sacrifice,
        DiabloClass.Barbarian => CombatAbility.Bash,
        DiabloClass.Druid => CombatAbility.Firestorm,
        DiabloClass.Assassin => CombatAbility.FireBlast,
        DiabloClass.Warlock => CombatAbility.MiasmaBolt,
        _ => CombatAbility.None
    };

    public static DiabloClass? StarterClass(Item item) => item is Dagger && ClassGear.State(item) is { Starter: true } state
        ? state.StarterTeeth ? DiabloClass.Necromancer : state.StarterClass
        : null;

    public static bool IsStarter(Item item) => StarterClass(item).HasValue;
    public static void Prepare(PlayerMobile mobile, bool creating = false)
    {
        if (mobile == null || !ClassCreation.Profiles.TryGet(mobile.Serial, out var profile))
        {
            return;
        }
        if (creating || profile.Class != DiabloClass.Necromancer)
        {
            if (mobile.Backpack == null || !ClassGear.Profiles.BeginStarterMigration(mobile.Serial))
                return;

            static bool Original(Dagger dagger) => dagger?.LootType == LootType.Blessed && dagger.Name == null
                && (ClassGear.State(dagger) is not { } state || state.Roll.Rarity == GearRarity.Normal && !state.Starter
                    && state.SocketCount == 0 && !state.CraftedEquipment);
            var equipped = mobile.FindItemOnLayer(Layer.OneHanded) as Dagger;
            var dagger = Original(equipped) ? equipped : null;
            if (dagger == null)
            {
                foreach (var item in mobile.Backpack.FindItems(true))
                {
                    if (item is Dagger candidate && Original(candidate))
                    {
                        dagger = candidate;
                        break;
                    }
                }
            }

            if (dagger == null)
            {
                dagger = new Dagger { LootType = LootType.Blessed };
                mobile.Backpack.DropItem(dagger);
            }
            ClassGear.Profiles.MarkStarter(dagger, profile.Class);
            dagger.Name = profile.Class == DiabloClass.Necromancer ? NecromancerDaggerName
                : $"{DiabloClasses.DisplayName(profile.Class, mobile.Female)} Starting Dagger";
            return;
        }
        if (!ClassGear.Profiles.BeginStarterMigration(mobile.Serial))
        {
            return;
        }
        // Only the already-named, protected starter is eligible for legacy migration.
        void Migrate(Item item)
        {
            if (item is Dagger dagger && dagger.Name == NecromancerDaggerName && dagger.LootType == LootType.Blessed
                && ClassGear.State(dagger) == null)
            {
                ClassGear.Profiles.MarkStarter(dagger);
            }
        }
        foreach (var item in mobile.Items)
        {
            Migrate(item);
        }
        if (mobile.Backpack != null)
        {
            foreach (var item in mobile.Backpack.FindItems(true))
            {
                Migrate(item);
            }
        }
    }
}
