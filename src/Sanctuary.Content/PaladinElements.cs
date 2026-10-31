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
    public static (int Min,int Max) WeaponRange(Mobile owner,CombatAbility ability,SkillValues values)
    {
        string element=Element(ability);
        int min=Enhanced(owner,values.Damage256(false),element);
        int max=Enhanced(owner,values.Damage256(true),element);
        return ((int)values.ElementalEval("passivecalc1",min,max),(int)values.ElementalEval("passivecalc2",min,max));
    }
    private static int Enhanced(Mobile owner,int damage256,string element)
    {
        if(owner is FireGolemBody golem)
        {
            int mastery=NpcEquipmentCombat.Context(golem)?.Bonus("extra-"+element)??0;
            return checked(damage256+SourcePercentage.Calculate(damage256,mastery,100));
        }
        return (int)Math.Clamp(NamedElements.Enhance(owner,damage256,element),0,int.MaxValue);
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
        =>Pulse(owner,owner,ability,values,now,Roll);
    internal static void Pulse(Mobile owner,PlayerMobile allegiance,CombatAbility ability,SkillValues values,DateTime now,Func<int,int> roll)
    {
        if(owner is not(PlayerMobile or FireGolemBody)||owner is FireGolemBody&&(ability!=CombatAbility.HolyFire||owner.Map==null||owner.Map==Map.Internal||owner.Map!=allegiance.Map))
        {throw new NotSupportedException("Unmapped elemental aura emitter");}
        int frames=Math.Max(5,(int)values.Eval("perdelay"));var range=WeaponRange(owner,ability,values);
        PaladinAuras.ElementalWeapon(new(owner.Serial.Value,owner.Serial.Value,ability,values.Rank,0,now.AddMilliseconds((frames+1)*40),WeaponMin:range.Min,WeaponMax:range.Max));
        if(SpellHelper.IsTown(owner.Location,owner)){return;}
        string element=Element(ability);int min=Enhanced(owner,values.Damage256(false),element),max=Enhanced(owner,values.Damage256(true),element);
        // The legacy routine rolls once before enumerating recipients; the upper endpoint is exclusive.
        int damage=min+(max>min?roll(max-min):0);
        double radius=SkillGeometry.Tiles(values.Eval("aurarangecalc"));
        using var targets=PooledRefList<Mobile>.Create();
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,(int)Math.Ceiling(radius)))
        {
            double dx=target.X-owner.X,dy=target.Y-owner.Y;
            if((ability!=CombatAbility.HolyFreeze || PaladinHolyFreeze.Eligible(target,out _)) && ClassCombat.IsHostile(allegiance,target) && Math.Abs(target.Z-owner.Z)<16 && dx*dx+dy*dy<=radius*radius
                && !SpellHelper.IsTown(target.Location,target)){targets.Add(target);}
        }
        foreach(var target in targets)
        {
            if(owner is not {Alive:true,Deleted:false}){break;}
            if(target is not {Alive:true,Deleted:false}){continue;}
            double dx=target.X-owner.X,dy=target.Y-owner.Y;
            if(ability==CombatAbility.HolyFreeze){PaladinHolyFreeze.Slow((PlayerMobile)owner,target,values,now);}
            SkillCombat.AuraElement(owner,target,(int)Math.Clamp(Math.Floor(damage*Proximity(Math.Sqrt(dx*dx+dy*dy),radius)),0,int.MaxValue),element);
            // The legacy callback executes damage before rerolling the target shatter marker.
            if(ability==CombatAbility.HolyFreeze){PaladinHolyFreeze.RollShatter(target);}
        }
        owner.FixedEffect(0x375A,10,10,ability==CombatAbility.HolyFire?0x489:0x481,0); // Placeholder elemental aura pulse.
    }
}
