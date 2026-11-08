// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Own NPC start24/do42 cache and repeated contacts. The approved Talon rule
// supplies modern per-successful-kick charges;NPC startup has no mana debit.
internal static class ShadowDragonTalon
{
    internal sealed class State(int remaining)
    {
        internal int Remaining=remaining;
        internal IronGolemContact.Prepared Cached;
        internal bool Rewind;
    }
    internal static ItemExecutionMetadata Row()
    {
        var r=ShadowKickPackets.Row();
        if(r.InTown||r.UseManaOnDo||r.Lob||r.DecrementQuantity||r.Missile!=-1||r.Number("TargetableOnly")!=1
            ||r.Number("LineOfSight")!=0||r.Number("UseAttackRate")!=0||ShadowSkillBook.Metadata(255).Mode!=4
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unqualified original Shadow Talon action row");}
        return r;
    }
    internal static bool Continues(AssassinShadowBody body,BaseCreature target)=>ShadowDragonClaw.Continues(body,target);
    private static bool Range(AssassinShadowBody body,BaseCreature target,int bonus)=>IronGolemWorld.Melee(body,
        new(body.Map.MapID,DruidRavenMotion.Center(body.X*2,body.Y*2),TimedEffects.Now),target,bonus);
    private static IronGolemContact.Prepared Allocate(ShadowSkillCasting.Cast cast,bool knockback,DateTime at)
        =>ShadowKickContacts.Allocate(cast,255,knockback,at);
    internal static int Start(ShadowSkillCasting.Cast cast,int rank)
    {
        _=Row();if(!Continues(cast.Body,cast.Target)){return 0;}
        int remaining=checked((int)ShadowSkillCasting.OwnValues(cast.Body,rank,255).Eval("calc1")-1);
        var state=new State(remaining);state.Cached=Allocate(cast,remaining==0,cast.StartedUtc);cast.Talon=state;return 1;
    }
    private static bool Drain(ShadowSkillCasting.Cast cast,IronGolemContact.Prepared packet,DateTime at)
    {
        var body=cast.Body;var target=cast.Target;
        if(NpcEquipmentImpact.Fills(packet.Impact.Packet.ResultFlags)&&Continues(body,target))
        {
            cast.ServerDo=true;
            var states=ShadowFinisherDamage.States(body,target,at);var effects=ShadowFinisherDamage.Effects(body,states);
            ShadowFinishers.Emit(body,target,packet,states,effects.Fire,effects.Ice,effects.Thunder,at,Row().Number("prgchargesconsumed"));
        }
        _=IronGolemContact.Release(body,(PlayerMobile)body.ControlMaster,target,packet,b=>Range(body,target,b),at);
        return !cast.Released&&Continues(body,target);
    }
    internal static int Release(ShadowSkillCasting.Cast cast,int rank,DateTime at)
    {
        var state=cast.Talon??throw new NotSupportedException("Shadow Talon lost its own startup cache");state.Rewind=false;
        if(!Continues(cast.Body,cast.Target)){return 0;}
        if(state.Cached is{} cached)
        {
            state.Cached=null;
            if(!Drain(cast,cached,at)){return 0;}
        }
        int remaining=state.Remaining-1;if(remaining<0){return 0;}state.Remaining=remaining;
        bool knockback=false;
        if(remaining==0)
        {
            var target=cast.Target;var v=ShadowSkillCasting.OwnValues(cast.Body,rank,255);
            int chance=DragonTalons.KnockbackChance(v,target is EncounterCreature{Grade:EncounterGrade.Unique or EncounterGrade.SuperUnique},CreatureProfiles.For(target).Traits.Boss==true,target is BaseHire);
            knockback=chance>0&&NpcEquipmentRandom.Raw(cast.Body)%100<chance;
        }
        var next=Allocate(cast,knockback,at);
        if(!Drain(cast,next,at)){return 1;}
        if(remaining>0){cast.ServerDo=false;state.Rewind=true;}return 1;
    }
    internal static int Duration(ShadowAnimationPlan plan,int kicks)=>checked(plan.Events.Single(e=>e.Type==2).Frame+
        Math.Max(0,kicks-2)*plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame);
}
