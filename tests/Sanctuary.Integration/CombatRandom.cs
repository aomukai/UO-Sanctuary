using System;
using System.Reflection;
using System.Collections.Generic;
using Server.Random;

namespace Sanctuary.Integration;

// Deterministic rolls keep cadence/targeting checks independent of hit chance and damage variance.
internal sealed class CombatRandom : System.Random, IDisposable
{
    private static readonly PropertyInfo Generator = typeof(BuiltInRng).GetProperty(nameof(BuiltInRng.Generator));
    private readonly System.Random _previous;
    private readonly double _roll;
    private readonly Queue<double> _rolls;
    public CombatRandom(double roll = 0, params double[] rolls)
    {
        _roll = roll;
        _rolls = new Queue<double>(rolls);
        _previous = (System.Random)Generator.GetValue(null);
        Generator.SetValue(null, this);
    }
    public override double NextDouble() => _rolls.TryDequeue(out var roll) ? roll : _roll;
    public override int Next(int maxValue) => 0;
    public override int Next(int minValue, int maxValue) => minValue;
    public void Dispose() => Generator.SetValue(null, _previous);
}
