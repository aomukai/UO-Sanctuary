// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public class ReviveTransferFaultSkeleton:Skeleton
{
    [Constructible] public ReviveTransferFaultSkeleton(){}
    public ReviveTransferFaultSkeleton(Serial serial):base(serial){}
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
    public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown transfer fault fixture version");}}
    internal bool FailActivation;
    protected override void OnMapChange(Map oldMap)
    {
        base.OnMapChange(oldMap);
        if(FailActivation&&Controlled&&Map!=Map.Internal)
        {FailActivation=false;throw new InvalidOperationException("Injected transfer fixture failure");}
    }
}

public class ReviveCustomDefragSpawner:Spawner
{
    [Constructible] public ReviveCustomDefragSpawner(){}
    public ReviveCustomDefragSpawner(Serial serial):base(serial){}
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
    public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown controller fixture version");}}
    public override bool OnDefragSpawn(ISpawnable spawned,bool remove)=>base.OnDefragSpawn(spawned,remove);
}

public static class ReviveWorldTransferChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveWorldTransferChecks));
    private sealed record Saved(uint Owner,uint Active,uint Pending,uint ActiveCorpse,uint PendingCorpse,uint[] Spawners,uint[] Replacements,uint[] Waypoints,int[] ActiveHome);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-world-transfer-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original Revive spawner/home/waypoint transfer",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("revive-world-transfer-check",Check);Register("revive-world-transfer-reload-check",Reload);}
    private static void Unsupported()
    {
        var custom=new ReviveCustomDefragSpawner{Running=false};var actor=new Skeleton{Spawner=custom};actor.Hits=0;bool refused=false;
        try{ReviveWorldControllers.DetachDeath(actor);}catch(NotSupportedException){refused=true;}
        Require(refused&&actor.Spawner==custom&&!actor.Deleted&&ReviveWorldControllers.Issue(custom)!=null,"Custom controller silently unlinked or lacked an unsupported reason");
        actor.Delete();custom.Delete();
    }
    private static Corpse Kill(PlayerMobile owner,BaseCreature actor)
    {
        Corpse corpse=null;var died=ClassLootHooks.CreatureDied;var before=ClassLootHooks.BeforeDeath;var retained=ClassLootHooks.RetainOriginal;
        ClassLootHooks.BeforeDeath=ReviveBeforeDeath.Capture;ClassLootHooks.RetainOriginal=ReviveOriginals.Capture;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==actor){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(actor,true);actor.Damage(actor.Hits+1,owner);CombatDamage.Finish(actor,owner);}
        finally{ClassLootHooks.CreatureDied=died;ClassLootHooks.BeforeDeath=before;ClassLootHooks.RetainOriginal=retained;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==actor,"Spawned original not retained after actual death");corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    private static (Spawner Spawner,WayPoint Waypoint,BaseCreature Actor,Corpse Corpse,BaseCreature Replacement) SpawnDeath(PlayerMobile owner,int offset)
    {
        var waypoint=new WayPoint();waypoint.MoveToWorld(new(owner.X+offset+1,owner.Y,owner.Z),owner.Map);
        var spawner=new Spawner("Skeleton"){Running=false,Count=1,WalkingRange=5,WayPoint=waypoint,SpawnBounds=new(owner.X+offset,owner.Y,owner.Z,1,1,1)};
        spawner.MoveToWorld(new(owner.X+offset,owner.Y,owner.Z),owner.Map);spawner.Spawn();
        var actor=spawner.Spawned.Keys.OfType<BaseCreature>().Single();actor.NoKillAwards=true;actor.AIObject.AITimer.Stop();
        Require(actor.Spawner==spawner&&actor.Home!=Point3D.Zero&&actor.CurrentWayPoint==waypoint,"Native spawn did not establish world controllers");
        var corpse=Kill(owner,actor);
        Require(actor.Spawner==null&&spawner.Spawned.Count==0&&spawner.Entries.All(e=>e.Spawned.Count==0)
            &&actor.Home!=Point3D.Zero&&actor.CurrentWayPoint==waypoint,"Death unlink did not free both native rosters or retain inactive home/waypoint");
        spawner.Respawn();var replacement=spawner.Spawned.Keys.OfType<BaseCreature>().Single();replacement.AIObject.AITimer.Stop();replacement.Frozen=true;
        Require(replacement!=actor&&!actor.Deleted&&ReviveOriginals.Find(corpse)==actor&&corpse.GetAmount(typeof(Gold))==1,"Spawner respawn deleted/duplicated original or changed loot");
        return(spawner,waypoint,actor,corpse,replacement);
    }
    private static BaseCreature Activate(PlayerMobile owner,Corpse corpse,BaseCreature expected)
    {
        var point=new Point3D(corpse.X,corpse.Y+1,corpse.Z);
        Require(ReviveNativeActivation.CommitPrepared(owner,corpse,point,new(false,false,false,false,false,true),new(2,211,194,4,10),out var actor,out var issue),issue);
        Require(actor==expected&&actor.Home==point&&actor.CurrentWayPoint==null&&actor.Spawner==null&&actor.ControlMaster==owner,"Native ownership did not replace old home/waypoint with the parked class anchor or preserve original");return actor;
    }
    private static void Rollback(PlayerMobile owner)
    {
        var waypoint=new WayPoint();waypoint.MoveToWorld(new(owner.X+1,owner.Y+2,owner.Z),owner.Map);
        var actor=new ReviveTransferFaultSkeleton{NoKillAwards=true,Home=new(owner.X+2,owner.Y,owner.Z),HomeMap=owner.Map,CurrentWayPoint=waypoint};
        actor.MoveToWorld(new(owner.X+1,owner.Y+1,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();var corpse=Kill(owner,actor);
        var home=actor.Home;var map=actor.HomeMap;actor.FailActivation=true;bool failed=false;
        try{Activate(owner,corpse,actor);}catch(InvalidOperationException e)when(e.Message=="Injected transfer fixture failure"){failed=true;}
        Require(failed&&actor.ControlMaster==null&&!actor.Alive&&actor.Map==Map.Internal&&actor.Home==home&&actor.HomeMap==map&&actor.CurrentWayPoint==waypoint
            &&ReviveOriginals.Find(corpse)==actor&&!CorpseSkillRules.NoSelect(corpse)&&!corpse.SanctuaryConsumed&&corpse.GetAmount(typeof(Gold))==1,"Failed activation lost controllers,storage lease or loot");
        Activate(owner,corpse,actor);actor.Delete();corpse.Delete();waypoint.Delete();
    }
    private static void Check()
    {
        Unsupported();
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6450,40,2850);
        var first=SpawnDeath(owner,2);var active=Activate(owner,first.Corpse,first.Actor);var second=SpawnDeath(owner,6);
        Rollback(owner);
        var deletion=SpawnDeath(owner,10);deletion.Spawner.Delete();
        Require(!deletion.Actor.Deleted&&ReviveOriginals.Find(deletion.Corpse)==deletion.Actor&&deletion.Corpse.GetAmount(typeof(Gold))==1,"Deleting former spawner deleted retained original/loot");
        deletion.Actor.Delete();deletion.Corpse.Delete();deletion.Waypoint.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,active.Serial.Value,second.Actor.Serial.Value,first.Corpse.Serial.Value,second.Corpse.Serial.Value,
            [first.Spawner.Serial.Value,second.Spawner.Serial.Value],[first.Replacement.Serial.Value,second.Replacement.Serial.Value],[first.Waypoint.Serial.Value,second.Waypoint.Serial.Value],[active.Home.X,active.Home.Y,active.Home.Z])));
        owner.Internalize();Report();
    }
    private static void Reload()
    {
        Unsupported();
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var active=(BaseCreature)World.FindMobile((Serial)saved.Active);var pending=(BaseCreature)World.FindMobile((Serial)saved.Pending);
        Require(active is{Alive:true,Deleted:false}&&active.ControlMaster==owner&&active.Map==Map.Internal&&active.Home==new Point3D(saved.ActiveHome[0],saved.ActiveHome[1],saved.ActiveHome[2])&&active.CurrentWayPoint==null&&active.Spawner==null,"Reload lost active world transfer");
        Require(pending is{Alive:false,Deleted:false}&&pending.Map==Map.Internal&&pending.Spawner==null&&pending.Home!=Point3D.Zero
            &&pending.CurrentWayPoint?.Serial.Value==saved.Waypoints[1]&&ReviveOriginals.Find((Corpse)World.FindItem((Serial)saved.PendingCorpse))==pending,"Reload lost inactive original/controllers");
        for(int i=0;i<2;i++)
        {
            var spawner=(Spawner)World.FindItem((Serial)saved.Spawners[i]);var replacement=World.FindMobile((Serial)saved.Replacements[i]);
            spawner.Defrag();Require(spawner.Spawned.Count==1&&spawner.Spawned.ContainsKey((ISpawnable)replacement)&&replacement.Spawner==spawner,"Reload restored dead/active original to former live roster");
        }
        foreach(uint serial in new[]{saved.ActiveCorpse,saved.PendingCorpse}){Require(World.FindItem((Serial)serial) is Corpse corpse&&corpse.GetAmount(typeof(Gold))==1,"Reload lost independent corpse loot");}
        owner.MoveToWorld(new(6450,2850,0),Map.Trammel);Rollback(owner);owner.Internalize();Report();
    }
    private static void Report()=>Log.Information("Revive world transfer:actual native spawns/deaths free dictionary and entry rosters,respawn/delete preserve original and loot,ownership clears home/waypoint;injected post-move failure restores controllers/lease/loot and retry works;active/pending originals and replacements survive actual reloads");
}
