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
using Server.Network;
namespace Sanctuary.Integration;

public static class PetObstacleChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetObstacleChecks));
    private sealed record Saved(uint Owner,uint Body,uint[] Walls,Point3D Origin,int Map,IronGolemWorldState Route,ulong Seed,int Life,int Mana,int Generation);
    private sealed record Scene(PlayerMobile Owner,SkeletonWorldBody Body,Static[] Walls,IronGolemWorldState Initial,IronGolemWorldState Route,ulong Seed);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-obstacle-fixture.json");
    public static void Configure()
    {
        Register("pet-obstacle-check",Check);Register("pet-obstacle-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("pet-obstacle-live","Natural learned skeleton automatic obstacle repair",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("pet-obstacle-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Native item collision and automatic source follow repair",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Scene Create()
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixturePetObstacle";FixtureSpace.Corridor(owner,6940,20,3400);
        foreach(var pair in new[]{("necromancer.raiseskeleton",4),("necromancer.skeletonmastery",20)})
        {while(ClassCreation.Profiles.GetRank(owner.Serial,pair.Item1)<pair.Item2){Require(ClassCreation.Profiles.TryTrain(owner.Serial,pair.Item1,out _),"Obstacle train "+pair.Item1);}}
        var victim=new GraveyardZombie{NoKillAwards=true};victim.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);victim.Damage(100000,owner);if(victim.Alive){victim.Kill();}
        var corpse=victim.Corpse as Corpse;Require(SkeletonPets.Usable(owner,corpse),"Native obstacle learned corpse refused");
        NecromancerEffects.SetMana256(owner,owner.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(owner);
        Require(SkeletonPets.Raise(owner,corpse)&&NecromancerEffects.ManaBalance256(owner)==mana-9*256,"Obstacle learned once-only birth payment");
        var body=NecromancerSkeletons.For(owner,CombatAbility.RaiseSkeleton).OfType<SkeletonWorldBody>().Single();corpse.Delete();victim.Delete();
        owner.MoveToWorld(new(body.X-1,body.Y-1,body.Z),body.Map); // Adjacent diagonal: full-size source distance is zero.
        var initial=body.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)};body.StoreWorld(initial);
        int tile=Enumerable.Range(1,0x200).First(id=>TileData.ItemTable[id].Impassable&&TileData.ItemTable[id].CalcHeight>=16);
        var walls=Enumerable.Range(-1,3).Select(dy=>{var wall=new Static(tile);wall.MoveToWorld(new(body.X+1,body.Y+dy,body.Z),body.Map);return wall;}).ToArray();
        var origin=IronGolemWorld.Cell(initial);
        Require(PetGroundWorld.Collision(body,origin.X+2,origin.Y)!=0,"Native obstacle did not block its actual item tile");
        for(ulong seed=1;seed<=256;seed++)
        {
            body.StoreWorld(initial);NpcEquipmentRandom.Set(body,seed);
            IronGolemWorld.Advance(body,initial.NextUtc.AddMilliseconds(24*40L),true);var route=body.WorldState.Controller;
            Require(route.Fault==null&&route.Frame==25,"Automatic obstacle selection faulted or changed first think");
            if(seed<=4){Log.Information("Obstacle setup seed{Seed},mode{Mode},path{Path},target{Target},points{Points}",seed,route.Mode,route.Motion.PathType,route.Motion.FinalTarget,JsonSerializer.Serialize(route.Motion.Points));}
            if(route.Mode!=2||route.Motion.PathType!=7||route.Motion.FinalTarget.X<origin.X+5||Math.Abs(route.Motion.FinalTarget.Y-origin.Y)>2){continue;}
            var motion=initial.Motion with{PathType=2,Flags=0,Velocity=route.Motion.Velocity,MaximumVelocity=route.Motion.MaximumVelocity};
            var toward=SourceTowardPath.Find(motion,route.Motion.FinalTarget,5,(x,y)=>PetGroundWorld.Collision(body,x,y)!=0);
            if(toward.Points.SequenceEqual(route.Motion.Points)||route.Motion.Points.Length<2){continue;}
            Require(route.Motion.Points.All(p=>PetGroundWorld.Collision(body,p.X,p.Y)==0),"Automatic repaired route contains blocked waypoints");
            Log.Information("Pet obstacle selected seed{Seed},native wall{Tile},target{Target},Toward{Toward},repair{Repair}",seed,tile,route.Motion.FinalTarget,JsonSerializer.Serialize(toward.Points),JsonSerializer.Serialize(route.Motion.Points));
            return new(owner,body,walls,initial,route,seed);
        }
        foreach(var wall in walls){wall.Delete();}body.Delete();owner.Delete();throw new InvalidOperationException("No automatic native obstacle repair scene in256seeds");
    }
    private static void Delete(Scene s){foreach(var wall in s.Walls){wall.Delete();}s.Body.Delete();s.Owner.Delete();}
    private static void Check()
    {
        var s=Create();int mana=NecromancerEffects.ManaBalance256(s.Owner);var origin=s.Body.Location;
        SkillHealth.SetBalance256(s.Body,SkillHealth.Maximum256(s.Body)-257);TimedEffects.PreventHealing(s.Body,TimedEffects.Now);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(s.Owner.Serial.Value,s.Body.Serial.Value,s.Walls.Select(w=>w.Serial.Value).ToArray(),origin,s.Body.Map.MapID,
            s.Route,NpcEquipmentRandom.State(s.Body).Seed,SkillHealth.Balance256(s.Body),mana,0)));
        Require(s.Body.WorldState.Controller.Mode==2&&s.Body.WorldState.Controller.Motion.Points.Length>=2,"Saved obstacle actor lost selected route");
        Log.Information("Pet obstacle:actual learned corpse/payment,automatic AI67/path7selection and native blocking item produce a changed repair route; original moving actor staged");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);var body=(SkeletonWorldBody)World.FindMobile((Serial)s.Body);
        Require(body.ControlMaster==owner&&body.Map==Map.Internal&&SkillHealth.Balance256(body)==s.Life&&NpcEquipmentRandom.State(body).Seed==s.Seed
            &&NecromancerEffects.ManaBalance256(owner)==s.Mana,"Obstacle original owner/body/wounds/seed/mana changed after reload");
        Require(body.WorldState.Controller is{Mode:1,ActionAt:0,EndAt:0}&&body.WorldState.Controller.Motion.Points.Length==0,"Reload replayed or resumed offline moving action");
        Require(s.Route.Motion.PathType==7&&s.Route.Motion.Points.Length>=2&&s.Walls.All(id=>World.FindItem((Serial)id) is{Deleted:false} wall&&wall.Map.MapID==s.Map),"Original obstacles or recorded route lost");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var s=Create();FixtureNetwork.Authenticate(network,s.Owner,"pet-obstacle");
        // Choose the deterministic setup above, then replay from a fresh birth
        // clock on the natural timer. Do not force a path, target or AI request.
        s.Body.StoreWorld(s.Initial with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});NpcEquipmentRandom.Set(s.Body,s.Seed);
        int mana=NecromancerEffects.ManaBalance256(s.Owner);bool captured=false,moved=false;long deadline=Core.TickCount+12000;var previous=s.Body.WorldState.Controller;
        var born=s.Body.Location;
        void Wait()
        {
            try
            {
                var state=s.Body.WorldState.Controller;Require(Core.TickCount<deadline&&state.Fault==null&&!s.Body.Deleted,"Natural obstacle timeout/fault");
                long frames=state.Frame-previous.Frame;
                Require(frames>=0&&Math.Abs((long)state.Motion.X-previous.Motion.X)<=frames*16*Math.Max(state.Motion.Velocity,previous.Motion.Velocity)
                    &&Math.Abs((long)state.Motion.Y-previous.Motion.Y)<=frames*16*Math.Max(state.Motion.Velocity,previous.Motion.Velocity),"Natural obstacle moved farther than its frame/velocity bound");previous=state;
                if(!captured&&state.Frame>=25)
                {
                    Require(state.Mode==2&&state.Motion.PathType==7&&state.Motion.Points.SequenceEqual(s.Route.Motion.Points),"Natural AI did not select the measured repaired route");captured=true;
                }
                if(captured)
                {
                    moved|=s.Body.Location!=born;
                    Require(PetGroundWorld.Collision(s.Body,IronGolemWorld.Cell(state).X,IronGolemWorld.Cell(state).Y)==0,"Natural movement entered blocked item space");
                    if(IronGolemWorld.Cell(state)==s.Route.Motion.FinalTarget)
                    {
                        Require(moved&&s.Body.X>s.Owner.X+1&&NecromancerEffects.ManaBalance256(s.Owner)==mana,"Natural repair teleported,stalled or paid mana again");
                        Delete(s);Log.Information("pet-obstacle-live-result: passed");return;
                    }
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
            }
            catch(Exception e){Delete(s);Log.Error(e,"pet-obstacle-live-result: failed");}
        }
        Log.Information("pet-obstacle-live: ready");FixtureClock.NextLoop("pet-obstacle-live-result",Wait);
    }
}
