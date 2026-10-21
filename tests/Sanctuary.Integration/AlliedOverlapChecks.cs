using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class AlliedOverlapChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(AlliedOverlapChecks));
    private static void Require(bool value, string why) => ImbuingChecks.Require(value, why);
    public static void Configure() => ConsoleInputHandler.RegisterCommand("allied-overlap-check", "Allied transit, stopping, action lock, retreat and regroup", _ => Core.LoopContext.Post(() =>
    { try { Check(); _logger.Information("allied-overlap-check: passed"); } catch (Exception e) { _logger.Error(e,"allied-overlap-check: failed"); } }));
    internal static PlayerMobile Player(int x = 5650, int y = 1100)
    {
        var p = ImbuingChecks.Player(SkillName.Inscribe); p.MoveToWorld(new Point3D(x,y,0),Map.Trammel); return p;
    }
    internal static SkeletonGuardian Pet(PlayerMobile owner, Point3D at)
    {
        if (ClassCreation.Profiles.GetRank(owner.Serial, "necromancer.raiseskeleton") == 0)
        { Require(ClassCreation.Profiles.TryTrain(owner.Serial, "necromancer.raiseskeleton", out _), "Overlap owner needs a permitted summon skill"); }
        var pet = new SkeletonGuardian { Frozen = true }; pet.Initialize(owner,1,0); pet.SetControlMaster(owner);
        pet.MoveToWorld(at,owner.Map); pet.IssueOrder(OrderType.Guard,null); return pet;
    }
    private static void Check()
    {
        var p=Player(); var at=p.Location; var pet=Pet(p,new Point3D(p.X+1,p.Y,p.Z));
        var enemy=new GraveyardSkeleton { Frozen=true }; enemy.MoveToWorld(new Point3D(p.X+2,p.Y,p.Z),p.Map);
        try
        {
            int stamina=1; p.Stam=stamina; p.Direction=Direction.East;
            Require(p.Move(Direction.East) && p.Location==pet.Location && p.Stam==stamina,"Owner could not pass ally at low stamina");
            Require(AlliedOverlap.Blocked(p) && !AlliedOverlap.Blocked(pet),"Passer/holder priority wrong");
            var scroll=new TownPortalScroll(); p.Backpack.DropItem(scroll); var id=new IdentifyTome(); p.Backpack.DropItem(id);
            Require(!p.AllowItemUse(scroll) && !p.AllowSkillUse(SkillName.Inscribe) && !p.OnDragLift(scroll)
                && !EquipmentEconomy.Ready(p) && !ClassProfessions.Ready(p) && !PotionBelts.Ready(p),"Overlap permitted an item/skill/craft/belt action");
            Require(!TownPortals.Open(p,scroll) && !scroll.Deleted && !Identification.Use(p,id) && id.Charges==3,"Overlap spent a utility");
            Require(!ClassCombat.Perform(p,CombatAbility.BasicAttack,enemy,enemy.Location,Core.TickCount),"Overlapping owner attacked");
            Require(AlliedOverlap.Allowed(pet),"Incumbent pet was disabled by passing owner");
            int ph=p.Hits, sh=pet.Hits; p.Damage(3,enemy); pet.Damage(3,enemy);
            Require(p.Hits<ph && pet.Hits<sh,"Sharing a tile granted damage immunity");
            pet.MoveToWorld(at,p.Map); pet.MoveToWorld(p.Location,p.Map);
            Require(AlliedOverlap.Allowed(p) && p.AllowItemUse(id) && AlliedOverlap.Blocked(pet),"Passing pet disabled its incumbent summoner");
            long now=Core.TickCount; var ownerLocation=p.Location; var serial=pet.Serial; int life=pet.Hits;
            Require(AlliedOverlap.UpdatePet(pet,now) && AlliedOverlap.UpdatePet(pet,now+4999) && pet.Location==p.Location,"Pet regrouped before five seconds");
            Require(AlliedOverlap.UpdatePet(pet,now+5000) && pet.Serial==serial && pet.GetMaster()==p && pet.Hits==life
                && pet.Location!=p.Location && !AlliedOverlap.Blocked(pet) && p.Location==ownerLocation,"Regroup healed, replaced, stacked or moved the owner");
            Require(AlliedOverlap.Allowed(p) && p.AllowItemUse(id),"Walk-clear did not unlock actions");
            p.Direction=Direction.East; var before=p.Location; Require(!p.Move(Direction.East) && p.Location==before,"An enemy was pass-through");
            pet.MoveToWorld(at,p.Map); p.MoveToWorld(new Point3D(at.X+1,at.Y,at.Z),p.Map); pet.Frozen=false; pet.Stam=1; pet.Direction=Direction.East;
            Require(pet.Move(Direction.East) && pet.Location==p.Location && pet.Stam==1,"Pet could not pass through summoner");
            pet.Frozen=true; pet.MoveToWorld(new Point3D(p.X,p.Y,p.Z+20),p.Map);
            Require(!AlliedOverlap.Blocked(p),"A different floor locked actions");
        }
        finally { pet.Delete(); enemy.Delete(); p.Delete(); }
        Priority(); PetResurrection(); Corridor(); Portal();
    }
    private static void Priority()
    {
        var p=Player(5665,1100); var first=Pet(p,p.Location); var second=Pet(p,p.Location);
        try
        {
            Require(AlliedOverlap.Allowed(p) && AlliedOverlap.Blocked(first) && AlliedOverlap.Blocked(second),"New arrivals displaced the tile holder");
            p.Resurrect(); Require(AlliedOverlap.Allowed(p),"Redundant resurrection displaced the living holder");
            p.MoveToWorld(new Point3D(p.X,p.Y+1,p.Z),p.Map);
            Require(AlliedOverlap.Allowed(first) && AlliedOverlap.Blocked(second),"Earliest remaining arrival was not promoted");
            var held=first.Location;
            Require(!AlliedOverlap.UpdatePet(first,Core.TickCount+10000) && first.Location==held,"Incoming ally forced holder to retreat or regroup");
            first.MoveToWorld(new Point3D(p.X+1,p.Y,p.Z),p.Map);
            Require(AlliedOverlap.Allowed(second),"Next remaining occupant was not promoted");
        }
        finally { second.Delete(); first.Delete(); p.Delete(); }
    }
    private static void PetResurrection()
    {
        var p=Player(5665,1110);
        var pet=new Skeleton { Controlled=true, ControlMaster=p, IsBonded=true, Frozen=true };
        pet.MoveToWorld(new Point3D(p.X+1,p.Y,p.Z),p.Map); p.MoveToWorld(pet.Location,p.Map);
        try
        {
            Require(AlliedOverlap.Blocked(p) && AlliedOverlap.Allowed(pet),"Bonded pet did not initially hold its tile");
            pet.Kill(); Require(pet.IsDeadPet && AlliedOverlap.Allowed(p) && !AlliedOverlap.Blocked(pet),"Ghost pet retained a tile claim");
            pet.ResurrectPet(); Require(!pet.IsDeadPet && AlliedOverlap.Allowed(p) && AlliedOverlap.Blocked(pet),"Resurrected pet displaced the incumbent");
        }
        finally { pet.Corpse?.Delete(); pet.Delete(); p.Delete(); }
    }
    private static void Corridor()
    {
        var p=Player(5680,1100); var pet=Pet(p,p.Location); var allies=new List<SkeletonGuardian>(); var walls=new List<Item>();
        var enemy=new GraveyardSkeleton { Frozen=true }; enemy.MoveToWorld(new Point3D(p.X+1,p.Y,p.Z),p.Map);
        try
        {
            // The only escape is west through two allies; the east front is occupied by an enemy.
            for(int x=-2;x<=1;x++) { foreach(int y in new[]{-1,1}) { var wall=new Static(0x80); wall.MoveToWorld(new Point3D(p.X+x,p.Y+y,p.Z),p.Map); walls.Add(wall); } }
            allies.Add(Pet(p,new Point3D(p.X-1,p.Y,p.Z))); allies.Add(Pet(p,new Point3D(p.X-2,p.Y,p.Z)));
            Require(AlliedOverlap.FindRoute(pet,enemy.Location,0,out var free,out var direction,escape:true)
                && free.X<p.X-2 && direction==Direction.West,"Blocked front did not retreat through allied chain to a free endpoint");
            Require(p.Map.CanSpawnMobile(free),"Planner selected an occupied endpoint");
            pet.Frozen=false; pet.Direction=Direction.West; pet.AIObject.NextMove=Core.TickCount;
            var before=pet.Location; AlliedOverlap.UpdatePet(pet,Core.TickCount);
            Require(pet.X==before.X-1 && pet.Y==before.Y,"Actual pet AI did not retreat through ally");
            var holder=allies[0]; var held=holder.Location;
            Require(AlliedOverlap.Blocked(pet) && AlliedOverlap.Allowed(holder)
                && !AlliedOverlap.UpdatePet(holder,Core.TickCount+10000) && holder.Location==held
                && AlliedOverlap.Allowed(allies[1]),"Retreat cascaded backwards through the allied line");
        }
        finally { foreach(var w in walls) { w.Delete(); } foreach(var a in allies) { a.Delete(); } pet.Delete(); enemy.Delete(); p.Delete(); }
    }
    private static void Portal()
    {
        var p=Player(1382,1495); p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel,1382,1495),Map.Trammel);
        var scroll=new TownPortalScroll(); p.Backpack.DropItem(scroll); PlayerMobile bystander=null;
        try
        {
            Require(TownPortals.Open(p,scroll),"Portal fixture unavailable"); var pair=TownPortals.For(p);
            bystander=Player(); bystander.MoveToWorld(pair.Town.Location,p.Map); var stayed=bystander.Location;
            Require(!TownPortals.Travel(bystander,pair.Town) && bystander.Location==stayed,"Portal transported a bystander");
            p.Location=new Point3D(pair.Field.X+1,pair.Field.Y,pair.Field.Z);
            Require(TownPortals.Travel(p,pair.Field) && p.Location!=bystander.Location && p.InRange(pair.Town,2)
                && bystander.Location==stayed && !AlliedOverlap.Blocked(p),"Occupied endpoint did not choose a free nearby arrival");
        }
        finally { TownPortals.Close(p.Serial); bystander?.Delete(); p.Delete(); }
    }
}
