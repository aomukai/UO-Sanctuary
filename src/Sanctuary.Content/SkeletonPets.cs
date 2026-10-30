// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using Server.Text;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

[assembly: InternalsVisibleTo("Sanctuary.Integration")]

namespace Sanctuary;

public static class SkeletonStats
{
    internal static (int Min,int Max,int Percent) WeaponBase(int rank,int mastery,MonsterDifficulty difficulty)
    {
        rank=Math.Clamp(rank,1,255);mastery=Math.Clamp(mastery,0,255);
        int flat=SkillValues.Tier(0,rank,i=>new[]{0,1,2,3,4}[i-1])+2*mastery;
        return (NecromancerSummons.Source("necroskeleton","A1MinD",difficulty)+flat,
            NecromancerSummons.Source("necroskeleton","A1MaxD",difficulty)+flat,7*Math.Max(0,rank-3));
    }
    // Expansion: pinned MonStats necroskeleton, MonLvl L-AC/L-TH,
    // and Skills Raise Skeleton/Skeleton Mastery. See docs/death-and-pets.md.
    public static MonsterProfile For(int level, int rank, int mastery, int auraDamagePercent = 0, MonsterDifficulty difficulty = MonsterDifficulty.Normal)
    {
        level = Math.Clamp(level, 1, 99);
        rank = Math.Clamp(rank, 1, 255);
        mastery = Math.Clamp(mastery, 0, 255);
        int extraRanks = Math.Max(0, rank - 3);
        int life = (NecromancerSummons.Source("necroskeleton", "minHP", difficulty) + 8 * mastery) * (2 + extraRanks) / 2;
        int ratingBonus = 15 * (rank + mastery);
        int levelAttack = NecromancerSummons.Source("level-" + level, "L-TH", difficulty);
        var weapon=WeaponBase(rank,mastery,difficulty);
        int damagePercent = Math.Max(10,100 + weapon.Percent + auraDamagePercent);
        return new MonsterProfile(level, life, NecromancerSummons.Source("necroskeleton", "A1TH", difficulty) + levelAttack + ratingBonus,
            NecromancerSummons.Source("necroskeleton", "AC", difficulty) + NecromancerSummons.Source("level-" + level, "L-AC", difficulty) + ratingBonus,
            weapon.Min * damagePercent / 100,
            weapon.Max * damagePercent / 100,
            720, 240, 0, 0, 0, 0, 0);
    }
}

