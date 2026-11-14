// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record ConsumeRelease(bool Consumed,int ManaPaid256,ConsumeEffect Effect,string Fault=null);
internal static class ConsumeCasting
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ConsumeCasting));
    internal sealed class Pending
    {
        internal bool Released,Cancelled;
        internal ConsumeRelease Result;
    }
    internal static bool Actor(PlayerMobile owner)=>owner is{Alive:true,Deleted:false,NetState:{Running:true},Map:not null}
        &&owner.Map!=Map.Internal&&ClassCreation.Profiles.TryGet(owner.Serial,out var p)&&p.Class==DiabloClass.Warlock
        &&SkillMechanics.Rank(owner,CombatAbility.Consume)>0;
    internal static bool Eligible(PlayerMobile owner,SkeletonGuardian body)
    {
        if(!Actor(owner)||body is not(WarlockGoatmanBody or WarlockTaintedBody or WarlockDefilerBody)
            ||body is not{Alive:true,Deleted:false,Blessed:false}||body.ControlMaster!=owner||!body.Permitted(owner)
            ||!PersistentMinions.Active(owner,body)||!DemonNativeState.Learned(body)||DemonNativeState.Controller(body)is not{Fault:null}
            ||NpcEquipmentCombat.Context(body)!=null||BattleCommand.PetOrder(body)<=0)
        {return false;}
        if(MinionSafeAreas.Contains(owner)){return MinionSafeAreas.Parked(body);}
        return DeathMarkWorld.State(body)is{Selector:1}&&body.Map==owner.Map&&!SpellHelper.IsTown(body.Location,owner)&&body.GetDistanceToSqrt(owner)<=19&&owner.InLOS(body);
    }
    internal static SkeletonGuardian[] Roster(PlayerMobile owner)=>Actor(owner)?WarlockGoatmanWorld.Demons(owner).Where(b=>Eligible(owner,b)).ToArray():[];
    internal static bool OpenPrepared(PlayerMobile owner)
    {
        if(!Actor(owner)){return false;}
        foreach(var body in WarlockGoatmanWorld.Demons(owner)){MinionSafeAreas.Enforce(body);}
        var bodies=Roster(owner);if(bodies.Length==0){return false;}
        owner.CloseGump<ConsumeRosterGump>();owner.SendGump(new ConsumeRosterGump(owner,bodies));return true;
    }
    internal static Pending BeginPrepared(PlayerMobile owner,Serial selected,long now)
    {
        if(!Eligible(owner,World.FindMobile(selected)as SkeletonGuardian)){return null;}
        var body=(SkeletonGuardian)World.FindMobile(selected);var point=body.Map==owner.Map?body.Location:owner.Location;
        if(!SkillCombat.BeginPrepared(owner,CombatAbility.Consume,selected,point,now,
            (who,_,id,at)=>Eligible(who,World.FindMobile(id)as SkeletonGuardian),(who,_)=>Actor(who),CombatTiming.For(owner,CombatAbility.Consume)))
        {return null;}
        var pending=new Pending();ClassCombat.For(owner).Consume=pending;return pending;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        var s=ClassCombat.Existing(owner);if(s?.Consume is not{} action){return;}action.Cancelled=true;s.Consume=null;
        if(s.PendingAbility==CombatAbility.Consume){s.PendingAbility=CombatAbility.None;}
    }
    internal static ConsumeRelease ReleasePrepared(PlayerMobile owner,SkeletonGuardian body,DateTime at)
    {
        var empty=new ConsumeRelease(false,0,null);if(!Eligible(owner,body)){return empty;}
        ConsumeEffect state;
        try
        {
            var captured=ConsumePrepared.Capture(owner,body.SourceMonsterId,SummonDifficulties.For(owner.Map));
            state=new(owner.Serial.Value,body.Serial.Value,BattleCommand.PetOrder(body),at,at.AddMilliseconds(40L*captured.DurationFrames),captured);
            ConsumeEffects.Validate(state);
        }
        catch(Exception e)when(e is NotSupportedException or System.IO.InvalidDataException or ArgumentException)
        {Log.Warning("Consume preflight refused: {Reason}",e.Message);return empty with{Fault=e.Message};}
        if(!NecromancerEffects.Pay(owner,state.Suppliers.Mana256)){return empty;}
        bool consumed=false;
        try
        {
            // Retirement,not a damage/death event:original summon equipment
            // contexts were refused above. Do not create corpses,rewards or
            // death-triggered attacks,and never return it from town parking.
            DeathMarkWorld.Remove(body);body.Delete();consumed=true;
            ConsumeEffects.ApplyPrepared(owner,state);WarlockGoatmanWorld.Enforce(owner);
            owner.FixedEffect(0x375A,10,15,0x48E,0);owner.PlaySound(0x1E5); // Identified shared buff/cast placeholders.
            return new(true,state.Suppliers.Mana256,state);
        }
        catch(Exception e)when(e is NotSupportedException or System.IO.InvalidDataException)
        {Log.Warning("Consume failed after resource commitment: {Reason}",e.Message);return new(consumed,state.Suppliers.Mana256,ConsumeEffects.State(owner),e.Message);}
    }
    internal static bool ResolvePrepared(PlayerMobile owner,long now)
    {
        var s=ClassCombat.For(owner);if(s.Consume is not{} action||action.Released||action.Cancelled){return false;}
        if(!Actor(owner)||!ShapeRestrictions.Allowed(owner,CombatAbility.Consume)||owner.Frozen||owner.Paralyzed||owner.Spell!=null||!AlliedOverlap.Allowed(owner)
            ||s.PendingAbility!=CombatAbility.Consume||owner.Map!=s.ActionMap||owner.Location!=s.ActionOrigin
            ||(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=s.PendingWeapon)
        {Cancel(owner);return false;}
        if(now<s.EffectAt){return false;}action.Released=true;s.Consume=null;s.PendingAbility=CombatAbility.None;
        action.Result=ReleasePrepared(owner,World.FindMobile(s.PendingTarget)as SkeletonGuardian,TimedEffects.Now);
        return action.Result.Consumed&&action.Result.Fault==null;
    }
}
