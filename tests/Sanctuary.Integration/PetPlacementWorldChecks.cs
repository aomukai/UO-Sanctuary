// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PetPlacementWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetPlacementWorldChecks));
    private sealed record Original(uint Owner,uint Body,uint Helm,bool Female,string State,int Life,int Maximum,int Mana,long Order);
    private sealed record Retained(uint Store,int Generation,string State,uint[] Bodies,Point3D[] Positions);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-placement-world-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Refuse(Action run)
    {try{run();throw new InvalidOperationException("Unsupported placement world accepted");}catch(Exception e)when(e is NotSupportedException or InvalidDataException){}}
    public static void Configure()
    {Register("pet-placement-world-check",false);Register("pet-placement-world-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Shared saved pet room/path world and actual actors",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static (GenericPetCommitPath,GenericPetMonsterPlacementUnit) Inputs(WarlockGoatmanBody body)
    {
        int id=GenericPetSearchTables.SourceId("wargoatman").Value;
        var path=new GenericPetCommitPath{X=((uint)(body.X*2)<<16)|0x4321,Y=((uint)(body.Y*2)<<16)|0x1234,
            ClientX=777,ClientY=-888,Room=GenericPetNativePlacement.RoomHandle(body.Map,body.X*2,body.Y*2),PreviousRoom=41,
            Flags=0x20,Word30=51,Word34=52,Raw96=0x1122334455667788,Size5c=1,Pattern58=3,Mask60=0x100,Mask64=0x1c09,
            Word200=69,Point204=0xfedcba98,Collision68=0x9876,Actor=new(checked((int)body.Serial.Value),1),Target=new(1,148,1,0)};
        return(path,new(new(id,0),true,1,id,2,2,97));
    }
    private static void Check(bool reload)
    {
        var originals=JsonSerializer.Deserialize<Original[]>(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-body-fixtures.json")));
        var owners=originals.Select(x=>World.FindMobile((Serial)x.Owner) as PlayerMobile).ToArray();
        var bodies=originals.Select(x=>World.FindMobile((Serial)x.Body) as WarlockGoatmanBody).ToArray();
        Require(originals.Length==2&&owners.All(x=>x!=null)&&bodies.All(x=>x!=null),"Original shared placement cohort missing");
        var ownerPoints=owners.Select(x=>x.Location).ToArray();var ownerMaps=owners.Select(x=>x.Map).ToArray();
        GenericPetPlacementStore store=null;Retained prior=null;int generation=0;
        if(reload)
        {
            prior=JsonSerializer.Deserialize<Retained>(File.ReadAllText(Manifest));generation=prior.Generation+1;
            store=World.FindItem((Serial)prior.Store) as GenericPetPlacementStore;
            Require(store!=null&&store.Map==Map.Internal&&store.SavedText==prior.State&&JsonSerializer.Serialize(store.State.Capture())==prior.State
                &&prior.Bodies.SequenceEqual(originals.Select(x=>x.Body)),"Original store/path/room graph changed during actual world load");
        }
        try
        {
            // Explicit fixture activation. No automatic AI/parking path is
            // inferred from staging the same saved actors on their held clocks.
            if(!reload)
            {
                FixtureSpace.Corridor(owners[0],6200,26,3500);owners[1].MoveToWorld(new(owners[0].X,owners[0].Y+1,owners[0].Z),owners[0].Map);
                store=GenericPetPlacementStore.Create(owners[0].Map);
                for(int i=0;i<2;i++)
                {
                    bodies[i].MoveToWorld(new(owners[0].X+2+2*i,owners[0].Y,owners[0].Z),owners[0].Map);bodies[i].AIObject?.AITimer.Stop();
                    var(path,unit)=Inputs(bodies[i]);store.Register(bodies[i],unit,path);
                }
                int first=checked((int)bodies[0].Serial.Value),second=checked((int)bodies[1].Serial.Value),room=store.State.Member(first).Path.Room;
                Require(store.State.Member(second).Path.Room==room&&store.State.Room(room).Head==second&&store.State.Member(second).Next160==first
                    &&store.State.Room(room).Update.Head==second&&store.State.Member(second).Update.Next150==first&&store.State.Room(room).FriendlyCount==2,
                    "Two real pets did not share source room/update lists in registration order");
            }
            else
            {
                var map=store.State.Map;
                for(int i=0;i<2;i++)
                {owners[i].MoveToWorld(new(prior.Positions[i].X-2,prior.Positions[i].Y+1,prior.Positions[i].Z),map);bodies[i].MoveToWorld(prior.Positions[i],map);bodies[i].AIObject?.AITimer.Stop();}
            }
            var world=store.State;var view=GenericPetPathTargets.Bind(1);int[] handles=bodies.Select(x=>checked((int)x.Serial.Value)).ToArray();
            string before=store.SavedText;Refuse(()=>store.Register(bodies[0],world.Unit(handles[0]),world.Member(handles[0]).Path));
            Require(store.SavedText==before,"Duplicate registration changed persistent state");
            var ownPoint=bodies[0].Location;
            var nativeOrder=bodies[0].ControlOrder;var nativeTarget=bodies[0].ControlTarget;
            try
            {
                Require(bodies[0].SetControlMaster(owners[1]),"Foreign-owner fixture setup refused");
                Refuse(()=>store.Place(handles[0],new(bodies[0].X*2,bodies[0].Y*2),view));
                Refuse(()=>new GenericPetPlacementWorld(world.Map).Register(bodies[0],world.Unit(handles[0]),world.Member(handles[0]).Path));
                Require(store.SavedText==before&&bodies[0].Location==ownPoint,"Foreign control master changed saved placement state");
            }
            finally
            {
                Require(bodies[0].SetControlMaster(owners[0]),"Original owner restore refused");
                bodies[0].ControlOrder=nativeOrder;bodies[0].ControlTarget=nativeTarget;bodies[0].AIObject?.AITimer.Stop();
            }
            // Preserve exact paths and list heads through JSON without replaying
            // Add, which would reverse list order and mutate dirty markers.
            var image=world.Capture();var copied=GenericPetPlacementWorld.Restore(JsonSerializer.Deserialize<GenericPetPlacementImage>(JsonSerializer.Serialize(image)));
            Require(JsonSerializer.Serialize(copied.Capture())==JsonSerializer.Serialize(image),"Saved graph reconstruction changed raw paths,list order or cache fields");
            Refuse(()=>GenericPetPlacementWorld.Restore(image with{Width=image.Width+1}));
            Refuse(()=>GenericPetPlacementWorld.Restore(image with{Members=[image.Members[0],image.Members[0]]}));
            var corrupt=(GenericPetSavedMember[])image.Members.Clone();corrupt[0]=corrupt[0] with{Next160=corrupt[0].Handle};
            Refuse(()=>GenericPetPlacementWorld.Restore(image with{Members=corrupt}));
            corrupt=(GenericPetSavedMember[])image.Members.Clone();corrupt[0]=corrupt[0] with{Next150=corrupt[0].Handle};
            Refuse(()=>GenericPetPlacementWorld.Restore(image with{Members=corrupt}));
            // Repeated real displacement reuses the original path objects and
            // shared room nodes. Both actors cross room boundaries on each boot.
            for(int i=0;i<2;i++)
            {
                var body=bodies[i];var path=world.Member(handles[i]).Path;int oldRoom=path.Room,oldHead=world.Room(oldRoom).Head;
                world.Member(handles[i]).ByteB9=23;
                if(i==0){world.Unit(handles[i]).Pet.Flags|=0x4000000;world.Member(handles[i]).Flags128=world.Unit(handles[i]).Pet.Flags;}
                int targetX=body.X+(generation%2==0?20:-20);
                Require(store.Place(handles[i],new(targetX*2,body.Y*2),view)==1&&ReferenceEquals(world.Member(handles[i]).Path,path)
                    &&body.X==targetX&&path.Room!=oldRoom&&path.PreviousRoom==oldRoom&&path.Raw96==0&&path.Word30==0&&path.Word34==0
                    &&path.Pattern58==3&&path.Word200==69&&path.Point204==0xfedcba98&&path.Collision68==0x9876
                    &&world.Member(handles[i]).Flags128==world.Unit(handles[i]).Pet.Flags&&world.Member(handles[i]).ByteB9==(i==0?23:0),
                    "Shared world lost actual movement,original path fields or room-removal flag behavior");
                Require(world.Room(oldRoom).Head!=handles[i]&&oldHead!=0,"Old source room retained moved actor");
            }
            var stale=new GenericPetNativePlacement(bodies[0],world.Unit(handles[0]),world.Member(handles[0]).Path,new(bodies[0].X*2,bodies[0].Y*2),view,world);
            Require(store.Place(handles[1],new(bodies[1].X*2,bodies[1].Y*2),view)==1,"Second persistent placement failed");
            before=store.SavedText;var native=bodies[0].Location;Refuse(()=>stale.Place());store.Capture();
            Require(store.SavedText==before&&bodies[0].Location==native,"Stale world context changed source/native state");
            copied=GenericPetPlacementWorld.Restore(world.Capture());copied.Remove(handles[0]);
            Require(copied.Count==1&&copied.Member(handles[1]).Next160==0&&copied.Member(handles[1]).Update.Next150==0,"Shared unlink lost remaining actor");
            copied.Remove(handles[1]);Require(copied.Count==0&&copied.Capture().Rooms.All(x=>x.Head==0&&x.UpdateHead==0&&x.FriendlyCount==0),"Final source unlink retained stale membership");
            for(int i=0;i<2;i++)
            {
                var original=originals[i];var body=bodies[i];
                Require(body.SavedText==original.State&&SkillHealth.Balance256(body)==original.Life&&SkillHealth.Maximum256(body)==original.Maximum
                    &&body.ControlMaster==owners[i]&&World.FindItem((Serial)original.Helm)?.Parent==owners[i]
                    &&NecromancerEffects.ManaBalance256(owners[i])==original.Mana&&BattleCommand.PetOrder(body)==original.Order,
                    "Persistent placement changed original pet/owner resources,gear or birth snapshot");
            }
            store.Capture();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Retained(store.Serial.Value,generation,store.SavedText,
                originals.Select(x=>x.Body).ToArray(),bodies.Select(x=>x.Location).ToArray())));
            Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Saved placement world enabled incomplete learned skill");
            Log.Information("Pet placement world:generation{Generation},same saved registry/two original pets;ordered shared room/update graph,full path/cache snapshots,two real cross-room moves,repeated placement,stale/duplicate/foreign-owner/corrupt guards and removal pass;birth/wounds/gear/mana/order retain;automatic AI/parking/target acquisition remain unfinished",generation);
        }
        finally
        {
            for(int i=0;i<2;i++){bodies[i].Internalize();bodies[i].AIObject?.AITimer.Stop();owners[i].MoveToWorld(ownerPoints[i],ownerMaps[i]);}
        }
    }
}
