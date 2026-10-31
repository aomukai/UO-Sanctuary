// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record PreparedMonsterDamagePacket(int Physical256,int Total256,int StunFrames,int ColdFrames,int ResultFlags,
    int HitClass=0,bool HitClassActive=false,int Overlay=0,int Magic256=0,int Fire256=0,int Lightning256=0,int Cold256=0,int Poison256=0,uint HitFlags=0,int AbsorbedLife256=0);
internal sealed record PreparedMonsterRecipient(bool Unique,Func<bool> Boss,Func<int> Velocity,Func<bool> Hireling);
// These services are mandatory. Supplying the actual callbacks, metadata and
// return/mode policy remains the actor consumer's job; no missing event is
// silently replaced by an inferred proc or a native Mobile.Damage replay.
internal abstract class PreparedMonsterDamageServices
{
    internal abstract void StoreLastAttacker();
    internal abstract PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket packet);
    internal abstract PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket packet);
    // Ordinary leech/actor recovery services run here after physical clipping.
    // Life Tap and packet absorb healing are owned by the shared pipeline.
    internal abstract void BeforeHealth(PreparedMonsterDamagePacket clipped);
    internal abstract void ApplyCold(int frames);
    internal abstract void SurvivedDamage();
    internal abstract PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket packet);
    internal abstract PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket packet);
    internal abstract void PresentOverlay(int overlay);
    internal abstract int BaseHitClass();
    internal abstract PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket packet);
    internal abstract PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket packet);
    internal abstract bool AttackerInActiveMode();
    // Diagnostic/service boundary before shared percentage return. Consumers
    // must not replay the shared flat or percentage damage in this callback.
    internal abstract void BeforeThorns(PreparedMonsterDamagePacket packet);
    internal abstract void Return(ref PreparedMonsterDamagePacket packet,Action enterDeathMode);
    internal abstract void FreePacket();
}
internal sealed record PreparedMonsterDamageResult(PreparedMonsterDamagePacket Packet,int Before256,int AfterHealth256,
    int StunFrames,bool DeathModeAttempted);
internal sealed record PreparedMonsterCommitResult(PreparedMonsterDamagePacket Packet,int Before256,int AfterHealth256,int StunFrames);
internal sealed record PreparedMonsterFinishResult(PreparedMonsterDamagePacket Packet,bool DeathModeAttempted);

