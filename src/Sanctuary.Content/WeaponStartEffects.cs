// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

// Direct hit/setup effects are not registered release-time damage events.
// Initially used by Poison Dagger; other weapon producers retain their own
// integration boundary until individually migrated.
internal static class WeaponStartEffects
{
    private static readonly Lazy<HashSet<string>> Immune=new(()=>
    {
        var rows=BalanceCatalog.Read<string>("prevent-heal-immune-monsters");var values=new HashSet<string>(rows,StringComparer.Ordinal);
        if(rows.Length!=9||values.Count!=rows.Length){throw new InvalidDataException("Invalid pinned Prevent Monster Heal immunity rules");}return values;
    });
    internal static void Require(PlayerMobile source)
        =>RequireArmorDelta(ClassGear.Bonus(source,"dmg-ac"));
    internal static void RequireArmorDelta(int delta)
    {
        if(delta>0){throw new NotSupportedException("Positive target-armor mutation needs its persistent stat adapter");}
        _=checked(-delta);
    }
    internal static bool PreventHealAllowed(string source)=>source==null||!Immune.Value.Contains(source);
    // Called after the hit roll succeeds, before shield blocking. Native
    // encounter archetypes retain their existing no-source-species policy.
    internal static void SuccessfulHitRoll(PlayerMobile source,BaseCreature target)
    {
        if(ClassGear.Bonus(source,"noheal")>0&&PreventHealAllowed(CreatureProfiles.Definition(target).Reference))
        {TimedEffects.PreventHealing(target,TimedEffects.Now);}
    }
    // FillDamageValues mutates target base armor only for an allocated damage
    // packet: after hit/block selection and before storing the weapon packet.
    internal static void PrepareDamage(PlayerMobile source,BaseCreature target)
        =>ApplyArmorDelta(target,ClassGear.Bonus(source,"dmg-ac"),TimedEffects.Now);
    internal static void ApplyArmorDelta(BaseCreature target,int delta,DateTime at)
    {
        RequireArmorDelta(delta);
        if(delta<0){TimedEffects.ReduceDefense(target,checked(-delta),at);}
    }
}
