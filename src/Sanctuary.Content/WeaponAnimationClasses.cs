// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

public sealed record WeaponAnimationClass(string Code,string OneHanded,string TwoHanded);

// Animation identity is source weapon metadata, independent of whether that
// base has a named item. Other action consumers can reuse this catalogue.
public static class WeaponAnimationClasses
{
    private static readonly Dictionary<string,WeaponAnimationClass> _rows=new(StringComparer.Ordinal);
    public static void Configure()
    {
        _rows.Clear();
        foreach(var row in BalanceCatalog.Read<WeaponAnimationClass>("weapon-animation-classes")){_rows.Add(row.Code,row);}
    }
    internal static string Equipped(PlayerMobile owner)
    {
        var weapon=CombatTiming.Equipped(owner);
        if(weapon==null||weapon.Type==WeaponType.Fists){return "HTH";}
        if(ClassGear.Base(weapon) is not{} basis||!_rows.TryGetValue(basis.Code,out var row)){return null;}
        return basis.OneOrTwoHanded||WeaponHands.TwoHanded(weapon)?row.TwoHanded:row.OneHanded;
    }
}
