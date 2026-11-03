// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Prepared recipient adapter, not a learned cast or periodic missile emitter.
// The caller supplies the missile's formula context; its Item serial remains
// the curse creator. Owner identity supplies hostility/credit only.
internal static class GrimWardCurseTransport
{
    internal static bool Valid(NecromancerCurse curse)
    {
        if(curse.Ability!=CombatAbility.GrimWard){return curse.Ward==null;}
        return curse.Ward is{Velocity:>=-75 and <=0,Attack:>=-75 and <=0,Other:>=-75 and <=0,FullPhysical:>=-1295 and <=-20} w
            &&w.Attack==w.Other&&w.FullPhysical%5==0
            &&curse.PhysicalDelta is{} p&&(p==w.FullPhysical||p==w.FullPhysical/5)
            &&((Serial)curse.Source).IsItem&&curse.Terror is{Skill:CombatAbility.GrimWard};
    }
    internal static int Velocity(Mobile target)=>NecromancerEffects.Curse(target)?.Ward?.Velocity??0;
    internal static int Attack(Mobile target)=>NecromancerEffects.Curse(target)?.Ward?.Attack??0;
    // Retained separately. Native nonattack animation modes have no certified
    // other_animrate consumer yet; storing it is not mechanical completion.
    internal static int Other(Mobile target)=>NecromancerEffects.Curse(target)?.Ward?.Other??0;
    internal static double? MovementDelay(Mobile target,double seconds)
    {
        if(NecromancerEffects.Curse(target)?.Ward==null){return null;}
        int running=SourceTerror.State(target) is{Goal:not null,Combat:false} ai?ai.VelocityPercent:0;
        // Legacy UNITS_UpdateRunWalkAnimRateAndVelocity sums velocitypercent
        // before its 25% floor. In particular, terror running and Ward slow
        // are two terms in one stat, not independent delay multipliers.
        int percent=Math.Max(25,100+running+PaladinVigor.VelocityPercent(target)-GearEffects.MovementSlowTotal(target));
        return seconds*100/percent;
    }
    internal static bool Apply(PlayerMobile owner,Item missile,BaseCreature target,SkillValues context,DateTime at)
    {
        if(owner is not{Alive:true,Deleted:false}||missile is not{Deleted:false}||target is not{Alive:true,Deleted:false}
            ||missile.Map!=target.Map||owner.Map!=target.Map||missile.Map==Map.Internal
            ||!ClassCombat.IsHostile(owner,target)||!NecromancerCurseCasts.Eligible(target)
            ||!NecromancerCurseCasts.AiEligible(target)||!SourceTerror.Supported(target)
            ||EliteAuras.Curses.Active(target.Serial,at)){return false;}
        var packet=GrimWardCurseOperation.Packet(context,SummonDifficulties.For(owner.Map));
        var state=NecromancerEffects.Profiles.Find(target)??new();
        var old=state.Curse is{} c&&c.UntilUtc>at?c:null;
        if(old?.Ability==CombatAbility.GrimWard&&!Valid(old))
        {throw new NotSupportedException("Incomplete prepared Ward recipient state");}
        GrimWardCurseList previous=old==null?null:new(old.Ability==CombatAbility.GrimWard?56:0,old.Ability==CombatAbility.GrimWard?150:0,
            old.Rank,unchecked((int)old.Source),checked((int)Math.Ceiling((old.UntilUtc-at).TotalMilliseconds/40)),
            old.Ward?.Velocity??0,old.Ward?.Attack??0,old.Ward?.Other??0,old.PhysicalDelta??0);
        EliteAuras.Refresh(target);
        var result=GrimWardCurseOperation.Apply(packet,new(1,1,true,AssassinBuffs.CurseResistance(target),target.PhysicalResistance,
            target is BaseHire,old?.Ability==CombatAbility.Attract),previous,unchecked((int)missile.Serial.Value),0);
        if(!result.Accepted){return false;}
        var list=result.List;
        bool retained=old?.Ability==CombatAbility.GrimWard&&old.Rank==packet.Rank;
        var ai=SourceTerror.Learned(target,packet.Rank,at) with{Skill=CombatAbility.GrimWard};
        var next=new NecromancerCurse(CombatAbility.GrimWard,unchecked((uint)list.Creator),list.Rank,at.AddMilliseconds(list.Expires*40L),
            retained?old.OriginX:missile.X,retained?old.OriginY:missile.Y,Terror:ai,
            PhysicalDelta:list.Physical,Ward:new(list.Velocity,list.AttackRate,list.OtherRate,packet.Physical));
        if(!Valid(next)){throw new NotSupportedException("Prepared Ward values exceed the saved recipient domain");}
        // Factory writes the primary velocity, then restarts AI, then writes
        // secondary stats. The server loop makes this transaction synchronous.
        var primary=next with{PhysicalDelta=retained?old.PhysicalDelta:0,
            Ward=next.Ward with{Attack=retained?old.Ward.Attack:0,Other=retained?old.Ward.Other:0}};
        bool resets=state.Curse?.Terror!=null&&(state.Curse.Source!=next.Source||state.Curse.UntilUtc!=next.UntilUtc);
        NecromancerEffects.Profiles.Store(target,state with{Curse=primary});
        if(!resets){SourceTerror.Reset(target);}
        NecromancerEffects.Profiles.Store(target,(NecromancerEffects.Profiles.Find(target)??state) with{Curse=next});
        target.AIObject?.AITimer.Prod();
        NecromancerEffects.CurseApplied(owner,target);
        return true;
    }
}