public static class SkeletonPets
{
    public const int Range = 8;
    public static int Rank(PlayerMobile owner) => ClassCreation.Profiles.TryGet(owner.Serial, out var profile)
        && profile.Class == DiabloClass.Necromancer ? NamedSkills.Rank(owner, "necromancer.raiseskeleton") : 0;
    public static int Limit(int rank) => rank <= 3 ? rank : 3 + (rank - 3) / 3;
    public static int Mana(PlayerMobile owner) => 5 + Rank(owner);
    public static bool Usable(PlayerMobile owner, Corpse corpse) => owner.Alive
        && corpse is { Deleted: false, Parent: null, OwnerWasBaseCreature: true, OwnerWasSummoned: false,
            Carved: false, Animated: false, SanctuaryHarvested: false, SanctuaryConsumed: false, SanctuaryRestInPeace: false, IsBones: false }
        && !CorpseSkillRules.Shattered(corpse) && !CorpseSkillRules.NoSelect(corpse) && corpse.Owner is not BaseVendor && corpse.Owner is not BaseCreature { Controlled: true }
        && corpse.Map == owner.Map && owner.InRange(corpse, Range) && Math.Abs(owner.Z - corpse.Z) < 16
        && owner.CanSee(corpse) && owner.InLOS(corpse) && corpse.CanLoot(owner, null) && !corpse.IsCriminalAction(owner);
    public static Corpse FindCorpse(PlayerMobile owner, Serial preferred)
    {
        if (World.FindItem(preferred) is Corpse hovered && Usable(owner, hovered))
        {
            return hovered;
        }
        Corpse best = null;
        double distance = double.MaxValue;
        foreach (var corpse in owner.Map.GetItemsInRange<Corpse>(owner.Location, Range))
        {
            double next = owner.GetDistanceToSqrt(corpse);
            if (Usable(owner, corpse) && (next < distance || next == distance && (best == null || corpse.Serial.Value < best.Serial.Value)))
            {
                best = corpse;
                distance = next;
            }
        }
        return best;
    }
    public static int Count(PlayerMobile owner)
    {
        int count = 0;
        foreach (var pet in owner.AllFollowers ?? [])
        {
            if (pet is SkeletonGuardian { Deleted: false, Alive: true } guardian && guardian.ControlMaster == owner && guardian.GetType() == typeof(SkeletonGuardian))
            {
                count++;
            }
        }
        return count;
    }
    public static bool CanRaise(PlayerMobile owner, Corpse corpse) => Rank(owner) > 0
        && owner.Mana >= Mana(owner) && Usable(owner, corpse)
        && owner.Map.CanSpawnMobile(corpse.X, corpse.Y, corpse.Z);
    public static bool Raise(PlayerMobile owner, Corpse corpse)
    {
        if (!CanRaise(owner, corpse))
        {
            return false;
        }
        SkeletonGuardian oldest = null;
        int count = 0;
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is SkeletonGuardian { Deleted: false, Alive: true } existing && existing.ControlMaster == owner && existing.GetType() == typeof(SkeletonGuardian))
            {
                count++;
                if (oldest == null || existing.Serial.Value < oldest.Serial.Value)
                {
                    oldest = existing;
                }
            }
        }
        var pet = new SkeletonGuardian();
        pet.Initialize(owner, Rank(owner), NamedSkills.Rank(owner, "necromancer.skeletonmastery"));
        if (!pet.SetControlMaster(owner))
        {
            pet.Delete();
            return false;
        }
        BattleCommand.Track(pet); PersistentMinions.Profiles.Track(pet);
        pet.MoveToWorld(corpse.Location, corpse.Map);
        if (count >= Limit(Rank(owner)))
        {
            oldest?.Delete();
        }
        pet.IssueOrder(OrderType.Guard, null);
        owner.Mana -= Mana(owner);
        corpse.SanctuaryConsumed = true;
        corpse.Animated = true;
        ClassLoot.RemoveHarvestedIfEmpty(corpse);
        pet.FixedParticles(0x376A, 9, 20, 5024, EffectLayer.Waist);
        pet.PlaySound(0x1FB);
        return true;
    }
    public static void Dismiss(PlayerMobile owner)
    {
        using var pets = PooledRefList<SkeletonGuardian>.Create();
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is SkeletonGuardian pet && pet.ControlMaster == owner)
            {
                pets.Add(pet);
            }
        }
        foreach (var pet in pets)
        {
            pet.Delete();
        }
    }
}

