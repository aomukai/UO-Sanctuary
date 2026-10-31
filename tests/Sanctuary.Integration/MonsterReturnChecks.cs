// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class MonsterReturnChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MonsterReturnChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"monster-return-fixtures.json");
    private sealed record Saved(uint Body,int Life256,PreparedMonsterDamagePacket Packet,int ExpectedHitClass=0x23);
    private static int StartupSharedCount;
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    public static void Configure()
    {
        StartupSharedCount=PreparedMonsterReturn.SharedHitClasses.Count;
        Register("monster-return-check",Start);Register("monster-return-reload-check",Reload);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Exact prepared NPC return/recovery policy",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    internal class Services:MonsterReturnServices
    {
        internal int[] P;internal int ModeValue,Last=777,Requested=-1,KillCount,ModeCount,RecoveryCount,Draws;
        internal uint Flags=0x10000000;internal ulong Seed;internal bool Delayed;internal List<int[]> Trace=new();
        internal Services(int[] p){P=p;ModeValue=p[6];Seed=SourceRandom.InitLow(p[21]);}
        internal override int? AttackerKind=>P[1]==6?null:P[1];
        internal override int TargetMode=>ModeValue;
        internal override int TargetClass=>P[8];
        internal override bool HasRoom(){bool v=P[2]>=0;Trace.Add(new[]{1,v?1:0,0});return v;}
        internal override bool RoomInTown(){bool v=P[2]==1;Trace.Add(new[]{2,v?1:0,0});return v;}
        internal override bool? AttackerTownRule(){Trace.Add(new[]{3,413,P[3]});return P[3]==0?null:P[4]!=0;}
        internal override bool Uninterruptible(){Trace.Add(new[]{5,37,P[5]});return P[5]!=0;}
        internal override bool Frozen(){Trace.Add(new[]{5,1001,P[12]});return P[12]!=0;}
        internal override bool Stunned(){Trace.Add(new[]{15,36,P[11]});return P[11]!=0;}
        internal override int BaseId(){int v=P[9]!=0?78:0;Trace.Add(new[]{7,v,0});return v;}
        internal override int? ModeFlags(){Trace.Add(new[]{8,TargetClass,P[10]>=0?1:0});return P[10]<0?null:P[10];}
        internal override int CurrentLife256=>P[19];
        internal override int MaximumLife256{get{Trace.Add(new[]{18,P[18],0});return P[18];}}
        internal override int LastSentLifePercent=>P[20];
        internal override uint RollDefender(){Draws++;return SourceRandom.Roll(ref Seed);}
        internal override void LastHitClass(int value)=>Last=value;
        internal override void DeathDelay(){Delayed=true;Trace.Add(new[]{6,30,1});}
        internal override void Kill(){KillCount++;ModeValue=0;Trace.Add(new[]{11,0,0});}
        internal override void Mode(int mode,bool targetAttacker){Requested=mode;ModeCount++;Trace.Add(new[]{9,mode,0});Trace.Add(new[]{10,mode,targetAttacker?1:0});}
        internal override void Refresh()=>Trace.Add(new[]{12,0,0});
        internal override void MarkLifeAndHitClass()=>Flags|=0x8000;
        internal override void AiState(int state)=>Trace.Add(new[]{13,state,0});
        internal override void RecoveryNotification()=>Trace.Add(new[]{14,0,0});
        internal override void SelectingHitClass(int count,int basis)=>Trace.Add(new[]{4,count,basis});
        internal override void CheckingRecovery(int hitClass,int damage256){RecoveryCount++;Trace.Add(new[]{16,hitClass,damage256});}
    }
    private static void SourceCases()
    {
        int count=0;var root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");
        foreach(string variant in new[]{"classic","ubers"})
        {
            var cycle=new MonsterHitClassCycle();int n=0,zero=0,one=0,two=0;
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(root,".local/class-skills/monster-return",variant+".json")));
            foreach(var row in doc.RootElement.EnumerateArray())
            {
                var p=N(row[0]);var expected=row[1].EnumerateArray().Select(x=>x.GetInt64()).ToArray();var s=new Services(p);int before=cycle.Count;
                var packet=new PreparedMonsterDamagePacket(0,p[16],0,0,p[7],p[13],p[14]!=0,0,0,(p[15]&2)!=0?512:0,(p[15]&4)!=0?512:0,(p[15]&1)!=0?512:0,p[17]!=0?p[16]:(p[15]&8)!=0?512:0);
                PreparedMonsterReturn.Dispatch(ref packet,cycle,s,variant=="ubers");
                long[] actual=[packet.ResultFlags,packet.HitClass,s.Last,s.Flags,s.Requested,s.Delayed?1:0,s.KillCount,s.ModeCount,(long)s.Seed,before,cycle.Count,s.RecoveryCount,s.ModeValue,s.CurrentLife256];
                Require(actual.SequenceEqual(expected),"Full-source NPC return output differs at "+variant+":"+n+" got="+string.Join(',',actual)+" expected="+string.Join(',',expected));
                var trace=row[2].EnumerateArray().Select(N).ToArray();
                Require(s.Trace.Count==trace.Length&&s.Trace.Zip(trace).All(pair=>pair.First.SequenceEqual(pair.Second)),"Full-source NPC return service order differs at "+variant+":"+n);
                if(s.Draws==0){zero++;}else if(s.Draws==1){one++;}else if(s.Draws==2){two++;}else{throw new InvalidOperationException("Unexpected recovery RNG draw count");}n++;count++;
            }
            Require(n==30244&&cycle.Count==3240&&(zero,one,two)==(29236,672,336),"NPC return source coverage/cycle/RNG counts incomplete");
        }
        Require(count==60488,"Both source feature variants required");
    }
    private static EncounterCreature Body()
    {
        var b=new EncounterCreature{Name="Monster return [fixture]"};b.Frozen=b.CantWalk=true;b.AIObject?.AITimer.Stop();return b;
    }
    internal class WorldServices:Services
    {
        internal BaseCreature Body;internal Action Death;
        internal WorldServices(BaseCreature body,bool guard=false):base(new[]{0,1,0,1,0,guard?1:0,1,5,413,0,(1<<3)|(1<<6)|(1<<13),0,0,3,0,0,0,0,body.HitsMax*256,0,128,123}){Body=body;}
        internal override int CurrentLife256=>SkillHealth.Balance256(Body);
        internal override int MaximumLife256=>SkillHealth.Maximum256(Body);
        internal override bool Stunned()=>CombatStuns.Remaining(Body)>0;
        internal override void Kill(){base.Kill();Death();}
        internal override void Mode(int mode,bool targetAttacker)
        {
            base.Mode(mode,targetAttacker);ModeValue=mode;
            if(mode==PreparedMonsterReturn.GetHit)
            {Require(CreatureHitRecovery.BeginPreparedGetHit(Body,320),"Accepted native GH entry failed");}
        }
    }
    internal class Release:PreparedMonsterDamageServices
    {
        internal WorldServices World;
        internal Release(WorldServices world)=>World=world;
        internal override void StoreLastAttacker(){}
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>p;
        internal override void BeforeHealth(PreparedMonsterDamagePacket p){}
        internal override void ApplyCold(int frames){Require(frames==0,"This native fixture supplies no cold-state delivery");}
        internal override void SurvivedDamage(){}
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>p;
        internal override void PresentOverlay(int overlay)=>throw new NotSupportedException();
        internal override int BaseHitClass()=>3;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>p;
        internal override bool AttackerInActiveMode()=>true;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p){}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
        {World.Death=death;PreparedMonsterReturn.Dispatch(ref p,World,true);}
        internal override void FreePacket(){}
    }
    private static void Start()
    {
        int shared=PreparedMonsterReturn.SharedHitClasses.Count;
        SourceCases();Require(PreparedMonsterReturn.SharedHitClasses.Count==shared,"Explicit source fixtures leaked into live shared hit-class history");
        var actor=Body();var target=Body();var now=TimedEffects.Now;var metadata=new PreparedMonsterRecipient(false,()=>false,()=>5,()=>false);
        SkillHealth.SetBalance256(target,100001);var world=new WorldServices(target);
        Require(CreatureHitRecovery.BeginPreparedGetHit(target,12000)&&CreatureProfiles.Session(target).RecoveryUntil-Core.TickCount>11000,"Accepted mode clock was capped or rejected");
        var zero=PreparedMonsterDamage.Release(actor,target,new(0,0,30,0,5),metadata,_=>0,new Release(world),now);
        Require(world.Requested==3&&world.Draws==0&&CreatureHitRecovery.Remaining(target)>0&&CreatureProfiles.Session(target).RecoveryUntil-Core.TickCount<1000&&SkillHealth.Balance256(target)==100001,"Native zero-damage Stun failed to replace the old GH clock without RNG/health changes");
        CombatStuns.Clear(target);Require(CreatureHitRecovery.Remaining(target)>200,"Accepted GH entry did not create its native recovery clock");
        // Live Iron consumers can precede this fixture. Continue their shared
        // cursor rather than resetting production history for the test.
        int expectedClass=((shared+1)%4) switch{1=>0x23,2=>0x43,_=>0x33};
        world=new WorldServices(target);var typed=PreparedMonsterDamage.Release(actor,target,new(7,17,0,0,1,0,false,0,1,2,3,4),metadata,_=>0,new Release(world),now);
        Require(typed.Packet.HitClass==expectedClass&&world.Last==expectedClass&&typed.Packet.Magic256==1&&typed.Packet.Fire256==2&&typed.Packet.Lightning256==3&&typed.Packet.Cold256==4&&SkillHealth.Balance256(target)==99984,"Typed channels/history or returned by-reference packet were lost");
        Require(PreparedMonsterReturn.SharedHitClasses.Count==shared+2,"Return consumers did not share hit-class history");
        var dying=Body();SkillHealth.SetBalance256(dying,256);world=new WorldServices(dying);
        var killed=PreparedMonsterDamage.Release(actor,dying,new(256,256,30,0,5),metadata,_=>0,new Release(world),now);
        Require(killed.DeathModeAttempted&&world.KillCount==1&&dying.Deleted,"Real return policy did not consume native lethal result once");
        var deferred=Body();SkillHealth.SetBalance256(deferred,256);world=new WorldServices(deferred,true);
        var delay=PreparedMonsterDamage.Release(actor,deferred,new(256,256,30,0,5),metadata,_=>0,new Release(world),now);
        Require(world.Delayed&&!delay.DeathModeAttempted&&deferred.Alive&&!deferred.Deleted&&deferred.Hits==0,"Uninterruptible source return did not defer native death");
        // Actual owner mode completion/persistence is a separate actor contract;
        // this fixture does not invent a timer or retain the scoped death action.
        CombatStuns.Clear(deferred);deferred.Delete();dying.Delete();actor.Delete();
        Timer.StartTimer(TimeSpan.FromMilliseconds(400),()=>
        {
            try
            {
                Require(CreatureHitRecovery.Remaining(target)==0&&SkillHealth.Balance256(target)==99984,"Natural prepared GH clock did not expire or changed health");
                target.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(target.Serial.Value,99984,typed.Packet,expectedClass)));
                Log.Information("monster-return-result: passed");
            }
            catch(Exception e){Log.Error(e,"monster-return-result: failed");}
        });
        Log.Information("NPC return:60488complete-source policies/exact traces,6480typed selections,zero-one-two defender RNG;native Stun-forced GH/typed packet/lethal/deferred entry pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var body=World.FindMobile((Serial)saved.Body) as EncounterCreature;
        Require(body is{Alive:true,Deleted:false}&&SkillHealth.Balance256(body)==saved.Life256&&SkillHealth.Remainder256(body)==112,"Returned health field did not persist without replay");
        Require(saved.Packet is{Magic256:1,Fire256:2,Lightning256:3,Cold256:4}&&saved.Packet.HitClass==saved.ExpectedHitClass,"Typed return payload fixture changed");
        Require(StartupSharedCount==0&&CreatureHitRecovery.Remaining(body)==0,"Process-local class history or an unsaved in-flight GH clock was replayed");
        Log.Information("NPC return:saved FP life/typed fixture retain result;process-local hit-class cursor and accepted in-flight GH clock reset without replay");
    }
}
