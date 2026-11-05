// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

// A synchronous item-skill frame, separate from the pet's saved AI controller.
// Only the shared do30 curse bodies are admitted. Random/corpse/start
// services must be supplied before admitting skills that require them.
internal static class NpcItemSpellCasts
{
    private static readonly Dictionary<uint, int> TargetScopes = new();
    internal static bool KeepsCombatTarget(SkeletonGuardian caster) => TargetScopes.ContainsKey(caster.Serial.Value);
    private sealed class TargetScope : IDisposable
    {
        private readonly uint _actor;
        private bool _disposed;
        internal TargetScope(SkeletonGuardian caster)
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
    private sealed class CurseServices(SkeletonGuardian source, DateTime at) : ItemEffectServices<NpcItemTarget>
    {
        private NpcItemTarget _target;
        private readonly NpcItemTarget Caster=NpcItemTarget.Capture(source);
        private (int X, int Y) _point = (source.X, source.Y);
        internal override NpcItemTarget Source => Caster;
        internal override bool HasGame => source.Map != null && source.Map != Map.Internal;
        internal override bool Uninterruptible => MonsterUninterruptibles.Active(source);
        internal override NpcItemTarget CurrentTarget => _target;
        internal override (int X, int Y) FirstPathPoint => _point;
        internal override bool ServerDo { get; set; }
        internal override ItemEffectSkill Skill(int id)
        {
            var row = ItemSkillExecution.RequireCurse(id);
            return new(row.ItemEffect, row.ItemTarget, row.ItemCheckStart);
        }
        internal override (int Type, int Id) Identity(NpcItemTarget unit) => (1, unit.Id);
        internal override void SetTarget(NpcItemTarget target) => _target = target;
        internal override void SetPosition(int x, int y) { _target = null; _point = (x, y); }
        internal override bool FindPosition(int skill, int level) => throw new NotSupportedException("NPC item random-position service is not admitted");
        internal override NpcItemTarget FindCorpse(NpcItemTarget target, int skill, int level) => throw new NotSupportedException("NPC item corpse-search service is not admitted");
        internal override NpcItemTarget LastAttacker() => throw new NotSupportedException("NPC item last-attacker service is not admitted");
        internal override int Start(int skill, int level, int item) => throw new NotSupportedException("NPC item start-check service is not admitted");
        internal override int Execute(int skill, int level, int mode, int item, int flag)
        {
            if (mode != 0 || item != 1 || _target == null)
            { throw new NotSupportedException("NPC curse needs its explicit target/item-execution context"); }
            return NpcItemSkillExecution.Run(source, _target, skill, level, mode, item, flag, at);
        }
        internal override void NotifyTarget(int skill, int level, int type, int id, int flag)
            => source.PlaySound(0x1E5); // Identified shared curse/item-cast audio placeholder.
        internal override void NotifyPosition(int skill, int level, int x, int y, int flag)
            => throw new NotSupportedException("NPC ground item curse is not admitted");
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
        return ItemEffectCasts.Target(new CurseServices(caster, at), SkillMechanics.For(ability).SourceId, rank, target, flag) != 0;
    }
}
