// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record DefilerBodyState(uint Owner, DefilerSnapshot Snapshot, MonsterDifficulty Difficulty, DateTime NextRecoveryUtc)
{ public string Profile { get; init; } = "prepared-defiler-life-owner-v1"; }

// Saved vulnerable vital/owner body only. Offense and Health Link are unavailable. Native UO AI,
// attacks, spells, regeneration and inherited skeleton immunities are disabled.
[SerializationGenerator(0)]
public partial class WarlockDefilerBody : SkeletonGuardian, ISourceLifeMaximum
{
    [SerializableField(0, setter: "private")] private string _saved = "{}";
    private DefilerBodyState _prepared;
    internal DefilerBodyState Prepared => _prepared ?? (_saved == "{}" ? null : _prepared = JsonSerializer.Deserialize<DefilerBodyState>(_saved));
    internal string SavedText => _saved;
    public override DiabloClass SummonClass => DiabloClass.Warlock;
    public override bool Permitted(PlayerMobile owner) => ClassCreation.Profiles.Histories(owner.Serial)
        .Any(h => h.Profile.Class == DiabloClass.Warlock && h.Ranks.GetValueOrDefault("warlock.summondefiler") > 0);
    public override bool UsesNativeSummonTimer => false;
    public override bool CanRegenStam => false;
    public override bool BleedImmune => false;
    public override Poison PoisonImmune => null;
    public override bool IsDispellable => false;
    internal override string SourceMonsterId => "warputriddefiler";
    internal override int SourceRegeneration256 => checked((Prepared?.Snapshot.Birth.Vitals.BaseLife256 ?? 140 * 256)
        * NecromancerSummons.Source(SourceMonsterId, "DamageRegen")) >> 12;
    public override long SourceLife256 => Prepared?.Snapshot.Birth.Vitals.MaximumLife256 ?? 140 * 256;
    public int MaximumLife256
    {
        get
        {
            long basis = SourceLife256 + ((long)HitsMaxSeed - CombatProfile.Life) * 256;
            return (int)Math.Clamp(basis + ClassResources.Percentage(basis, BattleOrders.LifePercent(this)), 256, 65000 * 256);
        }
    }
    public override MonsterProfile CombatProfile => Prepared is { } state
        ? new(state.Snapshot.Birth.Vitals.PetLevel, (state.Snapshot.Birth.Vitals.MaximumLife256 + 255) / 256,
            state.Snapshot.Birth.ProjectedAttack, state.Snapshot.Birth.ProjectedArmor, 0, 0, 1000, 400, 0, 0, 0, 0, 0)
        : new(1, 140, 0, 125, 0, 0, 1000, 400, 0, 0, 0, 0, 0);
    public override (int Min, int Max) SkillWeaponDamage => (0, 0);
    public WarlockDefilerBody()
    {
        Name = "Summoned Defiler"; ApplyAppearance(); CantWalk = true; ControlSlots = 0;
        AIObject?.AITimer.Stop(); SetStr(0); SetDex(0); SetInt(0); SetHits(140); SetDamage(0, 0);
    }
    private void ApplyAppearance()
    { var appearance = WarlockDemonAppearance.For(CombatAbility.SummonDefiler); Body = appearance.Body; BaseSoundID = appearance.Sound; }
    public override TimeSpan BeginMelee(Mobile target) => throw new NotSupportedException("Prepared Defiler offense, Health Link and GenericPet controller remain unverified");
    public override void OnThink() { AIObject?.AITimer.Stop(); Combatant = null; FocusMob = null; Warmode = false; }
    public override void SuspendActions() { base.SuspendActions(); }
    public override void OnAfterDelete() { NpcEquipmentRandom.Forget(this); base.OnAfterDelete(); }
    internal static void Validate(DefilerBodyState state)
    {
        try
        {
            if (state?.Snapshot?.Birth?.Suppliers is not { PhysicalSunder4: 0, PhysicalSunder7: 0 }
                || !((Serial)state.Owner).IsMobile || !Enum.IsDefined(state.Difficulty) || state.Snapshot.Birth.Vitals.Difficulty != state.Difficulty
                || state.NextRecoveryUtc.Kind != DateTimeKind.Utc || state.NextRecoveryUtc > DateTime.MaxValue.AddMilliseconds(-40)
                || state.Profile != "prepared-defiler-life-owner-v1" || DefilerPrepared.Rescale(state.Snapshot, state.Difficulty) != state.Snapshot)
            { throw new InvalidDataException("Invalid prepared Defiler life/owner state"); }
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        { throw new InvalidDataException("Unsupported prepared Defiler snapshot", e); }
    }
    internal void RestorePrepared(DefilerBodyState state)
    { Validate(state); string json = JsonSerializer.Serialize(state); _prepared = state; _saved = json; }
    internal static WarlockDefilerBody CreatePrepared(PlayerMobile owner, Point3D point, DateTime at, GenericPetBirthBinding startup=null)
    {
        if (at.Kind != DateTimeKind.Utc || at > DateTime.MaxValue.AddMilliseconds(-40) || owner?.Map == null
            || owner.Map == Map.Internal || !owner.Map.CanSpawnMobile(point))
        { throw new ArgumentException("Defiler needs a legal world point and UTC clock"); }
        var capture = DefilerPrepared.Capture(owner);
        var state = new DefilerBodyState(owner.Serial.Value, capture, SummonDifficulties.For(owner.Map), at.AddMilliseconds(40));
        Validate(state); var body = new WarlockDefilerBody();
        try
        {
            body.RestorePrepared(state); body.Initialize(owner, capture.Birth.Suppliers.Rank, 0); body.SetResistances();
            if (!body.SetControlMaster(owner)) { throw new InvalidOperationException("Defiler control master refused"); }
            BattleCommand.Track(body); PersistentMinions.Profiles.Track(body); body.MoveToWorld(point, owner.Map);
            body.AIObject?.AITimer.Stop(); SkillHealth.SetBalance256(body, body.MaximumLife256); startup?.RegisterNew(body); return body;
        }
        catch { DeathMarkWorld.Remove(body); body.Delete(); throw; }
    }
    internal CreatureResistances SourceResistances(MonsterDifficulty? difficulty = null)
    {
        var value = difficulty ?? SummonDifficulty;
        return new(checked(NecromancerSummons.Source(SourceMonsterId, "ResDm", value) + (Prepared?.Snapshot.Birth.Suppliers.PhysicalResistBonus ?? 0)),
            NecromancerSummons.Source(SourceMonsterId, "ResMa", value), NecromancerSummons.Source(SourceMonsterId, "ResFi", value),
            NecromancerSummons.Source(SourceMonsterId, "ResLi", value), NecromancerSummons.Source(SourceMonsterId, "ResCo", value), NecromancerSummons.Source(SourceMonsterId, "ResPo", value));
    }
    private void SetResistances()
    {
        var resistance = SourceResistances();
        SetResistance(ResistanceType.Physical, resistance.Physical ?? 0); SetResistance(ResistanceType.Fire, resistance.Fire ?? 0);
        SetResistance(ResistanceType.Cold, resistance.Cold ?? 0); SetResistance(ResistanceType.Energy, resistance.Lightning ?? 0);
        SetResistance(ResistanceType.Poison, resistance.Poison ?? 0);
    }
    internal override void RefreshDifficulty()
    {
        if (Prepared is { } state) { RestorePrepared(state with { Difficulty = SummonDifficulty, Snapshot = DefilerPrepared.Rescale(state.Snapshot, SummonDifficulty) }); }
        base.RefreshDifficulty(); SetResistances();
    }
    internal void AdvanceRecovery(DateTime at, bool actions)
    {
        if (at.Kind != DateTimeKind.Utc) { throw new ArgumentException("Defiler clock must be UTC"); }
        if (Prepared is { } state && WarlockDemonRecovery.Advance(this, state.Owner, state.NextRecoveryUtc, at, actions) is { } next)
        { RestorePrepared(Prepared with { NextRecoveryUtc = next }); }
    }
    [AfterDeserialization] private void AfterDefilerLoad()
    { AIObject?.AITimer.Stop(); _prepared = JsonSerializer.Deserialize<DefilerBodyState>(_saved); Validate(_prepared); ApplyAppearance(); }
}
