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
public static class MonsterOrderedReleaseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MonsterOrderedReleaseChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"monster-ordered-release-fixtures.json");
    private sealed record Saved(uint Target,int MaximumLife,int Life256,CombatStunState Stun,uint Buffed,int BuffedMaximum,int BuffedLife256,BattleOrdersState Buff);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    internal static EncounterCreature Body(bool wideLife=true)
    {
        var b=new EncounterCreature{Name="Ordered monster release [fixture]"};b.Frozen=b.CantWalk=true;b.AIObject?.AITimer.Stop();if(wideLife){b.SetHits(50000);b.Hits=b.HitsMax;}return b;
    }
    public static void Configure()
    {
        Register("monster-ordered-release-check",Start);Register("monster-ordered-release-reload-check",Reload);Register("monster-ordered-release-health-stage-check",StageHealth);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Ordered prepared NPC health/Stun/death release",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    internal class Services:PreparedMonsterDamageServices
    {
        internal BaseCreature Body;internal DateTime At;internal int Frame,ExpectedLife,ExpectedUntil,ExpectedCold,StunChange;internal bool ExpectedStun,HealBeforeDeath,TryTwice,HealthWrite;
        internal Action Escaped;internal int DeathCalls;internal List<int[]> Trace=new();
        private PreparedMonsterDamagePacket Event(int id,PreparedMonsterDamagePacket p){Trace.Add(new[]{20,id,p.Physical256});return p;}
        internal override void StoreLastAttacker()=>Trace.Add(new[]{3,0,0});
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)=>Event(1041,p);
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>Event(1039,p);
        internal override void BeforeHealth(PreparedMonsterDamagePacket p){if((p.ResultFlags&1)!=0){Trace.Add(new[]{4,0,0});}}
        internal override void ApplyCold(int frames)
        {
            Require(frames==ExpectedCold&&SkillHealth.Balance256(Body)==ExpectedLife&&Body.Alive&&!Body.Deleted,"Health must commit once before Stun/cold/death");
            if(HealthWrite){Trace.Add(new[]{21,29,ExpectedLife});} // Source writes HP for positive cached total.
            var stun=CombatStuns.Snapshot().SingleOrDefault(s=>s.Target==Body.Serial.Value);
            Require((stun!=null)==ExpectedStun,"Native transient/ordinary Stun differs");
            if(stun!=null){Require(stun.UntilUtc==At.AddMilliseconds((ExpectedUntil-Frame)*40),"Exact source Stun deadline differs");}
            if(StunChange is 2 or 3){Trace.Add(new[]{StunChange==2?30:31,ExpectedLife,ExpectedUntil});}
            Trace.Add(new[]{6,frames,0});
        }
        internal override void SurvivedDamage()=>Trace.Add(new[]{5,0,0});
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>Event(1044,p);
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>Event(1043,p);
        internal override void PresentOverlay(int overlay)=>throw new InvalidOperationException("Unexpected overlay in this source matrix");
        internal override int BaseHitClass()=>3;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>Event(1040,p);
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>Event(3,p);
        internal override bool AttackerInActiveMode()=>true;
        internal override void Thorns(PreparedMonsterDamagePacket p)=>Trace.Add(new[]{7,0,0});
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
        {
            Trace.Add(new[]{8,p.ResultFlags,p.HitClass});Escaped=death;
            if((p.ResultFlags&2)==0){return;}
            Require(Body.Alive&&!Body.Deleted&&SkillHealth.Balance256(Body)==ExpectedLife,"Death happened before source return service");
            if(HealBeforeDeath){SkillHealth.Heal(Body,2560);Require(Body.Hits>0,"Reentrant heal control failed");}
            DeathCalls++;death();Require(!Body.Alive||Body.Deleted,"Native death mode failed");
            if(TryTwice){bool refused=false;try{death();}catch(InvalidOperationException){refused=true;}Require(refused,"Duplicate death-mode dispatch accepted");}
        }
        internal override void FreePacket()=>Trace.Add(new[]{9,0,0});
    }
    private static void Start()
    {
        var actor=Body();var body=Body();var prior=CombatStuns.Snapshot();int count=0,writes=0,lethal=0,transient=0;
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-npc-release-source-probe.json"))))
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var i=N(row[0]);var o=N(row[1]);var s=N(row[2]);var state=N(row[3]);var sourceTrace=row[4].EnumerateArray().Select(N).ToArray();
            if((i[2]&1)==0||i[4]!=0||i[3]<=0){continue;} // Outer source contact/target guards remain a separate consumer.
            if(body.Deleted||!body.Alive){body=Body();}
            SkillHealth.SetBalance256(body,i[3]);Require(SkillHealth.Balance256(body)==i[3],"Source initial FP life not represented exactly");
            var at=TimedEffects.Now;var began=at.AddMilliseconds(-i[11]*40);GoatmanBerserkEffects.Clear(actor);
            CombatStuns.Restore(i[10]!=0?prior.Append(new CombatStunState(body.Serial.Value,at.AddMilliseconds((1000-i[11])*40))).ToArray():prior);
            int resistance=i[12];int Reduce(int n)=>NamedElements.Reduce256(n,resistance,0,0,0).Damage256;
            var packet=GoatmanNpcMelee.BeginFor(actor,i[0],i[1],i[2],10000000,
                new(30,50,210,512,1536,768,1792,1280,2048,31),p=>new(Reduce(p.Physical256),Reduce(p.Magic256),Reduce(p.Fire256),Reduce(p.Lightning256),Reduce(p.Cold256),p.ColdFrames*(100-resistance)/100,p.StunFrames),_=>0,()=>99,began);
            Require(new[]{packet.Physical256,packet.Magic256,packet.Fire256,packet.Lightning256,packet.Cold256,packet.Total256,packet.StunFrames}.SequenceEqual(new[]{o[3],o[4],o[5],o[6],o[7],o[8],o[9]}),"Captured native packet differs before release");
            int cold=sourceTrace.Single(t=>t[0]==6)[1];int change=packet.Total256>0?1:0;
            if(s[2]!=0){change=2;}if(s[3]!=0){change=3;}
            var services=new Services{Body=body,At=at,Frame=1000+i[11],ExpectedLife=o[2],ExpectedUntil=s[1],ExpectedStun=s[0]!=0,ExpectedCold=cold,StunChange=change,TryTwice=true,HealthWrite=packet.Total256>0};
            var draws=new List<int>();int releaseRank=i[1]==255?254:i[1]+1;
            var result=GoatmanNpcMelee.ReleaseQualified(actor,i[0],releaseRank,body,packet,new(i[5]!=0,()=>i[6]!=0,()=>i[7],()=>i[8]!=0),bound=>{draws.Add(bound);return i[9];},services,at);
            Require(result.AfterHealth256==o[2]&&result.Packet.ResultFlags==o[10]&&result.Packet.Physical256==o[11]&&result.Packet.Total256==o[12]&&result.Packet.HitClass==o[13],"Native release packet/health differs from full shared source");
            Require(services.Trace.Count==sourceTrace.Length&&services.Trace.Zip(sourceTrace).All(p=>p.First.SequenceEqual(p.Second)),"Native ordered service/health/Stun trace differs at "+count);
            Require(draws.SequenceEqual(N(row[5]))&&services.DeathCalls==(o[2]==0?1:0)&&result.DeathModeAttempted==(o[2]==0),"Source RNG/death consumption differs");
            if(i[0]==418)
            {
                var actual=GoatmanBerserkEffects.At(actor,at);Require(actual.Flags==state[1]&&actual.Rank==state[3]&&actual.UntilUtc==(state[2]==0?null:began.AddMilliseconds((state[2]-1000)*40)),"Berserk release state/captured deadline differs");
            }
            bool escaped=false;try{services.Escaped();}catch(InvalidOperationException){escaped=true;}Require(escaped,"Death callback escaped the synchronous return phase");
            if(packet.Total256>0){writes++;}if(o[2]==0){lethal++;if(s[2]+s[3]>0){transient++;}}
            CombatStuns.Clear(body);if(body.Deleted||!body.Alive){SkillHealth.Clear(body);}count++;
        }
        Require((count,writes,lethal,transient)==(1616,848,50,24),"Ordered release source scope incomplete");
        CombatStuns.Restore(prior);GoatmanBerserkEffects.Clear(actor);actor.Delete();body.Delete();
        ReentrantCachedDeath();Save();
        Log.Information("Ordered monster release:1616native complete-source compositions/848FP health writes/50native deaths/24transient lethal stuns;exact event order and once-only death,full actor/services still pending");
    }
    private static void ReentrantCachedDeath()
    {
        var actor=Body();var body=Body();SkillHealth.SetBalance256(body,256);var at=TimedEffects.Now;
        var services=new Services{Body=body,At=at,Frame=1000,ExpectedLife=0,ExpectedCold=0,StunChange=1,HealBeforeDeath=true,TryTwice=true,HealthWrite=true};
        var result=PreparedMonsterDamage.Release(actor,body,new(256,256,0,0,1),new(false,()=>false,()=>5,()=>false),_=>0,services,at);
        Require(result.DeathModeAttempted&&services.DeathCalls==1&&body.Deleted,"Cached death result incorrectly replaced by healed current HP");actor.Delete();body.Delete();
    }
    private static void Save()
    {
        var actor=Body();var body=Body(false);var at=TimedEffects.Now;Require(body.HitsMax*256>=100001,"Native encounter profile cannot hold the save fixture life");SkillHealth.SetBalance256(body,100001);
        var services=new Services{Body=body,At=at,Frame=1000,ExpectedLife=99984,ExpectedUntil=1030,ExpectedStun=true,ExpectedCold=0,StunChange=2,HealthWrite=true};
        var result=PreparedMonsterDamage.Release(actor,body,new(17,17,30,0,1),new(false,()=>false,()=>5,()=>false),_=>0,services,at);
        Require(!result.DeathModeAttempted&&SkillHealth.Balance256(body)==99984,"Fractional release save setup failed");
        var state=new CombatStunState(body.Serial.Value,at.Add(FixtureSpace.Retention));
        CombatStuns.Restore(CombatStuns.Snapshot().Where(s=>s.Target!=body.Serial.Value).Append(state).ToArray());
        // A saved buffed encounter may legitimately have more current life than
        // its unbuffed profile maximum. The loading guard must preserve it too.
        var buffed=Body(false);int basis=buffed.HitsMax;var supplier=new PlayerMobile{Name="Ordered release Orders supplier [fixture]",Body=0x190,Player=true};
        ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);BattleOrders.Apply(supplier,buffed,20,at);
        var buff=BattleOrders.State(buffed) with{UntilUtc=at.Add(FixtureSpace.Retention)};var orders=BattleOrders.Snapshot();
        BattleOrders.Restore(orders with{States=orders.States.Where(s=>s.Recipient!=buffed.Serial.Value).Append(buff).ToArray()});
        int buffedLife=(basis+10)*256-17;Require(buffedLife>basis*256&&buffedLife<buffed.HitsMax*256,"Buffed life fixture must exceed the ordinary maximum");SkillHealth.SetBalance256(buffed,buffedLife);
        body.Internalize();buffed.Internalize();supplier.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(body.Serial.Value,body.HitsMax,99984,state,buffed.Serial.Value,buffed.HitsMax,buffedLife,buff)));actor.Delete();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var body=World.FindMobile((Serial)saved.Target) as EncounterCreature;
        Require(body is{Alive:true,Deleted:false}&&body.HitsMax==saved.MaximumLife&&SkillHealth.Balance256(body)==saved.Life256&&SkillHealth.Remainder256(body)==112,$"Released fractional HP did not persist exactly once: target={saved.Target},life={body?.Hits}/{body?.HitsMax},expectedMax={saved.MaximumLife},balance={(body==null?-1:SkillHealth.Balance256(body))},fraction={(body==null?-1:SkillHealth.Remainder256(body))},expected={saved.Life256}");
        Require(CombatStuns.Snapshot().Single(s=>s.Target==saved.Target)==saved.Stun,"Released recipient Stun deadline did not persist");
        CheckBuff(saved);
        Log.Information("Ordered monster release:saved post-release FP health/stun resumes without replay or double subtraction");
    }
    private static void StageHealth()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var body=World.FindMobile((Serial)saved.Target) as EncounterCreature;
        Require(body is{Alive:true,Deleted:false}&&body.HitsMax==saved.MaximumLife&&SkillHealth.Balance256(body)==saved.Life256&&SkillHealth.Remainder256(body)==112,"Post-release FP health drifted before save");
        Require(CombatStuns.Snapshot().Single(s=>s.Target==saved.Target)==saved.Stun,"Post-release Stun changed before save");
        CheckBuff(saved);
    }
    private static void CheckBuff(Saved saved)
    {
        var body=World.FindMobile((Serial)saved.Buffed) as EncounterCreature;
        Require(body is{Alive:true,Deleted:false}&&body.HitsMax==saved.BuffedMaximum&&SkillHealth.Balance256(body)==saved.BuffedLife256&&SkillHealth.Remainder256(body)==17&&BattleOrders.State(body)==saved.Buff,"Saved encounter buff/above-base injured health was clamped or rescaled");
    }
}
