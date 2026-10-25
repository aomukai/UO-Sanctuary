// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct KickBootBase(string Code,int Min,int Max,int StrengthPercent,int DexterityPercent);
public readonly record struct KickDamageInput(int Strength,int Dexterity,int Level,bool Player,int SkillDamagePercent,
    KickBootBase? Boot,int FlatKickDamage,int DamagePercent,int MaxDamagePercent);

// Prepared physical component shared by the Dragon finishers. This does not
// register a cast, choose modern charge-consumption rules, or supply KK timing.
public static class DragonKickDamage
{
    private static readonly Dictionary<string,KickBootBase> Bases=new(StringComparer.Ordinal);
    public static void Configure()
    {
        Bases.Clear();foreach(var boot in BalanceCatalog.Read<KickBootBase>("kick-boots"))
        {
            if(string.IsNullOrEmpty(boot.Code) || boot.Min<0 || boot.Max<boot.Min || boot.StrengthPercent<0 || boot.DexterityPercent<0 || !Bases.TryAdd(boot.Code,boot))
            {throw new InvalidDataException("Invalid source kick boot base");}
        }
        if(Bases.Count==0){throw new InvalidDataException("Missing source kick boot bases");}
    }
    internal static KickBootBase Base(string code)=>Bases.TryGetValue(code,out var boot)?boot:throw new NotSupportedException("Unmapped source kick boot base: "+code);
    internal static (int Min,int Max) Body256(KickDamageInput input)
    {
        long basis=Math.Max(1,input.Player?(long)input.Strength+input.Dexterity-20:3L*input.Level);
        return(checked((int)(basis/4*256)),checked((int)(basis/3*256)));
    }
    internal static (int Min,int Max,int Percent) BootDamage(KickDamageInput input)
    {
        int min=input.FlatKickDamage,max=min,percent=input.SkillDamagePercent;
        if(input.Boot is{} boot)
        {
            // Literal old helper: the initial flat kick stat is included again
            // when boots exist. Current D2R corroboration is still outstanding.
            min=checked(min+input.FlatKickDamage+boot.Min);
            max=Math.Max(min,checked(max+input.FlatKickDamage+boot.Max));
            long stats=(long)boot.StrengthPercent*input.Strength/100+(long)boot.DexterityPercent*input.Dexterity/100;
            percent=checked((int)(percent+input.MaxDamagePercent+Math.Max(-90,stats+input.DamagePercent)));
        }
        return(min,max,percent);
    }
    private static int Enhance(int value,int percent)=>checked((int)(value+(long)value*percent/100));
    internal static (int Min,int Max) Range256(KickDamageInput input)
    {
        var body=Body256(input);var boot=BootDamage(input);
        return(checked(Enhance(body.Min,input.SkillDamagePercent)+Enhance(checked(boot.Min*256),boot.Percent)),
            checked(Enhance(body.Max,input.SkillDamagePercent)+Enhance(checked(boot.Max*256),boot.Percent)));
    }
    internal static int Roll256(KickDamageInput input,Func<int,int> random)
    {
        var range=Range256(input);int span=checked(range.Max-range.Min);
        if(span<0){throw new NotSupportedException("Unmapped inverted kick damage range");}
        int draw=(random??Utility.Random)(span);
        return checked(range.Min+(span==0?0:Math.Clamp(draw,0,span-1)));
    }
    internal static KickBootBase? Boots(PlayerMobile owner)
    {
        KickBootBase? result=null;
        foreach(var item in owner.Items)
        {
            if(ClassGear.Slot(item)!=GearSlot.Feet || !ClassGear.Active(owner,item)){continue;}
            if(result!=null){throw new NotSupportedException("Multiple active kick boot bases");}
            if(ClassGear.Base(item) is not{} basis || !ClassItemTypes.Is(basis.Type,"boot"))
            {throw new NotSupportedException("Unmapped kick footwear base");}
            result=Base(basis.Code);
        }
        return result;
    }
    internal static KickDamageInput Capture(PlayerMobile owner,int skillDamagePercent,int flatKickDamage=0)
    {
        if(owner==null){throw new ArgumentNullException(nameof(owner));}
        int itemPercent=NamedGear.SetBonus(owner,"dmg%");
        foreach(var item in owner.Items)
        {
            if(ClassGear.Slot(item)!=GearSlot.Weapon && ClassGear.Active(owner,item))
            {itemPercent=checked(itemPercent+ClassGear.Bonus(item,"dmg%"));}
        }
        // Weapon DamageRelated ED is suppressed for boot arithmetic, while
        // nondamage strength/dexterity still contribute. Later typed/leech fill
        // uses a different selected-primary context and is not supplied here.
        // Item ED stays outside the source global-ED lower clamp.
        return new(owner.Str,owner.Dex,ClassStats.Level(owner),true,skillDamagePercent,Boots(owner),flatKickDamage,
            PaladinAuras.DamagePercent(owner)+NecromancerEffects.DamageStatPercent(owner),itemPercent);
    }
}