[SerializationGenerator(0)]
public partial class SkeletonGuardian : BaseCreature, IPersistentMinion
{
    public virtual DiabloClass SummonClass => DiabloClass.Necromancer;
    public virtual bool Permitted(PlayerMobile owner)
    {
        foreach(var history in ClassCreation.Profiles.Histories(owner.Serial))
        {if(history.Profile.Class==SummonClass){return history.Ranks.TryGetValue("necromancer.raiseskeleton",out var rank) && rank>0;}}
        return false;
    }
    public MonsterDifficulty SummonDifficulty => SummonDifficulties.For(this);
    [SerializableField(0, setter: "private")]
    private int _level = 1;
    [SerializableField(1, setter: "private")]
    private int _rank = 1;
    [SerializableField(2, setter: "private")]
    private int _mastery;
    private TimerExecutionToken _attackTimer, _restoreTimer;
    private Mobile _target;
    private Point3D _origin;
    private Map _originMap;
    private long _nextAttack = Core.TickCount;
    private long _lastRecovery, _recoveryFraction;
    private MonsterProfile _profile;
    // Explicit owner-facet difficulty; action/movement periods are project balance values.
    public virtual MonsterProfile CombatProfile => _profile ??= CreateProfile();
    public virtual (int Min,int Max) SkillWeaponDamage
    {
        get { var profile=SkeletonStats.For(Level,Rank,Mastery,CombatAuras.DamagePercent(this)+NecromancerEffects.DamageStatPercent(this),SummonDifficulty); return (profile.MinDamage,profile.MaxDamage); }
    }
    internal virtual (int Min,int Max,int Percent) SourceWeaponBase=>SkeletonStats.WeaponBase(Rank,Mastery,SummonDifficulty);
    private MonsterProfile CreateProfile() => SkeletonStats.For(Level, Rank, Mastery, difficulty: SummonDifficulty);
    // Summon creation writes this FP256 result into its base life before BO.
    public virtual long SourceLife256=>((long)NecromancerSummons.Source("necroskeleton","minHP",SummonDifficulty)+8*Math.Clamp(Mastery,0,255))*256*(2+Math.Max(0,Math.Clamp(Rank,1,255)-3))/2;
    internal virtual string SourceMonsterId=>this is NecromancerMinion{Kind:not CombatAbility.None} minion?SkillMechanics.For(minion.Kind).Text("summon"):"necroskeleton";
    internal virtual int SourceRegeneration256=>5;
    internal virtual int SourceDefensePercent=>0;
    [Constructible]
    public SkeletonGuardian() : base(AIType.AI_Melee, FightMode.Closest, 14)
    {
        Name = "a skeleton guardian";
        Body = 50;
        BaseSoundID = 0x48D;
        Summoned = true;
        ControlSlots = 0;
        NoKillAwards = true;
        RangeFight = 1;
        SetStr(20);
        SetDex(20);
        SetInt(0);
        SetMana(0);
        SetStam(100);
        SetHits(21);
        SetDamage(1, 2);
    }
    public void Initialize(PlayerMobile owner, int rank, int mastery)
    {
        _level = ClassStats.Level(owner);
        _rank = rank;
        _mastery = mastery;
        SummonDifficulties.Created(this, owner);
        _profile = null;
        SetHits(CombatProfile.Life);
        SetDamage(CombatProfile.MinDamage, CombatProfile.MaxDamage);
        int resist = NecromancerSummons.Resistance(owner);
        SetResistance(ResistanceType.Fire, resist); SetResistance(ResistanceType.Cold, resist);
        SetResistance(ResistanceType.Energy, resist); SetResistance(ResistanceType.Poison, resist);
    }
    internal virtual void RefreshDifficulty()
    {
        _profile = null;
        SetHits(CombatProfile.Life); SetDamage(CombatProfile.MinDamage, CombatProfile.MaxDamage);
    }
    public override bool Commandable => false;
    public override bool IsBondable => false;
    public override bool DeleteCorpseOnDeath => true;
    public override bool CanRegenHits => false;
    public override bool CanRegenMana => false;
    public override bool BardImmune => true;
    public override bool BleedImmune => true;
    public override Poison PoisonImmune => Poison.Lethal;
    public override bool ReduceSpeedWithDamage => false;
    public override bool IsEnemy(Mobile mobile) => ControlMaster is PlayerMobile owner && ClassCombat.IsHostile(owner, mobile);
    public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness) => IsEnemy(target)
        && base.CanBeHarmful(target, message, ignoreOurBlessedness);
    public override void GenerateLoot(bool spawning) { }
    public override void GetSpeeds(out double activeSpeed, out double passiveSpeed)
    {
        activeSpeed = 0.1;
        passiveSpeed = 0.2;
    }
    public override void GetMoveSpeeds(out double activeMoveSpeed, out double passiveMoveSpeed)
    {
        activeMoveSpeed = passiveMoveSpeed = CombatProfile.MoveMs / 1000.0;
    }
    public override void OnThink()
    {
        if (!PersistentMinions.CanThink(this))
        {
            return;
        }
        RecoverLife(Core.TickCount);
        if (Combatant != null && !IsEnemy(Combatant))
        {
            Combatant = null;
            FocusMob = null;
        }
        if (ControlOrder != OrderType.Guard)
        {
            IssueOrder(OrderType.Guard, null);
        }
        base.OnThink();
    }
    internal void RecoverLife(long now)
    {
        if(NpcEquipmentRegeneration.Handles(this)){NpcEquipmentRegeneration.Recover(this,now);return;}
        NpcEquipmentRegeneration.Reset(this);
        if (_lastRecovery == 0 || !Alive || Deleted || Poisoned || !TimedEffects.CanRegenerateHits(this)
            || Map == null || Map == Map.Internal || Hits >= HitsMax)
        {
            _lastRecovery = now;
            _recoveryFraction = 0;
            return;
        }
        // D2MOO initializes HPRegen from base life: ((21 << 8) * 4) >> 12 = 5.
        // At 25 frames/sec this is 125/256 life/sec, independent of skill life bonuses.
        _recoveryFraction += Math.Max(0, now - _lastRecovery) * (BattleOrders.Regeneration256(this)*25L);
        _lastRecovery = now;
        int heal = (int)Math.Min(HitsMax - Hits, _recoveryFraction / 256000);
        _recoveryFraction %= 256000;
        if (heal > 0)
        {
            Hits += heal;
            if (Hits >= HitsMax)
            {
                _recoveryFraction = 0;
            }
        }
    }
    protected virtual bool CanHit(Mobile target) => Alive && !Deleted && AlliedOverlap.Allowed(this) && !Frozen && !Paralyzed && Map != Map.Internal && CreatureHitRecovery.Remaining(this) == 0
        && IsEnemy(target) && target.Map == Map && InRange(target, RangeFight) && Math.Abs(target.Z - Z) < 16 && InLOS(target);
    public virtual TimeSpan BeginMelee(Mobile target)
    {
        int milliseconds=PaladinOffense.CreatureCycle(this,CombatProfile.AttackMs);
        var cycle = TimeSpan.FromMilliseconds(milliseconds);
        if (!CanHit(target) || _attackTimer.Running || Core.TickCount - _nextAttack < 0)
        {
            return cycle;
        }
        _target = target;
        _origin = Location;
        _originMap = Map;
        _nextAttack = Core.TickCount + milliseconds;
        Direction = GetDirectionTo(target);
        Animate(4, 5, 1, true, false, 0);
        DoHarmful(target);
        Timer.StartTimer(TimeSpan.FromMilliseconds(milliseconds / 2), ResolveMelee, out _attackTimer);
        return cycle;
    }
    private void ResolveMelee()
    {
        var target = _target;
        _target = null;
        if (Map == _originMap && Location == _origin && CanHit(target))
        {
            using var attemptedMelee=PaladinThorns.Attempt(this,target,this is NecromancerMinion{IsMage:true});
            if(CheckAttackHit(target)){Strike(target);}
        }
        _originMap = null;
    }
    protected virtual bool CheckAttackHit(Mobile target) => ClassStats.CheckHit(this, target) == true;
    protected virtual void Strike(Mobile target)
    {
        if(NpcEquipmentDamage.TryStrike(this,target)){return;}
        SkillCombat.HitParts(this, target,
            new(Utility.RandomMinMax(SkillWeaponDamage.Min, SkillWeaponDamage.Max), 0, 0, 0, 0), true, true);
    }
    internal virtual void InterruptForRecovery()
    {
        _attackTimer.Cancel();
        _target = null;
        _originMap = null;
    }
    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (!ClassCombat.Friendly(from, this))
        {
            base.Damage(amount, from, informMount, ignoreEvilOmen);
        }
    }
    public override void OnDoubleClick(Mobile from)
    {
        if (from == ControlMaster)
        {
            PetStatsGump.DisplayTo(from);
        }
        else
        {
            base.OnDoubleClick(from);
        }
    }
    public override void OnDelete()
    {
        SuspendActions();
        _restoreTimer.Cancel();
        base.OnDelete();
    }
    internal void ResetLifeRecovery()
    {
        _lastRecovery = _recoveryFraction = 0;NpcEquipmentRegeneration.Reset(this);
    }
    public virtual void SuspendActions()
    {
        ResetLifeRecovery();
        _attackTimer.Cancel();
        _target = null;
        _originMap = null;
        _nextAttack = Core.TickCount + CombatProfile.AttackMs;
    }
    [AfterDeserialization]
    private void AfterLoad()
    {
        _profile = null;
        // Recalculate corrected derived stats, preserving saved current life.
        if (GetType() == typeof(SkeletonGuardian))
        {
            int life = Hits;
            SetHits(CombatProfile.Life);
            Hits = Math.Min(life, HitsMax);
            SetDamage(CombatProfile.MinDamage, CombatProfile.MaxDamage);
        }
        _nextAttack = Core.TickCount + CombatProfile.AttackMs;
        Timer.StartTimer(TimeSpan.Zero, () => PersistentMinions.Restore(this), out _restoreTimer);
    }
}

