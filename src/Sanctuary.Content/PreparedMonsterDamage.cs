// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record PreparedMonsterDamagePacket(int Physical256,int Total256,int StunFrames,int ColdFrames,int ResultFlags,
    int HitClass=0,bool HitClassActive=false,int Overlay=0,int Magic256=0,int Fire256=0,int Lightning256=0,int Cold256=0,int Poison256=0,uint HitFlags=0,int AbsorbedLife256=0,int LifeDrain256=0,int ManaDrain256=0,int StaminaDrain256=0,int FreezeFrames=0);
internal sealed record PreparedMonsterRecipient(bool Unique,Func<bool> Boss,Func<int> Velocity,Func<bool> Hireling);
// These services are mandatory. Supplying the actual callbacks, metadata and
// return/mode policy remains the actor consumer's job; no missing event is
// silently replaced by an inferred proc or a native Mobile.Damage replay.
internal abstract class PreparedMonsterDamageServices
{
    // Only an explicit retained-caster service can admit a dead missile source.
    internal virtual bool RetainedDeadSource(Mobile source)=>false;
    internal abstract void StoreLastAttacker();
    internal abstract PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket packet);
    internal abstract PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket packet);
    internal virtual PreparedMonsterDamagePacket MitigateMissile(PreparedMonsterDamagePacket packet)
        =>throw new NotSupportedException("Missile delivery requires its explicit mitigation service");
    // Ordinary leech/actor recovery services run here after physical clipping.
    // Life Tap and packet absorb healing are owned by the shared pipeline.
    internal abstract void BeforeHealth(PreparedMonsterDamagePacket clipped);
    internal virtual bool OrdinaryNpcFlatDrains=>false;
    // Actor adapters can dispatch native notifications around the real credit
    // write here, without replaying Mobile.Damage or committing HP early.
    internal virtual void BeforeHealthCommit(Mobile attacker,BaseCreature target,int after256)
        =>PreparedMonsterCredit.Register(attacker,target,after256);
    internal abstract void ApplyCold(int frames);
    internal virtual bool SupportsFreeze=>false;
    internal virtual void ApplyFreeze(int frames){if(frames!=0){throw new NotSupportedException("Prepared freeze needs its explicit recipient adapter");}}
    internal virtual void ApplyPoison(){}
    internal abstract void SurvivedDamage();
    internal abstract PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket packet);
    internal abstract PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket packet);
    internal abstract void PresentOverlay(int overlay);
    internal abstract int BaseHitClass();
    // Players wear their weapon after stored damage/presentation and before
    // attempt events. NPC actors have no corresponding inventory action.
    internal virtual void AfterHitPresentation(PreparedMonsterDamagePacket packet){}
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
        if(p==null||p.Physical256<0||p.Total256<0||p.StunFrames<0||p.ColdFrames<0||p.Magic256<0||p.Fire256<0||p.Lightning256<0||p.Cold256<0||p.Poison256<0||p.AbsorbedLife256<0||p.LifeDrain256<0||p.ManaDrain256<0||p.StaminaDrain256<0||p.FreezeFrames<0||(p.ResultFlags&~0xffff)!=0)
        {throw new NotSupportedException("Unsupported prepared monster damage packet");}
        return p;
    }
    // Qualified ordinary player/NPC-to-NPC melee: the caller has already
    // checked target/room/town/hostility/killable/death/contact and captured the
    // packet. Leech/absorb/regen/proc services are explicit, not approximated.
    internal static PreparedMonsterDamageResult Release(Mobile attacker,BaseCreature target,PreparedMonsterDamagePacket packet,
        PreparedMonsterRecipient recipient,Func<int,int> limited,PreparedMonsterDamageServices services,DateTime at)
    {
        var commit=CommitQualified(attacker,target,packet,recipient,limited,services,at);
        var finish=Finish(attacker,target,commit.Packet,services);
        return new(finish.Packet,commit.Before256,commit.AfterHealth256,commit.StunFrames,finish.DeathModeAttempted);
    }
    // Instant spells use the same source HP/Stun/kill/return phase, without
    // melee attempt/presentation/Thorns events. Hit flags select the source's
    // optional missile-damage events independently of this delivery kind.
    internal static PreparedMonsterDamageResult ReleaseMissile(Mobile attacker,BaseCreature target,PreparedMonsterDamagePacket packet,
        PreparedMonsterRecipient recipient,Func<int,int> limited,PreparedMonsterDamageServices services,DateTime at)
    {
        var commit=CommitQualified(attacker,target,packet,recipient,limited,services,at,missile:true);
        var finish=Finish(attacker,target,commit.Packet,services,hitPresentation:false,missile:true);
        return new(finish.Packet,commit.Before256,commit.AfterHealth256,commit.StunFrames,finish.DeathModeAttempted);
    }
    // Item function14 emits a zero-damage stack packet with no success flag.
    // Its nested ExecuteEvents commits status only;it does not run missile
    // return,attack-attempt,hit-presentation or an invented successful hit.
    internal static PreparedMonsterCommitResult CommitStatus(Mobile attacker,BaseCreature target,PreparedMonsterDamagePacket packet,
        PreparedMonsterRecipient recipient,Func<int,int> limited,PreparedMonsterDamageServices services,DateTime at)
    {
        CheckedStatus(packet);
        return CommitQualified(attacker,target,packet,recipient,limited,services,at,missile:true,statusOnly:true);
    }
    private static void CheckedStatus(PreparedMonsterDamagePacket packet)
    {
        Checked(packet);
        if(packet.ResultFlags!=0||packet.HitFlags!=0||packet.Total256!=0||packet.Physical256!=0||packet.Magic256!=0||packet.Fire256!=0||packet.Lightning256!=0||packet.Cold256!=0||packet.Poison256!=0
            ||packet.AbsorbedLife256!=0||NpcFlatDrains.Any(packet))
        {throw new NotSupportedException("Nested status event requires its actual zero-damage,zero-result packet");}
    }
    // The contact wrapper can reject health/event eligibility while still
    // continuing attack-attempt callbacks and return. Keep that boundary
    // separate from the already-qualified health/Stun/death event phase.
    internal static PreparedMonsterCommitResult CommitQualified(Mobile attacker,BaseCreature target,PreparedMonsterDamagePacket packet,
        PreparedMonsterRecipient recipient,Func<int,int> limited,PreparedMonsterDamageServices services,DateTime at,bool missile=false,bool statusOnly=false)
    {
        Checked(packet);if(statusOnly){CheckedStatus(packet);}
        ArgumentNullException.ThrowIfNull(recipient);ArgumentNullException.ThrowIfNull(limited);ArgumentNullException.ThrowIfNull(services);
        if(attacker is not(PlayerMobile or BaseCreature)||attacker.Deleted||!attacker.Alive&&!(missile&&services.RetainedDeadSource(attacker))||target is not{Alive:true,Deleted:false}||!target.CanBeDamaged()
            ||SkillHealth.Balance256(target)<=0||!statusOnly&&(packet.ResultFlags&1)==0||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Monster release requires qualified living source/recipient and a successful cached packet");}
        if(packet.FreezeFrames>0&&!services.SupportsFreeze){throw new NotSupportedException("Prepared freeze needs its qualified status receiver before HP/events");}
        if(!missile&&(packet.ResultFlags&32)==0){NpcLightningReturn.RequireIncoming(attacker,target,false);}
        NpcFlatDrains.Require(attacker,packet,services.OrdinaryNpcFlatDrains);
        using var credit=PreparedMonsterCredit.CaptureOwner(attacker,target);
        services.StoreLastAttacker();
        MonsterUninterruptibles.RecordLastAttacker(target,attacker);
        if(missile){packet=Checked(services.MitigateMissile(packet));}
        if((packet.ResultFlags&32)==0)
        {
            var incoming=!missile||(packet.HitFlags&32)!=0?NpcItemProcs.RequireIncoming(attacker,target,packet):null;
            if(missile)
            {
                if((packet.HitFlags&32)!=0)
                {
                    if((packet.HitFlags&128)==0){packet=Checked(services.DealtDamage(packet));}
                    packet=Checked(services.TookDamage(packet));
                    NpcItemProcs.Received("damagedbymissile",attacker,target,packet,at,incoming);
                    NecromancerEffects.LifeTap(attacker,target,packet.Physical256); // Pinned Life Tap also registers damagedbymissile.
                }
            }
            else
            {
                packet=Checked(services.DealtDamage(packet));
                IronMaiden.MeleeDamage(attacker,target,packet.Physical256,false);
                packet=Checked(services.TookDamage(packet));
                NpcItemProcs.Received("damagedinmelee",attacker,target,packet,at,incoming);
                ClayGolemSlow.Incoming(attacker,target,false,at);
                NecromancerEffects.LifeTap(attacker,target,packet.Physical256);
            }
        }
        int before=SkillHealth.Balance256(target);
        packet=packet with{Physical256=Math.Min(packet.Physical256,before)};
        services.BeforeHealth(packet);
        if(packet.FreezeFrames>0&&!services.SupportsFreeze){throw new NotSupportedException("Prepared freeze needs its qualified status receiver before HP/events");}
        NpcFlatDrains.Require(attacker,packet,services.OrdinaryNpcFlatDrains);
        if(NpcFlatDrains.Any(packet)){NpcFlatDrains.BeforeHealth((BaseCreature)attacker,target,packet);}
        if(packet.AbsorbedLife256>0&&!MonsterUninterruptibles.Delayed(target)){SkillHealth.Heal(target,packet.AbsorbedLife256);}
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<0)
        {throw new NotSupportedException("Reentrant native deletion/death before prepared health commitment is unsupported");}
        int after=SkillHealth.Balance256(target);
        if(packet.Total256>0)
        {
            after-=packet.Total256;if(after<256){after=0;}
            services.BeforeHealthCommit(attacker,target,after);
            // Write source HP exactly once. Native Damage would dispatch death
            // before Stun and replay unrelated native damage callbacks.
            SkillHealth.SetBalance256(target,after);
            if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)!=after)
            {throw new NotSupportedException("Reentrant native health-setter mutation/death is unsupported");}
        }
        NpcFlatDrains.AfterHealth(target,packet);
        int stun=PreparedMonsterStun.Resolve(packet.StunFrames,recipient.Unique,limited,recipient.Boss,recipient.Velocity,recipient.Hireling);
        if(stun>0&&!CombatStuns.ApplyPreparedDuration(target,stun,at))
        {throw new NotSupportedException("Prepared monster Stun expired or recipient changed during release");}
        services.ApplyCold(packet.ColdFrames);
        services.ApplyFreeze(packet.FreezeFrames);
        services.ApplyPoison();
        packet=packet with{ResultFlags=SkillHealth.Balance256(target)<=0?packet.ResultFlags|2:packet.ResultFlags&~2};
        if((packet.ResultFlags&32)==0&&(packet.ResultFlags&2)!=0)
        {packet=Checked(services.Killed(packet));packet=Checked(services.Kill(packet));}
        else if((packet.ResultFlags&2)==0&&packet.Total256!=0){services.SurvivedDamage();}
        return new(packet,before,after,stun);
    }
    internal static PreparedMonsterFinishResult Finish(Mobile attacker,BaseCreature target,PreparedMonsterDamagePacket packet,
        PreparedMonsterDamageServices services,bool hitPresentation=true,bool missile=false)
    {
        Checked(packet);ArgumentNullException.ThrowIfNull(services);
        if(hitPresentation)
        {
            if(packet.Overlay>0){services.PresentOverlay(packet.Overlay);}
            if(!packet.HitClassActive&&(packet.HitClass&15)==0){packet=packet with{HitClass=packet.HitClass|services.BaseHitClass()};}
            services.AfterHitPresentation(packet);
        }
        if(!missile)
        {
            packet=Checked(services.MeleeAttack(packet));packet=Checked(services.AttackedInMelee(packet));
            PaladinThorns.PreparedFlatAttempt(attacker,target);
            if((packet.ResultFlags&1)!=0&&services.AttackerInActiveMode())
            {services.BeforeThorns(packet);PaladinThorns.PreparedPercentage(attacker,target,packet.Physical256);}
        }
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
