// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// Stat78 only. This joins the existing return bridge; it does not certify
// that bridge's incoming-HP/death/event order or add lightning item returns.
internal static class NpcEquipmentRetaliation
{
    internal static int IronAmount(SkillValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var row=SkillMechanics.For(CombatAbility.IronGolem);
        if(values.Ability!=CombatAbility.IronGolem||values.Rank is <1 or >255||row.Text("aurastate")!="thorns"
            ||row.Text("aurastat1")!="item_attackertakesdamage"||row.Text("aurastatcalc1")!="edmn")
        {throw new NotSupportedException("Unmapped Iron Golem flat retaliation source");}
        return checked((int)values.Eval("aurastatcalc1"));
    }
    internal static int Flat(Mobile target)
    {
        if(target is not SkeletonGuardian actor){return 0;}
        int inherent=actor is IronGolemBody{Prepared:{IntrinsicThornsReplaced:false}} iron?IronAmount(iron.BirthValues):0;
        return checked(inherent+NpcEquipmentCombat.Bonus(actor,"thorns"));
    }
}
