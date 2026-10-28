// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
using Server.Logging;
namespace Sanctuary;

internal sealed record RecyclerBodyState(uint Owner,RecyclerSnapshot Snapshot,MonsterDifficulty Difficulty,DateTime NextRecoveryUtc)
{
    public string Profile{get;init;}="prepared-recycler-life-owner-v1";
    public RecyclerWorldState Controller{get;init;}
}

// Saved life/ownership backend. Visible, vulnerable and silent placeholders;
// underground exposure and learned casting remain separate.
[SerializationGenerator(0)]
public partial class DruidRecyclerBody:SkeletonGuardian,ISourceLifeMaximum
{
    [SerializableField(0,setter:"private")]private string _saved="{}";
    private RecyclerBodyState _prepared;
    private bool _loaded;
    internal bool ControllerLoaded{get;set;}
    internal RecyclerBodyState Prepared
    {
        get
        {
            var s=_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<RecyclerBodyState>(_saved));
            return s?.Controller is{} c?s with{Controller=DruidRecyclerWorld.Copy(c)}:s;
        }
    }
    internal string SavedText=>_saved;
    internal CombatAbility VineAbility=>Prepared?.Snapshot.Birth.Ability??CombatAbility.CarrionVine;
    public override DiabloClass SummonClass=>DiabloClass.Druid;
    public override bool Permitted(PlayerMobile owner)=>Prepared!=null&&ClassCreation.Profiles.Histories(owner.Serial)
        .Any(h=>h.Profile.Class==DiabloClass.Druid&&h.Ranks.GetValueOrDefault(DruidRecyclerStats.Talent(VineAbility))>0);
    public override bool CanRegenStam=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool BleedImmune=>false;
    public override Poison PoisonImmune=>null;
    public override bool IsDispellable=>false;
    internal override string SourceMonsterId=>DruidRecyclerStats.Source(VineAbility);
    internal override int SourceRegeneration256=>Prepared?.Snapshot.Birth.Regeneration256??17;
    public override long SourceLife256=>Prepared?.Snapshot.Birth.Life256??95*256;
    public int MaximumLife256
    {
        get
        {
            long basis=SourceLife256+((long)HitsMaxSeed-CombatProfile.Life)*256;
            return (int)Math.Clamp(basis+ClassResources.Percentage(basis,BattleOrders.LifePercent(this)),256,65000*256);
        }
    }
    public override MonsterProfile CombatProfile=>Prepared is{} s
        ?new(s.Snapshot.Birth.Level,(s.Snapshot.Birth.Life256+255)/256,s.Snapshot.Birth.Attack,s.Snapshot.Birth.Defense,0,0,1000,400,0,0,0,0,0)
        :new(1,95,0,92,0,0,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(0,0);
    public DruidRecyclerBody()
    {Name="Recycler Vine [placeholder]";Body=0x8;BaseSoundID=-1;CantWalk=true;ControlSlots=0;AIObject?.AITimer.Stop();SetHits(95);SetDamage(0,0);}
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromSeconds(1);
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    internal static void Validate(RecyclerBodyState state)
    {
        try
        {
            if(state?.Snapshot?.Birth is not{Rank:>=1 and <=255,Level:>=1 and <=329}
                ||!((Serial)state.Owner).IsMobile||state.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
                ||state.NextRecoveryUtc.Kind!=DateTimeKind.Utc||state.NextRecoveryUtc==DateTime.MaxValue
                ||state.Profile!="prepared-recycler-life-owner-v1"||DruidRecyclerStats.Rescale(state.Snapshot,state.Difficulty)!=state.Snapshot)
            {throw new InvalidDataException("Invalid recycler body snapshot");}
            if(state.Controller!=null){DruidRecyclerWorld.Validate(state.Controller);}
        }
        catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported recycler body snapshot",e);}
    }
    internal void RestorePrepared(RecyclerBodyState state)
    {Validate(state);var copy=state.Controller is{} c?state with{Controller=DruidRecyclerWorld.Copy(c)}:state;string text=JsonSerializer.Serialize(copy);_prepared=copy;_saved=text;}
    internal static DruidRecyclerBody CreatePrepared(PlayerMobile owner,CombatAbility ability,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-40)||owner?.Map==null||owner.Map==Map.Internal
            ||!PetGroundWorld.ZeroFootprint(owner.Map,point)){throw new ArgumentException("Recycler needs a legal ground point and UTC clock");}
        var snapshot=DruidRecyclerStats.Capture(owner,ability);
        var state=new RecyclerBodyState(owner.Serial.Value,snapshot,SummonDifficulties.For(owner.Map),at.AddMilliseconds(40));Validate(state);
        var body=new DruidRecyclerBody();
        try
        {
            body.RestorePrepared(state);body.Name=ability==CombatAbility.CarrionVine?"Carrion Vine [placeholder]":"Solar Creeper [placeholder]";
            body.Initialize(owner,snapshot.Birth.Rank,0);body.SetResistances();
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Recycler control master refused");}
            BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);body.MoveToWorld(point,owner.Map);
            body.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(body,body.MaximumLife256);DruidRecyclerBodies.Track(body);return body;
        }
        catch{body.Delete();throw;}
    }
    private void SetResistances()
    {SetResistance(ResistanceType.Physical,0);SetResistance(ResistanceType.Fire,0);SetResistance(ResistanceType.Cold,0);SetResistance(ResistanceType.Energy,0);SetResistance(ResistanceType.Poison,0);}
    internal override void RefreshDifficulty()
    {if(Prepared is{} s){RestorePrepared(s with{Difficulty=SummonDifficulty,Snapshot=DruidRecyclerStats.Rescale(s.Snapshot,SummonDifficulty)});}base.RefreshDifficulty();SetResistances();}
    internal void AdvanceRecovery(DateTime at,bool actions)
    {
        if(Prepared?.Controller!=null){throw new InvalidOperationException("Recycler recovery belongs to its world controller");}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Recycler clock must be UTC");}
        if(Deleted||Prepared is not{} s||s.NextRecoveryUtc>at){return;}
        if(ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner||owner.Serial.Value!=s.Owner){Delete();return;}
        long frames=(at-s.NextRecoveryUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
        var next=s.NextRecoveryUtc.AddTicks(checked(frames*TimeSpan.FromMilliseconds(40).Ticks));
        if(_loaded){_loaded=false;RestorePrepared(s with{NextRecoveryUtc=next});return;}
        if(!PersistentMinions.Active(owner,this)||!Permitted(owner)){PersistentMinions.ParkClass(owner,DiabloClass.Druid);RestorePrepared(Prepared with{NextRecoveryUtc=next});return;}
        if(!actions||owner.Map==null||owner.Map==Map.Internal){PersistentMinions.Suspend(owner);RestorePrepared(Prepared with{NextRecoveryUtc=next});return;}
        if(Map==Map.Internal){PersistentMinions.Resume(owner);}
        if(Map!=Map.Internal&&Alive&&!Poisoned&&TimedEffects.CanRegenerateHits(this)&&!SkillHealth.Full(this))
        {SkillHealth.Heal(this,(int)Math.Min(int.MaxValue,frames*BattleOrders.Regeneration256(this)));}
        RestorePrepared(Prepared with{NextRecoveryUtc=next});AIObject?.AITimer.Stop();
    }
    [AfterDeserialization]private void AfterRecyclerLoad()
    {AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<RecyclerBodyState>(_saved);Validate(_prepared);_loaded=true;ControllerLoaded=_prepared.Controller!=null;DruidRecyclerBodies.Track(this);}
    public override void SuspendActions()
    {base.SuspendActions();if(Prepared is{Controller:{} c} s){RestorePrepared(s with{Controller=DruidRecyclerWorld.Park(c)});}}
    public override void OnAfterDelete(){DruidRecyclerBodies.Forget(this);base.OnAfterDelete();}
}

public static class DruidRecyclerBodies
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidRecyclerBodies));
    private static readonly HashSet<uint> _actors=new();
    internal static void Track(DruidRecyclerBody body)=>_actors.Add(body.Serial.Value);
    internal static void Forget(DruidRecyclerBody body)=>_actors.Remove(body.Serial.Value);
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void Tick(DateTime at,bool? actions=null)
    {
        foreach(uint id in _actors.ToArray())
        {
            if(World.FindMobile((Serial)id) is not DruidRecyclerBody{Deleted:false} body){_actors.Remove(id);continue;}
            bool active=actions??body.ControlMaster?.NetState is{Running:true};
            try
            {
                if(body.Prepared?.Controller!=null){DruidRecyclerWorld.Advance(body,at,active);}
                else{body.AdvanceRecovery(at,active);}
            }
            catch(Exception e){DruidRecyclerWorld.Fault(body,e.Message);Log.Error(e,"Recycler controller refused unsupported context for {Serial}",body.Serial);}
        }
    }
}
