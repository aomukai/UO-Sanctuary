// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record EldritchBlastData(string ReferenceVersion,string MissileSourceSha256,SummonSource Missile);
internal sealed record EldritchBlastNumbers(int Rank,int MagicMinimum256,int MagicMaximum256,int DurationFrames,
    int PeriodFrames,int RawAuraRange,int LifeStealPercent,int ManaStealPercent,int Mana256,int NovaMissile);

// Table suppliers only. Current do165 periodic installation/emission, recovery,
// recipient order and magic mastery timing remain separate, gated consumers.
internal static class EldritchBlastSuppliers
{
    private static readonly Lazy<EldritchBlastData> Data=new(()=>BalanceCatalog.ReadObject<EldritchBlastData>("eldritch-blast-profile"));
    internal static EldritchBlastNumbers Capture(SkillValues values)
    {
        if(values?.Ability!=CombatAbility.EldritchBlast||values.Rank is <1 or >255)
        {throw new NotSupportedException("Unsupported Eldritch Blast rank/context");}
        var row=SkillMechanics.For(CombatAbility.EldritchBlast);var data=Data.Value;
        if(row.SourceId!=384||row.Number("srvstfunc")!=28||row.Number("srvdofunc")!=165
            ||row.Text("EType")!="mag"||row.Number("SrcDam")!=0||row.Number("HitShift")!=8
            ||row.Text("aurastate")!="eldritchblastperiodic"||row.Text("srvmissilea")!="eldritchblastnova"
            ||row.Number("periodic")!=1||data.ReferenceVersion!="3.3.93847"
            ||data.MissileSourceSha256!=ItemSkillExecution.MissileSourceHash||data.Missile?.Key!="eldritchblastnova")
        {throw new NotSupportedException("Unqualified Eldritch Blast primary profile");}
        var nova=new MissileValues(data.Missile.Fields,values);
        if(nova.Number("*ID")!=721||nova.Number("pSrvDoFunc")!=1||nova.Number("pSrvHitFunc")!=0
            ||nova.Number("Vel")!=12||nova.Number("MaxVel")!=12||nova.Number("Accel")!=-600
            ||nova.Number("Range")!=16||nova.Number("CollideType")!=6||nova.Text("Skill")!="Eldritch Blast")
        {throw new InvalidDataException("Eldritch Blast nova identity changed");}
        return new(values.Rank,values.Damage256(false),values.Damage256(true),
            checked((int)values.Eval("auralencalc")),row.Number("perdelay"),checked((int)values.Eval("aurarangecalc")),
            checked((int)values.Eval("calc1")),checked((int)values.Eval("calc2")),values.Mana256,721);
    }
    internal static EldritchBlastNumbers Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock)
        {throw new NotSupportedException("Eldritch Blast needs a live Warlock owner");}
        if(NamedElements.MasteryPercent(owner,"mag")!=0)
        {throw new NotSupportedException("Eldritch Blast magic mastery timing remains unverified");}
        return Capture(SkillMechanics.Values(owner,CombatAbility.EldritchBlast));
    }
}
