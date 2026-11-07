// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowGroundChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowGroundChecks));
    private sealed record Held(uint Body,string Text,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held Held,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-ground-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    public static void Configure(){Register("shadow-ground-check",false);Register("shadow-ground-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Warrior AI105 over shared original equipped ground transport",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static ShadowAiSkill Skill(int[] a)=>new(a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7]!=0,a[8]!=0,a[9]!=0,a[10]);
    private static void Source()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-warrior-ai-cases.json")));int count=0;
        foreach(var row in d.RootElement.EnumerateArray())
        {
            int[] c=Ints(row.GetProperty("Context")),l=Ints(row.GetProperty("Left")),r=Ints(row.GetProperty("Right"));
            var left=Skill(l);var right=Skill(r);var attack=new ShadowAiSkill(0,-1,4,4,1,0,0,false,false,false,1);bool present=c[26]==0;int pct=0;
            var trace=new List<int[]>();void A(params int[] values)=>trace.Add(values);
            ShadowAiSkill Get(int id)=>id==0?(present?attack:null):id==left.Id?left:id==right.Id?right:null;
            int[] Flags(ShadowAiSkill s)=>s.Id==left.Id?l:r;
            var services=new ShadowWarriorServices(()=>c[0]!=0,()=>c[16]!=0?left.Id:null,()=>c[17]!=0?right.Id:null,
                (a,b)=>A(1,a,b),Get,s=>s.Id!=c[24],s=>Flags(s)[11]!=0,s=>Flags(s)[12]!=0,s=>Flags(s)[11]!=0&&Flags(s)[13]>=3,
                ()=>{A(2,c[18]);return(uint)c[18];},n=>{int v=c[23]%Math.Max(n,1);A(4,n,v);return v;},()=>{int v=c[19+pct++%4];A(3,v);return v;},
                (t,combat,f,leash)=>{A(5,t,combat?1:0,f,leash);return c[15]!=0;},n=>A(6,n),t=>A(7,t),(s,t)=>A(8,s.Id,s.Mode,t),()=>{present=true;A(9,0,1);});
            var memory=ShadowWarriorAi.Think(new(c[1],c[2],c[3],c[4]!=0,c[5],c[6],c[7],c[8],c[9],c[10],c[11],c[12]),new(c[13],c[14]),services);
            var expected=row.GetProperty("Expected");Require(Json(new[]{memory.AllowedFrame,memory.Budget})==expected[0].GetRawText()&&Json(trace)==expected[1].GetRawText(),"Whole AI105 policy/RNG mismatch at "+count+": "+Json(trace)+" expected "+expected.GetRawText());count++;
        }
        Require(count==323,"Missing whole AI105 controls");
        using var m=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-ground-cases.json")));count=0;
        foreach(var row in m.RootElement.EnumerateArray())
        {
            int mode=row.GetProperty("Mode").GetInt32();var speed=SourceActorMotion.Speed(6,mode==2?213:101,row.GetProperty("Percent").GetInt32(),row.GetProperty("Faster").GetInt32());var expected=Ints(row.GetProperty("Expected"));
            Require(expected[0]==0&&expected[1]==6&&expected[2]==(mode==2?2:3)&&speed==(expected[3],expected[4]),"Converted Shadow motion rate mismatch");count++;
        }
        Require(count==140,"Missing Shadow locomotion controls");
    }
    private static IronGolemWorldState State(AssassinShadowBody b)=>b.WorldGround.State.Controller;
    private static void Through(AssassinShadowBody b,int frame,bool actions=true)
    {
        var s=State(b);Require(frame>s.Frame,"Reverse Shadow ground time");IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
        Require(!b.Deleted&&State(b).Fault==null,"Shadow controller fault/deletion");
    }
    private static void Motion(AssassinShadowBody b,PlayerMobile owner)
    {
        foreach(bool run in new[]{false,true})
        {
            Require(IronGolemWorld.MovePrepared(b,new(b.X*2+8,b.Y*2),run),"Shadow source path refused clear ground");var before=State(b);
            Require(before.Mode==(run?15:2),"Shadow has no actual walk/run source mode");var random=NpcEquipmentRandom.Require(b);
            Require(ShadowEquipmentDefense.World(b,false,true)==0&&NpcEquipmentRandom.Require(b)==random,"Moving Shadow incorrectly rolled Weapon Block");
            var speed=ShadowGroundMotion.Speed(b,0,run);var graph=b.EquipmentNumbers;
            Require(speed==SourceActorMotion.Speed(6,run?101:213,graph.Read(67)+(run?50:0),graph.Read(96)),"Shadow movement missed own velocity/FRW");
            var expected=SourceActorMotion.Advance(before.Motion with{Velocity=speed.Velocity,MaximumVelocity=speed.Velocity},(x,y)=>PetGroundWorld.Collision(b,x,y));
            Through(b,before.Frame+1);Require(Json(State(b).Motion)==Json(expected.State)&&NpcEquipmentRandom.Require(b)==random,"Shadow ground frame differs from source transport or draws RNG");
            var detached=b.Prepared;detached.GroundMotion.World.Controller.Motion.Points[0]=new(1,1);
            Require(State(b).Motion.Points[0]!=new Point2D(1,1),"Shadow mutable path escaped snapshot");b.WorldGround.Store(IronGolemWorld.Park(State(b)));
        }
        var start=b.Location;owner.MoveToWorld(new(owner.X+15,owner.Y,owner.Z),owner.Map);Through(b,State(b).Frame+80);
        Require(b.Location!=start&&DruidWolfGeometry.Full(PetOwnerRoutes.Cell(b),PetOwnerRoutes.Cell(owner))<32,"Warrior own AI/common helper did not follow owner");
    }
    private static void Fight(AssassinShadowBody b,PlayerMobile owner)
    {
        b.WorldGround.Suspend();b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);((IPersistentMinion)b).ResumeActions();
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.BasicAttack),"Cannot select owner Attack controls");
        var target=new ValkyrieContactChecks.Recipient();target.SetHits(65000);target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();
        try
        {
            int mana=NecromancerEffects.ManaBalance256(owner),hp=SkillHealth.Balance256(target);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();
            Through(b,State(b).Frame+100);
            Require(State(b).Attempts>0&&SkillHealth.Balance256(target)<hp&&b.Prepared.GroundMotion.Warrior.AllowedFrame>0,"Own Warrior AI did not release ordinary original melee");
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"Warrior borrowed player mana or changed original inventory");
            b.InterruptForRecovery();Require(State(b).Mode==1&&State(b).ActionAt==0,"Recovery retained pending ground melee");
            target.MoveToWorld(new(b.X+6,b.Y,b.Z),b.Map);
            Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.FireBlast)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.FireBlast),"Cannot select owner Fire Blast controls");
            // Common-follow exits without RNG for this near-owner target.
            // Find a seed which passes the policy's cost/budget rolls.
            ulong chosen=0;
            for(int i=1;i<10000;i++)
            {
                ulong seed=SourceRandom.InitLow(i),probe=seed;SourceRandom.Roll(ref probe);
                if(SourceRandom.Roll(ref probe)%100>90){continue;}
                uint budgetRoll=SourceRandom.Roll(ref probe)%5,percentage=SourceRandom.Roll(ref probe)%100;
                if(budgetRoll<=percentage){chosen=seed;break;}
            }
            Require(chosen!=0,"No passing Warrior policy seed");NpcEquipmentRandom.Set(b,chosen);
            // The preceding melee cooldown is intentionally retained; wait
            // before requesting a spell rather than resetting its memory.
            var state=State(b);b.WorldGround.Store(state with{NextThink=Math.Max(state.Frame+1,b.Prepared.GroundMotion.Warrior.AllowedFrame)});
            Through(b,State(b).NextThink);var action=ShadowActions.State(b);
            Require(action?.Cast.Skill==251&&ShadowGroundMotion.Mode(b)==9,"Warrior AI did not enter its own Fire Blast S2");
            int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==2).Frame;var allocation=NpcWorldAllocation.Capture();int beforeSpell=SkillHealth.Balance256(target);
            Through(b,State(b).Frame+release);
            var bomb=FireBlasts.Snapshot().SingleOrDefault(x=>x.Npc?.Actor==b.Serial.Value);
            Require(bomb!=null&&NpcWorldAllocation.Capture()!=allocation&&SkillHealth.Balance256(target)==beforeSpell&&NecromancerEffects.ManaBalance256(owner)==mana,"Own AI/S2 handoff did not allocate exactly at its release marker");
            Through(b,State(b).Frame+action.Plan.Events.Single(e=>e.Type==2).Frame-release);
            Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null,"S2 end left a used skill or replayable action");
            FireBlasts.Restore(FireBlasts.Snapshot().Where(x=>x.Id!=bomb.Id).ToArray());
        }
        finally{target.Delete();b.WorldGround.Suspend();}
    }
    private static void Check(bool reload)
    {
        Source();PlayerMobile owner;AssassinShadowBody body;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);body=(AssassinShadowBody)World.FindMobile((Serial)saved.Held.Body);
            Require(Json(Capture(body))==Json(saved.Held)&&ShadowActions.State(body)==null,"Reload changed original ground actor/items/wounds/RNG or replayed cast");
            var before=Capture(body);Through(body,State(body).Frame+1,false);Require(SkillHealth.Balance256(body)==before.Life&&NpcEquipmentRandom.Require(body)==before.Random&&body.Map==Map.Internal,"Offline/load replayed motion,regen or RNG");
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);
            body=AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowWarrior,TimedEffects.Now);body.ChangeSkills(book=>{book.Assign(252,10);book.Assign(263,20);});
            GeneratedNpcLiveResources.ActivateItemEvents(body);ShadowEquipmentDefense.Activate(body);body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
            var random=NpcEquipmentRandom.Require(body);ShadowGroundMotion.Activate(body,owner,TimedEffects.Now.AddMinutes(5));
            Require(!body.CantWalk&&State(body)is{Mode:1,NextThink:20}&&NpcEquipmentRandom.Require(body)==random,"Warrior NU/20frame admission changed RNG");
            SkillHealth.SetBalance256(body,body.MaximumLife256-1000);Through(body,1);int life=SkillHealth.Balance256(body);body.AdvanceRecovery(State(body).NextUtc.AddMilliseconds(-40),true);
            Require(SkillHealth.Balance256(body)==life,"Shadow recovery double-counted controller regeneration");TimedEffects.PreventHealing(body,TimedEffects.Now);
            Motion(body,owner);Fight(body,owner);
        }
        int wounded=SkillHealth.Balance256(body);PersistentMinions.Park(body);Through(body,State(body).Frame+10,false);
        Require(body.Map==Map.Internal&&SkillHealth.Balance256(body)==wounded&&ShadowActions.State(body)==null,"Parked Shadow healed or replayed action");
        PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,Capture(body),generation)));
        Log.Information("Shadow ground:323whole AI105 contexts,140converted motion controls;actual walk/run,facing block restrictions,owner follow,own ordinary melee,recovery,parking and original state/items/wounds/RNG across reloads;Master/other skills/learned route remain gated");
    }
}
