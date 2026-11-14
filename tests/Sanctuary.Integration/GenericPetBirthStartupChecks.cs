// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetBirthStartupChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetBirthStartupChecks));
    private sealed record Row(uint Body,uint Owner,string Birth,int Life,int Mana,DeathMarkSavedActor State);
    private sealed record Saved(int Generation,Row[] Born,Row[] Legacy);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-birth-startup-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>b switch{WarlockGoatmanBody g=>g.SavedText,WarlockTaintedBody t=>t.SavedText,WarlockDefilerBody d=>d.SavedText,_=>throw new InvalidOperationException()};
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"pet-birth-startup-reload-check":"pet-birth-startup-check";ConsoleInputHandler.RegisterCommand(name,"Approved new-demon startup with live saved headers",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static GenericPetBirthPath SuppliedPath(SkeletonGuardian body)
    {
        // Explicit fixture geometry, never production birth defaults.
        int cls=WarlockDemonPlacement.ClassId(body);var row=GenericPetSecondaryTables.Table.Row(cls);
        var path=new GenericPetCommitPath{X=((uint)(body.X*2)<<16)|0x8000,Y=((uint)(body.Y*2)<<16)|0x8000,
            Room=GenericPetNativePlacement.RoomHandle(body.Map,body.X*2,body.Y*2),Flags=0x20,Word30=11,Word34=37,Raw96=0x7788,
            Size5c=1,Mask60=0x100,Mask64=0x1c09,Actor=new(checked((int)body.Serial.Value),1)};
        return new(new(new(cls,0),true,1,cls,row.SizeX,row.SizeY,37),path,1,1,TimedEffects.Now);
    }
    private static SkeletonGuardian Make(PlayerMobile owner,int species,Point3D point,GenericPetBirthBinding startup)=>species switch
    {0=>WarlockGoatmanBody.CreatePrepared(owner,point,TimedEffects.Now,startup),1=>WarlockTaintedBody.CreatePrepared(owner,point,TimedEffects.Now,startup),_=>WarlockDefilerBody.CreatePrepared(owner,point,TimedEffects.Now,startup)};
    private static Row Save(SkeletonGuardian body)=>new(body.Serial.Value,WarlockDemonPlacement.Owner(body),Birth(body),SkillHealth.Balance256(body),body.Mana,DeathMarkWorld.State(body));
    private static void Exercise(SkeletonGuardian body)
    {
        var h=DeathMarkWorld.Header(body);var expected=h.Capture();var state=DeathMarkWorld.State(body);
        var draft=GenericPetStartupProposal.Draft(WarlockDemonPlacement.ClassId(body),expected.Game,expected.OwnerId);
        Require(state.Control==draft.Words&&state.Target48==0&&state.Type4c==0&&h.Control is{State:0,Function:0x4b60c0,Field14:2,Executing:false}&&h.Control.Queue.Count==0,"Approved startup did not reach actual registered controller");
        GenericPetStartupProposalChecks.Run(body,draft,true);
        Require(ReferenceEquals(h,DeathMarkWorld.Header(body))&&ReferenceEquals(h.Control,DeathMarkWorld.Header(body).Control),"Live header is a detached draft");
        // Metadata/storage checks only; they do not certify special AI behavior.
        h.Mode10=7;h.Flags38=0x10;h.Ai90=17;h.Memory.Id=expected.OwnerId;h.Memory.Kind=0;
        Require(DeathMarkWorld.State(body).Header==h.Capture(),"Mutable live header did not publish to existing save image");
        h.Control.Queue.Add(new(1,2));bool refused=false;try{h.Capture();}catch(NotSupportedException){refused=true;}finally{h.Control.Queue.Clear();}
        Require(refused,"Unqualified queued native context was silently serialized");
        var image=DeathMarkWorld.Capture();string before=JsonSerializer.Serialize(image);int id=checked((int)body.Serial.Value);refused=false;
        try{DeathMarkWorld.Restore(image with{Actors=image.Actors.Select(a=>a.Handle==id?a with{Header=a.Header with{SourceClass=0}}:a).ToArray()});}catch(InvalidDataException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(DeathMarkWorld.Capture())==before,"Bad header restore changed registered actors");
        // Refusing re-registration must leave commands/header/control untouched.
        refused=false;try{new GenericPetBirthBinding(expected.Game,SuppliedPath).RegisterNew(body);}catch(NotSupportedException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(DeathMarkWorld.Capture())==before,"Duplicate startup reset an existing controller");
    }
    private static void Check(bool reload)
    {
        Saved saved;
        if(reload)
        {
            saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Startup generation");
            foreach(var row in saved.Born.Concat(saved.Legacy))
            {
                var body=(SkeletonGuardian)World.FindMobile((Serial)row.Body);
                Require(body!=null&&WarlockDemonPlacement.Owner(body)==row.Owner&&Birth(body)==row.Birth&&SkillHealth.Balance256(body)==row.Life&&body.Mana==row.Mana&&DeathMarkWorld.State(body)==row.State,"Original startup/header/control/birth/wounds changed on load");
            }
        }
        else
        {
            var owner=ClassSkillChecks.Player(DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(owner,99);foreach(string skill in new[]{"summongoatman","summontainted","summondefiler"}){ClassSkillChecks.Train(owner,"warlock."+skill);}
            var witch=ClassSkillChecks.Player(DiabloClass.Warlock);witch.Female=true;witch.Body=0x191;ClassCreation.Profiles.AdvanceLevel(witch,99);foreach(string skill in new[]{"summongoatman","summontainted","summondefiler"}){ClassSkillChecks.Train(witch,"warlock."+skill);}
            FixtureSpace.Corridor(owner,6500,40,3700);witch.MoveToWorld(new(owner.X,owner.Y+2,owner.Z),owner.Map);
            var born=new Row[3];var legacy=new Row[3];var binding=new GenericPetBirthBinding(71,SuppliedPath);
            for(int i=0;i<3;i++)
            {
                var p=i==1?witch:owner;var body=Make(p,i,new(owner.X+2+i*3,owner.Y,owner.Z),binding);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)-333);born[i]=Save(body);
                var old=Make(p,i,new(owner.X+2+i*3,owner.Y+3,owner.Z),null);DeathMarkWorldChecks.Register(old);SkillHealth.SetBalance256(old,SkillHealth.Maximum256(old)-129);legacy[i]=Save(old);
            }
            saved=new(0,born,legacy);
        }
        var next=new Row[3];for(int i=0;i<3;i++)
        {
            var body=(SkeletonGuardian)World.FindMobile((Serial)saved.Born[i].Body);Exercise(body);next[i]=Save(body);
            var old=(SkeletonGuardian)World.FindMobile((Serial)saved.Legacy[i].Body);var before=DeathMarkWorld.State(old);
            Require(before.Header==null&&before.Control==null&&!JsonSerializer.Serialize(before).Contains("Header"),"Legacy unknown header/control received birth defaults");
            bool refused=false;try{DeathMarkWorld.Header(old);}catch(NotSupportedException){refused=true;}Require(refused&&DeathMarkWorld.State(old)==before,"Missing legacy header was invented");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(saved.Generation+(reload?1:0),next,saved.Legacy)));
        Log.Information("New demon startup:actual three birth factories/native owner registry/registered managed headers/default AI entry,mutable saved metadata,no aliases,legacy unknown preservation,duplicate/queue/atomic refusal pass;path remains explicit,no automatic clock or learned summon.");
    }
}
