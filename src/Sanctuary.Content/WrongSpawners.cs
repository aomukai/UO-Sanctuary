// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class WrongOne : Spawner
{
    [Constructible]
    public WrongOne() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        WrongSpawns.Configure(this, WrongRole.Thug, WrongRole.Ruffian, WrongRole.Manhunter, WrongRole.SlaveMerchant);
}

[SerializationGenerator(0)]
public partial class WrongDrake1 : Spawner
{
    [Constructible]
    public WrongDrake1() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        WrongSpawns.Configure(this, WrongRole.Hatchling, WrongRole.Juvenile, WrongRole.AdultDrake);
}

[SerializationGenerator(0)]
public partial class WrongTwo : Spawner
{
    [Constructible]
    public WrongTwo() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        WrongSpawns.Configure(this, WrongRole.Rowdy, WrongRole.Brute);
}

[SerializationGenerator(0)]
public partial class WrongDrake2 : Spawner
{
    [Constructible]
    public WrongDrake2() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        WrongSpawns.Configure(this, WrongRole.BattleDrake, WrongRole.AssaultDrake);
}

internal static class WrongSpawns
{
    internal static void Configure(Spawner spawner, params WrongRole[] pool)
    {
        spawner.Name = spawner.GetType().Name;
        spawner.HomeRange = 4;
        spawner.WalkingRange = 4;
        spawner.SpawnLocationIsHome = true;
        foreach (var role in pool)
            spawner.AddEntry(nameof(WrongEncounter), probability: 100, dotimer: false, parameters: $"{role} 10");
    }
}
