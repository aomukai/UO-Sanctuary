// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

internal readonly record struct NpcPhysicalStats(int Minimum,int Maximum,int NormalDamage,int DamagePercent,
    int Strength,int Dexterity,bool Weapon,int StrengthCoefficient=0,int DexterityCoefficient=0,
    int MinimumPercent=0,int MaximumPercent=0);

// Ordinary nonplayer SUNITDMG_ApplyDamageBonuses. Item-local ED has already
// reached the supplied endpoints; it must not become general actor ED too.
internal static class NpcEquipmentDamage
{
    internal static int Roll256(NpcPhysicalStats stats,Func<int,int> limited,int sourceDamage=128)
    {
        ArgumentNullException.ThrowIfNull(limited);
        if(sourceDamage is <0 or >255){throw new ArgumentOutOfRangeException(nameof(sourceDamage));}
        int low=checked((stats.Weapon?stats.Minimum:Math.Max(1,stats.Minimum))*256);
        int high=checked((stats.Weapon?stats.Maximum:Math.Max(2,stats.Maximum))*256);
        low=checked(low+stats.NormalDamage*256);high=checked(high+stats.NormalDamage*256);
        if(low<1){low=256;}if(high<=low){high=checked(low+256);}
        int percent=checked(stats.DamagePercent+(stats.Weapon
            ?ClassStats.WeaponStatPercent(stats.Strength,stats.Dexterity,stats.StrengthCoefficient,stats.DexterityCoefficient):stats.Strength));
        percent=Math.Max(-90,percent);
        low=checked(low+SourcePercentage.Calculate(low,checked(percent+stats.MinimumPercent),100));
        high=checked(high+SourcePercentage.Calculate(high,checked(percent+stats.MaximumPercent),100));
        int value=low;
        if(high>low)
        {
            int bound=checked(high-low),roll=limited(bound);
            if(roll<0||roll>=bound){throw new InvalidOperationException("NPC damage roll outside source range");}
            value=checked(value+roll);
        }
        value=Math.Max(0,value);
        return sourceDamage==128?value:SourcePercentage.Calculate(value,sourceDamage,128);
    }
    internal static NpcPhysicalStats? Capture(SkeletonGuardian actor,Mobile target=null)
    {
        if(NpcEquipmentCombat.Context(actor) is not{} item){return null;}
        var raw=actor.SourceWeaponBase;var basis=ClassGear.Base(item.Item);
        bool weapon=ClassGear.Slot(item.Item)==GearSlot.Weapon&&!ClassGear.Broken(item.Item);
        bool two=weapon&&(basis.TwoHanded||basis.OneOrTwoHanded);
        int normal=item.Bonus("normal-damage");
        var local=weapon?ClassGear.Damage(item.Item,item.RecipientLevel,two,true)
            :(item.Bonus("dmg-min"),item.Bonus("dmg-max"));
        int min=checked((two?0:raw.Min)+local.Item1-normal),max=checked((two?0:raw.Max)+local.Item2-normal);
        int percent=checked(raw.Percent+CombatAuras.DamagePercent(actor)+NecromancerEffects.DamageStatPercent(actor)
            +(weapon?0:item.Bonus("dmg%")));
        if(GemCombat.Undead(target)){percent=checked(percent+item.Bonus("dmg-undead%")+(weapon&&ClassItemTypes.Is(basis.Type,"blun")?50:0));}
        else if(NamedCombat.Demon(target)){percent=checked(percent+item.Bonus("dmg-demon%"));}
        return new(min,max,normal,percent,item.Bonus("str"),item.Bonus("dex"),weapon,
            weapon?basis.StrengthBonus:0,weapon?basis.DexterityBonus:0);
    }
    internal static bool TryResolve(SkeletonGuardian actor,Mobile target,out SkillHitResult result,Func<int,int> limited=null)
    {
        result=default;if(Capture(actor,target) is not{} stats){return false;}
        limited??=Utility.Random;
        int physical=Roll256(stats,limited);
        var snapshot=NpcEquipmentElements.Capture(actor);
        var elements=NpcEquipmentElements.Roll(snapshot,limited);
        result=SkillCombat.ResolveHitParts(actor,target,elements with{Physical=physical/256.0},true,true,
            enhanceBase:false,addWeaponElements:false,sourceColdFrames:snapshot.Stats.ColdFrames);
        return true;
    }
    internal static bool TryStrike(SkeletonGuardian actor,Mobile target)=>TryResolve(actor,target,out _);
}
