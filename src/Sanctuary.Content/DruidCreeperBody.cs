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

internal sealed record CreeperBodyState(uint Owner,CreeperSnapshot Snapshot,MonsterDifficulty Difficulty,DateTime NextRecoveryUtc)
{
    public string Profile{get;init;}="prepared-noRatio-life-and-owner-v1";
    public CreeperWorldState Controller{get;init;}
}

// Internal saved vulnerable life/owner backend. No live VINE_BEAST state,
// movement, skill/mats or learned cast is implied by this visible placeholder.
[SerializationGenerator(0)]
public partial class DruidCreeperBody:SkeletonGuardian,ISourceLifeMaximum
{
    [SerializableField(0,setter:"private")]private string _saved="{}";
    private CreeperBodyState _prepared;
    internal bool ControllerLoaded{get;set;}
    internal CreeperBodyState Prepared
    {
        get
        {
            var s=_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<CreeperBodyState>(_saved));
            return s?.Controller is{} c?s with{Controller=DruidCreeperWorld.Copy(c)}:s;
        }
    }
    internal string SavedText=>_saved;
    public override DiabloClass SummonClass=>DiabloClass.Druid;
    public override bool Permitted(PlayerMobile owner)=>ClassCreation.Profiles.Histories(owner.Serial)
        .Any(x=>x.Profile.Class==DiabloClass.Druid && x.Ranks.GetValueOrDefault("druid.poisoncreeper")>0);
    public override bool CanRegenStam=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool BleedImmune=>false;
    public override Poison PoisonImmune=>null;
    public override bool IsDispellable=>false;
    internal override string SourceMonsterId=>"plaguepoppy";
    internal override int SourceRegeneration256=>Prepared?.Snapshot.Birth.Regeneration256??9;
    public override long SourceLife256=>Prepared?.Snapshot.Birth.Life256??50*256;
    public int MaximumLife256
    {
        get
        {
            long basis=SourceLife256+((long)HitsMaxSeed-CombatProfile.Life)*256;
            return (int)Math.Clamp(basis+ClassResources.Percentage(basis,BattleOrders.LifePercent(this)),256,65000*256);
        }
    }
    public override MonsterProfile CombatProfile=>Prepared is{} s
        ?new(s.Snapshot.Birth.Level,(s.Snapshot.Birth.Life256+255)/256,s.Snapshot.Birth.Attack,
            s.Snapshot.Birth.Defense,0,0,1000,400,0,0,0,0,0)
        :new(1,50,0,25,0,0,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(0,0);
    public DruidCreeperBody()
    {
        Name="Poison Creeper [placeholder]";Body=0x8;BaseSoundID=-1;CantWalk=true;ControlSlots=0;
        AIObject?.AITimer.Stop();SetHits(50);SetDamage(0,0);
    }
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromSeconds(1);
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    internal static void Validate(CreeperBodyState state)
    {
        try
        {
            if(state?.Snapshot?.Birth is not{Rank:>=1 and <=255,Level:>=1 and <=329,Pierce:0}
                || !((Serial)state.Owner).IsMobile || state.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
                || state.NextRecoveryUtc.Kind!=DateTimeKind.Utc || state.NextRecoveryUtc==DateTime.MaxValue
                || state.Profile!="prepared-noRatio-life-and-owner-v1"
                || DruidCreeperStats.Rescale(state.Snapshot,state.Difficulty)!=state.Snapshot)
            {throw new InvalidDataException("Invalid prepared Creeper life/ownership state");}
            if(state.Controller!=null){DruidCreeperWorld.Validate(state.Controller);}
        }
        catch(Exception e) when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported prepared Creeper snapshot",e);}
    }
    internal void RestorePrepared(CreeperBodyState state)
    {Validate(state);var copy=state.Controller is{} c?state with{Controller=DruidCreeperWorld.Copy(c)}:state;string json=JsonSerializer.Serialize(copy);_prepared=copy;_saved=json;}
    internal static DruidCreeperBody CreatePrepared(PlayerMobile owner,Point3D point,DateTime at)
    {
        // Source footprint0does not reserve a mobile's space. Native16height/
        // surface geometry remains the declared terrain projection.
        if(at.Kind!=DateTimeKind.Utc || at>DateTime.MaxValue.AddMilliseconds(-40) || owner?.Map==null || owner.Map==Map.Internal
            || !PetGroundWorld.ZeroFootprint(owner.Map,point))
        {throw new ArgumentException("Creeper needs a legal prepared ground point and UTC clock");}
        var snapshot=DruidCreeperStats.Capture(owner);
        if(snapshot.Birth.Pierce!=0){throw new NotSupportedException("Creeper poison-sunder consumer remains unverified");}
        var state=new CreeperBodyState(owner.Serial.Value,snapshot,SummonDifficulties.For(owner.Map),at.AddMilliseconds(40));
        Validate(state);var body=new DruidCreeperBody();
        try
        {
            body.RestorePrepared(state);body.Initialize(owner,snapshot.Birth.Rank,0);body.SetResistances();
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Creeper control master refused");}
            BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);body.MoveToWorld(point,owner.Map);
            body.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(body,body.MaximumLife256);return body;
        }
        catch{body.Delete();throw;}
    }
    private void SetResistances()
    {
        SetResistance(ResistanceType.Physical,0);SetResistance(ResistanceType.Fire,0);SetResistance(ResistanceType.Cold,0);
        SetResistance(ResistanceType.Energy,0);SetResistance(ResistanceType.Poison,0);
    }
    internal override void RefreshDifficulty()
    {
        if(Prepared is{} state){RestorePrepared(state with{Difficulty=SummonDifficulty,Snapshot=DruidCreeperStats.Rescale(state.Snapshot,SummonDifficulty)});}
        base.RefreshDifficulty();SetResistances();
    }
    // Explicit source-frame recovery preparation. No native timer/AI fallback;
    // offline/inactive-history frames are discarded without healing or replay.
    internal void AdvanceRecovery(DateTime at,bool actions)
    {
        if(Prepared?.Controller!=null){throw new InvalidOperationException("Creeper recovery belongs to its world controller");}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Creeper clock must be UTC");}
        if(Deleted || Prepared is not{} state || state.NextRecoveryUtc>at){return;}
        if(ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner || owner.Serial.Value!=state.Owner){Delete();return;}
        long frames=(at-state.NextRecoveryUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
        var next=state.NextRecoveryUtc.AddTicks(checked(frames*TimeSpan.FromMilliseconds(40).Ticks));
        if(!PersistentMinions.Active(owner,this) || !Permitted(owner))
        {PersistentMinions.ParkClass(owner,DiabloClass.Druid);RestorePrepared(Prepared with{NextRecoveryUtc=next});return;}
        if(!actions || owner.Map==null || owner.Map==Map.Internal)
        {PersistentMinions.Suspend(owner);RestorePrepared(Prepared with{NextRecoveryUtc=next});return;}
        if(Map==Map.Internal){PersistentMinions.Resume(owner);}
        if(Map!=Map.Internal && Alive && !Poisoned && TimedEffects.CanRegenerateHits(this) && !SkillHealth.Full(this))
        {SkillHealth.Heal(this,(int)Math.Min(int.MaxValue,frames*BattleOrders.Regeneration256(this)));}
        RestorePrepared(Prepared with{NextRecoveryUtc=next});AIObject?.AITimer.Stop();
    }
    [AfterDeserialization]private void AfterCreeperLoad()
    {AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<CreeperBodyState>(_saved);Validate(_prepared);if(_prepared.Controller!=null){RestorePrepared(_prepared);ControllerLoaded=true;DruidCreepers.Track(this);}}
    public override void SuspendActions()
    {
        base.SuspendActions();if(Prepared is{Controller:{} c} s){RestorePrepared(s with{Controller=DruidCreeperWorld.Park(c)});}
    }
    public override void OnAfterDelete(){DruidCreepers.Forget(this);base.OnAfterDelete();}
}
