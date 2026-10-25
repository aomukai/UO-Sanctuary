// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

// Actual saved stationary actors. Dispatch is a declared fixture service:
// these checks do not certify live wave movement/damage or retirement.
public static class WakeFireActorChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WakeFireActorChecks));
    private sealed record Body(uint Serial,SentryBodyState State,int Life256);
    private sealed record Saved(uint Owner,int OwnerMap,Point3D OwnerPosition,Body[] Bodies,AssassinTrapEntry[] Roster);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"wake-fire-actor-fixtures.json");
    public static void Configure(){Register("wake-fire-actor-check",Check);Register("wake-fire-actor-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared saved Wake actor; dispatch is an explicit service, learned input disabled",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank)
    {string id=SkillMechanics.For(ability).TalentId;while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static void Frames(AssassinSentryBody b,int count,bool actions=true)
    {for(int i=0;i<count;i++){b.AdvanceController(b.Prepared.NextFrameUtc,actions);}}
    private static AssassinSentryBody Create(PlayerMobile p)=>AssassinSentryBody.CreateEmerging(p,new(p.X+2,p.Y,p.Z),DateTime.UtcNow.AddDays(1),SourceRandom.InitLow(91),CombatAbility.WakeOfFire);
    private static void Creation()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"wake-of-fire-damage-source-probe.json")));
        var fields=doc.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();
        int min=Array.IndexOf(fields,"nFireMinDamage"),max=Array.IndexOf(fields,"nFireMaxDamage"),count=0;
        var row=SkillMechanics.For(CombatAbility.WakeOfFire);
        foreach(var r in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();if(V(5)!=0){continue;}
            int Rank(string key)=>key==row.SourceKey?V(0):key=="Fire Trauma"?V(1):key=="Inferno Sentry"?V(2):0;
            var values=new SkillValues(row,Rank,Rank,99);
            var creation=AssassinSentryCreation.Prepare(values,99,MonsterDifficulty.Normal,
                key=>key=="passive_fire_mastery"?V(3):0);
            Require(creation.Ability==CombatAbility.WakeOfFire&&creation.Skill("Wake Of Destruction Sentry")==V(0)
                &&creation.Skill("Fire Trauma")==V(1)&&creation.Skill("Inferno Sentry")==V(2)
                &&AssassinSentryCreation.Shots(creation)==5&&AssassinSentryCreation.Values(creation).Rank==V(0),"Wake captured skills/main rank/fixed shots");
            Require(AssassinSentryCreation.Fire(creation)==(r[8][min].GetInt32(),r[8][max].GetInt32()),"Captured Wake source damage differential "+count);count++;
        }
        Require(count==3711,"Wake creation/damage source inventory");
        foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        foreach(int rank in new[]{1,3,20,21,255})
        {
            var values=new SkillValues(row,k=>k==row.SourceKey?rank:20,_=>20,99);
            var stats=AssassinSentryCreation.Prepare(values,99,difficulty,k=>k=="passive_fire_mastery"?55:k=="passive_fire_pierce"?-11:0);
            Require(stats.Passive("passive_fire_mastery")==55&&stats.Passive("passive_fire_pierce")==-11&&stats.Passives.Length==3,"Inherited fire stats");
            var hell=AssassinSentryCreation.Rescale(stats,MonsterDifficulty.Hell);
            Require(hell.Ability==stats.Ability&&hell.Passives==stats.Passives&&hell.Skills==stats.Skills&&hell.Level==stats.Level
                &&AssassinSentryCreation.Fire(hell)==AssassinSentryCreation.Fire(stats),"Difficulty recomputed captured skill/passive context");
            bool rejected=false;try{AssassinSentryCreation.Lightning(stats);}catch(NotSupportedException){rejected=true;}
            Require(rejected,"Wake silently used lightning packet");
            rejected=false;try{AssassinSentryCreation.Bolts(stats);}catch(NotSupportedException){rejected=true;}
            Require(rejected,"Wake silently used lightning bolt count");
        }
        bool refused=false;try{AssassinSentryCreation.Prepare(new(row,_=>1,_=>1,99),99,MonsterDifficulty.Normal,
            name=>name=="item_pierce_fire_immunity"?1:0);}catch(NotSupportedException){refused=true;}
        Require(refused,"Unsupported inherited fire sunder silently accepted");
        // Old saved contexts have no discriminator. Preserve CBS, including its original formula.
        var cbs=AssassinSentryCreation.Prepare(new(SkillMechanics.For(CombatAbility.ChargedBoltSentry),_=>1,_=>1,99),99,MonsterDifficulty.Normal,_=>0);
        var old=JsonSerializer.SerializeToNode(cbs).AsObject();old.Remove("Ability");
        var copy=JsonSerializer.Deserialize<SentryCreationStats>(old.ToJsonString());
        Require(copy.Ability==CombatAbility.ChargedBoltSentry&&AssassinSentryCreation.Shots(copy)==5
            &&AssassinSentryCreation.Lightning(copy)==AssassinSentryCreation.Lightning(cbs),"Legacy saved creation changed ability or damage");
    }
    private static void Services(Action run)
    {
        var selector=AssassinSentryActors.SelectTarget;var dispatch=AssassinSentryActors.DispatchWake;
        AssassinSentryActors.SelectTarget=b=>new(b.Serial.Value,10);
        AssassinSentryActors.DispatchWake=(b,target)=>Require(target==b.Serial.Value,"Wake pending unit identity changed");
        try{run();}finally{AssassinSentryActors.SelectTarget=selector;AssassinSentryActors.DispatchWake=dispatch;}
    }
    private static void Save(PlayerMobile p)
    {
        var roster=AssassinTrapRoster.For(p);var bodies=roster.Select(x=>(AssassinSentryBody)World.FindMobile((Serial)x.Actor)).ToArray();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,p.Map.MapID,p.Location,
            bodies.Select(x=>new Body(x.Serial.Value,x.Prepared,SkillHealth.Balance256(x))).ToArray(),roster)));
    }
    private static void Check()
    {
        Creation();var p=BladeBodyChecks.Fresh(6460,16);p.Name="WakeActorFixture";
        var facets=SummonDifficulties.Facets();int mana=NecromancerEffects.ManaBalance256(p);
        try
        {
            Train(p,CombatAbility.FireBlast,15);Train(p,SkillMechanics.Source("Inferno Sentry").Ability,20);
            var blade=BladeSentinelSummons.Create(p,new(p.X+8,p.Y,p.Z),DateTime.UtcNow.AddDays(1));
            var cbs=AssassinSentryBody.CreatePrepared(p,new(p.X+3,p.Y,p.Z),DateTime.UtcNow.AddDays(1),1);
            var bodies=Enumerable.Range(0,5).Select(_=>Create(p)).ToArray();
            Require(blade.Deleted&&cbs.Deleted&&AssassinTrapRoster.For(p).Select(x=>x.Actor).SequenceEqual(bodies.Select(x=>x.Serial.Value)),"Mixed Blade/CBS/Wake cap5 FIFO");
            Require(bodies.All(b=>b.Prepared.Creation is{Ability:CombatAbility.WakeOfFire,Rank:1,Level:75,Life256:25600}
                &&b.Prepared.Control==null&&b.Prepared.WakeControl is{Mode:8,NextThink:25,EndAt:15}
                &&b.SummonMaster==p&&b.HitsMax==100&&!b.BlocksFit&&!b.CanBeDamaged()&&!b.UsesNativeSummonTimer
                &&b.Name.Contains("[placeholder]")&&ClassStats.Level(b)==75
                &&ClassStats.Defense(b)==b.Prepared.Creation.Defense&&ClassStats.AttackRating(b)==b.Prepared.Creation.Attack),"Actual Wake actor source bases/clock/placeholder/owner");
            Require(bodies[0].Prepared.Creation.Skill("Fire Trauma")==15&&bodies[0].Prepared.Creation.Skill("Inferno Sentry")==20
                &&AssassinSentryCreation.Shots(bodies[0].Prepared.Creation)==5,"Actual learned fire synergy capture");
            Services(()=>
            {
                var b=bodies[0];Frames(b,14);Require(b.Prepared.WakeControl is{Frame:14,Mode:8,Thinks:0},"Wake S1 early AI");
                Frames(b,1);Require(b.Prepared.WakeControl is{Frame:15,Mode:1,NextThink:25},"Wake S1 original think lost");
                Frames(b,10);Require(b.Prepared.WakeControl is{Frame:25,Mode:9,ActionAt:26,EndAt:27,Attempts:1,Dispatches:0,Ai.RemainingShots:4},"Wake direct mode first windup");
                ulong seed=b.Prepared.Seed;Frames(b,1);Require(b.Prepared.WakeControl is{Frame:26,Mode:9,Dispatches:1}&&b.Prepared.Seed==seed,"Wake actual direct action phase/RNG");
                Frames(b,1);Require(b.Prepared.WakeControl is{Frame:27,Mode:1,NextThink:42},"Wake direct end handoff");
                Frames(bodies[1],25); // Save an actual pending action.
                var offline=bodies[2];Frames(offline,25);var before=offline.Prepared;
                offline.AdvanceController(offline.Prepared.NextFrameUtc.AddMilliseconds(4000),false);
                Require(offline.Prepared.WakeControl is{Frame:126,Mode:1,Dispatches:0,Ai.RemainingShots:4}
                    &&offline.Prepared.Seed==before.Seed,"Offline Wake replay/refund/RNG/frozen clock");
                var exhausted=bodies[3];Frames(exhausted,111);
                Require(!exhausted.Deleted&&exhausted.Prepared.WakeControl is{Frame:111,Mode:0,Attempts:5,Dispatches:5,Ai.RemainingShots:0}
                    &&exhausted.Prepared.ControllerFault.Contains("retirement"),"Unproved Wake retirement was silently invented");
                ulong finalSeed=exhausted.Prepared.Seed;Frames(exhausted,3);
                Require(exhausted.Prepared.WakeControl.Dispatches==5&&exhausted.Prepared.Seed==finalSeed,"Terminal prepared Wake replayed damage");
            });
            var body=bodies[0];SkillHealth.SetBalance256(body,12850);var map=body.Map;var origin=body.Location;var captured=body.Prepared.Creation;
            SummonDifficulties.SetFacets([new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            p.MoveToWorld(new(p.X,p.Y,p.Z),Map.Felucca);SummonDifficulties.Sync(p);
            Require(body.Map==map&&body.Location==origin&&body.Prepared.Creation.Difficulty==MonsterDifficulty.Hell
                &&body.Prepared.Creation.Skills.SequenceEqual(captured.Skills)&&body.Prepared.Creation.Passives.SequenceEqual(captured.Passives)
                &&SkillHealth.Balance256(body)==12850,"Wake old-facet retention/owner difficulty/fractional injury");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&!AssassinTrapRoster.Fighting(p.Serial.Value),"Prepared factory spent input mana or offline actor can fight");
            Save(p);Require(!AssassinCombat.Implemented(CombatAbility.WakeOfFire),"Unfinished Wake learned handler enabled");
            Log.Information("Wake actor:3711 complete damage comparisons, captured fire skills/stats, actual saved S1/direct S2, mixed cap5, offline cancellation, old-facet difficulty/injury and explicit retirement boundary pass; no live wave claim");
        }
        finally{SummonDifficulties.SetFacets(facets);}
        Unbound();
    }
    private static void Unbound()
    {
        var p=BladeBodyChecks.Fresh(6490,12);p.Name="WakeUnboundFixture";
        try
        {
            var b=Create(p);var selector=AssassinSentryActors.SelectTarget;var dispatch=AssassinSentryActors.DispatchWake;
            AssassinSentryActors.SelectTarget=_=>new(b.Serial.Value,10);AssassinSentryActors.DispatchWake=null;
            try
            {
                Frames(b,25);bool refused=false;
                try{Frames(b,1);}catch(NotSupportedException e){refused=e.Message.Contains("maker/wave");b.SuspendController(e.Message);}
                Require(refused&&b.Prepared.WakeControl.Dispatches==0&&b.Prepared.ControllerFault!=null,"Unbound Wake factory claimed a real dispatch");
                Frames(b,4);Require(b.Prepared.WakeControl.Dispatches==0,"Faulted Wake replayed unbound dispatch");
            }
            finally{AssassinSentryActors.SelectTarget=selector;AssassinSentryActors.DispatchWake=dispatch;}
            p.Kill();AssassinTrapRoster.SweepOwners();Require(b.Deleted&&AssassinTrapRoster.For(p).Length==0,"Owner death retained Wake roster");
        }
        finally{p.Delete();}
    }
    private static void Reload()
    {
        Creation();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(AssassinTrapRoster.For(p).SequenceEqual(saved.Roster),"Wake saved shared roster changed");
        foreach(var old in saved.Bodies)
        {
            var b=World.FindMobile((Serial)old.Serial) as AssassinSentryBody;
            Require(b!=null&&!b.Deleted&&b.SummonMaster==p&&b.Map.MapID==old.State.Map&&b.Location==old.State.Position
                &&JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(old.State)&&SkillHealth.Balance256(b)==old.Life256
                &&b.Prepared.Control==null&&b.Prepared.Creation.Ability==CombatAbility.WakeOfFire,"Actual Wake actor save lost clock/source/owner/position/injury");
        }
        p.MoveToWorld(saved.OwnerPosition,Map.Maps[saved.OwnerMap]);
        Services(()=>
        {
            var b=(AssassinSentryBody)World.FindMobile((Serial)saved.Bodies[1].Serial);int before=b.Prepared.WakeControl.Dispatches;
            for(int i=0;i<20&&b.Prepared.WakeControl.Dispatches==before;i++){Frames(b,1);}
            Require(b.Prepared.WakeControl.Dispatches==before+1,"Reload repeated/lost pending Wake action");
        });
        var oldest=saved.Roster[0];var fresh=Create(p);var roster=AssassinTrapRoster.For(p);
        Require(World.FindMobile((Serial)oldest.Actor)==null&&roster.Length==5&&roster.Take(4).SequenceEqual(saved.Roster.Skip(1))
            &&roster[^1].Actor==fresh.Serial.Value&&roster[^1].Order>saved.Roster[^1].Order,"Reload mixed cap replaced wrong Wake");
        Save(p);Require(!AssassinCombat.Implemented(CombatAbility.WakeOfFire),"Reload enabled learned Wake");
        Log.Information("Wake actor reload:actual entities retain independent S1/S2 clocks, captured fire context, pending/terminal state, injury/facet and shared FIFO; explicit dispatch resumes once");
    }
}
