// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class EncounterCreature : GraveyardUndead
{
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
    private MonsterProfile _combat;
    private NormalMonsterStats _meleeStats;
    private EliteModifier[] _modifiers;
    private TimerExecutionToken _shot;
    private Mobile _shotTarget;
    private Map _shotMap;
    private Point3D _shotOrigin;
    private long _teleportAfter = Core.TickCount;
    public DungeonProfile Dungeon => EncounterProfiles.Profile(_profileId ?? "crypt-sentinel-60");
    public MonsterLevel Basis => MonsterBalance.For(Dungeon.Archetype, EncounterProfiles.Level(_baseLevel == 0 ? 60 : _baseLevel, _grade));
    public EliteModifier[] Modifiers => _modifiers ??= string.IsNullOrEmpty(_modifierIds) ? []
        : Array.ConvertAll(_modifierIds.Split(','), value => Enum.Parse<EliteModifier>(value));
    public bool Has(EliteModifier modifier) => _grade != EncounterGrade.Minion && Inherits(modifier);
    public bool Inherits(EliteModifier modifier) => Array.IndexOf(Modifiers, modifier) >= 0;
    public bool Leader => _grade is EncounterGrade.Unique or EncounterGrade.SuperUnique;
    public CreatureVariant EncounterProfile { get { EnsureProfile(); return _profile; } }
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
        _profileId = profile.Id; _baseLevel = baseLevel; _difficulty = difficulty; _grade = grade;
        _champion = champion; _modifierIds = string.Join(',', modifiers); _modifiers = (EliteModifier[])modifiers.Clone(); _aura = aura;
        var roll = Basis.Roll(random);
        _rolledLife = roll.Life; _rolledAttack = roll.AttackRating; _rolledDefense = roll.Defense;
        _profile = null; EnsureProfile();
        Body = profile.Body; BaseSoundID = profile.Sound; RangeFight = Caster ? 10 : 1;
        Name = name ?? (_grade == EncounterGrade.Champion ? $"{champion} {profile.Name}"
            : _grade == EncounterGrade.Unique ? $"{profile.Name} the {new[] { "Ravager", "Accursed", "Unhallowed", "Dire" }[random.Next(4)]}"
            : _grade == EncounterGrade.Minion ? $"{profile.Name} minion" : profile.Name);
        Hue = grade switch { EncounterGrade.Champion => 0x482, EncounterGrade.Unique or EncounterGrade.SuperUnique => 0x489, _ => 0 };
        CreatureProfiles.Session(this).Initialized = false; CreatureProfiles.Initialize(this); Hits = HitsMax;
    }
    protected virtual int[] AdjustResistances(int[] resistances) => resistances;
    private void EnsureProfile()
    {
        if (_profile != null) { return; }
        var basis = Basis; var dungeon = Dungeon;
        int index = (int)_difficulty;
        int life = _rolledLife == 0 ? basis.Life.Min : _rolledLife;
        life = life * EncounterProfiles.LifePermille(_grade, _champion, _difficulty) / 1000;
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
            damageBonus += (_grade == EncounterGrade.Minion ? new[] { 67, 56, 49 } : new[] { 135, 112, 99 })[index];
            ratingBonus += (_grade == EncounterGrade.Minion ? new[] { 45, 37, 33 } : new[] { 90, 75, 66 })[index];
        }
        if (Inherits(EliteModifier.ExtraFast)) { moveBonus += 100; }
        if (Has(EliteModifier.StoneSkin)) { defense *= 2; }
        int[] res = EncounterProfiles.Resistances(dungeon.Resistances, _grade == EncounterGrade.Champion ? _champion : null,
            _grade == EncounterGrade.Minion ? [] : Modifiers);
        res = AdjustResistances(res);
        int min = basis.MinDamage * (100 + damageBonus) / 100, max = basis.MaxDamage * (100 + damageBonus) / 100;
        rating = rating * (100 + ratingBonus) / 100;
        int move = Math.Max(40, basis.MoveMs * 100 / (100 + moveBonus));
        int xp = checked(basis.Experience * (_grade == EncounterGrade.Ordinary ? 1 : _grade == EncounterGrade.Champion
            ? _champion == ChampionKind.Berserker ? 5 : 3 : 5));
        var attack = new CreatureAttack(min, max, rating, Caster ? 10 : 1, basis.AttackMs + basis.RecoveryMs,
            basis.EffectMs, basis.RecoveryMs, dungeon.Archetype == MonsterArchetype.Caster ? 0 : 100,
            dungeon.Archetype == MonsterArchetype.Caster ? 100 : 0, 0, 0, 0, 0, basis.CriticalChance,
            Caster ? dungeon.Archetype == MonsterArchetype.Caster ? "Firebolt" : "Arrow" : "Melee", 1);
        _profile = new(true, true, basis.Level, life, defense, new(res[0], res[1], res[2], res[3], res[4], res[5]),
            attack, attack, Caster ? attack : new(), new(true, dungeon.Archetype == MonsterArchetype.Tank ? 6 : 4),
            new(move, move, move), new("Species", 0, 200, 100, 0, 400, false, 100, 75, basis.RecoveryMs),
            new(xp, null, true, basis.Level, dungeon.Reference, "Bounded players-1 dungeon archetype with saved encounter grade"),
            new("DungeonUndead", true, false, Leader, false, true, 1, 2, dungeon.Archetype == MonsterArchetype.Tank ? 100 : 0,
                Leader || _grade == EncounterGrade.Champion ? 50 : 100, 0), new("Native", null, true),
            new Dictionary<string, string>(), new Dictionary<string, string>());
        _combat = new(basis.Level, life, rating, defense, min, max, basis.AttackMs + basis.RecoveryMs, move,
            res[0], res[2], res[4], res[5], res[3]);
        var melee = new MonsterMelee(min, max, basis.AttackMs / 40, 256, basis.EffectMs / 40);
        _meleeStats = new(life, melee, melee, 100, 75, basis.RecoveryMs / 40, 10, basis.CriticalChance, 2, xp, new(100, 0, 400, false));
    }
    protected override void Deal(Mobile target, int damage, bool fire) => EliteCombat.Hit(this, target, damage, fire ? ResistanceType.Fire : ResistanceType.Physical);
    public override bool CanRaise(Corpse corpse) => false;
    public override bool BeginFirebolt(Mobile target)
    {
        if (!Caster || !EliteCombat.CanAct(this) || _shot.Running || !ActionReady || !EliteCombat.Enemy(this, target, Basis.Range) || CreatureHitRecovery.Remaining(this) > 0)
        { return false; }
        StartAction(target); _shotTarget = target; _shotMap = Map; _shotOrigin = Location;
        Direction = GetDirectionTo(target); Animate(4, 5, 1, true, false, 0);
        int delay = Math.Max(40, Basis.EffectMs * 100 / (100 + EliteAuras.Speed(this)) * 100 / (100 - GearEffects.AttackSlow(this)));
        Timer.StartTimer(TimeSpan.FromMilliseconds(delay), ReleaseShot, out _shot);
        return true;
    }
    private void ReleaseShot()
    {
        var target = _shotTarget; _shotTarget = null; var map = _shotMap; _shotMap = null;
        if (!NamedControl.SpecialAllowed(this) || Map != map || Location != _shotOrigin || Frozen || Paralyzed || !EliteCombat.Enemy(this, target, Basis.Range)
            || CreatureHitRecovery.Remaining(this) > 0) { return; }
        bool fire = Dungeon.Archetype == MonsterArchetype.Caster;
        EliteBolts.Fan(this, target, Has(EliteModifier.Multishot) ? 3 : 1, fire);
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
        _shot.Cancel(); _shotTarget = null; _shotMap = null; base.StopCombatActions();
    }
    public override void OnDelete()
    {
        EliteAuras.Forget(this); EliteBolts.Cancel(this); base.OnDelete();
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{_difficulty}{" / "}{_grade}");
        if (_grade == EncounterGrade.Champion) { list.Add($"{_champion}"); }
        if (_grade != EncounterGrade.Minion)
        {
            foreach (var modifier in Modifiers) { list.Add($"{modifier}"); }
            if (_aura != EliteAura.None) { list.Add($"{"Aura: "}{_aura}"); }
        }
    }
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
        _profile = null; EnsureProfile(); _teleportAfter = Core.TickCount;
        // Native mobile fields already contain the injured life and stat roll; do not roll or heal on load.
        CreatureProfiles.Session(this).Initialized = false; CreatureProfiles.Initialize(this,preserveLoadedLife:true);
        EliteAuras.Observe(this);
    }
}
