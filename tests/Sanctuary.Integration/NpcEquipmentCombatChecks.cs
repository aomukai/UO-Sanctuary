// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcEquipmentCombatChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentCombatChecks));
    private sealed record Stored(uint Actor,uint Item,string Name,string Gear,int Attack,int Defense,int Flat,int Percent,int AddedDefense);
    private sealed record Saved(uint Owner,Stored[] Actors);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-combat-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-equipment-combat-check",Check);Register("npc-equipment-combat-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Actual bound equipment attack/defense consumers",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static SkeletonGuardian Pet(PlayerMobile owner)
    {
        var pet=new SkeletonGuardian{Name="Equipment combat holder [prepared]"};pet.Initialize(owner,2,0);pet.ControlSlots=0;pet.NoKillAwards=true;
        Require(pet.SetControlMaster(owner),"Prepared holder ownership failed");pet.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        PersistentMinions.Profiles.Track(pet);pet.AIObject.AITimer.Stop();return pet;
    }
    private static Item Named(string name)
    {
        var row=NamedGear.Catalog.Items.Single(x=>x.SourceKey==name);Require(NamedGear.Available(row.Id),"Actual item unavailable: "+name);
        return NamedGear.Create(row.Id,99,new Random(317),true);
    }
    private static int Attack(BaseCreature actor)=>ClassStats.AttackRatingAgainst(actor,null);
    private static void Verify(BaseCreature actor,Stored row)
    {
        int basis=CreatureProfiles.AttackRating(actor),defense=CreatureProfiles.Defense(actor);
        Require(NpcEquipmentCombat.AttackFlat(actor)==row.Flat&&NpcEquipmentCombat.AttackPercent(actor)==row.Percent
            &&NpcEquipmentCombat.Defense(actor)==row.AddedDefense,"Actual bound original contributions changed");
        int expected=basis+row.Flat;expected+=expected*row.Percent/100;
        Require(Attack(actor)==expected&&ClassStats.Defense(actor)==defense+row.AddedDefense,"NPC hit/defense getters did not consume equipment exactly once");
        var target=new SkeletonGuardian();target.Initialize((PlayerMobile)actor.ControlMaster,20,0);
        try
        {
            double chance=ClassStats.HitChance(expected,ClassStats.Defense(target),ClassStats.Level(actor),ClassStats.Level(target));
            Require(ClassStats.CheckHit(actor,target,0,()=>Math.Max(0,chance-0.000001),notifyMelee:false)==true
                &&ClassStats.CheckHit(actor,target,0,()=>Math.Min(1,chance+0.000001),notifyMelee:false)==false,"Actual attack check ignored the equipped NPC rating");
        }
        finally{target.Delete();}
    }
    private static Stored Bind(PlayerMobile owner,string name,int flat,int percent,int defense)
    {
        var pet=Pet(owner);var item=Named(name);int attackBefore=Attack(pet),defenseBefore=ClassStats.Defense(pet);
        Require(NpcEquipmentCombat.AttackFlat(pet)==0&&NpcEquipmentCombat.Defense(pet)==0,"Unbound summon received gear stats");
        item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);string gear=JsonSerializer.Serialize(ClassGear.State(item));
        Require(IronGolemItems.TryAttach(owner,pet,item,out string why),"Original binding failed: "+why);
        var row=new Stored(pet.Serial.Value,item.Serial.Value,name,gear,Attack(pet),ClassStats.Defense(pet),flat,percent,defense);
        Require(item.Parent==pet&&!item.Movable&&!item.Visible&&row.Attack>=attackBefore&&row.Defense>=defenseBefore,"Binding lost original or reduced known-positive stats");
        Verify(pet,row);
        var state=ClassGear.State(item);ClassGear.Profiles.Store(item,state with{Identified=false});
        Require(Attack(pet)==attackBefore&&ClassStats.Defense(pet)==defenseBefore,"Unidentified original still contributed numeric stats");
        ClassGear.Profiles.Store(item,state);Verify(pet,row);return row;
    }
    private static void Check()
    {
        var owner=new PlayerMobile{Name="NpcEquipmentCombatOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Holder history prerequisite failed");FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        var rows=new[]{Bind(owner,"The Atlantian",60,50,78),Bind(owner,"Steelgoad",30,0,0),Bind(owner,"The Diggler",50,0,2)};
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows)));PersistentMinions.Suspend(owner);owner.Internalize();Report();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(var row in saved.Actors)
        {
            var actor=(BaseCreature)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner&&actor.Map==Map.Internal
                &&item is{Deleted:false}&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==row.Gear,"Reload lost original identity/parent/rolls");
            Verify(actor,row);Require(Attack(actor)==row.Attack&&ClassStats.Defense(actor)==row.Defense,"Actual reload changed equipped NPC combat stats");
        }
        Report();
    }
    private static void Report()=>Log.Information("NPC original equipment:actual Atlantian/Steelgoad/Diggler flat/dex/percent attack and local/dex defense reach stat and hit-check consumers once;unidentified control and original ownership/full-roll persistence pass;not full properties/procs or learned Iron Golem");
}
