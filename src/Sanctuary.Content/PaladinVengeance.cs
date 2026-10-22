// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public readonly record struct VengeanceElements(int Base256,int Fire256,int Cold256,int Lightning256,int ColdFrames)
{
    public GearDamage Damage=>new(0,Fire256/256.0,Cold256/256.0,Lightning256/256.0,0,ColdMs:ColdFrames*40);
}

public static class PaladinVengeance
{
    internal static Func<int,int> Roll=Utility.Random;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> PhysicalRoll=Utility.RandomDouble;
    public static bool WeaponAllowed(PlayerMobile owner)
    {
        var weapon=CombatTiming.Equipped(owner);
        if(weapon==null){return true;}
        if(CombatTiming.Weapon(weapon).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw){return false;}
        // Converted throwing bases currently expose missile damage; do not reuse it for melee.
        return ClassGear.Base(weapon) is not{} basis || ClassGear.Definition(weapon).Category!="Throwing" && EquipmentBalance.IsType(basis.Type,"mele");
    }
    public static bool ValidTarget(PlayerMobile owner,Serial selected)
    {
        var target=World.FindMobile(selected);
        return owner.Map!=null && owner.Map!=Map.Internal && WeaponAllowed(owner) && ClassCombat.IsHostile(owner,target)
            && owner.InRange(target,CombatTiming.Range(owner,CombatAbility.Vengeance)) && owner.InLOS(target)
            && Math.Abs(owner.Z-target.Z)<16 && SpellHelper.CheckTown(owner.Location,owner) && SpellHelper.CheckTown(target.Location,owner);
    }
    internal static int Scale(int basis256,int percent,int mastery)
    {
        long enhanced=(long)percent+(long)percent*mastery/100;
        return(int)Math.Clamp((long)basis256*enhanced/100,0,int.MaxValue);
    }
    public static VengeanceElements Elements(PlayerMobile owner,SkillValues values)
    {
        var raw=ClassStats.WeaponBaseDamage(owner);
        // Ordinary weapon endpoints project item_normaldamage for physical attacks.
        // Do35 reads min/max without that separate stat for its elemental roll.
        int normal=ClassGear.Bonus(owner,"normal-damage");
        int min=Math.Max(1,raw.Min-normal)*256,max=Math.Max(2,raw.Max-normal)*256;
        if(max<=min){max=min+256;}
        int basis=min+Math.Clamp(Roll(max-min),0,max-min-1);
        return new(basis,Scale(basis,(int)values.Eval("calc1"),NamedElements.MasteryPercent(owner,"fire")),
            Scale(basis,(int)values.Eval("calc2"),NamedElements.MasteryPercent(owner,"cold")),
            Scale(basis,(int)values.Eval("calc3"),NamedElements.MasteryPercent(owner,"ltng")),values.DurationFrames);
    }
    public static bool Strike(PlayerMobile owner,Mobile target,SkillValues values)
    {
        owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing weapon sound placeholder.
        using var attempted=PaladinThorns.Attempt(owner,target,false);
        if(ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll)!=true){return true;}
        var bonus=Elements(owner,values);
        GearEffects.Strike(owner,target,PhysicalRoll(),skillElements:bonus.Damage);
        if(target is{Alive:true,Deleted:false}){target.FixedEffect(0x374A,10,10,0x480,0);} // Placeholder elemental strike.
        return true;
    }
}
