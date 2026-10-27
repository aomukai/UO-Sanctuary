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

internal sealed record DireWolfBodyState(uint Owner,DireWolfSnapshot Snapshot,MonsterDifficulty Difficulty,DateTime NextRecoveryUtc)
{
    public string Profile{get;init;}="prepared-life-and-ownership-v1";
    public DireWolfWorldState Controller{get;init;}
    public bool LearnedCast{get;init;}
}

// Saved vulnerable Fenris backend. Native skeleton AI and damage are
// suppressed. This factory prepares life/ownership; automatic Fenris policy,
// attack and corpse/rage dispatch are separate consumers still to be connected.
[SerializationGenerator(0)]
public partial class DruidDireWolfBody:SkeletonGuardian,ISourceLifeMaximum
{
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private DireWolfBodyState _prepared;
    internal bool ControllerLoaded{get;set;}
    internal DireWolfBodyState Prepared
    {
        get
        {
            var s=_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<DireWolfBodyState>(_saved));
            return s?.Controller is{} c?s with{Controller=DruidDireWolfWorld.Copy(c)}:s;
        }
    }
    internal string SavedText=>_saved;
    public override DiabloClass SummonClass=>DiabloClass.Druid;
    public override bool Permitted(PlayerMobile owner)=>ClassCreation.Profiles.Histories(owner.Serial)
        .Any(x=>x.Profile.Class==DiabloClass.Druid && x.Ranks.GetValueOrDefault("druid.summondirewolf")>0);
    public override bool CanRegenStam=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool BleedImmune=>false;
    public override Poison PoisonImmune=>null;
    public override bool IsDispellable=>false;
    internal override string SourceMonsterId=>"fenris";
    internal override int SourceRegeneration256=>Prepared?.Snapshot.Birth.Regeneration256??54;
    internal override int SourceDefensePercent=>Prepared?.Snapshot.Birth.ArmorPercent??0;
    public override long SourceLife256=>Prepared?.Snapshot.Birth.Life256??216*256;
    public int MaximumLife256
    {
        get
        {
            long basis=SourceLife256+((long)HitsMaxSeed-CombatProfile.Life)*256;
            return (int)Math.Clamp(basis+ClassResources.Percentage(basis,BattleOrders.LifePercent(this)),256,65000*256);
        }
    }
    public override MonsterProfile CombatProfile=>Prepared is{} s
        ?new(s.Snapshot.Birth.Level,(s.Snapshot.Birth.Life256+255)/256,
            checked(s.Snapshot.Birth.BaseAttack+s.Snapshot.Birth.AttackBonus+(s.Controller is{Mode:4,AttackPacket:{} packet}?packet.Attack:0)),
            s.Snapshot.Birth.BaseDefense,0,0,1000,400,0,0,0,0,0)
        :new(1,216,0,116,0,0,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(0,0);
    public DruidDireWolfBody()
    {
        Name="Dire Wolf [placeholder]";Body=0xE1;BaseSoundID=-1;CantWalk=true;ControlSlots=0;
        AIObject?.AITimer.Stop();SetHits(216);SetDamage(0,0);
    }
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromSeconds(1);
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    internal static void Validate(DireWolfBodyState state)
    {
        try
        {
            if(state?.Snapshot?.Birth is not{Rank:>=1 and <=255,Level:>=1 and <=99,PhysicalSunder:0}
                || !((Serial)state.Owner).IsMobile || state.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
                || state.NextRecoveryUtc.Kind!=DateTimeKind.Utc || state.NextRecoveryUtc==DateTime.MaxValue
                || state.Profile!="prepared-life-and-ownership-v1"
                || DruidDireWolfStats.Rescale(state.Snapshot,state.Difficulty)!=state.Snapshot)
            {throw new InvalidDataException("Invalid prepared Dire Wolf life/ownership state");}
            if(state.LearnedCast && state.Controller==null){throw new InvalidDataException("Learned Dire Wolf requires its controller");}
            if(state.Controller is{} controller)
            {
                DruidDireWolfWorld.Validate(controller);
                if(controller.AttackPacket is{} packet && packet!=DruidBeastAttackStats.Capture(
                    new(SkillMechanics.For(CombatAbility.SummonDireWolf),_=>packet.Rank,_=>0,state.Snapshot.Birth.Level),state.Difficulty,4))
                {throw new InvalidDataException("Invalid captured Fenris mode stats");}
            }
        }
        catch(Exception e) when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported prepared Dire Wolf snapshot",e);}
    }
    internal void RestorePrepared(DireWolfBodyState state)
    {Validate(state);var copy=state.Controller is{} c?state with{Controller=DruidDireWolfWorld.Copy(c)}:state;string json=JsonSerializer.Serialize(copy);_prepared=copy;_saved=json;}
    internal static DruidDireWolfBody CreatePrepared(PlayerMobile owner,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc || at> DateTime.MaxValue.AddMilliseconds(-40) || owner?.Map==null || owner.Map==Map.Internal
            || !owner.Map.CanSpawnMobile(point))
        {throw new ArgumentException("Dire Wolf needs a legal world point and UTC clock");}
        var snapshot=DruidDireWolfStats.Capture(owner);
        if(snapshot.Birth.PhysicalSunder!=0){throw new NotSupportedException("Dire Wolf physical-sunder consumer remains unverified");}
        var state=new DireWolfBodyState(owner.Serial.Value,snapshot,SummonDifficulties.For(owner.Map),at.AddMilliseconds(40));
        Validate(state);var body=new DruidDireWolfBody();
        try
        {
            body.RestorePrepared(state);body.Initialize(owner,snapshot.Birth.Rank,0);body.SetResistances();
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Dire Wolf control master refused");}
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
        if(Prepared is{} state){RestorePrepared(state with{Difficulty=SummonDifficulty,Snapshot=DruidDireWolfStats.Rescale(state.Snapshot,SummonDifficulty)});}
        base.RefreshDifficulty();SetResistances();
    }
    // The prepared body has no automatic AI timer yet. This bounded life clock
    // advances only at explicit actor frames; offline frames are discarded.
    internal void AdvanceRecovery(DateTime at,bool actions)
    {
        if(Prepared?.Controller!=null){throw new InvalidOperationException("Dire actor recovery belongs to the world clock");}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Dire Wolf clock must be UTC");}
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
    {AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<DireWolfBodyState>(_saved);Validate(_prepared);if(_prepared.Controller!=null){ControllerLoaded=true;DruidDireWolves.Track(this);}}
    public override void SuspendActions()
    {base.SuspendActions();if(Prepared is{Controller:{} c} s){RestorePrepared(s with{Controller=DruidDireWolfWorld.Park(c)});}}
    internal override void InterruptForRecovery()
    {base.InterruptForRecovery();if(Prepared is{Controller:{} c} s){RestorePrepared(s with{Controller=DruidDireWolfWorld.Park(c)});}}
    public override void OnAfterDelete(){DruidDireWolves.Forget(this);base.OnAfterDelete();}
}
