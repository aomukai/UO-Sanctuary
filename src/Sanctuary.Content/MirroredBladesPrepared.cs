// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
namespace Sanctuary;
internal sealed record MirroredBladeValues(int Rank,int AttackPercent,int SpeedPercent,int TotalAttacks,int DuplicatePercent,int Mana256,
    int DuplicateMeleeDivisor,int DuplicateRangedDivisor,int DuplicatePlayerDivisor,int PhysicalPercent,int FlatPhysical,int ElementPercent,int DamageSynergyPercent);
internal sealed record MirroredBladeAttack(int Ordinal,int DamagePercent,int? RawMeleeDivisor,int? RawRangedDivisor,int? RawPlayerDivisor);
// Suppliers and per-attack descriptors only. New start68/do166, mirror emission,
// packet scaling placement and item-event chronology remain unsupported.
internal static class MirroredBladesPrepared
{
    internal static MirroredBladeValues Capture(SkillValues v)
    {
        var s=SkillMechanics.For(CombatAbility.MirroredBlades);
        if(v.Ability!=CombatAbility.MirroredBlades||v.Rank is <1 or >255||s.SourceId!=392||s.Number("srvstfunc")!=68||s.Number("srvdofunc")!=166||s.Number("seqnum")!=25
            ||s.Text("anim")!="SQ"||s.Number("UseAttackRate")!=1||s.Number("SrcDam")!=128||s.Number("HitShift")!=8||s.Parameter(11)!=1||s.Parameter(12)!=0||s.Text("range")!="both"
            ||s.Text("itypea1")!="weap"||s.Text("etypea1")!="tpot"||s.Text("EType")!="0")
        {throw new NotSupportedException("Unsupported Mirrored Blades route");}
        return new(v.Rank,checked((int)v.Formula("toht")),checked((int)v.Eval("calc3")),checked((int)v.Eval("calc5")),checked((int)v.Eval("calc6")),v.Mana256,
            checked((int)v.Eval("calc7")),checked((int)v.Eval("calc8")),checked((int)v.Eval("calc9")),checked((int)v.Eval("calc1")),checked((int)v.Eval("calc2")),checked((int)v.Eval("calc4")),checked((int)v.Eval("DmgSymPerCalc")));
    }
    internal static ImmutableArray<MirroredBladeAttack> Attacks(MirroredBladeValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        // Reconstruct rank-only suppliers to reject malformed saved/caller DTOs.
        var expected=Capture(new(SkillMechanics.For(CombatAbility.MirroredBlades),k=>k=="Mirrored Blades"?values.Rank:0,_=>0,99));
        if(values!=expected){throw new ArgumentException("Invalid Mirrored Blades suppliers");}
        var attacks=ImmutableArray.CreateBuilder<MirroredBladeAttack>(values.TotalAttacks);
        attacks.Add(new(0,100,null,null,null));
        for(int i=1;i<values.TotalAttacks;i++){attacks.Add(new(i,values.DuplicatePercent,values.DuplicateMeleeDivisor,values.DuplicateRangedDivisor,values.DuplicatePlayerDivisor));}
        return attacks.MoveToImmutable();
    }
    internal static PlayerWeaponSequence Sequence(string sourceWeaponCode)=>PlayerWeaponSequences.Profile(25,sourceWeaponCode);
    internal static SourceAnimationState Schedule(string sourceWeaponCode,int advance,int frameBonus,int atFrame)
        =>PlayerWeaponSequences.Schedule(25,sourceWeaponCode,advance,frameBonus,atFrame);
    // Explicit scalar arithmetic services. These do not decide where to scale
    // a packet, what target class/divisor applies, or dispatch an item callback.
    internal static int ScaleSuppliedDirect256(int amount256,MirroredBladeAttack attack)
    {
        ArgumentNullException.ThrowIfNull(attack);
        if(amount256<0||attack.Ordinal is <0 or >4||attack.Ordinal==0&&attack.DamagePercent!=100||attack.Ordinal>0&&attack.DamagePercent is <60 or >120){throw new ArgumentOutOfRangeException();}
        return attack.Ordinal==0?amount256:checked((int)SourcePercentage.Damage(amount256,attack.DamagePercent));
    }
    internal static int SuppliedCrushingLoss256(int life256,int rawDivisor,int resistance,int hpBonus)
    {
        if(rawDivisor is not(8 or 12 or 16 or 20 or 24 or 30 or 32 or 40 or 50)){throw new NotSupportedException("Unmapped duplicate Crushing Blow divisor");}
        // The table's ranged divisor already includes the ranged doubling.
        // Do not also pass ranged=true to the common arithmetic primitive.
        return NamedCombat.CrushingLoss256(life256,rawDivisor,resistance,hpBonus,ranged:false);
    }
}
