// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ValkyrieMotionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieMotionChecks));
    private sealed record Held(uint Body,string Text,string[] Items,NpcEquipmentSeed Random,int Life,int X,int Y,int Map);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-motion-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-motion-check",false);Register("valkyrie-motion-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Valkyrie shared ground movement/source Evade and reloads",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Store(AmazonValkyrieBody b)=>new(b.Serial.Value,b.SavedText,ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.State(b),SkillHealth.Balance256(b),b.X,b.Y,b.Map.MapID);
    private static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="ValkyrieMotionFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6200,110,3680);return p;
    }
    private static IronGolemWorldState State(AmazonValkyrieBody b)=>b.Ground.State.Controller;
    private static void Through(AmazonValkyrieBody b,int frame,bool actions=true)
    {
        var s=State(b);Require(frame>s.Frame,"Reverse Valkyrie world time");IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
        Require(!b.Deleted&&State(b).Fault==null,"Valkyrie controller fault/deletion");
    }
    private static ulong EvadeSeed(AmazonValkyrieBody b)
    {
        var passive=ValkyrieEquipmentDamage.Passives(b);int evade=passive.Single(p=>p.Stat==340).Value,avoid=passive.Single(p=>p.Stat==339).Value,dodge=passive.Single(p=>p.Stat==338).Value;
        for(int i=0;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;int roll=(int)(SourceRandom.Roll(ref copy)%100);if(roll>=Math.Max(avoid,dodge)&&roll<evade){return seed;}}
        throw new InvalidOperationException("No seed distinguishes moving Evade from stationary Dodge/Avoid");
    }
    private static void Defense(AmazonValkyrieBody b,PlayerMobile owner,int mode)
    {
        Require(ValkyrieGroundMotion.Mode(b)==mode,"Actual world source mode missing");
        var attacker=new GraveyardSkeleton{Frozen=true,CantWalk=true};attacker.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);attacker.AIObject.AITimer.Stop();
        var caster=new PlayerMobile{Name="Unassigned hostile Evade fixture",Body=0x190,Player=true};caster.MoveToWorld(new(b.X+2,b.Y,b.Z),b.Map);
        try
        {
            ulong seed=EvadeSeed(b),expected=seed;SourceRandom.Roll(ref expected);int life=SkillHealth.Balance256(b);
            NpcEquipmentRandom.Set(b,seed);Require(ClassStats.CheckHit(attacker,b,0,()=>0,guaranteed:true)==false&&NpcEquipmentRandom.State(b).Seed==expected,"Actual moving melee used stationary Dodge or wrong RNG");
            NpcEquipmentRandom.Set(b,seed);Require(NpcMeleeResult.Resolve(attacker,b,_=>true,()=>true,_=>0)==0&&NpcEquipmentRandom.State(b).Seed==expected,"Ordered moving Evade copied a false Dodge bit or rolled twice");
            NpcEquipmentRandom.Set(b,seed);Require(SkillCombat.FireImpact(caster,b,2560)==0&&SkillHealth.Balance256(b)==life&&NpcEquipmentRandom.State(b).Seed==expected,"Actual moving missile bypassed Evade or committed HP");
        }
        finally{attacker.Delete();caster.Delete();}
    }
    private static void WalkRun(AmazonValkyrieBody b,PlayerMobile owner)
    {
        foreach(bool run in new[]{false,true})
        {
            Require(IronGolemWorld.MovePrepared(b,new(b.X*2+8,b.Y*2),run),"Source equipped path refused clear ground");var before=State(b);
            Require(before.Mode==(run?15:2)&&before.Motion.Points.Length>0,"Source path did not own walk/run mode");Defense(b,owner,before.Mode);
            var detached=b.Prepared;detached.GroundMotion.World.Controller.Motion.Points[0]=new(1,1);
            Require(State(b).Motion.Points[0]!=new Point2D(1,1),"Mutable source points escaped detached body snapshot");
            var speed=ValkyrieGroundMotion.Speed(b,0,run);var plan=ValkyrieEquipmentAnimation.Plan(b,run?15:2,0,run?125:75);
            Require(speed==(plan.Velocity,plan.Speed),"Original equipment movement bypassed transformed rate plan");
            var expected=SourceActorMotion.Advance(before.Motion with{Velocity=speed.Velocity,MaximumVelocity=speed.Velocity},(x,y)=>PetGroundWorld.Collision(b,x,y));
            ulong seed=NpcEquipmentRandom.State(b).Seed;Through(b,before.Frame+1);var after=State(b);
            Require(Json(after.Motion)==Json(expected.State)&&after.Mode==(expected.Moving?before.Mode:1)&&NpcEquipmentRandom.State(b).Seed==seed,"Actual equipped frame changed source position/vector/mode or consumed motion RNG");
            Require(b.X==(IronGolemWorld.Cell(after).X+1)/2&&b.Y==(IronGolemWorld.Cell(after).Y+1)/2,"Native Valkyrie projection diverged from owned source pose");
            b.Ground.Store(IronGolemWorld.Park(after));
        }
        // A manually changed native pose cannot be mistaken for a source mode.
        b.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);bool refused=false;var random=NpcEquipmentRandom.State(b);
        try{ClassStats.CheckHit(owner,b,0,()=>throw new InvalidOperationException("Accuracy drew before pose validation"),guaranteed:true);}catch(NotSupportedException){refused=true;}
        Require(refused&&NpcEquipmentRandom.State(b)==random,"Unowned pose change consumed defender RNG");
        Through(b,State(b).Frame+1);Require(ValkyrieGroundMotion.Mode(b)is 1 or 2,"Controller did not repair native/source pose");
    }
    private static void Follow(AmazonValkyrieBody b,PlayerMobile owner)
    {
        var start=b.Location;owner.MoveToWorld(new(owner.X+15,owner.Y,owner.Z),owner.Map);Through(b,State(b).Frame+50);
        Require(b.Location!=start&&DruidWolfGeometry.Full(PetOwnerRoutes.Cell(b),PetOwnerRoutes.Cell(owner))<32&&State(b).Ai.RememberedOwner==(int)owner.Serial.Value,"Shared AI67 did not automatically follow its Amazon owner");
        owner.MoveToWorld(new(owner.X+32,owner.Y,owner.Z),owner.Map);Through(b,State(b).Frame+70);
        Require(b.InRange(owner,12),"Shared long-leash teleport/follow did not close distance");
        b.Ground.Store(IronGolemWorld.Park(State(b)));int life=SkillHealth.Balance256(b);ulong seed=NpcEquipmentRandom.State(b).Seed;
        ClassCreation.Profiles.SelectHistory(owner,DiabloClass.Warlock);Through(b,State(b).Frame+1);
        Require(b.Map==Map.Internal&&SkillHealth.Balance256(b)==life&&NpcEquipmentRandom.State(b).Seed==seed,"Inactive Amazon motion parked wrong class,healed or fought");
        ClassCreation.Profiles.SelectHistory(owner,DiabloClass.Amazon);PersistentMinions.Resume(owner);
        Through(b,State(b).Frame+1);Require(b.Map==owner.Map,"Amazon motion did not resume after class return");
    }
    private static void OffenseRefusal(AmazonValkyrieBody b,PlayerMobile owner)
    {
        b.Ground.Store(IronGolemWorld.Park(State(b)));var target=new GraveyardSkeleton{Frozen=true,CantWalk=true};target.MoveToWorld(b.Location,b.Map);target.AIObject.AITimer.Stop();
        try
        {
            ulong seed=0,expected=0;
            for(int i=0;i<10000;i++){ulong candidate=SourceRandom.InitLow(i),copy=candidate;SourceRandom.Roll(ref copy);if(SourceRandom.Roll(ref copy)%100<80){seed=candidate;expected=copy;break;}}
            Require(seed!=0,"Missing controlled AI attack seed");NpcEquipmentRandom.Set(b,seed);int life=SkillHealth.Balance256(target);bool refused=false;
            try{Through(b,State(b).Frame+1);}catch(NotSupportedException e){refused=e.Message.Contains("full item-event/contact binding");}
            Require(refused&&SkillHealth.Balance256(target)==life&&NpcEquipmentRandom.State(b).Seed==expected&&State(b).Attempts==0,"Automatic AI used legacy offense or consumed hit/damage RNG");
            b.Ground.Store(IronGolemWorld.Park(State(b)));
        }
        finally{target.Delete();}
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;int generation=0;var bodies=new List<AmazonValkyrieBody>();
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Motion reload count");generation=saved.Generation+1;owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(owner is{Alive:true,Deleted:false},"Motion owner lost");
            foreach(var h in saved.Bodies)
            {
                var b=World.FindMobile((Serial)h.Body)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&Json(Store(b))==Json(h),"Original motion/body/items/wounds/seed changed on actual reload");
                var before=Store(b);Through(b,State(b).Frame+1,false);Require(SkillHealth.Balance256(b)==before.Life&&NpcEquipmentRandom.State(b)==before.Random&&b.Map==Map.Internal,"Load replayed movement/regen/RNG");bodies.Add(b);
            }
        }
        else
        {
            owner=Owner();using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var cases=doc.RootElement.EnumerateArray().Where(c=>c.GetProperty("Critical").GetInt32()==1).Take(2).ToArray();
            foreach(var c in cases)
            {
                var b=ValkyrieDamageChecks.Body(owner,c);ValkyrieEquipmentDefense.Activate(b);var old=Store(b);bool refused=false;
                try{ValkyrieGroundMotion.Activate(b,75,125,TimedEffects.Now.AddHours(1));}catch(NotSupportedException){refused=true;}
                Require(refused&&Json(Store(b))==Json(old),"Missing resource basis silently dropped original item regeneration");
                ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);old=Store(b);refused=false;
                try{ValkyrieGroundMotion.Activate(b,10001,125,TimedEffects.Now.AddHours(1));}catch(InvalidDataException){refused=true;}
                Require(refused&&Json(Store(b))==Json(old),"Invalid motion rates partially changed original body");
                var at=TimedEffects.Now.AddHours(1);ValkyrieGroundMotion.Activate(b,75,125,at);
                Require(!b.CantWalk&&State(b)is{Mode:1,NextThink:20,ActionAt:0,EndAt:0}&&NpcEquipmentRandom.State(b)==old.Random,"Explicit NU/source20first-think admission changed seed");bodies.Add(b);
                SkillHealth.SetBalance256(b,b.MaximumLife256-1000);int life=SkillHealth.Balance256(b),delta=300+NpcEquipmentRegeneration.ItemRate(b);Through(b,1);
                Require(SkillHealth.Balance256(b)==Math.Min(b.MaximumLife256,life+delta)&&NpcEquipmentRandom.State(b)==old.Random,"World motion lost supplied/item regeneration or drew RNG before first think");
                life=SkillHealth.Balance256(b);b.AdvanceRecovery(at.AddMilliseconds(40),true);Require(SkillHealth.Balance256(b)==life,"Old recovery entry double-counted the controller frame");TimedEffects.PreventHealing(b,TimedEffects.Now);
                WalkRun(b,owner);Follow(b,owner);OffenseRefusal(b,owner);PersistentMinions.Suspend(owner);PersistentMinions.Resume(owner);
            }
            // Offense is an explicit separate capability,never a legacy attack.
            bool rejected=false;try{bodies[0].BeginMelee(owner);}catch(NotSupportedException){rejected=true;}Require(rejected,"Movement enabled unfinished ordinary offense");
        }
        PersistentMinions.Suspend(owner);owner.Internalize();var held=bodies.Select(Store).ToArray();
        Require(bodies.All(b=>b.Prepared.GroundMotion is{WalkVelocityPercent:75,RunVelocityPercent:125}&&b.Map==Map.Internal),"Saved motion recaptured bases or enabled cast");
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,held,generation)));
        Log.Information("Valkyrie motion:actual shared walk/run/follow and source-mode melee/missile Evade;original rates/items/wounds/stream through parking/two reloads;offense and learned cast remain gated");
    }
}
