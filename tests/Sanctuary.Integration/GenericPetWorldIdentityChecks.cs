// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class GenericPetWorldIdentityChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetWorldIdentityChecks));
    private sealed record Saved(uint Owner,uint Pet,uint Item,string Gear,IronGolemItemBinding Binding);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"generic-pet-world-identity-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Refuses(Action action,string why)
    {bool refused=false;try{action();}catch(NotSupportedException){refused=true;}Require(refused,why);}
    private static int Id(IEntity entity)=>checked((int)entity.Serial.Value);
    public static void Configure()
    {
        Register("generic-pet-world-identity-check",Check);Register("generic-pet-world-identity-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));if(World.FindMobile((Serial)s.Pet) is BaseCreature p){p.AIObject?.AITimer.Stop();}}};
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Native registry/ownership and retained source refresh",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Exercise(PlayerMobile owner,SkeletonGuardian pet,Item item)
    {
        int player=Id(owner),actor=Id(pet),gear=Id(item);var identity=new GenericPetWorldIdentity(71);int queries=0;
        foreach(int game in new[]{71,193})
        {
            var registry=new GenericPetWorldIdentity(game);
            foreach(var pair in new[]{(player,0),(actor,1),(gear,4)})foreach(int kind in new[]{0,1,4})
            {Require(registry.Resolve(game,kind,pair.Item1)==(kind==pair.Item2?pair.Item1:0),"Native role-qualified identity collision");queries++;}
            foreach(int kind in new[]{0,1,4})foreach(int missing in new[]{-1,0,int.MaxValue})
            {Require(registry.Resolve(game,kind,missing)==0,"Absent identity resolved");queries++;}
        }
        Require(queries==36,"Incomplete live lookup matrix");
        foreach(int kind in new[]{2,3,5,6}){Refuses(()=>identity.Resolve(71,kind,actor),"Unsupported source registry invented");}
        Refuses(()=>identity.Resolve(193,1,actor),"Unbound world context accepted");
        bool zero=false;try{new GenericPetWorldIdentity(0);}catch(ArgumentOutOfRangeException){zero=true;}Require(zero,"Null game context accepted");
        var unknown=new Item(1);try{Refuses(()=>identity.Resolve(71,4,Id(unknown)),"Unmapped native item silently classified as source equipment");}finally{unknown.Delete();}
        Require(identity.PlayerOwner(pet)==new GenericPetOwnerControl(71,0,player,0),"Actual control owner not projected");
        var path=new GenericPetPathTarget(player,0,player);
        var units=new Dictionary<int,GenericPetTraversalUnit>
        {
            [player]=new(0,1,player,4,0,null,true,null),
            [actor]=new(1,1,actor,4,0,path,true,new(99,0,-1,123)),
            [gear]=new(4,1,gear,0,0,null,false,null)
        };
        var traversal=identity.Bind(new GenericPetTraversalServices(id=>units[id],(_,_,_)=>throw new InvalidOperationException("Old registry used"),
            _=>0,_=>throw new NotSupportedException(),_=>0,_=>throw new NotSupportedException(),_=>throw new NotSupportedException(),_=>throw new NotSupportedException()));
        Require(traversal.Unit(actor).Ai==new GenericPetOwnerControl(71,0,player,123)&&ReferenceEquals(traversal.Unit(actor).Path,path),"Ownership binding lost path/owned-list metadata");
        GenericPetTraversal.Refresh(71,actor,traversal);Require(path.Target==player,"Live path identity was cleared");
        path.Target=actor;GenericPetTraversal.Refresh(71,actor,traversal);Require(path.Target==0,"Mismatched cached identity survived actual registry refresh");
        var equipped=new GenericPetPathTarget(gear,4,gear);units[actor]=units[actor] with{Path=equipped};
        Require(item.Parent==pet&&IronGolemItems.State(pet)?.Item==item.Serial.Value,"Source equipped-item fixture lost original binding");
        GenericPetTraversal.Refresh(71,actor,traversal);Require(equipped.Target==0,"Equipped original item survived source refresh's inventory gate");
        GenericPetSearchWorldUnit WorldUnit(int kind)=>new(new(kind,true,0,0),0,1,3,0,0,true,0,new(99,0,-1,123),0,0,null,new(-1,-1,0,0));
        var worldUnits=new Dictionary<int,GenericPetSearchWorldUnit>{[player]=WorldUnit(0),[actor]=WorldUnit(1)};
        var world=identity.Bind(new GenericPetSearchWorldServices(id=>worldUnits[id],(_,_)=>null,(_,_)=>null,_=>232,
            (_,_,_)=>throw new InvalidOperationException("Old world registry used"),(_,_)=>null,(_,_)=>0,(_,_)=>null,(_,_)=>0,_=>0,_=>0,(_,_,_,_,_,_,_,_)=>0,(_,_)=>0,(_,_,_,_)=>0));
        Require(world.Unit(actor).Owner==traversal.Unit(actor).Ai&&ReferenceEquals(world.Unit(actor).Cache,worldUnits[actor].Cache),"Search/traversal disagree on actual ownership or lost cache");
        Require(ReferenceEquals(world.Unit(player),worldUnits[player])&&world.Resolve(71,1,actor)==actor,"Identity binding rewrote player metadata or missed actor");
        var other=new PlayerMobile{Name="WorldIdentityTemporaryOwner",Player=true};
        try
        {
            Require(pet.SetControlMaster(other),"Could not change live fixture owner");
            Require(world.Unit(actor).Owner.Id==Id(other)&&traversal.Unit(actor).Ai.Id==Id(other),"Binding reused a stale owner snapshot");
            pet.SummonMaster=owner;
            Require(pet.ControlMaster==owner&&pet.SummonMaster==owner&&pet.Master==owner&&pet.GetMaster()==owner&&identity.PlayerOwner(pet)?.Id==player,"Native shared-master aliases diverged");
            pet.SummonMaster=null;
            Require(pet.SetControlMaster(null),"Could not clear fixture owner");
            Require(identity.PlayerOwner(pet)==null,"Unowned actor retained prior owner");
            Refuses(()=>traversal.Unit(actor),"Unowned nonempty source child list silently discarded");
            units[actor]=units[actor] with{Ai=units[actor].Ai with{First=0}};
            Require(traversal.Unit(actor).Ai==null,"Unowned empty list retained stale owner");
            pet.SummonMaster=owner;Require(identity.PlayerOwner(pet)?.Id==player,"Summon-only owner did not resolve");pet.SummonMaster=null;
            var npc=new Zombie();npc.AIObject?.AITimer.Stop();
            try{pet.SummonMaster=npc;Refuses(()=>identity.PlayerOwner(pet),"NPC ownership invented a child-list binding");}
            finally{pet.SummonMaster=null;npc.Delete();}
        }
        finally{pet.SummonMaster=null;Require(pet.SetControlMaster(owner),"Fixture owner not restored");other.Delete();}
        var vanished=new PlayerMobile{Name="WorldIdentityDeleted",Player=true};int deleted=Id(vanished);vanished.Delete();
        Require(identity.Resolve(71,0,deleted)==0,"Deleted native player still resolved");
        units[actor]=units[actor] with{Path=new(deleted,0,deleted)};GenericPetTraversal.Refresh(71,actor,traversal);
        Require(units[actor].Path.Target==0,"Deleted actual target survived path refresh");
        owner.Internalize();
        Require(identity.Resolve(71,0,player)==player&&identity.Resolve(71,1,actor)==actor&&identity.Resolve(71,4,gear)==gear,"World lookup confused map membership with identity");
        // Keep this native persistence fixture parked between boots.
        pet.Internalize();Require(owner.Map==Map.Internal&&item.Parent==pet,"Identity query moved an entity or detached its item");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!NecromancerCombat.Implemented(CombatAbility.IronGolem),"Identity adapter enabled incomplete actors");
        Log.Information("GenericPet world identity:36live role/context lookups,actual player/control/summon ownership,shared search/traversal binding,stale/deleted/equipped target refresh and explicit unsupported registries pass;native geometry,corpse identities,stat lists,ordering and complete actors remain gated");
    }
    private static void Check()
    {
        var owner=new PlayerMobile{Name="WorldIdentityOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,30);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Persistent holder training failed");FixtureSpace.Place(owner,new(6100,1630,0),Map.Trammel);
        var pet=new SkeletonGuardian{ControlSlots=0,Frozen=true,CantWalk=true,Name="World identity storage [fixture]"};pet.Initialize(owner,1,0);
        Require(pet.SetControlMaster(owner),"Persistent holder owner refused");PersistentMinions.Profiles.Track(pet);pet.MoveToWorld(owner.Location,owner.Map);pet.AIObject?.AITimer.Stop();
        var item=new Broadsword();ClassGear.Profiles.Convert(item,30,GearRarity.Rare,new Random(719));ClassGear.Profiles.Store(item,ClassGear.State(item) with{Identified=true});item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,pet,item,out var why),"Original equipment attachment failed:"+why);
        string gear=JsonSerializer.Serialize(ClassGear.State(item));var binding=IronGolemItems.State(pet);
        Exercise(owner,pet,item);Require(gear==JsonSerializer.Serialize(ClassGear.State(item))&&binding==IronGolemItems.State(pet),"World binding altered original equipment");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,pet.Serial.Value,item.Serial.Value,gear,binding)));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)s.Owner) as PlayerMobile;var pet=World.FindMobile((Serial)s.Pet) as SkeletonGuardian;var item=World.FindItem((Serial)s.Item);
        Require(owner is{Deleted:false}&&pet is{Alive:true,Deleted:false}&&item is{Deleted:false}&&pet.ControlMaster==owner&&item.Parent==pet,"Reload lost native identity/ownership/original equipment");
        Require(JsonSerializer.Serialize(ClassGear.State(item))==s.Gear&&IronGolemItems.State(pet)==s.Binding,"Reload changed original gear rolls or binding");
        pet.AIObject?.AITimer.Stop();Exercise(owner,pet,item);
    }
}
