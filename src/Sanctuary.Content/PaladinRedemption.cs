// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class PaladinRedemption
{
    internal static Func<int> RollPercent=static()=>Utility.Random(100);
    internal static bool Redeem(PlayerMobile owner,Corpse corpse,SkillValues values,int roll)
    {
        if(roll is <0 or >99){throw new ArgumentOutOfRangeException(nameof(roll));}
        if(SpellHelper.IsTown(owner.Location,owner) || corpse!=null && SpellHelper.IsTown(corpse.Location,owner) || !CorpseSkillRules.Usable(owner,corpse,SkillGeometry.Tiles(values.Eval("aurarangecalc")))
            || roll>=values.Eval("calc1")){return false;}
        // Reserve the one-use corpse before applying either resource gain; retained loot stays in its container.
        NecromancerCombat.Consume(corpse);
        SkillHealth.Heal(owner,(int)values.Eval("calc2")*256);
        NecromancerEffects.AddMana(owner,(int)values.Eval("calc3")*256);
        owner.FixedEffect(0x375A,10,10,0x481,0); // Placeholder redemption/healing effect.
        return true;
    }
    public static void Pulse(PlayerMobile owner,SkillValues values)
    {
        if(SpellHelper.IsTown(owner.Location,owner)){return;}
        double radius=SkillGeometry.Tiles(values.Eval("aurarangecalc"));
        using var corpses=Server.Collections.PooledRefList<Corpse>.Create();
        foreach(var corpse in owner.Map.GetItemsInRange<Corpse>(owner.Location,(int)Math.Ceiling(radius)))
        {if(CorpseSkillRules.Usable(owner,corpse,radius)){corpses.Add(corpse);}}
        foreach(var corpse in corpses){Redeem(owner,corpse,values,RollPercent());}
    }
}
