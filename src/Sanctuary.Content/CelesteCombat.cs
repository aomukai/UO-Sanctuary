// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public partial class Celeste
{
    private const double CombatPace = 0.18, ChargePace = 0.085;
    private TimerExecutionToken _strike;
    private DateTime _nextSmite, _nextCharge, _chargeEnds, _nextAura, _shieldRefresh;
    private Point3D _chargeTrail;
    private Mobile _chargingTarget;

    private BaseShield ActiveShield => GearRecovered && !IsDeadPet && FindItemOnLayer(Layer.TwoHanded) is BaseShield shield
        && !ClassGear.Broken(shield) && ClassGear.State(shield) is not { Identified: false } ? shield : null;

    internal int DefenseBonus => ActiveShield != null && MercenaryCare.CompanionLevel(this) is { } level
        ? HolyShields.DefensePercent(CompanionBuild.Values(CombatAbility.HolyShield, level)) : 0;

    internal int BlockChance => ActiveShield is { } shield && MercenaryCare.CompanionLevel(this) is { } level
        ? Math.Clamp((ClassGear.Base(shield)?.Block ?? 0) + ClassGear.ClassBlock(DiabloClass.Paladin)
            + HolyShields.BlockBonus(CompanionBuild.Values(CombatAbility.HolyShield, level))
            + CompanionBuild.Bonus(this, "block"), 0, 75) : 0;

    internal void PresentBlock()
    {
        SourceBlocking.Present(this, false);
        PaladinDefenseVisuals.ShieldMoment(this, SpellEffectGraphics.HolyShieldBlock, SpellEffectSounds.HolyShieldBlock,
            7, TimedEffects.Now);
    }

    private bool CanFight(PlayerMobile owner, Mobile target) =>
        GearRecovered && Alive && !IsDeadPet && !Frozen && !Paralyzed
        && ControlOrder is OrderType.Attack or OrderType.Guard && Combatant == target
        && MercenaryCare.WithinCompanionLeash(owner, this, target)
        && ClassCombatHooks.ActionAllowed?.Invoke(this) != false && CreatureHitRecovery.Remaining(this) == 0
        && ClassCombat.IsHostile(owner, target) && CanBeHarmful(target, false) && Math.Abs(Z - target.Z) < 16
        && InLOS(target);

    private void ThinkCombat(PlayerMobile owner, DateTime now)
    {
        if (owner.Alive && !owner.Mounted && owner.Map == Map && MercenaryCare.CompanionLevel(this) is { } level)
        {
            if (now >= _nextAura)
            {
                var conviction = CompanionBuild.Values(CombatAbility.Conviction, level);
                _nextAura = now.AddMilliseconds(Math.Max(5, conviction.Eval("perdelay")) * ActionTiming.FrameMs);
                PaladinConviction.Pulse(this, owner, conviction, now);
            }

            if (ActiveShield != null && now >= _shieldRefresh
                && PaladinAuraVisuals.Attach(this, SpellEffectGraphics.HolyShieldShell, now.AddSeconds(3), now))
                _shieldRefresh = now.AddSeconds(1);
        }

        if (_chargingTarget != null)
        {
            if (now >= _chargeEnds || !CanFight(owner, _chargingTarget))
                EndCharge();
            else if (_chargeTrail != Location)
            {
                PaladinMeleeVisuals.ChargeStep(this, _chargeTrail);
                _chargeTrail = Location;
            }
        }

        if (_chargingTarget == null && !_strike.Running && now >= _nextCharge
            && Combatant is Mobile target && !InRange(target, 1) && InRange(target, 7)
            && Weapon is BaseBashing weapon && !ClassGear.Broken(weapon) && CanFight(owner, target))
        {
            _chargingTarget = target;
            _chargeTrail = Location;
            _chargeEnds = now.AddSeconds(1.4);
            _nextCharge = now.AddSeconds(4);
            ActiveMoveSpeed = ChargePace;
            PaladinMeleeVisuals.Swing(this, CombatAbility.Charge);
        }
    }

    private void EndCharge()
    {
        _chargingTarget = null;
        ActiveMoveSpeed = CombatPace;
    }

    internal TimeSpan BeginMelee(Mobile target)
    {
        if (_strike.Running || ControlMaster is not PlayerMobile owner || !CanFight(owner, target)
            || !InRange(target, 1) || Weapon is not BaseWeapon weapon)
            return TimeSpan.FromMilliseconds(100);

        var now = TimedEffects.Now;
        var shield = ActiveShield;
        var smite = PaladinSmite.Shield(shield);
        var ability = _chargingTarget == target ? CombatAbility.Charge
            : smite != null && now >= _nextSmite ? CombatAbility.Smite
            : weapon is Fists || ClassGear.Broken(weapon) ? CombatAbility.BasicAttack : CombatAbility.Zeal;
        if (ability != CombatAbility.Smite && ClassGear.Broken(weapon))
            return TimeSpan.FromMilliseconds(100);

        EndCharge();
        int level = MercenaryCare.CompanionLevel(this) ?? 1;
        var values = ability == CombatAbility.BasicAttack ? null : CompanionBuild.Values(ability, level);
        var timing = CompanionBuild.Timing(this, DiabloClass.Paladin, ability, weapon);
        int strikes = ability == CombatAbility.Zeal ? Math.Clamp((int)values.Eval("calc1"), 1, 5) : 1;
        if (ability == CombatAbility.Smite)
            _nextSmite = now.AddSeconds(3);

        Direction = GetDirectionTo(target);
        weapon.PlaySwingAnimation(this);
        if (ability is CombatAbility.Smite or CombatAbility.Zeal)
            PaladinMeleeVisuals.Swing(this, ability);

        Timer.StartTimer(TimeSpan.FromMilliseconds(timing.EffectMs),
            () => Strike(owner, target, weapon, shield, smite, ability, values, level, timing, strikes), out _strike);
        return TimeSpan.FromMilliseconds(timing.DurationMs * strikes);
    }

    private void Strike(PlayerMobile owner, Mobile target, BaseWeapon weapon, BaseShield shield, SmiteShield smite,
        CombatAbility ability, SkillValues values, int level, ActionTiming timing, int strikes)
    {
        _strike = default;
        if (!CanFight(owner, target) || !InRange(target, 1) || Weapon != weapon
            || (ability == CombatAbility.Smite ? ActiveShield != shield : ClassGear.Broken(weapon)))
            return;

        DoHarmful(target);
        int attack = CompanionBuild.Bonus(this, "att%")
            + (ability is CombatAbility.Charge or CombatAbility.Zeal ? (int)values.Formula("toht") : 0);
        if (ClassStats.CheckHit(this, target, attack, guaranteed: ability == CombatAbility.Smite) == true)
        {
            var point = target.Location;
            var map = target.Map;
            var parts = SkillCombat.MitigatePrepared(this, target, new(StrikeDamage(ability, values, weapon, smite, level), 0, 0, 0, 0));
            var settled = DirectDamageParts.From(parts);
            SkillHealth.TypedDamage(this, target, checked((int)settled.Total), settled);
            if (ability != CombatAbility.BasicAttack)
                PaladinMeleeVisuals.Land(this, point, map, ability);
            if (target is BaseCreature { Alive: true } creature)
            {
                if (ability == CombatAbility.Smite)
                    CombatStuns.Apply(creature, (int)values.Eval("calc2"));
                else if (ability == CombatAbility.Charge)
                    SourceKnockbacks.Begin(this, creature);
            }
        }

        if (strikes > 1 && CanFight(owner, target))
        {
            Timer.StartTimer(TimeSpan.FromMilliseconds(timing.DurationMs - timing.EffectMs), () =>
            {
                _strike = default;
                if (CanFight(owner, target) && InRange(target, 1) && Weapon == weapon)
                {
                    weapon.PlaySwingAnimation(this);
                    PaladinMeleeVisuals.Swing(this, ability);
                    Timer.StartTimer(TimeSpan.FromMilliseconds(timing.EffectMs),
                        () => Strike(owner, target, weapon, shield, smite, ability, values, level, timing, strikes - 1), out _strike);
                }
            }, out _strike);
        }
    }

    private double StrikeDamage(CombatAbility ability, SkillValues values, BaseWeapon weapon, SmiteShield smite, int level)
    {
        if (ability == CombatAbility.Smite)
        {
            var holy = HolyShields.SmiteBonus(CompanionBuild.Values(CombatAbility.HolyShield, level).Rank);
            int bonus = ClassStats.WeaponStatPercent(Str + CompanionBuild.Bonus(this, "str"), Dex + CompanionBuild.Bonus(this, "dex"),
                smite.StrengthBonus, smite.DexterityBonus) + CompanionBuild.Bonus(this, "dmg%") + (int)values.Eval("calc1");
            var (min, max) = PaladinSmite.Endpoints256(smite.MinDamage * 256 + holy.Min256, smite.MaxDamage * 256 + holy.Max256,
                bonus, CompanionBuild.Bonus(this, "normal-damage"));
            return (min + Utility.Random(Math.Max(1, max - min))) / 256.0;
        }

        int skillDamage = ability switch
        {
            CombatAbility.Charge => (int)values.Eval("calc1"),
            CombatAbility.Zeal => (int)values.Eval("calc2"),
            _ => 0
        };
        var (minimum, maximum, factor) = CompanionBuild.WeaponDamage(this, weapon, level, skillDamage);
        return Utility.RandomMinMax(minimum, maximum) * factor / 100.0;
    }

    public override void OnAfterDelete()
    {
        _strike.Cancel();
        base.OnAfterDelete();
    }
}
