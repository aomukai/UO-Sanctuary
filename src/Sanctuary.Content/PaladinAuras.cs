// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record PaladinEmitter(uint Owner, CombatAbility Ability, DateTime NextUtc,bool SuppressBaseMana=false,int FlatManaNumerator=0);
public sealed record PaladinAuraLease(uint Target, uint Source, CombatAbility Ability, int Rank, int Percent, DateTime UntilUtc,int MaximumResistance=0,int StaminaPercent=0,int VelocityPercent=0,int DamagePercent=0,int AttackPercent=0,int UninterruptChance=0,int WeaponMin=0,int WeaponMax=0);
public sealed record PaladinAuraSave(PaladinEmitter[] Emitters, PaladinAuraLease[] Leases);
public sealed class PaladinAuraProfiles : GenericPersistence
{
    public PaladinAuraProfiles() : base("SanctuaryPaladinAuras", 121) { }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(PaladinAuras.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt()!=0) { throw new InvalidDataException("Unknown Paladin aura version"); }
        PaladinAuras.Restore(JsonSerializer.Deserialize<PaladinAuraSave>(reader.ReadString()));
    }
    public override void PostDeserialize()
    {
        foreach(var lease in PaladinAuras.Snapshot().Leases)
        {if(lease.Ability==CombatAbility.Vigor && World.FindMobile((Serial)lease.Target) is {} target){PaladinVigor.Refresh(target);}}
        foreach (var emitter in PaladinAuras.Snapshot().Emitters)
        { if (World.FindMobile((Serial)emitter.Owner) is {} actor) { actor.CheckStatTimers(); } }
    }
}

