// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class GenericPetStartupProposalChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetStartupProposalChecks));
    private sealed record Entry(uint Body,string Birth,int Life,int Mana,DeathMarkSavedActor Command,GenericPetStartupDraft Draft);
    private sealed record Saved(uint[] Owners,Entry[] Entries,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-startup-proposal-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int Id(IEntity e)=>checked((int)e.Serial.Value);
    private static string Birth(SkeletonGuardian b)=>b switch{WarlockGoatmanBody g=>g.SavedText,WarlockTaintedBody t=>t.SavedText,WarlockDefilerBody d=>d.SavedText,_=>throw new InvalidOperationException()};
    private static void Refused(Action action,string why){bool refused=false;try{action();}catch(NotSupportedException){refused=true;}Require(refused,why);}
    public static void Configure(){Register("pet-startup-proposal-check",Check);Register("pet-startup-proposal-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Review-only normal demon startup",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Contracts()
    {
        Require(!GenericPetStartupProposal.Approved,"Pending proposal silently became approval");
        Refused(GenericPetStartupProposal.RequireApproval,"Unapproved startup admitted");
        foreach(int cls in new[]{-1,0,741,745,751}){Refused(()=>GenericPetStartupProposal.Draft(cls,71,77),"Unknown species acquired proposed defaults");}
        Refused(()=>GenericPetStartupProposal.Draft(742,0,77),"Implicit world invented");Refused(()=>GenericPetStartupProposal.Draft(742,71,0),"Implicit owner invented");
        foreach(int cls in new[]{742,743,744})
        {
            var draft=GenericPetStartupProposal.Draft(cls,71,77);var first=draft.RuntimeForReview();var second=draft.RuntimeForReview();
            Require(draft is{State:0,Function:0x4b60c0,Mode10:0,Field14:2,CommandTarget:0,CommandType:0,Flags38:0,TargetId3c:-1,TargetKind40:1,Ai90:0,OwnerKind:0}&&draft.Words==new GenericPetSavedControl(-1,0,0),"Proposed startup values changed");
            Require(!first.Executing&&first.Queue.Count==0&&second.Queue.Count==0,"Proposed queue was not empty");
            first.State=12;first.Function=1;first.Field14=0;first.Queue.Add(new(4,5));first.Executing=true;
            Require(second is{State:0,Function:0x4b60c0,Field14:2,Executing:false}&&second.Queue.Count==0,"Review controls alias another birth");
            var target=draft.TargetForReview();target.Id=99;Require(draft.TargetForReview().Id==-1,"Review target aliases immutable draft");
            Require(JsonSerializer.Deserialize<GenericPetStartupDraft>(JsonSerializer.Serialize(draft))==draft,"Review policy did not round trip");
        }
    }
    private static void Run(SkeletonGuardian body,GenericPetStartupDraft draft)
    {
        var before=DeathMarkWorld.State(body);int id=Id(body),owner=Id(body.GetMaster());var identity=new GenericPetWorldIdentity(draft.OwnerGame);
        Require(draft.SourceClass==WarlockDemonPlacement.ClassId(body)&&draft.OwnerId==owner&&draft.OwnerKind==0,"Review birth identity changed");
        Require(identity.Resolve(draft.OwnerGame,draft.OwnerKind,draft.OwnerId)==owner,"Review owner did not resolve through native registry");
        var control=draft.RuntimeForReview();int roots=0,combats=0;GenericPetInitializedFrame initialized=null;
        var survey=new GenericPetOwnerSurveyServices(_=>throw new InvalidOperationException("Unbound survey"),(_,_)=>throw new InvalidOperationException("Unbound survey row"),_=>throw new InvalidOperationException("Unbound survey target"),(_,_,_)=>throw new InvalidOperationException("Unbound survey validity"),_=>throw new InvalidOperationException("Unbound survey refresh"));
        var leaves=new GenericPetInitializationServices(identity.Resolve,_=>throw new InvalidOperationException("Native life getter not bound"),who=>World.FindMobile((Serial)(uint)who)is PlayerMobile?0:1,
            (_,visit)=>{visit(id);},survey);
        var pet=new GenericPetDecisionUnit(1,true,body.X*2,body.Y*2);var player=new GenericPetDecisionUnit(0,true,body.GetMaster().X*2,body.GetMaster().Y*2);
        var root=new GenericPetRootServices(()=>player,_=>
        {
            initialized=DeathMarkWorld.InitializePrepared(body,owner,identity,leaves);Require(initialized.Decision.Memory.Value0==-1&&initialized.Decision.Memory.Value1==0&&initialized.Decision.Memory.Value2==0,"Startup memory did not reach registered initializer");return initialized.Decision;
        },_=>throw new InvalidOperationException("Unexpected empty-store removal"),GenericPetSearch.Squared,_=>2,
            (_,_)=>true,(_,frame)=>
            {
                combats++;var combat=new GenericPetCombatServices(()=>[],_=>throw new InvalidOperationException("Unremembered startup queried a previous skill"),_=>0,(_,_,_)=>new(new int[5],0,0),
                    (_,_)=>throw new InvalidOperationException("Startup rated remembered skill"),()=>0,()=>1,(_,_,_,_,_)=>false,()=>throw new InvalidOperationException("Startup reset movement"),(_,_,_,_,_)=>false,()=>throw new InvalidOperationException("Startup moved"),
                    _=>{},(candidate,_)=>candidate,_=>{},(_,_,_)=>throw new InvalidOperationException("Empty startup attacked"),(_,_)=>false);
                Require(!GenericPetCombat.Try(new(1,pet),frame,combat),"Empty first decision invented an attack");return false;
            },_=>throw new InvalidOperationException("Follow fixture should handle idle"),memory=>
            {Require(DeathMarkWorld.FinishPrepared(body,null)==1,"Startup finishing changed command");},(_,_,_)=>throw new InvalidOperationException("Unbound persistence"));
        root=DeathMarkWorld.BindControlPersistence(body,root);
        var events=new GenericPetTimedEvents();var bound=events.Bind(new(pet,true,true,true,Array.Empty<byte>()),root,id);
        var entry=new GenericPetAiEntryServices(()=>new(0x12345678,2,1,3),actor=>new(1,draft.SourceClass,true,control,0,0),(_,_)=>1,(_,_)=>2,
            _=>throw new InvalidOperationException("Normal startup invoked a special override"),tick=>GenericPetAiPreprocessChecks.Original(body,control,tick),
            (_,state)=>GenericPetAiSelectionChecks.Original(body,state),(_,_)=>throw new InvalidOperationException("GenericPet type0 acquired a pre-root target"),
            tick=>GenericPetAiPostTargetChecks.Original(body,tick),(_,_)=>throw new InvalidOperationException("Empty startup queue queried predicate"),(_,_,_)=>throw new InvalidOperationException("Empty startup queue executed"),
            _=>throw new InvalidOperationException("Empty startup queue released"),(_,_,_)=>throw new InvalidOperationException("Normal startup called special AI"),
            (function,_)=>{Require(function==draft.Function,"Normal startup changed default AI");roots++;GenericPetDecisions.Think(bound.Context,bound.Services);});
        GenericPetAiEntry.Run(id,0,entry);
        Require(roots==1&&combats==1&&DeathMarkWorld.State(body)==before,"Review normal startup reset controller state");
        Log.Information("Startup proposal original:normal entry/default root/empty combat decision use proposed unremembered memory;commands/wounds remain. World/path/search/follow inputs are explicit review fixtures;approval pending.");
    }
    private static void Save(PlayerMobile[] owners,SkeletonGuardian[] bodies,GenericPetStartupDraft[] drafts,int generation)
        =>File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owners.Select(o=>o.Serial.Value).ToArray(),bodies.Select((b,i)=>new Entry(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),b.Mana,DeathMarkWorld.State(b),drafts[i])).ToArray(),generation)));
    private static void Check()
    {
        Contracts();var owners=new[]{ClassSkillChecks.Player(DiabloClass.Warlock),ClassSkillChecks.Player(DiabloClass.Warlock)};owners[1].Female=true;owners[1].Body=0x191;
        FixtureSpace.Corridor(owners[0],6420,40,3700);owners[1].MoveToWorld(new(6420,3702,0),Map.Trammel);
        SkeletonGuardian[] bodies=[WarlockGoatmanBody.CreatePrepared(owners[0],new(6421,3700,0),TimedEffects.Now),WarlockTaintedBody.CreatePrepared(owners[1],new(6423,3700,0),TimedEffects.Now),WarlockDefilerBody.CreatePrepared(owners[0],new(6425,3700,0),TimedEffects.Now)];
        var drafts=new GenericPetStartupDraft[3];
        for(int i=0;i<bodies.Length;i++)
        {
            var body=bodies[i];DeathMarkWorldChecks.Register(body);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)-333);var before=DeathMarkWorld.State(body);
            Require(before.Control==null&&!JsonSerializer.Serialize(before).Contains("\"Control\""),"Unknown legacy control got defaults");
            drafts[i]=GenericPetStartupProposal.Draft(WarlockDemonPlacement.ClassId(body),71,Id(body.GetMaster()));
            Require(DeathMarkWorld.State(body)==before,"Draft mutated unknown controller");
            Refused(GenericPetStartupProposal.RequireApproval,"Draft silently authorized production");
            // Explicit disposable test installation only. No production birth
            // path or save migration invokes this pending proposal.
            var image=DeathMarkWorld.Capture();int handle=Id(body);
            DeathMarkWorld.Restore(image with{Actors=image.Actors.Select(a=>a.Handle==handle?a with{Target48=drafts[i].CommandTarget,Type4c=drafts[i].CommandType}:a).ToArray()});
            DeathMarkWorld.RegisterControl(body,drafts[i].Words);Run(body,drafts[i]);
        }
        Save(owners,bodies,drafts,0);
    }
    private static void Reload()
    {
        Contracts();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Extra startup reload");
        var owners=saved.Owners.Select(s=>(PlayerMobile)World.FindMobile((Serial)s)).ToArray();var bodies=saved.Entries.Select(e=>(SkeletonGuardian)World.FindMobile((Serial)e.Body)).ToArray();
        foreach(var e in saved.Entries)
        {
            var body=(SkeletonGuardian)World.FindMobile((Serial)e.Body);Require(Birth(body)==e.Birth&&SkillHealth.Balance256(body)==e.Life&&body.Mana==e.Mana&&DeathMarkWorld.State(body)==e.Command,"Review birth/command/wound changed on load");Run(body,e.Draft);
        }
        Require(owners[1].Female,"Witch review fixture lost identity");Save(owners,bodies,saved.Entries.Select(e=>e.Draft).ToArray(),saved.Generation+1);
    }
}
