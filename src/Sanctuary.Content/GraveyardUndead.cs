// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record MonsterProfile(int Level, int Life, int AttackRating, int Defense, int MinDamage,
    int MaxDamage, int AttackMs, int MoveMs, int Physical, int Fire, int Cold, int Poison, int Lightning);

public static class GraveyardProfiles
{
    public static MonsterProfile Skeleton { get; private set; }
    public static MonsterProfile Zombie { get; private set; }
    public static readonly MonsterProfile Lich = new(5, 90, 50, 20, 3, 5, 1200, 360, 10, 20, 40, 80, 10);
    public static void Configure()
    {
        Skeleton = Profile(MonsterBalance.For(MonsterArchetype.Melee, 2));
        Zombie = Profile(MonsterBalance.For(MonsterArchetype.Tank, 1));
        ClassCombatHooks.CreatureSwing = (attacker, target) =>
            attacker is GraveyardUndead undead ? undead.BeginMelee(target)
                : attacker is SkeletonGuardian pet ? pet.BeginMelee(target) : null;
    }
    private static MonsterProfile Profile(MonsterLevel row) => new(row.Level, row.Life.Min, row.AttackRating.Min,
        row.Defense.Min, row.MinDamage, row.MaxDamage, row.AttackMs + row.RecoveryMs, row.MoveMs, 0, 0, 0, 50, 0);
}

