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

internal sealed record GoatmanBodyState(uint Owner,GoatmanSnapshot Snapshot,MonsterDifficulty Difficulty,DateTime NextRecoveryUtc)
{
    public string Profile{get;init;}="prepared-goatman-life-owner-v1";
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public GoatmanFrenzyState Frenzy{get;init;}
}

// Saved body/life/ownership backend. GenericPet movement,12-slot live event
// transport and learned casting remain separate; native skeleton AI is disabled.
[SerializationGenerator(0)]
public partial class WarlockGoatmanBody:SkeletonGuardian,ISourceLifeMaximum
{
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private GoatmanBodyState _prepared;
    internal GoatmanBodyState Prepared=>_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<GoatmanBodyState>(_saved));
    internal string SavedText=>_saved;
    public override DiabloClass SummonClass=>DiabloClass.Warlock;
    public override bool Permitted(PlayerMobile owner)=>ClassCreation.Profiles.Histories(owner.Serial)
        .Any(x=>x.Profile.Class==DiabloClass.Warlock&&x.Ranks.GetValueOrDefault("warlock.summongoatman")>0);
    public override bool UsesNativeSummonTimer=>false;
    public override bool CanRegenStam=>false;
    public override bool BleedImmune=>false;
    public override Poison PoisonImmune=>null;
    public override bool IsDispellable=>false;
    internal override string SourceMonsterId=>"wargoatman";
    internal override int SourceRegeneration256=>checked((Prepared?.Snapshot.Birth.BaseLife256??100*256)*NecromancerSummons.Source("wargoatman","DamageRegen"))>>12;
    public override long SourceLife256=>Prepared?.Snapshot.Birth.MaximumLife256??100*256;
    public int MaximumLife256
    {
        get
        {
            long basis=SourceLife256+((long)HitsMaxSeed-CombatProfile.Life)*256;
            return (int)Math.Clamp(basis+ClassResources.Percentage(basis,BattleOrders.LifePercent(this)),256,65000*256);
        }
    }
    public override MonsterProfile CombatProfile=>Prepared is{} s
        ?new(s.Snapshot.Birth.PetLevel,(s.Snapshot.Birth.MaximumLife256+255)/256,s.Snapshot.Birth.ProjectedAttack,s.Snapshot.Birth.ProjectedArmor,0,0,1000,400,0,0,0,0,0)
        :new(1,100,0,100,0,0,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(0,0);
    public WarlockGoatmanBody()
    {
        Name="Summoned Goatman";ApplyAppearance();CantWalk=true;ControlSlots=0;
        AIObject?.AITimer.Stop();SetStr(0);SetDex(0);SetInt(0);SetHits(100);SetDamage(0,0);
    }
    private void ApplyAppearance()
    {
        var appearance=WarlockDemonAppearance.For(CombatAbility.SummonGoatman);
        Body=appearance.Body;BaseSoundID=appearance.Sound;
        if(Name=="Summoned Goatman [placeholder]"){Name="Summoned Goatman";}
    }
    public override TimeSpan BeginMelee(Mobile target)=>throw new NotSupportedException("Prepared Goatman body has no automatic GenericPet combat controller");
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    internal static void Validate(GoatmanBodyState state)
    {
        try
        {
            if(state?.Snapshot?.Birth is not{OwnerLevel:>=1 and <=99,PetLevel:>=1 and <=99,Suppliers:{InheritedPhysical5:0,InheritedPhysical7:0}}
                ||!((Serial)state.Owner).IsMobile||!Enum.IsDefined(state.Difficulty)||state.Snapshot.Birth.Difficulty!=state.Difficulty
                ||state.NextRecoveryUtc.Kind!=DateTimeKind.Utc||state.NextRecoveryUtc>DateTime.MaxValue.AddMilliseconds(-40)
                ||state.Profile!="prepared-goatman-life-owner-v1"||GoatmanPrepared.Rescale(state.Snapshot,state.Difficulty)!=state.Snapshot)
            {throw new InvalidDataException("Invalid prepared Goatman life/owner state");}
            GoatmanNpcBodyActions.Validate(state.Frenzy,state.Snapshot.Birth.Suppliers.FrenzyRank);
        }
        catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported prepared Goatman snapshot",e);}
    }
    internal void RestorePrepared(GoatmanBodyState state)
    {Validate(state);string json=JsonSerializer.Serialize(state);_prepared=state;_saved=json;}
    internal static WarlockGoatmanBody CreatePrepared(PlayerMobile owner,Point3D point,DateTime at,GenericPetBirthBinding startup=null)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-40)||owner?.Map==null||owner.Map==Map.Internal||!owner.Map.CanSpawnMobile(point))
        {throw new ArgumentException("Goatman needs a legal world point and UTC clock");}
        var snapshot=GoatmanPrepared.Capture(owner);
        if(snapshot.Birth.Suppliers is not{InheritedPhysical5:0,InheritedPhysical7:0})
        {throw new NotSupportedException("Goatman duplicate nonzero inherited sunder aggregation remains unverified");}
        var state=new GoatmanBodyState(owner.Serial.Value,snapshot,SummonDifficulties.For(owner.Map),at.AddMilliseconds(40));Validate(state);var body=new WarlockGoatmanBody();
        try
        {
            body.RestorePrepared(state);body.Initialize(owner,snapshot.Birth.Suppliers.Rank,0);body.SetResistances();
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Goatman control master refused");}
            BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);body.MoveToWorld(point,owner.Map);
            body.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(body,body.MaximumLife256);startup?.RegisterNew(body);return body;
        }
        catch{DeathMarkWorld.Remove(body);body.Delete();throw;}
    }
    internal CreatureResistances BodyResistances=>SourceResistances();
    internal CreatureResistances SourceResistances(MonsterDifficulty? difficulty=null)
    {
        var d=difficulty??SummonDifficulty;
        return new(checked(NecromancerSummons.Source("wargoatman","ResDm",d)+(Prepared?.Snapshot.Birth.Suppliers.PhysicalResistBonus??0)),
            NecromancerSummons.Source("wargoatman","ResMa",d),NecromancerSummons.Source("wargoatman","ResFi",d),
            NecromancerSummons.Source("wargoatman","ResLi",d),NecromancerSummons.Source("wargoatman","ResCo",d),NecromancerSummons.Source("wargoatman","ResPo",d));
    }
    private void SetResistances()
    {
        var r=BodyResistances;SetResistance(ResistanceType.Physical,r.Physical??0);SetResistance(ResistanceType.Fire,r.Fire??0);
        SetResistance(ResistanceType.Cold,r.Cold??0);SetResistance(ResistanceType.Energy,r.Lightning??0);SetResistance(ResistanceType.Poison,r.Poison??0);
    }
    internal override void RefreshDifficulty()
    {
        if(Prepared is{} s){RestorePrepared(s with{Difficulty=SummonDifficulty,Snapshot=GoatmanPrepared.Rescale(s.Snapshot,SummonDifficulty)});}
        base.RefreshDifficulty();SetResistances();
    }
    // Explicit frame recovery only; an eventual world controller owns this clock.
    // No automatic movement/combat/healing timer is registered by the body.
    internal void AdvanceRecovery(DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Goatman clock must be UTC");}
        if(Prepared is{} state&&WarlockDemonRecovery.Advance(this,state.Owner,state.NextRecoveryUtc,at,actions) is{} next)
        {RestorePrepared(Prepared with{NextRecoveryUtc=next});}
    }
    [AfterDeserialization]private void AfterGoatmanLoad()
    {AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<GoatmanBodyState>(_saved);Validate(_prepared);ApplyAppearance();}
}
