// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Approved UO presentation only. The summon keeps its D2R source species,
// stats, footprint, AI and skill clocks instead of inheriting a native mage.
internal sealed record WarlockDemonAppearance(int Body,int Sound,string NativeName)
{
    internal static WarlockDemonAppearance For(CombatAbility ability)=>ability switch
    {
        CombatAbility.SummonGoatman=>new(4,372,"Gargoyle"),
        CombatAbility.SummonDefiler=>new(22,377,"Gazer"),
        CombatAbility.SummonTainted=>new(149,0x4B0,"Succubus"),
        _=>throw new NotSupportedException("No approved Warlock demon appearance")
    };
}
