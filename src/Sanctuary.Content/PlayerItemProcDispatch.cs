// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record PlayerItemProcOutcome(int CallbackResult,int CastAttempts,int CastResults);

// Callback -> protected item frame -> prepared missile. This adapter consumes
// declared source stat/RNG services;it does not subscribe an equipped item or
// invent a player RNG,ordinary packet phase or uninterruptible state.
internal static class PlayerItemProcDispatch
{
    private sealed class Services(PlayerMobile source,NpcItemTarget target,Point3D path,PreparedMonsterDamagePacket packet,
        Func<int,int,int> chance,Func<uint> roll,bool uninterruptible,DateTime at,Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,
        Action<ItemEffectResult> notified,int procBonus):ItemProcServices
    {
        internal int Attempts,Results;
        internal override bool HasSource=>source is{Deleted:false};
        internal override bool HasTarget=>target!=null;
        internal override bool HasPacket=>packet!=null;
        internal override uint HitFlags=>packet?.HitFlags??0;
        internal override ushort ResultFlags=>unchecked((ushort)(packet?.ResultFlags??0));
        internal override int LevelBits=>6;
        internal override int Chance(int stat,int layer)=>chance(stat,layer);
        internal override uint Roll()=>roll();
        internal override bool ChancePassed(int chance) =>
            procBonus == 0 ? base.ChancePassed(chance) : Roll() % 10000 < chance * (100L + procBonus);
        internal override ItemProcSkill Skill(int id)=>id==47?new(NpcItemSpellDamage.Require(id).ItemTargetDo)
            :throw new NotSupportedException("Player proc spell body is not admitted: "+id);
        internal override (int X,int Y) FirstPathPoint()=>(path.X,path.Y);
        internal override void CastTarget(bool targetAsCaster,int skill,int level,int flag)
        {
            if(targetAsCaster||skill!=47){throw new NotSupportedException("Player proc needs its own caster/skill adapter");}
            Attempts++;Results+=PlayerItemSpellCasts.Target(source,target,level,flag,uninterruptible,at,allocate,notified,procBonus);
        }
        internal override void CastPosition(int skill,int level,int x,int y,int flag)
        {
            if(skill!=47){throw new NotSupportedException("Player ground proc needs its own spell adapter");}
            Attempts++;Results+=PlayerItemSpellCasts.Position(source,new(x,y,path.Z),level,flag,uninterruptible,at,allocate,notified,procBonus);
        }
    }
    internal static PlayerItemProcOutcome Run(int function,uint packed,PlayerMobile source,NpcItemTarget target,Point3D path,
        PreparedMonsterDamagePacket packet,Func<int,int,int> chance,Func<uint> roll,bool uninterruptible,DateTime at,
        Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,Action<ItemEffectResult> notified=null,int procBonus=0)
    {
        ArgumentNullException.ThrowIfNull(chance);ArgumentNullException.ThrowIfNull(roll);ArgumentNullException.ThrowIfNull(allocate);
        if(function is not(20 or 21)||ItemEventDefinitions.Bindings((int)(packed>>16))?.Any(e=>e.Function==function)!=true)
        {throw new NotSupportedException("Player proc callback/stat binding is not admitted");}
        var services=new Services(source,target,path,packet,chance,roll,uninterruptible,at,allocate,notified,procBonus);
        int result=ItemProcCallbacks.Run(function,packed,services);return new(result,services.Attempts,services.Results);
    }
}
