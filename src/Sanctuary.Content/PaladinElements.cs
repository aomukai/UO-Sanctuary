// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Collections;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class PaladinElements
{
    public static bool Handles(CombatAbility ability)=>ability is CombatAbility.HolyFire or CombatAbility.HolyShock or CombatAbility.HolyFreeze;
    internal static Func<int,int> Roll=static range=>Utility.Random(range);
    public static string Element(CombatAbility ability)=>ability==CombatAbility.HolyFire?"fire":ability==CombatAbility.HolyFreeze?"cold":"ltng";
    public static (int Min,int Max) WeaponRange(PlayerMobile owner,CombatAbility ability,SkillValues values)
    {
        string element=Element(ability);
        int min=(int)Math.Clamp(NamedElements.Enhance(owner,values.Damage256(false),element),0,int.MaxValue);
        int max=(int)Math.Clamp(NamedElements.Enhance(owner,values.Damage256(true),element),0,int.MaxValue);
        return ((int)values.ElementalEval("passivecalc1",min,max),(int)values.ElementalEval("passivecalc2",min,max));
    }
    public static double Weapon(Mobile owner,string element,double roll)
    {
        if(element is not ("fire" or "ltng" or "cold")){return 0;}
        var lease=PaladinAuras.Effect(owner,element=="fire"?CombatAbility.HolyFire:element=="cold"?CombatAbility.HolyFreeze:CombatAbility.HolyShock);
        return lease==null?0:lease.WeaponMin+(lease.WeaponMax-lease.WeaponMin)*Math.Clamp(roll,0,1);
    }
    public static bool WeaponActive(Mobile owner)=>PaladinAuras.Effect(owner,CombatAbility.HolyFire)!=null || PaladinAuras.Effect(owner,CombatAbility.HolyShock)!=null || PaladinAuras.Effect(owner,CombatAbility.HolyFreeze)!=null;
    public static double WeaponForHit(Mobile owner,string element,double roll,bool ranged)
    {
        double value=Weapon(owner,element,roll);
        return ranged?value:NamedElements.Enhance(owner,value,element);
    }
    // Explicit Sanctuary tile projection of the released 2.4 rule. The modern engine's
    // distance metric, unit-size melee boundary and integer operation order remain unverified.
    public static double Proximity(double distance,double radius)=>radius<=1?2:1+Math.Clamp((radius-distance)/(radius-1),0,1);
    internal static void Pulse(PlayerMobile owner,CombatAbility ability,SkillValues values,DateTime now)
    {
        int frames=Math.Max(5,(int)values.Eval("perdelay"));var range=WeaponRange(owner,ability,values);
        PaladinAuras.ElementalWeapon(new(owner.Serial.Value,owner.Serial.Value,ability,values.Rank,0,now.AddMilliseconds((frames+1)*40),WeaponMin:range.Min,WeaponMax:range.Max));
        if(SpellHelper.IsTown(owner.Location,owner)){return;}
        string element=Element(ability);int min=(int)Math.Clamp(NamedElements.Enhance(owner,values.Damage256(false),element),0,int.MaxValue),max=(int)Math.Clamp(NamedElements.Enhance(owner,values.Damage256(true),element),0,int.MaxValue);
        // The legacy routine rolls once before enumerating recipients; the upper endpoint is exclusive.
        int damage=min+(max>min?Roll(max-min):0);
        double radius=SkillGeometry.Tiles(values.Eval("aurarangecalc"));
        using var targets=PooledRefList<Mobile>.Create();
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,(int)Math.Ceiling(radius)))
        {
            double dx=target.X-owner.X,dy=target.Y-owner.Y;
            if((ability!=CombatAbility.HolyFreeze || PaladinHolyFreeze.Eligible(target,out _)) && ClassCombat.IsHostile(owner,target) && Math.Abs(target.Z-owner.Z)<16 && dx*dx+dy*dy<=radius*radius
                && !SpellHelper.IsTown(target.Location,target)){targets.Add(target);}
        }
        foreach(var target in targets)
        {
            if(owner is not {Alive:true,Deleted:false}){break;}
            if(target is not {Alive:true,Deleted:false}){continue;}
            double dx=target.X-owner.X,dy=target.Y-owner.Y;
            if(ability==CombatAbility.HolyFreeze){PaladinHolyFreeze.Slow(owner,target,values,now);}
            SkillCombat.AuraElement(owner,target,(int)Math.Clamp(Math.Floor(damage*Proximity(Math.Sqrt(dx*dx+dy*dy),radius)),0,int.MaxValue),element);
            // The legacy callback executes damage before rerolling the target shatter marker.
            if(ability==CombatAbility.HolyFreeze){PaladinHolyFreeze.RollShatter(target);}
        }
        owner.FixedEffect(0x375A,10,10,ability==CombatAbility.HolyFire?0x489:0x481,0); // Placeholder elemental aura pulse.
    }
}
