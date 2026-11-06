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
    internal static Func<double> HitRoll=Utility.RandomDouble;
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
    internal static bool Ready(PlayerMobile owner,BaseCreature target)
    {
        try
        {
            PlayerWeaponContact.Require(owner,target,true,false);
            if(PlayerWeaponContact.Metadata(target,true)!=null&&!SourceBlocking.TryRate(target,out _,out _))
            {throw new NotSupportedException("Smite recipient shield rule is unavailable");}
            return true;
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
        =>PaladinCombat.Available(owner,CombatAbility.Smite)&&ValidTarget(owner,selected)&&World.FindMobile(selected) is BaseCreature target&&Ready(owner,target)
            &&SkillCombat.Begin(owner,CombatAbility.Smite,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id));
    private sealed class Result(PlayerMobile owner,BaseCreature target,bool native):SourceMeleeResultServices
    {
        internal override int? AttackerKind=>0;
        internal override int? DefenderKind=>1;
        internal override bool Hostile()=>ClassCombat.IsHostile(owner,target);
        internal override bool DefenderRunning()=>throw new InvalidOperationException("NPC is not a running player");
        internal override int MeleeReach()=>throw new NotSupportedException("Smite uses zero range offset");
        internal override bool InRange(int bonus)=>bonus==1&&ValidTarget(owner,target.Serial);
        internal override bool HitSuccessful(int percent,bool missile)
        {
            if(missile||percent!=0){throw new InvalidOperationException("Smite requests ordinary melee AR with no skill bonus");}
            bool hit=ClassStats.CheckHit(owner,target,0,HitRoll,notifyMelee:false,npcDefense:false)==true;
            if(hit){WeaponStartEffects.SuccessfulHitRoll(owner,target);}return hit;
        }
        internal override int BlockOrDodge(bool avoid,bool block)
        {
            if(native){return 0;} // Retained native UO actor policy;no source shield component is invented.
            if(!NpcBlockOrDodge.Try(target,avoid,block,out int flags,out _))
            {throw new NotSupportedException("Smite recipient shield rule is unavailable");}
            return flags;
        }
        internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(target);
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
        if(!Ready(owner,target)){return false;}
        owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing sound/weapon animation placeholders; S1 deadlines are distinct.
        var range=DamageRange256(owner,values);int span=Math.Max(1,range.Max-range.Min);
        int raw=range.Min+Math.Clamp(Roll(span),0,span-1);
        // Do150 rolls the ordinary result after its shield damage,then forces
        // success while retaining block/miss result bits. HitFlags2 skips
        // ordinary weapon filling;neither weapon critical nor leech is added.
        int flags=SourceMeleeResult.Resolve(new Result(owner,target,PlayerWeaponContact.Metadata(target,true)==null))|1|SkillMechanics.For(CombatAbility.Smite).Number("ResultFlags");
        var parts=SkillCombat.MitigatePrepared(owner,target,new(NamedElements.Enhance(owner,raw/256.0,"physical"),0,0,0,0),out int absorbed);
        var impact=new PreparedWeaponImpact(new(raw/256.0,0,0,0,0),parts,absorbed){SourceStunFrames=(int)values.Eval("calc2")};
        return PlayerWeaponContact.Release(owner,target,new(flags,impact,default,0,0,Effects,0x65,Smite:true,StunRoll:CombatStuns.Roll,MeleeRange:Range(owner)),TimedEffects.Now,true).Exit==MonsterContactExit.Finished;
    }
}
