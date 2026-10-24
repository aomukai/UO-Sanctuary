// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;

namespace Sanctuary;

// Shared source-stat collection for assassintrap creation. Nonzero sunder
// remains explicitly unsupported until its native immunity consumer is present.
internal static class AssassinTrapStats
{
    internal static int InheritedValue(string name,int value)
    {
        if(name.StartsWith("item_pierce_",StringComparison.Ordinal) && value!=0)
        {throw new NotSupportedException("Assassin trap inherited sunder consumer is not implemented: "+name);}
        return value;
    }
    internal static int Accrued(PlayerMobile owner,string name)
    {
        string code=name switch
        {
            "item_pierce_damage_immunity"=>"pierce-immunity-damage","item_pierce_fire_immunity"=>"pierce-immunity-fire",
            "item_pierce_light_immunity"=>"pierce-immunity-light","item_pierce_poison_immunity"=>"pierce-immunity-poison",
            "item_pierce_cold_immunity"=>"pierce-immunity-cold",
            "passive_fire_mastery"=>"extra-fire","passive_ltng_mastery"=>"extra-ltng",
            "passive_cold_mastery"=>"extra-cold","passive_pois_mastery"=>"extra-pois",
            "passive_fire_pierce"=>"pierce-fire","passive_ltng_pierce"=>"pierce-ltng",
            "passive_cold_pierce"=>"pierce-cold","passive_pois_pierce"=>"pierce-pois",
            _=>throw new NotSupportedException("Unmapped inherited trap stat: "+name)
        };
        return InheritedValue(name,ClassGear.Bonus(owner,code));
    }
}
