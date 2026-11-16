// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record PlayerItemCastContext(int Target,int X,int Y,bool ServerDo);

// Protected synchronous item frames. Actual equipment subscriptions,source
// chance/RNG and uninterruptibility remain mandatory outer host services.
internal static class PlayerItemSpellCasts
{
    private sealed record Actor(PlayerMobile Player,NpcItemTarget Monster)
    {
        internal (int Type,int Id) Identity=>Player!=null?(0,checked((int)Player.Serial.Value)):(1,Monster.Id);
        internal Point3D Point(Map map)=>Player!=null?Player.Location:Monster.Point(map);
    }
    private sealed class State(PlayerMobile owner)
    {internal Actor Target;internal (int X,int Y) Point=(owner.X,owner.Y);internal bool ServerDo;}
    private static readonly Dictionary<uint,State> Frames=new();
    internal static PlayerItemCastContext Context(PlayerMobile owner)=>owner!=null&&Frames.TryGetValue(owner.Serial.Value,out var s)
        ?new(s.Target?.Identity.Id??0,s.Point.X,s.Point.Y,s.ServerDo):null;
    private sealed class Services(PlayerMobile owner,bool uninterruptible,DateTime at,Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,
        Action<ItemEffectResult> notify):ItemEffectServices<Actor>
    {
        private readonly State Frame=Frames.GetValueOrDefault(owner.Serial.Value)??new(owner);
        internal override Actor Source=>new(owner,null);
        internal override bool HasGame=>owner is{Alive:true,Deleted:false}&&owner.Map!=null&&owner.Map!=Map.Internal;
        internal override bool Uninterruptible=>uninterruptible;
        internal override Actor CurrentTarget=>Frame.Target;
        internal override (int X,int Y) FirstPathPoint=>Frame.Point;
        internal override bool ServerDo{get=>Frame.ServerDo;set=>Frame.ServerDo=value;}
        internal override ItemEffectSkill Skill(int id)
        {if(id!=47){throw Missing();}var row=NpcItemSpellDamage.Require(id);return new(row.ItemEffect,row.ItemTarget,row.ItemCheckStart);}
        internal override (int Type,int Id) Identity(Actor unit)=>unit.Identity;
        internal override void SetTarget(Actor target)=>Frame.Target=target;
        internal override void SetPosition(int x,int y){Frame.Target=null;Frame.Point=(x,y);}
        internal override bool FindPosition(int skill,int level)=>throw Missing();
        internal override Actor FindCorpse(Actor target,int skill,int level)=>throw Missing();
        internal override Actor LastAttacker()=>throw Missing();
        internal override int Start(int skill,int level,int item)=>throw Missing();
        internal override int Execute(int skill,int level,int mode,int item,int flag)
        {
            if(skill!=47||mode!=0||item!=1||flag is <0 or >1){throw Missing();}
            uint id=owner.Serial.Value;bool had=Frames.TryGetValue(id,out var prior);Frames[id]=Frame;
            try{return SorceressSpellMissiles.EmitPlayerItemFireBall(owner,Frame.Target?.Point(owner.Map),new(Frame.Point.X,Frame.Point.Y,owner.Z),level,flag,at,allocate,Frame.ServerDo,value=>Frame.ServerDo=value).Result;}
            finally{if(had){Frames[id]=prior;}else{Frames.Remove(id);}}
        }
        internal override void NotifyTarget(int skill,int level,int type,int id,int flag)
        {owner.PlaySound(0x1E5);notify?.Invoke(new(type,id,0,0));} // Shared item-cast audio placeholder.
        internal override void NotifyPosition(int skill,int level,int x,int y,int flag)
        {owner.PlaySound(0x1E5);notify?.Invoke(new(6,-1,x,y));}
        private static NotSupportedException Missing()=>new("Player item frame has no admitted service for this skill/context");
    }
    internal static int Target(PlayerMobile owner,NpcItemTarget target,int rank,int flag,bool uninterruptible,DateTime at,
        Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,Action<ItemEffectResult> notify=null)
    {
        ArgumentNullException.ThrowIfNull(allocate);if(owner==null||target==null){return 0;}
        using var scope=NpcItemSpellCasts.PreserveCombatTarget(owner);
        return ItemEffectCasts.Target(new Services(owner,uninterruptible,at,allocate,notify),47,rank,new(null,target),flag);
    }
    internal static int Position(PlayerMobile owner,Point3D point,int rank,int flag,bool uninterruptible,DateTime at,
        Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,Action<ItemEffectResult> notify=null)
    {
        ArgumentNullException.ThrowIfNull(allocate);if(owner==null){return 0;}
        using var scope=NpcItemSpellCasts.PreserveCombatTarget(owner);
        return ItemEffectCasts.Position(new Services(owner,uninterruptible,at,allocate,notify),47,rank,point.X,point.Y,flag);
    }
}
