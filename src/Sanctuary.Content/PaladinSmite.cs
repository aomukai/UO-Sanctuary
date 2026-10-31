// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record SmiteShield(string Code,string Name,string Type,int MinDamage,int MaxDamage,int StrengthBonus,int DexterityBonus);
public static class PaladinSmite
{
    private static readonly Dictionary<string,SmiteShield> _shields=new(StringComparer.Ordinal);
    internal static Func<int,int> Roll=Utility.Random;
    internal static Random Effects=Random.Shared;
    public static void Configure()
    {
        foreach(var row in BalanceCatalog.Read<SmiteShield>("smite-shields"))
        {if(row.MinDamage<0 || row.MaxDamage<row.MinDamage){throw new InvalidOperationException("Invalid Smite shield damage");}_shields.Add(row.Code,row);}
    }
    public static SmiteShield Shield(PlayerMobile owner)=>HolyShields.Shield(owner) is{} shield && ClassGear.Base(shield) is{} basis
        ?_shields.GetValueOrDefault(basis.Code):null;
    public static int Range(PlayerMobile owner)=>CombatTiming.Weapon(CombatTiming.Equipped(owner)).Family==AttackFamily.Throw?1:CombatTiming.Range(owner,CombatAbility.BasicAttack);
    public static bool ValidTarget(PlayerMobile owner,Serial selected)
    {
        var target=World.FindMobile(selected);
        return owner.Map!=null && owner.Map!=Map.Internal && Shield(owner)!=null && ClassCombat.IsHostile(owner,target)
            && owner.InRange(target,Range(owner)) && owner.InLOS(target) && Math.Abs(owner.Z-target.Z)<16
            && SpellHelper.CheckTown(owner.Location,owner) && SpellHelper.CheckTown(target.Location,owner);
    }
    internal static (int Min,int Max) Endpoints256(int min256,int max256,int bonus,int normalDamage=0)
    {
        long min=min256+(long)normalDamage*256,max=max256+(long)normalDamage*256;
        if(min<1){min=256;}if(max<=min){max=min+256;}
        bonus=Math.Max(-90,bonus);min+=min*bonus/100;max+=max*bonus/100;
        return((int)Math.Clamp(min,0,int.MaxValue-1),(int)Math.Clamp(max,1,int.MaxValue));
    }
    public static (int Min,int Max) DamageRange256(PlayerMobile owner,SkillValues values)
    {
        var shield=Shield(owner)??throw new InvalidOperationException("Smite requires a supported active shield");
        var weapon=CombatTiming.Equipped(owner);var basis=weapon==null?null:ClassGear.Base(weapon);
        int weaponStats=basis==null?owner.Str:ClassStats.WeaponStatPercent(owner.Str,owner.Dex,basis.StrengthBonus,basis.DexterityBonus);
        int global=ClassStats.DamageBonus(owner,weapon)-weaponStats;
        int bonus=global+ClassStats.WeaponStatPercent(owner.Str,owner.Dex,shield.StrengthBonus,shield.DexterityBonus)
            +(int)values.Eval("calc1")+NecromancerEffects.DamageStatPercent(owner);
        var holy=HolyShields.SmiteBonus(owner);
        // Only item_normaldamage augments both shield endpoints before percentages;
        // ordinary flat min/max and weapon-local ED are not Smite bases.
        return Endpoints256(shield.MinDamage*256+holy.Min256,shield.MaxDamage*256+holy.Max256,bonus,ClassGear.Bonus(owner,"normal-damage"));
    }
    public static bool Strike(PlayerMobile owner,BaseCreature target,SkillValues values)
    {
        owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing sound/weapon animation placeholders; S1 deadlines are distinct.
        using var attempted=PaladinThorns.Attempt(owner,target,false);
        var range=DamageRange256(owner,values);int span=Math.Max(1,range.Max-range.Min);
        int raw=range.Min+Math.Clamp(Roll(span),0,span-1);
        var parts=SkillCombat.MitigatePrepared(owner,target,new(NamedElements.Enhance(owner,raw/256.0,"physical"),0,0,0,0),out int absorbed);
        int physical=NamedElements.To256(parts.Physical),before=target.Hits;
        NamedCombat.Crushing(owner,target,false,Effects);
        PreparedWeaponEvents.BeforeReturn(owner,target,physical,false,absorbed);
        if(parts.Total>0){SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));}
        int loss=Math.Max(0,before-target.Hits);
        CreatureHitRecovery.Apply(target,owner,loss,true);
        NecromancerEffects.WeaponHit(owner,target,Math.Min(loss,physical/256),false,lifeTap:false,ironMaiden:false);
        NamedCombat.AfterStrike(owner,target,false,Effects,smite:true);
        GemCombat.WeaponHit(owner,target,loss,false);
        if(target is{Alive:true,Deleted:false})
        {
            CombatStuns.Apply(target,(int)values.Eval("calc2"));
            SourceKnockbacks.Begin(owner,target);
        }
        return true;
    }
}
