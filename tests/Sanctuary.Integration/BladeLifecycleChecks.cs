// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BladeLifecycleChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeLifecycleChecks));
    private sealed record Saved(uint Owner,uint Actor,Point3D Origin,int Map,BladeBodyState State,int Life256,string Missile);
    private sealed record OfflineSaved(uint Owner,uint Actor,uint Target,DateTime Expiry,int Health,BladeMissileState Missile);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-lifecycle-fixtures.json");
    public static void Configure()
    {Register("blade-lifecycle-check",Check);Register("blade-lifecycle-reload-check",Reload);Register("blade-offline-stage",OfflineStage);Register("blade-offline-reload-check",OfflineReload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Retained blade difficulty delta, persistence and owner death",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void SameContext(BladeBodyState before,BladeBodyState after)
    {
        Require(JsonSerializer.Serialize(before.Control)==JsonSerializer.Serialize(after.Control)
            && JsonSerializer.Serialize(before.Motion)==JsonSerializer.Serialize(after.Motion)
            && before.NextFrameUtc==after.NextFrameUtc && before.Frames==after.Frames && before.Map==after.Map
            && before.Creation.Level==after.Creation.Level && before.Creation.Rank==after.Creation.Rank
            && before.Creation.BaseStatsLevel==after.Creation.BaseStatsLevel && before.Creation.AttackPercent==after.Creation.AttackPercent
            && before.Creation.MissileArmed==after.Creation.MissileArmed
            && before.Creation.Passives.SequenceEqual(after.Creation.Passives) && before.Creation.Skills.SequenceEqual(after.Creation.Skills),"Rescale restarted/recollected retained blade context");
    }
    private static void Check()
    {
        Travel();NaturalOffline();
        Casting();
        var p=BladeBodyChecks.Fresh();BladeSentinelBody body=null;var facets=SummonDifficulties.Facets();
        try
        {
            body=BladeSentinelSummons.Create(p,new(p.X+8,p.Y,p.Z),DateTime.UtcNow.AddDays(1));
            int mana=NecromancerEffects.ManaBalance256(p);SkillHealth.SetBalance256(body,12345);
            foreach(bool armed in new[]{false,true})
            {
                if(armed)
                {
                    body.AdvanceController(body.Prepared.NextFrameUtc);
                    Require(body.Prepared.Creation.MissileArmed,"First-missile inheritance did not run");
                }
                var normal=body.Prepared;var packet=BladeSentinelMissiles.Snapshot().Missiles.SingleOrDefault(x=>x.Owner==body.Serial.Value);
                string retained=JsonSerializer.Serialize(packet);
                foreach(var difficulty in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell,MonsterDifficulty.Normal})
                {
                    var before=body.Prepared;body.RescaleDifficulty(difficulty);var after=body.Prepared;
                    int level=normal.Creation.BaseStatsLevel;
                    Require(after.Spawn.Difficulty==difficulty && after.Creation.Attack==normal.Creation.Attack
                        +NecromancerSummons.Source("level-"+level,"L-TH",difficulty)-NecromancerSummons.Source("level-"+level,"L-TH",MonsterDifficulty.Normal)
                        && after.Creation.Defense==normal.Creation.Defense+NecromancerSummons.Source("level-"+level,"L-AC",difficulty)
                            -NecromancerSummons.Source("level-"+level,"L-AC",MonsterDifficulty.Normal),"Difficulty delta lost inherited AR or used caster rather than retained base-stat level");
                    Require(SkillHealth.Balance256(body)==12345 && body.HitsMax==100,"Equal-life difficulty refresh healed a blade");
                    SameContext(before,after);
                    Require(JsonSerializer.Serialize(BladeSentinelMissiles.Snapshot().Missiles.SingleOrDefault(x=>x.Owner==body.Serial.Value))==retained,"Rescale rewrote an already-created weapon missile");
                    body.RescaleDifficulty(difficulty);Require(body.Prepared==after && SkillHealth.Balance256(body)==12345,"Same difficulty notification rerounded or changed state");
                }
            }
            var origin=p.Location;var map=p.Map;var state=body.Prepared;
            string weapon=JsonSerializer.Serialize(BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==body.Serial.Value));
            SummonDifficulties.SetFacets([new(Map.Felucca.MapID,MonsterDifficulty.Nightmare)]);
            p.MoveToWorld(origin,Map.Felucca);
            Require(body.Prepared.Spawn.Difficulty==MonsterDifficulty.Nightmare && body.Map==map && SkillHealth.Balance256(body)==12345,"Owner facet hook missed blade stat rescaling or moved/healed it");
            SameContext(state,body.Prepared);
            Require(JsonSerializer.Serialize(BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==body.Serial.Value))==weapon,"Facet hook recollected a missile weapon packet");
            p.MoveToWorld(origin,map);Require(body.Prepared.Spawn.Difficulty==MonsterDifficulty.Normal,"Return facet did not restore source base stats");
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Difficulty transition charged summon mana");
            var mortal=BladeBodyChecks.Fresh();var doomed=BladeSentinelSummons.Create(mortal,new(mortal.X+8,mortal.Y,mortal.Z),DateTime.UtcNow.AddDays(1));
            mortal.Kill();BladeSentinelActors.Tick(DateTime.UtcNow);
            Require(doomed.Deleted && AssassinTrapRoster.For(mortal).Length==0,"Dead owner retained a registered trap");mortal.Delete();
            body.RescaleDifficulty(MonsterDifficulty.Hell);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,p.Location,p.Map.MapID,body.Prepared,SkillHealth.Balance256(body),weapon)));
            Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Incomplete lifecycle enabled player input");
            Log.Information("Blade lifecycle: retained base-stat difficulty deltas before/after weapon inheritance, automatic owner facet stat hook, unchanged FP256 injury/packet/path/clock/seed/ranks, idempotence and owner-death trap removal pass; approved old-facet/offline-expiry adapter installed");
            body=null;p=null;
        }
        finally{SummonDifficulties.SetFacets(facets);body?.Delete();p?.Delete();}
    }
    private static void NaturalOffline()
    {
        var p=BladeBodyChecks.Fresh();var body=BladeSentinelSummons.Create(p,new(p.X+8,p.Y,p.Z),TimedEffects.Now);
        var state=body.Prepared;
        Timer.StartTimer(TimeSpan.FromMilliseconds((state.Creation.DurationFrames+2)*40L+250),()=>
        {
            try
            {
                Require(p.NetState==null && body.Deleted && AssassinTrapRoster.For(p).Length==0
                    && !BladeSentinelMissiles.Snapshot().Missiles.Any(x=>x.Owner==body.Serial.Value),
                    "Natural offline conductor did not expire an unarmed trap");
                Log.Information("blade-offline-result: passed");
            }
            catch(Exception e){Log.Error(e,"blade-offline-result: failed");}
            finally{body.Delete();p.Delete();}
        });
    }
    private static string OfflineManifest=>Path.Join(Core.BaseDirectory,"blade-offline-fixtures.json");
    private static void OfflineStage()
    {
        var p=BladeBodyChecks.Fresh();var at=TimedEffects.Now.AddSeconds(2);
        at=new DateTime(at.Ticks-at.Ticks%(40*TimeSpan.TicksPerMillisecond),DateTimeKind.Utc);
        var body=BladeSentinelSummons.Create(p,new(p.X+8,p.Y,p.Z),at);
        body.AdvanceController(body.Prepared.NextFrameUtc);
        var missile=BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==body.Serial.Value);
        var target=new BladeMissileTarget{Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(50000);
        target.MoveToWorld(body.Location,body.Map);target.AIObject.AITimer.Stop();p.Internalize();
        File.WriteAllText(OfflineManifest,JsonSerializer.Serialize(new OfflineSaved(p.Serial.Value,body.Serial.Value,target.Serial.Value,
            at.AddMilliseconds((body.Prepared.Creation.DurationFrames+2)*40L),SkillHealth.Balance256(target),missile)));
    }
    private static void OfflineReload()
    {
        var saved=JsonSerializer.Deserialize<OfflineSaved>(File.ReadAllText(OfflineManifest));
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Actor) as BladeSentinelBody;
        var target=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);
        Require(p is{NetState:null} && p.Map==Map.Internal && target!=null && SkillHealth.Balance256(target)==saved.Health,
            "Reload lost offline owner/recipient or dealt disconnected damage");
        if(body is{Deleted:false})
        {
            var missile=BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==body.Serial.Value);
            Require(!AssassinTrapRoster.Fighting(body) && body.Prepared.ControllerFault==null && missile.Fault==null
                && missile.Seed==saved.Missile.Seed && missile.Position==saved.Missile.Position
                && JsonSerializer.Serialize(missile.Damage)==JsonSerializer.Serialize(saved.Missile.Damage),
                "Reload/offline catchup attacked, faulted, rerolled or recollected the saved packet");
        }
        var delay=saved.Expiry.AddMilliseconds(250)-TimedEffects.Now;
        Timer.StartTimer(delay>TimeSpan.Zero?delay:TimeSpan.FromMilliseconds(100),()=>
        {
            try
            {
                Require(World.FindMobile((Serial)saved.Actor)==null && AssassinTrapRoster.For(p).Length==0
                    && !BladeSentinelMissiles.Snapshot().Missiles.Any(x=>x.Owner==saved.Actor)
                    && SkillHealth.Balance256(target)==saved.Health,"Process reload restarted lifetime or attacked offline");
                Log.Information("blade-offline-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"blade-offline-reload-result: failed");}
            finally{body?.Delete();target.Delete();p.Delete();}
        });
    }
    private static void Travel()
    {
        var p=BladeBodyChecks.Fresh();BladeSentinelBody body=null;
        try
        {
            body=BladeSentinelSummons.Create(p,new(p.X+8,p.Y,p.Z),DateTime.UtcNow.AddDays(2));
            var map=body.Map;var origin=body.Location;var start=body.Prepared;
            Require(!AssassinTrapRoster.Fighting(body),"Disconnected actual trap permitted combat");
            body.AdvanceController(start.NextFrameUtc,false);
            Require(!body.Prepared.Creation.MissileArmed && body.Prepared.Control.Creates==0
                && body.Prepared.Motion==start.Motion && body.Prepared.Control.Seed==start.Control.Seed
                && body.Prepared.Control.Frame==1,"Offline first frame armed/moved/drew random or froze lifetime");
            // Explicit controller action service models a connected owner. The
            // production conductor supplies Fighting() from the native socket.
            p.MoveToWorld(new(p.X-2,p.Y,p.Z),Map.Felucca);
            body.AdvanceController(body.Prepared.NextFrameUtc);
            Require(body.Map==map && body.Prepared.ControllerFault==null && body.Prepared.Creation.MissileArmed,
                "Owner facet travel faulted/relocated trap or prevented first arm");
            var missile=BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==body.Serial.Value);
            var target=new BladeMissileTarget{Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(50000);
            target.MoveToWorld(origin,map);target.AIObject.AITimer.Stop();int health=SkillHealth.Balance256(target);
            try
            {
                Require(body.TrapTarget(p,target) && BladeSentinelMissiles.Recipient(body,missile,target.X*2,target.Y*2)==target,
                    "Trap inherited remote owner visibility/map instead of its own targeting position");
                var before=body.Prepared;var clock=before.NextFrameUtc;
                p.Internalize();Require(!AssassinTrapRoster.Fighting(body),"Internal/offline owner permitted trap combat");
                BladeSentinelMissiles.TickFrame(missile.NextUtc);body.AdvanceController(clock,false);
                var quiet=BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==body.Serial.Value);
                Require(quiet.NextUtc==missile.NextUtc.AddMilliseconds(40) && quiet.Position==missile.Position
                    && quiet.Seed==missile.Seed && quiet.Damage==missile.Damage && quiet.Fault==null
                    && SkillHealth.Balance256(target)==health && body.Prepared.Motion==before.Motion
                    && body.Prepared.Control.Seed==before.Control.Seed && body.Map==map,
                    "Offline trap attacked, moved, rerolled, resnapshotted or faulted");
                p.MoveToWorld(new(origin.X-2,origin.Y,origin.Z),Map.Felucca);
                body.AdvanceController(body.Prepared.NextFrameUtc);
                Require(body.Prepared.ControllerFault==null && body.Prepared.Control.Creates==1 && body.Map==map,
                    "Returning owner restarted/relocated/faulted existing blade");
                // A fault must suppress mechanics without preserving an immortal
                // trap. Advance only this held-clock fixture, never the world.
                body.SuspendController("integration path fault");
                var saved=BladeSentinelMissiles.Snapshot();
                BladeSentinelMissiles.Restore(saved with{Missiles=saved.Missiles.Select(x=>x.Id==quiet.Id?x with{Fault="integration missile fault"}:x).ToArray()});
                int expiry=body.Prepared.Control.Ai.Expiry;
                body.AdvanceController(body.Prepared.NextFrameUtc.AddMilliseconds((expiry-body.Prepared.Control.Frame)*40L),false);
                Require(body.Deleted && AssassinTrapRoster.For(p).Length==0,"Offline/faulted trap outlived its original lifetime");
                // No due actor/missile frame remains for this faulted packet.
                // The ordinary wall-clock tick must still prune the orphan.
                BladeSentinelActors.Tick(TimedEffects.Now);
                Require(!BladeSentinelMissiles.Snapshot().Missiles.Any(x=>x.Owner==body.Serial.Value),"Expired body retained following missile");
            }
            finally{target.Delete();}
            Log.Information("Blade travel/offline: actual socket-null combat gate, old-facet controller/arming, Internal-map suppression, no damage/pose/RNG/snapshot replay, return without restart and original-clock faulted expiry pass");
        }
        finally{body?.Delete();p.Delete();}
    }
    private static void Casting()
    {
        var p=BladeBodyChecks.Fresh();
        try
        {
            var values=SkillMechanics.Values(p,CombatAbility.BladeSentinel);
            Require(values.Mana256==1792 && SkillMechanics.For(CombatAbility.BladeSentinel).Number("localdelay")==25
                && BladeSentinelCasts.Timing(p)==LobProjectiles.Timing(p),"Pinned mana/local delay/shared S2 timing");
            ClassCombat.For(p).PendingPoint=new(p.X+6,p.Y,p.Z);NecromancerEffects.SetMana256(p,1791);
            Require(!BladeSentinelCasts.Release(p,Serial.Zero) && AssassinTrapRoster.For(p).Length==0
                && NecromancerEffects.ManaBalance256(p)==1791 && FireGround.Ready(p,CombatAbility.BladeSentinel),"Unaffordable release created a trap/paid/delayed");
            NecromancerEffects.SetMana256(p,1792);Require(BladeSentinelCasts.Release(p,Serial.Zero)
                && NecromancerEffects.ManaBalance256(p)==0 && AssassinTrapRoster.For(p).Length==1
                && !FireGround.Ready(p,CombatAbility.BladeSentinel) && FireGround.Ready(p,CombatAbility.FireBlast),"Successful release did not pay exactly7 once or delayed another skill");
            NecromancerEffects.SetMana256(p,1792);Require(!BladeSentinelCasts.Release(p,Serial.Zero)
                && NecromancerEffects.ManaBalance256(p)==1792 && AssassinTrapRoster.For(p).Length==1,"Local-delay duplicate release created/paid another trap");
            Require(!BladeSentinelCasts.Begin(p,Serial.Zero,ClassCombat.For(p).PendingPoint,Core.TickCount)
                && !ClassCombat.Available(p,CombatAbility.BladeSentinel),"Unregistered cast adapter exposed learned input");
            Log.Information("Blade release adapter: exact7mana successful factory payment, insufficient-mana refusal,25-frame independent skill delay and duplicate suppression pass; full windup/network input remains gated");
        }
        finally
        {
            foreach(var entry in AssassinTrapRoster.For(p)){World.FindMobile((Serial)entry.Actor)?.Delete();}p.Delete();
        }
    }
    private static void Reload()
    {
        Travel();NaturalOffline();
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var body=(BladeSentinelBody)World.FindMobile((Serial)saved.Actor);
        Require(body!=null && body.SummonMaster==p && body.Prepared.Spawn.Difficulty==MonsterDifficulty.Hell
            && JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.State) && SkillHealth.Balance256(body)==saved.Life256,"Reload lost rescaled blade context or fractional injury");
        Require(JsonSerializer.Serialize(BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==body.Serial.Value))==saved.Missile,"Reload changed preexisting blade missile packet/clock");
        var state=body.Prepared;p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        Require(body.Prepared.Spawn.Difficulty==MonsterDifficulty.Normal && SkillHealth.Balance256(body)==saved.Life256,"Returning owner failed automatic stat rescale or healed blade");SameContext(state,body.Prepared);
        body.RescaleDifficulty(MonsterDifficulty.Hell);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=body.Prepared,Life256=SkillHealth.Balance256(body)}));
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Reload enabled unfinished input");
        Log.Information("Blade lifecycle reload: rescaled actor retains captured rank/AR inheritance/FP256 injury/path/seed/clock and immutable missile; owner map notification reapplies current facet stats without recasting");
    }
}
