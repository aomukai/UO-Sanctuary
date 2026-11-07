// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

// This is a factory request, not a saved projectile. Flags retain the source
// distinction between absolute destinations (33) and ring offsets (7).
internal sealed record NpcItemMissileRequest(int Missile, int Skill, int Rank,
    int X, int Y, int TargetX, int TargetY, int Flags, int Velocity, int AttackRating,int ChargedOrdinal=-1);

// Explicit synchronous NPC item frame with no selected ordinary skill entry.
// The caller owns actual allocation/seed/path/damage and the returned handle.
// These services do not enable item callbacks or borrow a player's cast route.
internal sealed class NpcItemSpellOrigins : SkillExecutionServices
{
    private readonly (int X, int Y) _source;
    private readonly (int X, int Y)? _target;
    private readonly (int X, int Y) _path;
    private readonly int _attackRating;
    private readonly Func<NpcItemMissileRequest, object> _create;
    private readonly Action<object, int> _setBudget;
    private readonly Func<int,(int X,int Y)?> _chargedAim;
    private readonly Action<bool> _serverDoChanged;
    internal bool ServerDo { get; private set; }

    internal NpcItemSpellOrigins((int X, int Y) source, (int X, int Y)? target,
        (int X, int Y) path, int attackRating, bool serverDo,
        Func<NpcItemMissileRequest, object> create, Action<object, int> setBudget,Func<int,(int X,int Y)?> chargedAim=null,Action<bool> serverDoChanged=null)
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(setBudget);
        static bool Bounded((int X, int Y) p) => p.X is >= 0 and <= 65535 && p.Y is >= 0 and <= 65535;
        if (!Bounded(source) || !Bounded(path) || target is {} t && !Bounded(t) || attackRating < 0)
        { throw new NotSupportedException("NPC item origin frame exceeds its coordinate/stat domain"); }
        _source = source; _target = target; _path = path; _attackRating = attackRating;
        ServerDo = serverDo; _create = create; _setBudget = setBudget;_chargedAim=chargedAim;_serverDoChanged=serverDoChanged;
    }

    internal int Run(int skill, int rank, int outgoing)
    {
        if (rank is < 1 or > 63 || outgoing is < 0 or > 1)
        { throw new NotSupportedException("NPC item origin frame needs an explicit six-bit rank and caller flag"); }
        return SkillExecution.Run(this, skill, rank, false, true, outgoing != 0);
    }

    private static ItemExecutionMetadata Require(int skill)
    {
        if (skill is not (36 or 38 or 39 or 44 or 48 or 49 or 53))
        { throw new NotSupportedException("NPC item origin frame has no qualified do body for this spell"); }
        return NpcItemSpellDamage.Require(skill);
    }
    internal override bool HasGame => true;
    internal override bool HasSource => true;
    internal override int UnitType => 1;
    internal override SkillExecutionRecord Record(int skill) => Require(skill).LegacyRecord(false, 1);
    internal override ExecutionSkill UsedSkill() => null;
    internal override int DoCount => 191;
    internal override int MissileCount => ItemSkillExecution.MissileCount;
    internal override void SetServerDo(){ServerDo=true;_serverDoChanged?.Invoke(true);}
    internal override (int X, int Y)? TargetPoint => _target;
    internal override (int X, int Y) FirstPathPoint => _path;
    internal override (int X, int Y) SourcePoint => _source;
    internal override bool HasDo(int function) => function switch
    {
        0 => false,
        17 or 22 or 26 or 36 or 151 => true,
        _ => throw Missing()
    };

    // Complete sub_6FD11420 coordinate/AR helper for this item's NPC context.
    // Quantity is never checked: these pinned rows do not request it.
    private object Missile(int missile, int skill, int rank, int x, int y, int targetX, int targetY)
    {
        if (targetX == 0 || targetY == 0)
        {
            var aim = _target ?? _path;
            targetX = aim.X; targetY = aim.Y;
            if (targetX == 0 || targetY == 0) { return null; }
        }
        return _create(new(missile, skill, rank, checked(_source.X + x), checked(_source.Y + y),
            targetX, targetY, 33 | (_attackRating != 0 ? 0x1000 : 0), 0, _attackRating));
    }
    internal override void Launch(bool lob, int missile, int skill, int rank, int x, int y, int targetX, int targetY, int flags)
    {
        if (lob || flags != 0 || skill is not(36 or 39 or 49) || missile != Require(skill).Missile) { throw Missing(); }
        _ = Missile(missile, skill, rank, x, y, targetX, targetY);
    }
    internal override int Execute(int function, int skill, int rank)
    {
        var row = Require(skill);
        int missile = ItemSkillExecution.MissileId(row.Text("srvmissilea"));
        if(function==17&&skill==38)
        {
            SetServerDo();
            var values=new SkillValues(SkillMechanics.For(CombatAbility.ChargedBolt),_=>rank,_=>0,1);
            int count=checked((int)values.Eval("calc1"));
            for(int i=0;i<count;i++)
            {
                var aim=_chargedAim==null?_target??_path:_chargedAim(i);
                if(aim is not{} p||p.X==0||p.Y==0){continue;}
                _=_create(new(missile,skill,rank,_source.X,_source.Y,p.X,p.Y,33,0,0,i));
            }
            return 1;
        }
        if (function is 22 or 36)
        {
            if (skill is not (44 or 48)) { throw Missing(); }
            if (function == 36 && _target == null) { return 0; }
            if (function == 22) { SetServerDo(); }
            var center = function == 36 ? _target.Value : _source;
            var values = new SkillValues(SkillMechanics.For(NpcItemSpellDamage.Ability(skill)), _ => rank, _ => 0, 1);
            int velocity = checked((int)values.Eval("calc1") + NecromancerSummons.Source(row.Text("srvmissilea"), "Vel"));
            for (int i = 0; i < 64; i++)
            {
                var direction = FrozenOrbs.Direction(i);
                _ = _create(new(missile, skill, rank, center.X, center.Y, direction.X, direction.Y,
                    velocity != 0 ? 7 : 3, velocity, 0));
            }
            return 1;
        }
        if (function is 26 or 151 && skill == 53)
        {
            SetServerDo();
            var values = new SkillValues(SkillMechanics.For(CombatAbility.ChainLightning), _ => rank, _ => 0, 1);
            int count = checked((int)values.Eval("calc1"));
            var offset = function == 151 && _target is {} target ? (target.X - _source.X, target.Y - _source.Y) : (0, 0);
            var created = Missile(missile, skill, rank, offset.Item1, offset.Item2, 0, 0);
            if (created == null) { return 0; }
            _setBudget(created, count);
            return 1;
        }
        throw Missing();
    }

    private static NotSupportedException Missing() => new("NPC item origin frame needs an additional execution service");
    internal override bool HasRoom => throw Missing();
    internal override bool RoomInTown => throw Missing();
    internal override void ClearUsedSkill() => throw Missing();
    internal override void TownNeutral() => throw Missing();
    internal override ExecutionSkill HighestSkill(int skill) => throw Missing();
    internal override int Rank(ExecutionSkill skill, bool bonuses) => throw Missing();
    internal override bool ManaAllowed() => throw Missing();
    internal override bool HasStart(int function) => throw Missing();
    internal override void ConsumeMana(int skill, int level) => throw Missing();
    internal override void ConsumeQuantity() => throw Missing();
    internal override int Delay(int formula, int skill, int level) => throw Missing();
    internal override uint SequenceFrame => throw Missing();
    internal override void SetDelay(int frames) => throw Missing();
}
