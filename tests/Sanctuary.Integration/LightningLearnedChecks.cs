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

public static class LightningLearnedChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(LightningLearnedChecks));
    private const CombatAbility Ability=CombatAbility.LightningSentry;
    private sealed record Saved(uint Owner,uint Body,uint Target,uint Helm,Point3D Origin,int Map,int Mana,int Health,SentryBodyState Actor,SentryLightningState Bolt,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lightning-learned-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Lightning Sentry casting, retired source and saved original penetrating bolt",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("lightning-learned-mechanics-check",Mechanics);
        Register("lightning-learned-check",()=>FixtureNetwork.Connected("lightning-learned-result",Save));
        Register("lightning-learned-reload-check",()=>FixtureNetwork.Connected("lightning-learned-reload-result",Reload));
        foreach(string mode in new[]{"left","right","quick"}){string label="lightning-learned-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>SentryLearnedChecks.Input(n,label,Ability)));}
    }
    private static BladeMissileTarget Target(AssassinSentryBody body,int dx=2)
    {
        var t=new BladeMissileTarget{NoKillAwards=true,CantWalk=true,Frozen=true};t.MoveToWorld(new(body.X+dx,body.Y,body.Z),body.Map);t.AIObject?.AITimer.Stop();
        t.SetHits(100000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Energy,0);return t;
    }
    private static void AtomicRestore()
    {
        var before=AssassinSentryLightning.Snapshot();string json=JsonSerializer.Serialize(before);var row=before.Bolts.Last();
        foreach(var bad in new[]{before with{Bolts=before.Bolts.Where(x=>x.Id!=row.Id).Append(row with{Frame=row.Frames}).ToArray()},before with{Bolts=before.Bolts.Append(row).ToArray()},before with{Bolts=before.Bolts.Prepend(null).ToArray()}})
        {bool refused=false;try{AssassinSentryLightning.Restore(bad);}catch(InvalidDataException){refused=true;}Require(refused&&JsonSerializer.Serialize(AssassinSentryLightning.Snapshot())==json,"Straight-flight restore changed live pool/next-id on malformed tail");}
    }
    private static void Mechanics()
    {
        var p=SentryLearnedChecks.Fresh();AssassinSentryBody body=null;BladeMissileTarget first=null,second=null;
        try
        {
            Require(ClassCombat.Available(p,Ability)&&AssassinSentryCasts.Supported(p,Ability)&&SkillMechanics.Values(p,Ability).Mana256==5120,"Pinned learned Lightning20mana/context");
            SentryLearnedChecks.Bind(p,Ability);var timing=CombatTiming.Calculate(DiabloClass.Assassin,Ability,new(AttackFamily.Unarmed,0));
            Require(timing==new ActionTiming(15,8)&&CombatTiming.Calculate(DiabloClass.Assassin,Ability,new(AttackFamily.Unarmed,0),fcr:200)==timing
                &&CombatTiming.Calculate(DiabloClass.Assassin,Ability,new(AttackFamily.Unarmed,0),ias:200)==new ActionTiming(9,5),"Lightning S2 IAS/no FCR");
            var aim=new Point3D(p.X+4,p.Y,p.Z);NecromancerEffects.SetMana256(p,5119);SentryLearnedChecks.Ready(p);long next=ClassCombat.For(p).NextAction;
            Require(!ClassCombat.Perform(p,Ability,null,aim,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==5119&&ClassCombat.For(p).NextAction==next,"Low-mana Lightning begin changed state");
            NecromancerEffects.SetMana256(p,5120);SentryLearnedChecks.Ready(p);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Lightning stop begin");var session=ClassCombat.For(p);long effect=session.EffectAt;next=session.NextAction;
            ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==5120&&AssassinTrapRoster.For(p).Length==0&&session.NextAction==next,"Lightning stop paid/created/refunded cycle");
            SentryLearnedChecks.Ready(p);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Lightning mana-loss begin");NecromancerEffects.SetMana256(p,5119);
            Require(!ClassCombat.ResolveAction(p,session.EffectAt)&&NecromancerEffects.ManaBalance256(p)==5119&&AssassinTrapRoster.For(p).Length==0,"Mana-loss created free Lightning trap");
            NecromancerEffects.SetMana256(p,5120);body=SentryLearnedChecks.Cast(p,Ability);
            Require(body.Prepared.Creation.Ability==Ability&&body.Prepared.Control is{Mode:8,Frame:0}&&FireGround.Ready(p,Ability),"Lightning actor/no independent delay");
            first=Target(body);long id=AssassinSentryLightning.Create(body,new((body.X+10)*2,body.Y*2));AtomicRestore();
            for(int i=0;i<5;i++){AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now.AddHours(1),SourceRandom.InitLow(19),Ability);}
            Require(!body.Alive&&!body.Deleted&&body.Prepared.Retirement is{Cause:"replacement"}&&AssassinTrapRoster.For(p).Length==5,"Sixth Lightning trap deleted original source/lost cap");
            int hp=SkillHealth.Balance256(first);
            for(int i=0;i<5&&AssassinSentryLightning.State(id) is{} bolt;i++){AssassinSentryLightning.TickFrame(bolt.NextUtc,true);}
            Require(SkillHealth.Balance256(first)<hp&&AssassinSentryLightning.State(id) is{LastCollision:>0},"Dying Lightning source could not damage");
            body.AdvanceController(body.Prepared.Retirement.CompletesUtc,true);Require(body.RetainedCorpse&&!AssassinSentryActors.Actors().Contains(body),"Lightning completed source restarted AI");
            first.Blessed=true;var state=AssassinSentryLightning.State(id);second=Target(body);second.MoveToWorld(new((int)Math.Round(state.X+state.Vx),(int)Math.Round(state.Y+state.Vy),state.Z),body.Map);second.NoKillAwards=false;second.SetHits(1);second.Hits=1;
            AssassinSentryLightning.TickFrame(state.NextUtc,true);
            Require(!second.Alive&&second.LastKiller==body&&ClassLoot.Killer(second)==p,"Completed original Lightning source lost lethal damage/credit");
        }
        finally{first?.Delete();second?.Delete();body?.Delete();SentryLearnedChecks.Clean(p);}
        p=SentryLearnedChecks.Fresh();try
        {
            SentryLearnedChecks.Ready(p);var origin=p.Location;int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,Ability,null,new(p.X+4,p.Y,p.Z),Core.TickCount),"Lightning movement begin");long effect=ClassCombat.For(p).EffectAt;
            p.Direction=Direction.West;Require(p.Move(Direction.West)&&p.Location!=origin,"Accepted Lightning cancellation step");
            Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinTrapRoster.For(p).Length==0,"Movement paid/created Lightning trap");
        }
        finally{SentryLearnedChecks.Clean(p);}
    }
    private static void Hold(PlayerMobile p,AssassinSentryBody body,BladeMissileTarget target,Item helm,long id,Point3D origin,int map,int generation)
    {
        SentryLearnedChecks.Clock(body,TimedEffects.Now.Add(FixtureSpace.Retention));p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,target.Serial.Value,helm.Serial.Value,origin,map,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),body.Prepared,AssassinSentryLightning.State(id),generation)));
    }
    private static void Save(NetState network)
    {
        var p=SentryLearnedChecks.Fresh();while(ClassCreation.Profiles.GetRank(p.Serial,"assassin.lightningsentry")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"assassin.lightningsentry",out _),"Train Lightning Sentry");}
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(p.EquipItem(helm),"Actual Lightning soft-rank helm");ClassGear.Refresh(p);SentryLearnedChecks.Bind(p,Ability);
        FixtureNetwork.Authenticate(network,p,"lightning-learned-");var body=SentryLearnedChecks.Cast(p,Ability);var target=Target(body);int hp=SkillHealth.Balance256(target);long end=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                Require(body is{Alive:true,Deleted:false}&&body.Prepared.ControllerFault==null,"Paid Lightning actor fault/death");var bolt=AssassinSentryLightning.Snapshot().Bolts.SingleOrDefault(x=>x.Actor==body.Serial.Value);
                if(SkillHealth.Balance256(target)==hp||bolt is not{LastCollision:>0}){Require(Core.TickCount<end,"Paid Lightning trap did not naturally emit/damage");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(body.Prepared.Creation is{Ability:Ability,Rank:21}&&body.Prepared.Control.Dispatches==1&&bolt.Rank==21&&bolt.Fault==null,"Paid rank/one straight bolt/first volley");
                target.MoveToWorld(new(target.X,target.Y+6,target.Z),target.Map);SkillHealth.SetBalance256(body,12850);
                Hold(p,body,target,helm,bolt.Id,p.Location,p.Map.MapID,0);Log.Information("lightning-learned-result: passed");
            }
            catch(Exception e){Log.Error(e,"lightning-learned-result: failed");}
        }
        Observe();
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(AssassinSentryBody)World.FindMobile((Serial)saved.Body);var target=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);var helm=World.FindItem((Serial)saved.Helm);
        Require(saved.Generation<2&&helm?.Parent==p&&JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.Actor)
            &&JsonSerializer.Serialize(AssassinSentryLightning.State(saved.Bolt.Id))==JsonSerializer.Serialize(saved.Bolt)&&SkillHealth.Balance256(body)==12850
            &&SkillHealth.Balance256(target)==saved.Health&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Original Lightning actor/bolt/history/gear/wounds/mana changed");
        Require(SkillBindings.Instance.Get(p).Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==Ability,"Saved Lightning right/quick bindings");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);FixtureNetwork.Authenticate(network,p,"lightning-learned-resume-");SentryLearnedChecks.Clock(body,TimedEffects.Now.AddMilliseconds(40));long end=Core.TickCount+3000;
        void Observe()
        {
            try
            {
                var bolt=AssassinSentryLightning.State(saved.Bolt.Id);Require(bolt!=null&&bolt.Fault==null&&body.Prepared.ControllerFault==null,"Original Lightning resume fault");
                if(body.Prepared.Control.Frame<saved.Actor.Control.Frame+2||bolt.Frame<saved.Bolt.Frame+2){Require(Core.TickCount<end,"Original Lightning controller/flight failed natural resume");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(bolt.Actor==body.Serial.Value&&bolt.Owner==p.Serial.Value&&bolt.Minimum256==saved.Bolt.Minimum256&&bolt.Maximum256==saved.Bolt.Maximum256
                    &&bolt.LastCollision==saved.Bolt.LastCollision&&body.Prepared.Control.Dispatches==saved.Actor.Control.Dispatches&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Lightning resume lost source/history/packet or recast/repaid");
                Hold(p,body,target,helm,bolt.Id,saved.Origin,saved.Map,saved.Generation+1);Log.Information("lightning-learned-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"lightning-learned-reload-result: failed");}
        }
        Observe();
    }
}
