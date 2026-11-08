// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowMasterWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowMasterWorldChecks));
    private sealed record Saved(uint Owner,uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,uint Target,int TargetLife,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-master-world-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static IronGolemWorldState State(AssassinShadowBody b)=>b.WorldGround.State.Controller;
    private static void Frame(AssassinShadowBody b,bool actions=true)
    {IronGolemWorld.Advance(b,State(b).NextUtc,actions);Require(!b.Deleted&&State(b).Fault==null,"Master world fault: "+State(b)?.Fault);}
    private static BaseCreature Target(AssassinShadowBody b,int dx=5)
    {
        var t=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};t.SetHits(65000);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}
        t.MoveToWorld(new(b.X+dx,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    public static void Configure(){Register("shadow-master-world-check",false);Register("shadow-master-world-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Master AI106 world/actions/movement/repeat memory and saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Follow(AssassinShadowBody b,PlayerMobile owner)
    {
        var start=b.Location;owner.MoveToWorld(new(owner.X+15,owner.Y,owner.Z),owner.Map);
        for(int i=0;i<200;i++){Frame(b);}
        Require(b.Location!=start&&DruidWolfGeometry.Full(PetOwnerRoutes.Cell(b),PetOwnerRoutes.Cell(owner))<32,"Distinct Master AI/shared transport did not follow owner");
        ShadowActions.Cancel(b);owner.MoveToWorld(new(6260,3608,0),owner.Map);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        b.WorldGround.Store(IronGolemWorld.Park(State(b))with{Motion=DruidRavenMotion.Center(b.X*2,b.Y*2),Target=0});
    }
    private static void Combat(AssassinShadowBody b,PlayerMobile owner,BaseCreature enemy)
    {
        int hp=SkillHealth.Balance256(enemy),mana=NecromancerEffects.ManaBalance256(owner);var skills=new HashSet<int>();int scans=0;var previous=ShadowMasterWorld.ObservedScan;
        ShadowMasterWorld.ObservedScan=(actor,result)=>{if(actor==b){scans++;Require(result.Within32>0,"Actual Master scan missed its enemy");}previous?.Invoke(actor,result);};
        try
        {
            for(int i=0;i<600&&SkillHealth.Balance256(enemy)==hp;i++)
            {Frame(b);if(ShadowActions.State(b)is{} action){skills.Add(action.Cast.Skill);}FireBlasts.Tick(State(b).NextUtc,true);}
            Require(SkillHealth.Balance256(enemy)<hp,"Master AI did not cause actual damage;state="+Json(State(b))+" gaps="+Json(b.Prepared.GroundMotion.Gaps));
            Require(scans>0&&b.Prepared.GroundMotion.Master.Rank==b.Prepared.Capture.Rank&&b.Prepared.GroundMotion.Warrior==new ShadowWarriorMemory(),"Master used another brain or lost captured rank");
            Require(NecromancerEffects.ManaBalance256(owner)==mana,"Master AI borrowed player mana");
            // Enabling a candidate changes normal selection order. Exercise a
            // declared unavailable repeat directly instead of relying on a
            // particular unsupported skill being randomly tried before damage.
            ShadowActions.Cancel(b);b.ChangeMasterMemory(b.Prepared.GroundMotion.Master with{Repeat=1,Skill=266});
            for(int i=0;i<200&&b.Prepared.GroundMotion.Master.Repeat>0;i++){Frame(b);}
            Require(b.Prepared.GroundMotion.Master.Repeat==0&&b.Prepared.GroundMotion.Gaps?.Any(g=>g.Skill==266&&g.Reason.Contains("consumer",StringComparison.Ordinal))==true,"Unavailable intrinsic repeat was not decremented/recorded");
            ShadowActions.Cancel(b);
        }
        finally{ShadowMasterWorld.ObservedScan=previous;}
    }
    private static void NativeScan(AssassinShadowBody b,PlayerMobile owner)
    {
        var creatures=new List<EncounterCreature>();var old=ShadowMasterWorld.ObservedScan;ShadowMasterScan observed=null;
        var sentry=AssassinSentryBody.CreateEmerging(owner,new(b.X+5,b.Y,b.Z),TimedEffects.Now.AddMinutes(40),SourceRandom.InitLow(17));
        try
        {
            foreach(var grade in new[]{EncounterGrade.Champion,EncounterGrade.Unique})
            {
                var t=new EncounterCreature();var profile=EncounterProfiles.All.First(p=>p.Archetype==MonsterArchetype.Tank);t.Assign(profile,60,MonsterDifficulty.Normal,grade,ChampionKind.Champion,[],EliteAura.None,null,new Random(1));
                t.Frozen=t.CantWalk=true;t.NoKillAwards=true;t.SetHits(65000);t.MoveToWorld(new(b.X+2+creatures.Count,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);creatures.Add(t);
            }
            ShadowMasterWorld.ObservedScan=(actor,result)=>{if(actor==b){observed=result;}old?.Invoke(actor,result);};
            b.WorldGround.Store(IronGolemWorld.Park(State(b)));for(int i=0;i<100&&observed==null;i++){Frame(b);}
            Require(observed is{Traps:>=1,Within32:>=2},"Native Master callback missed invulnerable aligned trap or elite enemies");
            var ordered=ShadowMasterWorld.Adjacent(b,IronGolemWorld.Cell(State(b))).Where(m=>creatures.Contains(m)).ToArray();
            Require(observed.LastSpecial==(int)ordered.Last().Serial.Value&&observed.Near>=2,"Native champion/unique special-target order or close count changed");
        }
        finally{ShadowMasterWorld.ObservedScan=old;ShadowActions.Cancel(b);foreach(var t in creatures){t.Delete();}sentry.Delete();AssassinTrapRoster.Prune();}
    }
    private static void Queries(AssassinShadowBody b,PlayerMobile owner)
    {
        Require(ShadowSkillCasting.Range(b,0)==1&&ShadowSkillCasting.Range(b,261)==2,"Original dual-claw both/ranged skill resolution");
        Require(SkillMissileRanges.Range(ItemSkillExecution.MissileId("shock field in air"))==50,"Pinned numeric missile base range");
        bool expressionRefused=false;try{_=SkillMissileRanges.Range(ItemSkillExecution.MissileId("blade creeper"));}catch(NotSupportedException){expressionRefused=true;}
        Require(expressionRefused&&ShadowMasterWorld.CandidateRange(257,ItemSkillExecution.MissileId("blade creeper"),b.OwnedSkillRank(257))==10+5*b.OwnedSkillRank(257)
            &&!b.Prepared.GroundMotion.Gaps.Any(g=>g.Skill==257&&g.Reason.Contains("expression",StringComparison.Ordinal)),"Approved Master Sentinel scope/default range gate changed");
        var shadow=Json(b.Prepared);var malformed=b.Prepared with{GroundMotion=b.Prepared.GroundMotion with{Master=new(-1,0,b.Prepared.Capture.Rank)}};bool refused=false;
        try{b.RestorePrepared(malformed);}catch(InvalidDataException){refused=true;}
        Require(refused&&Json(b.Prepared)==shadow,"Invalid Master memory partially replaced original state");
        var clone=b.Prepared;if(clone.GroundMotion.Gaps is{Length:>0}){clone.GroundMotion.Gaps[0]=new(428,"changed");Require(b.Prepared.GroundMotion.Gaps[0]!=clone.GroundMotion.Gaps[0],"Diagnostic array escaped saved-state copy");}
        var before=State(b);var random=NpcEquipmentRandom.Require(b);
        Require(IronGolemWorld.MovePrepared(b,new(b.X*2+4,b.Y*2),true),"Master source run path refused");
        Require(ShadowEquipmentDefense.World(b,false,true)==0&&NpcEquipmentRandom.Require(b)==random,"Moving Master rolled Weapon Block");Frame(b);
        ShadowActions.Cancel(b);b.WorldGround.Store(IronGolemWorld.Park(State(b)));
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody b;BaseCreature target;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);b=(AssassinShadowBody)World.FindMobile((Serial)saved.Actor);target=(BaseCreature)World.FindMobile((Serial)saved.Target);
            Require(b.SavedText==saved.Body&&Json(GeneratedNpcLiveResources.State(b))==saved.Resources&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(saved.Items)
                &&NpcEquipmentRandom.Require(b)==saved.Random&&SkillHealth.Balance256(b)==saved.Life&&SkillHealth.Balance256(target)==saved.TargetLife,"Reload lost Master body/equipment/wounds/RNG/target or repeat state");
            Require(b.Prepared.GroundMotion.Master is{Repeat:3,Skill:266}&&ShadowActions.State(b)==null,"Reload reset repeat memory or replayed cast");
            owner.MoveToWorld(new(6260,3608,0),Map.Trammel);PersistentMinions.Resume(owner);if(b.Map==Map.Internal){b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);}
            int hp=SkillHealth.Balance256(target);var random=NpcEquipmentRandom.Require(b);Frame(b);
            Require(NpcEquipmentRandom.Require(b)==random&&SkillHealth.Balance256(target)==hp&&b.Prepared.GroundMotion.Master.Repeat==3,"Loaded skip replayed an action/RNG or cleared repeat state");
            // This is a synthetic own-class repeat input,not a claim that
            // do48 Blade Fury generates do19 repeat state. The unavailable
            // consumer must decrement/record while other skills continue.
            Combat(b,owner,target);Require(b.Prepared.GroundMotion.Master.Repeat<3&&b.Prepared.GroundMotion.Gaps.Any(g=>g.Skill==266),"Unavailable retained repeated skill did not decrement/record/continue");target.Delete();
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3608);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.chargedboltsentry",1);
            b=AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowMaster,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);NpcEquipmentRandom.Set(b,SourceRandom.InitLow(1234));
            ShadowGroundMotion.Activate(b,owner,TimedEffects.Now.AddMinutes(10));Require(State(b).Profile==ShadowGroundMotion.MasterProfile&&b.Prepared.GroundMotion.Master is{Repeat:0,Skill:0}&&!b.CantWalk,"Master did not receive its distinct world profile");
            Follow(b,owner);target=Target(b);NecromancerEffects.SetMana256(owner,0);Combat(b,owner,target);target.Delete();
        }
        NativeScan(b,owner);Queries(b,owner);ShadowActions.Cancel(b);
        // Retain real original inventory/resources with a synthetic repeat
        // input. Actual do19 production is proved by the separate full-source
        // policy controls;no off-class skill is inserted into this body.
        b.ChangeSkills(book=>book.Assign(266,3));b.ChangeMasterMemory(new(3,266,b.Prepared.Capture.Rank));
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var state=State(b);b.WorldGround.Store(IronGolemWorld.Park(state)with{Motion=DruidRavenMotion.Center(b.X*2,b.Y*2),Target=0,NextUtc=TimedEffects.Now.AddMinutes(30)});
        target=Target(b,3);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),target.Serial.Value,SkillHealth.Balance256(target),generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowMaster)&&!AssassinCombat.Implemented(CombatAbility.ShadowWarrior),"Internal Master bridge enabled unfinished learned summons");
        Log.Information("Master world:distinct AI106/native sectors,own skill/RNG and shared transport/contact/actions,target queries and actual damage,follow/run/moving block,explicit unsupported consumers,saved original state and nontrivial repeated input,no replay and resumed damage after two reloads;learned/world parity gates remain");
    }
}
