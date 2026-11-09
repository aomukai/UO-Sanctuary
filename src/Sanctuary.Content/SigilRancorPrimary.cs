// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record RancorPrimaryState(uint Creator,int Rank,int DamagePercent,int AttackRate,DateTime UntilUtc);
internal sealed record RancorPrimaryValues(int Rank,int DamagePercent,int AttackRate,int RawDurationFrames,int RawRadius,int AiChance);

// Explicit primary-stat receiver only. Caller supplies recipient eligibility
// and expiry; protected do61's selection, reapplication and AI remain gated.
// No Sigil pulse or learned input subscribes to this prepared component.
internal static class SigilRancorPrimary
{
    internal static RancorPrimaryValues Capture(SkillValues v)
    {
        var s=SkillMechanics.For(CombatAbility.SigilRancor);
        if(v.Ability!=CombatAbility.SigilRancor||v.Rank is <1 or >255||s.SourceId!=396||s.Number("srvdofunc")!=157
            ||s.Text("auratargetstate")!="sigilrancor"||s.Number("aurafilter")!=2||s.Text("aurastat1")!="damagepercent"
            ||s.Text("aurastat2")!="attackrate"||PaladinCleansing.IsCurse("sigilrancor"))
        {throw new NotSupportedException("Unsupported Rancor primary-stat route");}
        return new(v.Rank,checked((int)v.Eval("aurastatcalc1")),checked((int)v.Eval("aurastatcalc2")),
            checked((int)v.Eval("auralencalc")),checked((int)v.Eval("aurarangecalc")),checked((int)v.Eval("calc1")));
    }
    private static RancorPrimaryValues Values(int rank)=>Capture(new(SkillMechanics.For(CombatAbility.SigilRancor),_=>rank,_=>0,99));
    internal static void Validate(RancorPrimaryState s)
    {
        if(s==null||!((Serial)s.Creator).IsMobile||s.Rank is <1 or >255||s.UntilUtc.Kind!=DateTimeKind.Utc||s.UntilUtc<=DateTime.UnixEpoch)
        {throw new InvalidDataException("Invalid prepared Rancor primary state");}
        var expected=Values(s.Rank);
        if(s.DamagePercent!=expected.DamagePercent||s.AttackRate!=expected.AttackRate)
        {throw new InvalidDataException("Rancor primary values differ from their captured rank");}
    }
    internal static RancorPrimaryState State(Mobile target,DateTime? at=null)=>target is BaseCreature{Alive:true,Deleted:false}
        &&NecromancerEffects.Profiles.Find(target)?.RancorPrimary is{} s&&s.UntilUtc>(at??TimedEffects.Now)?s:null;
    internal static int DamagePercent(Mobile target)=>State(target)?.DamagePercent??0;
    internal static int AttackRate(Mobile target)=>State(target)?.AttackRate??0;
    internal static bool ApplyPrepared(PlayerMobile owner,BaseCreature target,SkillValues values,DateTime until,bool eligible)
    {
        if(!eligible||owner is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}||!target.CanBeDamaged()
            ||owner.Map==null||owner.Map==Map.Internal||target.Map!=owner.Map||until<=TimedEffects.Now
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var p)||p.Class!=DiabloClass.Warlock){return false;}
        var v=Capture(values);var next=new RancorPrimaryState(owner.Serial.Value,v.Rank,v.DamagePercent,v.AttackRate,until);Validate(next);
        if(State(target)!=null){throw new NotSupportedException("Current Rancor reapplication policy remains unverified");}
        NecromancerEffects.Profiles.Store(target,(NecromancerEffects.Profiles.Find(target)??new())with{RancorPrimary=next,RancorMind=null});
        return true;
    }
    internal static void Clear(Mobile target)
    {
        if(NecromancerEffects.Profiles.Find(target)is{} s)
        {NecromancerEffects.Profiles.Store(target,s with{RancorPrimary=null,RancorMind=s.RancorMind is{} mind?mind with{Mode=0,Focus=0}:null});if(target is BaseCreature npc){npc.ForceReacquire();}}
    }
}
