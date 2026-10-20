// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using ModernUO.Serialization;
using Server;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class SocketGem : Item
{
    [SerializableField(0, setter: "private")] private string _family;
    [SerializableField(1, setter: "private")] private GemQuality _quality;
    [SerializableField(2, setter: "private")] private string _effectsJson;
    private GemEffects _effects;
    public GemEffects Effects => _effects;
    internal string EffectIdentity => _effectsJson;
    public int SellPrice => SocketGems.Settings.Prices[(int)_quality];
    [Constructible] public SocketGem() : this("Ruby", GemQuality.Chipped) { }
    public SocketGem(string family, GemQuality quality, int amount = 1) : base(SocketGems.Family(family).Art)
    {
        if (!Enum.IsDefined(quality) || amount is < 1 or > 60000) { throw new ArgumentOutOfRangeException(nameof(quality)); }
        var row = SocketGems.Family(family); _family = family; _quality = quality;
        _effectsJson = JsonSerializer.Serialize(row.Effects[(int)quality]); Decode();
        Name = $"{quality} {row.Name}"; Hue = row.Hue; Stackable = true; Weight = .1; Amount = amount;
    }
    [AfterDeserialization]
    private void Decode()
    {
        SocketGems.Family(_family);
        if (!Enum.IsDefined(_quality) || _effectsJson is not { Length: > 0 and <= 16000 }) { throw new InvalidDataException("Invalid saved gem identity."); }
        _effects = JsonSerializer.Deserialize<GemEffects>(_effectsJson); SocketGems.Validate(_effects);
    }
    public override bool CanStackWith(Item dropped) => dropped is SocketGem gem && gem._family == _family
        && gem._quality == _quality && gem._effectsJson == _effectsJson && base.CanStackWith(dropped);
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list); list.Add($"{"Socket gem — "}{_quality}");
        AddEffects(list, "Weapon", _effects.Weapon); AddEffects(list, "Shield", _effects.Shield); AddEffects(list, "Armour", _effects.Armor);
        list.Add($"{"Recorded socket effects; gem insertion is a later step."}");
    }
    private static void AddEffects(IPropertyList list, string slot, GemEffect[] effects)
    {
        foreach (var effect in effects)
        {
            if (effect.DurationMs > 0)
            { list.Add($"{slot}{": "}{effect.Label}{" "}{effect.Minimum}{"–"}{effect.Maximum}{" over "}{effect.DurationMs / 1000.0:F1}{" seconds"}"); }
            else if (effect.Minimum != effect.Maximum)
            { list.Add($"{slot}{": "}{effect.Label}{" "}{effect.Minimum}{"–"}{effect.Maximum}"); }
            else { list.Add($"{slot}{": "}{effect.Label}{" +"}{effect.Minimum}"); }
        }
    }
    public override void OnDoubleClick(Mobile from)
    { if (from.InRange(GetWorldLocation(), 2)) { from.SendMessage("Keep this gem for socket insertion, or upgrade three matching gems using tinker's tools."); } }
    internal static void Copy(Item source, Item destination)
    {
        if (source is not SocketGem a || destination is not SocketGem b) { return; }
        b._family = a._family; b._quality = a._quality; b._effectsJson = a._effectsJson; b.Decode(); b.InvalidateProperties();
    }
}
