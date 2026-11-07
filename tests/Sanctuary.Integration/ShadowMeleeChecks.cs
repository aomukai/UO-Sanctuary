// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowMeleeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowMeleeChecks));
    private sealed record Held(uint Actor,string Body,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-melee-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    public static void Configure(){Register("shadow-melee-check",false);Register("shadow-melee-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Shadow ordinary melee,masteries and source A1/kick clock",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-melee-cases.json")));int count=0;
        foreach(var row in d.RootElement.EnumerateArray())
        {
            var a=row.GetProperty("Rates").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var expected=row.GetProperty("Expected");
            var plan=ShadowAnimation.SuppliedMelee(a[0],a[1],a[2],a[3],a[4]);
            Require(expected[2].GetInt32()==12&&plan.Speed==expected[4].GetInt32()&&Json(plan.Events.Select(e=>new[]{e.Type,e.Frame,e.Kind,e.Param}).ToArray())==expected[6].GetRawText(),"Complete converted Shadow A1 clock differs");count++;
        }
        Require(count==900,"Missing converted Shadow A1 controls");
    }
    private static ulong HitSeed()
    {for(int i=0;i<100000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;if(SourceRandom.Roll(ref copy)%100==0){return seed;}}throw new InvalidOperationException("No hit seed");}
    private static void Live(AssassinShadowBody body,PlayerMobile owner)
    {
        var target=new ValkyrieContactChecks.Recipient();target.SetHits(65000);target.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
        var held=Capture(body);var at=TimedEffects.Now.AddMinutes(5);var oldBook=body.Prepared.Skills;
        try
        {
            var graph=body.EquipmentNumbers;var weapon=OriginalNpcEquipment.WeaponFrame(body);weapon.Enter();
            Require(weapon.Mastery(0,0,1)>0&&weapon.Mastery(1,0,1)>0&&weapon.Mastery(2,0,1)>0,"Actual claws lost posted Claw Mastery AR/damage/critical layers");
            int expectedAr=checked(graph.Read(119)+weapon.Mastery(0,0,1));
            Require(NpcEquipmentCombat.AttackPercent(body)==expectedAr,"Shadow AR borrowed a Valkyrie/player passive");
            var before=NpcEquipmentRandom.Require(body);var facts=new GeneratedNpcFillTarget(true,true,false,true,0,()=>0,_=>{},(_,_)=>false);
            var fill=OriginalNpcMelee.Allocate(body,facts,new(ResultFlags:1));
            Require(fill.Physical>0&&NpcEquipmentRandom.Require(body).Seed!=before.Seed,"Original full filler failed to roll own claw damage");
            NpcEquipmentRandom.Set(body,HitSeed());int hp=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(owner);var game=NpcWorldAllocation.Capture();
            var actor=NpcEquipmentRandom.Require(body);Require(ShadowActions.BeginMelee(body,target,at),"Ordinary Shadow A1 entry refused");var action=ShadowActions.State(body);
            int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame,end=action.Plan.Events.Single(e=>e.Type==2).Frame;
            ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true);
            Require(SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.Require(body)==actor,"Melee allocated/damaged before source action marker");
            ShadowActions.Tick(at.AddMilliseconds(release*40),true);int wounded=SkillHealth.Balance256(target);
            Require(wounded<hp&&NpcEquipmentRandom.Require(body).Seed!=actor.Seed&&NecromancerEffects.ManaBalance256(owner)==mana&&NpcWorldAllocation.Capture()==game,"Own melee contact failed,borrowed mana or allocated a missile");
            ShadowActions.Tick(at.AddMilliseconds((end+10)*40),true);
            Require(ShadowActions.State(body)==null&&body.OwnedSkills.Used==null&&SkillHealth.Balance256(target)==wounded,"Late end replayed ordinary melee");
            Require(ShadowActions.BeginMelee(body,target,at),"Range-loss fixture startup refused");var pending=ShadowActions.State(body);var random=NpcEquipmentRandom.Require(body);
            target.MoveToWorld(new(owner.X+10,owner.Y,owner.Z),owner.Map);ShadowActions.Tick(at.AddSeconds(20),true);
            Require(SkillHealth.Balance256(target)==wounded&&NpcEquipmentRandom.Require(body)==random,"Out-of-range melee damaged or drew actor RNG");
            target.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
            Require(ShadowActions.BeginMelee(body,target,at),"Park fixture startup refused");random=NpcEquipmentRandom.Require(body);PersistentMinions.Park(body);ShadowActions.Tick(at.AddSeconds(20),true);
            Require(ShadowActions.State(body)==null&&body.OwnedSkills.Used==null&&NpcEquipmentRandom.Require(body)==random&&SkillHealth.Balance256(target)==wounded,"Parking replayed melee");
        }
        finally{ShadowActions.Cancel(body);GeneratedNpcLiveResources.ChangeShadowSkills(body,oldBook);target.Delete();PersistentMinions.Park(body);}
        Require(body.SavedText==held.Body&&ShadowLoadout.Originals(body).Select(i=>i.SavedText).SequenceEqual(held.Items)&&SkillHealth.Balance256(body)==held.Life,"Melee changed original birth/items/wounds");
    }
    private static void Check(bool reload)
    {
        Source();PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed original melee actor or replayed action");}owner.MoveToWorld(new(6260,3480,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();
            foreach(var body in bodies){body.ChangeSkills(book=>book.Assign(252,10));GeneratedNpcLiveResources.ActivateItemEvents(body);ShadowEquipmentDefense.Activate(body);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)*2/3-1);}
        }
        foreach(var body in bodies){body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Live(body,owner);}
        PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Log.Information("Shadow melee:900complete converted A1/kick clocks;both original species use shared full fill/contact with own Claw Mastery AR/damage/critical,source marker allocation,range recheck,late/cancel/park handling and saved original items/wounds/RNG;movement/AI/learned routes remain gated");
    }
}