public static class PaladinAuras
{
    private static readonly Dictionary<uint,PaladinEmitter> _emitters=new();
    private static readonly Dictionary<(uint,CombatAbility),PaladinAuraLease> _leases=new();
    private static readonly List<uint> _owners=new();
    private static readonly List<(uint,CombatAbility)> _expired=new();
    public static PaladinAuraProfiles Profiles { get; private set; }
    public static void Configure()
    {
        Profiles=new();var previous=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateMana=actor=>!ManaSuppressed(actor) && previous?.Invoke(actor)!=false;
    }
    public static void Initialize() => Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),Tick);
    public static CombatAbility Selected(Mobile owner) => _emitters.TryGetValue(owner.Serial.Value,out var emitter) ? emitter.Ability : CombatAbility.None;
    public static PaladinAuraLease Effect(Mobile target, CombatAbility ability) => target is { Alive:true,Deleted:false }
        && _leases.TryGetValue((target.Serial.Value,ability),out var lease) && lease.UntilUtc>TimedEffects.Now ? lease : null;
    public static bool ManaSuppressed(Mobile target)=>target!=null && _emitters.TryGetValue(target.Serial.Value,out var emitter)
        && emitter.Ability==CombatAbility.Prayer && emitter.SuppressBaseMana && Effect(target,CombatAbility.Prayer)!=null;
    public static int ManaRecoveryPercent(Mobile target)=>Effect(target,CombatAbility.Meditation)?.Percent ?? 0;
    public static int DamagePercent(Mobile target) => (Effect(target,CombatAbility.Might)?.Percent ?? 0)+(Effect(target,CombatAbility.Concentration)?.Percent ?? 0)+(Effect(target,CombatAbility.Fanaticism)?.DamagePercent ?? 0);
    public static int DefensePercent(Mobile target) => (Effect(target,CombatAbility.Defiance)?.Percent ?? 0)+PaladinConviction.DefensePercent(target);
    public static int AttackPercent(Mobile target)
    {
        int fanaticism=Effect(target,CombatAbility.Fanaticism)?.AttackPercent ?? 0;
        if (Effect(target,CombatAbility.BlessedAim) is {} aura) { return aura.Percent+fanaticism; }
        return target is PlayerMobile player && ClassCreation.Profiles.TryGet(player.Serial,out var profile) && profile.Class==DiabloClass.Paladin
            ? ClassCreation.Profiles.GetRank(player.Serial,"paladin.blessedaim")*SkillMechanics.For(CombatAbility.BlessedAim).Parameter(8)+fanaticism : fanaticism;
    }
    public static double NativeWeaponMultiplier(Mobile source, bool weapon) => CombatAuras.NativeWeaponMultiplier(source,weapon);
    public static bool CombatStats(Mobile mobile) => Effect(mobile,CombatAbility.Defiance)!=null || Effect(mobile,CombatAbility.BlessedAim)!=null || Effect(mobile,CombatAbility.Fanaticism)!=null || PaladinConviction.Managed(mobile);
    public static void SelectionChanged(PlayerMobile owner, CombatAbility oldAbility, CombatAbility ability)
    {
        bool wasSuppressed=ManaSuppressed(owner);_emitters.Remove(owner.Serial.Value);
        if (PaladinCombat.IsAura(oldAbility)) { _leases.Remove((owner.Serial.Value,oldAbility));if(oldAbility==CombatAbility.Vigor){PaladinVigor.Refresh(owner);} }
        if (owner.Alive && PaladinCombat.IsAura(ability) && PaladinCombat.Available(owner,ability))
        {
            var now=TimedEffects.Now;
            if (SkillMechanics.For(ability).Number("immediate")!=0) { Pulse(owner,ability,now); }
            else { _emitters[owner.Serial.Value]=new(owner.Serial.Value,ability,now.AddMilliseconds(Math.Max(5,SkillMechanics.Values(owner,ability).Eval("perdelay"))*40)); }
        }
        if (wasSuppressed!=ManaSuppressed(owner)) { owner.CheckStatTimers(); }
    }
    private static bool Apply(Mobile target, PlayerMobile owner, CombatAbility ability, int rank, int value, int healing256, DateTime now, DateTime until,int maximumResistance=0,int staminaPercent=0,int velocityPercent=0,int damagePercent=0,int attackPercent=0,int uninterruptChance=0)
    {
        var key=(target.Serial.Value,ability);
        if (_leases.TryGetValue(key,out var old) && old.UntilUtc>now && old.Rank>rank) { return false; }
        uint source=old is { } && old.UntilUtc>now && old.Rank==rank ? old.Source : owner.Serial.Value;
        _leases[key]=new(target.Serial.Value,source,ability,rank,value,until,maximumResistance,staminaPercent,velocityPercent,damagePercent,attackPercent,uninterruptChance);
        if(ability==CombatAbility.Vigor && old?.StaminaPercent!=staminaPercent){PaladinVigor.Refresh(target);}
        bool changed=ability==CombatAbility.Cleansing && value>0 && PaladinCleansing.Apply(target,value,now);
        if (healing256>0 && SkillHealth.Heal(target,healing256)>0) { changed=true; }
        return changed || value!=0 && ability is not (CombatAbility.Prayer or CombatAbility.Cleansing);
    }
    private static void Pulse(PlayerMobile owner, CombatAbility ability, DateTime now)
    {
        var values=SkillMechanics.Values(owner,ability);var definition=SkillMechanics.For(ability);
        int frames=Math.Max(5,(int)values.Eval("perdelay")),cost=values.Mana256;
        if(ability==CombatAbility.Sanctuary)
        {
            PaladinSanctuary.Pulse(owner,values,now);
            if(owner is{Alive:true,Deleted:false}){_emitters[owner.Serial.Value]=new(owner.Serial.Value,ability,now.AddMilliseconds(frames*40));}
            return;
        }
        if(ability==CombatAbility.Conviction)
        {
            PaladinConviction.Pulse(owner,values,now);
            if(owner is{Alive:true,Deleted:false}){_emitters[owner.Serial.Value]=new(owner.Serial.Value,ability,now.AddMilliseconds(frames*40));}
            return;
        }
        if(PaladinElements.Handles(ability))
        {
            PaladinElements.Pulse(owner,ability,values,now);
            if(owner is {Alive:true,Deleted:false}){_emitters[owner.Serial.Value]=new(owner.Serial.Value,ability,now.AddMilliseconds(frames*40));}
            return;
        }
        bool canPay=NecromancerEffects.CanPay(owner,cost),wasSuppressed=ManaSuppressed(owner);
        int value=canPay?(int)values.Eval("aurastatcalc1"):0;
        int healing=canPay ? ability==CombatAbility.Prayer?value:ability is CombatAbility.Cleansing or CombatAbility.Meditation?(int)values.Eval("aurastatcalc2"):0 :0;
        int stamina=ability==CombatAbility.Vigor?(int)values.Eval("aurastatcalc2"):0;
        int velocity=ability==CombatAbility.Vigor?(int)values.Eval("aurastatcalc3"):0;
        int damage=ability==CombatAbility.Fanaticism?(int)values.Eval("aurastatcalc3"):ability==CombatAbility.Thorns?(int)values.Eval("aurastatcalc2"):0;
        int selfDamage=ability==CombatAbility.Fanaticism && NecromancerEffects.ManaBalance256(owner)>cost?(int)values.Eval("passivecalc1"):damage;
        int attack=ability==CombatAbility.Fanaticism?(int)values.Eval("aurastatcalc2"):0;
        int uninterrupt=ability==CombatAbility.Concentration?(int)values.Eval("aurastatcalc2"):0;
        int maximum=canPay && PaladinResistance.Single(ability)?(int)values.Eval("aurastatcalc2"):0;
        var until=now.AddMilliseconds((frames+1)*40);
        bool changed=Apply(owner,owner,ability,values.Rank,value,healing,now,until,maximum,stamina,velocity,selfDamage,attack,uninterrupt);
        // Settling a pending poison tick during Cleansing can kill the caster before allied applications.
        if (owner is not { Alive:true,Deleted:false }) { return; }
        if(ability==CombatAbility.Redemption)
        {
            PaladinRedemption.Pulse(owner,values);
            _emitters[owner.Serial.Value]=new(owner.Serial.Value,ability,now.AddMilliseconds(frames*40));
            return;
        }
        double radius=SkillGeometry.Tiles(values.Eval("aurarangecalc"));int filter=definition.Number("aurafilter");
        using var targets=Server.Collections.PooledRefList<Mobile>.Create();
        foreach (var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,(int)Math.Ceiling(radius)))
        {
            double dx=target.X-owner.X,dy=target.Y-owner.Y;
            if (target!=owner && target is { Alive:true,Deleted:false } && target is not BaseVendor
                && (target is PlayerMobile || (filter&2)!=0) && Math.Abs(target.Z-owner.Z)<16 && dx*dx+dy*dy<=radius*radius
                && ClassCombat.Friendly(owner,target) && CorpseSkillRules.AllowsAura(target) && ((filter&0x2000)==0 || !SpellHelper.IsTown(target.Location,target)))
            { targets.Add(target); }
        }
        foreach (var target in targets)
        { if (target is { Alive:true,Deleted:false }) { changed=Apply(target,owner,ability,values.Rank,value,healing,now,until,maximum,stamina,velocity,damage,attack,uninterrupt) || changed; } }
        bool suppress=cost>0 && changed;
        if (suppress) { NecromancerEffects.Pay(owner,cost); } // One payment for the useful pulse, not per recipient.
        int flat=wasSuppressed && suppress && _emitters.TryGetValue(owner.Serial.Value,out var oldEmitter)?oldEmitter.FlatManaNumerator:0;
        _emitters[owner.Serial.Value]=new(owner.Serial.Value,ability,now.AddMilliseconds(frames*40),suppress,flat);
        if (wasSuppressed!=suppress) { owner.CheckStatTimers(); }
        owner.FixedEffect(0x375A,10,10,0x481,0); // Placeholder aura; final rings/overlays tracked separately.
    }
    internal static void ElementalWeapon(PaladinAuraLease lease)=>_leases[(lease.Target,lease.Ability)]=lease;
    public static void Clear(Mobile actor)
    {
        bool suppressed=ManaSuppressed(actor);_emitters.Remove(actor.Serial.Value); _expired.Clear();
        foreach (var key in _leases.Keys) { if (key.Item1==actor.Serial.Value) { _expired.Add(key); } }
        foreach (var key in _expired) { _leases.Remove(key); }
        if (suppressed) { actor.CheckStatTimers(); }
        PaladinVigor.Refresh(actor);
        // Recipient stat lists on other actors retain their remaining source lifetime.
    }
    private static void Tick()
    {
        var now=TimedEffects.Now; _expired.Clear();
        foreach (var row in _leases)
        { if (row.Value.UntilUtc<=now || World.FindMobile((Serial)row.Key.Item1) is not { Alive:true,Deleted:false }) { _expired.Add(row.Key); } }
        foreach (var key in _expired) { _leases.Remove(key);if(key.Item2==CombatAbility.Vigor && World.FindMobile((Serial)key.Item1) is {} target){PaladinVigor.Refresh(target);} }
        _owners.Clear(); _owners.AddRange(_emitters.Keys);
        foreach (uint serial in _owners)
        {
            if (!_emitters.TryGetValue(serial,out var emitter)) { continue; }
            if (World.FindMobile((Serial)serial) is not PlayerMobile { Alive:true,Deleted:false } owner
                || !PaladinCombat.Available(owner,emitter.Ability) || SkillBindings.Instance.Get(owner).Right!=emitter.Ability)
            { if (World.FindMobile((Serial)serial) is {} actor) { SelectionChangedOrClear(actor,emitter.Ability); } else { _emitters.Remove(serial); } continue; }
            if (emitter.SuppressBaseMana && Effect(owner,CombatAbility.Prayer)==null)
            { emitter=emitter with { SuppressBaseMana=false,FlatManaNumerator=0 };_emitters[serial]=emitter;owner.CheckStatTimers(); }
            if (owner.Map==null || owner.Map==Map.Internal) { continue; }
            if (emitter.SuppressBaseMana)
            {
                // Prayer suppresses natural recovery, while approved Verite flat recovery remains independent.
                int numerator=emitter.FlatManaNumerator+Math.Max(0,ClassGear.Material(owner,"mana-per-five"))*256;
                NecromancerEffects.AddMana(owner,numerator/125);
                emitter=emitter with { FlatManaNumerator=owner.Mana>=owner.ManaMax?0:numerator%125 };_emitters[serial]=emitter;
            }
            if (emitter.NextUtc>now) { continue; }
            Pulse(owner,emitter.Ability,now); // No offline/lag catch-up burst.
        }
    }
    private static void SelectionChangedOrClear(Mobile actor, CombatAbility ability)
    { bool suppressed=ManaSuppressed(actor);_emitters.Remove(actor.Serial.Value); _leases.Remove((actor.Serial.Value,ability));if(ability==CombatAbility.Vigor){PaladinVigor.Refresh(actor);}if(suppressed){actor.CheckStatTimers();} }
    public static PaladinAuraSave Snapshot() => new(new List<PaladinEmitter>(_emitters.Values).ToArray(),new List<PaladinAuraLease>(_leases.Values).ToArray());
    public static void Restore(PaladinAuraSave saved)
    {
        if (saved?.Emitters==null || saved.Leases==null) { throw new InvalidDataException("Missing Paladin auras"); }
        _emitters.Clear(); _leases.Clear();
        foreach (var emitter in saved.Emitters)
        { if (!PaladinCombat.IsAura(emitter.Ability) || emitter.FlatManaNumerator is <0 or >=125 || emitter.SuppressBaseMana && emitter.Ability!=CombatAbility.Prayer || !_emitters.TryAdd(emitter.Owner,emitter)) { throw new InvalidDataException("Invalid Paladin emitter"); } }
        foreach (var lease in saved.Leases)
        { if (!PaladinCombat.IsAura(lease.Ability) || lease.Rank<1 || lease.Percent<0 || lease.MaximumResistance<0 || lease.StaminaPercent<0 || lease.VelocityPercent<0 || lease.DamagePercent<0 || lease.AttackPercent<0 || lease.UninterruptChance is <0 or >100 || !_leases.TryAdd((lease.Target,lease.Ability),lease)) { throw new InvalidDataException("Invalid Paladin aura lease"); } }
    }
}
