// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class ClassGear
{
    private static readonly HashSet<Serial> _refresh = new();
    public static GearProfiles Profiles { get; private set; }
    private static readonly HashSet<string> _implemented = new(StringComparer.Ordinal)
    {
        "str", "dex", "enr", "vit", "hp", "mana", "stam", "att", "att%", "ac", "ac%",
        "dmg%", "dmg-min", "dmg-max", "res-all", "res-fire", "res-cold", "res-ltng", "res-pois",
        "swing1", "swing2", "swing3", "cast1", "cast2", "cast3", "move1", "move2", "move3", "block",
        "balance1", "balance2", "balance3", "gold%", "mag%"
    };
    public static bool Implemented(AffixDefinition affix)
    {
        foreach (var mod in affix.Modifiers)
        {
            if (mod.Mode != ModifierMode.Numeric || !_implemented.Contains(mod.Code))
            {
                return false;
            }
        }
        return true;
    }
    public static bool Converted(Item item) => item != null && EquipmentBalance.TryGet(item, out var definition)
        && definition.Category != "Unarmed";
    public static GearDefinition Definition(Item item) => NamedGear.Definition(item, State(item))
        ?? (EquipmentBalance.TryGet(item, out var definition) ? definition : null);
    public static bool CosmeticClothing(Item item) => Definition(item) is { Category: "Clothing" } definition
        && State(item)?.NamedItemId == null && definition.Slot != GearSlot.Belt
        && EquipmentCrafting.PhysicalMaterial(item.GetType()) != GearMaterial.Leather;
    public static bool Broken(Item item) => item switch
    {
        BaseWeapon weapon => weapon.HitPoints <= 0 && (weapon.MaxHitPoints > 0 || weapon.InitMaxHits > 0),
        BaseArmor armor => armor.HitPoints <= 0 && (armor.MaxHitPoints > 0 || armor.InitMaxHits > 0),
        BaseClothing clothing => clothing.HitPoints <= 0 && (clothing.MaxHitPoints > 0 || clothing.InitMaxHits > 0),
        BaseJewel jewel => jewel.HitPoints <= 0 && (jewel.MaxHitPoints > 0 || jewel.InitMaxHits > 0),
        _ => false
    };
    public static bool KeepBroken(Item item) => Converted(item) && item.RootParent is PlayerMobile player && ClassCombat.Enabled(player);
    public static GearState State(Item item) => Profiles?.Find(item.Serial);
    public static NormalBase Base(Item item)
    {
        var definition = Definition(item);
        return (State(item)?.BaseCode ?? definition?.BaseCode) is { } code ? EquipmentBalance.Base(code) : null;
    }
    public static GearSlot Slot(Item item)
    {
        var definition = Definition(item);
        if (definition == null || CosmeticClothing(item))
        {
            return GearSlot.Cosmetic;
        }
        // Body armour cannot acquire a second stat slot through a UO-only layer.
        if (item is BaseArmor && item is not BaseShield && item.Layer is Layer.Arms or Layer.Neck or Layer.Pants or Layer.InnerLegs or Layer.OuterLegs)
        {
            return GearSlot.Cosmetic;
        }
        return definition.Slot;
    }
    public static bool Active(PlayerMobile player, Item item)
    {
        var slot = Slot(item);
        if (item.Parent != player || slot == GearSlot.Cosmetic || Broken(item) || State(item) is { Identified: false })
        {
            return false;
        }
        if (slot == GearSlot.Shield)
        {
            foreach (var other in player.Items)
            {
                if (Slot(other) == GearSlot.Weapon && Base(other) is { TwoHanded: true, OneOrTwoHanded: false })
                {
                    return false;
                }
            }
        }
        foreach (var other in player.Items)
        {
            if (slot == GearSlot.Belt && other is PotionTabard && item is not PotionTabard)
            {
                return false;
            }
            if (other != item && Slot(other) == slot && other.Serial.Value < item.Serial.Value)
            {
                if (item is PotionTabard && other is not PotionTabard)
                {
                    continue;
                }
                return false;
            }
        }
        return true;
    }
    public static int Bonus(Item item, string code)
    {
        if (Slot(item) == GearSlot.Cosmetic || Broken(item) || State(item) is not { Identified: true } state)
        {
            return 0;
        }
        int value = GemSockets.Bonus(item, state, code) + NamedGear.Bonus(item, code);
        foreach (var affix in state.Roll.Affixes)
        {
            foreach (var mod in affix.Modifiers)
            {
                if (mod.Code == code)
                {
                    value += mod.Value ?? 0;
                }
            }
        }
        foreach (var rune in state.Runes) { if (rune.Pattern.EffectCode == code) { value += rune.Percentage; } }
        string materialCode = code switch
        {
            "res-fire" => "resistance-fire", "res-cold" => "resistance-cold", "res-ltng" => "resistance-lightning",
            "res-pois" => "resistance-poison", "res-physical" => "resistance-physical", _ => null
        };
        return value + (materialCode == null ? 0 : Material(item, materialCode));
    }
    public static int Material(Item item, string code)
    {
        if (item == null || Slot(item) == GearSlot.Cosmetic || Broken(item) || State(item) is not { } state)
        {
            return 0;
        }
        int value = 0;
        foreach (var roll in state.MaterialRolls)
        {
            if (roll.Code == code) { value += roll.Value; }
        }
        return value;
    }
    public static int Material(PlayerMobile player, string code)
    {
        int value = 0;
        foreach (var item in player.Items)
        {
            if (Active(player, item)) { value += Material(item, code); }
        }
        return value;
    }
    public static int Bonus(PlayerMobile player, string code)
    {
        int value = NamedGear.SetBonus(player, code);
        foreach (var item in player.Items)
        {
            if (Active(player, item))
            {
                value += Bonus(item, code);
            }
        }
        return value;
    }
    private static int Enhanced(Item item, string code)
    {
        int value = Bonus(item, code);
        // Rune percentages apply in every permitted host slot. Local affixes stay local.
        if (item.Parent is PlayerMobile player && Active(player, item))
        {
            if (code == "ac%") { value += NamedGear.SetBonus(player, code); }
            foreach (var other in player.Items)
            {
                if (other == item || !Active(player, other) || State(other) is not { } state) { continue; }
                foreach (var rune in state.Runes) { if (rune.Pattern.EffectCode == code) { value += rune.Percentage; } }
            }
        }
        return value;
    }
    public static int Defense(Item item)
    {
        if (Slot(item) == GearSlot.Cosmetic || Broken(item) || Base(item) is not { } basis)
        {
            return 0;
        }
        if (basis.MaxDefense <= 0) { return 0; }
        int enhanced = Enhanced(item, "ac%");
        int defense = enhanced > 0 ? basis.MaxDefense + 1 : State(item)?.Defense
            ?? basis.MinDefense + (int)(item.Serial.Value % (uint)(basis.MaxDefense - basis.MinDefense + 1));
        if (NamedGear.Ethereal(item)) { defense = defense * 3 / 2; }
        return defense * (100 + enhanced) / 100 + Bonus(item, "ac");
    }
    public static (int Min, int Max) Damage(Item item)
    {
        if (item == null || !Converted(item))
        {
            return (1, 2);
        }
        if (Slot(item) != GearSlot.Weapon || Broken(item) || Base(item) is not { } basis)
        {
            return (0, 0);
        }
        bool twoHanded = item.Layer == Layer.TwoHanded || basis.TwoHanded && !basis.OneOrTwoHanded;
        int min = twoHanded && basis.TwoHandMax > 0 ? basis.TwoHandMin : basis.MinDamage;
        int max = twoHanded && basis.TwoHandMax > 0 ? basis.TwoHandMax : basis.MaxDamage;
        if (Definition(item).Category == "Throwing" && basis.MissileMax > 0)
        {
            min = basis.MissileMin;
            max = basis.MissileMax;
        }
        int enhanced = Enhanced(item, "dmg%");
        if (NamedGear.Ethereal(item)) { min = min * 3 / 2; max = max * 3 / 2; }
        min = min * (100 + enhanced) / 100 + Bonus(item, "dmg-min");
        max = max * (100 + enhanced) / 100 + Bonus(item, "dmg-max");
        return (min, Math.Max(min, max));
    }
    public static int RequiredLevel(Item item) => Slot(item) == GearSlot.Cosmetic ? 0
        : Math.Max(State(item)?.Roll.RequiredLevel ?? Base(item)?.RequiredLevel ?? 0, RuneSockets.RequiredLevel(State(item) is { } saved ? RuneSockets.Filled(saved) : 0));
    public static int BlockChance(PlayerMobile player)
    {
        Item shield = null;
        foreach (var item in player.Items)
        {
            if (Slot(item) == GearSlot.Shield && Active(player, item))
            {
                shield = item;
                break;
            }
        }
        if (shield == null || ClassMovement.IsRunning(player, Core.TickCount)
            || !ClassCreation.Profiles.TryGet(player.Serial, out var profile))
        {
            return 0;
        }
        int classBlock = profile.Class switch
        {
            DiabloClass.Paladin => 30,
            DiabloClass.Amazon or DiabloClass.Barbarian or DiabloClass.Assassin => 25,
            _ => 20
        };
        return Math.Clamp(((Base(shield)?.Block ?? 0) + classBlock + Bonus(player, "block")+HolyShields.BlockBonus(player))
            * Math.Max(0, player.Dex - 15) / (2 * profile.Level), 0, 75);
    }
    public static int? Stat(Item item, ClassGearStat stat)
    {
        if (!Converted(item))
        {
            return null;
        }
        var basis = Slot(item) == GearSlot.Cosmetic ? null : Base(item);
        return stat switch
        {
            ClassGearStat.MinDamage => Damage(item).Min,
            ClassGearStat.MaxDamage => Damage(item).Max,
            ClassGearStat.Defense => Defense(item),
            ClassGearStat.StrengthRequirement => Math.Max(0, (basis?.Strength ?? 0) - (NamedGear.Ethereal(item) ? 10 : 0)) * Math.Max(0, 100 + NamedGear.Bonus(item, "ease")) / 100,
            ClassGearStat.DexterityRequirement => Math.Max(0, (basis?.Dexterity ?? 0) - (NamedGear.Ethereal(item) ? 10 : 0)) * Math.Max(0, 100 + NamedGear.Bonus(item, "ease")) / 100,
            ClassGearStat.IntelligenceRequirement => 0,
            ClassGearStat.StrengthBonus => EffectiveBonus(item, "str"),
            ClassGearStat.DexterityBonus => EffectiveBonus(item, "dex"),
            ClassGearStat.IntelligenceBonus => EffectiveBonus(item, "enr"),
            _ => 0
        };
    }
    private static int EffectiveBonus(Item item, string code) => item.Parent is PlayerMobile player && !Active(player, item)
        ? 0 : Bonus(item, code);
    private static string AttributeCode(AosAttribute attribute) => attribute switch
    {
        AosAttribute.BonusStr => "str", AosAttribute.BonusDex => "dex", AosAttribute.BonusInt => "enr",
        AosAttribute.BonusHits => "hp", AosAttribute.BonusMana => "mana", AosAttribute.BonusStam => "stam",
        AosAttribute.AttackChance => "att%", AosAttribute.DefendChance => "ac%", _ => null
    };
    private static int Attribute(Item item, AosAttribute attribute)
    {
        var code = AttributeCode(attribute);
        if (code != null)
        {
            return EffectiveBonus(item, code);
        }
        return attribute switch
        {
            AosAttribute.WeaponSpeed => EffectiveBonus(item, "swing1") + EffectiveBonus(item, "swing2") + EffectiveBonus(item, "swing3"),
            AosAttribute.CastSpeed => EffectiveBonus(item, "cast1") + EffectiveBonus(item, "cast2") + EffectiveBonus(item, "cast3"),
            AosAttribute.WeaponDamage => EffectiveBonus(item, "dmg%"), _ => 0
        };
    }
    public static int? MobileAttribute(Mobile mobile, AosAttribute attribute)
    {
        if (mobile is not PlayerMobile player || !ClassCombat.Enabled(player))
        {
            return null;
        }
        int value = 0;
        foreach (var item in player.Items)
        {
            if (Active(player, item))
            {
                value += Attribute(item, attribute);
            }
        }
        return value;
    }
    public static int RunBonus(PlayerMobile player)
    {
        int frw = Math.Max(0, Bonus(player, "move1") + Bonus(player, "move2") + Bonus(player, "move3"));
        return 120 * frw / (120 + frw);
    }
    public static bool? CanEquip(Item item, Mobile mobile)
    {
        if (mobile is not PlayerMobile player || !ClassCombat.Enabled(player))
        {
            return null;
        }
        if (!Converted(item))
        {
            return false;
        }
        if (State(item) is { Identified: false })
        {
            player.SendMessage("Identify this item before equipping it.");
            return false;
        }
        if (ClassStats.Level(player) < RequiredLevel(item)
            || player.Str < (Stat(item, ClassGearStat.StrengthRequirement) ?? 0)
            || player.Dex < (Stat(item, ClassGearStat.DexterityRequirement) ?? 0))
        {
            player.SendMessage("You do not meet this item's level or attribute requirements.");
            return false;
        }
        if (Slot(item) is GearSlot.Weapon or GearSlot.Shield)
        {
            foreach (var other in player.Items)
            {
                if (other != item && Slot(other) is GearSlot.Weapon or GearSlot.Shield
                    && (Base(item) is { TwoHanded: true, OneOrTwoHanded: false }
                        || Base(other) is { TwoHanded: true, OneOrTwoHanded: false }))
                {
                    player.SendMessage("This weapon requires both hands.");
                    return false;
                }
            }
        }
        if (Base(item) is { } basis && EquipmentBalance.Types[basis.Type].CharacterClass is { Length: > 0 } required)
        {
            ClassCreation.Profiles.TryGet(player.Serial, out var profile);
            string code = profile.Class switch
            {
                DiabloClass.Amazon => "ama", DiabloClass.Sorceress => "sor", DiabloClass.Necromancer => "nec",
                DiabloClass.Paladin => "pal", DiabloClass.Barbarian => "bar", DiabloClass.Druid => "dru", _ => "ass"
            };
            if (required != code)
            {
                player.SendMessage("This base is restricted to its Diablo class.");
                return false;
            }
        }
        return true;
    }
    public static bool AddProperties(Item item, IPropertyList list)
    {
        if (ClassProfessions.Enabled && SocketGems.Legacy(item.GetType()))
        { list.Add($"{"Decorative legacy gem — not for crafting or sockets."}"); return true; }
        if (!Converted(item))
        {
            return false;
        }
        var definition = Definition(item);
        if (State(item) is { Identified: false })
        {
            list.Add($"{"Unidentified — properties hidden"}");
            return true;
        }
        if (Slot(item) == GearSlot.Cosmetic)
        {
            list.Add($"{"Cosmetic — no combat stats, material effects or active sockets"}");
            return true;
        }
        var basis = Base(item);
        var saved = State(item);
        var roll = saved?.Roll;
        if (saved?.NamedItemId != null) { NamedGear.Properties(item, saved, list); }
        if (saved != null)
        {
            list.Add($"{"Base tier: "}{saved.Tier}{"; quality: "}{CraftTransactions.QualityName(saved.Quality)}");
            if (saved.SocketCount > 0) { list.Add($"{"Empty sockets: "}{saved.SocketCount - RuneSockets.Filled(saved)}{"; filled: "}{RuneSockets.Filled(saved)}"); }
            GemSockets.Properties(item, saved, list);
            foreach (var rune in saved.Runes)
            { list.Add($"{"Installed "}{rune.Pattern.Name}{": "}{rune.Pattern.StatLabel}{" +"}{rune.Percentage}{"%"}"); }
            if (saved.CraftedEquipment)
            {
                var native = GearDetails.Read(item);
                list.Add($"{"Durability: "}{native.Durability}{" / "}{native.MaximumDurability}");
                if (native.Creator is { Length: > 0 } maker) { list.Add($"{"Crafted by "}{maker}"); }
                if (Broken(item)) { list.Add($"{"Broken — combat effects inactive"}"); }
                foreach (var material in saved.MaterialRolls)
                {
                    if (material.Code == "magic-find")
                    { list.Add($"{"Better chance of finding magic items: +"}{material.Value}{"%"}"); }
                    else if (material.Code == "gold-find")
                    { list.Add($"{"Extra gold from monsters: +"}{material.Value}{"%"}"); }
                    else if (material.Code is "life-per-five" or "mana-per-five")
                    {
                        string resource = material.Code == "life-per-five" ? "Life" : "Mana";
                        list.Add($"{resource}{" regeneration: +"}{material.Value}{" per 5 seconds"}");
                    }
                    else
                    {
                        string element = material.Code[(material.Code.IndexOf('-') + 1)..];
                        if (material.Code.StartsWith("resistance-", StringComparison.Ordinal))
                        { list.Add($"{element}{" resistance: +"}{material.Value}{" percentage points"}"); }
                        else
                        {
                            list.Add($"{"Added "}{element}{" damage: "}{material.Value}{"% of reference weapon damage"}");
                            if (element == "cold") { list.Add($"{"Chills attacks and movement (base 2 seconds)"}"); }
                            if (element == "poison") { list.Add($"{"Poison damage is delivered over 3 seconds"}"); }
                        }
                    }
                }
            }
        }
        list.Add($"{"Rarity: "}{roll?.Rarity ?? GearRarity.Normal}{"; base: "}{basis.Name}");
        list.Add($"{"Item level: "}{roll?.ItemLevel ?? 1}{"; affix level: "}{roll?.AffixLevel ?? AffixBalance.Level(1, basis.QualityLevel, basis.MagicLevel)}");
        if (Slot(item) == GearSlot.Weapon && item is BaseWeapon)
        {
            var damage = Damage(item);
            list.Add(1061168, $"{damage.Min}\t{damage.Max}");
            CombatTiming.AddSpeedProperties((BaseWeapon)item, list);
        }
        if (basis.MaxDefense > 0)
        {
            list.Add($"{"Defense: "}{Defense(item)}");
        }
        if (Slot(item) == GearSlot.Shield && item.Parent is PlayerMobile player)
        {
            list.Add($"{"Chance to block: "}{BlockChance(player)}{"%"}");
        }
        list.Add($"{"Requires level "}{RequiredLevel(item)}{"; Strength "}{basis.Strength}{"; Dexterity "}{basis.Dexterity}");
        if (roll != null)
        {
            foreach (var affix in roll.Affixes)
            {
                foreach (var modifier in affix.Modifiers)
                {
                    if (modifier.Code == "mag%")
                    { list.Add($"{affix.Definition.Name}{": better chance of finding magic items +"}{modifier.Value}{"%"}"); }
                    else { list.Add($"{affix.Definition.Name}{": "}{modifier.Code}{" "}{modifier.Value}"); }
                }
            }
        }
        return true;
    }
    public static void Changed(PlayerMobile player)
    {
        if (!_refresh.Add(player.Serial))
        {
            return;
        }
        Timer.DelayCall(() =>
        {
            _refresh.Remove(player.Serial);
            if (player.Deleted || !ClassCombat.Enabled(player))
            {
                return;
            }
            Refresh(player);
            foreach (var item in player.Items)
            {
                if (State(item) is { NamedItemId: not null }) { item.InvalidateProperties(); }
            }
            StatNetwork.Send(player);
            if (player.NetState != null)
            { player.CheckLightLevels(false); ClassCombat.SendState(player, ClassCombat.For(player)); SkillBindingNetwork.Send(player); }
        });
    }
    public static void Refresh(PlayerMobile player)
    {
        foreach (var item in player.Items)
        {
            if (!Converted(item))
            {
                continue;
            }
            foreach (var (stat, code, suffix) in new[] { (StatType.Str, "str", "Str"), (StatType.Dex, "dex", "Dex"), (StatType.Int, "enr", "Int") })
            {
                string name = $"{item.Serial}{suffix}";
                player.RemoveStatMod(name);
                int value = Active(player, item) ? Bonus(item, code) : 0;
                if (value != 0)
                {
                    player.AddStatMod(new StatMod(stat, name, value, TimeSpan.Zero));
                }
            }
        }
        foreach (var (stat, code) in new[] { (StatType.Str, "str"), (StatType.Dex, "dex"), (StatType.Int, "enr") })
        {
            string name = "SanctuaryNamedSet/" + code;
            player.RemoveStatMod(name);
            int value = NamedGear.SetBonus(player, code);
            if (value != 0) { player.AddStatMod(new StatMod(stat, name, value, TimeSpan.Zero)); }
        }
        player.UpdateResistances();
        ClassStats.Refresh(player);
        player.Hits = Math.Min(player.Hits, player.HitsMax);
        player.Mana = Math.Min(player.Mana, player.ManaMax);
        player.Stam = Math.Min(player.Stam, player.StamMax);
        NamedSetVisuals.Track(player);
    }
    public static void Configure()
    {
        Profiles = new GearProfiles();
        ClassGearHooks.IgnoreAppearanceRestrictions = item => State(item)?.NamedItemId != null;
        ItemStateHooks.Copied = (source, copy) =>
        {
            RuneMould.Copy(source, copy);
            Runebark.Copy(source, copy);
            InscribedRune.Copy(source, copy);
            SocketGem.Copy(source, copy);
            Profiles.Copy(source, copy);
            CraftTransactions.Profiles?.Copy(source, copy);
            EquipmentRepairs.Profiles?.Copy(source, copy);
        };
        ItemStateHooks.CanStack = (first, second) => !Converted(first) && !Converted(second);
        ClassGearHooks.PersistResource = item => State(item)?.CraftedEquipment == true;
        ClassGearHooks.CanEnhance = item => State(item)?.CraftedEquipment != true;
        ClassGearHooks.NativeDurabilityBonus = item => State(item)?.CraftedEquipment == true ? 0 : null;
        ClassGearHooks.KeepBroken = KeepBroken;
        ClassGearHooks.WearAllowed = item => !NamedGear.Indestructible(item);
        ClassGearHooks.LightBonus = player => ClassCombat.Enabled(player) && player.Alive ? Bonus(player, "light") : 0;
        ClassGearHooks.DurabilityChanged = item =>
        {
            NamedMaintenance.Track(item);
            if (item.Parent is PlayerMobile player && ClassCombat.Enabled(player))
            {
                Refresh(player);
                Changed(player);
            }
        };
        ClassGearHooks.Stat = Stat;
        ClassGearHooks.CanEquip = CanEquip;
        ClassGearHooks.Properties = AddProperties;
        ClassGearHooks.Unidentified = item => State(item) is { Identified: false };
        ClassGearHooks.Attribute = (attributes, mask) => attributes.Owner is Item item && Converted(item)
            ? attributes is AosAttributes ? Attribute(item, (AosAttribute)mask) : 0 : null;
        ClassGearHooks.MobileAttribute = MobileAttribute;
        ClassGearHooks.Resistance = (player, type) => !ClassCombat.Enabled(player) ? null
            : Math.Clamp(RawResistance(player,type),-100,NamedCombat.ResistanceCap(player,type));
        EventSink.WorldSave += Profiles.Prune;
    }
    public static int RawResistance(PlayerMobile player,ResistanceType type) => EliteAuras.PlayerResistanceRaw(player,type,
        Bonus(player,type switch
        {
            ResistanceType.Fire=>"res-fire",ResistanceType.Cold=>"res-cold",ResistanceType.Poison=>"res-pois",
            ResistanceType.Energy=>"res-ltng",_=>"res-physical"
        })+(type==ResistanceType.Physical?0:Bonus(player,"res-all"))+PaladinResistance.Bonus(player,type)+BarbarianCombat.Resistance(player,type));
}
