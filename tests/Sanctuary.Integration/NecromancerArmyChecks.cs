// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class NecromancerArmyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NecromancerArmyChecks));
    private sealed record Pet(uint Id,int Life,int Rank,long Order,bool Mage);
    private sealed record Saved(uint Owner,uint Helm,Point3D Origin,int Map,Pet[] Pets,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"necromancer-army-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("necromancer-army-check",Check);Register("necromancer-army-reload-check",Reload);
        Register("necromancer-army-live",()=>FixtureClock.NextLoop("necromancer-army-live-result",()=>FixtureNetwork.Connected("necromancer-army-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved Mage/Revive effective-rank limits and original roster lifecycle",_=>Core.LoopContext.Post(()=>
    {try{action();if(name!="necromancer-army-live"){Log.Information("{Name}: passed",name);}}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BaseCreature[] Mages(PlayerMobile p)=>NecromancerSkeletons.For(p,CombatAbility.RaiseSkeletalMage);
    private static BaseCreature[] Revives(PlayerMobile p)=>ReviveNativeActivation.Snapshot().Where(r=>r.Owner==p.Serial.Value)
        .Select(r=>World.FindMobile((Serial)r.Actor)).OfType<BaseCreature>().Where(a=>a.Alive&&!a.Deleted).ToArray();
    private static Pet Capture(BaseCreature p)=>new(p.Serial.Value,SkillHealth.Balance256(p),p is SkeletonGuardian mage?mage.Rank:ReviveNativeActivation.State(p).Setup.Rank,
        p is SkeletonGuardian?BattleCommand.PetOrder(p):ReviveNativeActivation.State(p).Order,p is SkeletonGuardian);
    private static BaseCreature Revive(PlayerMobile p)
    {
        var corpse=ReviveNativeActivationChecks.Body(p,false,out var actor);p.Mana=p.ManaMax;
        int before=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.Revive).Mana256;
        Require(ReviveCasts.Plan(p,corpse,out _,out _,out var issue)&&ReviveCasts.Release(p,corpse),"Learned original Revive failed: "+issue);
        Require(ReviveNativeActivation.State(actor)is{Setup.LifePlan:not null,Setup.RegenerationRate:not null}
            &&NecromancerEffects.ManaBalance256(p)==before-cost&&(corpse.Deleted||corpse.SanctuaryConsumed),"Learned Revive lost source life/recovery,original identity or exact payment/corpse");
        actor.AIObject?.AITimer.Stop();return actor;
    }
    private static SkeletonGuardian Mage(PlayerMobile p)=>SkeletonRosterChecks.Cast(p,CombatAbility.RaiseSkeletalMage);
    private static PlayerMobile Owner()
    {
        var p=MagePetBuffChecks.Owner(19,6660,2800);p.Name="PoisonDaggerFixtureArmy";
        MagePetBuffChecks.Train(p,"necromancer.revive",19);return p;
    }
    private static void End(PlayerMobile p)
    {foreach(var pet in(p.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Delete();}p.Delete();}
    private static void Numeric()
    {
        foreach(var row in new[]{(-1,0,0),(0,0,0),(1,1,1),(2,2,1),(3,3,1),(9,3,1),(10,4,2),(19,4,2),(20,5,3),(29,5,3),(30,6,4),(39,6,4),(40,7,5),(41,7,5),(255,7,5),(int.MaxValue,7,5)})
        {
            Require(NecromancerArmyLimits.ForRank(CombatAbility.RaiseSkeletalMage,row.Item1)==row.Item2&&NecromancerArmyLimits.ForRank(CombatAbility.Revive,row.Item1)==row.Item3,"Approved army boundary "+row.Item1);
        }
        bool refused=false;try{NecromancerArmyLimits.ForRank(CombatAbility.RaiseSkeleton,20);}catch(ArgumentOutOfRangeException){refused=true;}
        Require(refused&&SkeletonPets.Limit(20)==8&&SkeletonPets.Limit(40)==15,"Army override changed Warrior policy");
    }
    private static Saved Build()
    {
        var p=Owner();var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");
        Require(SkillMechanics.Rank(p,CombatAbility.RaiseSkeletalMage)==20&&SkillMechanics.Rank(p,CombatAbility.Revive)==20,"Real gear effective ranks");
        var firstMage=Mage(p);for(int i=0;i<5;i++){Mage(p);}var firstRevive=Revive(p);for(int i=0;i<3;i++){Revive(p);}
        Require(firstMage.Deleted&&firstRevive.Deleted&&Mages(p).Length==5&&Revives(p).Length==3,"Approved creation FIFO/counts");
        foreach(var pet in Mages(p).Concat(Revives(p))){SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)-1237);}
        return new(p.Serial.Value,helm.Serial.Value,p.Location,p.Map.MapID,Mages(p).Concat(Revives(p)).Select(Capture).ToArray(),0);
    }
    private static void Drop(PlayerMobile p,Item helm)
    {p.Backpack.DropItem(helm);ClassGear.Refresh(p);NecromancerArmyLimits.Enforce(p);}
    private static void Lowered(PlayerMobile p,Pet[] before)
    {
        foreach(bool mage in new[]{true,false})
        {
            var original=before.Where(r=>r.Mage==mage).ToArray();var actual=mage?Mages(p):Revives(p);
            Require(World.FindMobile((Serial)original[0].Id)is not{Deleted:false},"Cap drop kept oldest original");
            Require(actual.Select(Capture).SequenceEqual(original.Skip(1)),"Cap drop changed survivor identity/wounds/ranks/order");
        }
        Require(Mages(p).Length==4&&Revives(p).Length==2,"Current effective caps did not fall at19");
    }
    private static void Wardrobe(PlayerMobile p,Item helm)
    {
        var before=Mages(p).Concat(Revives(p)).Select(Capture).ToArray();
        Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Sorceress,out _,out _),"Army wardrobe outward transition");
        Require(Mages(p).Concat(Revives(p)).Select(Capture).SequenceEqual(before),"Foreign-class parking lost army");
        var state=ClassGear.State(helm);ClassGear.Profiles.Store(helm,state with{Identified=false});
        var filler=new Gold(1);p.Backpack.DropItem(filler);int capacity=p.Backpack.MaxItems;p.Backpack.MaxItems=p.Backpack.TotalItems;
        try
        {
            Require(!WardrobeTransaction.ChangeClass(p,DiabloClass.Necromancer,out _,out _),"Blocked wardrobe did not roll back");
            Require(ClassCreation.Profiles.TryGet(p.Serial,out var profile)&&profile.Class==DiabloClass.Sorceress
                &&Mages(p).Concat(Revives(p)).Select(Capture).SequenceEqual(before),"Failed wardrobe trimmed army during temporary gear loss");
        }
        finally{p.Backpack.MaxItems=capacity;filler.Delete();ClassGear.Profiles.Store(helm,state);}
        Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Necromancer,out _,out _)&&helm.Parent==p,"Army wardrobe return");
        Require(Mages(p).Concat(Revives(p)).Select(Capture).SequenceEqual(before),"Successful wardrobe trimmed army before restoring plus skills");
    }
    private static void Check()
    {
        Numeric();var saved=Build();var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var helm=World.FindItem((Serial)saved.Helm);
        Wardrobe(p,helm);PersistentMinions.Suspend(p);Drop(p,helm);Lowered(p,saved.Pets);
        var pets=Mages(p).Concat(Revives(p)).ToArray();Require(pets.All(a=>a.Map==Map.Internal),"Cap enforcement moved parked army");
        var output=saved with{Pets=pets.Select(Capture).ToArray()};p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(output));
        Log.Information("Necromancer army:approved16boundaries,real19+1gear,5Mage/3RevivecreationFIFO,parked4/2capdrop and original wounds/ranks/order staged");
    }
    private static void Reload()
    {
        Numeric();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var helm=World.FindItem((Serial)saved.Helm);
        var actual=Mages(p).Concat(Revives(p)).Select(Capture).ToArray();
        Require(actual.SequenceEqual(saved.Pets)&&p.AllFollowers.OfType<BaseCreature>().All(a=>a.Map==Map.Internal),"Saved army changed original identities/wounds/order: "+JsonSerializer.Serialize(new{saved=saved.Pets,actual,maps=p.AllFollowers.OfType<BaseCreature>().Select(a=>new{Id=a.Serial.Value,Map=a.Map?.MapID})}));
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Require(p.EquipItem(helm),"Reload re-equip original helm");ClassGear.Refresh(p);
        var oldestMage=Mages(p)[0];var oldestRevive=Revives(p)[0];Mage(p);Mage(p);Revive(p);Revive(p);
        Require(oldestMage.Deleted&&oldestRevive.Deleted&&Mages(p).Length==5&&Revives(p).Length==3,"Reloaded FIFO/caps");
        var before=Mages(p).Concat(Revives(p)).Select(Capture).ToArray();PersistentMinions.Suspend(p);Drop(p,helm);Lowered(p,before);
        var pets=Mages(p).Concat(Revives(p)).Select(Capture).ToArray();p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Pets=pets,Generation=saved.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var saved=Build();var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var helm=World.FindItem((Serial)saved.Helm);ReviveNativeActivationChecks.Attach(network,p);
        var destination=new Point3D(p.X+16,p.Y,p.Z);var town=new GuardedRegion("ArmyCapTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(destination.X-2,destination.Y-2,5,5));town.Register();p.MoveToWorld(destination,p.Map);
        Require(Mages(p).Concat(Revives(p)).All(a=>MinionSafeAreas.Parked(a)),"Live town entry did not park original army");
        var before=Mages(p).Concat(Revives(p)).Select(Capture).ToArray();p.Backpack.DropItem(helm);ClassGear.Changed(p);
        Log.Information("necromancer-army-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(350),()=>
        {
            try
            {
                Lowered(p,before);Require(Mages(p).Concat(Revives(p)).All(a=>MinionSafeAreas.Parked(a)),"Deferred cap change unparked army");
                PetStatsGump.DisplayTo(p);TalentGump.DisplayTo(p);Log.Information("necromancer-army-live-result: passed");
            }
            catch(Exception e){Log.Error(e,"necromancer-army-live-result: failed");}
            finally{network.Mobile=null;p.NetState=null;town.Unregister();End(p);}
        });
    }
}
