// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record NecromancerMotion(uint Target, bool Flee, int OriginX, int OriginY);
public sealed record NecromancerCurse(CombatAbility Ability, uint Source, int Rank, DateTime UntilUtc, int OriginX, int OriginY,
    NecromancerMotion Motion = null, TerrorAi Terror = null);
public sealed record NecromancerState(NecromancerCurse Curse = null, int BoneArmor = 0, int ManaFraction = 0);

public sealed class NecromancerEffectProfiles : GenericPersistence
{
    private Dictionary<Serial, NecromancerState> _states = new();
    public NecromancerEffectProfiles() : base("SanctuaryNecromancerEffects", 112) { }
    public NecromancerState Find(Mobile actor) => actor is { Alive: true, Deleted: false }
        ? _states.GetValueOrDefault(actor.Serial) : null;
    public void Store(Mobile actor, NecromancerState state)
    {var before=_states.GetValueOrDefault(actor.Serial);_states[actor.Serial]=state;SourceTerror.Changed(actor,before?.Curse,state.Curse);}
    public void Clear(Mobile actor)
    {if(_states.Remove(actor.Serial,out var old)){SourceTerror.Changed(actor,old.Curse,null);}}
    public override void Serialize(IGenericWriter writer)
    {
        Prune(); writer.Write(0); writer.Write(_states.Count);
        foreach (var (serial, state) in _states) { writer.Write(serial.Value); writer.Write(JsonSerializer.Serialize(state)); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Necromancer state version."); }
        int count = reader.ReadInt(); if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid Necromancer state count."); }
        var states = new Dictionary<Serial, NecromancerState>();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); var state = JsonSerializer.Deserialize<NecromancerState>(reader.ReadString());
            if (!serial.IsMobile || state == null || state.BoneArmor < 0 || state.ManaFraction is < 0 or > 255
                || state.Curse is { } curse && (!NecromancerEffects.IsCurse(curse.Ability) || curse.Rank is < 1 or > 255 || curse.Terror is{} terror && (curse.Ability!=CombatAbility.Terror || terror.Skill!=CombatAbility.Howl || terror.LaunchRank<1 || terror.Map<0 || terror.Map>=Map.Maps.Length || terror.VelocityPercent is <0 or >120))
                || !states.TryAdd(serial, state)) { throw new InvalidDataException("Invalid Necromancer state."); }
        }
        _states = states;
    }
    public void Prune()
    {
        var removed = new List<Serial>();
        foreach (var (serial, state) in _states)
        { if (World.FindMobile(serial) is not { Alive: true, Deleted: false }) { removed.Add(serial); } }
        foreach (var serial in removed) { _states.Remove(serial); }
    }
    public override void PostDeserialize(){Prune();SourceTerror.AfterLoad(_states);}
}

