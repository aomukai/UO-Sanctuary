// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class RavenBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RavenBodyChecks));
    private sealed record Saved(uint Owner,uint Body,RavenBodyState State,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"raven-body-fixtures.json");
    private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("raven-body-check",Check);Register("raven-body-reload-check",Reload);
        Register("raven-body-live-check",()=>FixtureNetwork.Connected("raven-body-live-result",Live));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Raven body; current AI timing remains unverified",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Fresh(int rank=1)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="RavenBodyFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        for(int i=0;i<rank;i++){Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.raven",out _),"Train Raven");}
        FixtureSpace.Corridor(p,7050,24,1800+32*_scene++);return p;
    }
    private static GraveyardZombie Enemy(PlayerMobile p,int dx=0)
    {
        var t=new GraveyardZombie{CantWalk=true,NoKillAwards=true};t.SetHits(10000);
        foreach(var kind in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(kind,0);}
        t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;
    }
    private static void End(PlayerMobile p)
    {foreach(var body in DruidRavens.For(p)){body.Delete();}p.Delete();}
    private static void Through(DruidRavenBody body,int frame)
    {var s=body.Prepared;Require(frame>=s.Frame,"Test clock reversed");body.Advance(s.NextFrameUtc.AddMilliseconds((frame-s.Frame-1)*40L),true);}
    private static void Snapshots(DruidRavenBody body)
    {
        var s=body.Prepared;string before=body.SavedText;
        var bad=new[]{s with{Motion=null},s with{Owner=0},s with{Birth=s.Birth with{Life256=1}},s with{Mode=17},s with{NextThink=s.Frame},s with{TimingProfile="unknown"}};
        foreach(var row in bad)
        {bool refused=false;try{body.RestorePrepared(row);}catch(InvalidDataException){refused=true;}Require(refused && body.SavedText==before,"Malformed restore was not atomic");}
        body.RestorePrepared(s with{Motion=s.Motion with{SavedSteps=[new(1,2)]}});before=body.SavedText;
        var input=body.Prepared;body.RestorePrepared(input);input.Motion.SavedSteps[0]=new(3,4);
        var output=body.Prepared;output.Motion.SavedSteps[0]=new(5,6);
        Require(body.SavedText==before && body.Prepared.Motion.SavedSteps[0]==new Point2D(1,2),"Snapshot/restore array aliases live state");
        Require(Task.Run(()=>body.SavedText).GetAwaiter().GetResult()==before,"Worker changed immutable serialized state");
        body.RestorePrepared(JsonSerializer.Deserialize<RavenBodyState>(before));Require(body.SavedText==before,"JSON state round trip");
    }
    private static void Attacks()
    {
        var p=Fresh();var t=Enemy(p);var at=TimedEffects.Now.AddHours(1);var body=DruidRavenBody.CreatePrepared(p,p.Location,at,1);
        try
        {
            Require(body is{Blessed:true,BlocksFit:false,CanTarget:false,Hits:1,HitsMax:1,CanRegenHits:false,CanRegenMana:false,CanRegenStam:false,ControlSlots:0}
                && !body.CanBeDamaged() && body.ControlMaster==p && body.SummonClass==DiabloClass.Druid && body.Permitted(p),"Actual source invulnerability/ownership/body contract");
            Require(body.Prepared is{Mode:8,Frame:0,Remaining:12,NextThink:25,EndAt:14,StatProfile:DruidRavenBody.SnapshotStats},"Birth clock context");
            var unmapped=new GraveyardLich();unmapped.MoveToWorld(p.Location,p.Map);unmapped.AIObject.AITimer.Stop();
            Require(body.Eligible(p,unmapped),"Native monster without a source footprint cannot be targeted");unmapped.Delete();
            int life=SkillHealth.Balance256(t);body.BeginMelee(t);body.OnThink();Require(SkillHealth.Balance256(t)==life,"Native skeleton swing leaked");
            Through(body,14);Require(body.Prepared is{Mode:1,NextThink:25},"S1 end must preserve initial AI event");
            Through(body,24);Require(SkillHealth.Balance256(t)==life,"Damage before strike decision");
            Through(body,25);Require(body.Prepared is{Mode:4,Remaining:11,Attempts:1,Hits:1,ActionAt:0,EndAt:50}
                && SkillHealth.Balance256(t)<life,"Attack did not resolve immediately");
            int afterFirst=SkillHealth.Balance256(t);
            t.MoveToWorld(new(p.X+10,p.Y,p.Z),p.Map);
            Through(body,50);Require(body.Prepared is{Mode:1,NextThink:51},"Chase should resume after the attack animation");
            Through(body,65);Require(body.Prepared is{ActionAt:0,Remaining:11,Attempts:1} && SkillHealth.Balance256(t)==afterFirst,
                "Flock spent an attack while the monster was out of reach");
            t.MoveToWorld(p.Location,p.Map);body.MoveToWorld(p.Location,p.Map);
            // Keep a stationary target in source melee range and run the
            // declared legacy timing profile through natural budget exhaustion.
            for(int frame=66;frame<2000&&!body.Deleted;frame++){Through(body,frame);}
            Require(body.Deleted && SkillHealth.Balance256(t)<life && NamedControl.Blind(t),"Natural finite dives/damage/signed blind did not complete");
        }
        finally{t.Delete();End(p);}
    }
    private static void BoglingFocus()
    {
        var p=Fresh(5);
        var at=TimedEffects.Now.AddHours(1);
        var perch=new Point3D(p.X+6,p.Y,p.Z);
        var body=DruidRavenBody.CreatePrepared(p,perch,at,1);
        var t=new HavenEncounter(HavenEncounter.Bogling,1){CantWalk=true,NoKillAwards=true};
        try
        {
            t.SetHits(10000);t.SetResistance(ResistanceType.Physical,0);
            t.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);
            var ai=(LeashedMeleeAI)t.AIObject;
            ai.AITimer.Stop();
            long now=Core.TickCount;
            t.Combatant=p;
            Require(t.Combatant==p && !ai.UpdateLeash(now),"Start with an engaged bogling");
            int life=SkillHealth.Balance256(t);
            Through(body,25);
            Require(body.Location==perch && body.Prepared is{Attempts:1,Hits:1,ActionAt:0}
                && body.Prepared.Focus==t.Serial.Value && SkillHealth.Balance256(t)<life,"The first two-tile strike did not land immediately");
            Require(NamedControl.ApplySummonBlind(body,t,-1,false,()=>0) && NamedControl.Think(t)
                && t.Combatant==null && !ai.UpdateLeash(now+1) && body.Eligible(p,t),
                "The bogling's blindness made it enter the leash reset despite the flock's attack");
            life=SkillHealth.Balance256(t);
            Through(body,72);
            Require(body.Location==perch && body.Prepared.Focus==t.Serial.Value && body.Prepared is{Attempts:2,Hits:2}
                && SkillHealth.Balance256(t)<life,"The flock stopped attacking its blinded target");

            t.Home=new(t.X-t.HomeLeashRadius-1,t.Y,t.Z);
            Require(ai.UpdateLeash(now+2) && !t.IsEnemy(p) && body.Eligible(p,t),
                "Returning bogling either stayed in combat or stopped being a target");
            life=SkillHealth.Balance256(t);
            Through(body,120);
            Require(body.Location==perch && body.Prepared.Focus==t.Serial.Value && body.Prepared.Attempts>2
                && SkillHealth.Balance256(t)<life && t.ReturningHome && t.Combatant==null,
                "The flock stopped damaging the bogling or interrupted its return home");
        }
        finally{t.Delete();End(p);}
    }
    private static void FlockAndLifecycle()
    {
        var p=Fresh(5);var at=TimedEffects.Now.AddHours(1);var t=Enemy(p,1);var other=Enemy(p,2);
        try
        {
            var bodies=Enumerable.Range(0,6).Select(i=>DruidRavenBody.CreatePrepared(p,p.Location,at,(ulong)(100+i))).ToArray();
            Require(bodies.Take(5).All(x=>x.Deleted) && DruidRavens.For(p).Single()==bodies[^1]
                && bodies[^1].FlockSize==5,"One logical five-bird flock");
            DruidRavens.Focus(p,t);Require(DruidRavens.For(p).All(x=>x.Prepared.Focus==t.Serial.Value),"Whole-flock explicit target");
            DruidRavens.Focus(p,other);Require(DruidRavens.For(p).All(x=>x.Prepared.Focus==other.Serial.Value),"Whole-flock target switch");
            DruidRavens.Focus(p,p);Require(DruidRavens.For(p).All(x=>x.Prepared.Focus==other.Serial.Value),"Friendly focus accepted");
            var body=bodies[^1];Snapshots(body);var before=body.Prepared;
            body.Advance(at.AddMinutes(1),false);var offline=body.Prepared;
            Require(body.Map==Map.Internal && offline.Seed==before.Seed && offline.Remaining==before.Remaining && offline.Attempts==0 && offline.Hits==0,"Offline attacks/RNG/budget replay");
            body.Advance(at.AddMinutes(1).AddMilliseconds(40),true);Require(body.Map==p.Map && body.Prepared.Frame>offline.Frame,"Resume parked zero-size body");
            var oldFacets=SummonDifficulties.Facets();
            try
            {
                SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
                var old=body.Prepared.Birth;
                p.MoveToWorld(p.Location,Map.Felucca);
                Require(body.Prepared.Difficulty==MonsterDifficulty.Hell && body.Prepared.Birth.Level==old.Level
                    && body.Prepared.Birth.Rank==old.Rank && body is{Alive:true,Hits:1,HitsMax:1},"Facet callback changed birth or native alive marker");
                body.Advance(body.Prepared.NextFrameUtc,true);Require(body.Map==p.Map && body.Prepared.Map==p.Map.MapID,"Raven owner facet follow");
            }
            finally{SummonDifficulties.SetFacets(oldFacets);}
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Switch owner class");
            var parked=body.Prepared;body.Advance(parked.NextFrameUtc.AddSeconds(5),true);
            Require(body.Map==Map.Internal && body.Prepared.Seed==parked.Seed && body.Prepared.Remaining==parked.Remaining,"Wardrobe park consumed RNG/budget");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _),"Permission permits returning Druid with Ravens");
            body.Advance(body.Prepared.NextFrameUtc,true);Require(body.Map==p.Map,"Wardrobe resume");
            body.SetControlMaster(null);body.Advance(body.Prepared.NextFrameUtc,true);Require(body.Deleted,"Orphan Raven retained");
        }
        finally{t.Delete();other.Delete();End(p);}
    }
    private static void Flight()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var body=DruidRavenBody.CreatePrepared(p,new(p.X+15,p.Y,p.Z),at,42);
        try
        {
            var start=body.Location;Through(body,100);
            Require(body.X==start.X-1 && body.Y==start.Y && body.Prepared.Fault==null,"Flock did not take a native follow step");
        }
        finally{End(p);}
    }
    private static void ModeCapture()
    {
        var p=Fresh();var t=Enemy(p);var at=TimedEffects.Now.AddHours(1);var body=DruidRavenBody.CreatePrepared(p,p.Location,at,1);
        RavenSnapshotChecks.Legacy(body); // Construct the historical missing-profile scope.
        try
        {
            Through(body,25);Require(body.Prepared.Packet.Rank==1,"Initial mode packet");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(p.EquipItem(helm),"Mode capture actual Tarnhelm");ClassGear.Refresh(p);
            Through(body,36);Require(body.Prepared.Packet.Rank==1 && body.Prepared.Birth.Rank==1,"Mid-mode gear rewrote stored packet/birth");
            Through(body,65);Require(body.Prepared.Packet.Rank==2 && body.Prepared.Remaining==10 && body.Prepared.Birth.Rank==1,"Next strike ignored live gear or recaptured finite budget/birth");
            var before=body.Prepared;int life=SkillHealth.Balance256(t);PersistentMinions.Suspend(p);
            Require(body.Prepared is{Mode:1,ActionAt:0,Target:0} && body.Prepared.Remaining==before.Remaining,"Offline cancellation changed attack budget");
            body.Advance(before.NextFrameUtc.AddMinutes(5),false);
            Require(body.Prepared.Seed==before.Seed && body.Prepared.Attempts==before.Attempts && SkillHealth.Balance256(t)==life,"Offline pending strike replayed");
        }
        finally{t.Delete();End(p);}
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.Raven),"Learned Raven route missing");Attacks();BoglingFocus();ModeCapture();FlockAndLifecycle();Flight();
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidRavenBody.CreatePrepared(p,p.Location,at,442);
        Through(body,14);Snapshots(body);PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,p.Map.MapID,p.Location)));
        Log.Information("Prepared Raven flock: instant two-tile damage, bogling focus through leash reset, finite strikes, damage/blind, movement and lifecycle pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var body=(DruidRavenBody)World.FindMobile((Serial)saved.Body);
        Require(body!=null && !body.Deleted && body.ControlMaster==p && body.Map==Map.Internal && body.Permitted(p),"Saved ownership/body/permission/parking");
        Require(body.SavedText==JsonSerializer.Serialize(saved.State),"Saved actor budget/seed/clock/birth changed");Snapshots(body);
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        var before=body.Prepared;body.Advance(before.NextFrameUtc,false);
        Require(body.Prepared.Attempts==before.Attempts && body.Prepared.Remaining==before.Remaining && body.Prepared.Seed==before.Seed,"Reload offline replay");
        body.Advance(body.Prepared.NextFrameUtc,true);Require(body.Map==p.Map && body.ControlMaster==p,"Reload resumed body");PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=body.Prepared}));
    }
    private static void Live(NetState network)
    {
        var p=Fresh();var t=Enemy(p,5);TimedEffects.PreventHealing(t,TimedEffects.Now);int life=SkillHealth.Balance256(t);
        var town=new GuardedRegion("Raven live guard test",p.Map,1000,new Rectangle2D(p.X-3,p.Y-3,15,7));
        town.Register();
        network.Seeded=true;network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"Protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();
        var start=p.Location;var body=DruidRavenBody.CreatePrepared(p,start,TimedEffects.Now,1);
        body.FocusOn(t);
        t.MoveToWorld(new(p.X+7,p.Y,p.Z),p.Map);
        p.Direction=Direction.North;Require(p.Move(Direction.North),"Owner move inside guarded town");
        long deadline=Core.TickCount+30000;var previous=start;int steps=0;
        void Clean(){town.Unregister();t.Delete();End(p);}
        void Inspect()
        {
            try
            {
                Require(network.Running,"Fixture transport stopped: "+JsonSerializer.Serialize(body.Prepared));
                Require(Core.TickCount<deadline,"Natural Raven attack exhaustion timed out: "+JsonSerializer.Serialize(body.Prepared));
                if(!body.Deleted)
                {
                    Require(body.Map==p.Map && !MinionSafeAreas.Parked(body) && body.Prepared.Fault==null
                        && body.InRange(previous,1),"Flock warped or parked during native guarded-town pursuit");
                    if(body.Location!=previous){previous=body.Location;steps++;}
                    Timer.StartTimer(TimeSpan.FromMilliseconds(30),Inspect);return;
                }
                Require(steps>=5 && SkillHealth.Balance256(t)<life && body.Prepared.Attempts==12 && body.Prepared.Remaining==0,
                    "Native pursuit and twelve contact attempts: steps "+steps+" life "+life+" -> "+SkillHealth.Balance256(t)+" state="+JsonSerializer.Serialize(body.Prepared));
                Clean();Log.Information("raven-body-live-result: passed");
            }
            catch(Exception e){Clean();Log.Error(e,"raven-body-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("raven-body-live: ready");Inspect();});
    }
}
