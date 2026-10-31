// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

internal static class RaisedSkeletonContact
{
    internal static bool Within(RaisedSkeleton body,BaseCreature target,int bonus)
    {
        if(target==null||body.Map==null||body.Map==Map.Internal||target.Map!=body.Map){return false;}
        int size=CreatureProfiles.For(target).Traits.Size??throw new NotSupportedException("Recipient footprint is not mapped");
        var metadata=NativeMonsterReturn.Metadata(target);
        return SourceMeleeGeometry.Within(new(new(body.X*2,body.Y*2),2,1,363,0),
            new(new(target.X*2,target.Y*2),size,1,metadata.Class),bonus,(_,_,_)=>!body.InLOS(target));
    }
    private sealed class Result(RaisedSkeleton body,PlayerMobile owner,BaseCreature target):SourceMeleeResultServices
    {
        internal override int? AttackerKind=>1;
        internal override int? DefenderKind=>1;
        internal override bool Hostile()=>ClassCombat.IsHostile(owner,target);
        internal override bool DefenderRunning()=>throw new InvalidOperationException("NPC result must not query player running");
        internal override int MeleeReach()=>0;
        internal override bool InRange(int bonus)=>Within(body,target,bonus);
        internal override bool HitSuccessful(int percent,bool missile)
        {
            if(missile){throw new NotSupportedException("Skeleton A1 is not a missile");}
            return ClassStats.CheckHit(body,target,percent,()=>NpcEquipmentRandom.Limited(body,100)/100.0,notifyMelee:false)
                ??throw new NotSupportedException("Missing skeleton attack supplier");
        }
        internal override int BlockOrDodge(bool avoid,bool block)
        {
            if(avoid||!block||!SourceBlocking.TryRoll(target,true,out bool blocked,out _))
            {throw new NotSupportedException("Recipient shield rule is not mapped");}
            return blocked?1:0;
        }
        internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(target);
    }
    internal static IronGolemContact.Rules Require(RaisedSkeleton body,PlayerMobile owner,BaseCreature target)
    {
        var rules=IronGolemContact.Require(body,owner,target,nativeRecovery:true);
        if(CreatureProfiles.For(target).Traits.Size is not(>=0 and <=3))
        {throw new NotSupportedException("Recipient source footprint is not mapped");}
        if(!SourceBlocking.TryRate(target,out _,out _))
        {throw new NotSupportedException("Recipient shield rule is not mapped");}
        return rules;
    }
    internal static IronGolemContact.Prepared Prepare(RaisedSkeleton body,PlayerMobile owner,BaseCreature target,IronGolemContact.Rules rules)
        =>IronGolemContact.Prepare(body,target,rules,()=>SourceMeleeResult.Resolve(new Result(body,owner,target)));
    internal static MonsterContactResult Release(RaisedSkeleton body,PlayerMobile owner,BaseCreature target,IronGolemContact.Prepared prepared,DateTime at)
        =>IronGolemContact.Release(body,owner,target,prepared,bonus=>Within(body,target,bonus),at);
}

public partial class RaisedSkeleton
{
    private static readonly ILogger ContactLog=LogFactory.GetLogger(typeof(RaisedSkeleton));
    private IronGolemContact.Prepared _preparedContact;
    private string _lastLegacyContact;
    private uint _faultTarget;
    internal IronGolemContact.Prepared PendingContact=>_preparedContact;
    internal string LegacyContactReason=>_lastLegacyContact;
    private void ReportContact(string reason,bool legacy)
    {
        string text=(legacy?"legacy: ":"fault: ")+reason;
        if(_lastLegacyContact==text){return;}_lastLegacyContact=text;
        ContactLog.Warning("Skeleton contact {Actor}/{TargetMode}: {Reason}",Serial.Value,legacy?"existing native rules":"paused",reason);
        if(ControlMaster is PlayerMobile owner)
        {owner.SendMessage(0x22,legacy?"Some combat reactions on this creature still use Sanctuary's existing rules.":"This skeleton cannot finish that combat reaction yet; another target can still be attacked.");}
    }
    protected override bool PrepareMeleeContact(Mobile target)
    {
        DiscardMeleeContact();if(_faultTarget==target?.Serial.Value){return false;}_faultTarget=0;
        if(ControlMaster is not PlayerMobile owner||target is not BaseCreature creature)
        {ReportContact("Owned NPC recipient mapping is required",true);return true;}
        IronGolemContact.Rules rules;
        try{rules=RaisedSkeletonContact.Require(this,owner,creature);}
        catch(NotSupportedException e){ReportContact(e.Message,true);return true;}
        // A later allocation failure must never retry through the native bridge:
        // the source random stream/defensive pools may already have advanced.
        try{_preparedContact=RaisedSkeletonContact.Prepare(this,owner,creature,rules);_lastLegacyContact=null;return true;}
        catch(NotSupportedException e){_faultTarget=target.Serial.Value;ReportContact(e.Message,false);return false;}
    }
    protected override bool CanReleaseMeleeContact(Mobile target)=>_preparedContact==null?base.CanReleaseMeleeContact(target)
        :Alive&&!Deleted&&AlliedOverlap.Allowed(this)&&!Frozen&&!Paralyzed&&Map!=Map.Internal&&CreatureHitRecovery.Remaining(this)==0
            &&target is BaseCreature{Alive:true,Deleted:false}&&target.Map==Map&&Math.Abs(target.Z-Z)<16&&IsEnemy(target);
    protected override void ResolveMeleeContact(Mobile target)
    {
        var prepared=_preparedContact;_preparedContact=null;
        if(prepared==null){base.ResolveMeleeContact(target);return;}
        if(ControlMaster is not PlayerMobile owner||target is not BaseCreature creature){prepared.Cancel();return;}
        try{RaisedSkeletonContact.Release(this,owner,creature,prepared,TimedEffects.Now);}
        catch(NotSupportedException e){_faultTarget=target.Serial.Value;ReportContact(e.Message,false);}
    }
    protected override void DiscardMeleeContact(){_preparedContact?.Cancel();_preparedContact=null;}
}