public static class NecromancerEffects
{
    public static NecromancerEffectProfiles Profiles { get; private set; }
    public static void Configure()
    {
        Profiles = new(); EventSink.WorldSave += Profiles.Prune;
        ClassCombatHooks.SkillResistedDamage = SkillCombat.ResistedDamage;
        ClassCombatHooks.SkillWeaponHit = WeaponHit;
    }
    public static bool IsCurse(CombatAbility a) => a is CombatAbility.AmplifyDamage or CombatAbility.DimVision
        or CombatAbility.Weaken or CombatAbility.IronMaiden or CombatAbility.Terror or CombatAbility.Confuse
        or CombatAbility.LifeTap or CombatAbility.Attract or CombatAbility.Decrepify or CombatAbility.LowerResist;
    public static NecromancerCurse Curse(Mobile actor) => Profiles?.Find(actor)?.Curse is { } curse && curse.UntilUtc > TimedEffects.Now ? curse : null;
    internal static SkillValues Values(NecromancerCurse curse) => new(SkillMechanics.For(curse.Ability),
        key => key == SkillMechanics.For(curse.Ability).SourceKey ? curse.Rank : 0, _ => 0, 1);
    public static bool AiCurse(Mobile target) => target is BaseCreature { Alive: true, Deleted: false, Controlled: false, Summoned: false }
        && !NamedCombat.Elite(target) && target is not BaseVendor;
    public static int DifficultyDivisor(Mobile target) => target is BaseCreature creature ? MonsterVariants.Difficulty(creature) switch
    { MonsterDifficulty.Nightmare => 2, MonsterDifficulty.Hell => 4, _ => 1 } : 1;
    public static bool Apply(PlayerMobile owner, Mobile target, CombatAbility ability, int rank)
    {
        if (!IsCurse(ability) || !ClassCombat.IsHostile(owner, target) || Curse(target)?.Ability == CombatAbility.Attract
            || ability == CombatAbility.DimVision && Curse(target)?.Ability == CombatAbility.Confuse) { return false; }
        bool ai = ability is CombatAbility.DimVision or CombatAbility.Terror or CombatAbility.Confuse or CombatAbility.Attract;
        if (ai && !AiCurse(target)) { return false; }
        var values = SkillMechanics.Values(owner, ability);
        int frames = (int)values.Eval("auralencalc");
        if (ai) { frames /= DifficultyDivisor(target); }
        var state = Profiles.Find(target) ?? new();
        var motion = ability == CombatAbility.DimVision && target is BaseCreature blinded ? CaptureMotion(blinded) : null;
        Profiles.Store(target, state with { Curse = new(ability, owner.Serial.Value, rank,
            TimedEffects.Now.AddMilliseconds(frames * 40), owner.X, owner.Y, motion) });
        if (TimedEffects.TryActor(target, out var actor) && actor.Control is { } control)
        { actor.Control = control with { BlindUntil = default, FearUntil = default }; }
        if (target is BaseCreature creature)
        { creature.AIObject?.ResetApproachState(); creature.AIObject?.ClearMoveIntent(); creature.AIObject?.AITimer.Prod(); }
        target.FixedEffect(0x375A, 10, 15, 0x455, 0); // Placeholder curse marker.
        owner.DoHarmful(target); return true;
    }
    private static NecromancerMotion CaptureMotion(BaseCreature creature)
    {
        var previous = Curse(creature);
        if (previous?.Ability == CombatAbility.DimVision) { return previous.Motion; }
        if (previous?.Ability == CombatAbility.Terror) { return new(0, true, previous.OriginX, previous.OriginY); }
        if (NamedControl.State(creature) is { } control && control.FearUntil > TimedEffects.Now)
        { return new(0, true, control.FearX, control.FearY); }
        return creature.AIObject?.Action == ActionType.Combat && creature.Combatant is { Alive: true, Deleted: false } target
            && target.Map == creature.Map ? new(target.Serial.Value, false, 0, 0) : null;
    }
    public static void ClearOrdinaryCurse(Mobile actor)
    {
        if (Profiles.Find(actor) is { } state && Curse(actor)?.Ability != CombatAbility.Attract)
        { Profiles.Store(actor, state with { Curse = null }); }
    }
    public static int Resistance(Mobile target, string element, int baseline,int? immunityBaseline=null,bool deferCap=false)
    {
        if (Curse(target) is not { } curse) { return baseline; }
        var values = Values(curse);
        int delta = curse.Ability switch
        {
            CombatAbility.AmplifyDamage when element == "physical" => -(int)values.Param(5),
            CombatAbility.Decrepify when element == "physical" => values.Param(5),
            CombatAbility.LowerResist when element is "fire" or "cold" or "ltng" or "pois" => -values.Diminishing(5, 6),
            _ => 0
        };
        // Immunity-breaking curses run at one fifth effectiveness against an immune monster.
        if (target is BaseCreature and not BaseHire && (immunityBaseline ?? baseline) >= 100) { delta /= 5; }
        // Player caps apply after pierce; retain overcapped resistance until that final step.
        return target is PlayerMobile || deferCap ? baseline+delta : Math.Clamp(baseline + delta, -100, 200);
    }
    public static int OutgoingPhysicalPercent(Mobile actor) => Curse(actor) is { } curse ? curse.Ability switch
    {
        CombatAbility.Weaken => 100 + Values(curse).Linear(5, 6),
        CombatAbility.Decrepify => 50,
        _ => 100
    } : 100;
    public static int Slow(Mobile actor) => Curse(actor)?.Ability == CombatAbility.Decrepify ? 50 : 0;
    public static bool TargetAllowed(Mobile actor, Mobile target)
    {
        if (Curse(actor) is not { } curse) { return true; }
        return curse.Ability switch
        {
            CombatAbility.Terror => curse.Terror!=null && SourceTerror.TargetAllowed(actor,target),
            CombatAbility.DimVision => target != null && actor.Map == target.Map && actor.InRange(target, 1),
            _ => true
        };
    }
    public static bool Think(BaseCreature creature)
    {
        if (creature.AIObject is not { } ai) { return false; }
        if (Profiles.Find(creature) is { Curse: { } elapsed } state && elapsed.UntilUtc <= TimedEffects.Now)
        {
            Profiles.Store(creature, state with { Curse = null });
            if (elapsed.Ability is CombatAbility.DimVision or CombatAbility.Terror or CombatAbility.Confuse or CombatAbility.Attract)
            {
                creature.Combatant = null; creature.FocusMob = null;
                ai.ResetApproachState(); ai.ClearMoveIntent(); ai.Action = ActionType.Wander;
                if (creature is GraveyardUndead undead) { undead.InterruptForRecovery(); }
            }
        }
        var curse = Curse(creature);
        if (curse?.Ability == CombatAbility.Terror)
        {
            if(curse.Terror!=null){return SourceTerror.Think(creature,curse);}
            creature.Combatant = null; creature.FocusMob = null;
            var origin = new Point3D(curse.OriginX, curse.OriginY, creature.Z);
            ai.DoMove((Direction)(((int)creature.GetDirectionTo(origin) + 4) & 7)); return true;
        }
        if (curse?.Ability == CombatAbility.DimVision)
        {
            if (curse.Motion is { } motion)
            {
                var target = World.FindMobile((Serial)motion.Target);
                if (motion.Flee || target is { Alive: true, Deleted: false } && target.Map == creature.Map)
                {
                    var destination = motion.Flee ? new Point3D(motion.OriginX, motion.OriginY, creature.Z) : target.Location;
                    if (!motion.Flee && creature.InRange(target, 1)) { creature.Combatant = target; return true; }
                    var direction = creature.GetDirectionTo(destination);
                    if (motion.Flee) { direction = (Direction)(((int)direction + 4) & 7); }
                    if (ai.DoMoveDirect(direction) != MoveResult.Blocked) { return true; }
                }
                Profiles.Store(creature, Profiles.Find(creature) with { Curse = curse with { Motion = null } });
                creature.Combatant = null; creature.FocusMob = null; ai.ClearMoveIntent();
            }
            if (!TargetAllowed(creature, creature.Combatant))
            {
                creature.Combatant = null; creature.FocusMob = null; ai.ClearMoveIntent();
                if (ai.AcquireFocusMob(1, creature.FightMode, false, false, true)) { creature.Combatant = creature.FocusMob; }
            }
            return true;
        }
        if (curse?.Ability is not (CombatAbility.Confuse or CombatAbility.Attract))
        {
            // An Attract target draws nearby ordinary monsters even while they carry another curse.
            if (!AiCurse(creature)) { return false; }
            foreach (var mobile in creature.Map.GetMobilesInRange<BaseCreature>(creature.Location, 5))
            {
                if (mobile != creature && Curse(mobile) is { Ability: CombatAbility.Attract } attraction
                    && creature.GetDistanceToSqrt(mobile) <= SkillGeometry.Tiles(Values(attraction).Eval("aurarangecalc")) && creature.InLOS(mobile))
                { creature.Combatant = mobile; return ai.DoActionCombat(); }
            }
            return false;
        }
        if (curse.Ability == CombatAbility.Attract) { return false; }
        Mobile nearest = null; double distance = double.MaxValue;
        foreach (var candidate in creature.Map.GetMobilesInRange<BaseCreature>(creature.Location, 12))
        {
            if (candidate == creature || !AiCurse(candidate) || !creature.CanSee(candidate) || !creature.InLOS(candidate)) { continue; }
            double next = creature.GetDistanceToSqrt(candidate);
            if (next < distance) { nearest = candidate; distance = next; }
        }
        if (nearest == null) { return false; }
        creature.Combatant = nearest; return ai.DoActionCombat();
    }
    public static int Armor(Mobile actor) => Profiles?.Find(actor)?.BoneArmor ?? 0;
    public static double AbsorbPhysical(Mobile target, double damage)
    {
        if (damage <= 0 || Profiles?.Find(target) is not { BoneArmor: > 0 } state) { return damage; }
        int absorb = Math.Min(state.BoneArmor, (int)Math.Ceiling(damage));
        Profiles.Store(target, state with { BoneArmor = state.BoneArmor - absorb });
        return Math.Max(0, damage - absorb);
    }
    public static void BoneArmor(PlayerMobile owner, SkillValues values)
    {
        int amount = (int)Math.Clamp(values.Eval("aurastatcalc1") / 256, 0, int.MaxValue);
        Profiles.Store(owner, (Profiles.Find(owner) ?? new()) with { BoneArmor = amount });
        owner.FixedEffect(0x375A, 10, 15, 0x481, 0); // Placeholder shield marker.
    }
    public static int ManaBalance256(Mobile owner) => owner.Mana * 256 + (Profiles.Find(owner)?.ManaFraction ?? 0);
    internal static void SetMana256(Mobile owner,int amount256)
    {
        amount256=Math.Clamp(amount256,0,owner.ManaMax*256);var state=Profiles.Find(owner)??new();
        owner.Mana=amount256/256;Profiles.Store(owner,state with{ManaFraction=amount256%256});
    }
    public static bool CanPay(PlayerMobile owner, int cost256) => ManaBalance256(owner) >= cost256;
    public static void AddMana(PlayerMobile owner, int amount256)
    {
        if (amount256<=0 || owner is not { Alive:true,Deleted:false }) { return; }
        var state=Profiles.Find(owner) ?? new();
        long balance=Math.Min((long)owner.ManaMax*256,(long)ManaBalance256(owner)+amount256);
        owner.Mana=(int)(balance/256);Profiles.Store(owner,state with { ManaFraction=(int)(balance%256) });
    }
    public static bool Pay(PlayerMobile owner, int cost256)
    {
        if (!CanPay(owner, cost256)) { return false; }
        var state = Profiles.Find(owner) ?? new();
        long balance = (long)owner.Mana * 256 + state.ManaFraction - cost256;
        owner.Mana = (int)(balance / 256); Profiles.Store(owner, state with { ManaFraction = (int)(balance % 256) }); return true;
    }
    public static void WeaponHit(Mobile attacker, Mobile target, int physicalLoss, bool ranged)
    {
        SorceressEffects.MeleeHit(attacker, target, physicalLoss, ranged);
        if (physicalLoss <= 0) { return; }
        if (Curse(target) is { Ability: CombatAbility.LifeTap } tap && attacker is { Alive: true, Deleted: false })
        { attacker.Hits += physicalLoss * Values(tap).Linear(5, 6) / 100; }
        if (!ranged && Curse(attacker) is { Ability: CombatAbility.IronMaiden } maiden && attacker.Alive && !attacker.Deleted)
        {
            int reflect = physicalLoss * Values(maiden).Linear(5, 6) / 100;
            SkillCombat.Hit(target, attacker, reflect, "physical", false);
        }
    }
}
