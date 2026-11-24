// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class DestLow : Spawner
{
    [Constructible]
    public DestLow() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        DestardSpawns.Configure(this, (DestardRole.Wyvern, 34), (DestardRole.EnslavedDrake, 33), (DestardRole.LesserDaemon, 33));
}

[SerializationGenerator(0)]
public partial class DestMed : Spawner
{
    [Constructible]
    public DestMed() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        DestardSpawns.Configure(this, (DestardRole.Daemon, 50), (DestardRole.EnslavedDragon, 50));
}

[SerializationGenerator(0)]
public partial class DestHigh : Spawner
{
    [Constructible]
    public DestHigh() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        DestardSpawns.Configure(this, (DestardRole.Wyrm, 50), (DestardRole.Archdaemon, 50));
}

[SerializationGenerator(0)]
public partial class DestSpawn : Spawner
{
    [Constructible]
    public DestSpawn() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        DestardSpawns.Configure(this, (DestardRole.CorruptedHatchling, 50), (DestardRole.DaemonSpawn, 50));
}

internal static class DestardSpawns
{
    internal static void Configure(Spawner spawner, params (DestardRole Role, int Chance)[] pool)
    {
        spawner.Name = spawner.GetType().Name;
        spawner.HomeRange = 3;
        spawner.WalkingRange = 3;
        spawner.SpawnLocationIsHome = true;
        foreach (var (role, chance) in pool)
            spawner.AddEntry(nameof(DestardEncounter), probability: chance, dotimer: false, parameters: role.ToString());
    }
}
