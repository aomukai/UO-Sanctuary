// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record EchoingStrikeValues(int Rank,int HardMirroredBlades,int HardBladeWarp,int Mana256,int AttackPercent,
    int PhysicalPercent,int MinimumPhysical256,int MaximumPhysical256,int DamageSynergyPercent,
    int TotalAttacks,int SuppliedAttackPercent,int ScaleOnlyDuplicates,int SeparateMissileCastId,int WeaponSource128);

// Table suppliers only. Shared do166 and missile do44/hit34/damage16 are
// unrecovered: no inferred fan,return flight,primary scaling or learned cast.
internal static class EchoingStrikePrepared
{
    internal static EchoingStrikeValues Capture(SkillValues v)
    {
        var s=SkillMechanics.For(CombatAbility.EchoingStrike);
        if(v.Ability!=CombatAbility.EchoingStrike||v.Rank is <1 or >255||s.SourceId!=388||s.Number("srvstfunc")!=0||s.Number("srvdofunc")!=166
            ||s.Text("anim")!="SC"||s.Text("range").Trim()!="rng"||s.Number("requiresweapon")!=1||s.Text("itypea1")!="mele"
            ||s.Text("srvmissilea")!="echoingstrike"||s.Number("SrcDam")!=116||s.Number("HitShift")!=8||s.Number("ResultFlags")!=8
            ||s.Parameter(11)!=0||s.Parameter(12)!=1)
        {throw new NotSupportedException("Unsupported Echoing Strike route");}
        int mirror=checked((int)v.Formula("skill('Mirrored Blades'.blvl)")),warp=checked((int)v.Formula("skill('Blade Warp'.blvl)"));
        if(mirror is <0 or >20||warp is <0 or >20){throw new NotSupportedException("Echoing Strike requires valid hard-point suppliers");}
        return new(v.Rank,mirror,warp,v.Mana256,checked((int)v.Eval("ToHitCalc")),checked((int)v.Eval("calc1")),
            v.PhysicalDamage256(false),v.PhysicalDamage256(true),checked((int)v.Eval("DmgSymPerCalc")),
            checked((int)v.Eval("calc5")),checked((int)v.Eval("calc6")),s.Parameter(11),s.Parameter(12),s.Number("SrcDam"));
    }
}
