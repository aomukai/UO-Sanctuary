// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record BladeBodyState(uint Owner,AssassinTrapCreationStats Creation,ActorMotionState Motion,int Map,int Z,long Frames,
    BladeControlState Control=null,DateTime NextFrameUtc=default,string ControllerFault=null,BladeSpawnContext Spawn=null);

// Prepared world entity. Its source controller can be activated with explicit
// movement stats and a missile-creation service; no learned cast is enabled.
[SerializationGenerator(0)]
public partial class BladeSentinelBody:BaseCreature
{
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private BladeBodyState _prepared;
    internal BladeBodyState Prepared=>_prepared??(string.IsNullOrEmpty(_saved) || _saved=="{}"?null:_prepared=JsonSerializer.Deserialize<BladeBodyState>(_saved));
    public override bool BlocksFit=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool CanBeDamaged()=>false;
    public override bool Commandable=>false;
    public override bool IsBondable=>false;
    public override bool IsDispellable=>false;
    public override bool CanRegenHits=>false;
    public override bool CanRegenMana=>false;
    public override bool CanRegenStam=>false;
    public override bool DeleteCorpseOnDeath=>true;
    public override bool OnMoveOver(Mobile mobile)=>true;
    public override bool IsEnemy(Mobile mobile)=>false;
    public override bool CanBeHarmful(Mobile mobile,bool message,bool ignoreOurBlessedness)=>false;
    internal bool TrapTarget(PlayerMobile owner,Mobile target)=>AssassinTrapTargets.Eligible(this,owner,target,()=>base.CanBeHarmful(target,false,true));
    // Deliberately absent Constructible: the unfinished component has no GM Add
    // or player acquisition path. The serializer still needs its serial ctor.
    public BladeSentinelBody():base(AIType.AI_Melee,FightMode.None,0)
    {
        Name="Blade Sentinel [placeholder]";Body=0x9C;Hue=0x481;Blessed=true;CantWalk=true;
        Summoned=true;ControlSlots=0;NoKillAwards=true;SetStr(0);SetDex(0);SetInt(0);SetStam(0);SetMana(0);SetDamage(0,0);
        AIObject?.AITimer.Stop();
    }
    private static Point3D Position(BladeBodyState s)=>new(
        (int)Math.Round(((long)s.Motion.X-32768)/131072.0,MidpointRounding.AwayFromZero),
        (int)Math.Round(((long)s.Motion.Y-32768)/131072.0,MidpointRounding.AwayFromZero),s.Z);
    private static void Validate(BladeBodyState s)
    {
        if(s?.Creation is not{Rank:>=1 and <=255,Level:>=1 and <=99,Life256:>=256 and <=16640000} c
            || c.Life256%256!=0 || c.Passives is not{Length:14} || c.Skills==null
            || !c.Skills.Any(x=>x.Name=="Blade Sentinel" && x.Value==c.Rank) || c.DurationFrames!=100+12*(c.Rank-1)
            || !((Serial)s.Owner).IsMobile || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal
            || s.Motion?.Points==null || s.Motion.SavedSteps==null || s.Frames<0 || s.Z is <-128 or >127)
        {throw new InvalidDataException("Unsupported prepared blade body state");}
        if(s.Spawn is{} spawn && (spawn.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
            || spawn.Speed<=0 || c.BaseStatsLevel is <1 or >99 || s.Control is{} clock && clock.Speed!=spawn.Speed))
        {throw new InvalidDataException("Invalid saved blade spawn context");}
        var point=Position(s);var map=Map.Maps[s.Map];
        if(point.X<=0 || point.Y<=0 || point.X>=map.Width || point.Y>=map.Height){throw new InvalidDataException("Prepared blade outside world");}
        // Reuse the motion validator without advancing or consulting the world.
        SourceActorMotion.Advance(s.Motion with{Flags=s.Motion.Flags&~0x20},(_,_)=>throw new InvalidOperationException());
        if(s.Control is{} control && (s.NextFrameUtc.Kind!=DateTimeKind.Utc || control.Frame<0 || control.Mode is not(0 or 1 or 2)
            || control.Duration!=c.DurationFrames || control.Speed<=0 || control.Ai==null || control.NextThink<0))
        {throw new InvalidDataException("Invalid saved blade controller");}
    }
    private void Store(BladeBodyState s){Validate(s);_prepared=s;_saved=JsonSerializer.Serialize(s);}
    internal static BladeSentinelBody CreatePrepared(PlayerMobile owner,AssassinTrapCreationStats stats,ActorMotionState motion,int z,BladeSpawnContext spawn=null)
    {
        if(owner is not{Alive:true,Deleted:false} || owner.Map==null || owner.Map==Map.Internal
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Assassin)
        {throw new InvalidOperationException("Prepared blade needs a live Assassin world owner");}
        var s=new BladeBodyState(owner.Serial.Value,stats,motion,owner.Map.MapID,z,0,Spawn:spawn);Validate(s);
        var body=new BladeSentinelBody();body.Store(s);body.SummonMaster=owner;body.SetHits(stats.Life256/256);
        body.MoveToWorld(Position(s),owner.Map);body.AIObject?.AITimer.Stop();return body;
    }
    internal ActorMotionResult AdvancePrepared()
    {
        var before=Prepared;Validate(before);
        if(Deleted || Map?.MapID!=before.Map || World.FindMobile((Serial)before.Owner) is not PlayerMobile{Alive:true,Deleted:false} owner
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Assassin)
        {throw new NotSupportedException("Prepared blade owner/map lifecycle needs full AI integration");}
        var next=SourceActorMotion.Advance(before.Motion,(x,y)=>BladeWorldSpace.Mask(Map,before.Z,x,y,this));
        if(next.RepathRequired){throw new NotSupportedException("Prepared blade successful path repair is not connected");}
        Store(before with{Motion=next.State,Frames=before.Frames+1});var point=Position(Prepared);
        if(Location!=point){MoveToWorld(point,Map);}AIObject?.AITimer.Stop();return next;
    }
    internal ActorPathState BeginPreparedWalk(Point2D target,int distance=20,int pathType=15)
    {
        var before=Prepared;Validate(before);
        if(Deleted || Map?.MapID!=before.Map || World.FindMobile((Serial)before.Owner) is not PlayerMobile{Alive:true,Deleted:false} owner
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Assassin)
        {throw new NotSupportedException("Prepared blade owner/map lifecycle needs full AI integration");}
        if(Map.Width*2>65536 || Map.Height*2>65536){throw new NotSupportedException("Native map exceeds source path coordinates");}
        if(pathType is not(2 or 13 or 15)){throw new NotSupportedException("Prepared blade path type is not connected");}
        // Explicit native-map room adapter. The source keeps fractional position
        // while computing from its integer cell; do not snap the actor here.
        var input=new ActorPathState(before.Motion with{Flags=(before.Motion.Flags&0xFF)|(pathType==15?0x800:pathType==13?0x100:0),Distance=distance,PathType=pathType},target,new(0,0));
        var path=SourceActorPath.Initialize(input,distance,new(0,0,Map.Width*2,Map.Height*2),(x,y)=>BladeWorldSpace.Mask(Map,before.Z,x,y,this));
        Store(before with{Motion=path.Motion});return path;
    }
    internal void StartPreparedController(Point2D destination,int speed,ulong seed,DateTime at,bool hasCommand=true)
    {
        var before=Prepared;Validate(before);
        if(before.Control!=null || at.Kind!=DateTimeKind.Utc){throw new InvalidOperationException("Prepared blade controller already started or invalid clock");}
        var origin=new Point2D((int)(before.Motion.X>>16),(int)(before.Motion.Y>>16));
        var control=BladeSentinelController.Start(origin,destination,before.Creation.DurationFrames,speed,seed) with{HasCommand=hasCommand};
        var motion=before.Motion with{Points=[],Index=0,PathType=15,Flags=0x800,Direction=new(0,0),Vector=new(0,0),Distance=20,Velocity=speed,MaximumVelocity=speed};
        // All blade actors and their missiles share source frame boundaries.
        at=new DateTime(at.Ticks-at.Ticks%(40*TimeSpan.TicksPerMillisecond),DateTimeKind.Utc);
        Store(before with{Control=control,Motion=motion,NextFrameUtc=at.AddMilliseconds(40),ControllerFault=null});BladeSentinelActors.Track(this);
    }
    internal int Passive(string name)=>Prepared.Creation.Passives.Where(x=>x.Name==name).Sum(x=>x.Value);
    internal void RescaleDifficulty(MonsterDifficulty difficulty)
    {
        var before=Prepared;Validate(before);
        if(before.Spawn==null){throw new NotSupportedException("Synthetic prepared blade has no retained spawn difficulty");}
        if(before.Spawn.Difficulty==difficulty){return;}
        var next=BladeSentinelSummons.Rescale(before.Creation,before.Spawn,difficulty);
        int life=SkillHealth.Balance256(this),maximum=SkillHealth.Maximum256(this);
        Store(before with{Creation=next.Creation,Spawn=next.Context});
        if(next.Creation.Life256!=before.Creation.Life256){SetHits(next.Creation.Life256/256);}
        SkillHealth.SetBalance256(this,SummonDifficulties.Life256(life,maximum,SkillHealth.Maximum256(this)));
    }
    internal uint RollSourceRandom()
    {
        var before=Prepared;var control=before.Control??throw new InvalidOperationException("Missing blade seed context");
        ulong seed=control.Seed;uint value=SourceRandom.Roll(ref seed);Store(before with{Control=control with{Seed=seed}});return value;
    }
    internal void AdvanceController(DateTime at,bool allowActions=true)
    {
        while(!Deleted && Prepared is{Control:not null} before && before.NextFrameUtc<=at)
        {
            Validate(before);
            if(!allowActions || before.ControllerFault!=null)
            {
                // Approved offline policy: freeze actions/pose/random draws,
                // never the lifetime. No missed attacks are replayed on login.
                var control=before.Control;int offlineFrame=checked(control.Frame+1);
                int expiry=control.Ai.Expiry<0?checked(control.Duration+1):control.Ai.Expiry;
                Store(before with{Control=control with{Frame=offlineFrame,NextThink=checked(offlineFrame+1),Ai=control.Ai with{Expiry=expiry}},
                    NextFrameUtc=before.NextFrameUtc.AddMilliseconds(40)});
                if(offlineFrame>expiry){Delete();}continue;
            }
            if(Map?.MapID!=before.Map || World.FindMobile((Serial)before.Owner) is not PlayerMobile{Alive:true,Deleted:false} owner
                || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Assassin)
            {throw new NotSupportedException("Blade controller owner/map/death/class lifecycle is not connected");}
            var creation=before.Creation;
            var frame=BladeSentinelController.Advance(before.Control,before.Motion,new(0,0,Map.Width*2,Map.Height*2),
                (x,y)=>BladeWorldSpace.Mask(Map,before.Z,x,y,this),owner.Map==Map?new Point2D(owner.X*2,owner.Y*2):null,()=>
                {
                    var create=BladeSentinelActors.CreateFollowingMissile??throw new NotSupportedException("Blade following missile/stat service is not connected");
                    creation=create(this)??throw new InvalidDataException("Missing blade missile creation snapshot");
                });
            Store(before with{Control=frame.Control,Creation=creation,Motion=frame.Motion,Frames=before.Frames+frame.Control.Moves-before.Control.Moves,NextFrameUtc=before.NextFrameUtc.AddMilliseconds(40)});
            var point=Position(Prepared);if(Location!=point){MoveToWorld(point,Map);}AIObject?.AITimer.Stop();
            if(frame.Control.Mode==0){Delete();}
        }
    }
    internal void SuspendController(string reason)
    {if(Prepared is{} before){Store(before with{ControllerFault=reason});}}
    public override void OnAfterDelete(){BladeSentinelActors.Forget(this);AssassinTrapRoster.Forget(this);base.OnAfterDelete();}
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override void GenerateLoot(bool spawning){}
    public override bool OnBeforeDeath(){Delete();return false;}
    [AfterDeserialization]private void AfterLoad()
    {
        AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<BladeBodyState>(_saved);Validate(_prepared);
        if(SummonMaster?.Serial.Value!=_prepared.Owner || Map?.MapID!=_prepared.Map || Location!=Position(_prepared))
        {throw new InvalidDataException("Prepared blade owner/position mismatch");}
        if(_prepared.Control is{Mode:not 0}){BladeSentinelActors.Track(this);}
    }
}

