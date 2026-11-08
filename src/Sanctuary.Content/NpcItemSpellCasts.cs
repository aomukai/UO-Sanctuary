// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// A synchronous item-skill frame, separate from the pet's saved AI controller.
// Shared do30 curses and qualified original-item spell bodies use the same
// temporary target/path/flag restoration. Other search/start services refuse.
internal static class NpcItemSpellCasts
{
    private sealed record Invocation(NpcItemTarget Target,(int X,int Y) Point,int Skill,int Level,int Mode,int Item,int Flag);
    private sealed class FrameState(SkeletonGuardian source)
    {
        internal NpcItemTarget Target;
        internal (int X,int Y) Point=(source.X,source.Y);
        internal bool ServerDo;
    }
    // Nested synchronous item casts share the current unit frame. This entry
    // exists only during execution and is restored even on an exception.
    private static readonly Dictionary<uint,FrameState> ActiveFrames=new();
    private static readonly Dictionary<uint, int> TargetScopes = new();
    internal static bool KeepsCombatTarget(BaseCreature caster) => TargetScopes.ContainsKey(caster.Serial.Value);
    public static void Initialize()
    {
        var allowed=ClassCombatHooks.TargetAllowed;
        // Native Mobile.Combatant validates CanBeHarmful while its tentative
        // assignment is marked ChangingCombatant. Veto that assignment before
        // retaliation/notifications,including original frozen Revive bodies.
        ClassCombatHooks.TargetAllowed=(actor,target)=>!(actor is BaseCreature{Alive:true,Deleted:false,ChangingCombatant:true} source&&KeepsCombatTarget(source))
            &&allowed?.Invoke(actor,target)!=false;
    }
    internal static IDisposable PreserveCombatTarget(BaseCreature caster)=>new TargetScope(caster);
    private sealed class TargetScope : IDisposable
    {
        private readonly uint _actor;
        private bool _disposed;
        internal TargetScope(BaseCreature caster)
        {
            _actor = caster.Serial.Value;
            TargetScopes[_actor] = TargetScopes.GetValueOrDefault(_actor) + 1;
        }
        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            if (TargetScopes[_actor] == 1) { TargetScopes.Remove(_actor); }
            else { TargetScopes[_actor]--; }
        }
    }
    private sealed class Services(SkeletonGuardian source,Func<int,ItemExecutionMetadata> require,Func<Invocation,int> execute,
        bool positions=false,Action<ItemEffectResult> notified=null,Func<int,int,int,int> start=null) : ItemEffectServices<NpcItemTarget>
    {
        private readonly FrameState State=ActiveFrames.GetValueOrDefault(source.Serial.Value)??new(source);
        private readonly NpcItemTarget Caster=NpcItemTarget.Capture(source);
        internal NpcItemTarget TargetForHydra=>State.Target;
        internal (int X,int Y) PointForHydra=>State.Point;
        internal override NpcItemTarget Source => Caster;
        internal override bool HasGame => source.Map != null && source.Map != Map.Internal;
        internal override bool Uninterruptible => MonsterUninterruptibles.Active(source);
        internal override NpcItemTarget CurrentTarget => State.Target;
        internal override (int X, int Y) FirstPathPoint => State.Point;
        internal override bool ServerDo { get=>State.ServerDo;set=>State.ServerDo=value; }
        internal override ItemEffectSkill Skill(int id)
        {
            var row = require(id);
            return new(row.ItemEffect, row.ItemTarget, row.ItemCheckStart);
        }
        internal override (int Type, int Id) Identity(NpcItemTarget unit) => (1, unit.Id);
        internal override void SetTarget(NpcItemTarget target) => State.Target = target;
        internal override void SetPosition(int x, int y) { State.Target = null; State.Point = (x, y); }
        internal override bool FindPosition(int skill, int level) => throw new NotSupportedException("NPC item random-position service is not admitted");
        internal override NpcItemTarget FindCorpse(NpcItemTarget target, int skill, int level) => throw new NotSupportedException("NPC item corpse-search service is not admitted");
        internal override NpcItemTarget LastAttacker() => throw new NotSupportedException("NPC item last-attacker service is not admitted");
        internal override int Start(int skill, int level, int item) => start?.Invoke(skill,level,item)
            ??throw new NotSupportedException("NPC item start-check service is not admitted");
        internal override int Execute(int skill, int level, int mode, int item, int flag)
        {
            uint id=source.Serial.Value;bool had=ActiveFrames.TryGetValue(id,out var previous);ActiveFrames[id]=State;
            try{return execute(new(State.Target,State.Point,skill,level,mode,item,flag));}
            finally{if(had){ActiveFrames[id]=previous;}else{ActiveFrames.Remove(id);}}
        }
        internal override void NotifyTarget(int skill, int level, int type, int id, int flag)
        {source.PlaySound(0x1E5);notified?.Invoke(new(type,id,0,0));} // Identified shared item-cast audio placeholder.
        internal override void NotifyPosition(int skill, int level, int x, int y, int flag)
        {
            if(!positions){throw new NotSupportedException("NPC ground item curse is not admitted");}
            source.PlaySound(0x1E5);notified?.Invoke(new(6,-1,x,y));
        }
    }
    internal static bool LifeTap(SkeletonGuardian caster, Server.Mobiles.BaseCreature target, int rank, DateTime at)
        => CastCurse(caster, target, CombatAbility.LifeTap, rank, at);
    internal static bool CastCurse(SkeletonGuardian caster, Server.Mobiles.BaseCreature target, CombatAbility ability, int rank, DateTime at,int flag=1)
        => caster == null || target is not { Alive:true,Deleted:false } ? false
            : CastCurse(caster,NpcItemTarget.Capture(target),ability,rank,at,flag);
    internal static bool CastCurse(SkeletonGuardian caster,NpcItemTarget target,CombatAbility ability,int rank,DateTime at,int flag=1)
    {
        if (caster == null || target == null) { return false; }
        // Native retaliation can synchronously feed back through BaseAI and
        // assign the caster's ordinary Combatant even for indirect aggression.
        // The item skill uses its explicit temporary frame instead.
        using var scope = new TargetScope(caster);
        var frame=new Services(caster,ItemSkillExecution.RequireCurse,i=>
        {
            if(i.Mode!=0||i.Item!=1||i.Target==null){throw new NotSupportedException("NPC curse needs its explicit target/item-execution context");}
            return NpcItemSkillExecution.Run(caster,i.Target,i.Skill,i.Level,i.Mode,i.Item,i.Flag,at);
        });
        return ItemEffectCasts.Target(frame,SkillMechanics.For(ability).SourceId,rank,target,flag)!=0;
    }
    // Explicit prepared factory seam. This does not extend the automatic proc
    // whitelist until actual NPC-owned head placement/lifetime is qualified.
    internal static ItemEffectServices<NpcItemTarget> PreparedHydraFrame(SkeletonGuardian caster,DateTime at,
        Func<NpcHydraRequest,bool> summon,Action<ItemEffectResult> notified=null)
    {
        ArgumentNullException.ThrowIfNull(summon);Services frame=null;
        frame=new(caster,id=>id==62?NpcHydraCreation.Require():throw new NotSupportedException("Prepared Hydra frame skill"),
            i=>new NpcHydraExecution(caster,i.Target,i.Point,at,summon).Run(i.Level,i.Flag),true,notified,
            (id,rank,item)=>id==62&&item==1&&NpcHydraCreation.Start(caster,frame.TargetForHydra,frame.PointForHydra,rank,out _)?1:0);
        return frame;
    }
    internal static ItemEffectServices<NpcItemTarget> OriginalFrame(SkeletonGuardian caster,DateTime at,
        Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,Action<ItemEffectResult> notified=null)
    {
        if(!NpcItemSpellFlights.Attached(caster,caster?.ControlMaster as PlayerMobile,caster?.Map?.MapID??-1))
        {throw new NotSupportedException("Original item frame needs its qualified attached NPC species");}
        ArgumentNullException.ThrowIfNull(allocate);Services frame=null;
        frame=new(caster,NpcItemSpellDamage.Require,i=>NpcItemSkillExecution.RunOriginal(caster,i.Target,new(i.Point.X,i.Point.Y,caster.Z),
            i.Skill,i.Level,i.Mode,i.Item,i.Flag,at,allocate,frame.ServerDo,value=>frame.ServerDo=value),true,notified);
        return frame;
    }
    internal static bool CastOriginal(SkeletonGuardian caster,NpcItemTarget target,int skill,int rank,int flag,DateTime at,
        Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate)
    {
        if(caster is not{Alive:true,Deleted:false}||caster.Map==null||caster.Map==Map.Internal||target==null){return false;}
        using var scope=new TargetScope(caster);
        return ItemEffectCasts.Target(OriginalFrame(caster,at,allocate),skill,rank,target,flag)!=0;
    }
    internal static bool CastOriginalPosition(SkeletonGuardian caster,Point3D point,int skill,int rank,int flag,DateTime at,
        Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate)
    {
        if(caster is not{Alive:true,Deleted:false}||caster.Map==null||caster.Map==Map.Internal){return false;}
        using var scope=new TargetScope(caster);
        return ItemEffectCasts.Position(OriginalFrame(caster,at,allocate),skill,rank,point.X,point.Y,flag)!=0;
    }
}
