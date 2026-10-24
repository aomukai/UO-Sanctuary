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
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-lifecycle-fixtures.json");
    public static void Configure(){Register("blade-lifecycle-check",Check);Register("blade-lifecycle-reload-check",Reload);}
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
            Log.Information("Blade lifecycle: retained base-stat difficulty deltas before/after weapon inheritance, automatic owner facet stat hook, unchanged FP256 injury/packet/path/clock/seed/ranks, idempotence and owner-death trap removal pass; facet/logout spatial policy pending");
            body=null;p=null;
        }
        finally{SummonDifficulties.SetFacets(facets);body?.Delete();p?.Delete();}
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
