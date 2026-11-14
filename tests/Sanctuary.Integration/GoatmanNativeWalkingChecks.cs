// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GoatmanNativeWalkingChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GoatmanNativeWalkingChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,string Birth,DeathMarkSavedActor Controller,GenericPetSavedPath Path,
        int Life,int Maximum,NpcEquipmentSeed Random);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-native-walking-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"goatman-native-walking-reload-check":"goatman-native-walking-check";
            ConsoleInputHandler.RegisterCommand(name,"Registered source walking/native room and geometry transport",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Start();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static GenericPetBirthPath Fixture(SkeletonGuardian b)
    {
        // Explicit input to this walking acceptance,NOT production birth
        // defaults. Active coordinate updates are required by Coordinates.
        var path=new GenericPetCommitPath{X=((uint)(b.X*2)<<16)|32768,Y=((uint)(b.Y*2)<<16)|32768,Flags=1,
            Room=GenericPetNativePlacement.RoomHandle(b.Map,b.X*2,b.Y*2),Size5c=2,Mask60=0x100,Mask64=0x3401,Actor=new(checked((int)b.Serial.Value),1)};
        return new(new(new(742,0),true,1,742,2,2,0),path,1,1,TimedEffects.Now);
    }
    private static void Start()
    {
        var p=new PlayerMobile{Name="Goatman walking transport fixture",Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,47);
        ClassSkillChecks.Train(p,"warlock.summongoatman",6);ClassSkillChecks.Train(p,"warlock.demonicmastery",16);
        FixtureSpace.Corridor(p,6560,28,3500);WarlockGoatmanBody b=null;bool kept=false;
        try
        {
            b=WarlockGoatmanBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now,new(1,Fixture));
            NpcEquipmentRandom.Set(b,SourceRandom.InitLow(173));var seed=NpcEquipmentRandom.State(b);int life=b.MaximumLife256-257;
            SkillHealth.SetBalance256(b,life);var path=DeathMarkWorld.Path(b);int oldRoom=path.Room;
            int sx=checked((int)(path.X>>16)),sy=checked((int)(path.Y>>16));var target=new Point2D(sx+40,sy);
            var speed=GoatmanNpcBodyActions.Movement(b,TimedEffects.Now);
            var motion=DruidRavenMotion.Center(sx,sy) with{Velocity=speed.Velocity,MaximumVelocity=speed.Velocity,
                Points=Enumerable.Range(1,40).Select(i=>new Point2D(sx+i,sy)).ToArray(),FinalTarget=target,Distance=40,Flags=0x20};
            motion=SourceActorMotion.DirectionToNext(motion);int frames=0,crossings=0,subtile=0;
            for(;frames<1000;frames++)
            {
                var result=GoatmanNativeWalking.Advance(b,motion);motion=result.State;
                int cx=checked((int)(motion.X>>16)),cy=checked((int)(motion.Y>>16));var unit=ReviveMeleeGeometry.Unit(b);
                Require(path.X==motion.X&&path.Y==motion.Y&&b.X==(cx+1)/2&&b.Y==(cy+1)/2&&unit.Cell==new Point2D(cx,cy),"Source/native/combat positions diverged");
                Require(SkillHealth.Balance256(b)==life&&NpcEquipmentRandom.State(b)==seed,"Walking changed wounds or consumed combat RNG");
                if(cx!=b.X*2){subtile++;Require(unit.Cell!=PetOwnerRoutes.Cell(b),"Combat geometry used rounded native tile instead of registered source position");}
                if(path.Room!=oldRoom)
                {
                    Require(path.PreviousRoom==oldRoom,"Source previous-room link lost");var image=DeathMarkWorld.Capture();
                    Require(image.Worlds.Single(w=>w.Map==b.Map.MapID).Members.Single(m=>m.Handle==checked((int)b.Serial.Value)).Path.Room==path.Room,"Room graph did not publish new membership");
                    crossings++;oldRoom=path.Room;
                }
                if(!result.Moving){break;}
            }
            Require(frames<1000&&crossings>=1&&subtile>0&&new Point2D((int)(motion.X>>16),(int)(motion.Y>>16))==target,"Motion did not reach its declared route/cross room/show subtiles");
            string before=JsonSerializer.Serialize(DeathMarkWorld.Capture());var point=b.Location;bool refused=false;
            try{GoatmanNativeWalking.Advance(b,motion with{X=motion.X-1});}catch(NotSupportedException){refused=true;}
            Require(refused&&b.Location==point&&JsonSerializer.Serialize(DeathMarkWorld.Capture())==before,"Mismatched walking state partially changed native world");
            uint flags=path.Flags;path.Flags&=~1u;refused=false;
            try{GoatmanNativeWalking.Advance(b,motion);}catch(NotSupportedException){refused=true;}finally{path.Flags=flags;}
            Require(refused&&b.Location==point,"Inactive path was silently given constructor flags");
            // Actual native occupancy must block a proposed source step.
            var blocker=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};
            blocker.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);blocker.AIObject?.AITimer.Stop();
            try
            {
                Require(PetGroundWorld.Collision(b,(b.X+1)*2,b.Y*2)!=0,"Native occupancy control was not blocking");
                before=JsonSerializer.Serialize(DeathMarkWorld.Capture());refused=false;
                try{DeathMarkWorld.WalkPrepared(b,((uint)((b.X+1)*2)<<16)|32768,path.Y);}catch(NotSupportedException){refused=true;}
                Require(refused&&b.Location==point&&JsonSerializer.Serialize(DeathMarkWorld.Capture())==before,"Blocked walking destination mutated room graph/position");
            }
            finally{blocker.Delete();}
            PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,b.SavedText,DeathMarkWorld.State(b),GenericPetSavedPath.Capture(path),
                life,b.MaximumLife256,seed)));kept=true;
            Log.Information("Goatman walking:{Frames} source updates/{Crossings} room crossings/{Subtiles} fractional-cell geometry samples;actual native movement/links/occupancy/wounds/RNG;planning/birth/clock remain supplied",frames,crossings,subtile);
        }
        finally{if(!kept){b?.Delete();p.Delete();}}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)s.Body)as WarlockGoatmanBody;
        Require(b!=null&&b.ControlMaster?.Serial.Value==s.Owner&&b.Map==Map.Internal&&b.SavedText==s.Birth&&DeathMarkWorld.State(b)==s.Controller
            &&GenericPetSavedPath.Capture(DeathMarkWorld.Path(b))==s.Path&&SkillHealth.Balance256(b)==s.Life&&b.MaximumLife256==s.Maximum&&NpcEquipmentRandom.State(b)==s.Random,
            "Original walking source path/room/controller/wounds/RNG changed on reload");
        Require(!ReviveMeleeGeometry.Unit(b).HasRoom,"Parked actor retained a live combat room");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
