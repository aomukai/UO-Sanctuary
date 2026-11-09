// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Live projection for the two states read by monster event delivery and AI
// preprocessing. This is not a complete source stat-root or state catalog.
// Native Frozen/Paralyzed flags and the post-freeze AI wait are separate.
internal static class GenericPetNativeControlStates
{
    internal const uint Mask=2u|0x200000u;
    private static int Count(byte selector)
    {
        if(selector!=1){throw new NotSupportedException("Native control states require the qualified selector1 view");}
        var table=GenericPetMetadata.Table;
        if(table?.State(1)?.Name!="freeze"||table.State(21)?.Name!="stunned")
        {throw new NotSupportedException("Native control-state identities do not match pinned metadata");}
        return table.StateCount;
    }
    internal static uint Read(BaseCreature actor)
    {
        if(actor is not{Alive:true,Deleted:false}){throw new NotSupportedException("Control-state projection requires a live original actor");}
        return (NamedControl.FrozenRemaining(actor)>0?2u:0u)|(CombatStuns.Remaining(actor)>0?0x200000u:0u);
    }
    internal static GenericPetMonsterEventState Events(BaseCreature actor,byte selector,uint flags,int mode)
        =>new(selector,Count(selector),true,-1,Read(actor),flags,mode);
    internal static GenericPetAiPreUnit Preprocess(BaseCreature actor,GenericPetAiPreUnit original)
    {
        ArgumentNullException.ThrowIfNull(original);int count=Count(original.Selector);
        if(original.StateCount!=count||original.HasRoot&&original.Root1c>=0)
        {throw new NotSupportedException("Cannot merge control states into an unqualified source root");}
        return original with{HasRoot=true,Root1c=-1,Rootb30=(original.Rootb30&~Mask)|Read(actor)};
    }
}
