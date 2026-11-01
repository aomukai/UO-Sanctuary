// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record ShiverPacket(int Rank,int Minimum256,int Maximum256,int ColdFrames,FirestormModifiers Modifiers);

internal static class ShiverArmor
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ShiverArmor));
    private static readonly HashSet<string> Warned=new();
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    private static void Gap(string message)
    {if(Warned.Add(message)){Log.Warning("Shiver Armor unsupported context: {Reason}",message);}}
    internal static ShiverPacket Prepare(PlayerMobile owner,ColdArmorState armor)
    {
        var row=SkillMechanics.For(CombatAbility.ShiverArmor);
        if(armor is not{Ability:CombatAbility.ShiverArmor,Rank:>=0 and <=255}||row.SourceId!=50||row.Number("srvdofunc")!=18
            ||row.Number("auraeventfunc1")!=3||row.Text("auraevent1")!="attackedinmelee"||row.Text("EType")!="cold"||row.Number("SrcDam")!=0)
        {throw new NotSupportedException("Unsupported pinned Shiver Armor reaction");}
        var mods=DruidFirestorm.Modifiers(owner);
        var v=armor.Rank>0?SkillMechanics.Values(owner,CombatAbility.ShiverArmor,armor.Rank):null;
        var ends=v==null?DruidFirestorm.Endpoints(armor.Min256,armor.Max256,mods.ColdMastery,SlowMissilePayload.Normal)
            :DruidFirestorm.Endpoints(v,mods.ColdMastery,SlowMissilePayload.Normal);
        // Direct skill callback: no missile factory Slow Missiles payload.
        return new(armor.Rank,ends.Min,ends.Max,v?.DurationFrames??armor.ColdFrames,mods);
    }
    internal static PreparedMonsterDamagePacket Roll(ShiverPacket prepared,ref ulong seed)
    {
        if(prepared.Rank is <0 or >255||prepared.Minimum256<0||prepared.Maximum256<prepared.Minimum256||prepared.ColdFrames<0)
        {throw new NotSupportedException("Invalid Shiver Armor packet");}
        int raw=SorceressSpellMissiles.Roll(prepared.Minimum256,prepared.Maximum256,seed,out seed);
        // Typed cold mapping supplies48 before event03 ORs13; actual class61.
        return new(0,raw,0,prepared.ColdFrames,16417,61,true,Cold256:raw);
    }
    internal static bool Supported(PlayerMobile owner,Mobile actor,out string reason)
    {
        reason=null;
        if(actor is BaseHire){reason="Hireling early/duplicate melee notification paths need their source adapter";return false;}
        if(actor is not BaseCreature{Alive:true,Deleted:false} target){reason="Player return/PvP needs its own source adapter";return false;}
        if(target.Map==null||target.Map==Map.Internal||target.Map!=owner.Map||!target.CanBeDamaged()
            ||SpellHelper.IsTown(owner.Location,owner)||SpellHelper.IsTown(target.Location,owner)) {reason="Reaction requires an eligible combat world recipient";return false;}
        try
        {
            PreparedMonsterNativeDamage.RequireIncoming(owner,target);
            if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
            {reason="Uninterruptible/death-delay completion needs its actor adapter";return false;}
            return true;
        }
        catch(NotSupportedException e){reason=e.Message;return false;}
    }
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,PreparedMonsterDamagePacket packet,FirestormModifiers mods,DateTime at)
    {
        if(!Supported(owner,target,out string reason)||at.Kind!=DateTimeKind.Utc||packet.Physical256!=0||packet.StunFrames!=0
            ||packet.ResultFlags!=16417||packet.HitClass!=61||!packet.HitClassActive||packet.HitFlags!=0||packet.Overlay!=0
            ||packet.Total256!=packet.Cold256||packet.Fire256!=0||packet.Lightning256!=0||packet.Magic256!=0||packet.Poison256!=0||packet.AbsorbedLife256!=0)
        {throw new NotSupportedException(reason??"Unmapped Shiver Armor packet");}
        // SOFTHIT with no get-hit/block/knockback and active class never reads
        // species/class/base/mode/velocity fields. These are unused sentinels,
        // not invented D2 identities for native UO monsters.
        var metadata=new MonsterReturnMetadata(-1,-1,0,0,true,"SOFTHIT unused identity fields");
        var services=new InstantSpellNpcDamage(owner,target,metadata,mods,at,"Shiver Armor",
            ()=>throw new InvalidOperationException("SOFTHIT must not draw defender recovery RNG"),cold:true);
        using var suppressed=CombatItemEvents.Suppress(owner,target);
        var result=PreparedMonsterDamage.ReleaseMissile(owner,target,packet,new(NamedCombat.Elite(target),()=>false,()=>0,()=>false),
            _=>throw new InvalidOperationException("Shiver Armor has no timed stun"),services,at);
        if(!target.Deleted){target.FixedEffect(0x37B9,10,12,0x480,0);} // Identified hit overlay placeholder, after return.
        return result;
    }
    internal static bool React(PlayerMobile owner,Mobile target,ColdArmorState armor)
    {
        if(!Supported(owner,target,out string reason)){Gap(reason);return false;}
        var prepared=Prepare(owner,armor);ulong seed=SourceRandom.InitLow(SeedLow());var packet=Roll(prepared,ref seed);
        Deliver(owner,(BaseCreature)target,packet,prepared.Modifiers,TimedEffects.Now);return true;
    }
    internal static IDisposable Attempt(Mobile attacker,Mobile defender,bool ranged,IDisposable thorns)
    {
        if(ranged||attacker is not BaseCreature||defender is not PlayerMobile owner
            ||SorceressEffects.Armor(owner) is not{Ability:CombatAbility.ShiverArmor}){return thorns;}
        return new AttemptScope(attacker,owner,thorns);
    }
    private sealed class AttemptScope(Mobile attacker,PlayerMobile owner,IDisposable thorns):IDisposable
    {
        private bool Done;
        public void Dispose()
        {
            if(Done){return;}Done=true;
            try
            {
                if(!owner.Alive){Gap("Late reaction after native player death requires a deferred player HP/death bridge");return;}
                if(attacker is{Alive:true,Deleted:false}&&attacker.Map==owner.Map&&attacker.InRange(owner,1)
                    &&ClassCombat.IsHostile(owner,attacker)&&SorceressEffects.Armor(owner) is{Ability:CombatAbility.ShiverArmor} armor)
                {React(owner,attacker,armor);}
            }
            finally{thorns?.Dispose();}
        }
    }
}
