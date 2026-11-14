// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record EngorgeSuppliers(int Rank,int HardBloodOath,int Mana256,int DurationFrames,int HealPercent,
    int PhysicalResistance,int MinimumDrainStat,int MaximumDrainStat,int AttackSpeed,int FlatArmor,int RegenerationStat);

// Owned table/tooltip suppliers only. Raw NPC drain and regeneration stats
// are deliberately not relabeled as player leech or percentage regeneration.
// No corpse,recipient,state writer,timer or learned cast is installed here.
internal static class EngorgePrepared
{
    internal static EngorgeSuppliers Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            ||profile.Class!=DiabloClass.Warlock||SkillMechanics.Rank(owner,CombatAbility.Engorge)<1)
        {throw new InvalidOperationException("Engorge needs a live Warlock with an available skill rank");}
        return Capture(SkillMechanics.Values(owner,CombatAbility.Engorge),ClassCreation.Profiles.GetRank(owner.Serial,"warlock.bloodoath"));
    }
    internal static EngorgeSuppliers Capture(SkillValues values,int hardBloodOath)
    {
        ArgumentNullException.ThrowIfNull(values);var row=SkillMechanics.For(CombatAbility.Engorge);
        if(values.Ability!=CombatAbility.Engorge||values.Rank is<1 or>255||hardBloodOath is<0 or>20
            ||row.SourceId!=379||row.Number("srvstfunc")!=17||row.Number("srvdofunc")!=161
            ||row.Text("aurastate")!="engorge"||row.Text("pettype")!="demon"||row.Number("requirespettype")!=1
            ||row.Text("auralencalc")!="ln56"||row.Text("calc1")!="ln34"
            ||row.Text("aurastat1")!="damageresist"||row.Text("aurastat2")!="lifedrainmindam"
            ||row.Text("aurastat3")!="lifedrainmaxdam"||row.Text("aurastat4")!="attackrate"
            ||row.Text("aurastat5")!="armorclass"||row.Text("aurastat6")!="hpregen")
        {throw new NotSupportedException("Unqualified Engorge supplier context");}
        int Eval(string key)=>checked((int)values.Eval(key));
        var result=new EngorgeSuppliers(values.Rank,hardBloodOath,values.Mana256,Eval("auralencalc"),Eval("calc1"),
            Eval("aurastatcalc1"),Eval("aurastatcalc2"),Eval("aurastatcalc3"),Eval("aurastatcalc4"),Eval("aurastatcalc5"),Eval("aurastatcalc6"));
        // The saved hard rank must describe the same formula context. Reject
        // mismatches rather than storing a contradictory future buff payload.
        var oath=SkillMechanics.For(CombatAbility.BloodOath);
        if(result.FlatArmor!=checked(hardBloodOath*oath.Parameter(11))||result.RegenerationStat!=checked(hardBloodOath*oath.Parameter(12)))
        {throw new NotSupportedException("Engorge hard Blood Oath capture differs from its supplied formula context");}
        return result;
    }
    internal static void Validate(EngorgeSuppliers captured)
    {
        if(captured==null){throw new InvalidDataException("Missing captured Engorge suppliers");}
        try
        {
            var values=new SkillValues(SkillMechanics.For(CombatAbility.Engorge),key=>key=="Engorge"?captured.Rank:0,
                key=>key=="Blood Oath"?captured.HardBloodOath:0,99);
            if(Capture(values,captured.HardBloodOath)!=captured){throw new InvalidDataException("Captured Engorge suppliers do not match their pinned ranks");}
        }
        catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Invalid captured Engorge suppliers",e);}
    }
    internal static int Healing256(EngorgeSuppliers captured,int maximum256,int current256)
    {
        Validate(captured);
        if(maximum256<0||current256<0||current256>maximum256){throw new ArgumentOutOfRangeException();}
        return checked((int)Math.Min(maximum256-current256,(long)maximum256*captured.HealPercent/100));
    }
}
