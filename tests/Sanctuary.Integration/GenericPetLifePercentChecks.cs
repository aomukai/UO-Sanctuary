// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetLifePercentChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetLifePercentChecks));
    private sealed record Saved(uint Owner,uint Body,string Birth,int Life256,int Maximum256,int Percent,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-life-percent-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int Id(Mobile mobile)=>checked((int)mobile.Serial.Value);
    public static void Configure(){Register("pet-life-percent-check",Check);Register("pet-life-percent-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Current whole-life percent/native health/initializer/scoring",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-life-percent-cases.json")));int n=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");int value=p.GetProperty("present").GetBoolean()
                ?GenericPetLifePercent.Calculate(p.GetProperty("current256").GetInt32(),p.GetProperty("maximum256").GetInt32()):GenericPetLifePercent.For(0);
            Require(value==row.GetProperty("output").GetProperty("value").GetInt32(),"Current life/max helper arithmetic differs");n++;
        }
        Require(n==57&&GenericPetLifePercent.Calculate(2688,5504)==47&&GenericPetLifePercent.Calculate(257,511)==100
            &&GenericPetLifePercent.Calculate(512,256)==200,"Source fractional shift order or invented percentage clamp");
        var unknown=new PlayerMobile();bool rejected=false;
        try{GenericPetLifePercent.For(Id(unknown));}catch(NotSupportedException){rejected=true;}finally{unknown.Delete();}
        Require(rejected&&GenericPetLifePercent.For(Id(unknown))==0,"Unclassified or deleted native health projection invented");
    }
    private static GenericPetInitializedFrame Initialize(PlayerMobile owner,WarlockGoatmanBody body)
    {
        var survey=new GenericPetOwnerSurveyServices(_=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
            _=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException());
        var leaves=new GenericPetInitializationServices((_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException("Unbound native life percent"),
            id=>id==Id(owner)?0:1,(which,visit)=>{Require(which==Id(owner),"Wrong original owner");visit(Id(body));},survey);
        var frame=DeathMarkWorld.InitializePrepared(body,Id(owner),new(){Value0=-1},new GenericPetWorldIdentity(71),leaves);
        Require(DeathMarkWorld.FinishPrepared(body,null)==1,"Current null-selection finishing failed");
        return frame;
    }
    private static void Scoring(GenericPetInitializedFrame frame,PlayerMobile owner,WarlockGoatmanBody body,bool petHurt)
    {
        var context=frame.ScoreContext(1,0,Id(body),Id(owner));
        Require(context.PetLifePercent==frame.PetLifePercent&&context.OwnerLifePercent==frame.OwnerLifePercent&&context.GatherValue108==0,"Frame health/gather fields not bound to scorer");
        var skill=new GenericPetScoreSkill(0,4,0,0,0,-1,-1,0,0);
        var services=new GenericPetScoreServices((_,_)=>skill,(_,_)=>0,
            id=>new(new(id==Id(owner)?0:1,true,0,0),0,id,1,true),(_,_)=>false,(_,_)=>0,(_,_,_)=>0,
            _=>0,(_,_)=>null,(_,_)=>false,(_,_)=>0,_=>0,(_,_,_,_,_)=>0,(_,_)=>100,_=>false);
        var row=new GenericPetCombatCandidate(0,0,int.MinValue,1,0,0,0,8,0,0,0);
        var result=GenericPetTargetScoring.Rate(context,frame.Decision.Parameters,0,0,row,new(173),services);
        Require(result.Target==(petHurt?Id(body):Id(owner))&&result.Rating==20,"Actual life percentages did not reach existing type8 scoring");
        var selection=new GenericPetCombatSelection([Id(body),0,Id(owner),0,0],0,0);
        var routed=GenericPetRatingRouting.Rate(1,frame.Decision.Parameters,frame.PetLifePercent,selection,row with{Byte36=13},
            new((_,_)=>0,(r,_,_)=>r with{Rating=0}));
        Require(routed.Target==(petHurt?Id(owner):Id(body)),"Actual life threshold did not reach existing type13 routing");
    }
    private static void Exercise(PlayerMobile owner,WarlockGoatmanBody body)
    {
        int ownerMaximum=SkillHealth.Maximum256(owner),ownerWhole=ownerMaximum>>8;
        SkillHealth.SetBalance256(owner,((ownerWhole+1)/2)*256-1);
        Require(GenericPetLifePercent.For(Id(owner))==49&&owner.Hits*100/owner.HitsMax>=50,"Native displayed hits hid the required fractional wound");
        SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body));var full=Initialize(owner,body);
        Require(full.PetLifePercent==100&&full.OwnerLifePercent==49,"Actual whole-life properties not supplied to initializer");Scoring(full,owner,body,false);
        int maximum=SkillHealth.Maximum256(body),whole=maximum>>8;
        SkillHealth.SetBalance256(body,((whole+1)/2)*256-1);int before=SkillHealth.Balance256(body);
        var injured=Initialize(owner,body);Require(injured.PetLifePercent==49&&full.PetLifePercent==100,"Live/captured health contexts mixed");Scoring(injured,owner,body,true);
        Require(SkillHealth.Balance256(body)==before,"Read-only initialization/scoring healed original demon");
        // Explicit owner fixture cleanup;no claim of suppressing player regen
        // or persisting an injured player's life is made by this cohort.
        SkillHealth.SetBalance256(owner,ownerMaximum);
    }
    private static void Check()
    {
        Source();var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6400,30,3400);
        var body=WarlockGoatmanBody.CreatePrepared(owner,new(owner.X+1,owner.Y,owner.Z),TimedEffects.Now);DeathMarkWorldChecks.Register(body);
        string birth=body.SavedText;Exercise(owner,body);Require(body.SavedText==birth,"Life read recaptured birth");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,body.Serial.Value,birth,SkillHealth.Balance256(body),SkillHealth.Maximum256(body),GenericPetLifePercent.For(Id(body)),0)));
        Log.Information("Life percent:57complete-current arithmetic controls;native FP wound reports49while rounded player hits report50. Actual original demon/owner life feeds initializer and existing type8 scoring/type13 routing;source birth/wounds preserved,automatic AI remains gated.");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Extra life-percent reload");
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(WarlockGoatmanBody)World.FindMobile((Serial)saved.Body);
        Require(body.SavedText==saved.Birth&&SkillHealth.Balance256(body)==saved.Life256&&SkillHealth.Maximum256(body)==saved.Maximum256
            &&GenericPetLifePercent.For(Id(body))==saved.Percent,"Original life/fractional wound/percentage changed across reload");
        var frame=Initialize(owner,body);Require(frame.PetLifePercent==saved.Percent&&frame.OwnerLifePercent==100,"Loaded health did not reach initializer");
        Require(SkillHealth.Balance256(body)==saved.Life256&&body.SavedText==saved.Birth,"Loaded initializer healed or recaptured original");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
