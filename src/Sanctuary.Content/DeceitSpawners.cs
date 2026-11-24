// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class DecLow : Spawner
{
    [Constructible]
    public DecLow() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        DeceitSpawns.Configure(this, DeceitRole.Slave, DeceitRole.DemonicTaskmaster,
            DeceitRole.WatchfulOverseer, DeceitRole.DevoutFollower);
}

[SerializationGenerator(0)]
public partial class DecMed : Spawner
{
    [Constructible]
    public DecMed() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        DeceitSpawns.Configure(this, DeceitRole.Familiar, DeceitRole.NecromancerApprentice,
            DeceitRole.CultistEnslaver, DeceitRole.DemonicTorturer);
}

[SerializationGenerator(0)]
public partial class DecHigh : Spawner
{
    [Constructible]
    public DecHigh() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        DeceitSpawns.Configure(this, DeceitRole.Necromancer, DeceitRole.Seductress,
            DeceitRole.InfernalMistress, DeceitRole.Bishop);
}

internal static class DeceitSpawns
{
    internal static void Configure(Spawner spawner, params DeceitRole[] roles)
    {
        spawner.Name = spawner.GetType().Name;
        spawner.HomeRange = 3;
        spawner.WalkingRange = 3;
        spawner.SpawnLocationIsHome = true;
        foreach (var role in roles)
            spawner.AddEntry(nameof(DeceitEncounter), probability: 25, dotimer: false, parameters: role.ToString());
    }
}
