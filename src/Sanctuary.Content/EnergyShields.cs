// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;

namespace Sanctuary;

// Event24's fields are distinct from GearDamage. The last three are source
// monster drain fields, not poison damage or ordinary weapon leech.
// Integer damage, mana and absorption values in this module use FP256.
internal sealed record EnergyShieldDamage(int Physical,int Fire,int Lightning,int Cold,int Magic,
    int LifeDrain=0,int ManaDrain=0,int StaminaDrain=0);
internal sealed record EnergyShieldResult(EnergyShieldDamage Damage,int Mana,int Absorbed);

internal static class EnergyShields
{
    internal static EnergyShieldState Prepare(SkillValues values,DateTime at)
    {
        var row=SkillMechanics.For(CombatAbility.EnergyShield);
        if(values.Ability!=CombatAbility.EnergyShield||values.Rank is <1 or >255||at.Kind!=DateTimeKind.Utc
            ||row.SourceId!=58||row.Number("srvdofunc")!=23||row.Text("aurastate")!="energyshield"
            ||row.Text("auraevent1")!="absorbdamage"||row.Number("auraeventfunc1")!=24
            ||values.Mana256!=1280||values.Eval("auralencalc")<=0)
        {throw new NotSupportedException("Unsupported pinned Energy Shield cast");}
        int percent=(int)values.Eval("calc1");
        if(percent is <1 or >95){throw new NotSupportedException("Unsupported Energy Shield absorption");}
        return new(at.AddMilliseconds(values.Eval("auralencalc")*40),percent){Rank=values.Rank};
    }
    private static bool CanApply(PlayerMobile owner,EnergyShieldState prepared)
        =>SorceressEffects.Shield(owner) is not{} old||old.Rank==0||old.Rank<=prepared.Rank;
    internal static bool Apply(PlayerMobile owner,EnergyShieldState prepared)
    {
        if(!CanApply(owner,prepared)){return false;}
        SorceressEffects.Profiles.Store(owner,(SorceressEffects.Profiles.Find(owner)??new()) with{Shield=prepared});
        owner.FixedEffect(0x375A,10,15,0x482,0); // Identified mana-shield placeholder.
        return true;
    }
    internal static bool Release(PlayerMobile owner,SkillValues values)
    {
        var prepared=Prepare(values,TimedEffects.Now);
        // The shared state-list helper refuses a weaker same-skill rank.
        // Refusal cannot replace/extend the old list or charge a new cast.
        if(!CanApply(owner,prepared)||!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        if(!Apply(owner,prepared)){throw new InvalidOperationException("Energy Shield state changed during synchronous release");}
        owner.PlaySound(0x1E5);return true; // Identified lightning-cast placeholder.
    }
    internal static EnergyShieldResult Calculate(EnergyShieldDamage packet,int mana,int percent,int divisor,bool drainEligible)
    {
        // Keep source percentage intermediates and accumulated absorption in
        // their defined signed-int domain. Unsupported extreme stat totals
        // must not wrap into healing or manufacture mana.
        if(mana<0||mana>(int.MaxValue-120)/16||percent is <0 or >95)
        {throw new NotSupportedException("Energy Shield exceeds the supported source integer domain");}
        if(percent==0){return new(packet,mana,0);}
        divisor=Math.Max(1,divisor);long absorbed=0;
        int Part(int value)
        {
            if(value<=0){return value;}
            int amount=Math.Min(SourcePercentage.Calculate(value,percent,100),SourcePercentage.Calculate(mana,16,divisor));
            mana=Math.Max(0,mana-SourcePercentage.Calculate(amount,divisor,16));absorbed+=amount;return value-amount;
        }
        // Evaluation order is the actual source table order; all fields spend
        // one shared mana balance, including the separately supplied drains.
        int physical=Part(packet.Physical),fire=Part(packet.Fire),lightning=Part(packet.Lightning),cold=Part(packet.Cold),magic=Part(packet.Magic);
        int life=drainEligible?Part(packet.LifeDrain):packet.LifeDrain;
        int drain=drainEligible?Part(packet.ManaDrain):packet.ManaDrain;
        int stamina=drainEligible?Part(packet.StaminaDrain):packet.StaminaDrain;
        return new(new(physical,fire,lightning,cold,magic,life,drain,stamina),mana,checked((int)absorbed));
    }
    internal static GearDamage Absorb(PlayerMobile owner,GearDamage damage,EnergyShieldState shield)
    {
        int rank=shield.Rank;
        var values=rank>0?SkillMechanics.Values(owner,CombatAbility.EnergyShield,rank):null;
        int percent=values==null?shield.AbsorbPercent:(int)values.Eval("calc1");
        int divisor=values==null?SkillMechanics.For(CombatAbility.EnergyShield).Parameter(5)
            -ClassCreation.Profiles.GetRank(owner.Serial,"sorceress.telekinesis"):(int)values.Eval("calc2");
        int Raw(double value)
        {
            if(!double.IsFinite(value)){throw new NotSupportedException("Nonfinite Energy Shield component");}
            return (int)Math.Clamp(value*256,int.MinValue,int.MaxValue);
        }
        var result=Calculate(new(Raw(damage.Physical),Raw(damage.Fire),Raw(damage.Lightning),Raw(damage.Cold),Raw(damage.Magic)),
            NecromancerEffects.ManaBalance256(owner),percent,divisor,false);
        if(percent==0){return damage;}
        NecromancerEffects.SetMana256(owner,result.Mana);
        if(result.Mana==0){SorceressEffects.Profiles.Store(owner,SorceressEffects.Profiles.Find(owner) with{Shield=null});}
        // GearDamage supplies no source drain fields or attacking-unit
        // classification. Do not fabricate them from poison/leech. Its five
        // eligible damage lanes are independent of that source qualification.
        return damage with{Physical=result.Damage.Physical/256.0,Fire=result.Damage.Fire/256.0,
            Lightning=result.Damage.Lightning/256.0,Cold=result.Damage.Cold/256.0,Magic=result.Damage.Magic/256.0};
    }
}
