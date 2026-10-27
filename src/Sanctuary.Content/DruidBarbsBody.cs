// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
namespace Sanctuary;

[SerializationGenerator(0)]
public partial class DruidBarbsBody:DruidOakBody
{
    internal override CombatAbility SpiritAbility=>CombatAbility.SpiritOfBarbs;
    internal override void ScheduleAura(DateTime at)=>DruidWolverineAuras.Schedule(this,at);
    internal override void AdvanceAura(DateTime at)=>DruidWolverineAuras.Advance(this,at);
    internal override void StopAura()=>DruidWolverineAuras.Stop(this);
    public DruidBarbsBody()
    {
        Name="Spirit of Barbs [placeholder]";Hue=0x48D;SetHits(213);
        // Silent UO wisp/violet hue is a labelled placeholder for final X4 art.
    }
}
