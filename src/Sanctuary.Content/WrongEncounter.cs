// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal enum WrongRole
{
    Thug, Ruffian, Manhunter, SlaveMerchant, Hatchling, Juvenile, AdultDrake, Rowdy, Brute,
    BattleDrake, AssaultDrake, Wonk, Uruk, Glokurg, Goegork, Xoldred, Suldri
}

[SerializationGenerator(0)]
public partial class WrongEncounter : EncounterCreature
{
    private static readonly Dictionary<WrongRole, DungeonProfile> _roster = new()
    {
        [WrongRole.Thug] = Species(WrongRole.Thug, "an ogre thug", 50, 52),
        [WrongRole.Ruffian] = Species(WrongRole.Ruffian, "an ogre ruffian", 51, 53),
        [WrongRole.Manhunter] = Species(WrongRole.Manhunter, "a manhunter", 53, 54, 400, MonsterArchetype.Ranged),
        [WrongRole.SlaveMerchant] = Species(WrongRole.SlaveMerchant, "a slave merchant", 54, 56, 400, MonsterArchetype.Caster),
        [WrongRole.Hatchling] = Species(WrongRole.Hatchling, "a drake hatchling", 52, 54, 206, MonsterArchetype.Caster),
        [WrongRole.Juvenile] = Species(WrongRole.Juvenile, "a drake juvenile", 53, 55, 60, MonsterArchetype.Caster),
        [WrongRole.AdultDrake] = Species(WrongRole.AdultDrake, "an adult drake", 54, 56, 61, MonsterArchetype.Caster),
        [WrongRole.Rowdy] = Species(WrongRole.Rowdy, "an ogre rowdy", 55, 57),
        [WrongRole.Brute] = Species(WrongRole.Brute, "an ogre brute", 56, 58, archetype: MonsterArchetype.Tank),
        [WrongRole.BattleDrake] = Species(WrongRole.BattleDrake, "a battle drake", 56, 58, 60, MonsterArchetype.Caster),
        [WrongRole.AssaultDrake] = Species(WrongRole.AssaultDrake, "an assault drake", 57, 59, 61, MonsterArchetype.Caster),
        [WrongRole.Wonk] = Species(WrongRole.Wonk, "Wonk the Ogre Chef", 58, 58),
        [WrongRole.Uruk] = Species(WrongRole.Uruk, "Uruk the Ogre Barbecuer", 58, 58),
        [WrongRole.Glokurg] = Species(WrongRole.Glokurg, "Glokurg the Ogre Culinarian", 58, 58),
        [WrongRole.Goegork] = Species(WrongRole.Goegork, "Goegork the Ogre Mage", 60, 60, archetype: MonsterArchetype.Caster),
        [WrongRole.Xoldred] = Species(WrongRole.Xoldred, "Xoldred the Strong Minded", 59, 59, 61, MonsterArchetype.Tank),
        [WrongRole.Suldri] = Species(WrongRole.Suldri, "Suldri Icebreath", 59, 59, 60, MonsterArchetype.Tank)
    };
    private static readonly Dictionary<string, WrongRole> _roles =
        _roster.ToDictionary(row => row.Value.Id, row => row.Key, StringComparer.Ordinal);

    private sealed class StunWindow { internal long Until; }
    private static readonly ConditionalWeakTable<Mobile, StunWindow> _stunWindows = new();
    private static readonly int[] _bodyParts = [0x1DA0, 0x1D9F, 0x1DA1, 0x1DA2, 0x1DA3, 0x1DA4];

    [SerializableField(0)] private WrongBossSite _site;
    private TimerExecutionToken _special;
    private long _nextSpecial = Core.TickCount, _nextSpell = Core.TickCount;
    private long _nextBark = Core.TickCount;
    private int _nextCast, _spellGeneration;
    private bool _opened;
    private int _ward256, _wardStunFrames;

    internal static DungeonProfile FindSpecies(string id) => _roles.TryGetValue(id, out var role) ? _roster[role] : null;
    internal WrongRole Role => _roles.GetValueOrDefault(Dungeon.Id);
    internal bool IsGuard => Role is WrongRole.Xoldred or WrongRole.Suldri;
    private bool WaitingMage => Role == WrongRole.Goegork && _site is { MageReleased: false };
    private bool Cook => Role is >= WrongRole.Wonk and <= WrongRole.Glokurg;
    protected override bool Caster => Role != WrongRole.Goegork && base.Caster;
    protected override bool BusyWithSpecial => _special.Running || SpellInProgress;
    public override bool DisallowAllMoves => WaitingMage || _special.Running;
    public override Poison PoisonImmune => null;
    public override bool BleedImmune => false;
    public override OppositionGroup OppositionGroup => null;
    public override bool AlwaysMurderer => true;
    public override string CorpseName => $"{Name}'s corpse";
    protected override int EncounterLevel(int baseLevel) => baseLevel;
    protected override ResistanceType MeleeElement => Role switch
    {
        WrongRole.Xoldred => ResistanceType.Fire, WrongRole.Suldri => ResistanceType.Cold, _ => ResistanceType.Physical
    };
    internal override int HomeLeashRadius => Cook ? 48 : base.HomeLeashRadius;
    protected override int AdjustEncounterLife(int life) => IsGuard ? checked(life * 2) : life;
    protected override bool CanCastEncounterBolt => Role == WrongRole.Manhunter || !Spells(Role).IsEmpty;
    protected override int EncounterBoltRange => Role == WrongRole.Suldri ? 6 : 10;
    protected override int EncounterCastAnimation => Body == 400 ? Role == WrongRole.Manhunter ? 19 : 16 : base.EncounterCastAnimation;