internal static class PreparedMonsterDamage
{
    private static PreparedMonsterDamagePacket Checked(PreparedMonsterDamagePacket p)
    {
        if(p==null||p.Physical256<0||p.Total256<0||p.StunFrames<0||p.ColdFrames<0||p.Magic256<0||p.Fire256<0||p.Lightning256<0||p.Cold256<0||p.Poison256<0||p.AbsorbedLife256<0||(p.ResultFlags&~0xffff)!=0)
        {throw new NotSupportedException("Unsupported prepared monster damage packet");}
        return p;
    }
    // Qualified ordinary monster-to-monster melee only: the caller has already
    // checked target/room/town/hostility/killable/death/contact and captured the
    // packet. Leech/absorb/regen/proc services are explicit, not approximated.
    internal static PreparedMonsterDamageResult Release(BaseCreature attacker,BaseCreature target,PreparedMonsterDamagePacket packet,
        PreparedMonsterRecipient recipient,Func<int,int> limited,PreparedMonsterDamageServices services,DateTime at)
    {
        var commit=CommitQualified(attacker,target,packet,recipient,limited,services,at);
        var finish=Finish(attacker,target,commit.Packet,services);
        return new(finish.Packet,commit.Before256,commit.AfterHealth256,commit.StunFrames,finish.DeathModeAttempted);
    }
    // The contact wrapper can reject health/event eligibility while still
    // continuing attack-attempt callbacks and return. Keep that boundary
    // separate from the already-qualified health/Stun/death event phase.
    internal static PreparedMonsterCommitResult CommitQualified(BaseCreature attacker,BaseCreature target,PreparedMonsterDamagePacket packet,
        PreparedMonsterRecipient recipient,Func<int,int> limited,PreparedMonsterDamageServices services,DateTime at)
    {
        Checked(packet);ArgumentNullException.ThrowIfNull(recipient);ArgumentNullException.ThrowIfNull(limited);ArgumentNullException.ThrowIfNull(services);
        if(attacker is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}||!target.CanBeDamaged()
            ||SkillHealth.Balance256(target)<=0||(packet.ResultFlags&1)==0||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Monster release requires qualified living source/recipient and a successful cached packet");}
        services.StoreLastAttacker();
        MonsterUninterruptibles.RecordLastAttacker(target,attacker);
        if((packet.ResultFlags&32)==0)
        {
            packet=Checked(services.DealtDamage(packet));packet=Checked(services.TookDamage(packet));
            NecromancerEffects.LifeTap(attacker,target,packet.Physical256);
        }
        int before=SkillHealth.Balance256(target);
        packet=packet with{Physical256=Math.Min(packet.Physical256,before)};
        services.BeforeHealth(packet);
        if(packet.AbsorbedLife256>0&&!MonsterUninterruptibles.Delayed(target)){SkillHealth.Heal(target,packet.AbsorbedLife256);}
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<0)
        {throw new NotSupportedException("Reentrant native deletion/death before prepared health commitment is unsupported");}
        int after=SkillHealth.Balance256(target);
        if(packet.Total256>0)
        {
            after-=packet.Total256;if(after<256){after=0;}
            // Write source HP exactly once. Native Damage would dispatch death
            // before Stun and replay unrelated native damage callbacks.
            SkillHealth.SetBalance256(target,after);
            if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)!=after)
            {throw new NotSupportedException("Reentrant native health-setter mutation/death is unsupported");}
        }
        int stun=PreparedMonsterStun.Resolve(packet.StunFrames,recipient.Unique,limited,recipient.Boss,recipient.Velocity,recipient.Hireling);
        if(stun>0&&!CombatStuns.ApplyPreparedDuration(target,stun,at))
        {throw new NotSupportedException("Prepared monster Stun expired or recipient changed during release");}
        services.ApplyCold(packet.ColdFrames);
        packet=packet with{ResultFlags=SkillHealth.Balance256(target)<=0?packet.ResultFlags|2:packet.ResultFlags&~2};
        if((packet.ResultFlags&32)==0&&(packet.ResultFlags&2)!=0)
        {packet=Checked(services.Killed(packet));packet=Checked(services.Kill(packet));}
        else if((packet.ResultFlags&2)==0&&packet.Total256!=0){services.SurvivedDamage();}
        return new(packet,before,after,stun);
    }
    internal static PreparedMonsterFinishResult Finish(BaseCreature attacker,BaseCreature target,PreparedMonsterDamagePacket packet,
        PreparedMonsterDamageServices services,bool hitPresentation=true)
    {
        Checked(packet);ArgumentNullException.ThrowIfNull(services);
        if(hitPresentation)
        {
            if(packet.Overlay>0){services.PresentOverlay(packet.Overlay);}
            if(!packet.HitClassActive&&(packet.HitClass&15)==0){packet=packet with{HitClass=packet.HitClass|services.BaseHitClass()};}
        }
        packet=Checked(services.MeleeAttack(packet));packet=Checked(services.AttackedInMelee(packet));
        PaladinThorns.PreparedFlatAttempt(attacker,target);
        if((packet.ResultFlags&1)!=0&&services.AttackerInActiveMode())
        {services.BeforeThorns(packet);PaladinThorns.PreparedPercentage(attacker,target,packet.Physical256);}
        bool death=false,inReturn=true;
        void EnterDeathMode()
        {
            if(!inReturn){throw new InvalidOperationException("Prepared death callback escaped its return phase");}
            if(death){throw new InvalidOperationException("Prepared death mode already consumed");}
            if((packet.ResultFlags&2)==0){throw new InvalidOperationException("No prepared death result to consume");}
            death=true;
            if(target is{Alive:true,Deleted:false}){target.LastKiller=attacker;target.Kill();}
        }
        try{services.Return(ref packet,EnterDeathMode);Checked(packet);}finally{inReturn=false;}
        services.FreePacket();
        return new(packet,death);
    }
}
