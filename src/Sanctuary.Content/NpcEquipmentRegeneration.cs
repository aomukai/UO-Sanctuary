// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class NpcEquipmentRegeneration
{
    private sealed class Clock{internal long Last;internal int Milliseconds;}
    private static readonly ConditionalWeakTable<Mobile,Clock> Clocks=new();
    internal static bool Handles(Mobile actor)=>ItemDurationDefenses.HasEquipment(actor);
    internal static int ItemRate(Mobile actor)
    {
        if(actor is not BaseCreature creature||!Handles(actor)||NpcEquipmentCombat.Context(creature) is not{} item){return 0;}
        if(ClassGear.Material(item.Item,"life-per-five")!=0)
        {throw new NotSupportedException("NPC material life-per-five needs a verified source-rate projection");}
        return item.Bonus("replenish-life");
    }
    internal static int Delta(int basis,int item,int poison,bool lifeMask,bool preventHeal)
    {
        int rate=checked((lifeMask?0:basis)+item-poison);
        return preventHeal&&rate>=0?0:rate;
    }
    internal static void Apply(Mobile actor,int delta,Mobile poisonSource=null)
    {
        if(delta>0){SkillHealth.Heal(actor,delta);}
        else if(delta<0){PoisonStates.Drain(poisonSource,actor,-(long)delta);}
    }
    internal static void Recover(Mobile actor,long now,int? sourceRate=null)
    {
        var clock=Clocks.GetValue(actor,static _=>new());
        if(clock.Last==0||now<clock.Last||actor is not{Alive:true,Deleted:false}||actor.Map==null||actor.Map==Map.Internal
            ||actor.Poisoned||!TimedEffects.CanRegenerateHits(actor))
        {clock.Last=now;clock.Milliseconds=0;return;}
        // Match existing persistent-summon policy: do not repay offline or
        // paused-sector intervals as a burst of recovery on return.
        long elapsed=Math.Clamp(now-clock.Last,0,1000);clock.Last=now;
        long total=clock.Milliseconds+elapsed;int frames=(int)(total/40);clock.Milliseconds=(int)(total%40);
        int delta=sourceRate??(actor is SkeletonGuardian guardian?Delta(BattleOrders.Regeneration256(guardian),ItemRate(guardian),0,false,false)
            :throw new NotSupportedException("NPC recovery requires a supplied source rate"));
        for(int i=0;i<frames&&actor is{Alive:true,Deleted:false};i++){Apply(actor,delta);}
    }
    internal static void Reset(Mobile actor)=>Clocks.Remove(actor);
}
