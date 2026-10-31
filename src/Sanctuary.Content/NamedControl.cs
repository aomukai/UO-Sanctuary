// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record NamedControlState(DateTime FreezeUntil, DateTime BlindUntil, DateTime FearUntil,
    uint FearSource, int FearX, int FearY, SourceFreezeState Freeze = null);

public static class NamedControl
{
    public static void Configure()
    {
        ClassCombatHooks.ControlThink = creature => Think(creature) || NecromancerEffects.Think(creature);
        ClassCombatHooks.TargetAllowed = (actor, target) => !PaladinConversion.Friendly(actor,target)
            && TargetAllowed(actor, target) && NecromancerEffects.TargetAllowed(actor, target);
        ClassCombatHooks.RunningStaminaCost = RunningCost;
    }
    internal static NamedControlState State(Mobile owner) => owner is { Alive: true, Deleted: false }
        && TimedEffects.TryActor(owner, out var actor) ? actor.Control : null;
    private static NamedControlState Empty => new(default, default, default, 0, 0, 0);
    public static int FrozenRemaining(Mobile owner) => State(owner) is { } state
        ? (int)Math.Clamp((state.FreezeUntil - TimedEffects.Now).TotalMilliseconds, 0, 20000) : 0;
    public static bool Blind(Mobile owner) => State(owner)?.BlindUntil > TimedEffects.Now
        || NecromancerEffects.Vision(NecromancerEffects.Curse(owner));
    public static bool Fear(Mobile owner) => State(owner)?.FearUntil > TimedEffects.Now;
    public static bool SpecialAllowed(Mobile owner) => FrozenRemaining(owner) == 0 && !FreezeStates.Waiting(owner) && !Blind(owner) && !Fear(owner)
        && NecromancerEffects.Curse(owner)?.Ability is not (CombatAbility.DimVision or CombatAbility.Terror)
        && SourceTerror.State(owner)?.Skill!=CombatAbility.Taunt;
    public static bool TargetAllowed(Mobile owner, Mobile target) => FrozenRemaining(owner) == 0 && !FreezeStates.Waiting(owner) && !Fear(owner)
        && (!Blind(owner) || SourceBlind.AcquiringTarget(owner) || target != null && owner.Map == target.Map && owner.InRange(target, 1));
    private static int DifficultyDivisor(BaseCreature creature) => MonsterVariants.Difficulty(creature) switch
    { MonsterDifficulty.Nightmare => 2, MonsterDifficulty.Hell => 4, _ => 1 };
    private static bool AiTarget(Mobile target) => target is BaseCreature { Controlled: false, Summoned: false, Alive: true, Deleted: false }
        && !NamedCombat.Elite(target) && target is not BaseVendor;
    internal static void Interrupt(BaseCreature target)
    {
        target.AIObject?.ResetApproachState(); target.AIObject?.ClearMoveIntent();
        if (target is GraveyardUndead undead) { undead.InterruptForRecovery(); }
        if (target.Spell is Server.Spells.Spell spell) { spell.Disturb(Server.Spells.DisturbType.Hurt); }
        target.AIObject?.AITimer.Prod();
    }
    public static int FreezeFrames(int attackerLevel, int targetLevel, int bonus, bool ranged, int roll)
    {
        int chance = 5 * (4 * (bonus - 1) + attackerLevel - (ranged ? 6 : 0) - targetLevel + 10);
        if (ranged) { chance /= 3; }
        int difference = Math.Clamp(chance, 0, 100) - roll;
        return difference > 0 ? Math.Clamp(2 * difference + 25, 25, 250) : 0;
    }
    public static bool Freeze(PlayerMobile source, Mobile target, int bonus, bool ranged, Random random)
    {
        if (bonus <= 0 || target is not { Alive: true, Deleted: false } || target.ColdResistance >= 100) { return false; }
        int frames = FreezeFrames(ClassStats.Level(source), ClassStats.Level(target), bonus, ranged, random.Next(100));
        if (frames == 0) { return false; }
        int duration = frames * ActionTiming.FrameMs;
        if (target is not BaseCreature creature || NamedCombat.Elite(target) || creature.Controlled)
        { return GearEffects.ApplyChill(target, Core.TickCount, duration, source); }
        var row = CreatureProfiles.For(creature);
        if (row.Traits.ChillEffectiveness == 0 || row.SourceStats.TryGetValue("coldeffect", out var cold)
            && int.TryParse(cold, out int effect) && effect >= 0) { return false; }
        int resistance = NamedElements.Resistance(source, target, "cold", target.ColdResistance);
        duration = duration * Math.Clamp(100 - resistance, 0, 200) / (100 * DifficultyDivisor(creature));
        if (duration <= 0) { return false; }
        var now = TimedEffects.Now; TimedEffects.Settle(target, now);
        if (!target.Alive || target.Deleted) { return false; }
        var actor = TimedEffects.Actor(target, now); var state = actor.Control ?? Empty;
        actor.Control = state with { FreezeUntil = new(Math.Max(state.FreezeUntil.Ticks, now.AddMilliseconds(duration).Ticks), DateTimeKind.Utc) };
        Interrupt(creature); target.FixedEffect(0x374A, 10, 15, 0x480, 0); return true;
    }
    public static bool FreezeSkill(PlayerMobile source, Mobile target, int durationMs)
        => FreezeStates.Apply(source,target,durationMs/40,Core.TickCount);
    public static int BlindLevel(int attackerLevel, int targetLevel, int bonus, bool ranged, int roll)
    {
        int chance = 5 * (attackerLevel + 4 * bonus - targetLevel + 6);
        if (ranged && chance > 0) { chance /= 3; }
        int difference = Math.Clamp(chance, 1, 99) - roll;
        return difference > 0 ? Math.Clamp(difference / 5 + 1, 1, 20) : 0;
    }
    public static bool ApplyBlind(PlayerMobile source, Mobile target, int bonus, bool ranged, Random random)
    {
        if (bonus <= 0 || !AiTarget(target) || NecromancerEffects.Curse(target)?.Ability is CombatAbility.Attract or CombatAbility.Confuse) { return false; }
        int level = BlindLevel(ClassStats.Level(source), ClassStats.Level(target), bonus, ranged, random.Next(100));
        return ApplyBlindLevel(target, level);
    }
    // Raven's monprop supplies a signed -1. Event09 accepts any nonzero value
    // and draws before the downstream curse eligibility check. Keep the
    // existing player/item positive-bonus entry point unchanged.
    internal static bool ApplySummonBlind(BaseCreature source, Mobile target, int bonus, bool ranged, Func<int> roll)
    {
        if(source is not{Alive:true,Deleted:false,Controlled:true,ControlMaster:PlayerMobile}
            || target is not BaseCreature{Alive:true,Deleted:false} || bonus==0){return false;}
        int draw=roll();if(draw is <0 or >=100){throw new ArgumentOutOfRangeException(nameof(roll));}
        int level=BlindLevel(ClassStats.Level(source),ClassStats.Level(target),bonus,ranged,draw);
        if(!AiTarget(target) || NecromancerEffects.Curse(target)?.Ability is CombatAbility.Attract or CombatAbility.Confuse){return false;}
        return ApplyBlindLevel(target,level);
    }
    private static bool ApplyBlindLevel(Mobile target,int level)
    {
        if (level == 0) { return false; }
        var creature = (BaseCreature)target; var now = TimedEffects.Now; TimedEffects.Settle(target, now);
        if (!target.Alive || target.Deleted) { return false; }
        var actor = TimedEffects.Actor(target, now);
        // Vision and fear occupy one AI curse lane; neither replaces independent item slow or cold freeze.
        actor.Control = (actor.Control ?? Empty) with { BlindUntil = now.AddMilliseconds((175 + 50 * (level - 1)) * 40 / DifficultyDivisor(creature)), FearUntil = default };
        NecromancerEffects.ClearOrdinaryCurse(target);
        Interrupt(creature); target.FixedEffect(0x375A, 10, 15, 0x455, 0); return true;
    }
    public static bool ApplyFear(PlayerMobile source, Mobile target, int chance, Random random)
    {
        if (chance <= 0 || !AiTarget(target) || NecromancerEffects.Curse(target)?.Ability == CombatAbility.Attract || random.Next(128) >= chance) { return false; }
        var now = TimedEffects.Now; TimedEffects.Settle(target, now);
        if (!target.Alive || target.Deleted) { return false; }
        var actor = TimedEffects.Actor(target, now);
        actor.Control = (actor.Control ?? Empty) with { FearUntil = now.AddMilliseconds(20 * 40), BlindUntil = default,
            FearSource = source.Serial.Value, FearX = source.X, FearY = source.Y };
        NecromancerEffects.ClearOrdinaryCurse(target);
        Interrupt((BaseCreature)target); target.FixedEffect(0x375A, 10, 15, 0x21, 0); return true;
    }
    public static bool Think(BaseCreature creature)
    {
        if (creature.AIObject is not { } ai || State(creature) is not { } state) { return false; }
        if (FrozenRemaining(creature) > 0 || FreezeStates.Waiting(creature)) { return true; }
        if (state.FearUntil > TimedEffects.Now)
        {
            var source = World.FindMobile((Serial)state.FearSource);
            var point = source is { Deleted: false } && source.Map == creature.Map ? source.Location : new Point3D(state.FearX, state.FearY, creature.Z);
            creature.Combatant = null; creature.FocusMob = null;
            var direction = (Direction)(((int)creature.GetDirectionTo(point) + 4) & 7);
            ai.DoMove(direction); return true;
        }
        if (state.BlindUntil > TimedEffects.Now)
        {
            if (creature.Combatant is not { Alive: true, Deleted: false } target || !TargetAllowed(creature, target))
            {
                creature.Combatant = null; creature.FocusMob = null; ai.ClearMoveIntent();
                if (ai.AcquireFocusMob(1, creature.FightMode, false, false, true))
                { creature.Combatant = creature.FocusMob; }
            }
            return true;
        }
        return false;
    }
    public static int RunningCost(Mobile owner, int cost)
    {
        if(ChargeMotion.Active(owner)){return 0;}
        if (owner is not PlayerMobile player || !ClassCombat.Enabled(player) || cost <= 0) { return cost; }
        int reduction = ClassGear.Bonus(player, "stamdrain");
        if (reduction <= 0) { return cost; }
        var actor = TimedEffects.Actor(player, TimedEffects.Now);
        long units = (long)cost * Math.Max(1, 256 * (100 - Math.Min(100, reduction)) / 100) + actor.RunningRemainder;
        actor.RunningRemainder = (int)(units % 256); return (int)Math.Min(int.MaxValue, units / 256);
    }
}
