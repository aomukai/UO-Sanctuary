// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class EncounterCreature : GraveyardUndead
{
    internal const int ParagonBodyHue = 0x482;

    [SerializableField(0, setter: "private")] private string _profileId;
    [SerializableField(1, setter: "private")] private int _baseLevel;
    [SerializableField(2, setter: "private")] private MonsterDifficulty _difficulty;
    [SerializableField(3, setter: "private")] private EncounterGrade _grade;
    [SerializableField(4, setter: "private")] private ChampionKind _champion;
    [SerializableField(5, setter: "private")] private string _modifierIds;
    [SerializableField(6, setter: "private")] private EliteAura _aura;
    [SerializableField(7, setter: "private")] private int _rolledLife;
    [SerializableField(8, setter: "private")] private int _rolledAttack;
    [SerializableField(9, setter: "private")] private int _rolledDefense;
    [SerializableField(10)] private uint _groupLeader;
    [SerializableField(11, setter: "private")] private bool _deathEffectsDone;
    private CreatureVariant _profile;
    private MonsterDifficulty? _profileDifficulty;
    private int _profilePlayers;
    private MonsterProfile _combat;
    private NormalMonsterStats _meleeStats;
    private EliteModifier[] _modifiers;
    private TimerExecutionToken _shot;
    private TimerExecutionToken _inferno;
    private Mobile _shotTarget;
    private Map _shotMap;
    private Point3D _shotOrigin;
    private long _teleportAfter = Core.TickCount;
    public DungeonProfile Dungeon => EncounterProfiles.Profile(_profileId ?? "crypt-sentinel-60");
    public MonsterLevel Basis => MonsterBalance.For(Dungeon.Archetype, EncounterLevel(_baseLevel == 0 ? 60 : _baseLevel));
    protected virtual int EncounterLevel(int baseLevel) => _grade == EncounterGrade.Champion && _champion == ChampionKind.SameLevel
        ? baseLevel : EncounterProfiles.Level(baseLevel, _grade);
    internal bool ParagonAlreadyRolled { get; set; }
    internal int BaseExperience => checked(Basis.Experience * (_grade switch
    {
        EncounterGrade.Ordinary => 1,
        EncounterGrade.Champion => ParagonBalance.ExperienceMultiplier,
        _ => 5
    }));
    public EliteModifier[] Modifiers => _modifiers ??= string.IsNullOrEmpty(_modifierIds) ? []
        : Array.ConvertAll(_modifierIds.Split(','), value => Enum.Parse<EliteModifier>(value));
    public bool Has(EliteModifier modifier) => _grade != EncounterGrade.Minion && Inherits(modifier);
    public bool Inherits(EliteModifier modifier) => Array.IndexOf(Modifiers, modifier) >= 0;
    public bool Leader => _grade is EncounterGrade.Unique or EncounterGrade.SuperUnique;
    public CreatureVariant EncounterProfile { get { EnsureProfile(); return _profile; } }
    internal CreatureVariant ProfileFor(MonsterDifficulty? difficulty)
    {
        EnsureProfile();
        if(!difficulty.HasValue||difficulty==_profileDifficulty){return _profile;}
        // Explicit previews must not replace the active profile or the
        // combat/animation objects already supplied to current consumers.
        var active=(_profile,_combat,_meleeStats,_profileDifficulty);
        try{EnsureProfile(difficulty);return _profile;}
        finally{(_profile,_combat,_meleeStats,_profileDifficulty)=active;}
    }
    public override MonsterProfile CombatProfile
    {
        get
        {
            EnsureProfile(); int speed = EliteAuras.Speed(this);
            return speed == 0 ? _combat : _combat with
            { AttackMs = Math.Max(40, _combat.AttackMs * 100 / (100 + speed)) };
        }
    }
    public override NormalMonsterStats NormalStats
    {
        get
        {
            EnsureProfile(); int speed = EliteAuras.Speed(this);
            return speed == 0 ? _meleeStats : _meleeStats with
            { Attack1 = _meleeStats.Attack1 with { Speed = _meleeStats.Attack1.Speed * (100 + speed) / 100 },
                Attack2 = _meleeStats.Attack2 with { Speed = _meleeStats.Attack2.Speed * (100 + speed) / 100 } };
        }
    }
    protected override bool Caster => Dungeon.Archetype is MonsterArchetype.Caster or MonsterArchetype.Ranged;
    public override string DefaultName => "a dungeon sentinel";
    public override string CorpseName => "a dungeon creature's corpse";
    [Constructible]
    public EncounterCreature()
    {
        Assign(Dungeon, 60, MonsterDifficulty.Normal, EncounterGrade.Ordinary, ChampionKind.Champion, [], EliteAura.None, null, Random.Shared);
    }
    internal void Assign(DungeonProfile profile, int baseLevel, MonsterDifficulty difficulty, EncounterGrade grade,
        ChampionKind champion, EliteModifier[] modifiers, EliteAura aura, string name, Random random)
    {
        if (Map != Map.Internal || DamageEntries.Count != 0 || baseLevel < profile.MinimumLevel || baseLevel > profile.MaximumLevel
            || !Enum.IsDefined(difficulty) || !Enum.IsDefined(grade) || !Enum.IsDefined(champion) || !Enum.IsDefined(aura)
            || !EncounterProfiles.Legal(modifiers, profile, difficulty))
        { throw new InvalidOperationException("An encounter identity requires a fresh wild creature."); }
        ApplyIdentity(profile, baseLevel, difficulty, grade, champion, modifiers, aura, name, random);
    }

    protected void ChangeForm(DungeonProfile profile, int level)
    {
        // Refilling Hits clears native loot credit. Carry the existing damage
        // and pet-owner credit across the change of form.
        using var credit = PooledRefList<DamageEntry>.Create();
        foreach (var entry in DamageEntries)
            credit.Add(entry);

        StopCombatActions();
        EliteBolts.Cancel(this);
        ApplyIdentity(profile, level, Difficulty, EncounterGrade.Ordinary, ChampionKind.Champion,
            [], EliteAura.None, null, Random.Shared);
        SkillHealth.Clear(this);
        LastKiller = null;
        foreach (var entry in credit)
        {
            var restored = RegisterDamage(entry.DamageGiven, entry.Damager);
            restored.LastDamage = entry.LastDamage;
            restored.Responsible = entry.Responsible;
        }
        this.MarkDirty();
    }

    private void ApplyIdentity(DungeonProfile profile, int baseLevel, MonsterDifficulty difficulty, EncounterGrade grade,
        ChampionKind champion, EliteModifier[] modifiers, EliteAura aura, string name, Random random)
    {
        _profileId = profile.Id; _baseLevel = baseLevel; _difficulty = difficulty; _grade = grade;
        _champion = champion; _modifierIds = string.Join(',', modifiers); _modifiers = (EliteModifier[])modifiers.Clone(); _aura = aura;
        var roll = Basis.Roll(random);
        _rolledLife = roll.Life; _rolledAttack = roll.AttackRating; _rolledDefense = roll.Defense;
        _profile = null; EnsureProfile();
        Body = profile.Body; BaseSoundID = profile.Sound; RangeFight = Caster ? 10 : 1;
        string encounterName = name ?? (_grade == EncounterGrade.Unique ? $"{profile.Name} the {new[] { "Ravager", "Accursed", "Unhallowed", "Dire" }[random.Next(4)]}"
            : _grade == EncounterGrade.Minion ? $"{profile.Name} minion" : profile.Name);
        Name = _grade == EncounterGrade.Champion ? ParagonName(encounterName) : encounterName;
        Hue = 0;
        ApplyElitePalette();
        CreatureProfiles.Session(this).Initialized = false; CreatureProfiles.Initialize(this); Hits = HitsMax;
    }
    private void ApplyElitePalette()
    {
        if (IsParagon)
            return;
        if (_grade == EncounterGrade.Champion)
            Hue = ParagonBodyHue;
        else if (Leader)
        {
            if (!Body.IsHuman && !Body.IsGargoyle)
                Hue = 0;
            else
            {
                int skin = Hue & 0x7FFF;
                if ((Hue & 0x8000) == 0 || Race.ClipSkinHue(skin) != skin)
                    Hue = Race.RandomSkinHue();
            }
        }
    }

    internal void PromoteSpawnToParagon()
    {
        _grade = EncounterGrade.Champion;
        _champion = ChampionKind.SameLevel;
        _profile = null;
        Name = ParagonName(Name);
        ApplyElitePalette();
        CreatureProfiles.Session(this).Initialized = false;
        CreatureProfiles.Initialize(this);
        Hits = HitsMax;
        this.MarkDirty();
    }

    protected virtual int[] AdjustResistances(int[] resistances) => resistances;
    internal int EncounterPlayers => RegionalEncounterPlayers == DungeonPlayerScaling.Players
        || DungeonPlayerScaling.IsWildDungeonCreature(this) ? DungeonPlayerScaling.Players : RegionalEncounterPlayers;
    protected virtual int RegionalEncounterPlayers => DungeonPlayerScaling.Players;
    protected virtual ResistanceType MeleeElement => ResistanceType.Physical;
    protected virtual int EncounterMoveInterval(int interval) => interval;
    protected virtual int EncounterDamageRegen => 2;
    protected virtual int AdjustEncounterLife(int life) => life;
    protected virtual bool RollMissingModifiersOnLoad => false;
    protected virtual bool IsRefuge(Point3D point) => AnkhRefuges.Contains(Map, point);
    internal bool RepelledByRefuge(Point3D point) => !Controlled && !Summoned && IsRefuge(point);

    internal override bool IsEnemyIgnoringLeash(Mobile mobile) =>
        mobile != null && !RepelledByRefuge(mobile.Location) && base.IsEnemyIgnoringLeash(mobile);

    public override bool Move(Direction direction)
    {
        var next = Location;
        Server.Movement.Movement.Offset(direction, ref next);
        if (RepelledByRefuge(next) && !RepelledByRefuge(Location))
            return false;
        return base.Move(direction);
    }

    private int ScaleLifeForPlayers(int life, int players) =>
        AdjustEncounterLife(Math.Min(65000, DungeonPlayerScaling.Scale(life, players)));
    private int LifeBeforePlayerScaling(MonsterDifficulty difficulty) => (_rolledLife == 0 ? Basis.Life.Min : _rolledLife)
        * EncounterProfiles.LifePermille(_grade, _champion, difficulty) / 1000;
    protected virtual CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new("DungeonUndead", true, false, Leader, false, true, 1, 2,
            dungeon.Archetype == MonsterArchetype.Tank ? 100 : 0, Leader || _grade == EncounterGrade.Champion ? 50 : 100, 0);
    private void EnsureProfile(MonsterDifficulty? requested=null)
    {
        var difficulty=requested??ReviveFacets.Difficulty(this)??_difficulty;
        if(!Enum.IsDefined(difficulty)){throw new ArgumentOutOfRangeException(nameof(requested));}
        int players = EncounterPlayers;
        if (_profile != null && _profileDifficulty == difficulty && _profilePlayers == players) { return; }
        var basis = Basis; var dungeon = Dungeon;
        int index = (int)difficulty;
        int life = ScaleLifeForPlayers(LifeBeforePlayerScaling(difficulty), players);
        int rating = _rolledAttack == 0 ? basis.AttackRating.Min : _rolledAttack;
        int defense = _rolledDefense == 0 ? basis.Defense.Min : _rolledDefense;
        int damageBonus = 0, ratingBonus = 0, moveBonus = 0;
        if (_grade == EncounterGrade.Champion)
        {
            damageBonus = new[] { 90, 75, 66 }[index]; ratingBonus = new[] { 67, 56, 49 }[index];
            if (_champion == ChampionKind.Berserker) { damageBonus *= 3; ratingBonus = damageBonus; }
            moveBonus = _champion switch { ChampionKind.Ghostly or ChampionKind.Berserker => 0, ChampionKind.Fanatic => 100, _ => 20 };
            if (_champion == ChampionKind.Fanatic) { defense = defense * 30 / 100; }
        }
        if (Inherits(EliteModifier.ExtraStrong))
        {
            var strong=EncounterProfiles.ExtraStrong(difficulty,_grade==EncounterGrade.Minion);
            damageBonus += strong.DamagePercent;ratingBonus += strong.AttackPercent;
        }
        if (Inherits(EliteModifier.ExtraFast)) { moveBonus += EncounterProfiles.ModifierMovementPercent(EliteModifier.ExtraFast); }
        if (Has(EliteModifier.StoneSkin)) { defense = EncounterProfiles.ModifierDefense(defense,EliteModifier.StoneSkin); }
        int[] res = EncounterProfiles.Resistances(dungeon.Resistances, _grade == EncounterGrade.Champion ? _champion : null,
            _grade == EncounterGrade.Minion ? [] : Modifiers);
        res = AdjustResistances(res);
        int min = basis.MinDamage * (100 + damageBonus) / 100, max = basis.MaxDamage * (100 + damageBonus) / 100;
        rating = rating * (100 + ratingBonus) / 100;
        int move = EncounterMoveInterval(EncounterProfiles.MovementInterval(basis.MoveMs,moveBonus));
        int xp = DungeonPlayerScaling.Scale(BaseExperience, players);
        var element = dungeon.Archetype == MonsterArchetype.Caster ? ResistanceType.Fire : MeleeElement;
        var attack = new CreatureAttack(min, max, rating, Caster ? 10 : 1, basis.AttackMs + basis.RecoveryMs,
            basis.EffectMs, basis.RecoveryMs, element == ResistanceType.Physical ? 100 : 0,
            element == ResistanceType.Fire ? 100 : 0, 0, element == ResistanceType.Cold ? 100 : 0,
            element == ResistanceType.Poison ? 100 : 0, 0, basis.CriticalChance,
            Caster ? dungeon.Archetype == MonsterArchetype.Caster ? "Firebolt" : "Arrow" : "Melee", 1);
        _profile = new(true, true, basis.Level, life, defense, new(res[0], res[1], res[2], res[3], res[4], res[5]),
            attack, attack, Caster ? attack : new(), new(true, dungeon.Archetype == MonsterArchetype.Tank ? 6 : 4),
            new(move, move, move), new("Species", 0, 200, 100, 0, 400, false, 100, 75, basis.RecoveryMs),
            new(xp, null, true, basis.Level, dungeon.Reference, $"Players-{players} dungeon archetype with saved encounter grade"),
            EncounterTraits(dungeon), new("Native", null, true),
            new Dictionary<string, string>(), new Dictionary<string, string>());
        _combat = new(basis.Level, life, rating, defense, min, max, basis.AttackMs + basis.RecoveryMs, move,
            res[0], res[2], res[4], res[5], res[3]);
        var melee = new MonsterMelee(min, max, basis.AttackMs / 40, 256, basis.EffectMs / 40);
        _meleeStats = new(life, melee, melee, 100, 75, basis.RecoveryMs / 40, 10, basis.CriticalChance, EncounterDamageRegen, xp, new(100, 0, 400, false));
        _profileDifficulty=difficulty;
        _profilePlayers = players;
    }
    protected override void Deal(Mobile target, int damage, bool fire) =>
        EliteCombat.Hit(this, target, damage, fire ? ResistanceType.Fire : MeleeElement);

    // Map levels 1–99 into the effect formulas' normal 1–20 range.
    internal int SpellEffectRank => Basis.Level / 5 + 1;

    internal SkillValues SpellEffects(CombatAbility ability) =>
        SkillMechanics.UnsynergizedValues(ability, SpellEffectRank, Basis.Level);

    internal int RollSpellDamage256()
    {
        var attack = EncounterProfile.Attack1;
        return 256 * Utility.RandomMinMax(attack.MinDamage.Value, attack.MaxDamage.Value);
    }

    protected override bool CanRaiseKind(Corpse corpse) => false;
    protected virtual bool CanCastEncounterBolt => Caster;
    protected virtual int EncounterBoltRange => Basis.Range;
    protected virtual int EncounterCastAnimation => 4;

    public override bool BeginFirebolt(Mobile target)
    {
        if (!CanCastEncounterBolt || !EliteCombat.CanAct(this) || SpellInProgress || !ActionReady || !EliteCombat.Enemy(this, target, EncounterBoltRange) || CreatureHitRecovery.Remaining(this) > 0)
        { return false; }
        StartAction(target); _shotTarget = target; _shotMap = Map; _shotOrigin = Location;
        Direction = GetDirectionTo(target); Animate(EncounterCastAnimation, 5, 1, true, false, 0);
        int delay = Math.Max(40, Basis.EffectMs * 100 / (100 + EliteAuras.Speed(this)) * 100 / (100 - GearEffects.AttackSlow(this)));
        Timer.StartTimer(TimeSpan.FromMilliseconds(delay), ReleaseShot, out _shot);
        return true;
    }
    private void ReleaseShot()
    {
        var target = _shotTarget; _shotTarget = null; var map = _shotMap; _shotMap = null;
        if (!NamedControl.SpecialAllowed(this) || Map != map || Location != _shotOrigin || Frozen || Paralyzed || !EliteCombat.Enemy(this, target, EncounterBoltRange)
            || CreatureHitRecovery.Remaining(this) > 0) { return; }
        ReleaseEncounterBolt(target);
    }

    protected virtual void ReleaseEncounterBolt(Mobile target)
    {
        bool fire = Dungeon.Archetype == MonsterArchetype.Caster;
        EliteBolts.Fan(this, target, Has(EliteModifier.Multishot) ? 3 : 1, fire);
    }

    protected T FindWoundedAlly<T>(int range) where T : EncounterCreature
    {
        T wounded = null;
        foreach (var ally in Map.GetMobilesInRange<T>(Location, range))
        {
            if (ally == this || !ally.Alive || ally.Controlled || ally.Summoned || !IsFriend(ally)
                || Math.Abs(ally.Z - Z) >= 16 || SkillHealth.Full(ally) || !CanSee(ally) || !InLOS(ally))
                continue;
            if (wounded == null || (long)ally.Hits * wounded.HitsMax < (long)wounded.Hits * ally.HitsMax)
                wounded = ally;
        }
        return wounded;
    }

    protected void HealAlly(Mobile ally, int percent)
    {
        Direction = GetDirectionTo(ally);
        Animate(EncounterCastAnimation, 5, 1, true, false, 0);
        SkillHealth.Heal(ally, SkillHealth.Maximum256(ally) * percent / 100);
        ally.FixedEffect(0x376A, 9, 32);
        ally.PlaySound(0x1F2);
    }

    protected const int InfernoRange = 5;
    protected bool SpellInProgress => _shot.Running || _inferno.Running;

    protected bool BeginInferno(Mobile target)
    {
        if (SpellInProgress || !ActionReady || !EliteCombat.CanAct(this) || CreatureHitRecovery.Remaining(this) > 0
            || !EliteCombat.Enemy(this, target, InfernoRange) || target.X == X && target.Y == Y)
            return false;

        var origin = Location;
        var map = Map;
        int dx = target.X - X, dy = target.Y - Y;
        int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        double stepX = dx / (double)distance, stepY = dy / (double)distance;
        StartAction(target);
        Direction = GetDirectionTo(target);
        Animate(4, 5, 1, true, false, 0);
        int phase = 0;
        Timer.StartTimer(TimeSpan.FromMilliseconds(520), TimeSpan.FromMilliseconds(180), 4,
            () => InfernoPulse(map, origin, stepX, stepY, phase++), out _inferno);
        return true;
    }

    private void InfernoPulse(Map map, Point3D origin, double stepX, double stepY, int phase)
    {
        if (Map != map || Location != origin || ReturningHome || !EliteCombat.CanAct(this)
            || !NamedControl.SpecialAllowed(this) || CreatureHitRecovery.Remaining(this) > 0)
        {
            _inferno.Cancel();
            return;
        }
        if (phase == 0)
            PlaySound(SpellEffectSounds.InfernoJet);

        var previous = origin;
        int graphic = SorceressVisuals.DirectionalFrame(SpellEffectGraphics.InfernoFlight, stepX, stepY, phase);
        for (int i = 1; i <= InfernoRange; i++)
        {
            var point = new Point3D(origin.X + (int)Math.Round(stepX * i), origin.Y + (int)Math.Round(stepY * i), origin.Z);
            if (!EncounterProfiles.ClearSight(map, previous, point))
                break;

            Effects.SendLocationEffect(point, map, graphic, SpellEffectGraphics.FireFrames);
            foreach (var target in map.GetMobilesInRange<Mobile>(point, 0))
            {
                if (EliteCombat.Enemy(this, target, InfernoRange))
                    EliteCombat.Damage(this, target, fire: RollSpellDamage256() / (3.0 * 256));
            }
            previous = point;
        }
    }
    public override void OnThink()
    {
        EliteAuras.Observe(this);
        if (NamedControl.SpecialAllowed(this) && Has(EliteModifier.Teleport) && Core.TickCount - _teleportAfter >= 0 && !Frozen && !Paralyzed
            && CreatureHitRecovery.Remaining(this) == 0 && EliteCombat.Enemy(this, Combatant, 18)
            && (Hits * 100L < HitsMax * 30L || Caster && InRange(Combatant, 2)) && EliteCombat.CanAct(this))
        {
            _teleportAfter = Core.TickCount + EncounterProfiles.Settings.TeleportCooldownMs;
            EliteCombat.Teleport(this);
        }
        base.OnThink();
    }
    internal void DeathEffects()
    {
        if (_deathEffectsDone) { return; }
        _deathEffectsDone = true; EliteCombat.Death(this);
    }
    public override void OnDeath(Container corpse)
    {
        DeathEffects(); EliteAuras.Forget(this); base.OnDeath(corpse);
    }
    internal override void StopCombatActions()
    {
        _inferno.Cancel();
        _shot.Cancel(); _shotTarget = null; _shotMap = null; base.StopCombatActions();
    }
    public override void OnAfterSpawn()
    {
        base.OnAfterSpawn();
        ApplyElitePalette();
        if (_grade == EncounterGrade.Champion)
            Name = ParagonName(Name);
    }
    public override void OnDelete()
    {
        EliteAuras.Forget(this); EliteBolts.Cancel(this); base.OnDelete();
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (_grade == EncounterGrade.Champion && _champion is not (ChampionKind.Champion or ChampionKind.SameLevel))
            list.Add($"{_champion} {EncounterProfiles.ParagonLabel}");
        if (_grade != EncounterGrade.Minion)
        {
            foreach (var modifier in Modifiers) { list.Add(EncounterProfiles.ModifierLabel(modifier)); }
            if (_aura != EliteAura.None) { list.Add($"{"Aura: "}{_aura}"); }
        }
    }

    private const string ParagonSuffix = " (" + EncounterProfiles.ParagonLabel + ")";

    private static string ParagonName(string name) => name.EndsWith(ParagonSuffix, StringComparison.Ordinal) ? name : name + ParagonSuffix;

    [AfterDeserialization]
    private void RestoreEncounter()
    {
        _modifiers = null; _profile = null;
        var dungeon = Dungeon; var basis = Basis;
        if (_baseLevel < dungeon.MinimumLevel || _baseLevel > dungeon.MaximumLevel || !Enum.IsDefined(_difficulty)
            || !Enum.IsDefined(_grade) || !Enum.IsDefined(_champion) || !Enum.IsDefined(_aura)
            || !EncounterProfiles.Legal(Modifiers, dungeon, _difficulty)
            || _rolledLife < basis.Life.Min || _rolledLife > basis.Life.Max
            || _rolledAttack < basis.AttackRating.Min || _rolledAttack > basis.AttackRating.Max
            || _rolledDefense < basis.Defense.Min || _rolledDefense > basis.Defense.Max)
        { throw new InvalidDataException("Invalid saved encounter identity."); }
        if (_grade == EncounterGrade.Champion)
        {
            string name = Name;
            if (name == $"{_champion} {dungeon.Name}" || name == $"{dungeon.Name} champion")
                name = dungeon.Name;
            Name = ParagonName(name);
        }
        ApplyElitePalette();

        if (RollMissingModifiersOnLoad && Modifiers.Length == 0)
        {
            _modifiers = EncounterProfiles.RollModifiers(dungeon, _difficulty, Random.Shared);
            _modifierIds = string.Join(',', _modifiers);
            _aura = EncounterProfiles.RollAura(_modifiers, basis.Level, Random.Shared);
        }
        _profile = null; EnsureProfile(); _teleportAfter = Core.TickCount;
        // Preserve the health percentage of spawns that predate a change in regional player scaling.
        int unscaledLife = LifeBeforePlayerScaling(_difficulty);
        int savedLife = HitsMaxSeed;
        int scaledLife = ScaleLifeForPlayers(unscaledLife, EncounterPlayers);
        bool rescale = savedLife != scaledLife
            && (savedLife == ScaleLifeForPlayers(unscaledLife, 1) || savedLife == ScaleLifeForPlayers(unscaledLife, 3));
        int? migratedHits = rescale ? (int)((long)Hits * scaledLife / savedLife) : null;
        // Native mobile fields already contain the injured life and stat roll; do not roll or heal on load.
        CreatureProfiles.Session(this).Initialized = false; CreatureProfiles.Initialize(this,preserveLoadedLife:true);
        if (migratedHits is { } hits)
            Hits = hits;
        EliteAuras.Observe(this);
    }
}
