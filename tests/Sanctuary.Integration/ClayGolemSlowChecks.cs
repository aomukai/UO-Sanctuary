// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ClayGolemSlowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ClayGolemSlowChecks));
    private sealed record Saved(uint Enemy,DateTime Until,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"clay-golem-slow-fixtures.json");
    public static void Configure(){Register("clay-golem-slow-check",Check);Register("clay-golem-slow-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Clay incoming melee slow and split movement/attack persistence",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"clay-golem-slow-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int? amount=V(0)==0||V(1)==0?null:ClayGolemSlow.Amount(V(2),V(3),V(4));bool applied=amount.HasValue&&V(5)!=0;
            Require(V(6)==(applied?1:0)&&V(7)==(amount.HasValue?-amount.Value:0)&&V(8)==(amount.HasValue?750:0)
                &&V(9)==(applied?-amount.Value:0)&&V(10)==V(9)&&V(11)==V(6)&&V(12)==(amount.HasValue?1:0),"Complete incoming callback27case "+count);count++;
        }
        Require(count==1280,"Incomplete incoming slow source cases");
        Require(!PaladinCleansing.IsCurse("slowed")&&!PaladinCleansing.Curable("slowed"),"Slowed is not a cleansable curse");
    }
    private static (PlayerMobile Owner,ClayGolemBody Body) Scene(int rank)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="ClaySlowFixture";
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.claygolem")<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.claygolem",out _),"Train Clay");}
        Require(NecromancerSummons.Create(p,CombatAbility.ClayGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Create learned Clay");
        var b=NecromancerGolems.For(p).OfType<ClayGolemBody>().Single();b.AIObject.AITimer.Stop();b.SuspendActions();return(p,b);
    }
    private sealed class Phase(ClayGolemBody target,Mobile source):PreparedMonsterDamageServices
    {
        internal int Events;
        internal override void StoreLastAttacker(){}
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p){Events++;return p;}
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)
        {Require(TimedEffects.Slow(source,TimedEffects.Now)==0,"Clay reaction preceded recipient event");Events++;return p;}
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)
        {Require(target.Alive&&Events==2&&TimedEffects.Slow(source,TimedEffects.Now)==11,"Central qualified contact missed before-health reaction");}
        internal override void ApplyCold(int frames){}
        internal override void SurvivedDamage(){}
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>p;
        internal override void PresentOverlay(int overlay){}
        internal override int BaseHitClass()=>0;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>p;
        internal override bool AttackerInActiveMode()=>true;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p){}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death){}
        internal override void FreePacket(){}
    }
    private static void Check()
    {
        Source();var(p,b)=Scene(1);var(q,strong)=Scene(5);var enemy=PoisonDaggerChecks.Target(p);enemy.Name="ClaySlowFixtureEnemy";
        Require(b.ReactiveSlowPercent==11&&strong.ReactiveSlowPercent==37,"Captured Clay slow ranks");
        int hp=SkillHealth.Balance256(b);using(PaladinThorns.Attempt(enemy,b,false)){}
        Require(TimedEffects.Slow(enemy,TimedEffects.Now)==0,"Miss/attempt applied incoming slow");
        AOS.Damage(b,enemy,1,false,100,0,0,0,0,archer:true,weaponDamage:true);
        Require(TimedEffects.Slow(enemy,TimedEffects.Now)==0,"Ranged incoming hit slowed attacker");
        AOS.Damage(b,enemy,1,false,100,0,0,0,0,weaponDamage:false);
        Require(TimedEffects.Slow(enemy,TimedEffects.Now)==0,"Spell/unclassified incoming hit slowed attacker");
        b.SetResistance(ResistanceType.Physical,100);hp=SkillHealth.Balance256(b);
        AOS.Damage(b,enemy,100,false,100,0,0,0,0,weaponDamage:true);
        Require(SkillHealth.Balance256(b)==hp&&TimedEffects.Slow(enemy,TimedEffects.Now)==11&&TimedEffects.SlowAttack(enemy,TimedEffects.Now)==11,"Fully absorbed successful native melee missed reaction");
        var first=TimedEffects.Snapshot().Single(x=>x.Owner==enemy.Serial.Value);Require((first.SlowUntilUtc-TimedEffects.Now).TotalSeconds is >29 and <=30,"Clay slow must last750frames/30seconds");
        TimedEffects.Clear(enemy);
        var phase=new Phase(b,enemy);PreparedMonsterDamage.CommitQualified(enemy,b,new(0,0,0,0,5),new(false,()=>false,()=>8,()=>false),_=>throw new InvalidOperationException("Zero stun drew RNG"),phase,TimedEffects.Now);
        Require(SkillHealth.Balance256(b)==hp&&phase.Events==2,"Shared qualified zero-damage contact changed HP or missed events");
        TimedEffects.Clear(enemy);b.SetResistance(ResistanceType.Physical,25);
        SkillCombat.HitParts(enemy,b,new(1,0,0,0,0),true,weapon:true);
        Require(TimedEffects.Slow(enemy,TimedEffects.Now)==11,"Explicit native weapon consumer missed reaction");
        AOS.Damage(strong,enemy,1,false,100,0,0,0,0,weaponDamage:true);
        Require(TimedEffects.Slow(enemy,TimedEffects.Now)==11&&TimedEffects.SlowAttack(enemy,TimedEffects.Now)==37&&GearEffects.MovementSlow(enemy)==11
            &&GearEffects.AttackSlow(enemy)==37&&SourceAttackRate.WolfRate(enemy)==63,"Same-state refresh failed to retain velocity and replace attack slow");
        Require(!PaladinCleansing.Apply(enemy,1,TimedEffects.Now)&&TimedEffects.SlowAttack(enemy,TimedEffects.Now)==37,"Cleansing removed noncurse slow");
        var state=TimedEffects.Snapshot().Single(x=>x.Owner==enemy.Serial.Value);
        Require(TimedEffects.Slow(enemy,state.SlowUntilUtc.AddTicks(-1))==11&&TimedEffects.SlowAttack(enemy,state.SlowUntilUtc.AddTicks(-1))==37
            &&TimedEffects.Slow(enemy,state.SlowUntilUtc)==0&&TimedEffects.SlowAttack(enemy,state.SlowUntilUtc)==0,"Exact slow expiry");
        var original=TimedEffects.Snapshot();bool bad=false;
        try{TimedEffects.Load(original.Select(x=>x.Owner==enemy.Serial.Value?x with{SlowAttackPercent=91}:x).ToArray());}catch(InvalidDataException){bad=true;}
        Require(bad&&JsonSerializer.Serialize(TimedEffects.Snapshot())==JsonSerializer.Serialize(original),"Malformed split slow restore was not atomic");
        var legacy=state with{SlowAttackPercent=null};string legacyJson=JsonSerializer.Serialize(legacy);Require(!legacyJson.Contains("SlowAttackPercent"),"Default optional slow field changed legacy JSON");
        TimedEffects.Load(original.Select(x=>x.Owner==enemy.Serial.Value?legacy:x).ToArray());Require(TimedEffects.SlowAttack(enemy,TimedEffects.Now)==11,"Legacy slow did not retain shared attack value");TimedEffects.Load(original);
        // Reaction is before lethal health commitment;it must survive source removal.
        SkillHealth.SetBalance256(b,256);AOS.Damage(b,enemy,100000,false,100,0,0,0,0,weaponDamage:true);
        Require((b.Deleted||!b.Alive)&&TimedEffects.SlowAttack(enemy,TimedEffects.Now)==11,"Lethal incoming melee lost pre-death reaction");
        AOS.Damage(strong,enemy,1,false,100,0,0,0,0,weaponDamage:true);
        state=TimedEffects.Snapshot().Single(x=>x.Owner==enemy.Serial.Value);DateTime held=TimedEffects.Now.Add(FixtureSpace.Retention);
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==enemy.Serial.Value?x with{SlowUntilUtc=held}:x).ToArray());
        strong.Delete();p.Delete();q.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(enemy.Serial.Value,held,0)));
        Log.Information("Clay incoming slow:1280complete source cases;native AOS full absorption/ranged/spell/miss controls,central before-health order,split velocity/attack rates,noncurse,expiry,atomic legacy saves and lethal source removal pass");
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var enemy=World.FindMobile((Serial)s.Enemy);
        var row=TimedEffects.Snapshot().Single(x=>x.Owner==s.Enemy);
        Require(row.SlowUntilUtc==s.Until&&TimedEffects.Slow(enemy,TimedEffects.Now)==11&&TimedEffects.SlowAttack(enemy,TimedEffects.Now)==37
            &&GearEffects.MovementSlow(enemy)==11&&GearEffects.AttackSlow(enemy)==37,"Saved split slow lost values/deadline after source deletion");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
