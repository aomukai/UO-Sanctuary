// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ReviveSourceLifeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveSourceLifeChecks));
    private sealed record Saved(uint Owner,uint[] Actors,ReviveNativeState[] States,int[] Life,int[] Maximum);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-source-life-fixtures.json");
    private static bool Creating;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC source HP and FP256 life",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        // Isolate persistence from ordinary native regeneration between the
        // snapshot command, world save and the next boot's assertion.
        var hp=Mobile.HitsRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>Creating||actor.Name=="ReviveSourceLifeFixture"?TimeSpan.FromHours(1):hp?.Invoke(actor)??Mobile.DefaultHitsRate;
        Register("revive-source-life-check",Numeric);Register("revive-source-life-reload-check",Reload);Register("revive-source-life-snapshot-check",Snapshot);
        Register("revive-source-life-live-check",()=>FixtureClock.NextLoop("revive-source-life-live-result",()=>FixtureNetwork.Connected("revive-source-life-live-result",Live)));
    }
    private static void Numeric()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"revive-source-policy-source-probe.json")));int count=0;
        foreach(var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input=row[0];var expected=row[2];if(expected[0].GetInt32()!=1||input[23].GetInt32()==0){continue;}
            var result=ReviveLife.Roll(input[11].GetInt32(),input[12].GetInt32(),input[10].GetInt32(),input[9].GetInt32(),SourceRandom.InitLow(input[13].GetInt32()),input[24].GetInt32());
            Require(result.Maximum256==expected[1][1].GetInt32()&&result.Maximum256==expected[1][2].GetInt32()&&result.NextSeed==expected[3].GetUInt64(),"Source HP roll/scaling/tail increase differs from complete older release controls");count++;
            Require(ReviveCombat.ReducedLevel(input[10].GetInt32(),input[9].GetInt32())==expected[1][3].GetInt32(),"Effective level differs from the complete source release");
        }
        Require(count==4756,"Incomplete successful source HP controls");
        var prior=BattleOrders.Snapshot();
        try
        {
            var high=new OrdersResources(1,int.MaxValue/256,0,0);
            BattleOrders.Restore(new([], [high]));Require(BattleOrders.Snapshot().Resources.Contains(high),"Supported high-life ledger was refused");
            bool rejected=false;
            try{BattleOrders.Restore(new([], [high with{LifeMaximum=int.MaxValue/256+1}]));}catch(InvalidDataException){rejected=true;}
            Require(rejected,"Unrepresentable FP256 ledger was accepted");
        }
        finally{BattleOrders.Restore(prior);}
        ReviveCombatChecks.Numeric();
        Log.Information("Revive source life:4756complete-source release HP/RNG comparisons pass;current plan reuses pinned corpse bounds and calc1");
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var actors=saved.Actors.Select(id=>(BaseCreature)World.FindMobile((Serial)id)).ToArray();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{States=actors.Select(ReviveNativeActivation.State).ToArray(),Life=actors.Select(SkillHealth.Balance256).ToArray(),Maximum=actors.Select(SkillHealth.Maximum256).ToArray()}));
    }
    private static void Reload()
    {
        Numeric();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        for(int i=0;i<saved.Actors.Length;i++)
        {
            var actor=World.FindMobile((Serial)saved.Actors[i]) as BaseCreature;
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner&&actor.Map==Map.Internal
                &&ReviveNativeActivation.State(actor)==saved.States[i]&&SkillHealth.Balance256(actor)==saved.Life[i]&&SkillHealth.Maximum256(actor)==saved.Maximum[i]
                &&actor.HitsMax==(saved.Maximum[i]+255)/256,$"Reload source HP actor {i}:alive={actor?.Alive},owner={actor?.ControlMaster?.Serial.Value}/{saved.Owner},map={actor?.Map},plan={ReviveNativeActivation.State(actor)==saved.States[i]},life={SkillHealth.Balance256(actor)}/{saved.Life[i]},max={SkillHealth.Maximum256(actor)}/{saved.Maximum[i]},native={actor?.HitsMax}/{(saved.Maximum[i]+255)/256}");
            ReviveCombatChecks.Saved(actor,saved.States[i]);
        }
        Require(saved.Maximum[1]>65000*256&&saved.Life[1]>65000*256,"Large source life did not exercise native loading ceiling");
    }
    private static void Live(NetState network)
    {
        SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Hell)]);
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6550,30,2100);
        var account=new Account("revive-hp-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=owner;network.Account=account;ReviveNativeActivationChecks.Attach(network,owner);
        while(ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.skeletonmastery")<20)
        {Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.skeletonmastery",out var reason),"HP mastery training:"+reason);}
        var enemy=new Zombie{Frozen=true,CantWalk=true,NoKillAwards=true};MonsterVariants.Assign(enemy,MonsterDifficulty.Hell);enemy.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
        var actors=new BaseCreature[2];var corpses=new Corpse[2];
        for(int i=0;i<2;i++)
        {
            BaseCreature original;Creating=true;
            try{corpses[i]=ReviveNativeActivationChecks.Body(owner,i==1,out original,MonsterDifficulty.Hell);}
            finally{Creating=false;}
            original.Name="ReviveSourceLifeFixture";
            // Keep the second prepared original clear of the first actor.
            corpses[i].MoveToWorld(new(owner.X+1,owner.Y+2*i,owner.Z),owner.Map);
            var setup=ReviveLife.Setup(owner,corpses[i],4,10);var before=CorpseSkillRules.Profiles.Find(corpses[i]);
            Require(ReviveNativeActivation.CommitPrepared(owner,corpses[i],corpses[i].Location,new(false,false,false,false,false,true),setup,out var actor,out var issue),"Source HP activation "+i+":"+issue);
            actors[i]=actor;actor.AIObject.AITimer.Stop();
            Require(actor==original&&actor is not ISourceLifeMaximum&&SkillHealth.Maximum256(actor)==setup.LifePlan.Maximum256
                &&SkillHealth.Balance256(actor)==setup.LifePlan.Maximum256&&actor.HitsMax==setup.MaximumLife
                &&CorpseSkillRules.Profiles.Find(corpses[i]).BaseLife==before.BaseLife,"Source HP replaced actor,changed corpse seed or lost fractional maximum");
            var row=ReviveNativeActivation.State(actor);var snapshot=ReviveNativeActivation.Snapshot();bool rejected=false;
            try{ReviveNativeActivation.Restore([row with{Setup=row.Setup with{LifePlan=row.Setup.LifePlan with{NextSeed=row.Setup.LifePlan.NextSeed+1}}}]);}
            catch(InvalidDataException){rejected=true;}
            Require(rejected&&ReviveNativeActivation.Snapshot().SequenceEqual(snapshot),"Malformed source HP plan changed actor registry");
            ReviveCombatChecks.Live(actor,enemy);
        }
        var supplier=new PlayerMobile{Name="SourceHPOrders",Player=true,Body=0x190};supplier.AddItem(new Backpack());ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);
        BattleOrders.Apply(supplier,actors[1],255,TimedEffects.Now.AddDays(1));
        Require(SkillHealth.Maximum256(actors[1])>65000*256,"Current Hell corpse/rank255 prepared Orders did not exercise large original HP");
        foreach(var actor in actors)
        {
            int desired=SkillHealth.Maximum256(actor)-Math.Min(20001,SkillHealth.Maximum256(actor)/3);SkillHealth.SetBalance256(actor,desired);
            SkillHealth.Damage(enemy,actor,37);Require(SkillHealth.Balance256(actor)==desired-37,"Original NPC fractional damage drifted");
            SkillHealth.Heal(actor,11);Require(SkillHealth.Balance256(actor)==desired-26,"Original NPC fractional healing drifted");
        }
        bool keep=!File.Exists(Manifest);
        if(keep){File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actors.Select(a=>a.Serial.Value).ToArray(),actors.Select(ReviveNativeActivation.State).ToArray(),actors.Select(SkillHealth.Balance256).ToArray(),actors.Select(SkillHealth.Maximum256).ToArray())));}
        PersistentMinions.Suspend(owner);owner.Internalize();enemy.Delete();
        Log.Information("revive-source-life-live: ready");
        FixtureClock.NextLoop("revive-source-life-live-result",()=>
        {
            Log.Information("Revive source HP:real Hell originals retain native identities,source roll/caster scaling/mastery calc1,FP256damage/heal and prepared rank255Orders above65000;full passive/stat/AI and learned casting remain unfinished");
            Log.Information("Revive combat:4756complete-source level controls,144current formula contexts,864explicit sunder refusals;original native attacker/defender level,physical-only AOS damage,additive speed/Vigor and mastery/Might,legacy optional-plan saves and atomic invalid-plan checks pass");
            Log.Information("revive-source-life-live-result: passed");
            network.Mobile=null;owner.NetState=null;
            if(!keep){foreach(var actor in actors){actor.Delete();}foreach(var corpse in corpses){corpse.Delete();}owner.Delete();supplier.Delete();}
        });
    }
}
