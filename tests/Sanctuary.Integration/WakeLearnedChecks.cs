// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class WakeLearnedChecks
{
    private const CombatAbility Ability=CombatAbility.WakeOfFire;
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WakeLearnedChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,uint Helm,Point3D Point,int Map,int Mana,int Health,SentryBodyState Actor,WakeFireSave Pool,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"wake-learned-fixtures.json");
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Wake cast and approved per-firing hit windows",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("wake-learned-mechanics-check",Mechanics);
        Register("wake-learned-check",()=>FixtureNetwork.Connected("wake-learned-result",Save));
        Register("wake-learned-reload-check",()=>FixtureNetwork.Connected("wake-learned-reload-result",Reload));
        foreach(string mode in new[]{"left","right","quick"})
        {string label="wake-learned-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>SentryLearnedChecks.Input(n,label,Ability)));}
    }
    private static void Mechanics()
    {
        var p=SentryLearnedChecks.Fresh(6020);try
        {
            var row=SkillMechanics.For(Ability);Require(AssassinCombat.Implemented(Ability)&&ClassCombat.Available(p,Ability)&&AssassinSentryCasts.Supported(p,Ability)
                &&row.SourceId==262&&row.Number("srvdofunc")==45&&row.Number("srvstfunc")==0&&SkillMechanics.Values(p,Ability).Mana256==3328,"Learned Wake source route/cost");
            SentryLearnedChecks.Bind(p,Ability);var timing=CombatTiming.Calculate(DiabloClass.Assassin,Ability,new(AttackFamily.Unarmed,0));
            Require(timing==new ActionTiming(15,8)&&CombatTiming.Calculate(DiabloClass.Assassin,Ability,new(AttackFamily.Unarmed,0),fcr:200)==timing
                &&CombatTiming.Calculate(DiabloClass.Assassin,Ability,new(AttackFamily.Unarmed,0),ias:200)==new ActionTiming(9,5),"Wake must use shared S2 IAS clock,not FCR");
            var aim=new Point3D(p.X+4,p.Y,p.Z);SentryLearnedChecks.Ready(p);NecromancerEffects.SetMana256(p,3327);long next=ClassCombat.For(p).NextAction;
            Require(!ClassCombat.Perform(p,Ability,null,aim,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==3327&&ClassCombat.For(p).NextAction==next,"Unaffordable Wake begin changed action/resources");
            NecromancerEffects.SetMana256(p,3328+127);SentryLearnedChecks.Ready(p);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Wake Stop begin");long effect=ClassCombat.For(p).EffectAt;
            ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&AssassinTrapRoster.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==3455,"Stopped Wake paid/created a trap");
            SentryLearnedChecks.Ready(p);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Wake depleted release begin");NecromancerEffects.SetMana256(p,3327);
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&AssassinTrapRoster.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==3327,"Mana loss created free Wake");
            NecromancerEffects.SetMana256(p,3455);SentryLearnedChecks.Ready(p);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Wake placement revalidation begin");
            var wall=new Static(0x80);wall.MoveToWorld(aim,p.Map);try
            {Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&AssassinTrapRoster.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==3455,"Blocked Wake release paid or invented fallback");}
            finally{wall.Delete();}
            SentryLearnedChecks.Ready(p);var body=SentryLearnedChecks.Cast(p,Ability);
            Require(body.Prepared.Creation.Ability==Ability&&body.Prepared.WakeControl is{Mode:8,Frame:0}&&AssassinSentryCreation.Shots(body.Prepared.Creation)==5
                &&NecromancerEffects.ManaBalance256(p)==127&&FireGround.Ready(p,Ability),"Paid Wake source S1/five shots/no extra delay");
            var first=body.Serial.Value;
            for(int i=0;i<5;i++)
            {
                SentryLearnedChecks.Ready(p);NecromancerEffects.SetMana256(p,3455);
                Require(ClassCombat.Perform(p,Ability,null,new(p.X+5+i,p.Y,p.Z),Core.TickCount)&&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)
                    &&NecromancerEffects.ManaBalance256(p)==127,"Independent Wake cast failed or paid twice");
            }
            Require(AssassinTrapRoster.For(p).Length==5&&!AssassinTrapRoster.For(p).Any(x=>x.Actor==first)&&body is{Alive:false,Deleted:false}
                &&body.Prepared.Retirement is{Cause:"replacement",Profile:AssassinSentryRetirement.InfernoProfile},"Sixth learned Wake lost cap/FIFO/E9 ownership");
            body.Delete();
        }
        finally{SentryLearnedChecks.Clean(p);}
        p=SentryLearnedChecks.Fresh(6020);try
        {
            int mana=NecromancerEffects.ManaBalance256(p);SentryLearnedChecks.Ready(p);
            Require(ClassCombat.Perform(p,Ability,null,new(p.X+4,p.Y,p.Z),Core.TickCount),"Wake movement begin");long effect=ClassCombat.For(p).EffectAt;var point=p.Location;
            p.Direction=Direction.West;Require(p.Move(Direction.West)&&p.Location!=point&&!ClassCombat.ResolveAction(p,effect)
                &&AssassinTrapRoster.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Movement failed Wake cancellation");
        }
        finally{SentryLearnedChecks.Clean(p);}
    }
    private static void Hold(PlayerMobile p,AssassinSentryBody body,BladeMissileTarget target,Item helm,Point3D point,int map,int generation)
    {
        SentryLearnedChecks.Clock(body,TimedEffects.Now.Add(FixtureSpace.Retention));p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,body.Serial.Value,target.Serial.Value,helm.Serial.Value,point,map,
            NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),body.Prepared,WakeFireAutomaticChecks.Scope(body),generation)));
    }
    private static void Save(NetState network)
    {
        var p=SentryLearnedChecks.Fresh(6020);ClassSkillChecks.Train(p,"assassin.wakeoffire",20);ClassSkillChecks.Train(p,"assassin.fireblast",20);ClassSkillChecks.Train(p,"assassin.wakeofinferno",20);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(p.EquipItem(helm),"Actual Wake soft-rank helm");ClassGear.Refresh(p);SentryLearnedChecks.Bind(p,Ability);
        FixtureNetwork.Authenticate(network,p,"wake-learned-");var body=SentryLearnedChecks.Cast(p,Ability);
        var target=new BladeMissileTarget{NoKillAwards=true,CantWalk=true,Frozen=true};target.MoveToWorld(new(body.X+2,body.Y+1,body.Z),body.Map);
        target.AIObject?.AITimer.Stop();target.SetHits(100000);target.Hits=target.HitsMax;target.SetResistance(ResistanceType.Fire,0);int before=SkillHealth.Balance256(target);long end=Core.TickCount+6000;bool hit=false;
        void Observe()
        {
            try
            {
                Require(body is{Alive:true,Deleted:false}&&body.Prepared.ControllerFault==null&&Core.TickCount<end,"Paid Wake failed natural first firing");
                var pool=WakeFireAutomaticChecks.Scope(body);
                if(!hit&&SkillHealth.Balance256(target)<before){hit=true;target.MoveToWorld(new(target.X,target.Y+7,target.Z),target.Map);}
                if(!hit||pool.Leases.Length!=0||!pool.Flights.Any(x=>x.Wave&&x.Frame<x.Frames-6))
                {Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(body.Prepared.Creation is{Ability:Ability,Rank:21}&&body.Prepared.WakeControl.Dispatches==1
                    &&pool.Flights.All(x=>x.Rank==21&&x.Fault==null)&&pool.Flights.Select(x=>x.Execution).Distinct().Count()==1,$"Paid Wake rank={body.Prepared.Creation.Rank},control={Json(body.Prepared.WakeControl)},flights={Json(pool.Flights.Select(x=>new{x.Id,x.Execution,x.Rank,x.Frame,x.Frames,x.Fault}))},leases={pool.Leases.Length}");
                SkillHealth.SetBalance256(body,12850);Hold(p,body,target,helm,p.Location,p.Map.MapID,0);Log.Information("wake-learned-result: passed");
            }
            catch(Exception e){Log.Error(e,"wake-learned-result: failed");}
        }
        Observe();
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(AssassinSentryBody)World.FindMobile((Serial)s.Body);var t=(BladeMissileTarget)World.FindMobile((Serial)s.Target);var helm=World.FindItem((Serial)s.Helm);var pool=WakeFireAutomaticChecks.Scope(b);
        Require(s.Generation<2&&helm?.Parent==p&&Json(b.Prepared)==Json(s.Actor)&&Json(pool.Flights)==Json(s.Pool.Flights)&&Json(pool.Leases)==Json(s.Pool.Leases)
            &&SkillHealth.Balance256(b)==12850&&SkillHealth.Balance256(t)==s.Health&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&SkillBindings.Instance.Get(p).Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==Ability,"Original learned Wake actor/fire/gear/wounds/mana/bindings changed on reload");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);FixtureNetwork.Authenticate(network,p,"wake-learned-resume-");SentryLearnedChecks.Clock(b,TimedEffects.Now.AddMilliseconds(40));long end=Core.TickCount+3000;
        void Observe()
        {
            try
            {
                var current=WakeFireAutomaticChecks.Scope(b);Require(b.Prepared.ControllerFault==null&&current.Flights.All(x=>x.Fault==null)&&Core.TickCount<end,"Original Wake resume fault/timeout");
                if(b.Prepared.WakeControl.Frame<s.Actor.WakeControl.Frame+2||!s.Pool.Flights.All(old=>current.Flights.Any(x=>x.Id==old.Id&&x.Frame>=old.Frame+2)))
                {Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                var originalGroups=s.Pool.Flights.Select(x=>x.Execution).ToHashSet();int newFirings=current.Flights.Select(x=>x.Execution).Distinct().Count(x=>!originalGroups.Contains(x));
                Require(b.Prepared.WakeControl.Dispatches-s.Actor.WakeControl.Dispatches==newFirings&&NecromancerEffects.ManaBalance256(p)==s.Mana
                    &&b.Prepared.WakeControl.Ai.RemainingShots==5-b.Prepared.WakeControl.Attempts
                    &&s.Pool.Flights.All(old=>current.Flights.Any(x=>x.Id==old.Id&&x.Execution==old.Execution&&x.Actor==old.Actor&&x.Owner==old.Owner&&x.Minimum256==old.Minimum256&&x.Maximum256==old.Maximum256)),"Wake resumed by duplicating firings,replacing original fire,corrupting shot budget or repaying");
                Hold(p,b,t,helm,s.Point,s.Map,s.Generation+1);Log.Information("wake-learned-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"wake-learned-reload-result: failed");}
        }
        Observe();
    }
}
