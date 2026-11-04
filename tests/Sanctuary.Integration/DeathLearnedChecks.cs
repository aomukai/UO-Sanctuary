// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DeathLearnedChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathLearnedChecks));
    private const CombatAbility Ability=CombatAbility.DeathSentry;
    private sealed record Saved(uint Owner,uint Body,uint Target,uint Helm,uint Corpse,CorpseSkillRecord CorpseRow,Point3D Origin,int Map,int Mana,int Health,SentryBodyState Actor,SentryLightningState Bolt,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-learned-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Lightning Sentry casting, retired source and saved original penetrating bolt",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("death-learned-mechanics-check",Mechanics);
        Register("death-learned-check",()=>FixtureNetwork.Connected("death-learned-result",Save));
        Register("death-learned-reload-check",()=>FixtureNetwork.Connected("death-learned-reload-result",Reload));
        foreach(string mode in new[]{"left","right","quick"}){string label="death-learned-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>SentryLearnedChecks.Input(n,label,Ability)));}
    }
    private static BladeMissileTarget Target(AssassinSentryBody body,int dx=2)
    {
        var t=new BladeMissileTarget{NoKillAwards=true,CantWalk=true,Frozen=true};t.MoveToWorld(new(body.X+dx,body.Y,body.Z),body.Map);t.AIObject?.AITimer.Stop();
        t.SetHits(100000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Energy,0);return t;
    }
    private static void Mechanics()
    {
        var p=SentryLearnedChecks.Fresh(7000);AssassinSentryBody body=null;BladeMissileTarget first=null,second=null;
        try
        {
            Require(ClassCombat.Available(p,Ability)&&AssassinSentryCasts.Supported(p,Ability)&&SkillMechanics.Values(p,Ability).Mana256==5120,"Pinned learned Death20mana/context");
            SentryLearnedChecks.Bind(p,Ability);var timing=CombatTiming.Calculate(DiabloClass.Assassin,Ability,new(AttackFamily.Unarmed,0));
            Require(timing==new ActionTiming(15,8)&&CombatTiming.Calculate(DiabloClass.Assassin,Ability,new(AttackFamily.Unarmed,0),fcr:200)==timing
                &&CombatTiming.Calculate(DiabloClass.Assassin,Ability,new(AttackFamily.Unarmed,0),ias:200)==new ActionTiming(9,5),"Death S2 IAS/no FCR");
            var aim=new Point3D(p.X+4,p.Y,p.Z);NecromancerEffects.SetMana256(p,5119);SentryLearnedChecks.Ready(p);long next=ClassCombat.For(p).NextAction;
            Require(!ClassCombat.Perform(p,Ability,null,aim,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==5119&&ClassCombat.For(p).NextAction==next,"Low-mana Lightning begin changed state");
            NecromancerEffects.SetMana256(p,5120);SentryLearnedChecks.Ready(p);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Death stop begin");var session=ClassCombat.For(p);long effect=session.EffectAt;next=session.NextAction;
            ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==5120&&AssassinTrapRoster.For(p).Length==0&&session.NextAction==next,"Death stop paid/created/refunded cycle");
            SentryLearnedChecks.Ready(p);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Death mana-loss begin");NecromancerEffects.SetMana256(p,5119);
            Require(!ClassCombat.ResolveAction(p,session.EffectAt)&&NecromancerEffects.ManaBalance256(p)==5119&&AssassinTrapRoster.For(p).Length==0,"Mana-loss created free Lightning trap");
            NecromancerEffects.SetMana256(p,5120);body=SentryLearnedChecks.Cast(p,Ability);
            Require(body.Prepared.Creation.Ability==Ability&&body.Prepared.DeathControl is{Mode:8,Frame:0}&&FireGround.Ready(p,Ability),"Death actor/no independent delay");
            first=Target(body);long id=AssassinSentryLightning.DispatchDeath(body,first.Serial.Value);LightningLearnedChecks.AtomicRestore();
            for(int i=0;i<5;i++){AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now.AddHours(1),SourceRandom.InitLow(19),Ability);}
            Require(!body.Alive&&!body.Deleted&&body.Prepared.Retirement is{Cause:"replacement"}&&AssassinTrapRoster.For(p).Length==5,"Sixth Death trap deleted original source/lost cap");
            int hp=SkillHealth.Balance256(first);
            for(int i=0;i<5&&AssassinSentryLightning.State(id) is{} bolt;i++){AssassinSentryLightning.TickFrame(bolt.NextUtc,true);}
            Require(SkillHealth.Balance256(first)<hp&&AssassinSentryLightning.State(id) is{LastCollision:>0},"Dying Lightning source could not damage");
            body.AdvanceController(body.Prepared.Retirement.CompletesUtc,true);Require(body.RetainedCorpse&&!AssassinSentryActors.Actors().Contains(body),"Death completed source restarted AI");
            first.Blessed=true;var state=AssassinSentryLightning.State(id);second=Target(body);second.MoveToWorld(new((int)Math.Round(state.X+state.Vx),(int)Math.Round(state.Y+state.Vy),state.Z),body.Map);second.NoKillAwards=false;second.SetHits(1);second.Hits=1;
            AssassinSentryLightning.TickFrame(state.NextUtc,true);
            Require(!second.Alive&&second.LastKiller==body&&ClassLoot.Killer(second)==p,"Completed original Death source lost lethal damage/credit");
        }
        finally{first?.Delete();second?.Delete();body?.Delete();SentryLearnedChecks.Clean(p);}
        p=SentryLearnedChecks.Fresh(7000);try
        {
            SentryLearnedChecks.Ready(p);var origin=p.Location;int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,Ability,null,new(p.X+4,p.Y,p.Z),Core.TickCount),"Death movement begin");long effect=ClassCombat.For(p).EffectAt;
            p.Direction=Direction.West;Require(p.Move(Direction.West)&&p.Location!=origin,"Accepted Lightning cancellation step");
            Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinTrapRoster.For(p).Length==0,"Movement paid/created Lightning trap");
        }
        finally{SentryLearnedChecks.Clean(p);}
    }
    private static void Hold(PlayerMobile p,AssassinSentryBody body,BladeMissileTarget target,Item helm,Server.Items.Corpse corpse,long id,Point3D origin,int map,int generation)
    {
        SentryLearnedChecks.Clock(body,TimedEffects.Now.Add(FixtureSpace.Retention));p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,target.Serial.Value,helm.Serial.Value,corpse.Serial.Value,CorpseSkillRules.Profiles.Find(corpse),origin,map,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),body.Prepared,AssassinSentryLightning.State(id),generation)));
    }
    private static void Save(NetState network)
    {
        var p=SentryLearnedChecks.Fresh(7000);while(ClassCreation.Profiles.GetRank(p.Serial,"assassin.deathsentry")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"assassin.deathsentry",out _),"Train Death Sentry");}
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(p.EquipItem(helm),"Actual Death soft-rank helm");ClassGear.Refresh(p);SentryLearnedChecks.Bind(p,Ability);
        var corpse=DeathSentryExplosionChecks.Body(p,new(p.X+5,p.Y,p.Z));var row=CorpseSkillRules.Profiles.Find(corpse);var target=DeathSentryExplosionChecks.Target(corpse,1);target.Frozen=target.CantWalk=true;target.SetResistance(ResistanceType.Energy,0);
        int hp=SkillHealth.Balance256(target);FixtureNetwork.Authenticate(network,p,"death-learned-");
        int low=0;for(;low<1000;low++){ulong trial=SourceRandom.InitLow(low);if(SourceRandom.Limited(ref trial,100)<50){break;}}Require(low<1000,"Deterministic first fallback chance");
        var supplier=BladeSentinelSummons.SeedLow;AssassinSentryBody body;try{BladeSentinelSummons.SeedLow=()=>low;body=SentryLearnedChecks.Cast(p,Ability);}finally{BladeSentinelSummons.SeedLow=supplier;}
        var selection=AssassinSentryWorldTargets.Select(body);var selectedCorpse=AssassinDeathSentryCorpses.Select(body,p,target,21);
        Require(selection?.Serial==target.Serial.Value&&selectedCorpse.Corpse==corpse,
            $"Hybrid scene unavailable: target={selection}, corpse={selectedCorpse.Corpse?.Serial}, wanted={corpse.Serial}, usable={CorpseSkillRules.UsableAt(p,corpse,body,double.PositiveInfinity)}, wantedTarget={target.Serial}, base={JsonSerializer.Serialize(CorpseSkillRules.Profiles.Find(corpse)?.BaseLife)}");
        int paidMana=NecromancerEffects.ManaBalance256(p);long end=Core.TickCount+6000;bool exploded=false;
        void Observe()
        {
            try
            {
                Require(body is{Alive:true,Deleted:false}&&body.Prepared.ControllerFault==null,"Paid Death actor fault/death");
                if(corpse.SanctuaryConsumed)
                {
                    var current=CorpseSkillRules.Profiles.Find(corpse);
                    Require(current is{NoSelect:true}&&current.BaseLife.OriginalMonsterSerial==row.BaseLife.OriginalMonsterSerial
                        &&current.BaseLife.Seed!=row.BaseLife.Seed&&corpse.Animated&&corpse.GetAmount(typeof(Gold))==1
                        &&body.Prepared.DeathControl.Ai.LastCorpse==(int)row.BaseLife.OriginalMonsterSerial&&SkillHealth.Balance256(target)<hp,"Actual automatic corpse consumption/seed/remembered identity/damage/loot");exploded=true;
                }
                var bolt=AssassinSentryLightning.Snapshot().Bolts.FirstOrDefault(x=>x.Actor==body.Serial.Value&&x.Owner==p.Serial.Value&&x.Ability==Ability&&x.LastCollision==target.Serial.Value&&x.Frame<x.Frames-6);
                if(!exploded||bolt is not{LastCollision:>0}){Require(Core.TickCount<end,"Paid Death trap did not explode then emit damaging lightning");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(body.Prepared.Creation is{Ability:Ability,Rank:21}&&body.Prepared.DeathControl is{Attempts:2,Dispatches:2,Ai.RemainingShots:3}
                    &&AssassinSentryCreation.Shots(body.Prepared.Creation)==5&&bolt.Ability==Ability&&NecromancerEffects.ManaBalance256(p)==paidMana,"Hybrid shared budget/rank or repeated player payment");
                target.MoveToWorld(new(target.X,target.Y+6,target.Z),target.Map);SkillHealth.SetBalance256(body,12850);
                Hold(p,body,target,helm,corpse,bolt.Id,p.Location,p.Map.MapID,0);Log.Information("death-learned-result: passed");
            }
            catch(Exception e){Log.Error(e,"death-learned-result: failed; body={Body}; corpse={Corpse}; row={Row}; flights={Flights}",JsonSerializer.Serialize(body.Prepared),corpse.Serial,JsonSerializer.Serialize(CorpseSkillRules.Profiles.Find(corpse)),JsonSerializer.Serialize(AssassinSentryLightning.Snapshot().Bolts.Where(x=>x.Actor==body.Serial.Value).Select(x=>new{x.Id,x.Actor,x.Owner,x.Ability,x.Frame,x.LastCollision,x.NextUtc})));}
        }
        Observe();
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(AssassinSentryBody)World.FindMobile((Serial)saved.Body);var target=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);var helm=World.FindItem((Serial)saved.Helm);var corpse=(Server.Items.Corpse)World.FindItem((Serial)saved.Corpse);
        Require(saved.Generation<2&&helm?.Parent==p&&JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.Actor)
            &&JsonSerializer.Serialize(AssassinSentryLightning.State(saved.Bolt.Id))==JsonSerializer.Serialize(saved.Bolt)&&SkillHealth.Balance256(body)==12850
            &&SkillHealth.Balance256(target)==saved.Health&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Original Death actor/bolt/history/gear/wounds/mana changed");
        Require(corpse is{Deleted:false,SanctuaryConsumed:true,Animated:true}&&corpse.GetAmount(typeof(Gold))==1&&CorpseSkillRules.Profiles.Find(corpse)==saved.CorpseRow
            &&AssassinDeathSentryCorpses.Explode(body,corpse).Status==DeathExplosionStatus.NotEligible,"Consumed source corpse/loot/seed changed or fired twice");
        Require(SkillBindings.Instance.Get(p).Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==Ability,"Saved Death right/quick bindings");
        target.Frozen=target.CantWalk=true;target.AIObject?.AITimer.Stop(); // This persistence fixture requires a stationary aim inside its controlled corridor.
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);FixtureNetwork.Authenticate(network,p,"death-learned-resume-");SentryLearnedChecks.Clock(body,TimedEffects.Now.AddMilliseconds(40));long end=Core.TickCount+3000;
        void Observe()
        {
            try
            {
                var bolt=AssassinSentryLightning.State(saved.Bolt.Id);Require(bolt!=null&&bolt.Fault==null&&body.Prepared.ControllerFault==null,$"Original Death resume fault: bolt={JsonSerializer.Serialize(bolt)}, actor={JsonSerializer.Serialize(body.Prepared)}, target={target.Location}, generation={saved.Generation}");
                if(body.Prepared.DeathControl.Frame<saved.Actor.DeathControl.Frame+2||bolt.Frame<saved.Bolt.Frame+2){Require(Core.TickCount<end,"Original Death controller/flight failed natural resume");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(bolt.Actor==body.Serial.Value&&bolt.Owner==p.Serial.Value&&bolt.Minimum256==saved.Bolt.Minimum256&&bolt.Maximum256==saved.Bolt.Maximum256
                    &&bolt.LastCollision==saved.Bolt.LastCollision&&body.Prepared.DeathControl.Dispatches==saved.Actor.DeathControl.Dispatches&&body.Prepared.DeathControl.Ai==saved.Actor.DeathControl.Ai
                    &&CorpseSkillRules.Profiles.Find(corpse)==saved.CorpseRow&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Death resume lost source/budget/history or replayed corpse/payment");
                Hold(p,body,target,helm,corpse,bolt.Id,saved.Origin,saved.Map,saved.Generation+1);Log.Information("death-learned-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"death-learned-reload-result: failed");}
        }
        Observe();
    }
}
