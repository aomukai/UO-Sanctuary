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

internal sealed record GrizzlyBodyState(uint Owner,GrizzlySnapshot Snapshot,MonsterDifficulty Difficulty,DateTime NextRecoveryUtc)
{
    public string Profile{get;init;}="prepared-life-and-ownership-v1";
}

// Saved vulnerable noRatio bear backend. Reuse the existing serialized
// SkeletonGuardian chain and owner/life/difficulty services. Native skeleton
// AI and damage stay suppressed; AI112/world/BearSmite are separate consumers.
[SerializationGenerator(0)]
public partial class DruidGrizzlyBody:SkeletonGuardian,ISourceLifeMaximum
{
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private GrizzlyBodyState _prepared;
    internal GrizzlyBodyState Prepared=>_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<GrizzlyBodyState>(_saved));
    internal string SavedText=>_saved;
    public override DiabloClass SummonClass=>DiabloClass.Druid;
    public override bool Permitted(PlayerMobile owner)=>ClassCreation.Profiles.Histories(owner.Serial)
        .Any(x=>x.Profile.Class==DiabloClass.Druid&&x.Ranks.GetValueOrDefault("druid.summongrizzly")>0);
    public override bool CanRegenStam=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool BleedImmune=>false;
    public override Poison PoisonImmune=>null;
    public override bool IsDispellable=>false;
    internal override string SourceMonsterId=>"druidbear";
    internal override int SourceRegeneration256=>Prepared?.Snapshot.Birth.Regeneration256??234;
    internal override int SourceDefensePercent=>Prepared?.Snapshot.Birth.ArmorPercent??0;
    public override long SourceLife256=>Prepared?.Snapshot.Birth.Life256??750*256;
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
            checked(s.Snapshot.Birth.BaseAttack+s.Snapshot.Birth.AttackBonus),
            s.Snapshot.Birth.BaseDefense,0,0,1000,400,0,0,0,0,0)
        :new(1,750,0,245,0,0,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(0,0);
    public DruidGrizzlyBody()
    {
        Name="Grizzly [placeholder]";Body=0xD4;BaseSoundID=-1;CantWalk=true;ControlSlots=0;
        AIObject?.AITimer.Stop();SetHits(750);SetDamage(0,0);
    }
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromSeconds(1);
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    internal static void Validate(GrizzlyBodyState state)
    {
        try
        {
            if(state?.Snapshot?.Birth is not{Rank:>=1 and <=255,Level:>=1 and <=99,PhysicalSunder:0}
                ||!((Serial)state.Owner).IsMobile||state.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
                ||state.NextRecoveryUtc.Kind!=DateTimeKind.Utc||state.NextRecoveryUtc==DateTime.MaxValue
                ||state.Profile!="prepared-life-and-ownership-v1"
                ||DruidGrizzlyStats.Rescale(state.Snapshot,state.Difficulty)!=state.Snapshot)
            {throw new InvalidDataException("Invalid prepared Grizzly life/ownership state");}
        }
        catch(Exception e) when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported prepared Grizzly snapshot",e);}
    }
    internal void RestorePrepared(GrizzlyBodyState state)
    {Validate(state);string json=JsonSerializer.Serialize(state);_prepared=state;_saved=json;}
    internal static DruidGrizzlyBody CreatePrepared(PlayerMobile owner,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-40)||owner?.Map==null||owner.Map==Map.Internal
            ||!owner.Map.CanSpawnMobile(point))
        {throw new ArgumentException("Grizzly needs a legal world point and UTC clock");}
        var snapshot=DruidGrizzlyStats.Capture(owner);
        if(snapshot.Birth.PhysicalSunder!=0){throw new NotSupportedException("Grizzly physical-sunder consumer remains unverified");}
        var state=new GrizzlyBodyState(owner.Serial.Value,snapshot,SummonDifficulties.For(owner.Map),at.AddMilliseconds(40));
        Validate(state);var body=new DruidGrizzlyBody();
        try
        {
            body.RestorePrepared(state);body.Initialize(owner,snapshot.Birth.Rank,0);body.SetResistances();
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Grizzly control master refused");}
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
        if(Prepared is{} state){RestorePrepared(state with{Difficulty=SummonDifficulty,Snapshot=DruidGrizzlyStats.Rescale(state.Snapshot,SummonDifficulty)});}
        base.RefreshDifficulty();SetResistances();
    }
    // Life-only preparation advances at explicit actor frames. No automatic
    // AI/movement/attack or offline recovery is enabled by this factory.
    internal void AdvanceRecovery(DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Grizzly clock must be UTC");}
        if(Deleted||Prepared is not{} state||state.NextRecoveryUtc>at){return;}
        if(ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner||owner.Serial.Value!=state.Owner){Delete();return;}
        long frames=(at-state.NextRecoveryUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
        var next=state.NextRecoveryUtc.AddTicks(checked(frames*TimeSpan.FromMilliseconds(40).Ticks));
        if(!PersistentMinions.Active(owner,this)||!Permitted(owner))
        {PersistentMinions.ParkClass(owner,DiabloClass.Druid);RestorePrepared(Prepared with{NextRecoveryUtc=next});return;}
        if(!actions||owner.Map==null||owner.Map==Map.Internal)
        {PersistentMinions.Suspend(owner);RestorePrepared(Prepared with{NextRecoveryUtc=next});return;}
        if(Map==Map.Internal){PersistentMinions.Resume(owner);}
        if(Map!=Map.Internal&&Alive&&!Poisoned&&TimedEffects.CanRegenerateHits(this)&&!SkillHealth.Full(this))
        {SkillHealth.Heal(this,(int)Math.Min(int.MaxValue,frames*BattleOrders.Regeneration256(this)));}
        RestorePrepared(Prepared with{NextRecoveryUtc=next});AIObject?.AITimer.Stop();
    }
    [AfterDeserialization]private void AfterBearLoad()
    {AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<GrizzlyBodyState>(_saved);Validate(_prepared);}
}