public sealed class PetStatsGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    public override bool Singleton => true;
    private PetStatsGump(PlayerMobile owner) : base(60, 40) => _owner = owner;
    public static void DisplayTo(Mobile mobile)
    {
        if (mobile is PlayerMobile { NetState: not null } player && ClassCombat.Enabled(player))
        {
            player.SendGump(new PetStatsGump(player));
        }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(0, 0, 510, 410, 5054);
        builder.AddLabel(20, 15, 0x481, $"Skeleton guardians: {SkeletonPets.Count(_owner)} / {SkeletonPets.Limit(SkeletonPets.Rank(_owner))}");
        builder.AddLabel(20, 42, 0x47E, "Behaviour: Guard me — follow, defend, return.");
        using var text = ValueStringBuilder.Create();
        foreach (var follower in _owner.AllFollowers ?? [])
        {
            if (follower is not SkeletonGuardian { Deleted: false } pet || pet.ControlMaster != _owner)
            {
                continue;
            }
            var p = pet.CombatProfile;
            text.Append($"<BASEFONT COLOR=#FFD37A>Skeleton — Level {p.Level}, Raise rank {pet.Rank}</BASEFONT><BR>");
            text.Append($"Life {pet.Hits}/{pet.HitsMax} | Damage {p.MinDamage}–{p.MaxDamage}<BR>");
            text.Append($"Attack rating {p.AttackRating} | Defense {p.Defense}<BR>");
            text.Append($"Resists: Physical {pet.PhysicalResistance}, Fire {pet.FireResistance}, Cold {pet.ColdResistance}, Poison {pet.PoisonResistance}, Lightning {pet.EnergyResistance}<BR>");
            text.Append($"Attack {p.AttackMs} ms | Movement {p.MoveMs} ms/tile<BR><BR>");
        }
        if (SkeletonPets.Count(_owner) == 0)
        {
            text.Append("No active skeletons. Learn Raise Skeleton, bind it, then cast near a monster corpse.");
        }
        builder.AddHtml(20, 80, 470, 270, text.ToString(), background: false, scrollbar: true);
        builder.AddButton(20, 365, 4005, 4007, 1);
        builder.AddLabel(55, 367, 0x481, "Refresh");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile == _owner && info.ButtonID == 1)
        {
            DisplayTo(_owner);
        }
    }
}
