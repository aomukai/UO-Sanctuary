// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

public partial class FireGolemBody
{
    private static readonly ILogger ContactLog=LogFactory.GetLogger(typeof(FireGolemBody));
    private IronGolemContact.Prepared _preparedContact;
    private string _lastLegacyContact;
    private uint _faultTarget;
    internal IronGolemContact.Prepared PendingContact=>_preparedContact;
    internal string LegacyContactReason=>_lastLegacyContact;
    private void ReportContact(string reason,bool legacy)
    {
        string text=(legacy?"legacy: ":"fault: ")+reason;
        if(_lastLegacyContact==text){return;}_lastLegacyContact=text;
        ContactLog.Warning("Fire contact {Actor}/{TargetMode}: {Reason}",Serial.Value,legacy?"existing native rules":"paused",reason);
        if(ControlMaster is PlayerMobile owner)
        {owner.SendMessage(0x22,legacy?"Some combat reactions on this creature still use Sanctuary's existing rules.":"This Fire Golem cannot finish that combat reaction yet; another target can still be attacked.");}
    }
    protected override bool PrepareMeleeContact(Mobile target)
    {
        DiscardMeleeContact();if(_faultTarget==target?.Serial.Value){return false;}_faultTarget=0;
        if(ControlMaster is not PlayerMobile owner||target is not BaseCreature creature)
        {ReportContact("Owned NPC recipient mapping is required",true);return true;}
        IronGolemContact.Rules rules;
        try{rules=UnarmedSummonContact.Require(this,owner,creature);}
        catch(NotSupportedException e){ReportContact(e.Message,true);return true;}
        // A later allocation failure must never retry through the native bridge:
        // the source random stream/defensive pools may already have advanced.
        try{_preparedContact=UnarmedSummonContact.Prepare(this,owner,creature,rules);_lastLegacyContact=null;return true;}
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
        try
        {
            UnarmedSummonContact.Release(this,owner,creature,prepared,TimedEffects.Now);
        }
        catch(NotSupportedException e){_faultTarget=target.Serial.Value;ReportContact(e.Message,false);}
    }
    protected override void DiscardMeleeContact(){_preparedContact?.Cancel();_preparedContact=null;}
}