    private static DungeonProfile Species(WrongRole role, string name, int minimum, int maximum, int body = 1,
        MonsterArchetype archetype = MonsterArchetype.Melee) =>
        new($"wrong-{role.ToString().ToLowerInvariant()}", "Wrong", name, archetype, "Wrong prison",
            minimum, maximum, body, body == 1 ? 427 : body == 400 ? 0 : 362, new(0, 0, 0, 0, 0, 0));

    [Constructible]
    public WrongEncounter(string role) : this(Parse(role), 0, null) { }

    [Constructible]
    public WrongEncounter(string role, int paragonChance) : this(Parse(role), paragonChance, null) => ParagonAlreadyRolled = true;

    private static WrongRole Parse(string role) =>
        Enum.TryParse<WrongRole>(role, true, out var parsed) && _roster.ContainsKey(parsed) ? parsed
            : throw new ArgumentException("Unknown Wrong encounter role.", nameof(role));

    internal WrongEncounter(WrongRole role, int paragonChance, WrongBossSite site)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(paragonChance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(paragonChance, 100);
        Site = site;
        var species = _roster[role];
        var grade = role >= WrongRole.Wonk ? EncounterGrade.Unique
            : Utility.Random(100) < ParagonBalance.SpawnChance(paragonChance) ? EncounterGrade.Champion : EncounterGrade.Ordinary;
        EliteModifier[] modifiers = role is WrongRole.Xoldred or WrongRole.Suldri ? [EliteModifier.StoneSkin] : [];
        Assign(species, Utility.RandomMinMax(species.MinimumLevel, species.MaximumLevel), MonsterDifficulty.Normal,
            grade, ChampionKind.Champion, modifiers, EliteAura.None, species.Name, Random.Shared);

        if (role is WrongRole.Manhunter or WrongRole.SlaveMerchant)
        {
            if (grade != EncounterGrade.Champion)
                Hue = 0x83EA;
            HairItemID = 0x203B;
            HairHue = 0x455;
            AddItem(new Boots(0x455) { Movable = false });
            AddItem(new LongPants(0x455) { Movable = false });
            if (role == WrongRole.Manhunter)
            {
                AddItem(new FancyShirt(0x497) { Movable = false });
                AddItem(new LeatherChest { Hue = 0x455, Movable = false });
                AddItem(new Crossbow { Movable = false });
            }
            else
                AddItem(new Robe(0x48E) { Movable = false });
        }
        SyncMage();
    }

