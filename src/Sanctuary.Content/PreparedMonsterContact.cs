// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Player/NPC-to-NPC. Real range/collision,hostility,room/row and mode metadata
// remain mandatory services; no tile radius or native death mode is guessed.
internal abstract class MonsterContactServices
{
    internal abstract PreparedMonsterDamagePacket CapturedPacket();
    internal abstract bool InMeleeRange(int bonus);
    internal abstract bool AttackerInDeathMode();
    internal abstract bool Hostile();
    internal abstract bool DefenderHasRoom();
    internal abstract bool DefenderInTown();
    internal abstract bool AttackerAllowedInTown();
    internal abstract bool DefenderKillable();
    internal abstract bool DefenderDead();
}
internal abstract class GeometricMonsterContactServices:MonsterContactServices
{
    internal abstract MeleeGeometryUnit AttackerGeometry();
    internal abstract MeleeGeometryUnit DefenderGeometry();
    internal abstract bool CollisionRay(Point2D first,Point2D second,int mask);
    internal override bool InMeleeRange(int bonus)=>SourceMeleeGeometry.Within(AttackerGeometry(),DefenderGeometry(),bonus,CollisionRay);
}
internal enum MonsterContactExit{MissingPacket,OutOfRangeFreed,DeathModeRetained,Finished}
internal sealed record MonsterContactResult(MonsterContactExit Exit,PreparedMonsterDamagePacket Packet,
    PreparedMonsterCommitResult Commit,bool DeathModeAttempted=false);
internal static class PreparedMonsterContact
{
    internal static MonsterContactResult Release(Mobile attacker,BaseCreature defender,MonsterContactServices contact,
        PreparedMonsterRecipient recipient,Func<int,int> limited,PreparedMonsterDamageServices damage,DateTime at)
    {
        ArgumentNullException.ThrowIfNull(contact);ArgumentNullException.ThrowIfNull(damage);
        var captured=contact.CapturedPacket();
        if(captured==null){return new(MonsterContactExit.MissingPacket,null,null);}
        // DrainItemDurability copies the stored packet before range/events.
        // Release-time fields never rewrite the packet captured by start.
        var packet=captured with{};
        if(!contact.InMeleeRange(attacker is PlayerMobile?1:3))
        {damage.FreePacket();return new(MonsterContactExit.OutOfRangeFreed,packet,null);}
        PreparedMonsterCommitResult commit=null;
        bool hit=(packet.ResultFlags&1)!=0;
        if(hit)
        {
            if(contact.AttackerInDeathMode()){return new(MonsterContactExit.DeathModeRetained,packet,null);}
            packet=packet with{HitFlags=32};
            if(!contact.Hostile()&&(packet.HitFlags&4096)==0){packet=packet with{ResultFlags=packet.ResultFlags&~3};}
            else if(!contact.DefenderHasRoom()){packet=packet with{ResultFlags=packet.ResultFlags&~3};}
            else if(contact.DefenderInTown()&&!contact.AttackerAllowedInTown()){packet=packet with{ResultFlags=packet.ResultFlags&~2};}
            else if(contact.DefenderKillable()&&!contact.DefenderDead())
            {
                commit=PreparedMonsterDamage.CommitQualified(attacker,defender,packet,recipient,limited,damage,at);
                packet=commit.Packet;
            }
        }
        // Even a miss or rejected health event still reaches attack-attempt
        // events/return/free. Reuse the qualified path's unchanged final phase.
        var finish=PreparedMonsterDamage.Finish(attacker,defender,packet,damage,hit);
        return new(MonsterContactExit.Finished,finish.Packet,commit,finish.DeathModeAttempted);
    }
}