[SerializationGenerator(0)]
public abstract partial class GraveyardUndead : BaseCreature
{
    [SerializableField(0, setter: "private")]
    private bool _raised;
    private TimerExecutionToken _actionTimer;
    private Mobile _actionTarget;
    private Corpse _raiseTarget;
    private Point3D _actionOrigin;
    private Map _actionMap;
    private long _nextAction = Core.TickCount;
    private long _nextRaise = Core.TickCount;
    private long _lastRecovery, _recoveryFraction;
    private MonsterMelee _melee;
    internal long NextMeleeDecision => _nextAction;
    public MonsterLevel BalanceLevel => this switch
    {
        GraveyardSkeleton => MonsterBalance.For(MonsterArchetype.Melee, 2),
        GraveyardZombie => MonsterBalance.For(MonsterArchetype.Tank, 1),
        _ => null
    };
    public virtual NormalMonsterStats NormalStats => MonsterVariants.Melee(this, this switch
    {
        GraveyardSkeleton => EarlyNormalMonsters.Skeleton,
        GraveyardZombie => EarlyNormalMonsters.Zombie,
        _ => null
    });
    public abstract MonsterProfile CombatProfile { get; }
    protected virtual int RaiseKind => 0;
    protected virtual bool Caster => false;
    protected override BaseAI ForcedAI => new LeashedMeleeAI(this);
    public bool ReturningHome => AIObject is LeashedMeleeAI { ReturningHome: true };
    protected GraveyardUndead() : base(AIType.AI_Melee, FightMode.Closest, 14)
    {
        var profile = CombatProfile;
        SetStr(20);
        SetDex(20);
        SetInt(20);
        SetHits(Utility.RandomMinMax(profile.Life, BalanceLevel?.Life.Max ?? NormalStats?.MaxLife ?? profile.Life));
        SetStam(100);
        SetMana(0);
        SetDamage(profile.MinDamage, profile.MaxDamage);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, profile.Physical);
        SetResistance(ResistanceType.Fire, profile.Fire);
        SetResistance(ResistanceType.Cold, profile.Cold);
        SetResistance(ResistanceType.Poison, profile.Poison);
        SetResistance(ResistanceType.Energy, profile.Lightning);
        Fame = profile.Level * 100;
        Karma = -Fame;
        RangeFight = Caster ? 10 : 1;
        _nextRaise = Core.TickCount + 3000;
    }
    // Movement notices use the species decision immediately; broad idle scans stay paced.
    public override TimeSpan AcquireOnApproachDelay => CreatureProfiles.For(this).Reaction.NoticeMs is { } notice
        ? TimeSpan.FromMilliseconds(notice) : base.AcquireOnApproachDelay;
    public override TimeSpan ReacquireDelay => CreatureProfiles.For(this).Reaction.ScanMs is { } scan
        ? TimeSpan.FromMilliseconds(scan) : base.ReacquireDelay;
    public override bool CanRegenHits => false;
    public override bool CanRegenMana => false;
    public override bool BleedImmune => true;
    public override Poison PoisonImmune => Poison.Lethal;
    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;
    public override bool IsEnemy(Mobile mobile) => !ReturningHome && (PaladinConversion.Enemy(this,mobile) ?? ((mobile is PlayerMobile
        || mobile is BaseCreature { Controlled: true } or BaseCreature { Summoned: true }) && base.IsEnemy(mobile)));
    public override void GetSpeeds(out double activeSpeed, out double passiveSpeed)
    {
        activeSpeed = 0.1;
        passiveSpeed = 0.4;
    }
    public override void GetMoveSpeeds(out double activeMoveSpeed, out double passiveMoveSpeed)
    {
        activeMoveSpeed = CombatProfile.MoveMs / 1000.0;
        passiveMoveSpeed = activeMoveSpeed;
    }
    public override void GenerateLoot(bool spawning)
    {
        // Death loot is disabled until project drop tables are designed.
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Level "}{CreatureProfiles.Level(this)}");
        if (Raised)
        {
            list.Add($"{"Revived undead"}");
        }
    }
    private bool ValidEnemy(Mobile target, int range) => !Deleted && Alive && !Frozen && !Paralyzed && !FreezeStates.Waiting(this) && CreatureHitRecovery.Remaining(this) == 0
        && target is { Deleted: false, Alive: true, Blessed: false } && target.Map == Map
        && IsEnemy(target) && InRange(target, range) && Math.Abs(target.Z - Z) < 16
        && CanSee(target) && InLOS(target) && CanBeHarmful(target, false);
    public TimeSpan BeginMelee(Mobile target)
    {
        return BeginMelee(target, Core.TickCount);
    }
    internal TimeSpan BeginMelee(Mobile target, long now)
    {
        if (Caster && !NamedControl.Blind(this) && !SourceTerror.InCombat(this) || !ValidEnemy(target, 1))
        {
            return TimeSpan.FromMilliseconds(40);
        }
        if (_actionTimer.Running || now - _nextAction < 0)
        {
            return TimeSpan.FromMilliseconds(Math.Max(40, _nextAction - now));
        }
        if (AIObject is LeashedMeleeAI ai && ai.MeleeWait(target, now) is > 0 and var wait)
        {
            return TimeSpan.FromMilliseconds(Math.Max(40, wait));
        }
        var normal = NormalStats;
        bool sourceAttack=SourceTerror.InCombat(this);
        if (!sourceAttack && normal.AttackChance < 100 && Utility.RandomDouble() * 100 >= normal.AttackChance)
        {
            _nextAction = now + normal.StallMs;
            AIObject?.ResetApproachState();
            return TimeSpan.FromMilliseconds(normal.StallMs);
        }
        _melee = sourceAttack || Utility.RandomDouble() * 100 < normal.Attack1Chance ? normal.Attack1 : normal.Attack2;
        _melee = _melee with { Speed = Math.Max(1, _melee.Speed * Math.Clamp(100+PaladinOffense.AttackRate(this)-GearEffects.AttackSlow(this),15,175) / 100) };
        StartAction(target);
        _nextAction = now + _melee.DurationMs + normal.AiDelayMs;
        AIObject?.ResetApproachState();
        Direction = GetDirectionTo(target);
        ClassCombat.SendAnimation(this, 4, _melee.DurationMs);
        Animate(4, 5, 1, true, false, 0);
        Timer.StartTimer(TimeSpan.FromMilliseconds(_melee.EffectMs), ResolveMelee, out _actionTimer);
        return TimeSpan.FromMilliseconds(_melee.DurationMs + normal.AiDelayMs);
    }
    protected void StartAction(Mobile target)
    {
        _actionTarget = target;
        _actionOrigin = Location;
        _actionMap = Map;
        _nextAction = Core.TickCount + CombatProfile.AttackMs;
        DoHarmful(target);
    }
    protected bool ActionReady => !_actionTimer.Running && Core.TickCount - _nextAction >= 0;
    private bool SameOrigin => Map == _actionMap && Location == _actionOrigin;
    private void ResolveMelee()
    {
        var target = _actionTarget;
        _actionTarget = null;
        if (SameOrigin && ValidEnemy(target, 1))
        {
            using var attemptedMelee=PaladinThorns.Attempt(this,target,false);
            if (ClassStats.CheckHit(this, target) == true)
            {
                int damage = Utility.RandomMinMax(_melee.MinDamage, _melee.MaxDamage);
                if (Utility.RandomDouble() * 100 < NormalStats.CriticalChance)
                {
                    damage *= 2;
                }
                Deal(target, damage, false);
            }
            else
            {
                PlaySound(0x238);
            }
        }
        _actionMap = null;
    }
    protected virtual void Deal(Mobile target, int damage, bool fire)
    {
        DoHarmful(target);
        int before = target.Hits;
        AOS.Damage(target, this, damage, false, fire ? 0 : 100, fire ? 100 : 0, 0, 0, 0, weaponDamage: !fire);
        CombatDamage.Finish(target, this);
        if (!fire && target is PlayerMobile player)
        {
            HitRecovery.Apply(player, before - target.Hits, Core.TickCount);
            GearEffects.WearArmor(player, (BaseWeapon)Weapon, damage);
        }
    }
    public override void OnThink()
    {
        RecoverLife(Core.TickCount);
        if (AIObject is LeashedMeleeAI ai && ai.UpdateLeash(Core.TickCount))
        {
            return;
        }
        base.OnThink();
        long now = Core.TickCount;
        if (!Caster || !NamedControl.SpecialAllowed(this) || _actionTimer.Running || now - _nextAction < 0
            || !ValidEnemy(Combatant, 12))
        {
            return;
        }
        if (now - _nextRaise >= 0 && FindRaiseTarget() is { } corpse && BeginRaise(corpse))
        {
            return;
        }
        BeginFirebolt(Combatant);
    }
    internal void RecoverLife(long now)
    {
        var normal = NormalStats;
        if (normal == null || _lastRecovery == 0 || Deleted || !Alive || Poisoned || !TimedEffects.CanRegenerateHits(this) || ReturningHome
            || Map == null || Map == Map.Internal || Hits >= HitsMax)
        {
            _lastRecovery = now;
            _recoveryFraction = 0;
            return;
        }
        // MonStats DamageRegen applies to rolled base life, in 1/256 HP per 40 ms.
        // Do not accrue long inactive-sector/paused-server gaps as offline healing.
        long elapsed = Math.Clamp(now - _lastRecovery, 0, 1000);
        _lastRecovery = now;
        int regen = ((HitsMax << 8) * normal.DamageRegen) >> 12;
        _recoveryFraction += elapsed * regen;
        int heal = (int)Math.Min(HitsMax - Hits, _recoveryFraction / 10240);
        _recoveryFraction %= 10240;
        Hits += heal;
        if (Hits >= HitsMax)
        {
            _recoveryFraction = 0;
        }
    }
    public virtual bool BeginFirebolt(Mobile target)
    {
        if (!Caster || !NamedControl.SpecialAllowed(this) || !ValidEnemy(target, 12) || _actionTimer.Running
            || Core.TickCount - _nextAction < 0)
        {
            return false;
        }
        StartAction(target);
        Direction = GetDirectionTo(target);
        Animate(4, 5, 1, true, false, 0);
        PlaySound(0x15E);
        Timer.StartTimer(TimeSpan.FromMilliseconds(480), ReleaseFirebolt, out _actionTimer);
        return true;
    }
    private void ReleaseFirebolt()
    {
        if (!SameOrigin || !ValidEnemy(_actionTarget, 12))
        {
            _actionTarget = null;
            _actionMap = null;
            return;
        }
        var target=_actionTarget;_actionTarget=null;_actionMap=null;
        var row=CreatureProfiles.For(this);
        int minimum=MonsterVariants.Difficulty(this)==MonsterDifficulty.Normal?6:row.Attack1.MinDamage??6;
        int maximum=MonsterVariants.Difficulty(this)==MonsterDifficulty.Normal?10:row.Attack1.MaxDamage??10;
        // Shared Q12 flight snapshots endpoints/slow at release. No homing
        // impact timer or wait-for-arrival gate on the next attack cycle.
        WeaponMissiles.LaunchHostileFirebolt(this,target.Location,minimum*256,maximum*256);
    }
    public virtual bool CanRaise(Corpse corpse) => Caster && corpse is { Deleted: false, OwnerWasBaseCreature: true, Parent: null,
        Carved: false, Animated: false, SanctuaryHarvested: false, SanctuaryConsumed: false, SanctuaryRestInPeace: false, IsBones: false, OwnerWasSummoned: false }
        && !CorpseSkillRules.NoSelect(corpse) && corpse.SanctuaryUndeadKind is 1 or 2 && corpse.Map == Map && InRange(corpse, 8)
        && Math.Abs(corpse.Z - Z) < 16 && InLOS(corpse) && CanSee(corpse);
    private Corpse FindRaiseTarget()
    {
        Corpse result = null;
        int distance = int.MaxValue;
        foreach (var corpse in Map.GetItemsInRange<Corpse>(Location, 8))
        {
            int candidate = Math.Max(Math.Abs(corpse.X - X), Math.Abs(corpse.Y - Y));
            if (CanRaise(corpse) && (candidate < distance || candidate == distance && (result == null || corpse.Serial.Value < result.Serial.Value)))
            {
                result = corpse;
                distance = candidate;
            }
        }
        return result;
    }
    public bool BeginRaise(Corpse corpse)
    {
        if (!Alive || Deleted || Frozen || Paralyzed || !NamedControl.SpecialAllowed(this) || !CanRaise(corpse) || _actionTimer.Running
            || Core.TickCount - _nextAction < 0 || Core.TickCount - _nextRaise < 0)
        {
            return false;
        }
        _raiseTarget = corpse;
        _actionOrigin = Location;
        _actionMap = Map;
        _nextAction = Core.TickCount + 1600;
        _nextRaise = Core.TickCount + 8000;
        Direction = GetDirectionTo(corpse);
        Animate(4, 5, 1, true, false, 0);
        Effects.SendLocationParticles(EffectItem.Create(corpse.Location, corpse.Map, EffectItem.DefaultDuration), 0x376A, 9, 20, 5024);
        Timer.StartTimer(TimeSpan.FromMilliseconds(800), ResolveRaise, out _actionTimer);
        return true;
    }
    private void ResolveRaise()
    {
        var corpse = _raiseTarget;
        _raiseTarget = null;
        if (!SameOrigin || !Alive || Deleted || Frozen || Paralyzed || !NamedControl.SpecialAllowed(this) || !CanRaise(corpse)
            || !Map.CanSpawnMobile(corpse.X, corpse.Y, corpse.Z))
        {
            _actionMap = null;
            return;
        }
        int nearby = 0;
        foreach (var creature in Map.GetMobilesInRange<GraveyardUndead>(Location, 18))
        {
            if (creature.Raised && ++nearby >= 6)
            {
                _actionMap = null;
                return;
            }
        }
        GraveyardUndead raised = corpse.SanctuaryUndeadKind == 1 ? new GraveyardSkeleton() : new GraveyardZombie();
        if (MonsterVariants.Difficulty(this) is not MonsterDifficulty.Normal and var difficulty) { MonsterVariants.Assign(raised, difficulty); }
        raised.MarkRaised(corpse.SanctuaryNoRewards);
        raised.MoveToWorld(corpse.Location, corpse.Map);
        raised.Combatant = Combatant;
        raised.FixedParticles(0x376A, 9, 20, 5024, EffectLayer.Waist);
        raised.PlaySound(0x1FB);
        corpse.Animated = true;
        corpse.SanctuaryConsumed = true;
        if (!ClassLoot.HasContents(corpse))
        {
            corpse.Delete();
        }
        _actionMap = null;
    }
    private void MarkRaised(bool noRewards)
    {
        _raised = noRewards;
        NoKillAwards = noRewards;
    }
    public override void OnDeath(Container corpse)
    {
        if (corpse is Corpse body)
        {
            body.SanctuaryUndeadKind = RaiseKind;
        }
        base.OnDeath(corpse);
    }
    internal virtual void StopCombatActions()
    {
        _actionTimer.Cancel();
        _actionTarget = null;
        _raiseTarget = null;
        _actionMap = null;
        _nextAction = Core.TickCount;
    }
    internal void InterruptForRecovery()
    {
        long deadline = _nextAction;
        StopCombatActions();
        _nextAction = deadline;
    }
    public override void OnDelete()
    {
        StopCombatActions();
        base.OnDelete();
    }
    [AfterDeserialization]
    private void AfterLoad()
    {
        if (NormalStats is { } normal)
        {
            int oldMax = HitsMax, oldLife = Hits;
            if (this is GraveyardSkeleton && oldMax == 24 || this is GraveyardZombie && oldMax == 44)
            {
                int life = CombatProfile.Life + (int)(Serial.Value % (uint)(normal.MaxLife - CombatProfile.Life + 1));
                SetHits(life);
                Hits = Math.Clamp((int)(((long)oldLife * life + oldMax - 1) / oldMax), 0, life);
            }
            var profile = CombatProfile;
            SetDamage(profile.MinDamage, profile.MaxDamage);
            SetResistance(ResistanceType.Physical, profile.Physical);
            SetResistance(ResistanceType.Fire, profile.Fire);
            SetResistance(ResistanceType.Cold, profile.Cold);
            SetResistance(ResistanceType.Poison, profile.Poison);
            SetResistance(ResistanceType.Energy, profile.Lightning);
        }
        _nextAction = Core.TickCount + (NormalStats?.AiDelayMs ?? CombatProfile.AttackMs);
        _nextRaise = Core.TickCount + 3000;
        NoKillAwards = Raised;
    }
}

