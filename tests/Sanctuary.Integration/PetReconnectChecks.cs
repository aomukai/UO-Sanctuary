// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class PetReconnectChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetReconnectChecks));
    private sealed record Saved(uint Owner,uint Body,int Rank,int Mastery,int Level,long BaseLife,int Maximum,int Life,int Mana,
        int Slots,long Order,DateTime HealBlockedUntil,ulong OriginalSeed,ulong Seed,int Generation,int TotalDraws,int LastFrame);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-reconnect-fixture.json");
    private static string ObstacleManifest=>Path.Join(Core.BaseDirectory,"pet-obstacle-fixture.json");
    public static void Configure()
    {
        Register("pet-reconnect-check",Check);
        Register("pet-reconnect-reload-check",()=>FixtureNetwork.Connected("pet-reconnect-reload-result",Reload));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original saved skeleton reconnect and natural AI continuation",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static DateTime Healing(SkeletonWorldBody body)=>TimedEffects.Snapshot().Single(s=>s.Owner==body.Serial.Value).HealBlockedUntilUtc;
    private static void Check()
    {
        var original=JsonNode.Parse(File.ReadAllText(ObstacleManifest));uint id=original["Body"].GetValue<uint>();var body=(SkeletonWorldBody)World.FindMobile((Serial)id);
        var owner=(PlayerMobile)body.ControlMaster;var state=body.WorldState.Controller;var seed=NpcEquipmentRandom.State(body);
        Require(state.Mode==2&&state.Motion.PathType==7&&state.Motion.Points.Length>=2,"Reconnect cohort must be the staged original moving skeleton");
        var saved=new Saved(owner.Serial.Value,id,body.Rank,body.Mastery,body.Level,body.SourceLife256,SkillHealth.Maximum256(body),SkillHealth.Balance256(body),NecromancerEffects.ManaBalance256(owner),
            body.ControlSlots,BattleCommand.PetOrder(body),Healing(body),seed.Seed,seed.Seed,0,0,state.Frame);
        Require(seed.Unarmed&&seed.Owner==saved.Owner&&seed.Item==0&&saved.Life<saved.Maximum,"Reconnect seed/birth/injury binding missing");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Stable(Saved s,PlayerMobile owner,SkeletonWorldBody body)
    {
        Require(body.ControlMaster==owner&&owner.Serial.Value==s.Owner&&body.Serial.Value==s.Body&&!body.Deleted&&body.Alive
            &&body.Rank==s.Rank&&body.Mastery==s.Mastery&&body.Level==s.Level&&body.SourceLife256==s.BaseLife
            &&SkillHealth.Maximum256(body)==s.Maximum&&SkillHealth.Balance256(body)==s.Life
            &&NecromancerEffects.ManaBalance256(owner)==s.Mana&&body.ControlSlots==s.Slots&&BattleCommand.PetOrder(body)==s.Order
            &&Healing(body)==s.HealBlockedUntil,"Reconnect replaced/reset/healed the original actor,changed capture/order or paid mana");
        var seed=NpcEquipmentRandom.State(body);Require(seed.Unarmed&&seed.Owner==s.Owner&&seed.Item==0,"Reconnect reassigned actor random binding");
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);var body=(SkeletonWorldBody)World.FindMobile((Serial)s.Body);
        Stable(s,owner,body);var state=body.WorldState.Controller;
        Require(s.Generation<2&&body.Map==Map.Internal&&state.Mode==1&&state.Motion.Points.Length==0&&NpcEquipmentRandom.State(body).Seed==s.Seed,"Offline reload replayed route/RNG or lost original parked actor");
        var account=FixtureNetwork.Authenticate(network,owner,"pet-reconnect");
        // Release only the inspection hold. Leave loaded/suspension flags,
        // source pose, seed, ranks and action state to the real controller.
        body.StoreWorld(state with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});
        bool returned=false,moving=false;Point3D landing=default;ActorMotionState pose=null;int frame=0;long deadline=Core.TickCount+20000;
        void Observe()
        {
            try
            {
                Stable(s,owner,body);var current=body.WorldState.Controller;
                Require(Core.TickCount<deadline&&current.Fault==null,"Original reconnect did not resume natural movement");
                if(!returned&&body.Map==owner.Map&&body.Map!=Map.Internal)
                {
                    returned=true;landing=body.Location;pose=current.Motion;frame=current.Frame;
                    Require(current.Mode!=8&&current.Attempts==0&&current.Hits==0,"Reconnect replayed birth animation or contact");
                }
                else if(returned)
                {
                    long frames=current.Frame-frame;
                    Require(frames>=0&&Math.Abs((long)current.Motion.X-pose.X)<=frames*16*Math.Max(current.Motion.Velocity,pose.Velocity)
                        &&Math.Abs((long)current.Motion.Y-pose.Y)<=frames*16*Math.Max(current.Motion.Velocity,pose.Velocity),"Reconnected actor exceeded frame/velocity movement bound");
                    pose=current.Motion;frame=current.Frame;moving|=current.Mode==2;
                    Require(PetGroundWorld.Collision(body,IronGolemWorld.Cell(current).X,IronGolemWorld.Cell(current).Y)==0,"Reconnected actor entered blocked item space");
                    if(moving&&body.Location!=landing&&NpcEquipmentRandom.State(body).Seed!=s.Seed)
                    {
                        ulong seed=s.Seed;int draws=0;
                        do{SourceRandom.Roll(ref seed);draws++;}while(seed!=NpcEquipmentRandom.State(body).Seed&&draws<1024);
                        Require(seed==NpcEquipmentRandom.State(body).Seed&&draws<1024,"Reconnect reset/reseeded instead of continuing the saved random stream");
                        PersistentMinions.Suspend(owner);Stable(s,owner,body);
                        body.StoreWorld(body.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});
                        var next=s with{Seed=seed,Generation=s.Generation+1,TotalDraws=s.TotalDraws+draws,LastFrame=current.Frame};
                        File.WriteAllText(Manifest,JsonSerializer.Serialize(next));
                        var original=JsonNode.Parse(File.ReadAllText(ObstacleManifest));original["Seed"]=seed;
                        File.WriteAllText(ObstacleManifest,original.ToJsonString());
                        network.Mobile=null;owner.NetState=null;account[0]=null;network.Account=null;account.Delete();
                        Log.Information("Pet reconnect:original{Body},generation{Generation},natural draws{Draws},frame{Frame},retained life{Life},mana{Mana}",s.Body,next.Generation,draws,current.Frame,next.Life,next.Mana);
                        Log.Information("pet-reconnect-reload-result: passed");return;
                    }
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
            }
            catch(Exception e){Log.Error(e,"pet-reconnect-reload-result: failed");}
        }
        FixtureClock.NextLoop("pet-reconnect-reload-result",Observe);
    }
}
