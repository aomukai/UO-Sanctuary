// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record CleaveValues(int Rank,int DamagePercent,int AttackPercent,int SpeedPercent,int RawSlices,int TooltipArcDegrees,int Mana256);
internal sealed record CleaveSequence(string Code,int Sequence,int Points,int Frames,int RawAuxiliary,int[][] Rows);
// Components only: new start68/do164 and actual rank20 arc remain unsupported.
// No cast registration, automatic target selection, payment or windup is implied.
internal static class CleavePrepared
{
    private static readonly Lazy<IReadOnlyDictionary<string,CleaveSequence>> Sequences=new(()=>
    {
        var rows=BalanceCatalog.Read<CleaveSequence>("cleave-sequence").ToArray();
        string[] codes=["HTH","1HT","2HT","1HS","2HS","BOW","XBW","STF","1JS","1JT","1SS","1ST","HT1","HT2"];
        if(rows.Length!=14||!rows.Select(x=>x.Code).SequenceEqual(codes)){throw new InvalidDataException("Missing Cleave SQ24 profiles");}
        foreach(var r in rows)
        {
            if(r.Sequence!=24||r.Points!=r.Frames||r.Points is <16 or >22||r.RawAuxiliary!=256||r.Rows?.Length!=r.Points
                ||r.Rows.Any(x=>x?.Length!=5||x[0]!=0||x[1]!=10||x[3]!=0||x[4] is <0 or >1)||r.Rows.Count(x=>x[4]==1)!=1)
            {throw new InvalidDataException("Unsupported Cleave sequence row");}
        }
        return rows.ToDictionary(x=>x.Code,StringComparer.Ordinal);
    });
    internal static CleaveValues Capture(SkillValues v)
    {
        var s=SkillMechanics.For(CombatAbility.Cleave);
        if(v.Ability!=CombatAbility.Cleave||v.Rank is <1 or >255||s.SourceId!=391||s.Number("srvstfunc")!=68||s.Number("srvdofunc")!=164||s.Number("seqnum")!=24
            ||s.Text("anim")!="SQ"||s.Number("UseAttackRate")!=1||s.Text("range")!="h2h"||s.Number("requiresweapon")!=1||s.Text("itypea1")!="mele"
            ||s.Number("SrcDam")!=128||s.Number("HitShift")!=8||s.Text("EType")!="0"||v.Eval("calc2")!=0||v.Eval("calc4")!=0||v.Eval("calc7")!=0)
        {throw new NotSupportedException("Unsupported Cleave route");}
        int slices=checked((int)v.Eval("calc5"));
        return new(v.Rank,checked((int)v.Eval("calc1")),checked((int)v.Formula("toht")),checked((int)v.Eval("calc3")),slices,checked(slices*s.Parameter(10)),v.Mana256);
    }
    internal static CleaveSequence Sequence(string sourceWeaponCode)
    {
        if(sourceWeaponCode==null||!Sequences.Value.TryGetValue(sourceWeaponCode,out var r)){throw new NotSupportedException("Unknown source weapon class for Cleave SQ24");}
        return r with{Rows=r.Rows.Select(x=>x.ToArray()).ToArray()};
    }
    // Explicit source class, rate and frame bonus are required. CAST poses do
    // not select FCR; raw auxiliary256has no certified runtime interpretation.
    internal static SourceAnimationState Schedule(string sourceWeaponCode,int advance,int frameBonus,int atFrame)
    {
        var s=Sequence(sourceWeaponCode);
        return SourceAnimationEvents.Initial(new(0,advance,s.Frames*256,s.Rows.Select(x=>x[4]).ToArray(),[]),atFrame,frameBonus);
    }
    // Caller supplies modern eligibility, capture instant, one contact and wear
    // intent. This is the ordinary SrcDam128weapon pipeline, not do164's arc.
    internal static bool Contact(PlayerMobile owner,Mobile target,CleaveValues values,bool qualified,bool wearWeapon,Func<double> hitRoll,double damageRoll,Func<int,int> physicalRoll,Random effects)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(hitRoll);ArgumentNullException.ThrowIfNull(physicalRoll);ArgumentNullException.ThrowIfNull(effects);
        if(!qualified||owner is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map!=target.Map||owner.Map==Map.Internal
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var c)||c.Class!=DiabloClass.Warlock||!ClassCombat.IsHostile(owner,target)) {return false;}
        if(CombatTiming.Equipped(owner)==null||!PaladinVengeance.WeaponAllowed(owner)){throw new NotSupportedException("Cleave requires a supported ordinary melee weapon;throwing-base melee is unresolved");}
        if(values.Rank is <1 or >255||values.AttackPercent!=50+7*(values.Rank-1)||values.DamagePercent<20+5*(values.Rank-1)||values.DamagePercent>620+5*(values.Rank-1)||(values.DamagePercent-20-5*(values.Rank-1))%10!=0)
        {throw new ArgumentException("Invalid supplied Cleave contact values");}
        owner.DoHarmful(target,true);
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(ClassStats.CheckHit(owner,target,values.AttackPercent,hitRoll)!=true){return true;}
        owner.PlaySound(0x238); // Identified ordinary weapon-contact placeholder.
        GearEffects.Strike(owner,target,damageRoll,effects,wearWeapon,skillPhysicalPercent:values.DamagePercent,physicalRoll:physicalRoll);
        return true;
    }
}