[SerializationGenerator(0)]
public partial class GraveyardSkeleton : GraveyardUndead
{
    public override MonsterProfile CombatProfile => GraveyardProfiles.Skeleton;
    protected override int RaiseKind => 1;
    [Constructible]
    public GraveyardSkeleton()
    {
        Body = 50;
        BaseSoundID = 0x48D;
    }
    public override string DefaultName => "a graveyard skeleton";
    public override string CorpseName => "a skeletal corpse";
}

[SerializationGenerator(0)]
public partial class GraveyardZombie : GraveyardUndead
{
    public override MonsterProfile CombatProfile => GraveyardProfiles.Zombie;
    protected override int RaiseKind => 2;
    [Constructible]
    public GraveyardZombie()
    {
        Body = 3;
        BaseSoundID = 471;
    }
    public override string DefaultName => "a graveyard zombie";
    public override string CorpseName => "a rotting corpse";
}

[SerializationGenerator(0)]
public partial class GraveyardLich : GraveyardUndead
{
    public override MonsterProfile CombatProfile => GraveyardProfiles.Lich;
    protected override bool Caster => true;
    [Constructible]
    public GraveyardLich()
    {
        Body = 24;
        BaseSoundID = 0x3E9;
    }
    public override string DefaultName => "a graveyard lich";
    public override string CorpseName => "a lich's corpse";
}
