// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal static class NpcItemFear
{
    internal static int Bonus(BaseCreature source)=>NpcEquipmentCombat.EventBonus(source,"howl");
    internal static bool Apply(BaseCreature source,BaseCreature target,DateTime at,Func<uint> raw=null)
    {
        if(source==null||target==null||target is EncounterCreature{Grade:EncounterGrade.Unique or EncounterGrade.Champion}){return false;}
        int bonus=Bonus(source);if(bonus<=0){return false;}
        if(source is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false}}
            ||at.Kind!=DateTimeKind.Utc){throw new NotSupportedException("NPC item fear needs its original owned source and UTC event");}
        uint draw=(raw??(()=>NpcEquipmentRandom.Raw(source)))();
        if((draw&127)>=bonus){return false;}
        // Positive callback result does not depend on the helper accepting the
        // target; dead/AI-immune/protected recipients still consume this draw.
        if(target.Map==source.Map&&source.Map!=Server.Map.Internal)
        {_=SourceTerror.ApplyItem(source,target,at);}
        return true;
    }
}
