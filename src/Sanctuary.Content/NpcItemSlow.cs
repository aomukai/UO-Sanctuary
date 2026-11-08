// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server.Logging;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// User-approved Sanctuary rule. The old item19 guard is contradictory;
// this is not presented as a reconstructed modern callback body.
internal static class NpcItemSlow
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcItemSlow));
    private static readonly HashSet<string> Reported=[];
    internal static void NativeClay(Mobile source,Mobile target,DateTime at)
    {
        // Native fallback has no prepared capability phase. Refuse only this
        // unmapped status and preserve its existing ordinary damage route.
        try{Clay(source,target,false,at);}
        catch(NotSupportedException e)
        {
            string key=target?.GetType().FullName+":"+e.Message;
            if(Reported.Add(key)){Log.Warning("Outgoing Clay slow needs an explicit receiver for {Type}: {Reason};other combat continues",target?.GetType().FullName,e.Message);}
        }
    }
    internal static int Bonus(BaseCreature actor)=>NpcEquipmentCombat.EventBonus(actor,"slow");
    internal static int Cap(Mobile target)=>target switch
    {
        PlayerMobile=>50,
        BaseHire=>50,
        BaseCreature creature when creature.IsParagon=>throw new NotSupportedException("Pet slow needs an explicit Paragon classification"),
        BaseCreature creature=>NamedCombat.Elite(creature)||PreparedMonsterStun.IsUnique(creature)?50:90,
        _=>throw new NotSupportedException("Pet slow needs a player or NPC recipient")
    };
    internal static bool Apply(BaseCreature source,Mobile target,bool ranged,DateTime at)
        =>source!=null&&target!=null&&ApplyAmount(source,target,Bonus(source),ranged,at);
    internal static bool Clay(Mobile source,Mobile target,bool ranged,DateTime at)
    {
        if(source is not NecromancerMinion{Kind:CombatAbility.ClayGolem} clay){return false;}
        int amount=clay is ClayGolemBody body?body.ReactiveSlowPercent:clay.CapturedValues.Diminishing(3,4);
        return ApplyAmount(clay,target,amount,ranged,at);
    }
    internal static bool ApplyAmount(BaseCreature source,Mobile target,int amount,bool ranged,DateTime at)
    {
        if(amount==0){return false;}
        if(amount<0||ranged){throw new NotSupportedException("Approved pet slow covers nonnegative melee effects only");}
        if(source is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false}}
            ||target is not{Alive:true,Deleted:false}||source.Map==null||source.Map==Map.Internal||target.Map!=source.Map||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Pet slow needs its living original owned source,recipient,facet and UTC event");}
        TimedEffects.SlowReaction(target,Math.Min(amount,Cap(target)),at);return true;
    }
}
