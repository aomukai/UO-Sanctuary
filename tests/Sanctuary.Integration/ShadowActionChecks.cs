// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowActionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowActionChecks));
    private sealed record Held(uint Actor,string Body,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,uint Target,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-actions-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Held Capture(AssassinShadowBody body)=>new(body.Serial.Value,body.SavedText,ShadowLoadout.Originals(body).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(body),SkillHealth.Balance256(body));
    public static void Configure(){Register("shadow-actions-check",false);Register("shadow-actions-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Source Shadow S2 mode/event conductor and lifecycle",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-animation-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var a=row.GetProperty("Rates").EnumerateArray().Select(v=>v.GetInt32()).ToArray();var expected=row.GetProperty("Expected");
            var plan=ShadowAnimation.Supplied(a[0],a[1],a[2],a[3],a[4]);
            Require(plan.Speed==expected[4].GetInt32()&&Json(plan.Events.Select(e=>new[]{e.Type,e.Frame,e.Kind,e.Param}).ToArray())==expected[6].GetRawText(),"Shadow complete source conversion/rate/event control differs");count++;
        }
        Require(count==900,"Missing complete source Shadow clock contexts");
    }
    private static long[] Bombs(AssassinShadowBody body)=>FireBlasts.Snapshot().Where(b=>b.Npc?.Actor==body.Serial.Value).Select(b=>b.Id).ToArray();
    private static void FinishBomb(FireBomb bomb)
    {for(int i=0;i<300&&FireBlasts.Snapshot().SingleOrDefault(b=>b.Id==bomb.Id)is{} current;i++){FireBlasts.Tick(current.NextUtc,true);}Require(!FireBlasts.Snapshot().Any(b=>b.Id==bomb.Id),"Scheduled bomb did not finish");}
    private static void Live(AssassinShadowBody body,PlayerMobile owner,BaseCreature target)
    {
        var at=TimedEffects.Now.AddMinutes(5);var held=Capture(body);var world=NpcWorldAllocation.Capture();int mana=NecromancerEffects.ManaBalance256(owner);
        Require(ShadowActions.BeginFireBlast(body,target,target.Location,at),$"Original NPC action startup refused: actor={body.SourceMonsterId} attached={ShadowSkillCasting.Attached(body)} frozen={body.Frozen} recovery={CreatureHitRecovery.Remaining(body)} overlap={AlliedOverlap.Allowed(body)} rank={body.OwnedSkillRank(251)}");var state=ShadowActions.State(body);
        Require(!ShadowActions.BeginFireBlast(body,target,target.Location,at)&&NpcWorldAllocation.Capture()==world&&Bombs(body).Length==0,"Windup overlapped or allocated a missile early");
        int release=state.Plan.Events.Single(e=>e.Type==1&&e.Kind==2).Frame,end=state.Plan.Events.Single(e=>e.Type==2).Frame;
        ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true);
        Require(Bombs(body).Length==0&&ShadowActions.State(body).Frame==release-1,"S2 emitted before its exact source marker");
        var point=target.Location;target.MoveToWorld(new(point.X,point.Y+1,point.Z),target.Map);
        ShadowActions.Tick(at.AddMilliseconds(release*40),true);var bomb=FireBlasts.Snapshot().Single(b=>b.Npc?.Actor==body.Serial.Value);
        Require(bomb.Vy>0&&bomb.NextUtc==at.AddMilliseconds((release+1)*40)&&ShadowActions.State(body)!=null,"Release did not use live target and actual event time");
        ShadowActions.Tick(at.AddMilliseconds((end+20)*40),true);
        Require(ShadowActions.State(body)==null&&body.OwnedSkills.Used==null&&Bombs(body).Length==1&&NecromancerEffects.ManaBalance256(owner)==mana,"End/catchup replayed a release or borrowed mana");
        FinishBomb(bomb);target.MoveToWorld(point,body.Map);
        Require(ShadowActions.BeginFireBlast(body,target,point,at),"Second action refused");var second=ShadowActions.State(body);
        Require(ShadowSkillCasting.Release(state.Cast,at)==0&&Bombs(body).Length==0,"Completed stale cast released during a newer action");
        ShadowActions.Tick(at.AddMilliseconds((second.Plan.Events.Last().Frame+20)*40),true);
        Require(ShadowActions.State(body)==null&&Bombs(body).Length==1,"Late source-frame catchup did not emit exactly once");FinishBomb(FireBlasts.Snapshot().Single(b=>b.Npc?.Actor==body.Serial.Value));
        foreach(int reason in Enumerable.Range(0,5))
        {
            Require(ShadowActions.BeginFireBlast(body,target,point,at),"Cancellation fixture startup");var pending=ShadowActions.State(body);var before=NpcWorldAllocation.Capture();
            if(reason==0){body.InterruptForRecovery();}
            if(reason==1){body.Frozen=true;}
            if(reason==2){body.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);}
            if(reason==3){GeneratedNpcLiveResources.SetItemActive(body,ShadowLoadout.Binding(body).Primary,false);}
            if(reason==4){PersistentMinions.Park(body);}
            ShadowActions.Tick(at.AddSeconds(20),true);
            Require(ShadowActions.State(body)==null&&body.OwnedSkills.Used==null&&Bombs(body).Length==0&&NpcWorldAllocation.Capture()==before,"Interrupted/parked/changed action emitted or retained its windup");
            if(reason==1){body.Frozen=false;}
            if(reason==3){GeneratedNpcLiveResources.SetItemActive(body,ShadowLoadout.Binding(body).Primary,true);}
            body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
            Require(ShadowSkillCasting.Release(pending.Cast,at)==0,"Cancelled source action could be replayed");
        }
        Require(body.SavedText==held.Body&&ShadowLoadout.Originals(body).Select(i=>i.SavedText).SequenceEqual(held.Items)
            &&NpcEquipmentRandom.Require(body)==held.Random&&SkillHealth.Balance256(body)==held.Life,"S2 conductor changed original body/items/wounds/actor RNG");
    }
    private static void Check(bool reload)
    {
        Source();PlayerMobile owner;BaseCreature target;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);target=(BaseCreature)World.FindMobile((Serial)saved.Target);
            bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(ShadowActions.State(bodies[i])==null&&Json(Capture(bodies[i]))==Json(saved.Bodies[i]),"Reload replayed an action or changed saved original actor");}
            owner.MoveToWorld(new(6180,3400,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6180,24,3400);ClassSkillChecks.Train(owner,"assassin.fireblast",20);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();
            foreach(var body in bodies)
            {if(body.Prepared.Capture.Ability==CombatAbility.ShadowWarrior){body.ChangeSkills(book=>book.Mirror(body.Prepared.Capture,251,0));}GeneratedNpcLiveResources.ActivateItemEvents(body);ShadowEquipmentDefense.Activate(body);}
            target=new ValkyrieContactChecks.Recipient();target.SetHits(65000);target.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
        }
        foreach(var body in bodies){body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Live(body,owner,target);PersistentMinions.Park(body);}
        PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,target.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Log.Information("Shadow actions:900complete source rate/event contexts;both species exact S2 release/end,live aim,one late catchup release,stale-event rejection,recovery/freeze/move/item/park cancellation and original saved bodies/items/wounds/RNG;AI/other skills/learned routes remain gated");
    }
}
