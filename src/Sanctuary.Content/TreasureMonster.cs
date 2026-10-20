// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class TreasureMonster : EncounterCreature
{
    [SerializableField(0, setter: "private")] private int _treasureTier;
    [SerializableField(1, setter: "private")] private int _firstImmunity;
    [SerializableField(2, setter: "private")] private int _secondImmunity;
    [SerializableField(3, setter: "private")] private int _highResistance;
    [SerializableField(4, setter: "private")] private string _sourceTypeName;
    public Type SourceType => typeof(Server.Mobiles.BaseCreature).Assembly.GetType("Server.Mobiles." + (_sourceTypeName ?? "Skeleton"));
    [Server.Constructible] public TreasureMonster() { }
    internal void Bind(int tier, int first, int second, int high, string source)
    { _treasureTier = tier; _firstImmunity = first; _secondImmunity = second; _highResistance = high; _sourceTypeName = source; }
    protected override int[] AdjustResistances(int[] resistances)
    {
        if (_treasureTier == 0) { return resistances; }
        for (int i = 0; i < resistances.Length; i++)
        {
            resistances[i] = i == _firstImmunity || i == _secondImmunity ? Math.Max(100, resistances[i])
                : Math.Min(99, resistances[i] + (i == _highResistance ? 75 : 0));
        }
        return resistances;
    }
}
