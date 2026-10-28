// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// The shared source helper has process-local history, not attacker-local RNG.
// A caller shares this instance across its return consumers. It is not saved;
// actual modern cross-consumer scope remains a separate acceptance question.
internal sealed class MonsterHitClassCycle
{
    internal int Count{get;private set;}
    internal int Resolve(PreparedMonsterDamagePacket packet)
    {
        if(Count==int.MaxValue){throw new NotSupportedException("Source hit-class counter overflow is unverified");}
        int start=Count++%4,layer=0;
        for(int n=0;n<4;n++)
        {
            int slot=(start+n)%4;
            int amount=slot switch{0=>packet.Cold256,1=>packet.Fire256,2=>packet.Lightning256,_=>packet.Poison256};
            if(amount>0){layer=slot switch{0=>0x30,1=>0x20,2=>0x40,_=>0x50};break;}
        }
        if(layer==0&&(packet.ResultFlags&8192)!=0){layer=0x10;}
        return (packet.HitClass==0?13:packet.HitClass)|layer;
    }
}

// All mechanical services are mandatory: source rooms, states, rows, RNG and
// mode callbacks must be supplied by a real actor consumer. Observers only
// expose decisions to diagnostics; they cannot replace the source algorithms.
internal abstract class MonsterReturnServices
{
    internal abstract int? AttackerKind{get;}
    internal abstract int TargetMode{get;}
    internal abstract int TargetClass{get;}
    internal abstract bool HasRoom();
    internal abstract bool RoomInTown();
    internal abstract bool? AttackerTownRule();
    internal abstract bool Uninterruptible();
    internal abstract bool Frozen();
    internal abstract bool Stunned();
    internal abstract int BaseId();
    internal abstract int? ModeFlags();
    internal abstract int CurrentLife256{get;}
    internal abstract int MaximumLife256{get;}
    internal abstract int LastSentLifePercent{get;}
    internal abstract uint RollDefender();
    internal abstract void LastHitClass(int value);
    internal abstract void DeathDelay();
    internal abstract void Kill();
    internal abstract void Mode(int mode,bool targetAttacker);
    internal abstract void Refresh();
    internal abstract void MarkLifeAndHitClass();
    internal abstract void AiState(int state);
    internal abstract void RecoveryNotification();
    internal virtual void SelectingHitClass(int count,int basis){}
    internal virtual void CheckingRecovery(int hitClass,int damage256){}
}

internal static class PreparedMonsterReturn
{
    internal const int GetHit=3,Block=6,Knockback=13;
    internal static MonsterHitClassCycle SharedHitClasses{get;}=new();
    internal static void Dispatch(ref PreparedMonsterDamagePacket packet,MonsterReturnServices services,bool ubers)
        =>Dispatch(ref packet,SharedHitClasses,services,ubers);
    // This is the NPC branch. It intentionally retains missing-room behavior,
    // cached result precedence and source service order rather than adding
    // generic eligibility checks belonging to the earlier contact phase.
    internal static void Dispatch(ref PreparedMonsterDamagePacket packet,MonsterHitClassCycle cycle,MonsterReturnServices services,bool ubers)
    {
        ArgumentNullException.ThrowIfNull(packet);ArgumentNullException.ThrowIfNull(cycle);ArgumentNullException.ThrowIfNull(services);
        if((packet.ResultFlags&~0xffff)!=0||packet.Total256<0){throw new NotSupportedException("Invalid NPC return packet");}
        if(services.HasRoom()&&services.RoomInTown())
        {
            if(services.AttackerKind!=1){return;}
            if(services.AttackerTownRule()!=true){return;}
        }
        if((packet.ResultFlags&1)!=0)
        {
            if(!packet.HitClassActive&&(packet.HitClass&0xf0)==0)
            {
                services.SelectingHitClass(cycle.Count,packet.HitClass);
                packet=packet with{HitClass=cycle.Resolve(packet)};
            }
            services.LastHitClass(packet.HitClass);
        }
        if(services.Uninterruptible())
        {if((packet.ResultFlags&2)!=0){services.DeathDelay();}return;}
        if(services.AttackerKind==null){return;}
        if(services.TargetMode is 0 or 12){return;}
        if((packet.ResultFlags&8)!=0)
        {
            var flags=services.ModeFlags();
            if(flags==null||(flags.Value&(1<<Knockback))==0)
            {packet=packet with{ResultFlags=(packet.ResultFlags&~8)|4};}
        }
        // Sand-leaper adjustment is after that fallback, as in the source.
        if((packet.ResultFlags&4)!=0&&services.BaseId()==78&&!services.Frozen())
        {packet=packet with{ResultFlags=packet.ResultFlags|8};}
        if((packet.ResultFlags&2)!=0){services.Kill();return;}
        if((packet.ResultFlags&8)!=0){services.Mode(Knockback,true);return;}
        if((packet.ResultFlags&16)!=0)
        {
            if((packet.ResultFlags&16384)==0&&services.TargetClass!=243&&services.TargetClass!=333&&(!ubers||services.TargetClass!=705))
            {
                var flags=services.ModeFlags();
                if(flags!=null&&(flags.Value&(1<<Block))!=0){services.Mode(Block,false);return;}
            }
            services.AiState(19);return;
        }
        if((packet.ResultFlags&4)!=0)
        {
            if(services.Stunned()||!RecoverySuppressed(packet,services))
            {services.Mode(GetHit,false);services.RecoveryNotification();return;}
            Refresh(services,true);return;
        }
        if((packet.ResultFlags&16384)!=0){Refresh(services,true);return;}
        if((packet.ResultFlags&1)!=0&&packet.Total256>0)
        {
            int sent=services.LastSentLifePercent;
            if(Math.Abs(sent-LifePercent(services))>4){Refresh(services,false);}
        }
    }
    private static void Refresh(MonsterReturnServices services,bool recover)
    {
        services.Refresh();services.MarkLifeAndHitClass();
        if(recover){services.AiState(19);services.RecoveryNotification();}
    }
    internal static int LifePercent(MonsterReturnServices services)
    {
        int hp=services.CurrentLife256>>8,max=services.MaximumLife256>>8;
        return max>0&&hp<max?unchecked((byte)((hp<<7)/max)):128;
    }
    internal static bool RecoverySuppressed(PreparedMonsterDamagePacket packet,MonsterReturnServices services)
    {
        services.CheckingRecovery(packet.HitClass,packet.Total256);
        if(services.Frozen()){return true;}
        if(packet.Poison256!=0&&packet.Poison256==packet.Total256){return true;}
        if(packet.Total256<256){return true;}
        int maximum=services.MaximumLife256;
        if(maximum<0){throw new NotSupportedException("Negative source maximum life");}
        int divisor=packet.HitClass switch{2 or 6 or 10 or 11=>8,5=>64,4 or 8=>32,_=>16};
        if(packet.Total256<maximum/divisor){return true;}
        if(packet.Total256<maximum/(divisor/2)&&(services.RollDefender()&1)==0){return true;}
        if(packet.Total256<maximum/(divisor/4)&&(services.RollDefender()&3)==0){return true;}
        var flags=services.ModeFlags();
        return flags==null||(flags.Value&(1<<GetHit))==0;
    }
}
