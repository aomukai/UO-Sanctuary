// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class TaintedGroundMovementChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(TaintedGroundMovementChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,string Birth,DeathMarkSavedActor Source,GenericPetSavedPath Path,NpcEquipmentSeed Random,int Life,int Maximum);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"tainted-ground-movement-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"tainted-ground-movement-reload-check":"tainted-ground-movement-check";
            ConsoleInputHandler.RegisterCommand(name,"Shared source ground path/walk/follow/warp with Tainted species inputs",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Scene(true);}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    internal static GenericPetBirthPath PathFor(SkeletonGuardian body)
    {
        // Supplied acceptance context,not an enabled Tainted summon or a
        // claim that D2's native constructor chose these UO path defaults.
        var p=new GenericPetCommitPath{X=((uint)(body.X*2)<<16)|32768,Y=((uint)(body.Y*2)<<16)|32768,Flags=1,
            Room=GenericPetNativePlacement.RoomHandle(body.Map,body.X*2,body.Y*2),Size5c=3,Mask60=0x100,Mask64=0x3401,Actor=new(checked((int)body.Serial.Value),1)};
        return new(new(new(743,0),true,1,743,3,3,0),p,1,1,TimedEffects.Now);
    }
    private static void Scene(bool keep)
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Warlock);owner.Female=true;owner.Name="Tainted ground movement fixture";owner.AddItem(new Backpack());
        ClassCreation.Profiles.AdvanceLevel(owner,47);ClassSkillChecks.Train(owner,"warlock.summontainted",6);ClassSkillChecks.Train(owner,"warlock.demonicmastery",17);
        FixtureSpace.Corridor(owner,6560,28,3620);WarlockTaintedBody body=null;bool saved=false;
        try
        {
            DateTime at=TimedEffects.Now;body=WarlockTaintedBody.CreatePrepared(owner,new(owner.X+2,owner.Y,owner.Z),at,new(1,PathFor));
            NpcEquipmentRandom.Set(body,SourceRandom.InitLow(743));var seed=NpcEquipmentRandom.State(body);var birth=body.Prepared.Snapshot;
            SkillHealth.SetBalance256(body,body.MaximumLife256-257);int life=SkillHealth.Balance256(body);var path=DeathMarkWorld.Path(body);
            Require(DemonGroundMovement.Size(body)==3&&ReviveMeleeGeometry.Unit(body).Size==3&&body.Body==149,"Tainted lost original species footprint/appearance");
            var expected=SourceActorMotion.Speed(6,176,100+birth.Birth.Suppliers.VelocityPercent,0);
            Require(DemonGroundMovement.Speed(body,at)==expected&&DemonGroundMovement.Tainted.WalkFrames==8,"Tainted used Goatman velocity/animation/Frenzy");
            ClassSkillChecks.Train(owner,"warlock.demonicmastery",18);
            Require(body.Prepared.Snapshot==birth&&DemonGroundMovement.Speed(body,at)==expected,"Owner training recaptured Tainted movement");
            int frame=0,idles=0;var events=new GenericPetTimedEvents();var motion=new GoatmanNativeMotion(body,owner,events,()=>frame,()=>at.AddMilliseconds(frame*40),
                ()=>new(frame,DeathMarkWorld.State(body).Mode,1,GenericPetMetadata.Table.StateCount,true,-1,0,4),_=>idles++,DemonGroundMovement.Tainted.Flags241)
                {Random=new(seed.Seed)};
            int x=(int)(path.X>>16),y=(int)(path.Y>>16);var target=new Point2D(x+40,y);int room=path.Room,rooms=0,subtiles=0;
            GenericPetMovement.Prepare(motion.Unit,0,40,13);
            Require(GenericPetMovement.Execute(motion.Unit,motion.Random,2,0,target.X,target.Y,0,4,motion.Services)!=0&&motion.Paths==1,"Shared path planner refused Tainted walk");
            for(;frame<1000;frame++)
            {
                bool moving=motion.Advance();var p=motion.Motion;var geometry=ReviveMeleeGeometry.Unit(body);int cx=(int)(p.X>>16),cy=(int)(p.Y>>16);
                Require(path.X==p.X&&path.Y==p.Y&&body.X==(cx+1)/2&&body.Y==(cy+1)/2&&geometry.Cell==new Point2D(cx,cy)&&geometry.Size==3,"Tainted native/source/geometry positions diverged");
                if(cx!=body.X*2){subtiles++;}if(path.Room!=room){rooms++;Require(path.PreviousRoom==room,"Tainted lost old room link");room=path.Room;}
                Require(SkillHealth.Balance256(body)==life&&NpcEquipmentRandom.State(body)==seed,"Movement healed or rewrote private attack RNG");
                if(!moving){break;}
            }
            Require(frame<1000&&rooms>0&&subtiles>0&&motion.Cell()==target&&motion.Steps>0&&idles>0,"Tainted path did not finish across rooms with fractional cells");
            var before=body.Location;string world=JsonSerializer.Serialize(DeathMarkWorld.Capture());bool refused=false,warpRefused=false;int size=path.Size5c;path.Size5c=2;
            try
            {
                try{GoatmanNativeWalking.Advance(body,motion.Motion);}catch(NotSupportedException){refused=true;}
                try{_=motion.FollowExecution(Position).Warp();}catch(NotSupportedException){warpRefused=true;}
            }
            finally{path.Size5c=size;}
            Require(refused&&warpRefused&&body.Location==before&&JsonSerializer.Serialize(DeathMarkWorld.Capture())==world,"Wrong Goatman footprint silently accepted or partially moved Tainted");
            GenericPetDecisionUnit Position(int id)
            {
                var p=ReviveMeleeGeometry.Unit(body).Cell;return new(1,true,p.X,p.Y);
            }
            Require(motion.FollowExecution(Position).Warp()!=0&&motion.Warps==1&&body.GetDistanceToSqrt(owner)<=4&&DeathMarkWorld.Path(body).Size5c==3,
                "Shared nearby-owner warp lost Tainted path or failed native placement");
            Require(SkillHealth.Balance256(body)==life&&NpcEquipmentRandom.State(body)==seed&&body.Prepared.Snapshot==birth,"Warp recovered or recaptured the pet");
            var flying=new WarlockDefilerBody();refused=false;try{_=DemonGroundMovement.Size(flying);}catch(NotSupportedException){refused=true;}finally{flying.Delete();}
            Require(refused,"Flying Defiler silently entered ground movement");
            Require(!WarlockCombat.Implemented(CombatAbility.SummonTainted),"Movement component incorrectly enabled unfinished Tainted summon");
            if(keep)
            {
                PersistentMinions.Suspend(owner);body.RestorePrepared(body.Prepared with{NextRecoveryUtc=at.AddDays(2)});
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,owner.Serial.Value,body.Serial.Value,body.SavedText,DeathMarkWorld.State(body),
                    GenericPetSavedPath.Capture(DeathMarkWorld.Path(body)),seed,life,body.MaximumLife256)));saved=true;
            }
            Log.Information("Tainted shared ground movement:{Frames} updates,{Rooms} room crossings,{Subtiles} subtile samples;3x3 source footprint,6velocity/176walk,own captured mastery,near-owner warp,private RNG/wounds intact",frame,rooms,subtiles);
        }
        finally{if(!saved){DeathMarkWorld.Remove(body);body?.Delete();owner.Delete();}}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)s.Body)as WarlockTaintedBody;
        Require(b!=null&&b.ControlMaster?.Serial.Value==s.Owner&&b.Map==Map.Internal&&b.SavedText==s.Birth&&DeathMarkWorld.State(b)==s.Source
            &&GenericPetSavedPath.Capture(DeathMarkWorld.Path(b))==s.Path&&NpcEquipmentRandom.State(b)==s.Random
            &&SkillHealth.Balance256(b)==s.Life&&b.MaximumLife256==s.Maximum&&!ReviveMeleeGeometry.Unit(b).HasRoom,"Original Tainted path/birth/wounds/RNG/parked state changed on reload");
        Scene(false);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
