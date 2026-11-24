// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ConsumeRelease(bool Consumed,int ManaPaid256,ConsumeEffect Effect,string Fault=null);
internal static class ConsumeCasting
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ConsumeCasting));
    internal sealed class Pending(long order)
    {
        internal readonly long Order=order;
        internal bool Released,Cancelled;
        internal ConsumeRelease Result;
    }
    internal static bool Actor(PlayerMobile owner)=>owner is{Alive:true,Deleted:false,NetState:{Running:true},Map:not null}
        &&owner.Map!=Map.Internal&&ClassCreation.Profiles.TryGet(owner.Serial,out var p)&&p.Class==DiabloClass.Warlock
        &&SkillMechanics.Rank(owner,CombatAbility.Consume)>0;
    internal static string Monster(BaseCreature body)=>body is WarlockGoatmanBody or WarlockTaintedBody or WarlockDefilerBody
        ?((SkeletonGuardian)body).SourceMonsterId:BoundDemonHosts.State(body)?.Monster;
    internal static bool Eligible(PlayerMobile owner,BaseCreature body)
    {
        if(!Actor(owner)||!WarlockDemonRecipients.Owned(owner,body))
        {return false;}
        try{if(!ConsumePrepared.RuntimeSupported(ConsumePrepared.Capture(owner,Monster(body),SummonDifficulties.For(owner)))){return false;}}
        catch(Exception e)when(e is NotSupportedException or System.IO.InvalidDataException or ArgumentException){return false;}
        if(MinionSafeAreas.Parked(body)){return MinionSafeAreas.Contains(owner);}
        if(!MinionSafeAreas.Contains(owner)){return WarlockDemonRecipients.InRange(owner,body,19)&&owner.InLOS(body);}
        return body.Map==owner.Map&&!body.Blessed&&body.GetDistanceToSqrt(owner)<=19&&owner.InLOS(body);
    }
    internal static BaseCreature[] Roster(PlayerMobile owner)=>Actor(owner)?WarlockDemonRecipients.Roster(owner).Where(b=>Eligible(owner,b)).ToArray():[];
    internal static bool OpenPrepared(PlayerMobile owner)
    {
        if(!Actor(owner)){return false;}
        foreach(var body in WarlockDemonRecipients.Roster(owner)){MinionSafeAreas.Enforce(body);}
        var bodies=Roster(owner);if(bodies.Length==0){return false;}
        owner.CloseGump<ConsumeRosterGump>();owner.SendGump(new ConsumeRosterGump(owner,bodies));return true;
    }
    internal static Pending BeginPrepared(PlayerMobile owner,Serial selected,long now)
    {
        if(!Eligible(owner,World.FindMobile(selected)as BaseCreature)){return null;}
        var body=(BaseCreature)World.FindMobile(selected);long order=BattleCommand.PetOrder(body);var point=body.Map==owner.Map?body.Location:owner.Location;
        if(!SkillCombat.BeginPrepared(owner,CombatAbility.Consume,selected,point,now,
            (who,_,id,at)=>World.FindMobile(id)is BaseCreature b&&BattleCommand.PetOrder(b)==order&&Eligible(who,b),(who,_)=>Actor(who),CombatTiming.For(owner,CombatAbility.Consume)))
        {return null;}
        var pending=new Pending(order);ClassCombat.For(owner).Consume=pending;return pending;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        var s=ClassCombat.Existing(owner);if(s?.Consume is not{} action){return;}action.Cancelled=true;s.Consume=null;
        if(s.PendingAbility==CombatAbility.Consume){s.PendingAbility=CombatAbility.None;}
    }
    internal static ConsumeRelease ReleasePrepared(PlayerMobile owner,BaseCreature body,DateTime at)
    {
        var empty=new ConsumeRelease(false,0,null);if(!Eligible(owner,body)){return empty;}
        ConsumeEffect state;
        try
        {
            var captured=ConsumePrepared.Capture(owner,Monster(body),SummonDifficulties.For(owner));ConsumePrepared.RequireRuntime(captured);
            state=new(owner.Serial.Value,body.Serial.Value,BattleCommand.PetOrder(body),at,at.AddMilliseconds(40L*captured.DurationFrames),captured);
            ConsumeEffects.Validate(state);
        }
        catch(Exception e)when(e is NotSupportedException or System.IO.InvalidDataException or ArgumentException)
        {Log.Warning("Consume preflight refused: {Reason}",e.Message);return empty with{Fault=e.Message};}
        if(!NecromancerEffects.Pay(owner,state.Suppliers.Mana256)){return empty;}
        var origin=body.Location;var map=body.Map;
        bool visibleSource=map==owner.Map&&!body.Hidden;
        bool consumed=false;
        try
        {
            // Retirement,not a damage/death event:original summon equipment
            // contexts were refused above. Do not create corpses,rewards or
            // death-triggered attacks,and never return it from town parking.
            if(BoundDemonHosts.Active(body)){BoundDemonHosts.RetirePrepared(body);}
            else
            {
                WarlockDemonVisuals.Depart(body,show:false);
                DeathMarkWorld.Remove((SkeletonGuardian)body);body.Delete();
            }
            consumed=true;
            if(visibleSource)
            {
                WarlockRitualVisuals.Ground(map,origin,SpellEffectGraphics.ConsumeSource,SpellEffectSounds.ConsumeSource);
                if(!owner.Hidden&&owner.Map==map)
                {WarlockRitualVisuals.Trail(map,origin,owner.Location,SpellEffectGraphics.ConsumeTravel);}
            }
            ConsumeEffects.ApplyPrepared(owner,state);WarlockGoatmanWorld.Enforce(owner);
            WarlockRitualVisuals.Attached(owner,SpellEffectGraphics.ConsumeCast,SpellEffectSounds.ConsumeCast);
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
        var body=World.FindMobile(s.PendingTarget)as BaseCreature;
        action.Result=body!=null&&BattleCommand.PetOrder(body)==action.Order?ReleasePrepared(owner,body,TimedEffects.Now):new(false,0,null);
        return action.Result.Consumed&&action.Result.Fault==null;
    }
}
