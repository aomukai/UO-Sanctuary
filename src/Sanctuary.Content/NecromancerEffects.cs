// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record NecromancerMotion(uint Target, bool Flee, int OriginX, int OriginY);
public sealed record LowerResistDeltas(int? Fire,int Lightning,int Cold,int Poison);
public sealed record NecromancerCurse(CombatAbility Ability, uint Source, int Rank, DateTime UntilUtc, int OriginX, int OriginY,
    NecromancerMotion Motion = null, TerrorAi Terror = null, bool CloakBlind = false,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] int? PhysicalDelta = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] LowerResistDeltas ElementDeltas = null);
public sealed record BoneArmorState(int? Rank,int Remaining256,int? Maximum256);
public sealed record NecromancerState(NecromancerCurse Curse = null, int BoneArmor = 0, int ManaFraction = 0,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] BoneArmorState BoneArmorPool=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] BlindAiState BlindAi=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MindAiState Mind=null);

public sealed class NecromancerEffectProfiles : GenericPersistence
{
    private Dictionary<Serial, NecromancerState> _states = new();
    private Dictionary<Serial, NecromancerState> _snapshot;
    public NecromancerEffectProfiles() : base("SanctuaryNecromancerEffects", 112) { }
    public NecromancerState Find(Mobile actor) => actor is { Alive: true, Deleted: false }
        ? _states.GetValueOrDefault(actor.Serial) : null;
    public void Store(Mobile actor, NecromancerState state)
    {if(state.Curse?.Terror is{Override:not 0}){state=SourceMind.Superseded(state);}var before=_states.GetValueOrDefault(actor.Serial);_states[actor.Serial]=state;SourceTerror.Changed(actor,before?.Curse,state.Curse);CloakShadows.Changed(actor,before?.Curse,state.Curse);SourceBlind.Changed(actor,before?.Curse,state.Curse);SourceMind.Changed(actor,state);}
    public void Clear(Mobile actor)
    {if(_states.Remove(actor.Serial,out var old)){SourceTerror.Changed(actor,old.Curse,null);CloakShadows.Changed(actor,old.Curse,null);SourceBlind.Changed(actor,old.Curse,null);SourceMind.Changed(actor,null);}}
    public override void Serialize()
    {SourceBlind.CaptureMovement();Prune();_snapshot=new(_states);base.Serialize();}
    public override void Serialize(IGenericWriter writer)
    {
        var rows=_snapshot??new(_states);writer.Write(0);writer.Write(rows.Count);
        foreach (var (serial, state) in rows) { writer.Write(serial.Value); writer.Write(JsonSerializer.Serialize(state)); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Necromancer state version."); }
        int count = reader.ReadInt(); if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid Necromancer state count."); }
        var states = new Dictionary<Serial, NecromancerState>();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); var state = JsonSerializer.Deserialize<NecromancerState>(reader.ReadString());
            if(state?.BlindAi is{} blind){SourceBlind.Validate(blind);}
            if(state?.Mind is{} mind){SourceMind.Validate(mind);}
            if (!serial.IsMobile || state == null || state.BoneArmor < 0 || state.ManaFraction is < 0 or > 255
                || state.BoneArmorPool is{} pool && (pool.Rank is <1 or >255 || pool.Remaining256<=0
                    || pool.Rank.HasValue!=pool.Maximum256.HasValue || pool.Maximum256 is{} maximum && maximum<pool.Remaining256
                    || state.BoneArmor!=(pool.Remaining256+255L)/256)
                || state.Curse is { } curse && (!NecromancerEffects.KnownCurse(curse.Ability) || curse.Rank is < 1 or > 255
                    || curse.PhysicalDelta is{} delta&&!NecromancerEffects.ValidPhysicalDelta(curse.Ability,delta)
                    || curse.ElementDeltas is{} elements&&!NecromancerEffects.ValidElementDeltas(curse,elements)
                    || curse.Terror is{} terror && (!(curse.Ability==CombatAbility.Terror && terror.Skill is CombatAbility.Howl or CombatAbility.Terror || curse.Ability==CombatAbility.Taunt && terror.Skill==CombatAbility.Taunt) || terror.LaunchRank<1 || terror.Map<0 || terror.Map>=Map.Maps.Length || terror.VelocityPercent is <0 or >120))
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
    public override void PostDeserialize(){Prune();SourceTerror.AfterLoad(_states);CloakShadows.AfterLoad(_states);SourceBlind.AfterLoad(_states);SourceMind.AfterLoad(_states);}
}

public static class NecromancerEffects
{
    public static NecromancerEffectProfiles Profiles { get; private set; }
    public static void Configure()
    {
        Profiles = new(); EventSink.WorldSave += Profiles.Prune;
        ClassCombatHooks.SkillResistedDamage = SkillCombat.ResistedDamage;
        // Native AOS supplies the fractional physical event before committing life.
        // Its later ordinary hit callback must not repeat Life Tap.
        ClassCombatHooks.SkillWeaponHit = (attacker,target,loss,ranged)=>WeaponHit(attacker,target,loss,ranged,lifeTap:false,ironMaiden:false);
    }
    public static bool IsCurse(CombatAbility a) => a is CombatAbility.AmplifyDamage or CombatAbility.DimVision
        or CombatAbility.Weaken or CombatAbility.IronMaiden or CombatAbility.Terror or CombatAbility.Confuse
        or CombatAbility.LifeTap or CombatAbility.Attract or CombatAbility.Decrepify or CombatAbility.LowerResist;
    internal static bool KnownCurse(CombatAbility a)=>IsCurse(a) || a is CombatAbility.BattleCry or CombatAbility.Taunt or CombatAbility.CloakOfShadows;
    internal static bool ValidPhysicalDelta(CombatAbility ability,int delta)=>ability switch
    {CombatAbility.AmplifyDamage=>delta is -100 or -20,CombatAbility.Decrepify=>delta is -50 or -10,_=>false};
    internal static bool ValidElementDeltas(NecromancerCurse curse,LowerResistDeltas elements)
    {
        if(curse.Ability!=CombatAbility.LowerResist){return false;}
        int full=-Values(curse).Diminishing(5,6);
        bool Valid(int value)=>value==full||value==full/5;
        return (!elements.Fire.HasValue||Valid(elements.Fire.Value))&&Valid(elements.Lightning)&&Valid(elements.Cold)&&Valid(elements.Poison);
    }
    internal static int? ElementDelta(NecromancerCurse curse,string element)=>curse.Ability==CombatAbility.LowerResist&&curse.ElementDeltas is{} deltas
        ?element switch{"fire"=>deltas.Fire,"ltng"=>deltas.Lightning,"cold"=>deltas.Cold,"pois"=>deltas.Poison,_=>null}:null;
    internal static bool Vision(NecromancerCurse curse)=>curse?.Ability==CombatAbility.DimVision || curse is{Ability:CombatAbility.CloakOfShadows,CloakBlind:true};
    public static NecromancerCurse Curse(Mobile actor) => Profiles?.Find(actor)?.Curse is { } curse && curse.UntilUtc > TimedEffects.Now ? curse : null;
    internal static SkillValues Values(NecromancerCurse curse) => new(SkillMechanics.For(curse.Ability),
        key => key == SkillMechanics.For(curse.Ability).SourceKey ? curse.Rank : 0, _ => 0, 1);
    public static bool AiCurse(Mobile target) => target is BaseCreature { Alive: true, Deleted: false, Controlled: false, Summoned: false }
        && !NamedCombat.Elite(target) && target is not BaseVendor;
    public static int DifficultyDivisor(Mobile target) => target is BaseCreature creature ? MonsterVariants.Difficulty(creature) switch
    { MonsterDifficulty.Nightmare => 2, MonsterDifficulty.Hell => 4, _ => 1 } : 1;
    public static bool Apply(PlayerMobile owner, Mobile target, CombatAbility ability, int rank)
    {
        if(NecromancerCurseCasts.Handles(ability)){return NecromancerCurseCasts.Apply(owner,target,ability,rank);}
        if(ability==CombatAbility.Attract){return AttractCasts.Apply(owner,target,rank);}
        if (rank is <1 or >255 || !IsCurse(ability) || !ClassCombat.IsHostile(owner, target) || Curse(target)?.Ability == CombatAbility.Attract
            || ability == CombatAbility.DimVision && Curse(target)?.Ability == CombatAbility.Confuse) { return false; }
        bool ai = ability is CombatAbility.DimVision or CombatAbility.Terror or CombatAbility.Confuse or CombatAbility.Attract;
        if (ai && !AiCurse(target)) { return false; }
        // Duration and the stored effect must use the same supplied rank.
        var values = SkillMechanics.Values(owner, ability, rank);
        int frames = (int)values.Eval("auralencalc");
        if (ai) { frames /= DifficultyDivisor(target); }
        if(!SourceCurseDuration.Incoming(target,SkillMechanics.For(ability).Text("auratargetstate"),frames,out frames)){return false;}
        var state = Profiles.Find(target) ?? new();
        var motion = ability == CombatAbility.DimVision && target is BaseCreature blinded ? CaptureMotion(blinded) : null;
        Profiles.Store(target, state with { Curse = new(ability, owner.Serial.Value, rank,
            TimedEffects.Now.AddMilliseconds(frames * 40), owner.X, owner.Y, motion) });
        CurseApplied(owner,target);return true;
    }
    internal static void CurseApplied(PlayerMobile owner,Mobile target)
    {
        if (TimedEffects.TryActor(target, out var actor) && actor.Control is { } control)
        { actor.Control = control with { BlindUntil = default, FearUntil = default }; }
        if (target is BaseCreature creature)
        { creature.AIObject?.ResetApproachState(); creature.AIObject?.ClearMoveIntent(); creature.AIObject?.AITimer.Prod(); }
        target.FixedEffect(0x375A, 10, 15, 0x455, 0); // Placeholder curse marker.
        owner.DoHarmful(target);
    }
    internal static NecromancerMotion CaptureMotion(BaseCreature creature)
    {
        var previous = Curse(creature);
        if (Vision(previous)) { return previous.Motion; }
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
            CombatAbility.AmplifyDamage when element == "physical" => curse.PhysicalDelta??-(int)values.Param(5),
            CombatAbility.Decrepify when element == "physical" => curse.PhysicalDelta??values.Param(5),
            CombatAbility.LowerResist when element is "fire" or "cold" or "ltng" or "pois" => ElementDelta(curse,element)??-values.Diminishing(5, 6),
            _ => 0
        };
        // Immunity-breaking curses run at one fifth effectiveness against an immune monster.
        bool captured=(curse.Ability is CombatAbility.AmplifyDamage or CombatAbility.Decrepify&&element=="physical"&&curse.PhysicalDelta.HasValue)
            ||ElementDelta(curse,element).HasValue;
        if (!captured&&target is BaseCreature and not BaseHire && (immunityBaseline ?? baseline) >= 100) { delta /= 5; }
        // Player caps apply after pierce; retain overcapped resistance until that final step.
        return target is PlayerMobile || deferCap ? baseline+delta : Math.Clamp(baseline + delta, -100, 200);
    }
    // STAT_DAMAGEPERCENT joins weapon ED before its -90 floor. It does not
    // scale arbitrary physical spell/retaliation packets at mitigation time.
    public static int DamageStatPercent(Mobile actor) => HexSiphon.DamagePercent(actor)+(Curse(actor) is { } curse ? curse.Ability switch
    {
        CombatAbility.Weaken => Values(curse).Linear(5, 6),
        CombatAbility.Decrepify => Values(curse).Param(5),
        CombatAbility.BattleCry or CombatAbility.Taunt => (int)Values(curse).Eval("aurastatcalc2"),
        _ => 0
    } : 0);
    public static int OutgoingPhysicalPercent(Mobile actor)=>100+DamageStatPercent(actor);
    internal static int DefenseStatPercent(Mobile actor)=>Curse(actor) is{Ability:CombatAbility.BattleCry or CombatAbility.CloakOfShadows} curse?(int)Values(curse).Eval("aurastatcalc1"):0;
    internal static int AttackStatPercent(Mobile actor)=>Curse(actor) is{Ability:CombatAbility.Taunt} curse?(int)Values(curse).Eval("aurastatcalc1"):0;
    public static int Slow(Mobile actor) => Curse(actor) is{Ability:CombatAbility.Decrepify} curse ? Math.Max(0,-Values(curse).Param(5)) : 0;
    public static bool TargetAllowed(Mobile actor, Mobile target)
    {
        if (Curse(actor) is not { } curse) { return true; }
        return curse.Ability switch
        {
            CombatAbility.Taunt when curse.Terror!=null => SourceTerror.TargetAllowed(actor,target),
            CombatAbility.Terror => curse.Terror!=null && SourceTerror.TargetAllowed(actor,target),
            CombatAbility.CloakOfShadows when curse.CloakBlind => target != null && actor.Map == target.Map && actor.InRange(target, 1),
            CombatAbility.DimVision => SourceBlind.TargetAllowed(actor,target),
            _ => true
        };
    }
    public static bool Think(BaseCreature creature)
    {
        if (creature.AIObject is not { } ai) { return false; }
        if (Profiles.Find(creature) is { Curse: { } elapsed } state && elapsed.UntilUtc <= TimedEffects.Now)
        {
            Profiles.Store(creature, state with { Curse = null });
            if (Vision(elapsed) || elapsed.Ability is CombatAbility.Terror or CombatAbility.Confuse or CombatAbility.Attract)
            {
                creature.Combatant = null; creature.FocusMob = null;
                ai.ResetApproachState(); ai.ClearMoveIntent(); ai.Action = ActionType.Wander;
                if (creature is GraveyardUndead undead) { undead.InterruptForRecovery(); }
            }
        }
        var curse = Curse(creature);
        SourceMind.Think(creature);
        if(curse?.Ability==CombatAbility.DimVision&&SourceBlind.Think(creature,curse)){return true;}
        if(curse?.Ability==CombatAbility.Taunt && curse.Terror!=null){return SourceTerror.Think(creature,curse);}
        if (curse?.Ability == CombatAbility.Terror)
        {
            if(curse.Terror!=null){return SourceTerror.Think(creature,curse);}
            creature.Combatant = null; creature.FocusMob = null;
            var origin = new Point3D(curse.OriginX, curse.OriginY, creature.Z);
            ai.DoMove((Direction)(((int)creature.GetDirectionTo(origin) + 4) & 7)); return true;
        }
        if (Vision(curse))
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
        return false; // Generic mind overrides feed ordinary species AI.
    }
    public static int Armor(Mobile actor) => Profiles?.Find(actor)?.BoneArmor ?? 0;
    internal static int Armor256(Mobile actor)=>Profiles?.Find(actor) is{} state
        ?state.BoneArmorPool?.Remaining256??(int)Math.Min(int.MaxValue,(long)state.BoneArmor*256):0;
    internal static int? ArmorMaximum256(Mobile actor)=>Profiles?.Find(actor)?.BoneArmorPool?.Maximum256;
    internal static void ClearBoneArmor(Mobile actor)
    {if(Profiles?.Find(actor) is{} state){Profiles.Store(actor,state with{BoneArmor=0,BoneArmorPool=null});}}
    public static double AbsorbPhysical(Mobile target, double damage)
    {
        if (damage <= 0 || Profiles?.Find(target) is not { BoneArmor: > 0 } state) { return damage; }
        // Keep the existing min(pool,damage) overflow policy while the older
        // event22 exhaustion branch remains discrepant. Do not round each
        // incoming fractional hit up to a whole point of shield consumption.
        var step=SourceAbsorptionPool.Take(Armor256(target),NamedElements.To256(damage));
        var pool=step.Remaining256==0?null:state.BoneArmorPool is{} saved?saved with{Remaining256=step.Remaining256}
            :new BoneArmorState(null,step.Remaining256,null); // Legacy rank/max were not saved;do not invent them.
        Profiles.Store(target,state with{BoneArmor=(int)((step.Remaining256+255L)/256),BoneArmorPool=pool});
        return step.Damage256/256.0;
    }
    public static void BoneArmor(PlayerMobile owner, SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.BoneArmor);
        int amount=checked((int)values.Eval("aurastatcalc1"));
        if(values.Ability!=CombatAbility.BoneArmor||values.Rank is <1 or >255||row.SourceId!=68||row.Number("srvdofunc")!=18
            ||row.Text("aurastat1")!="bonearmor"||row.Text("aurastat2")!="bonearmormax"||row.Number("auraeventfunc1")!=22
            ||amount<=0||amount!=values.Eval("aurastatcalc2"))
        {throw new NotSupportedException("Unmapped Bone Armor pool");}
        DefensiveArmors.ClearGroup(owner);
        Profiles.Store(owner, (Profiles.Find(owner) ?? new()) with { BoneArmor=(int)((amount+255L)/256),BoneArmorPool=new(values.Rank,amount,amount) });
        owner.FixedEffect(0x375A, 10, 15, 0x481, 0); // Placeholder shield marker.
    }
    internal static int ManaMaximum256(Mobile owner)=>NpcEquipmentResources.Maximum256(owner,2)??owner.ManaMax*256;
    internal static int RawManaBalance256(Mobile owner)=>checked(owner.Mana*256+(Profiles.Find(owner)?.ManaFraction??0));
    public static int ManaBalance256(Mobile owner)
    {
        int balance=RawManaBalance256(owner);
        // Native whole-mana regeneration can reach the integer maximum while
        // an earlier fractional payment remainder is still stored. It cannot
        // grant usable mana above that maximum; preserve raw NPC observations.
        return NpcEquipmentResources.Maximum256(owner,2) is{} exact?Math.Min(exact,balance)
            :owner is PlayerMobile?Math.Min(owner.ManaMax*256,balance):balance;
    }
    internal static void SetMana256(Mobile owner,int amount256)
    {
        amount256=Math.Clamp(amount256,0,ManaMaximum256(owner));var state=Profiles.Find(owner)??new();
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
        long balance = (long)ManaBalance256(owner) - cost256;
        owner.Mana = (int)(balance / 256); Profiles.Store(owner, state with { ManaFraction = (int)(balance % 256) }); return true;
    }
    internal static int LifeTapHealing256(int physical256,int percent)
    {
        if(percent is <0 or >100){throw new NotSupportedException("Life Tap percent is outside the pinned healing domain");}
        // The actual source helper divides large FP256 values first. Its
        // intermediate is safe for this skill's50% and supplied0..100 controls.
        return SourcePercentage.Calculate(Math.Max(0,physical256),percent,100);
    }
    internal static int LifeTap(Mobile attacker,Mobile target,int physical256)
    {
        if(physical256<=0 || attacker is not(PlayerMobile or BaseCreature) || attacker is not{Alive:true,Deleted:false}
            || target is not(PlayerMobile or BaseCreature) || Curse(target) is not{Ability:CombatAbility.LifeTap} tap
            || SkillHealth.Balance256(attacker)<=0 || attacker.HitsMax<=0){return 0;}
        // The defender event precedes ordinary leech's victim-life cap and has no
        // monster drain/difficulty reduction. Pinned calc1=ln56 is50% at all ranks.
        int healed=SkillHealth.Heal(attacker,LifeTapHealing256(physical256,Values(tap).Linear(5,6)));
        attacker.FixedEffect(0x376A,10,10,0x26,0); // Life Tap steallife recovery placeholder.
        return healed;
    }
    internal static void PreparedNativeWeaponHit(Mobile attacker,Mobile target,int physical256,bool ranged)
    {PreparedWeaponEvents.BeforeReturn(attacker,target,physical256,ranged);}
    public static void WeaponHit(Mobile attacker, Mobile target, int physicalLoss, bool ranged,bool lifeTap=true,bool ironMaiden=true)
    {
        SorceressEffects.MeleeHit(attacker, target, physicalLoss, ranged);
        if (physicalLoss <= 0) { return; }
        // Compatibility entry point for explicit whole-HP events. Native and
        // prepared weapon paths already dispatched fractional Life Tap/Iron Maiden.
        if(ironMaiden){IronMaiden.MeleeDamage(attacker,target,(int)Math.Min((long)physicalLoss*256,int.MaxValue),ranged);}
        if(lifeTap){LifeTap(attacker,target,(int)Math.Min((long)physicalLoss*256,int.MaxValue));}
    }
}