    internal void SyncMage()
    {
        if (Role == WrongRole.Goegork)
            Blessed = WaitingMage;
    }

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon)
    {
        var role = _roles.GetValueOrDefault(dungeon.Id);
        bool human = dungeon.Body == 400;
        return new(dungeon.Family, false, false, role >= WrongRole.Wonk, false, true,
            human ? 2 : 3, 0, 0, 100, 0);
    }

    protected override int[] AdjustResistances(int[] resistances)
    {
        if (IsGuard)
            Array.Fill(resistances, 75);
        return resistances;
    }

    internal override bool IsEnemyIgnoringLeash(Mobile target) => !WaitingMage && base.IsEnemyIgnoringLeash(target);

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (!WaitingMage)
            base.Damage(amount, from, informMount, ignoreEvilOmen);
    }

    public override void OnCombatantChange()
    {
        base.OnCombatantChange();
        if (Combatant is not { } target || WaitingMage || !IsEnemyIgnoringLeash(target))
            return;

        if (Cook && !_opened)
        {
            _opened = true;
            _nextBark = Core.TickCount + 10000;
            WrongBarks.CookOpening(this);
        }
        _site?.Engage(this, target);
    }

    internal void AttackWithPack(Mobile target)
    {
        if (!Alive || Deleted || WaitingMage || target == null || target.Map != Map || !IsEnemyIgnoringLeash(target))
            return;
        if (Cook)
            RangePerception = HomeLeashRadius;
        if (Combatant is not { Alive: true, Deleted: false } existing || existing.Map != Map)
        {
            if (Cook && !InLOS(target))
                AggressiveAction(target, false);
            Combatant = target;
        }
        if (AIObject != null)
        {
            AIObject.Action = ActionType.Combat;
            AIObject.AITimer.Prod();
        }
    }

    public override void OnThink()
    {
        var target = Combatant;
        long now = Core.TickCount;
        if (WaitingMage)
        {
            Combatant = null;
            return;
        }
        if (_ward256 > 0)
            WarlockRitualVisuals.Renew(this, SpellEffectGraphics.PsychicWardShell, TimedEffects.Now);
        if (EliteCombat.Enemy(this, target, 18))
        {
            if (Cook && now >= _nextBark)
            {
                _nextBark = now + 10000;
                if (Utility.Random(100) < 10)
                    WrongBarks.CookCombat(this);
            }
            if (!Caster && CanCastEncounterBolt)
                BeginFirebolt(target);
            if (now >= _nextSpecial)
                TrySpecial(target, now);
        }
        base.OnThink();
    }

    private void TrySpecial(Mobile target, long now)
    {
        if (BusyWithSpecial || !ActionReady || !EliteCombat.CanAct(this) || !NamedControl.SpecialAllowed(this)
            || CreatureHitRecovery.Remaining(this) > 0)
            return;

        if (Role == WrongRole.Wonk && BeginRockThrow(target, ref _special, Utility.RandomList(_bodyParts)))
        {
            _nextSpecial = now + 4800;
            return;
        }
        if (Role == WrongRole.Rowdy && !InRange(target, 1) && EliteCombat.Enemy(this, target, 6))
        {
            _nextSpecial = now + 6500;
            StartAction(target);
            WrongCombat.Charge(this, target);
            return;
        }
        if (Role is not (WrongRole.Brute or WrongRole.Uruk or WrongRole.Glokurg) || !EliteCombat.Enemy(this, target, 1))
            return;

        _nextSpecial = now + 6500;
        StartAction(target);
        Direction = GetDirectionTo(target);
        int windup = Role == WrongRole.Brute ? 1450 : 520;
        if (Role == WrongRole.Brute)
            PublicOverheadMessage(MessageType.Emote, 0x35, false, "*raises both fists for a heavy swing*");
        ClassCombat.SendAnimation(this, MeleeAnimation, windup);
        Animate(MeleeAnimation, 5, 1, true, false, (byte)(Role == WrongRole.Brute ? 3 : 0));
        var map = Map;
        var origin = Location;
        Timer.StartTimer(TimeSpan.FromMilliseconds(windup), () =>
        {
            if (map != Map || origin != Location || !EliteCombat.CanAct(this) || !NamedControl.SpecialAllowed(this)
                || CreatureHitRecovery.Remaining(this) > 0 || !EliteCombat.Enemy(this, target, 1))
                return;
            if (Role == WrongRole.Uruk)
                WrongCombat.Zeal(this, target, map, origin, _spellGeneration);
            else
                SpecialHit(target, Role == WrongRole.Brute ? 2.35 : 1.1, smite: Role == WrongRole.Glokurg);
        }, out _special);
    }

    internal bool Active(Map map, int generation) =>
        Alive && !Deleted && !ReturningHome && Map == map && generation == _spellGeneration;

    internal void SpecialHit(Mobile target, double factor, bool smite = false)
    {
        if (!EliteCombat.Enemy(this, target, 1) || !EliteCombat.CanAct(this) || !NamedControl.SpecialAllowed(this)
            || CreatureHitRecovery.Remaining(this) > 0 || !smite && ClassStats.CheckHit(this, target, 0) != true)
            return;
        int before = target.Hits;
        Deal(target, (int)(Utility.RandomMinMax(CombatProfile.MinDamage, CombatProfile.MaxDamage) * factor), false);
        if (smite && target.Alive && target.Hits < before)
        {
            CombatStuns.ApplyEncounter(target, (int)SpellEffects(CombatAbility.Smite).Eval("calc2"));
            WrongCombat.Knockback(this, target);
        }
    }

    public override bool BeginFirebolt(Mobile target)
    {
        if (WaitingMage || ReturningHome || Core.TickCount < _nextSpell || _special.Running
            || !NamedControl.SpecialAllowed(this) || !base.BeginFirebolt(target))
            return false;
        _nextSpell = Core.TickCount + (Role == WrongRole.Manhunter ? 2300 : 4400);
        return true;
    }

    private static ReadOnlySpan<CombatAbility> Spells(WrongRole role) => role switch
    {
        WrongRole.SlaveMerchant => [CombatAbility.PsychicWard, CombatAbility.SigilLethargy, CombatAbility.EldritchBlast],
        WrongRole.Hatchling or WrongRole.Juvenile => [CombatAbility.MiasmaBolt],
        WrongRole.AdultDrake => [CombatAbility.MiasmaChain],
        WrongRole.BattleDrake => [CombatAbility.SigilLethargy, CombatAbility.MiasmaBolt],
        WrongRole.AssaultDrake => [CombatAbility.SigilLethargy, CombatAbility.MiasmaChain],
        WrongRole.Goegork => [CombatAbility.Abyss, CombatAbility.Apocalypse],
        WrongRole.Xoldred => [CombatAbility.FlameWave],
        WrongRole.Suldri => [CombatAbility.EldritchBlast],
        _ => []
    };

    protected override void ReleaseEncounterBolt(Mobile target)
    {
        if (Role == WrongRole.Manhunter)
        {
            WrongCombat.Crossbow(this, target, (_nextCast++ & 1) != 0, _spellGeneration);
            return;
        }

        var pool = Spells(Role);
        if (pool.IsEmpty)
            return;
        var ability = pool[_nextCast % pool.Length];
        _nextCast = (_nextCast + 1) % pool.Length;
        if (ability == CombatAbility.PsychicWard)
        {
            (_ward256, _wardStunFrames, _) = PsychicWard.Values(SpellEffects(ability));
            WarlockRitualVisuals.Attached(this, SpellEffectGraphics.PsychicWardCast, SpellEffectSounds.PsychicWardCast);
            WarlockRitualVisuals.Renew(this, SpellEffectGraphics.PsychicWardShell, TimedEffects.Now);
        }
        else
            WrongCombat.Cast(this, target, ability, _spellGeneration);
    }

    internal bool HasWard => _ward256 > 0;

    internal double AbsorbWard(double physical, Mobile attacker, bool melee)
    {
        if (_ward256 <= 0 || physical <= 0)
            return physical;
        var result = SourceAbsorptionPool.Take(_ward256, NamedElements.To256(physical));
        _ward256 = result.Remaining256;
        if (_ward256 == 0)
        {
            WarlockRitualVisuals.Clear(this, SpellEffectGraphics.PsychicWardShell);
            WarlockRitualVisuals.Attached(this, SpellEffectGraphics.PsychicWardBreak, SpellEffectSounds.PsychicWardBreak);
        }
        else if (result.Absorbed256 > 0)
            WarlockRitualVisuals.Attached(this, SpellEffectGraphics.PsychicWardCatch, SpellEffectSounds.PsychicWardCatch);
        if (melee && result.Absorbed256 > 0 && EliteCombat.Enemy(this, attacker, 1))
            CombatStuns.ApplyEncounter(attacker, _wardStunFrames);
        return result.Damage256 / 256.0;
    }

    protected override void Deal(Mobile target, int damage, bool fire)
    {
        int before = target.Hits;
        if (Role == WrongRole.Suldri)
            EliteCombat.Damage(this, target, cold: damage, weapon: true);
        else
            base.Deal(target, damage, fire);
        if (!target.Alive || target.Hits >= before)
            return;

        if (Body == 1 && Utility.Random(100) < 10)
            NamedCombat.ApplyCrushing(target, false);
        if (!target.Alive)
            return;
        if (IsGuard && Utility.Random(100) < 50)
            TimedEffects.Bleed(this, target, NamedCombat.OpenWoundsDamage(Basis.Level, target, false), TimedEffects.Now);
        else if (Role == WrongRole.Ruffian)
            WrongCombat.Knockback(this, target);
        else if (Role == WrongRole.Thug && Utility.Random(100) < 50)
        {
            var window = _stunWindows.GetOrCreateValue(target);
            if (Core.TickCount >= window.Until && CombatStuns.TryApplyEncounter(target, 25))
                window.Until = Core.TickCount + 2000;
        }
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        base.OnDamage(amount, from, willKill);
        if (IsGuard && !willKill && amount > 0)
            _site?.Injured(this, amount);
    }

    internal override void StopCombatActions()
    {
        _spellGeneration++;
        _special.Cancel();
        if (ReturningHome)
        {
            ClearWard();
            _opened = false;
            if (Cook)
                RangePerception = 14;
        }
        base.StopCombatActions();
    }

    private void ClearWard()
    {
        if (_ward256 <= 0)
            return;
        _ward256 = 0;
        WarlockRitualVisuals.Clear(this, SpellEffectGraphics.PsychicWardShell);
    }

    public override void OnDelete()
    {
        ClearWard();
        base.OnDelete();
    }

    public override void OnDeath(Container corpse)
    {
        ClearWard();
        if (Cook)
            WrongBarks.CookDeath(this);
        else if (Role == WrongRole.Goegork)
            WrongBarks.Say(this, "death", "Darlings… I’m coming…");
        _site?.Died(this);
        base.OnDeath(corpse);
    }
}
