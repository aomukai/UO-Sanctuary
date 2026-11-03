// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PetNativePlacementChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetNativePlacementChecks));
    private sealed record Original(uint Owner,uint Body,uint Helm,bool Female,string State,int Life,int Maximum,int Mana,long Order);
    private sealed record Retained(uint Owner,uint Body,int Life,int Mana,long Order,int Generation,Point3D LastLanding);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-native-placement-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("pet-native-placement-check",false);Register("pet-native-placement-reload-check",true);
    }
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Actual prepared pet terrain/room placement and retained actors",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static int NativeOccurrences(Mobile body)
    {int count=0;foreach(var other in body.Map.GetMobilesInRange<Mobile>(body.Location,0)){if(other==body){count++;}}return count;}
    private static void Refuse(Action run)
    {try{run();throw new InvalidOperationException("Unsupported native placement was admitted");}catch(NotSupportedException){}}
    private static (GenericPetCommitPath Path,GenericPetMonsterPlacementUnit Unit) Inputs(WarlockGoatmanBody body)
    {
        int id=GenericPetSearchTables.SourceId("wargoatman").Value;
        var path=new GenericPetCommitPath{X=((uint)(body.X*2)<<16)|0x8000,Y=((uint)(body.Y*2)<<16)|0x8000,
            Room=GenericPetNativePlacement.RoomHandle(body.Map,body.X*2,body.Y*2),Flags=0x20,Word30=11,Word34=37,
            Raw96=0x7788,Size5c=1,Mask60=0x100,Mask64=0x1c09,Actor=new(checked((int)body.Serial.Value),1)};
        return(path,new(new(id,0),true,1,id,2,2,37));
    }
    private static Retained Scene(Original original,int generation)
    {
        var owner=World.FindMobile((Serial)original.Owner) as PlayerMobile;var body=World.FindMobile((Serial)original.Body) as WarlockGoatmanBody;
        Require(owner!=null&&body!=null&&body.ControlMaster==owner&&body.SavedText==original.State&&SkillHealth.Balance256(body)==original.Life
            &&NecromancerEffects.ManaBalance256(owner)==original.Mana&&BattleCommand.PetOrder(body)==original.Order,"Original saved placement actor changed");
        var walls=new List<Item>();Point3D last=default,ownerPoint=owner.Location;var ownerMap=owner.Map;
        try
        {
            FixtureSpace.Corridor(owner,6200,26,original.Female?3600:3500);
            var start=new Point3D(owner.X+1,owner.Y,owner.Z);Require(PetGroundWorld.ZeroFootprint(owner.Map,start),"Prepared placement start floor");
            body.MoveToWorld(start,owner.Map);body.AIObject?.AITimer.Stop();
            // A search at the current point must remove its own source
            // occupancy; otherwise pattern2 would push this actor elsewhere.
            var (path,unit)=Inputs(body);var same=new GenericPetNativePlacement(body,unit,path,new(body.X*2,body.Y*2));
            Require(same.Place()==1&&body.Location==start&&path.Word30==0&&path.Word34==0&&path.Raw96==0
                &&(path.Flags&0x22)==0&&unit.PathWord34==0&&unit.Pet.Flags==0x10000,"Native self-footprint/path reset");
            var stayed=body.Location;Refuse(()=>same.Place());Require(body.Location==stayed,"Reused snapshot moved an actor");

            int wallX=owner.X+4;
            for(int dy=-2;dy<=2;dy++){var wall=new Static(6);wall.MoveToWorld(new(wallX,owner.Y+dy,owner.Z),owner.Map);walls.Add(wall);}
            Require(!PetGroundWorld.ZeroFootprint(owner.Map,new(wallX,owner.Y,owner.Z)),"Wall fixture did not block native terrain");
            (path,unit)=Inputs(body);var blocked=new GenericPetNativePlacement(body,unit,path,new(wallX*2,owner.Y*2));
            Require(blocked.Place()==1&&body.X!=wallX&&PetGroundWorld.ZeroFootprint(owner.Map,body.Location)
                &&NativeOccurrences(body)==1,"Native wall search committed an invalid or unregistered body");
            Require(((ushort)(path.X>>16)+1)/2==body.X&&((ushort)(path.Y>>16)+1)/2==body.Y,"Source/native coordinates diverged");

            // The explicit skip-search branch still requires a representable
            // native floor. Refusal happens before path/world commitment.
            var before=body.Location;(path,unit)=Inputs(body);uint oldX=path.X,oldY=path.Y;
            var refused=new GenericPetNativePlacement(body,unit,path,new(wallX*2,owner.Y*2));
            Refuse(()=>refused.Place(true));Require(body.Location==before&&path.X==oldX&&path.Y==oldY&&path.Word34==37,"Unrepresentable native floor partially committed");

            // Cross a logical room boundary on real native floor. Source room
            // and update lists must unlink the old occurrence,then link once.
            (path,unit)=Inputs(body);int oldRoom=path.Room;var target=new Point2D((owner.X+20)*2,owner.Y*2);
            var moved=new GenericPetNativePlacement(body,unit,path,target);
            Require(moved.Place()==1&&body.X==owner.X+20&&body.Y==owner.Y&&path.Room!=oldRoom
                &&path.PreviousRoom==oldRoom&&moved.LinkedRoom(oldRoom).Head==0&&moved.LinkedRoom(oldRoom).FriendlyCount==0
                &&moved.LinkedRoom(oldRoom).Update.Head==0&&moved.LinkedRoom(path.Room).Head==path.Actor.Handle
                &&moved.LinkedRoom(path.Room).FriendlyCount==1&&moved.LinkedRoom(path.Room).Update.Head==path.Actor.Handle
                &&moved.LinkedUnit.Next160==0&&(moved.LinkedUnit.Update.Flags124&0x2000)!=0&&(path.Flags&0x22)==0,
                "Native cross-room commitment lost room/update membership or movement reset");
            Require(NativeOccurrences(body)==1,"Actual native sector membership duplicated/lost original body");
            last=body.Location;
            // The reset getter is primary monstats, not secondary dimensions.
            // Ordinary, interactive and absent rows all retain an actual
            // monster's centered reset. Flag4 bypasses the getter entirely.
            var targetView=GenericPetPathTargets.Bind(1);
            foreach(var resetTarget in new[]{new GenericPetCommitTarget(1,GenericPetSearchTables.SourceId("wargoatman").Value,1,0),
                new GenericPetCommitTarget(1,GenericPetSearchTables.SourceId("akara").Value,1,0),
                new GenericPetCommitTarget(1,-1,1,0),new GenericPetCommitTarget(1,148,0,4)})
            {
                (path,unit)=Inputs(body);path.Target=resetTarget;
                var targeted=new GenericPetNativePlacement(body,unit,path,new(body.X*2,body.Y*2),resetTarget.Flags124==4?null:targetView);
                Require(targeted.Place()==1&&body.Location==last&&path.Target==resetTarget&&path.Raw96==0&&path.Word30==0&&path.Word34==0
                    &&(path.Flags&0x22)==0&&NativeOccurrences(body)==1,"Native monster-target reset lost position,identity or path state");
            }
            (path,unit)=Inputs(body);path.Target=new(1,148,1,0);uint unchangedX=path.X;
            Refuse(()=>new GenericPetNativePlacement(body,unit,path,new(body.X*2,body.Y*2)));
            Require(body.Location==last&&path.X==unchangedX&&path.Word34==37,"Missing target view partially changed native state");
            path.Target.Variant=2;Refuse(()=>new GenericPetNativePlacement(body,unit,path,new(body.X*2,body.Y*2),targetView));
            Require(body.Location==last&&path.X==unchangedX&&path.Word34==37,"Mismatched target view partially changed native state");
            (path,unit)=Inputs(body);path.Mask60=1;Refuse(()=>new GenericPetNativePlacement(body,unit,path,target));
            Require(body.SavedText==original.State&&SkillHealth.Balance256(body)==original.Life&&SkillHealth.Maximum256(body)==original.Maximum
                &&body.ControlMaster==owner&&World.FindItem((Serial)original.Helm)?.Parent==owner&&NecromancerEffects.ManaBalance256(owner)==original.Mana
                &&BattleCommand.PetOrder(body)==original.Order&&!ClassCombat.Available(owner,CombatAbility.SummonGoatman),"Placement changed birth,wounds,owner,gear,mana,order or learned gate");
            return new(owner.Serial.Value,body.Serial.Value,original.Life,original.Mana,original.Order,generation,last);
        }
        finally{foreach(var item in walls){item.Delete();}body.Internalize();owner.MoveToWorld(ownerPoint,ownerMap);body.AIObject?.AITimer.Stop();}
    }
    private static void Check(bool reload)
    {
        var originals=JsonSerializer.Deserialize<Original[]>(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-body-fixtures.json")));
        int generation=0;
        if(reload)
        {
            var prior=JsonSerializer.Deserialize<Retained[]>(File.ReadAllText(Manifest));generation=prior[0].Generation+1;
            Require(prior.Length==2&&prior.All(x=>originals.Any(y=>x.Body==y.Body&&x.Owner==y.Owner&&x.Life==y.Life&&x.Mana==y.Mana&&x.Order==y.Order)),"Native placement original identity did not survive reload");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(originals.Select(x=>Scene(x,generation)).ToArray()));
        Log.Information("Native pet placement:generation{Generation},two original actors;self-footprint,real wall,blocked skip floor,cross-room/native-sector membership and reuse guards pass;eight monster-target resets and four missing/mismatched-view refusals retain birth/wounds/owner/gear/mana/order;automatic AI/current wire transport remain unfinished",generation);
    }
}
