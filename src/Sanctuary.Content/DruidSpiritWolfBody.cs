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

internal sealed record SpiritWolfBodyState(uint Owner,SpiritWolfSnapshot Snapshot,MonsterDifficulty Difficulty,DateTime NextRecoveryUtc)
{
    public string Profile{get;init;}="prepared-life-and-ownership-v1";
    public SpiritWolfWorldState Controller{get;init;}
    public bool LearnedCast{get;init;}
}

// Saved, vulnerable summon backend. Native AI/swing are suppressed; the
// explicit source-frame controller owns movement and attacks. Internal
// preparation can still create life-only bodies without learned casting.
[SerializationGenerator(0)]
public partial class DruidSpiritWolfBody:SkeletonGuardian,ISourceLifeMaximum
{
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private SpiritWolfBodyState _prepared;
    internal bool ControllerLoaded{get;set;}
    // User-observed3.3.93847eligibility. Keep the old coldeffect metadata;
    // this gate does not assert cold-damage immunity or freeze eligibility.
    internal static bool ObservedChillImmune(Mobile actor)=>actor is DruidSpiritWolfBody{SourceMonsterId:"spiritwolf"};
    internal SpiritWolfBodyState Prepared
    {
        get
        {
            var s=_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<SpiritWolfBodyState>(_saved));
            return s?.Controller is{} c?s with{Controller=DruidSpiritWolfWorld.Copy(c)}:s;
        }
    }
    internal string SavedText=>_saved;
    public override DiabloClass SummonClass=>DiabloClass.Druid;
    public override bool Permitted(PlayerMobile owner)=>ClassCreation.Profiles.Histories(owner.Serial)
        .Any(x=>x.Profile.Class==DiabloClass.Druid && x.Ranks.GetValueOrDefault("druid.summonspiritwolf")>0);
    public override bool CanRegenStam=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool BleedImmune=>false;
    public override Poison PoisonImmune=>null;
    public override bool IsDispellable=>false;
    internal override string SourceMonsterId=>"spiritwolf";
    internal override int SourceRegeneration256=>Prepared?.Snapshot.Birth.Regeneration256??32;
    internal override int SourceDefensePercent=>Prepared?.Snapshot.Birth.ArmorPercent??0;
    public override long SourceLife256=>Prepared?.Snapshot.Birth.Life256??130*256;
    public int MaximumLife256
    {
        get
        {
            long basis=SourceLife256+((long)HitsMaxSeed-CombatProfile.Life)*256;
            return (int)Math.Clamp(basis+ClassResources.Percentage(basis,BattleOrders.Percent(this)),256,65000*256);
        }
    }
    public override MonsterProfile CombatProfile=>Prepared is{} s
        ?new(s.Snapshot.Birth.Level,(s.Snapshot.Birth.Life256+255)/256,s.Snapshot.Birth.BaseAttack+s.Snapshot.Birth.AttackBonus,
            s.Snapshot.Birth.BaseDefense,0,0,1000,400,0,0,0,0,0)
        :new(1,130,0,67,0,0,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(0,0);
    public DruidSpiritWolfBody()
    {
        Name="Spirit Wolf [placeholder]";Body=0xE1;BaseSoundID=-1;CantWalk=true;ControlSlots=0;
        AIObject?.AITimer.Stop();SetHits(130);SetDamage(0,0);
    }
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromSeconds(1);
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    internal static void Validate(SpiritWolfBodyState state)
    {
        try
        {
            if(state?.Snapshot?.Birth is not{Rank:>=1 and <=255,Level:>=1 and <=99,ColdSunder:0}
                || !((Serial)state.Owner).IsMobile || state.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
                || state.NextRecoveryUtc.Kind!=DateTimeKind.Utc || state.NextRecoveryUtc==DateTime.MaxValue
                || state.Profile!="prepared-life-and-ownership-v1"
                || DruidSpiritWolfStats.Rescale(state.Snapshot,state.Difficulty)!=state.Snapshot)
            {throw new InvalidDataException("Invalid prepared Spirit Wolf life/ownership state");}
            if(state.LearnedCast && state.Controller==null){throw new InvalidDataException("Learned wolf requires its world controller");}
            if(state.Controller!=null){DruidSpiritWolfWorld.Validate(state.Controller);}
        }
        catch(Exception e) when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported prepared Spirit Wolf snapshot",e);}
    }
    internal void RestorePrepared(SpiritWolfBodyState state)
    {Validate(state);var copy=state.Controller is{} c?state with{Controller=DruidSpiritWolfWorld.Copy(DruidSpiritWolfWorld.Upgrade(c)) with{Profile="source-ai-native-ground-fit-cold-hit-v2"}}:state;string json=JsonSerializer.Serialize(copy);_prepared=copy;_saved=json;}
    internal static DruidSpiritWolfBody CreatePrepared(PlayerMobile owner,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc || at> DateTime.MaxValue.AddMilliseconds(-40) || owner?.Map==null || owner.Map==Map.Internal
            || !owner.Map.CanSpawnMobile(point))
        {throw new ArgumentException("Spirit Wolf needs a legal world point and UTC clock");}
        var snapshot=DruidSpiritWolfStats.Capture(owner);
        if(snapshot.Birth.ColdSunder!=0){throw new NotSupportedException("Spirit Wolf cold-sunder consumer remains unverified");}
        var state=new SpiritWolfBodyState(owner.Serial.Value,snapshot,SummonDifficulties.For(owner.Map),at.AddMilliseconds(40));
        Validate(state);var body=new DruidSpiritWolfBody();
        try
        {
            body.RestorePrepared(state);body.Initialize(owner,snapshot.Birth.Rank,0);body.SetResistances();
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Spirit Wolf control master refused");}
            BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);body.MoveToWorld(point,owner.Map);
            body.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(body,body.MaximumLife256);return body;
        }
        catch{body.Delete();throw;}
    }
    private void SetResistances()
    {
        var birth=Prepared.Snapshot.Birth;
        SetResistance(ResistanceType.Physical,0);SetResistance(ResistanceType.Fire,birth.FireResistance);
        SetResistance(ResistanceType.Cold,birth.ColdResistance);SetResistance(ResistanceType.Energy,birth.LightningResistance);
        SetResistance(ResistanceType.Poison,birth.PoisonResistance);
    }
    internal override void RefreshDifficulty()
    {
        if(Prepared is{} state){RestorePrepared(state with{Difficulty=SummonDifficulty,Snapshot=DruidSpiritWolfStats.Rescale(state.Snapshot,SummonDifficulty)});}
        base.RefreshDifficulty();SetResistances();
    }
    // The prepared body has no automatic AI timer yet. This bounded life clock
    // advances only at explicit actor frames; offline frames are discarded.
    internal void AdvanceRecovery(DateTime at,bool actions)
    {
        if(Prepared?.Controller!=null){throw new InvalidOperationException("Actor recovery belongs to its source-frame controller");}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Spirit Wolf clock must be UTC");}
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
    [AfterDeserialization]private void AfterWolfLoad()
    {AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<SpiritWolfBodyState>(_saved);Validate(_prepared);if(_prepared.Controller!=null){RestorePrepared(_prepared);ControllerLoaded=true;DruidSpiritWolves.Track(this);}}
    public override void SuspendActions()
    {
        base.SuspendActions();
        if(Prepared is{Controller:{} c} s){RestorePrepared(s with{Controller=DruidSpiritWolfWorld.Park(c)});}
    }
    internal override void InterruptForRecovery()
    {
        base.InterruptForRecovery();
        if(Prepared is{Controller:{} c} s){RestorePrepared(s with{Controller=DruidSpiritWolfWorld.Park(c)});}
    }
    public override void OnAfterDelete(){DruidSpiritWolves.Forget(this);base.OnAfterDelete();}
}
