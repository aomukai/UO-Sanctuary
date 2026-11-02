// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

// One typed field hit path for Blaze, Fire Wall and Meteor. Producers own creation,
// saved lifetime/movement and their pinned row/rate contracts.
internal static class SourceFireFields
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SourceFireFields));
    private static readonly HashSet<string> Warned=new();
    internal static void Gap(string reason){if(Warned.Add(reason)){Log.Warning("Ground fire unsupported context: {Reason}",reason);}}
    internal static PreparedMonsterDamagePacket Roll(int minimum,int maximum,ref ulong seed)
    {
        int raw=SorceressSpellMissiles.Roll(minimum,maximum,seed,out seed);
        int soft=(SourceRandom.Roll(ref seed)&127)<19?16384:0;
        return new(0,raw,0,0,1|soft,32,true,Fire256:raw);
    }
    internal static bool Supported(PlayerMobile owner,Mobile actor,out string reason)
    {
        reason=null;
        if(actor is not BaseCreature{Alive:true,Deleted:false} target){reason="Player/PvP return needs its own prepared adapter";return false;}
        if(target.Map!=owner.Map||!target.CanBeDamaged()){reason="Field recipient is not in the caster combat world";return false;}
        try
        {
            PreparedMonsterNativeDamage.RequireIncoming(owner,target);
            if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
            {reason="Uninterruptible/death-delay field completion needs its actor adapter";return false;}
            return true;
        }
        catch(NotSupportedException e){reason=e.Message;return false;}
    }
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,PreparedMonsterDamagePacket packet,FirestormModifiers mods,DateTime at,int damageRate=0)
    {
        if(damageRate is not(0 or 41)){throw new NotSupportedException("Unsupported ground-fire damage rate");}
        if(!Supported(owner,target,out string reason)||packet.Physical256!=0||packet.StunFrames!=0||packet.ColdFrames!=0
            ||packet.HitClass!=32||!packet.HitClassActive||packet.HitFlags!=0||packet.ResultFlags is not(1 or 16385)
            ||packet.Total256!=packet.Fire256||packet.Magic256!=0||packet.Lightning256!=0||packet.Cold256!=0||packet.Poison256!=0)
        {throw new NotSupportedException(reason??"Unmapped ground fire packet");}
        // No block/KB/GETHIT requests: identity/mode/velocity fields are unused,
        // as in Shiver's SOFTHIT path. Do not invent a D2 species for UO actors.
        NativeMonsterReturn.Session(target); // Capture pre-hit life presentation state.
        var services=new InstantSpellNpcDamage(owner,target,new(-1,-1,0,0,false,"Ground fire unused return identity"),mods,at,"Ground fire",
            ()=>throw new InvalidOperationException("Ground fire must not draw defender get-hit RNG"),damageRate:damageRate);
        return PreparedMonsterDamage.ReleaseMissile(owner,target,packet,new(NamedCombat.Elite(target),()=>false,()=>0,()=>false),
            _=>throw new InvalidOperationException("Ground fire has no timed stun"),services,at);
    }
    internal static void Burn(PlayerMobile owner,FirePatch patch,DateTime at,ref ulong seed)
    {
        if(ImmolationArrows.Recipient(owner,patch) is not{} target){return;}
        if(!Supported(owner,target,out string reason)){Gap(reason);return;}
        var packet=Roll(patch.Min256,patch.Max256,ref seed);Deliver(owner,(BaseCreature)target,packet,patch.BlazeModifiers??patch.FireWallModifiers??patch.MeteorModifiers,at,patch.DamageRate);
    }
}
