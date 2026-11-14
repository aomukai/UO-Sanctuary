// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DefilerFlyingWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DefilerFlyingWorldChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,string Birth,DeathMarkSavedActor Source,GenericPetSavedPath Path,int Life,NpcEquipmentSeed Random);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"defiler-flying-world-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"defiler-flying-world-reload-check":"defiler-flying-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Review candidate flying terrain/source transport/follow/warp",_=>Core.LoopContext.Post(()=>
            {try{Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>{if(reload){Reload(n);}Scene(n,!reload,name+"-result");});}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Scene(NetState network,bool retain,string result)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.summondefiler",20);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);int floor=FixtureSpace.Corridor(p,6560,48,3500);
        // A real raised platform gives the gap an unambiguous absence of a
        // landing surface,regardless of this installation's underlying land.
        for(int x=p.X-4;x<=p.X+48;x++)for(int dy=-3;dy<=3;dy++){new Static(floor).MoveToWorld(new(x,p.Y+dy,20),p.Map);}
        p.MoveToWorld(new(p.X,p.Y,20),p.Map);
        var account=FixtureNetwork.Authenticate(network,p,"defiler-flight-");WarlockDefilerBody b=null;bool kept=false;var items=new List<Item>();
        try
        {
            DateTime at=TimedEffects.Now;b=DefilerFlyingWorld.CreateCandidate(p,new(p.X+1,p.Y,p.Z),at.AddDays(2));NpcEquipmentRandom.Set(b,SourceRandom.InitLow(744));
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);int life=SkillHealth.Balance256(b);var seed=NpcEquipmentRandom.State(b);string birth=b.SavedText;
            var path=DeathMarkWorld.Path(b);Require(DefilerFlyingWorld.Profile(b,path)&&DemonPathWorld.Size(b)==2&&DemonPathWorld.Mask(b)==0x1804,"Explicit flying candidate profile missing");
            bool refused=false;try{_=DemonGroundMovement.Size(b);}catch(NotSupportedException){refused=true;}Require(refused,"Flying candidate entered ground-only supplier");
            int gapX=b.X+6;var gap=new Point2D(gapX*2,b.Y*2);
            // Remove only this fixture's artificial floor:the native void is
            // real map data,not a collision delegate returning a canned zero.
            var floors=new List<Item>();
            foreach(var item in b.Map.GetItemsInRange<Item>(new Point3D(gapX,b.Y,b.Z),0))
            {if(item is Static&&item.Z==b.Z&&item.ItemData.Surface&&!item.ItemData.Impassable&&item.ItemData.CalcHeight==0){floors.Add(item);}}
            foreach(var item in floors){item.Delete();}
            bool ground=PetGroundWorld.Point(b,gap,out var groundPoint),flying=DefilerFlyingWorld.Point(b,gap,out var overGap);
            var land=b.Map.Tiles.GetLandTile(gapX,b.Y);
            Require(!ground&&flying,$"Gap mismatch:ground={ground}/{groundPoint},flying={flying}/{overGap},body={b.Location},floors={floors.Count},land={land.ID}/ignored{land.Ignored}/flags{TileData.LandTable[land.ID&TileData.MaxLandValue].Flags}/avg{b.Map.GetAverageZ(gapX,b.Y)},raw={DefilerFlyingWorld.RawCollision(b,gap.X,gap.Y)},footprint={DefilerFlyingWorld.Collision(b,gap.X,gap.Y)}");
            int wet=Enumerable.Range(1,TileData.MaxItemValue).First(i=>TileData.ItemTable[i].Wet);
            var water=new Static(wet);items.Add(water);water.MoveToWorld(new(gapX,b.Y,b.Z),b.Map);
            Require(DefilerFlyingWorld.Point(b,gap,out _)&&DefilerFlyingWorld.RawCollision(b,gap.X,gap.Y)==0,"Native wet tile incorrectly grounded the flyer");water.Delete();
            int wall=Enumerable.Range(1,TileData.MaxItemValue).First(i=>(TileData.ItemTable[i].Flags&TileFlag.Wall)!=0&&TileData.ItemTable[i].CalcHeight>=16);
            var barrier=new Static(wall);items.Add(barrier);barrier.MoveToWorld(new(gapX,b.Y,b.Z),b.Map);
            Require((DefilerFlyingWorld.Collision(b,gap.X,gap.Y)&4)!=0&&DemonPathWorld.PlacementCollision(b,gap.X,gap.Y)!=0,"Flight barrier or distinct placement mask ignored an actual wall");
            barrier.MoveToWorld(new(gapX+1,b.Y,b.Z),b.Map);
            Require(DefilerFlyingWorld.RawCollision(b,gap.X,gap.Y)==0&&(DefilerFlyingWorld.Collision(b,gap.X,gap.Y)&4)!=0,"Flying size2 footprint ignored an adjacent wall");
            barrier.MoveToWorld(new(gapX,b.Y,b.Z+20),b.Map);Require(DefilerFlyingWorld.RawCollision(b,gap.X,gap.Y)==0,"Nonintersecting upper geometry blocked the flyer");
            barrier.MoveToWorld(new(gapX,b.Y,b.Z+10),b.Map);Require((DefilerFlyingWorld.RawCollision(b,gap.X,gap.Y)&4)!=0,"Intersecting overhead geometry was ignored");barrier.Delete();
            var door=new MetalDoor(DoorFacing.NorthCW);items.Add(door);door.MoveToWorld(new(gapX,b.Y,b.Z),b.Map);
            Require((DefilerFlyingWorld.Collision(b,gap.X,gap.Y)&0x800)!=0,"Closed native door did not block flight");door.Delete();
            var solid=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};solid.MoveToWorld(new(gapX,b.Y,b.Z),b.Map);solid.AIObject?.AITimer.Stop();
            try{Require((DefilerFlyingWorld.Collision(b,gap.X,gap.Y)&0x1000)!=0,"Native hostile occupancy ignored");}finally{solid.Delete();}
            int frame=0,idles=0;var events=new GenericPetTimedEvents();var motion=new GoatmanNativeMotion(b,p,events,()=>frame,()=>at.AddMilliseconds(frame*40),
                ()=>new(frame,DeathMarkWorld.State(b).Mode,1,GenericPetMetadata.Table.StateCount,true,-1,0,4),_=>idles++,DefilerFlyingMovement.Species.Flags241)
                {Random=new(seed.Seed)};
            int x=(int)(path.X>>16),y=(int)(path.Y>>16),oldRoom=path.Room,rooms=0,subtiles=0;var target=new Point2D(x+40,y);
            GenericPetMovement.Prepare(motion.Unit,0,40,13);
            Require(GenericPetMovement.Execute(motion.Unit,motion.Random,2,0,target.X,target.Y,0,4,motion.Services)!=0,"Shared flight path refused");
            for(;frame<1000;frame++)
            {
                bool moving=motion.Advance();var state=motion.Motion;var geometry=ReviveMeleeGeometry.Unit(b);
                Require(path.X==state.X&&path.Y==state.Y&&geometry.Cell==motion.Cell()&&geometry.Size==2
                    &&b.X==((int)(state.X>>16)+1)/2&&b.Y==((int)(state.Y>>16)+1)/2,"Flying source/native/geometry transport diverged");
                if(path.Room!=oldRoom){rooms++;Require(path.PreviousRoom==oldRoom,"Flying room crossing lost old membership");oldRoom=path.Room;}
                if((int)(state.X>>16)!=b.X*2){subtiles++;}
                Require(SkillHealth.Balance256(b)==life&&NpcEquipmentRandom.State(b)==seed&&b.SavedText==birth,"Movement healed/recaptured or consumed attack RNG");
                if(!moving){break;}
            }
            Require(frame<1000&&rooms>0&&subtiles>0&&motion.Cell()==target&&idles>0,"Flight failed to cross actual gap/source room or finish");
            var point=b.Location;string world=JsonSerializer.Serialize(DeathMarkWorld.Capture());int size=path.Size5c;path.Size5c=3;refused=false;
            try{try{_=GoatmanNativeWalking.Advance(b,motion.Motion);}catch(NotSupportedException){refused=true;}}finally{path.Size5c=size;}
            Require(refused&&b.Location==point&&JsonSerializer.Serialize(DeathMarkWorld.Capture())==world,"Malformed flying footprint partially moved the original");
            GenericPetDecisionUnit Position(int id){var cell=ReviveMeleeGeometry.Unit(b).Cell;return new(1,true,cell.X,cell.Y);}
            Require(motion.FollowExecution(Position).Warp()!=0&&motion.Warps==1&&b.GetDistanceToSqrt(p)<=4&&DeathMarkWorld.Path(b).Mask64==0x1804
                &&(DeathMarkWorld.Flags128(b)&0x10000)!=0,"Shared owner warp lost flying profile/flag/placement");
            Require(SkillHealth.Balance256(b)==life&&NpcEquipmentRandom.State(b)==seed&&b.SavedText==birth,"Flight warp healed/recaptured or rerolled");
            if(retain)
            {
                PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=at.AddDays(2)});
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,b.SavedText,DeathMarkWorld.State(b),GenericPetSavedPath.Capture(DeathMarkWorld.Path(b)),life,seed)));kept=true;
            }
            Require(WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Learned Defiler route missing after source host integration");
            Log.Information("Defiler flight world:real gap/wet/wall/door/occupancy,source2x2/mask1804/FP16 room transport,wrong-profile refusal,shared owner warp,original wounds/RNG;candidate only");Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally{foreach(var item in items){item.Delete();}network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){b?.Delete();p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=(WarlockDefilerBody)World.FindMobile((Serial)s.Body);var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(b.Map==Map.Internal&&b.SavedText==s.Birth&&SkillHealth.Balance256(b)==s.Life&&NpcEquipmentRandom.State(b)==s.Random
            &&DeathMarkWorld.State(b)==s.Source&&GenericPetSavedPath.Capture(DeathMarkWorld.Path(b))==s.Path,"Original flying graph/header/deadline/wounds/RNG changed on reload");
        var account=FixtureNetwork.Authenticate(network,p,"defiler-flight-reload-");
        try
        {
            PersistentMinions.Resume(p);DeathMarkWorld.RebindPrepared(b,DefilerFlyingWorld.BirthPath(b));
            Require(b.Map==p.Map&&SkillHealth.Balance256(b)==s.Life&&NpcEquipmentRandom.State(b)==s.Random
                &&DefilerFlyingWorld.Profile(b,DeathMarkWorld.Path(b))&&DeathMarkWorld.State(b).Header==s.Source.Header,"Original flying profile failed roster/rebind without recovery/reset");
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Source=DeathMarkWorld.State(b),Path=GenericPetSavedPath.Capture(DeathMarkWorld.Path(b))}));
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
