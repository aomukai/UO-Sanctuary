// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class BlindWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BlindWorldChecks));
    private sealed record Saved(uint Owner,uint Target,int Map,Point3D OwnerAt,Point3D TargetAt,NecromancerCurse Curse,BlindAiState Ai,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blind-world-fixtures.json");
    public static void Configure()
    {
        Register("blind-world-check",Check);Register("blind-world-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("blind-world-live","Blind live clock candidate",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("blind-world-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Blind saved/native controller candidate",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureBlindWorld";
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.dimvision",out _),"Train blind");
        FixtureSpace.Corridor(p,7000,32,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int offset=4)
    {
        var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);
        t.Frozen=false;t.CantWalk=false;t.Combatant=null;t.AIObject.Action=ActionType.Wander;
        return t;
    }
    private static BlindAiState State(BaseCreature actor)=>NecromancerEffects.Profiles.Find(actor)?.BlindAi;
    private static void State(BaseCreature actor,BlindAiState ai)=>NecromancerEffects.Profiles.Store(actor,NecromancerEffects.Profiles.Find(actor) with{BlindAi=ai});
    private static ulong Seed(bool wander)
    {
        for(int low=1;low<10000;low++)
        {
            ulong original=SourceRandom.InitLow(low),s=original;bool walk=SourceRandom.Roll(ref s)%100<20;
            if(!wander&&!walk){return original;}if(!wander||!walk){continue;}
            uint Draw()=>SourceRandom.Roll(ref s);
            var p=BlindAiPolicy.Offset(3,Draw,bound=>(int)(Draw()%(uint)bound));
            if(p.X==3&&p.Y>=0){return original;} // Two native steps for saved-goal inspection.
        }
        throw new InvalidOperationException("No deterministic blind seed");
    }
    private static void Apply(PlayerMobile p,BaseCreature t)
    {Require(NecromancerEffects.Apply(p,t,CombatAbility.DimVision,1),"Apply actual blind receiver");Require(State(t)!=null,"Receiver did not attach world controller");}
    private static void Controls()
    {
        var p=Owner();var t=Enemy(p);var origin=p.Location;
        try
        {
            Apply(p,t);var initial=State(t) with{Seed=Seed(false),NextUtc=TimedEffects.Now};State(t,initial);t.NextReacquireTime=Core.TickCount;
            DateTime start=TimedEffects.Now;Require(SourceBlind.Think(t,NecromancerEffects.Curse(t)),"Controller did not handle idle");var idle=State(t);
            Require(idle.Thinks==1&&idle.Draws==1&&idle.Goal==null&&!idle.Combat&&Math.Abs((idle.NextUtc-start).TotalMilliseconds-400)<30,"Target-present idle clock/draw contract");
            Require(!t.CanBeHarmful(p,false)&&!SourceBlind.AcquiringTarget(t),"Acquisition bypass escaped into actual distant attacks");
            SourceBlind.Think(t,NecromancerEffects.Curse(t));Require(State(t)==idle,"A second poll rolled before its deadline");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.DimVision,1)&&State(t)==idle,"Equal recast reset controller seed/clock");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,1)&&State(t).Seed==idle.Seed,"Replacement lost actor RNG");
            Apply(p,t);Require(State(t).Seed==idle.Seed,"Fresh blind rerolled retained actor RNG");
            var expired=NecromancerEffects.Profiles.Find(t);NecromancerEffects.Profiles.Store(t,expired with{Curse=expired.Curse with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}});
            NecromancerEffects.Think(t);Require(!NamedControl.Blind(t)&&State(t).Seed==idle.Seed,"Expiry removed seed or retained blind");
            Apply(p,t);p.MoveToWorld(new(origin.X+100,origin.Y,origin.Z),p.Map);t.NextReacquireTime=Core.TickCount;
            var noTarget=State(t) with{NextUtc=TimedEffects.Now};State(t,noTarget);start=TimedEffects.Now;
            SourceBlind.Think(t,NecromancerEffects.Curse(t));var empty=State(t);
            Require(empty.Thinks==1&&empty.Draws==0&&empty.Seed==noTarget.Seed&&empty.Goal==null&&Math.Abs((empty.NextUtc-start).TotalMilliseconds-1000)<30,"No-target path made a percentage roll or used tick idle");
            // These synchronous controls advance source inspection deadlines,
            // not LeashedMeleeAI's independent 200ms scan throttle. Use a fresh
            // actor for admission; Live verifies the same-actor return in time.
            t.Delete();p.MoveToWorld(origin,p.Map);t=Enemy(p,1);Apply(p,t);
            SourceBlind.Think(t,NecromancerEffects.Curse(t));var attack=State(t);
            Require(attack.Combat&&attack.Focus==p.Serial.Value&&attack.Draws==0&&t.Combatant==p&&t.CanBeHarmful(p,false),
                $"Adjacent attack handoff/draw contract: state={JsonSerializer.Serialize(attack)}, before={JsonSerializer.Serialize(empty)}, combatant={t.Combatant?.Serial}, owner={p.Serial}, enemy={t.IsEnemy(p)}, harmful={t.CanBeHarmful(p,false)}, positions={t.Location}/{p.Location}, motion={NecromancerEffects.Curse(t)?.Motion}, visible={t.CanSee(p)}, los={t.InLOS(p)}, blessed={p.Blessed}/{t.Blessed}, hidden={p.Hidden}, alive={p.Alive}/{t.Alive}, mode={t.FightMode}, gate={t.NextReacquireTime-Core.TickCount}, map={p.Map}/{t.Map}, area={p.Region.Name}/{t.Region.Name}, food={ClassFoodHooks.CanAcquire?.Invoke(t,p)}");
            Log.Information("Blind world controls:target/no-target deadlines,no early/extra draws,attack admission,scoped acquisition,recast/replacement/expiry RNG continuity pass");
        }
        finally{t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Controls();var p=Owner();var t=Enemy(p);Apply(p,t);
        State(t,State(t) with{Seed=Seed(true),NextUtc=TimedEffects.Now});t.NextReacquireTime=Core.TickCount;t.AIObject.NextMove=Core.TickCount+3_600_000;
        Require(SourceBlind.Think(t,NecromancerEffects.Curse(t)),"Wander controller did not run");var s=State(t);
        Require(s.Fault==null&&s.Goal is{} goal&&goal.X==t.X+2&&s.Draws==5&&s.Thinks==1,"Actual wander goal or draw chronology");
        var curse=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddDays(1)};
        s=s with{NextUtc=TimedEffects.Now.AddDays(1),NextStepUtc=TimedEffects.Now.AddDays(1)};
        NecromancerEffects.Profiles.Store(t,NecromancerEffects.Profiles.Find(t) with{Curse=curse,BlindAi=s});
        var saved=new Saved(p.Serial.Value,t.Serial.Value,p.Map.MapID,p.Location,t.Location,curse,s,0);
        t.Internalize();p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(State(t)==s.Ai&&NecromancerEffects.Curse(t)==s.Curse,"Saved blind state/seed/goal/deadlines changed");
        p.MoveToWorld(s.OwnerAt,Map.Maps[s.Map]);t.MoveToWorld(s.TargetAt,p.Map);t.NextReacquireTime=Core.TickCount;t.AIObject.NextMove=Core.TickCount;
        SourceBlind.Think(t,NecromancerEffects.Curse(t));Require(t.Location==s.TargetAt&&State(t)==s.Ai,"Reload reset the saved movement cooldown or rerolled AI");
        // Advance only this inspection deadline, then execute a real native step.
        State(t,s.Ai with{NextStepUtc=TimedEffects.Now});t.AIObject.NextMove=Core.TickCount;
        SourceBlind.Think(t,NecromancerEffects.Curse(t));var next=State(t);
        Require(next.Fault==null&&t.Location!=s.TargetAt&&next.Seed==s.Ai.Seed&&next.Draws==s.Ai.Draws&&next.Thinks==s.Ai.Thinks,"Saved wander did not resume without a new decision");
        next=next with{NextUtc=TimedEffects.Now.AddDays(1),NextStepUtc=TimedEffects.Now.AddDays(1)};State(t,next);
        var saved=s with{TargetAt=t.Location,Ai=next,Generation=s.Generation+1};t.Internalize();p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Live(NetState network)
    {
        var p=Owner();var t=Enemy(p,6);
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("blind-world-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;network.Mobile=p;p.NetState=network;
        network.ResetMovementState();network.SendMobileUpdate(p);
        Require(t.Map.GetSector(t.Location).Active,"Connected player did not activate the blind actor's sector");
        Apply(p,t);State(t,State(t) with{Seed=Seed(false),NextUtc=TimedEffects.Now.AddHours(1)});
        bool finished=false;
        void Finish(Exception e=null)
        {if(finished){return;}finished=true;t.Delete();p.Delete();account.Delete();if(e==null){Log.Information("blind-world-live-result: passed");}else{Log.Error(e,"blind-world-live-result: failed");}}
        // Native wheel callbacks can run during catch-up while Core.TickCount
        // is ahead. Observe the controller's real clock on the loop queue;
        // polling never advances the actor's decision or step deadlines.
        void Observe(DateTime at,Action observation)
        {
            void Poll()
            {
                if(finished){return;}
                if(TimedEffects.Now<at){Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Poll));return;}
                try{observation();}catch(Exception e){Finish(e);}
            }
            Timer.StartTimer(TimeSpan.Zero,()=>Core.LoopContext.Post(Poll));
        }
        FixtureClock.NextLoop("blind-world-live-result",()=>
        {
            State(t,State(t) with{NextUtc=TimedEffects.Now});t.NextReacquireTime=Core.TickCount;
            Log.Information("blind-world-live: ready");
            Observe(TimedEffects.Now.AddMilliseconds(100),()=>
            {
                var first=State(t);Require(first is{Thinks:1,Draws:1,Goal:null,Fault:null},"Natural first idle decision");
                // Beyond the18-tile acquisition adapter while still inside
                // the connected player's two-sector native activation range.
                p.MoveToWorld(new(t.X+20,t.Y,t.Z),p.Map);
                Require(t.Map.GetSector(t.Location).Active,"No-target inspection culled the actor's sector");
                Observe(first.NextUtc.AddMilliseconds(-50),()=>Require(State(t)==first,"Natural controller acted before its source idle deadline"));
                Observe(first.NextUtc.AddMilliseconds(100),()=>
                {
                    var second=State(t);Require(second is{Thinks:2,Draws:1,Focus:0,Goal:null,Fault:null}&&network.Running,$"Natural no-target decision/connection: state={JsonSerializer.Serialize(second)}, running={network.Running}");
                    p.MoveToWorld(new(t.X-1,t.Y,t.Z),p.Map);
                    Observe(second.NextUtc.AddMilliseconds(-50),()=>Require(State(t)==second,"Returning enemy bypassed the no-target idle deadline"));
                    Observe(second.NextUtc.AddMilliseconds(100),()=>
                    {
                        var attack=State(t);Require(attack is{Combat:true,Draws:1,Fault:null}&&attack.Thinks>=3&&attack.Focus==p.Serial.Value&&t.Combatant==p&&network.Running,"Natural no-target-to-adjacent reacquisition/admission");Finish();
                    });
                });
            });
        });
    }
}