// Explicit native flat-floor projection for source path pattern0/mask0x3c01.
// Whole tile terrain/height and unmapped creature presence remain port adapters;
// this does not claim current D2R room geometry or automatic path initialization.
internal static class BladeWorldSpace
{
    internal static int Mask(Map map,int z,int cellX,int cellY,Mobile self)
    {
        int x=(int)Math.Round(cellX/2.0,MidpointRounding.AwayFromZero),y=(int)Math.Round(cellY/2.0,MidpointRounding.AwayFromZero);
        if(map==null || map==Map.Internal || x<=0 || y<=0 || x>=map.Width || y>=map.Height)
        {throw new NotSupportedException("Blade native room boundary is not mapped");}
        if(!map.CanFit(x,y,z,16,true,false,true)){return 0x1000;}
        // Pattern1/2 adds NO_PATH to the center/cardinal cells; the raw player
        // and monster bits are not themselves in the monster movement mask.
        foreach(var mobile in map.GetMobilesInRange<Mobile>(new Point3D(x,y,z),2))
        {
            if(mobile==self || !mobile.BlocksFit || mobile is not{Alive:true,Deleted:false} || mobile.IsDeadBondedPet
                || mobile.Z+16<=z || z+16<=mobile.Z || mobile.AccessLevel>AccessLevel.Player && mobile.Hidden){continue;}
            int dx=Math.Abs(cellX-mobile.X*2),dy=Math.Abs(cellY-mobile.Y*2);
            int size=mobile is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??2:2;
            if(size>0 && (size>=3?dx+dy<=1:dx==0 && dy==0)){return 0x1000;}
        }
        return 0;
    }
}
