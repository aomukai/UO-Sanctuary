// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record GoatmanFrenzyState(int Rank,int Charges,DateTime UntilUtc);

// Persistent body binding for the approved NPC action component. The world
// host still owns AI selection, clock delivery, movement and contact services.
// Rank/birth/passives are never recaptured from the owner's current equipment.
internal static class GoatmanNpcBodyActions
{
    private static void Clock(DateTime at)
    {if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("NPC body action clock must be UTC");}}
    internal static void Validate(GoatmanFrenzyState state,int rank)
    {
        if(state!=null&&(rank<1||state.Rank!=rank||state.Rank>255||state.Charges<1||state.Charges>state.Rank
            ||state.UntilUtc.Kind!=DateTimeKind.Utc||state.UntilUtc==DateTime.MinValue||state.UntilUtc>DateTime.MaxValue.AddMilliseconds(-40)))
        {throw new InvalidDataException("Invalid saved Goatman Frenzy state");}
    }
    private static GoatmanBodyState Body(WarlockGoatmanBody body)
        =>body is{Alive:true,Deleted:false,Prepared:{} state}?state:throw new NotSupportedException("NPC action needs its living captured Goatman body");
    internal static GoatmanFrenzyState Active(WarlockGoatmanBody body,DateTime at)
    {
        Clock(at);var s=Body(body);Validate(s.Frenzy,s.Snapshot.Birth.Suppliers.FrenzyRank);
        return s.Frenzy is{} f&&at<f.UntilUtc?f:null;
    }
    internal static int Bonus(WarlockGoatmanBody body,DateTime at)
        =>Active(body,at)is{} f?checked((int)GoatmanNpcSkills.Values(417,f.Charges).Eval("aurastatcalc2")):0;
    internal static int BaseAttackRate(WarlockGoatmanBody body)
        =>checked(SourceAttackRate.WolfRate(body)+Body(body).Snapshot.Birth.Suppliers.AttackSpeed);
    internal static int AttackRate(WarlockGoatmanBody body,DateTime at)=>checked(BaseAttackRate(body)+Bonus(body,at));
    internal static int MovementPercent(WarlockGoatmanBody body,DateTime at)
        =>checked(100+Body(body).Snapshot.Birth.Suppliers.VelocityPercent+Bonus(body,at)+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlow(body));
    internal static (int Velocity,int Animation) Movement(WarlockGoatmanBody body,DateTime at,int extraPercent=0)
        =>SourceActorMotion.Speed(NecromancerSummons.Source("wargoatman","Velocity"),GoatmanNpcActionProposal.WalkAnimation.Speed,
            checked(MovementPercent(body,at)+extraPercent),0);
    // The host must qualify the source mode's allows-speed flag. Do not apply
    // other_animrate to every mode, or add it again to movement's velocity rate.
    internal static int OtherAnimation(WarlockGoatmanBody body,int baseSpeed,bool ignoresOtherRate,DateTime at)
    {
        Clock(at);_=Body(body);
        if(baseSpeed is <0 or >32767){throw new ArgumentOutOfRangeException(nameof(baseSpeed));}
        return ignoresOtherRate?baseSpeed:SourceAttackRate.Speed(baseSpeed,0,checked(100+Bonus(body,at)));
    }

    internal static GoatmanNpcActionProposal.Action OpenPrepared(WarlockGoatmanBody body,BaseCreature target,int skill,int frame,DateTime at)
    {
        if(!GoatmanNpcActionProposal.Approved){throw new NotSupportedException("NPC action rule is not approved");}
        Clock(at);var state=Body(body);int rank=skill switch
        {417=>state.Snapshot.Birth.Suppliers.FrenzyRank,419=>state.Snapshot.Birth.Suppliers.CleaveRank,_=>throw new NotSupportedException("Use the qualified Stun/Berserk start adapter for other Goatman skills")};
        if(rank<1){throw new NotSupportedException("Captured Goatman has not unlocked this NPC skill");}
        // Anchor source frame0 to this action's actual observed UTC. A saved
        // subframe deadline rounds only its transient projection upward;reads
        // retain the original absolute deadline and never extend the buff.
        void Changed(int capturedRank,int changedFrame,FrenzyReleaseList list)
        {
            if(body is not{Alive:true,Deleted:false}){return;}
            long frames=checked((long)list.Expiry-frame);
            var until=at.AddTicks(checked(frames*SourceFrameTimeline.IntervalTicks));
            body.RestorePrepared(body.Prepared with{Frenzy=new(capturedRank,list.Charges,until)});
        }
        var active=Active(body,at);GoatmanNpcActionProposal.Frenzy speed;
        if(active==null){speed=new(Changed);}
        else
        {
            long ticks=(active.UntilUtc-at).Ticks;
            int remaining=checked((int)((ticks+SourceFrameTimeline.IntervalTicks-1)/SourceFrameTimeline.IntervalTicks));
            speed=GoatmanNpcActionProposal.Frenzy.Restore(active.Rank,active.Charges,checked(frame+remaining),Changed);
        }
        return new(body,target,skill,rank,BaseAttackRate(body),frame,speed);
    }
}
