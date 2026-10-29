// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class NamedGear
{
    public static NamedEquipmentCatalog Catalog { get; private set; }
    private static readonly Dictionary<string, string[]> _bindings = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string[]> _blocked = new(StringComparer.Ordinal);
    private sealed record Worn(Item Item, GearState State);
    private sealed record Wearer(Worn[] Items, NamedBonusPreview[] Bonuses);
    private static readonly Wearer _empty = new([], []);
    private static readonly Dictionary<Serial, Wearer> _wearers = new();
    private static readonly Dictionary<string, (string Key, NamedProperty Property)[]> _templates = new(StringComparer.Ordinal);
    public static IReadOnlyDictionary<string, string[]> Unavailable => _blocked;
    public static void Configure() => EventSink.Disconnected += from => Forget(from.Serial);
    public static void Forget(Serial owner) => _wearers.Remove(owner);
    private static bool Appearance(string name)
    {
        var type = EquipmentCrafting.Resolve(name);
        return type != null && EquipmentBalance.TryGet(type, out var definition) && definition.Category != "Unarmed"
            && (typeof(BaseWeapon).IsAssignableFrom(type) || typeof(BaseArmor).IsAssignableFrom(type)
                || typeof(BaseClothing).IsAssignableFrom(type) || typeof(BaseJewel).IsAssignableFrom(type));
    }

    public static void Load()
    {
        Catalog = NamedEquipmentCatalog.Load(Path.Join(Core.BaseDirectory, "Data", "Sanctuary", "Balance"));
        NamedSkills.Load();
        _bindings.Clear(); _blocked.Clear(); _wearers.Clear(); _templates.Clear();
        foreach (var basis in Catalog.Bases) { EquipmentBalance.AddNamedBase(basis); }
        EquipmentBalance.AddNamedAppearances();
        foreach (var basis in Catalog.Bases)
        {
            var available = basis.ProjectAppearances.Select(p => p.Type).Where(Appearance).Distinct().ToArray();
            if (available.Length == 0 && basis.SourceTable != "misc")
            {
                string Family(string type) => type switch
                { "h2h2" => "h2h", "pelt" or "phlm" => "helm", "head" or "ashd" => "shie", "abow" => "bow", "aspe" => "spea", "ajav" => "jave", "orb" => "wand", _ => type };
                var analogous = Catalog.Bases.Where(b => b.ProjectAppearances.Length > 0 && Family(b.ItemType) == Family(basis.ItemType))
                    .OrderBy(b => Math.Abs((b.SourceFields.TryGetProperty("level", out var level) ? level.GetInt32() : 0)
                        - (basis.SourceFields.TryGetProperty("level", out var desired) ? desired.GetInt32() : 0)))
                    .ThenBy(b => b.Code, StringComparer.Ordinal).FirstOrDefault();
                available = analogous?.ProjectAppearances.Select(p => p.Type).Where(Appearance).Distinct().ToArray() ?? [];
                // A source family can have no named normal-tier base at all
                // (Assassin claws), while its reviewed native appearance exists.
                if (available.Length == 0)
                {
                    available = EquipmentBalance.All.Where(g => g.AffixType != null
                        && Family(g.AffixType) == Family(basis.ItemType) && g.Slot != GearSlot.Cosmetic)
                        .Select(g => g.Type).Where(Appearance).Order(StringComparer.Ordinal).ToArray();
                }
            }
            if (basis.ItemType == "tkni") { available = [nameof(NamedThrowingKnife)]; }
            _bindings[basis.Code] = available;
        }
        foreach (var item in Catalog.Items)
        {
            _templates[item.Id] = NamedPropertyRuntime.Templates(item).ToArray();
            var reasons = new HashSet<string>(StringComparer.Ordinal);
            if (!item.SourceSpawnable) { reasons.Add("Source definition is not available to random acquisition"); }
            if (item.BaseCode is "cm1" or "cm2" or "cm3")
            {
                reasons.Add("Charms are excluded by project policy");
                _blocked[item.Id] = reasons.Order(StringComparer.Ordinal).ToArray();
                continue;
            }
            if (item.BaseCode == null || _bindings.GetValueOrDefault(item.BaseCode)?.Length is not > 0)
            { reasons.Add("No reviewed equipment appearance"); }
            if (item.BaseCode != null && Catalog.Base(item.BaseCode).RequiredClass == "war")
            { reasons.Add("Warlock class is not implemented"); }
            if (item.BaseCode != null && Catalog.Base(item.BaseCode).SourceFields.TryGetProperty("quest", out var quest)
                && quest.TryGetInt32(out int questId) && questId != 0)
            { reasons.Add("Quest-only base requires its quest acquisition path"); }
            foreach (var template in NamedPropertyRuntime.Templates(item))
            { if (!NamedPropertyRuntime.Supported(template.Property) || !NamedSkills.Supported(template.Property)) { reasons.Add("Effect: " + template.Property.Code); } }
            if (item.Rarity == "Set")
            {
                var set = Catalog.Set(item.SetId);
                foreach (var property in set.PartialBonuses.Concat(set.FullBonuses))
                { if (!NamedPropertyRuntime.Supported(property) || !NamedSkills.Supported(property)) { reasons.Add("Set effect: " + property.Code); } }
            }
            if (item.Properties.Any(p => p.SourceEnabled && p.Code == "sock" && (p.SourceMax > 5 || NamedPropertyRuntime.Parameter(p) > 5)))
            { reasons.Add("Source sockets exceed the project limit"); }
            if (reasons.Count > 0) { _blocked[item.Id] = reasons.Order(StringComparer.Ordinal).ToArray(); }
        }
        // A set must be completable: never supply one piece while its partners
        // or advertised full-set effects are unavailable.
        foreach (var set in Catalog.Sets)
        {
            if (set.PieceIds.Select(id => Catalog.Base(Catalog.Item(id).BaseCode).SourceTable).Count(t => t == "weapons") > 1)
            { foreach (string id in set.PieceIds) { _blocked[id] = _blocked.GetValueOrDefault(id, []).Append("Set requires dual wielding").ToArray(); } }
            if (set.PieceIds.Any(id => _blocked.ContainsKey(id)))
            {
                foreach (string id in set.PieceIds)
                { _blocked[id] = _blocked.GetValueOrDefault(id, []).Append("Set has unavailable pieces").Distinct().ToArray(); }
            }
        }
    }

    public static bool Available(string id) => Catalog != null && id != null && _templates.ContainsKey(id) && !_blocked.ContainsKey(id);
    internal static string[] Appearances(string id) => Available(id) ? _bindings[Catalog.Item(id).BaseCode] : [];
    public static NamedItemDefinition Select(string code, GearRarity rarity, int level, Random random)
    {
        if (rarity is not (GearRarity.Set or GearRarity.Unique)) { throw new ArgumentOutOfRangeException(nameof(rarity)); }
        var candidates = Catalog.SourceCandidates(code, rarity == GearRarity.Unique ? "Unique" : "Set", level)
            .OrderBy(i => i.SourceId).ToArray();
        int total = candidates.Sum(i => i.SelectionWeight);
        if (total == 0) { return null; }
        int draw = random.Next(total);
        foreach (var candidate in candidates)
        { if ((draw -= candidate.SelectionWeight) < 0) { return Available(candidate.Id) ? candidate : null; } }
        throw new InvalidOperationException("Named selection escaped its source weights.");
    }
    public static Item Generate(string code, GearRarity rarity, int level, Random random, bool identified = false, string appearance = null)
    {
        var definition = Select(code, rarity, level, random);
        if (definition == null) { return null; }
        return Create(definition.Id, level, random, identified, appearance);
    }
    public static Item Create(string id, int level, Random random, bool identified = true, string appearance = null)
    {
        if (!Available(id) || level is < 1 or > 99) { throw new InvalidOperationException("That named definition is not available."); }
        var definition = Catalog.Item(id); var basis = EquipmentBalance.Base(definition.BaseCode);
        var choices = _bindings[basis.Code]; string type = appearance != null && choices.Contains(appearance)
            ? appearance : choices[random.Next(choices.Length)];
        var item = ItemOrigins.Create(ItemOrigin.Drop, () => EquipmentCrafting.Resolve(type).CreateInstance<Item>());
        try
        {
            var rolls = NamedPropertyRuntime.Templates(definition).Select(p => NamedPropertyRuntime.Roll(p.Key, p.Property, random)).ToArray();
            int sockets = definition.Properties.Where(p => p.SourceEnabled && p.Code == "sock")
                .Sum(p => rolls.Single(r => r.Key == "p/" + p.Slot).Value);
            var tier = Enum.Parse<GearTier>(basis.Tier);
            var state = new GearState(basis.Code, random.Next(basis.MinDefense, basis.MaxDefense + 1), false,
                new(definition.Rarity == "Unique" ? GearRarity.Unique : GearRarity.Set, level,
                    AffixBalance.Level(level, basis.QualityLevel, basis.MagicLevel),
                    Math.Max(Catalog.SourceRequiredLevel(id), EquipmentCrafting.EquipFloor(tier)), []))
            { NamedItemId = id, NamedProperties = rolls, NamedSockets = sockets, Tier = tier, Identified = identified };
            ClassGear.Profiles.Store(item, state);
            int extraDurability = FixedValue(state, "dur");
            if (extraDurability > 0)
            {
                switch (item)
                {
                    case BaseWeapon weapon: weapon.MaxHitPoints = Math.Min(65535, weapon.MaxHitPoints + extraDurability); weapon.HitPoints = weapon.MaxHitPoints; break;
                    case BaseArmor armor: armor.MaxHitPoints = Math.Min(65535, armor.MaxHitPoints + extraDurability); armor.HitPoints = armor.MaxHitPoints; break;
                    case BaseClothing clothing: clothing.MaxHitPoints = Math.Min(65535, clothing.MaxHitPoints + extraDurability); clothing.HitPoints = clothing.MaxHitPoints; break;
                    case BaseJewel jewel: jewel.MaxHitPoints = Math.Min(65535, jewel.MaxHitPoints + extraDurability); jewel.HitPoints = jewel.MaxHitPoints; break;
                }
            }
            return item;
        }
        catch { item.Delete(); throw; }
    }
    public static GearDefinition Definition(Item item, GearState state)
    {
        if (state?.NamedItemId == null || Catalog == null || !EquipmentBalance.TryGet(item, out var native)) { return null; }
        var basis = Catalog.Base(state.BaseCode);
        var slot = basis.SourceTable == "weapons" ? GearSlot.Weapon : basis.ItemType switch
        { "head" or "ashd" or "shie" => GearSlot.Shield, "tors" => GearSlot.Body, "pelt" or "phlm" or "helm" or "circ" => GearSlot.Head,
            "boot" => GearSlot.Feet, "glov" => GearSlot.Hands, "belt" => GearSlot.Belt, "ring" => GearSlot.Ring, "amul" => GearSlot.Amulet, _ => native.Slot };
        return native with { BaseCode = state.BaseCode, AffixType = EquipmentBalance.Base(state.BaseCode).Type, Slot = slot };
    }
    public static bool Identity(Item item, GearState state) => state.NamedItemId != null && Available(state.NamedItemId)
        && Catalog.Item(state.NamedItemId).BaseCode == state.BaseCode && _bindings[state.BaseCode].Contains(item.GetType().Name);
    public static void UpdateName(Item item, GearState state)
    {
        if (state?.NamedItemId != null)
        { item.Name = state.Identified ? Catalog.Item(state.NamedItemId).Name : Catalog.Base(state.BaseCode).Name; }
    }
    public static int FixedValue(GearState state, string code)
    {
        if (state?.NamedItemId == null) { return OrdinaryAffixProperties.FixedValue(state,code); }
        if (Catalog == null) { return 0; }
        int total = 0;
        foreach (var template in _templates[state.NamedItemId])
        { if (template.Key.StartsWith("p/", StringComparison.Ordinal) && template.Property.Code == code) { total += FindRoll(state, template.Key).Value; } }
        return total;
    }
    public static bool Indestructible(Item item) => FixedValue(ClassGear.State(item), "indestruct") > 0;
    public static bool Ethereal(Item item) => FixedValue(ClassGear.State(item), "ethereal") > 0;
    public static void Validate(Item item, GearState state)
    {
        var definition = Catalog.Item(state.NamedItemId); var basis = EquipmentBalance.Base(state.BaseCode);
        if (!Identity(item, state) || state.Roll.Rarity != (definition.Rarity == "Unique" ? GearRarity.Unique : GearRarity.Set)
            || state.Tier.ToString() != basis.Tier || state.CraftedEquipment || state.StarterTeeth || state.MaterialRolls.Length != 0
            || state.Quality != CraftQuality.Average || state.NativeSockets != 0 || state.Roll.Affixes.Length != 0
            || state.Defense < basis.MinDefense || state.Defense > basis.MaxDefense
            || state.Roll.AffixLevel != AffixBalance.Level(state.Roll.ItemLevel, basis.QualityLevel, basis.MagicLevel)
            || state.Roll.RequiredLevel != Math.Max(Catalog.SourceRequiredLevel(definition.Id), EquipmentCrafting.EquipFloor(state.Tier)))
        { throw new InvalidDataException("Invalid named equipment identity, base or requirements."); }
        var expected = NamedPropertyRuntime.Templates(definition).ToDictionary(t => t.Key, t => t.Property);
        if (state.NamedProperties == null || state.NamedProperties.Length != expected.Count
            || state.NamedProperties.Select(p => p.Key).Distinct().Count() != expected.Count)
        { throw new InvalidDataException("Invalid named-property ledger."); }
        foreach (var property in state.NamedProperties)
        {
            if (!expected.TryGetValue(property.Key, out var template)) { throw new InvalidDataException("Unknown saved named property."); }
            NamedPropertyRuntime.Validate(template, property);
        }
        int sockets = state.NamedProperties.Where(r => expected[r.Key].Code == "sock" && r.Key.StartsWith("p/", StringComparison.Ordinal)).Sum(r => r.Value);
        if (state.NamedSockets != sockets) { throw new InvalidDataException("Named sockets lost their definition provenance."); }
    }

    private static Wearer Equipped(PlayerMobile player)
    {
        if (Catalog == null) { return _empty; }
        _wearers.TryGetValue(player.Serial, out var saved);
        bool matches = saved != null; int count = 0;
        foreach (var item in player.Items)
        {
            if (ClassGear.State(item) is not { NamedItemId: not null } state || !ClassGear.Active(player, item)) { continue; }
            if (saved == null || count >= saved.Items.Length || saved.Items[count].Item != item
                || !ReferenceEquals(saved.Items[count].State, state)) { matches = false; }
            count++;
        }
        if (count == 0) { _wearers.Remove(player.Serial); return _empty; }
        if (matches && count == saved.Items.Length) { return saved; }
        var entries = new List<Worn>(count);
        foreach (var item in player.Items)
        {
            if (ClassGear.State(item) is { NamedItemId: not null } state && ClassGear.Active(player, item)) { entries.Add(new(item, state)); }
        }
        var result = new Wearer(entries.ToArray(), Catalog.PreviewSetBonuses(entries.Where(e => e.State.Roll.Rarity == GearRarity.Set)
            .Select(e => e.State.NamedItemId)));
        _wearers[player.Serial] = result; return result;
    }
    private static NamedBonusPreview[] Bonuses(PlayerMobile player) => Equipped(player).Bonuses;
    public static IEnumerable<(NamedProperty Property, NamedPropertyRoll Roll)> ActiveProperties(PlayerMobile player)
    {
        foreach(var item in player.Items)
        {
            if(ClassGear.State(item) is not{NamedItemId:null} state || !ClassGear.Active(player,item)){continue;}
            foreach(var property in OrdinaryAffixProperties.Properties(state))
            {if(WeaponHands.Contributes(player,item,property.Property.Code)){yield return property;}}
        }
        foreach (var worn in Equipped(player).Items)
        {
            var state = worn.State;
            var definition = Catalog.Item(state.NamedItemId);
            foreach (var template in _templates[definition.Id])
            {
                if(!WeaponHands.Contributes(player,worn.Item,template.Property.Code)){continue;}
                if (template.Key.StartsWith("a/", StringComparison.Ordinal) && !PieceBonusEnabled(player, definition.Id, template.Property.Slot)) { continue; }
                yield return (template.Property, FindRoll(state, template.Key));
            }
        }
        foreach (var bonus in Bonuses(player).Where(b => b.Scope != "Piece"))
        { yield return (bonus.Property, new("", bonus.Property.SourceMin ?? NamedPropertyRuntime.Parameter(bonus.Property), bonus.Property.SourceMax ?? bonus.Property.SourceMin ?? 0)); }
    }
    private static NamedPropertyRoll FindRoll(GearState state, string key)
    {
        foreach (var roll in state.NamedProperties) { if (roll.Key == key) { return roll; } }
        throw new InvalidDataException("Named property is missing its saved roll.");
    }
    private static bool PieceBonusEnabled(PlayerMobile player, string id, string slot)
    {
        foreach (var bonus in Bonuses(player))
        { if (bonus.Scope == "Piece" && bonus.OwnerId == id && bonus.Property.Slot == slot) { return true; } }
        return false;
    }
    public static int Bonus(Item item, string code) => Bonus(item, code, ClassGear.RecipientLevel(item));
    internal static IEnumerable<(NamedProperty Property, NamedPropertyRoll Roll)> IntrinsicProperties(Item item)
    {
        if (ClassGear.State(item) is not { NamedItemId: not null, Identified: true } state || ClassGear.Broken(item)) { yield break; }
        foreach (var template in _templates[state.NamedItemId])
        {
            if (template.Key.StartsWith("p/", StringComparison.Ordinal)) { yield return (template.Property, FindRoll(state, template.Key)); }
        }
    }
    internal static int Bonus(Item item, string code, int recipientLevel)
    {
        if (recipientLevel is < 1 or > 99) { throw new ArgumentOutOfRangeException(nameof(recipientLevel)); }
        if (ClassGear.State(item) is not { NamedItemId: not null, Identified: true } state || ClassGear.Broken(item)) { return 0; }
        var definition = Catalog.Item(state.NamedItemId);
        int sum = 0;
        foreach (var template in _templates[definition.Id])
        {
            if (template.Key.StartsWith("a/", StringComparison.Ordinal) && (item.Parent is not PlayerMobile wearer
                || !ClassGear.Active(wearer, item) || !PieceBonusEnabled(wearer, definition.Id, template.Property.Slot))) { continue; }
            sum += NamedPropertyRuntime.Bonus(template.Property, FindRoll(state, template.Key), code, recipientLevel);
        }
        return sum;
    }
    public static int SetBonus(PlayerMobile player, string code)
    {
        if (Catalog == null) { return 0; }
        int sum = 0;
        foreach (var bonus in Bonuses(player))
        {
            if (bonus.Scope != "Piece")
            { sum += NamedPropertyRuntime.Bonus(bonus.Property, new("", bonus.Property.SourceMin ?? NamedPropertyRuntime.Parameter(bonus.Property), bonus.Property.SourceMax ?? bonus.Property.SourceMin ?? 0), code, ClassStats.Level(player)); }
        }
        return sum;
    }
    public static double Elemental(PlayerMobile player, string element, double roll)
    {
        string code = element switch { "fire" => "dmg-fire", "cold" => "dmg-cold", "magic" => "dmg-mag", _ => "dmg-ltng" };
        string prefix = element == "lightning" ? "ltng" : element == "magic" ? "mag" : element;
        double total = ClassGear.Bonus(player, prefix + "-min") * (1 - roll) + ClassGear.Bonus(player, prefix + "-max") * roll;
        foreach (var value in ActiveProperties(player))
        { if (value.Property.Code == code || element != "magic" && value.Property.Code == "dmg-elem") { total += value.Roll.Value + (value.Roll.Maximum - value.Roll.Value) * roll; } }
        return total;
    }
    public static int ColdDuration(PlayerMobile player)
    {
        int frames = ClassGear.Bonus(player, "cold-len");
        foreach (var value in ActiveProperties(player))
        { if (value.Property.Code is "dmg-cold" or "dmg-elem") { frames += NamedPropertyRuntime.Parameter(value.Property); } }
        return Math.Max(0, frames) * 40;
    }
    internal readonly record struct PoisonContribution(int Minimum,int Maximum,int Frames,bool StatAggregate=false)
    {
        internal double Value(double roll)=>StatAggregate?Minimum*(1-roll)+Maximum*roll:Minimum+(Maximum-Minimum)*roll;
    }
    internal static IEnumerable<PoisonContribution> PoisonSources(PlayerMobile player)
    {
        foreach (var value in ActiveProperties(player))
        {
            if (value.Property.Code != "dmg-pois") { continue; }
            int sourceFrames=NamedPropertyRuntime.Parameter(value.Property);
            if(sourceFrames>0){yield return new(value.Roll.Value,value.Roll.Maximum,sourceFrames);}
        }
        int frames = ClassGear.Bonus(player, "pois-len");
        if (frames > 0)
        {
            yield return new(ClassGear.Bonus(player,"pois-min"),ClassGear.Bonus(player,"pois-max"),frames,true);
        }
    }
    public static (double Rate, int Length, int Count) Poison(PlayerMobile player, double roll)
    {
        int length=0,count=0;double rate=0;
        // Preserve the ordinary hit reader's units, operation order and weighted
        // stat-aggregate interpolation while exposing raw rates to missiles.
        foreach(var source in PoisonSources(player))
        {rate+=source.Value(roll)/10240.0;length+=source.Frames*40;count++;}
        return (rate, length, count);
    }
    public static void Properties(Item item, GearState state, IPropertyList list)
    {
        if (state.NamedItemId == null) { return; }
        var definition = Catalog.Item(state.NamedItemId);
        foreach (var template in NamedPropertyRuntime.Templates(definition))
        {
            var value = state.NamedProperties.Single(r => r.Key == template.Key);
            string suffix = "";
            if (template.Key.StartsWith("a/", StringComparison.Ordinal))
            {
                var property = template.Property;
                string condition = property.RequiredPieceId != null ? Catalog.Item(property.RequiredPieceId).Name
                    : property.RequiredPieces + " set pieces";
                bool active = item.Parent is PlayerMobile wearer && ClassGear.Active(wearer, item)
                    && PieceBonusEnabled(wearer, definition.Id, property.Slot);
                suffix = " (with " + condition + (active ? "; active)" : "; inactive)");
            }
            string label = PropertyText(template.Property, value, item.RootParent is PlayerMobile owner ? ClassStats.Level(owner) : 1);
            list.Add($"{label}{suffix}");
        }
        int capacity = NamedAmmunition.Maximum(item);
        if (capacity > 0) { list.Add($"{"Throwing Quantity: "}{NamedAmmunition.Remaining(item)}{"/"}{capacity}"); }
        if (definition.SetId != null)
        {
            var set = Catalog.Set(definition.SetId);
            int count = item.Parent is PlayerMobile wearer ? Equipped(wearer).Items.Where(w => w.State.Roll.Rarity == GearRarity.Set
                && Catalog.Item(w.State.NamedItemId).SetId == set.Id).Select(w => w.State.NamedItemId).Distinct().Count() : 0;
            list.Add($"{set.Name}{" — "}{count}{"/"}{set.PieceIds.Length}{" pieces"}");
            int level = item.RootParent is PlayerMobile owner ? ClassStats.Level(owner) : 1;
            foreach (var bonus in set.PartialBonuses.Where(b => b.SourceEnabled))
            {
                string label = PropertyText(bonus, new("", bonus.SourceMin.GetValueOrDefault(), bonus.SourceMax ?? bonus.SourceMin.GetValueOrDefault()), level);
                list.Add($"{bonus.RequiredPieces}{" pieces: "}{label}{(count >= bonus.RequiredPieces ? " (active)" : " (inactive)")}");
            }
            foreach (var bonus in set.FullBonuses.Where(b => b.SourceEnabled))
            {
                string label = PropertyText(bonus, new("", bonus.SourceMin.GetValueOrDefault(), bonus.SourceMax ?? bonus.SourceMin.GetValueOrDefault()), level);
                list.Add($"{"Full set: "}{label}{(count == set.PieceIds.Length ? " (active)" : " (inactive)")}");
            }
        }
    }
    internal static string PropertyText(NamedProperty property, NamedPropertyRoll value, int level)
    {
        if (property.Code == "state" && property.Parameter?.ToString() == "fullsetgeneric") { return "Golden Set Aura"; }
        if (property.Code == "fade") { return "Visible Fade (50% opacity)"; }
        if (property.Code == "bloody") { return "Extra Blood (cosmetic)"; }
        if (property.Code == "pierce-mag")
        { return FormattableString.Invariant($"-{value.Value}% To Enemy Magic Resistance"); }
        if (property.Code == "howl")
        { return FormattableString.Invariant($"Hit Causes Monster To Flee {value.Value * 100.0 / 128:0.##}%"); }
        if (property.Code == "rep-quant")
        { return FormattableString.Invariant($"Replenishes 1 Quantity Every {NamedMaintenance.PeriodMs(value.Value) / 1000.0:0.##} Seconds"); }
        if (property.Code == "rep-dur")
        { return FormattableString.Invariant($"Repairs 1 Durability Every {NamedMaintenance.PeriodMs(value.Value) / 1000.0:0.##} Seconds"); }
        if (property.Code == "dmg-pois")
        {
            int frames = NamedPropertyRuntime.Parameter(property);
            return FormattableString.Invariant($"Adds {value.Value * frames / 256.0:0.##}–{value.Maximum * frames / 256.0:0.##} Poison Damage Over {frames / 25.0:0.##} Seconds");
        }
        string label = Catalog?.Properties.FirstOrDefault(p => p.Code == property.Code) is{} definition && definition.SourceFields.TryGetProperty("*Tooltip", out var tooltip)
            ? tooltip.GetString() : null;
        label ??= property.Code + ": #";
        var skill = NamedSkills.Description(property);
        label = label.Replace("[Skill]", skill.Skill, StringComparison.Ordinal).Replace("[Class]", skill.CharacterClass, StringComparison.Ordinal)
            .Replace("[Skill Tab]", skill.Skill, StringComparison.Ordinal).Replace("[SkillTab]", skill.Skill, StringComparison.Ordinal);
        int minimum = value.Value, maximum = value.Maximum;
        if (property.Code.EndsWith("/lvl", StringComparison.Ordinal))
        { minimum = maximum = (int)((long)value.Value * Math.Clamp(level, 1, 99) / 8); }
        int marker = label.IndexOf('#');
        if (marker >= 0)
        {
            label = label[..marker] + minimum.ToString(System.Globalization.CultureInfo.InvariantCulture) + label[(marker + 1)..];
            label = label.Replace("#", maximum.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        return label;
    }
}
