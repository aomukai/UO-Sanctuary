// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary;

internal sealed record GenericPetSavedControl(int Value0,int Value1,int Value2);
internal sealed record DeathMarkSavedActor(int Handle,int Map,byte Selector,int Target48,int Type4c,int Mode,DateTime NextAiUtc)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public GenericPetSavedControl Control{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public GenericPetHeaderImage Header{get;init;}
}
internal sealed record DeathMarkWorldImage(GenericPetPlacementImage[] Worlds,DeathMarkSavedActor[] Actors);
public sealed class DeathMarkWorldProfiles:GenericPersistence
{
    private DeathMarkWorldImage _snapshot;
    public DeathMarkWorldProfiles():base("SanctuaryDeathMarkWorld",246){}
    public override void Serialize(){DeathMarkWorld.Prune();_snapshot=DeathMarkWorld.Capture();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Death Mark world version");}DeathMarkWorld.Restore(JsonSerializer.Deserialize<DeathMarkWorldImage>(reader.ReadString()));}
    public override void PostDeserialize()=>DeathMarkWorld.AfterLoad();
}

// Registered live original demon paths/commands. Registration takes an actual
// caller-supplied controller path:it does not invent a birth or movement path.
// Neutral mode/next-AI requests persist;automatic GenericPet AI is not enabled.
internal static class DeathMarkWorld
{
    private sealed class Actor(int map,byte selector,GenericPetCommandUnit command,int mode,DateTime next)
    {internal int Map=map,Mode=mode;internal readonly byte Selector=selector;internal readonly GenericPetCommandUnit Command=command;internal DateTime Next=next;internal GenericPetSavedControl Control;internal GenericPetLiveHeader Header;}
    private static Dictionary<int,GenericPetPlacementWorld> _worlds=new();
    private static Dictionary<int,Actor> _actors=new();
    public static void Configure()=>_=new DeathMarkWorldProfiles();
    internal static void Register(SkeletonGuardian body,GenericPetMonsterPlacementUnit unit,GenericPetCommitPath path,byte selector,GenericPetCommandData command,int mode,DateTime next)
    {
        GenericPetNativePlacement.RequireActor(body,unit,path);_=GenericPetPathTargets.Bind(selector);
        if(command==null||command.Type4c>=5||mode is <0 or >255||next.Kind!=DateTimeKind.Utc||_actors.ContainsKey(path.Actor.Handle))
        {throw new NotSupportedException("Invalid or duplicate original demon controller context");}
        int map=body.Map.MapID;if(!_worlds.TryGetValue(map,out var world)){world=new(body.Map);}
        world.Register(body,unit,path);_worlds[map]=world;
        _actors.Add(path.Actor.Handle,new(map,selector,new(1,unit.Pet.ClassId,selector,command),mode,next));
    }
    // Called only from the new-body factory after its actual path is supplied.
    // Validate all startup data before touching the registered world/actor map.
    internal static void RegisterNew(SkeletonGuardian body,GenericPetMonsterPlacementUnit unit,GenericPetCommitPath path,byte selector,int mode,DateTime next,GenericPetStartupDraft draft)
    {
        GenericPetStartupProposal.RequireApproval();
        if(draft==null||draft.SourceClass!=WarlockDemonPlacement.ClassId(body)||draft.OwnerId!=checked((int)WarlockDemonPlacement.Owner(body))
            ||draft!=GenericPetStartupProposal.Draft(draft.SourceClass,draft.OwnerGame,draft.OwnerId))
        {throw new NotSupportedException("New demon startup differs from the approved birth tuple");}
        var header=new GenericPetLiveHeader(GenericPetLiveHeader.Initial(draft));
        Register(body,unit,path,selector,new(draft.CommandTarget,draft.CommandType),mode,next);
        var actor=_actors[path.Actor.Handle];actor.Control=draft.Words;actor.Header=header;
    }
    internal static GenericPetLiveHeader Header(SkeletonGuardian body)=>ControlledActor(body).Header
        ??throw new NotSupportedException("Legacy demon has no qualified live AI header");
    internal static DeathMarkSavedActor State(SkeletonGuardian body)
    {
        if(body==null||!_actors.TryGetValue(unchecked((int)body.Serial.Value),out var actor)){return null;}
        return new(unchecked((int)body.Serial.Value),actor.Map,actor.Selector,actor.Command.Data.Target48,actor.Command.Data.Type4c,actor.Mode,actor.Next){Control=actor.Control,Header=actor.Header?.Capture()};
    }
    internal static GenericPetCommitPath Path(SkeletonGuardian body)=>_worlds[_actors[checked((int)body.Serial.Value)].Map].Member(checked((int)body.Serial.Value)).Path;
    internal static uint Flags128(SkeletonGuardian body)=>_worlds[_actors[checked((int)body.Serial.Value)].Map].Member(checked((int)body.Serial.Value)).Flags128;
    // Continuous native transport uses the same registered source position
    // and room/update lists as placement. The caller has already advanced the
    // qualified movement kernel;this does not choose a path or birth flags.
    internal static void WalkPrepared(SkeletonGuardian body,uint x,uint y)
    {
        var actor=ControlledActor(body);var world=_worlds[actor.Map];int id=checked((int)body.Serial.Value);
        var path=world.Member(id).Path;var unit=world.Unit(id);world.Require(body,unit,path);GenericPetNativePlacement.RequireActor(body,unit,path);
        if(path.Size5c!=DemonPathWorld.Size(body)||(path.Flags&1)==0||unit.Pet.ClassId!=WarlockDemonPlacement.ClassId(body))
        {throw new NotSupportedException("Walking needs its qualified active species ground path");}
        int cx=checked((int)(x>>16)),cy=checked((int)(y>>16));
        if(!DemonPathWorld.Point(body,path,new(cx,cy),out var point))
        {throw new NotSupportedException("Source walking destination has no supported native floor/space");}
        int RoomAt(int _,int px,int py)
        {
            if(px<=0||py<=0||px>=body.Map.Width*2||py>=body.Map.Height*2){return 0;}
            int handle=GenericPetNativePlacement.RoomHandle(body.Map,px,py);int gx=px/32*32,gy=py/32*32;
            world.EnsureRoom(handle,new(gx,gy,Math.Min(32,body.Map.Width*2-gx),Math.Min(32,body.Map.Height*2-gy)));return handle;
        }
        _=RoomAt(0,cx,cy);
        var services=new GenericPetPathCommitServices(RoomAt,room=>world.Room(room).Bounds,
            _=>throw new InvalidOperationException("Coordinate transport does not remove collision patterns"),
            _=>throw new InvalidOperationException("Coordinate transport does not query collision patterns"),
            _=>throw new InvalidOperationException("Coordinate transport does not add collision patterns"),
            _=>throw new InvalidOperationException("Coordinate transport does not remove collision sizes"),
            _=>throw new InvalidOperationException("Coordinate transport does not add collision sizes"),
            member=>GenericPetRoomLinks.Remove(member,world.Links),(member,room)=>GenericPetRoomLinks.Add(member,room,world.Links),
            member=>GenericPetRoomLinks.Touch(member,world.Links),(_,_)=>throw new InvalidOperationException("Coordinate transport does not reset targets"),0);
        world.Begin(world.Revision);GenericPetPathCommit.Coordinates(path,x,y,0,services);body.MoveToWorld(point,body.Map);body.AIObject?.AITimer.Stop();
    }
    internal static int PlacePrepared(SkeletonGuardian body,Point2D target,bool skipSearch)
    {
        if(Path(body).Size5c!=DemonPathWorld.Size(body)){throw new NotSupportedException("Placement footprint does not match its ground demon species");}
        var actor=ControlledActor(body);var world=_worlds[actor.Map];int id=checked((int)body.Serial.Value);
        var unit=world.Unit(id);var path=world.Member(id).Path;world.Require(body,unit,path);
        return new GenericPetNativePlacement(body,unit,path,target,GenericPetPathTargets.Bind(actor.Selector),world).Place(skipSearch);
    }
    internal static int WarpPrepared(SkeletonGuardian body,GenericPetDecisionUnit owner,GenericPetWarpServices services)
    {
        if(Path(body).Size5c!=DemonPathWorld.Size(body)){throw new NotSupportedException("Warp footprint does not match its ground demon species");}
        var actor=ControlledActor(body);var world=_worlds[actor.Map];int id=checked((int)body.Serial.Value);
        int result=GenericPetWarp.Execute(new(owner,world.Unit(id).Pet,actor.Selector),services);
        world.Member(id).Flags128=world.Unit(id).Pet.Flags;return result;
    }
    // Explicit candidate host reactivation:the native roster has already
    // restored a legal body position. Build replacement graphs before publish;
    // retain its original header,commands,control words and combat/birth state.
    internal static void RebindPrepared(SkeletonGuardian body,GenericPetBirthPath next)
    {
        _=DemonPathWorld.Size(body);
        var actor=ControlledActor(body);_=actor.Header?.Capture()??throw new NotSupportedException("Reactivation needs the original normal header");
        GenericPetNativePlacement.RequireActor(body,next.Unit,next.Path);
        if(next.Selector!=actor.Selector||next.Mode!=1||next.NextAiUtc.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Reactivation requires a qualified neutral native path");}
        int id=checked((int)body.Serial.Value),oldMap=actor.Map,newMap=body.Map.MapID;
        var oldWorld=GenericPetPlacementWorld.Restore(_worlds[oldMap].Capture());next.Unit.Pet.Flags=oldWorld.Unit(id).Pet.Flags;oldWorld.Remove(id);
        var newWorld=newMap==oldMap?oldWorld:_worlds.TryGetValue(newMap,out var existing)
            ?GenericPetPlacementWorld.Restore(existing.Capture()):new GenericPetPlacementWorld(body.Map);
        newWorld.Register(body,next.Unit,next.Path);
        if(newMap!=oldMap){if(oldWorld.Count==0){_worlds.Remove(oldMap);}else{_worlds[oldMap]=oldWorld;}}
        _worlds[newMap]=newWorld;actor.Map=newMap;actor.Next=next.NextAiUtc;ChangeMode(body,1);
    }
    // Read the registered original command,not a detached saved DTO or native
    // Combatant. Lookup stays separate from the survey's validity/path checks.
    internal static GenericPetOwnerSurveyServices BindTargets(GenericPetOwnerSurveyServices services,GenericPetWorldIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return GenericPetTargetCommand.Bind(services,identity.Game,
            id=>_actors.TryGetValue(id,out var actor)?actor.Command:null,identity.Resolve);
    }
    internal static GenericPetInitializedFrame InitializePrepared(SkeletonGuardian body,int owner,GenericPetDecisionMemory control,
        GenericPetWorldIdentity identity,GenericPetInitializationServices services)
    {
        ArgumentNullException.ThrowIfNull(body);ArgumentNullException.ThrowIfNull(identity);ArgumentNullException.ThrowIfNull(services);
        if(!_actors.TryGetValue(checked((int)body.Serial.Value),out var actor))
        {throw new NotSupportedException("Original demon controller is not registered");}
        GenericPetLifePercent.Require(checked((int)body.Serial.Value));GenericPetLifePercent.Require(owner);
        var ability=body switch{WarlockGoatmanBody=>CombatAbility.SummonGoatman,WarlockTaintedBody=>CombatAbility.SummonTainted,
            WarlockDefilerBody=>CombatAbility.SummonDefiler,_=>throw new NotSupportedException("Unsupported original GenericPet body")};
        return GenericPetInitialization.Run(new(identity.Game,actor.Selector,checked((int)body.Serial.Value),owner,actor.Command,control,GenericPetProfiles.Raw(ability)),
            services with{Resolve=identity.Resolve,Property=GenericPetLifePercent.For,Survey=BindTargets(services.Survey,identity)});
    }
    // Birth/default production remains separate. Legacy controllers keep a null
    // control until a qualified caller explicitly provides all three words.
    internal static void RegisterControl(SkeletonGuardian body,GenericPetSavedControl control)
    {
        ArgumentNullException.ThrowIfNull(control);var actor=ControlledActor(body,false);
        if(actor.Control!=null){throw new NotSupportedException("Original demon AI control is already supplied");}
        actor.Control=control;
    }
    private static Actor ControlledActor(SkeletonGuardian body,bool requireControl=true)
    {
        if(body is not{Deleted:false,Alive:true}||!WarlockDemonPlacement.Prepared(body)
            ||!_actors.TryGetValue(checked((int)body.Serial.Value),out var actor)||requireControl&&actor.Control==null)
        {throw new NotSupportedException("Original demon has no qualified persistent AI control");}
        return actor;
    }
    internal static GenericPetInitializedFrame InitializePrepared(SkeletonGuardian body,int owner,
        GenericPetWorldIdentity identity,GenericPetInitializationServices services)
    {
        var control=ControlledActor(body).Control;
        return InitializePrepared(body,owner,new(){Value0=control.Value0,Value1=control.Value1,Value2=control.Value2},identity,services);
    }
    internal static GenericPetRootServices BindControlPersistence(SkeletonGuardian body,GenericPetRootServices root)
    {
        ArgumentNullException.ThrowIfNull(root);_=ControlledActor(body);
        // Re-resolve on publication: a stale closure must not resurrect an
        // actor removed or replaced during another explicit root service.
        return root with{Persist=(a,b,c)=>ControlledActor(body).Control=new(a,b,c)};
    }
    internal static int FinishPrepared(SkeletonGuardian body,(int Kind,int Id)? target)
    {
        if(body==null||!_actors.TryGetValue(checked((int)body.Serial.Value),out var actor)){return 0;}
        return GenericPetTargetCommand.Finish(actor.Command,target,GenericPetPathTargets.Bind(actor.Selector).CommandFlags);
    }
    // Registered source mode is shared with active intrinsic actions. Update
    // it before cancellation so a callback cannot restore the outgoing mode.
    internal static void ChangeMode(SkeletonGuardian body,int next)
    {
        if(body==null||!_actors.TryGetValue(unchecked((int)body.Serial.Value),out var actor)){return;}
        if(next is <0 or >255){throw new NotSupportedException("Unqualified GenericPet mode");}
        int previous=actor.Mode;if(previous==next){return;}actor.Mode=next;
        GoatmanBerserkEffects.ModeChanged(body,previous,next);
        if(body is WarlockTaintedBody tainted){TaintedFireBallCasting.Cancel(tainted);}
        if(body is WarlockDefilerBody defiler){DefilerLinkCasting.Cancel(defiler);}
        if(next==1){body.Combatant=null;body.FocusMob=null;body.Warmode=false;}
    }
    internal static void FinishAction(SkeletonGuardian body,int expected)
    {
        if(body!=null&&_actors.TryGetValue(unchecked((int)body.Serial.Value),out var actor)&&actor.Mode==expected){ChangeMode(body,1);}
    }
    private static DeathMarkControlRoot Controls(SkeletonGuardian body,DateTime at)
    {
        var lists=new List<DeathMarkControlList>();
        if(CombatStuns.Remaining(body)>0){lists.Add(new(21,21,-1));}
        if(TimedEffects.TryActor(body,out var actor))
        {
            if(actor.Control is{Freeze:{Removed:false}} control&&control.FreezeUntil>at){lists.Add(new(1,1,-1));}
            if(actor.Cold!=null&&actor.ChillUntil>at){lists.Add(new(11,11,-1));}
        }
        for(int i=1;i<lists.Count;i++){lists[i-1].Next=lists[i];}
        return new(-1,lists.FirstOrDefault());
    }
    internal static bool ApplyPrepared(SkeletonGuardian body,DeathMarkDirective directive,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||directive==null){throw new ArgumentException("Death Mark needs a UTC directive");}
        int id=checked((int)body.Serial.Value);
        if(!_actors.TryGetValue(id,out var actor)||!_worlds.TryGetValue(actor.Map,out var world)){throw new NotSupportedException("Demon controller path is not registered");}
        var unit=world.Unit(id);var path=world.Member(id).Path;world.Require(body,unit,path);GenericPetNativePlacement.RequireActor(body,unit,path);
        if(directive.ClearControl&&NamedControl.State(body)is{Freeze:null} legacy&&legacy.FreezeUntil>at)
        {throw new NotSupportedException("Legacy untagged freeze has no qualified source list");}
        var view=GenericPetPathTargets.Bind(actor.Selector);int before=directive.Remaining;
        var services=new DeathMarkPetServices(point=>new GenericPetNativePlacement(body,unit,path,point,view,world).Place(),_=>
            body.FixedEffect(0x3728,10,15), // Identified Death Mark pet/landing placeholder.
            null,ticks=>
            {
                ChangeMode(body,1);
                actor.Next=at.AddMilliseconds(Math.Max(1,ticks)*40L);body.AIObject?.AITimer.Stop();
            },()=>Controls(body,at),(selector,list)=>
            {
                if(selector!=actor.Selector){throw new NotSupportedException("Demon stat selector changed");}
                switch(list.State)
                {case 21:CombatStuns.Clear(body);break;case 1:FreezeStates.Remove(body,at);break;case 11:ColdStates.Remove(body,at);break;
                    default:throw new NotSupportedException("Death Mark attempted an unrelated control state");}
                list.Flags=0; // Managed projection:the selected manager owns removal;no raw allocator is emulated.
            },(_,_)=>{});
        DeathMarkPets.Run(new(true,0,actor.Selector,actor.Selector),directive,GenericPetTargetCommand.Bind(services,actor.Command,view.CommandFlags));
        return directive.Remaining<before;
    }
    internal static void Remove(SkeletonGuardian body)
    {
        if(body==null||!_actors.Remove(unchecked((int)body.Serial.Value),out var actor)){return;}
        var world=_worlds[actor.Map];world.Remove(checked((int)body.Serial.Value));if(world.Count==0){_worlds.Remove(actor.Map);}
    }
    internal static void Prune()
    {
        foreach(int id in _actors.Keys.ToArray())
        {
            if(World.FindMobile((Serial)(uint)id)is SkeletonGuardian{Deleted:false,Alive:true} body&&WarlockDemonPlacement.Prepared(body)){continue;}
            var actor=_actors[id];_actors.Remove(id);var world=_worlds[actor.Map];world.Remove(id);if(world.Count==0){_worlds.Remove(actor.Map);}
        }
    }
    internal static void AfterLoad()
    {
        Prune();
        // Intrinsic windups are transient,as with the existing player cast
        // hosts. Do not restore a saved cast mode without its action events.
        foreach(int id in _actors.Keys.ToArray())
        {
            if(World.FindMobile((Serial)(uint)id)is SkeletonGuardian body
                &&(body is WarlockTaintedBody&&_actors[id].Mode==5||body is WarlockDefilerBody&&_actors[id].Mode==8))
            {ChangeMode(body,1);}
            if(World.FindMobile((Serial)(uint)id)is WarlockDefilerBody{ControlMaster:Server.Mobiles.PlayerMobile{NetState:null}} offline)
            {PersistentMinions.Suspend(offline);}
        }
    }
    internal static DeathMarkWorldImage Capture()=>new(_worlds.Values.OrderBy(w=>w.Map.MapID).Select(w=>w.Capture()).ToArray(),
        _actors.OrderBy(p=>p.Key).Select(p=>new DeathMarkSavedActor(p.Key,p.Value.Map,p.Value.Selector,p.Value.Command.Data.Target48,p.Value.Command.Data.Type4c,p.Value.Mode,p.Value.Next){Control=p.Value.Control,Header=p.Value.Header?.Capture()}).ToArray());
    internal static void Restore(DeathMarkWorldImage image)
    {
        if(image?.Worlds==null||image.Actors==null){throw new InvalidDataException("Missing Death Mark world image");}
        var worlds=new Dictionary<int,GenericPetPlacementWorld>();var actors=new Dictionary<int,Actor>();var members=new Dictionary<int,int>();
        foreach(var saved in image.Worlds)
        {
            if(saved==null||worlds.ContainsKey(saved.Map)){throw new InvalidDataException("Duplicate Death Mark map");}
            var world=GenericPetPlacementWorld.Restore(saved);worlds.Add(saved.Map,world);
            foreach(var m in saved.Members){if(!members.TryAdd(m.Handle,saved.Map)){throw new InvalidDataException("Duplicate original demon");}}
        }
        foreach(var saved in image.Actors)
        {
            if(saved==null||saved.Selector is <1 or >3||saved.Type4c>=5||saved.Mode is <0 or >255||saved.NextAiUtc.Kind!=DateTimeKind.Utc
                ||!members.TryGetValue(saved.Handle,out int map)||map!=saved.Map||actors.ContainsKey(saved.Handle))
            {throw new InvalidDataException("Invalid saved demon controller context");}
            var unit=worlds[map].Unit(saved.Handle);
            GenericPetLiveHeader header=null;
            if(saved.Header!=null)
            {
                GenericPetLiveHeader.Validate(saved.Header);
                if(saved.Control==null||saved.Header.SourceClass!=unit.Pet.ClassId||saved.Header.OwnerId!=checked((int)worlds[map].Owner(saved.Handle)))
                {throw new InvalidDataException("Saved demon AI header lost its original class/owner/control");}
                header=new(saved.Header);
            }
            actors.Add(saved.Handle,new(map,saved.Selector,new(1,unit.Pet.ClassId,saved.Selector,new(saved.Target48,saved.Type4c)),saved.Mode,saved.NextAiUtc){Control=saved.Control,Header=header});
        }
        if(actors.Count!=members.Count){throw new InvalidDataException("Missing original demon command state");}
        _worlds=worlds;_actors=actors;
    }
}
