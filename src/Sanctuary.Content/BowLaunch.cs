// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record WeaponPhysicalSupplement(int Min256,int Max256)
{
    internal int Roll(Func<int,int> random)
    {
        if(Max256<=0){return 0;}
        int min=Math.Min(Min256,Max256),max=Math.Max(Min256,Max256),span=max-min;
        return min+(span>0?Math.Clamp(random(span),0,span-1):0);
    }
}

// Launch collection has no input, mana, quantity or item-event side effects.
// The caller supplies the separate per-created-missile critical result.
public static class BowLaunch
{
    internal static WeaponMissileDamage FromSource(int weaponMin256,int weaponMax256,int skillMin256,int skillMax256,
        int sourceDamage,int ownerPercent,int missilePercent,int demonPercent,int undeadPercent,bool critical,MissileSourceStats elements)
    {
        if(sourceDamage is <0 or >128){throw new ArgumentOutOfRangeException(nameof(sourceDamage));}
        int Scale(int value)=>checked((int)((long)value*sourceDamage/128));
        bool weapon=sourceDamage!=0;
        return new(checked(skillMin256+Scale(weaponMin256)),checked(skillMax256+Scale(weaponMax256)),
            Math.Max(-90,missilePercent+(weapon?ownerPercent:0)),weapon?demonPercent:0,weapon?undeadPercent:0,
            weapon && critical,weapon?elements.Scale(sourceDamage):new());
    }
    internal static bool WeaponAllowed(PlayerMobile owner)=>BowAmmunition.AmmoType(owner,CombatTiming.Equipped(owner))!=null;
    private static double MaterialReference(PlayerMobile owner,BaseWeapon weapon,double roll,bool physical,int skillPercent)
    {
        var basis=ClassGear.Base(weapon);bool two=WeaponHands.TwoHanded(weapon);
        int min=two && basis.TwoHandMax>0?basis.TwoHandMin:basis.MinDamage,max=two && basis.TwoHandMax>0?basis.TwoHandMax:basis.MaxDamage;
        int percent=physical?Math.Max(10,100+ClassStats.PhysicalDamageBonus(owner,weapon)+skillPercent)
            :Math.Max(0,100+ClassStats.DamageBonus(owner,weapon)-PaladinAuras.DamagePercent(owner));
        return (min+(max-min)*roll)*percent/10000.0;
    }
    internal static MissileSourceStats Poison(PlayerMobile owner,BaseWeapon weapon)
    {
        long minimum=0,maximum=0;int frames=0,count=0;
        foreach(var source in NamedGear.PoisonSources(owner))
        {minimum+=source.Minimum;maximum+=source.Maximum;frames+=source.Frames;count++;}
        foreach(var source in GemCombat.PoisonSources(weapon))
        {
            if(source.DurationMs%40!=0){throw new NotSupportedException("Socket poison duration has no exact source-frame projection.");}
            // Sanctuary gem poison currently uses its fixed Minimum total.
            int rate=(int)((long)source.Minimum*40*256/source.DurationMs);
            minimum+=rate;maximum+=rate;frames+=source.DurationMs/40;count++;
        }
        int material=ClassGear.Material(weapon,"damage-poison");
        if(material>0)
        {
            minimum+=NamedElements.To256(MaterialReference(owner,weapon,0,false,0)*material/75);
            maximum+=NamedElements.To256(MaterialReference(owner,weapon,1,false,0)*material/75);
            frames+=75;count++;
        }
        int mastery=NamedElements.MasteryPercent(owner,"pois");
        int Enhanced(long value)=>checked((int)Math.Max(0,value+value*mastery/100));
        return new(PoisonMin:Enhanced(minimum),PoisonMax:Enhanced(maximum),PoisonFrames:frames,PoisonCount:count);
    }
    private static int Element(PlayerMobile owner,double value,string element,double roll)
    {
        double aura=PaladinElements.Weapon(owner,element,roll);
        return NamedElements.To256(NamedElements.Enhance(owner,value-aura,element)+aura);
    }
    internal static MissileSourceStats ExplosionFire(PlayerMobile owner,SkillValues values)
    {
        // Child creation reads live owner stats, independent of the bow that
        // launched its parent. Its later fire-only getter ignores other lanes.
        var low=GearEffects.Damage(owner,0);var high=GearEffects.Damage(owner,1);
        int mastery=NamedElements.MasteryPercent(owner,"fire");
        return new(FireMin:checked(Element(owner,low.Fire,"fire",0)+AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery)),
            FireMax:checked(Element(owner,high.Fire,"fire",1)+AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery)));
    }
    internal static MissileSourceStats ExplosionCold(PlayerMobile owner,SkillValues values)
    {
        // The child creator reads current owner/item stats, retaining its
        // parent's rank. The freeze-only getter excludes all other channels.
        var low=GearEffects.Damage(owner,0);var high=GearEffects.Damage(owner,1);
        int mastery=NamedElements.MasteryPercent(owner,"cold");
        return new(ColdMin:checked(Element(owner,low.Cold,"cold",0)+AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery)),
            ColdMax:checked(Element(owner,high.Cold,"cold",1)+AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery)),
            ColdFrames:checked(low.ColdMs/40+values.DurationFrames));
    }
    internal static WeaponMissileDamage Capture(PlayerMobile owner,CombatAbility ability,bool critical,int? missilePercent=null,SkillValues preparedValues=null)
    {
        if(ability is not(CombatAbility.MagicArrow or CombatAbility.MultipleShot or CombatAbility.FireArrow or CombatAbility.ColdArrow or CombatAbility.ExplodingArrow or CombatAbility.IceArrow or CombatAbility.GuidedArrow or CombatAbility.FreezingArrow or CombatAbility.ImmolationArrow or CombatAbility.Strafe))
        {throw new NotSupportedException("This bow collector requires a separately integrated skill elemental source.");}
        if(!WeaponAllowed(owner)){return null;}
        var weapon=CombatTiming.Equipped(owner);var values=preparedValues??SkillMechanics.Values(owner,ability);var row=SkillMechanics.For(ability);
        if(values.Rank<=0){return null;}
        int skillPercent=missilePercent??(ability==CombatAbility.MultipleShot?(int)values.Eval("calc5"):ability==CombatAbility.GuidedArrow?(int)values.Eval("calc1"):ability==CombatAbility.Strafe?(int)values.Eval("calc2"):0);
        int sourceDamage=row.Number("SrcDam");var raw=ClassStats.WeaponBaseDamage(owner);
        var low=GearEffects.Damage(owner,0);var high=GearEffects.Damage(owner,1);
        var poison=Poison(owner,weapon);
        var elements=poison with
        {
            FireMin=Element(owner,low.Fire,"fire",0),FireMax=Element(owner,high.Fire,"fire",1),
            LightningMin=Element(owner,low.Lightning,"ltng",0),LightningMax=Element(owner,high.Lightning,"ltng",1),
            ColdMin=Element(owner,low.Cold,"cold",0),ColdMax=Element(owner,high.Cold,"cold",1),
            MagicMin=Element(owner,low.Magic,"mag",0),MagicMax=Element(owner,high.Magic,"mag",1),ColdFrames=low.ColdMs/40,
            LifeDrain=Math.Max(0,ClassGear.Bonus(owner,"life-steal%")+CombatPotions.Bonus(owner,"stealing")),
            ManaDrain=Math.Max(0,ClassGear.Bonus(owner,"mana-steal%")+CombatPotions.Bonus(owner,"stealing"))
        };
        if(ability is CombatAbility.FireArrow or CombatAbility.ColdArrow or CombatAbility.IceArrow or CombatAbility.ImmolationArrow)
        {
            // Source skill endpoints include their own mastery before owner
            // stat addition. Captured prepared weapon fire is not enhanced twice.
            bool fire=ability is CombatAbility.FireArrow or CombatAbility.ImmolationArrow;int mastery=NamedElements.MasteryPercent(owner,fire?"fire":"cold");
            elements=fire?elements with
            {
                FireMin=checked(elements.FireMin+AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery)),
                FireMax=checked(elements.FireMax+AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery))
            }:elements with
            {
                ColdMin=checked(elements.ColdMin+AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery)),
                ColdMax=checked(elements.ColdMax+AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery)),
                ColdFrames=checked(elements.ColdFrames+values.DurationFrames)
            };
        }
        var damage=FromSource(raw.Min*256,raw.Max*256,values.PhysicalDamage256(false),values.PhysicalDamage256(true),sourceDamage,
            ClassStats.PhysicalDamageBonus(owner,weapon),skillPercent,ClassGear.Bonus(owner,"dmg-demon%"),ClassGear.Bonus(owner,"dmg-undead%"),critical,elements);
        if(ability==CombatAbility.MagicArrow)
        {damage=damage with{PhysicalToMagicPercent=Math.Min(100,NecromancerSummons.Source("magicarrow","dParam1")+(values.Rank-1)*NecromancerSummons.Source("magicarrow","dParam2"))};}
        if(ability==CombatAbility.FireArrow)
        {damage=damage with{Conversion=new("fire",Math.Min(100,NecromancerSummons.Source("firearrow","dParam1")+(values.Rank-1)*NecromancerSummons.Source("firearrow","dParam2")))};}
        if(ability==CombatAbility.ColdArrow)
        {damage=damage with{Conversion=new("cold",Math.Min(100,NecromancerSummons.Source("coldarrow","dParam1")+(values.Rank-1)*NecromancerSummons.Source("coldarrow","dParam2")))};}
        if(ability==CombatAbility.IceArrow){damage=damage with{FreezeColdLength=true};}
        int material=ClassGear.Material(weapon,"damage-physical"),potion=CombatPotions.Bonus(owner,"deep-wounds");
        if(material!=0 || potion!=0)
        {
            int Extra(double roll)=>checked(NamedElements.To256(MaterialReference(owner,weapon,roll,true,skillPercent)*material+potion)*sourceDamage/128);
            damage=damage with{PhysicalSupplement=new(Extra(0),Extra(1))};
        }
        if(ability==CombatAbility.Strafe){damage=damage with{SkillAttackPercent=(int)values.Formula("toht")};}
        return damage;
    }
}
