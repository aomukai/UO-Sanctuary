// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class MonsterDeathDelayChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MonsterDeathDelayChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"monster-death-delay-fixtures.json");
    private sealed record Victim(uint Actor,string Kind,int Life256,MonsterModeFlags Flags);
    private sealed record Saved(uint Attacker,Victim[][] Batches,int Next);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    public static void Configure()
    {
        Register("monster-death-delay-check",Start);Register("monster-death-delay-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(var v in s.Batches.Skip(s.Next).SelectMany(x=>x)){if(World.FindMobile((Serial)v.Actor) is BaseCreature b){Stop(b);}}}};
    }
    private static void Register(string n,Action action)=>ConsoleInputHandler.RegisterCommand(n,"Source deferred NPC death state and actual completion",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    private sealed class Source:MonsterUninterruptibleServices
    {
        internal readonly int[] P;internal readonly MonsterReturnChecks.Services Return;internal bool Delayed;internal int Returns,Deletes;
        internal Source(int[] p)
        {
            P=p;Delayed=p[2]!=0;Return=new(new[]{0,p[5],p[6],p[7],p[8],p[1],p[9],2,413,0,(1<<3)|(1<<6)|(1<<13),0,0,0,0,0,0,0,25600,p[10],128,123});
        }
        internal override void Uninterruptible(bool enabled){Return.P[5]=enabled?1:0;Return.Trace.Add(new[]{19,37,enabled?1:0});}
        internal override bool HasGame(){Return.Trace.Add(new[]{20,P[3],0});return P[3]!=0;}
        internal override bool DeathDelayed(){Return.Trace.Add(new[]{5,30,Delayed?1:0});return Delayed;}
        internal override void ClearDeathDelay(){Delayed=false;Return.Trace.Add(new[]{6,30,0});}
        internal override bool ResolveLastAttacker(){Return.Trace.Add(new[]{21,P[4],0});return P[4]!=0;}
        internal override void ReturnFreshDeath()
        {
            Returns++;Return.Trace.Add(new[]{23,2,0});var packet=new PreparedMonsterDamagePacket(0,0,0,0,2);
            PreparedMonsterReturn.Dispatch(ref packet,new MonsterHitClassCycle(),Return,true);
            Require(packet is{Total256:0,Physical256:0,StunFrames:0,Fire256:0,Cold256:0,Magic256:0,ResultFlags:2},"Completion replayed a damage packet");
        }
        internal override void KillWithoutAttacker()=>Return.Kill();
        internal override void DeleteQueuedThink(){Deletes++;Return.Trace.Add(new[]{22,1002,0});}
        internal int[] State()=>[Return.P[5],Delayed?1:0,Return.ModeValue,Return.KillCount,Returns,Deletes,Return.CurrentLife256];
    }
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"monster-death-delay-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=N(row[0]);var s=new Source(p);MonsterUninterruptible.Change(p[0]!=0,s);Require(s.State().SequenceEqual(N(row[1])),"Source first deferred-death state differs at "+count);
            MonsterUninterruptible.Change(p[0]!=0,s);Require(s.State().SequenceEqual(N(row[2])),"Source repeated deferred-death state differs at "+count);
            var trace=row[3].EnumerateArray().Select(N).ToArray();Require(s.Return.Trace.Count==trace.Length&&s.Return.Trace.Zip(trace).All(x=>x.First.SequenceEqual(x.Second)),"Source deferred-death service order differs at "+count);count++;
        }
        Require(count==9216,"Complete deferred-death source grid missing");
    }
    private static void Stop(BaseCreature b){b.Frozen=b.CantWalk=true;b.Combatant=null;b.FocusMob=null;b.AIObject?.AITimer.Stop();}
    private static EncounterCreature Body(){var b=new EncounterCreature{Name="Deferred NPC death [fixture]"};Stop(b);return b;}
    private sealed class Return:MonsterReturnChecks.WorldServices
    {
        internal Return(BaseCreature b):base(b){}
        internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(Body);
        internal override void DeathDelay(){MonsterUninterruptibles.MarkDeathDelay(Body);Delayed=true;}
    }
    private sealed class Release:MonsterReturnChecks.Release
    {
        internal Release(Return s):base(s){}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
        {World.Death=death;PreparedMonsterReturn.Dispatch(ref p,new MonsterHitClassCycle(),World,true);}
    }
    private sealed class Completion:MonsterModeCompletion
    {
        internal bool Game=true;internal int Returns,Deletes,Kills;internal Mobile Resolved;
        internal override bool HasGame(BaseCreature actor)=>Game;
        internal override MonsterReturnServices ReturnServices(BaseCreature actor,Mobile attacker)
        {
            Require(!MonsterUninterruptibles.Active(actor)&&!MonsterUninterruptibles.Delayed(actor),"Delay/guard not cleared before attacker return");Returns++;Resolved=attacker;
            return new Return(actor){Death=()=>{Kills++;actor.LastKiller=attacker;actor.Kill();}};
        }
        internal override void DeleteQueuedThink(BaseCreature actor){Deletes++;actor.AIObject?.AITimer.Stop();}
    }
    private static Victim Pending(BaseCreature attacker,string kind)
    {
        var b=Body();var completion=new Completion();MonsterUninterruptibles.Change(b,true,completion);SkillHealth.SetBalance256(b,256);
        var result=PreparedMonsterDamage.Release(attacker,b,new(256,256,30,0,5),new(false,()=>false,()=>5,()=>false),_=>0,new Release(new Return(b)),TimedEffects.Now);
        Require(!result.DeathModeAttempted&&b.Alive&&b.Hits==0&&MonsterUninterruptibles.State(b) is{Uninterruptible:true,DeathDelay:true}&&MonsterUninterruptibles.State(b).LastAttacker==attacker.Serial.Value,"Lethal hit did not record actual pending mode/source");
        if(kind=="healed"){SkillHealth.Heal(b,2048);}
        if(kind=="no-game")
        {completion.Game=false;MonsterUninterruptibles.Change(b,false,completion);Require(MonsterUninterruptibles.State(b) is{Uninterruptible:false,DeathDelay:true}&&completion.Deletes==1,"Missing game cleared delayed death or deleted think");}
        CombatStuns.Clear(b);b.Internalize();return new(b.Serial.Value,kind,SkillHealth.Balance256(b),MonsterUninterruptibles.State(b));
    }
    private static void Start()
    {
        SourceCases();Require(MonsterUninterruptibles.Profiles!=null,"Mode flag persistence not configured");var attacker=Body();var missing=Body();var batches=new Victim[2][];
        for(int i=0;i<2;i++){batches[i]=[Pending(attacker,"present"),Pending(missing,"missing"),Pending(attacker,"healed"),Pending(attacker,"no-game")];}
        missing.Delete();attacker.Internalize();var states=MonsterUninterruptibles.Snapshot();
        var bytes=Task.Run(()=>{var w=new BufferWriter(new byte[1024*1024],true);MonsterUninterruptibles.Profiles.Serialize(w);return w.Buffer[..(int)w.Position];}).GetAwaiter().GetResult();
        var reader=new BufferReader(bytes);Require(reader.ReadInt()==0,"Unexpected mode flag version");var captured=JsonSerializer.Deserialize<MonsterModeFlags[]>(reader.ReadString());
        Require(states.SequenceEqual(captured)&&states.SequenceEqual(MonsterUninterruptibles.Snapshot()),"Worker serialization mutated mode flags");
        foreach(var bad in new[]{new MonsterModeFlags(0,true,true,0),new MonsterModeFlags(attacker.Serial.Value,false,false,0),new MonsterModeFlags(attacker.Serial.Value,true,true,0x40000001)})
        {
            bool refused=false;try{MonsterUninterruptibles.Restore(states.Append(bad).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&MonsterUninterruptibles.Snapshot().SequenceEqual(states),"Invalid mode restore was not atomic");
        }
        bool duplicate=false;try{MonsterUninterruptibles.Restore(states.Append(states[0]).ToArray());}catch(InvalidDataException){duplicate=true;}
        Require(duplicate&&MonsterUninterruptibles.Snapshot().SequenceEqual(states),"Duplicate mode restore was not atomic");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(attacker.Serial.Value,batches,0)));
        Timer.StartTimer(TimeSpan.FromMilliseconds(500),()=>
        {
            try{foreach(var v in batches.SelectMany(x=>x)){var b=World.FindMobile((Serial)v.Actor) as BaseCreature;Require(b is{Alive:true,Deleted:false}&&SkillHealth.Balance256(b)==v.Life256&&MonsterUninterruptibles.State(b)==v.Flags,"Pending death expired or ran without a real completion signal");}Log.Information("monster-death-delay-result: passed");}
            catch(Exception e){Log.Error(e,"monster-death-delay-result: failed");}
        });
        Log.Information("Deferred NPC death:9216complete-source two-call states/traces;actual lethal flags/source,zero/positive HP,missing-game,read-only worker and atomic restore pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Next is 0 or 1,"Unexpected completion batch");
        foreach(var v in saved.Batches.Skip(saved.Next).SelectMany(x=>x))
        {var b=World.FindMobile((Serial)v.Actor) as BaseCreature;Require(b is{Alive:true,Deleted:false}&&SkillHealth.Balance256(b)==v.Life256&&MonsterUninterruptibles.State(b)==v.Flags,"Actual saved pending actor/mode/source did not survive");Stop(b);}
        foreach(var v in saved.Batches[saved.Next])
        {
            var b=(BaseCreature)World.FindMobile((Serial)v.Actor);var context=new Completion();MonsterUninterruptibles.Change(b,false,context);
            Require(b.Deleted&&context.Deletes==1&&MonsterUninterruptibles.State(b)==null,"Completion failed native death/queued-think cleanup");
            Require(context.Returns==(v.Kind=="missing"?0:1)&&context.Kills==context.Returns,"Wrong missing-attacker versus fresh-return completion path");
            if(v.Kind!="missing"){Require(context.Resolved?.Serial.Value==saved.Attacker,"Wrong saved last-attacker identity");}
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Next=saved.Next+1}));
        Log.Information("Deferred NPC death:actual saved batch completes on real clear signal,including healed/missing-attacker/missing-game;no old damage replay or timer");
    }
}
