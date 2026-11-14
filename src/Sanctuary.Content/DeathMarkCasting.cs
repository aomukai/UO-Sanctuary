// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Prepared player action. The existing do155/world/recipient policies are
// qualified separately; current requirespettype startup and opaque unit12c
// production need the explicit decision documented with this candidate.
internal static class DeathMarkCasting
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkCasting));
    internal sealed class Pending(PlayerMobile owner,int context12c)
    {
        internal readonly PlayerMobile Owner=owner;
        internal readonly int Context12c=context12c;
        internal bool Released,Cancelled;
        internal int ManaPaid256;
        internal DeathMarkNativeResult Result;
        internal string Fault;
    }
    internal static bool Actor(PlayerMobile p)=>p is{Alive:true,Deleted:false,NetState:{Running:true}}
        &&ClassCreation.Profiles.TryGet(p.Serial,out var profile)&&profile.Class==DiabloClass.Warlock
        &&SkillMechanics.Rank(p,CombatAbility.DeathMark)>0;
    private static bool Destination(PlayerMobile p,Serial selected,Point3D aim)
        =>DeathMarkNativeRelease.TargetPoint(p,selected,aim,out var point,out _)
            &&p.GetDistanceToSqrt(point)<=19&&DeathMarkNativeRelease.Roster(p).Length>0;
    internal static Pending BeginPrepared(PlayerMobile p,Serial selected,Point3D aim,long now,int context12c)
    {
        if(!Actor(p)){return null;}
        var row=SkillMechanics.For(CombatAbility.DeathMark);
        if(row.SourceId!=375||row.Number("srvstfunc")!=0||row.Number("srvdofunc")!=155||row.Number("requirespettype")!=1
            ||row.Number("UseManaOnDo")!=0||row.Number("localdelay")!=20||row.Text("pettype")!="demon")
        {throw new NotSupportedException("Unqualified Death Mark player action row");}
        try
        {
            if(!SkillCombat.BeginPrepared(p,CombatAbility.DeathMark,selected,aim,now,
                (who,_,target,point)=>Destination(who,target,point),(who,_)=>Actor(who),CombatTiming.For(p,CombatAbility.DeathMark)))
            {return null;}
            var pending=new Pending(p,context12c);ClassCombat.For(p).DeathMark=pending;return pending;
        }
        catch(NotSupportedException e){Log.Warning("Death Mark startup refused unsupported context: {Reason}",e.Message);return null;}
    }
    internal static void Cancel(PlayerMobile p)
    {
        var s=ClassCombat.Existing(p);if(s?.DeathMark is not{} pending){return;}
        pending.Cancelled=true;s.DeathMark=null;
        if(s.PendingAbility==CombatAbility.DeathMark){s.PendingAbility=CombatAbility.None;}
    }
    internal static bool ResolvePrepared(PlayerMobile p,long now)
    {
        var s=ClassCombat.For(p);if(s.DeathMark is not{} action||action.Released||action.Cancelled){return false;}
        if(!Actor(p)||!ShapeRestrictions.Allowed(p,CombatAbility.DeathMark)||p.Frozen||p.Paralyzed||p.Spell!=null||!AlliedOverlap.Allowed(p)
            ||s.PendingAbility!=CombatAbility.DeathMark||p.Map!=s.ActionMap||p.Location!=s.ActionOrigin
            ||(CombatTiming.Equipped(p)?.Serial??Serial.Zero)!=s.PendingWeapon)
        {Cancel(p);return false;}
        if(now<s.EffectAt){return false;}
        // Claim before world callbacks. Failure may follow successful pet
        // relocations in do155;never replay or invent rollback/refunds.
        action.Released=true;s.DeathMark=null;s.PendingAbility=CombatAbility.None;
        var values=SkillMechanics.Values(p,CombatAbility.DeathMark);
        if(!NecromancerEffects.CanPay(p,values.Mana256)){return false;}
        try
        {
            int stateCount=checked((int)GenericPetStateMasks.Bind(1).StateCount(1));
            var context=new DeathMarkCastContext(p.Serial.Value,new(0,true,p.X*2,p.Y*2),true,375,values.Rank,1,
                action.Context12c,20,21,212,stateCount);
            var at=TimedEffects.Now;
            action.Result=DeathMarkNativeRelease.RunPrepared(p,context,s.PendingTarget,s.PendingPoint,at);
            if(action.Result.Result==0){return false;}
            if(!NecromancerEffects.Pay(p,values.Mana256)){throw new NotSupportedException("Death Mark mana changed during committed world callbacks");}
            action.ManaPaid256=values.Mana256;FireGround.Delay(p,CombatAbility.DeathMark,at);
            p.PlaySound(0x1E5); // Identified shared cast placeholder.
            return true;
        }
        catch(NotSupportedException e)
        {action.Fault=e.Message;Log.Warning("Death Mark release refused unsupported context: {Reason}",e.Message);return false;}
    }
}
