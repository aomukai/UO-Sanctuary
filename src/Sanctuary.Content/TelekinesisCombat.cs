// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record TelekinesisPacket(int Rank,int Minimum256,int Maximum256,FirestormModifiers Modifiers);

// do21 is an instant spell, not a weapon contact or a travelling missile.
// Reuse the shared ordered HP/death/return path and source motion controller.
internal static class TelekinesisCombat
{
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    internal static Func<uint> DefenderRoll=()=>unchecked((uint)Random.Shared.NextInt64(1L<<32));
    internal static bool Supported(PlayerMobile owner,Mobile mobile,out string reason)
    {
        reason=null;
        if(mobile is not BaseCreature{Alive:true,Deleted:false} target){reason="Telekinesis player return needs its own source adapter";return false;}
        try
        {
            var metadata=NativeMonsterReturn.Metadata(target);PreparedMonsterNativeDamage.RequireIncoming(owner,target);
            if(!metadata.Killable){reason="Recipient is not source-killable";return false;}
            if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
            {reason="Recipient uninterruptible/death-delay completion is not adapted for Telekinesis";return false;}
            if(metadata.Base==78){reason="Sand-leaper post-knockback AI completion needs its own actor adapter";return false;}
            bool canPush=(metadata.Modes&(1<<13))!=0;
            if(canPush&&(SourceKnockbacks.Rule(target) is not{Knockback:true,GetHit:true,Distance:5}
                ||target.X==owner.X&&target.Y==owner.Y))
            {reason="Recipient knockback trajectory/zero-vector path is not adapted";return false;}
            return true;
        }
        catch(NotSupportedException e){reason=e.Message;return false;}
    }
    internal static TelekinesisPacket Prepare(PlayerMobile owner,SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.Telekinesis);
        if(values.Ability!=CombatAbility.Telekinesis||values.Rank is <1 or >255||row.SourceId!=43
            ||row.Number("srvstfunc")!=12||row.Number("srvdofunc")!=21||row.Number("ResultFlags")!=5
            ||row.Number("HitFlags")!=2||row.Number("HitClass")!=109||row.Text("EType")!="ltng"
            ||row.Number("SrcDam")!=0||values.Mana256!=1792||values.Eval("aurarangecalc")!=25||values.Param(2)!=35)
        {throw new NotSupportedException("Unsupported pinned Telekinesis route");}
        var modifiers=DruidFirestorm.Modifiers(owner);
        // No missile factory is involved, so its Slow Missiles payload is not
        // imported into this direct SKILLS elemental roll.
        var ends=DruidFirestorm.Endpoints(values,modifiers.LightningMastery,SlowMissilePayload.Normal);
        return new(values.Rank,ends.Min,ends.Max,modifiers);
    }
    internal static PreparedMonsterDamagePacket Roll(TelekinesisPacket prepared,ref ulong seed)
    {
        if(prepared is not{Rank:>=1 and <=255}||prepared.Minimum256<0||prepared.Maximum256<prepared.Minimum256)
        {throw new NotSupportedException("Invalid Telekinesis endpoints");}
        int raw=SorceressSpellMissiles.Roll(prepared.Minimum256,prepared.Maximum256,seed,out seed);
        int flags=5;if(SourceRandom.Roll(ref seed)%100<35){flags|=9;}
        return new(0,raw,0,0,flags,109,Lightning256:raw,HitFlags:2);
    }
    internal static bool Release(PlayerMobile owner,Serial selected,SkillValues values)
    {
        if(World.FindMobile(selected) is not BaseCreature target||!Supported(owner,target,out _)){return false;}
        var prepared=Prepare(owner,values);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        ulong seed=SourceRandom.InitLow(SeedLow());var packet=Roll(prepared,ref seed);
        Deliver(owner,target,packet,prepared.Modifiers,TimedEffects.Now);
        owner.PlaySound(0x1E5);return true; // Identified cast/audio placeholder.
    }
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,PreparedMonsterDamagePacket packet,FirestormModifiers modifiers,DateTime at)
    {
        if(!Supported(owner,target,out string reason)||at.Kind!=DateTimeKind.Utc
            ||packet.Physical256!=0||packet.ColdFrames!=0||packet.StunFrames!=0||packet.HitClass!=109||packet.HitFlags!=2
            ||packet.ResultFlags is not(5 or 13)||packet.Total256!=packet.Lightning256||packet.Fire256!=0||packet.Cold256!=0
            ||packet.Poison256!=0||packet.Magic256!=0||packet.Overlay!=0||packet.AbsorbedLife256!=0||packet.HitClassActive)
        {throw new NotSupportedException(reason??"Unmapped Telekinesis delivery packet");}
        var metadata=NativeMonsterReturn.Metadata(target);NativeMonsterReturn.Session(target);
        var services=new InstantSpellNpcDamage(owner,target,metadata,modifiers,at,"Telekinesis",()=>DefenderRoll(),SourceKnockbacks.BeginPrepared);
        var recipient=new PreparedMonsterRecipient(NamedCombat.Elite(target),()=>false,()=>metadata.Velocity,()=>false);
        return PreparedMonsterDamage.ReleaseMissile(owner,target,packet,recipient,_=>throw new InvalidOperationException("Telekinesis has no timed Stun roll"),services,at);
    }
}
