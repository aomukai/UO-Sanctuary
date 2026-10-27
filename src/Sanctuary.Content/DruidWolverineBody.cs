// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
namespace Sanctuary;

// Runtime type supplies the spirit identity; the inherited saved birth and
// controller schema remains compatible with existing Oak bodies.
[SerializationGenerator(0)]
public partial class DruidWolverineBody:DruidOakBody
{
    internal override CombatAbility SpiritAbility=>CombatAbility.HeartOfWolverine;
    internal override void ScheduleAura(DateTime at)=>DruidWolverineAuras.Schedule(this,at);
    internal override void AdvanceAura(DateTime at)=>DruidWolverineAuras.Advance(this,at);
    internal override void StopAura()=>DruidWolverineAuras.Stop(this);
    public DruidWolverineBody()
    {
        Name="Heart of Wolverine [placeholder]";Hue=0x21;SetHits(136);
        // Silent UO wisp/red hue is an identified placeholder, not X3 art.
    }
}
