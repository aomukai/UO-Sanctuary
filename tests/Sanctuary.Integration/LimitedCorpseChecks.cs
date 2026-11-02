// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Collections;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

// Explicit completed-death service for pool ordering across flagged families;
// this fixture does not claim their real death adapters have been installed.
public sealed class LimitedCorpseFixture:BaseCreature,ILimitedSummonCorpse
{
    private string _source;
    public LimitedCorpseFixture(string source):base(AIType.AI_Melee,FightMode.None,0){_source=source;Body=0x9C;Hidden=Blessed=CantWalk=true;AIObject?.AITimer.Stop();}
    public LimitedCorpseFixture(Serial serial):base(serial){}
    public override bool Alive=>false;
    public override bool BlocksFit=>false;
    public override bool CanBeDamaged()=>false;
    public override bool CanRegenHits=>false;
    public override bool CanRegenMana=>false;
    public override bool CanRegenStam=>false;
    string ILimitedSummonCorpse.CorpseSource=>_source;
    bool ILimitedSummonCorpse.SourceDead=>true;
    public override void OnThink()=>AIObject?.AITimer.Stop();
    public override void OnAfterDelete(){LimitedSummonCorpses.Forget(this);base.OnAfterDelete();}
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);writer.Write(_source);}
    public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown corpse fixture");}_source=reader.ReadString();AIObject?.AITimer.Stop();}
}
public static class LimitedCorpseChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(LimitedCorpseChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Target,int Map,Point3D Origin,LimitedCorpseSave Pool,SentryBodyState Body,SentryBoltState Bolt,int Health,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"limited-corpse-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared shared corpse pool and actual retired Sentry projectile ownership",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("limited-corpse-check",()=>FixtureNetwork.Connected("limited-corpse-result",Check));Register("limited-corpse-reload-check",()=>FixtureNetwork.Connected("limited-corpse-reload-result",Reload));}
    private static void Pool(PlayerMobile p)
    {
        var rules=JsonSerializer.Deserialize<LimitedCorpseRules>(File.ReadAllText(Path.Join(Core.BaseDirectory,"Data/Sanctuary/Balance/limited-summon-corpses.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        var bodies=Enumerable.Range(0,51).Select(i=>
        {var b=new LimitedCorpseFixture(rules.Sources[i%rules.Sources.Length]);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);LimitedSummonCorpses.Retain(b);return b;}).ToArray();
        var before=LimitedSummonCorpses.Snapshot();Require(before.Entries.Length==50&&bodies[0].Deleted&&!bodies[1].Deleted&&before.Entries.Select(x=>x.Actor).SequenceEqual(bodies.Skip(1).Select(x=>x.Serial.Value)),"Shared50cap/FIFO or original identity failed");
        LimitedSummonCorpses.Retain(bodies[^1]);Require(JsonSerializer.Serialize(before)==JsonSerializer.Serialize(LimitedSummonCorpses.Snapshot()),"Repeat registration reset corpse order");
        string json=JsonSerializer.Serialize(before);
        foreach(var bad in new[]{before with{Entries=before.Entries.Take(49).Append(before.Entries[^1] with{Order=0}).ToArray()},before with{Entries=before.Entries.Take(49).Append(before.Entries[0]).ToArray()},before with{Entries=before.Entries.Skip(1).Prepend(null).ToArray()},before with{Entries=before.Entries.Take(49).Append(before.Entries[^1] with{Source="necromage"}).ToArray()}})
        {bool rejected=false;try{LimitedSummonCorpses.Restore(bad);}catch(InvalidDataException){rejected=true;}Require(rejected&&JsonSerializer.Serialize(LimitedSummonCorpses.Snapshot())==json,"Malformed corpse restore changed live pool");}
        var alive=new BladeMissileTarget();bool living=false;try{LimitedSummonCorpses.Retain(alive);}catch(InvalidOperationException){living=true;}finally{alive.Delete();}Require(living,"Living actor accepted as retained corpse");
    }
    private static BladeMissileTarget Target(PlayerMobile p,Point3D at)
    {
        var t=new BladeMissileTarget{NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(at,p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Energy,0);return t;
    }
    private static void HoldBolt(long id,DateTime at)
    {var state=AssassinSentryBolts.Snapshot();AssassinSentryBolts.Restore(state with{Bolts=state.Bolts.Select(x=>x.Id==id?x with{NextUtc=at}:x).ToArray()});}
    private static void AtomicBolt(SentryBoltState bolt)
    {
        var before=AssassinSentryBolts.Snapshot();string json=JsonSerializer.Serialize(before);
        foreach(var bad in new[]{before with{Bolts=before.Bolts.Where(x=>x.Id!=bolt.Id).Append(bolt with{Frame=77}).ToArray()},before with{Bolts=before.Bolts.Append(bolt).ToArray()},before with{Bolts=before.Bolts.Prepend(null).ToArray()}})
        {bool rejected=false;try{AssassinSentryBolts.Restore(bad);}catch(InvalidDataException){rejected=true;}Require(rejected&&JsonSerializer.Serialize(AssassinSentryBolts.Snapshot())==json,"Bolt restore lost source pool/next-id");}
    }
    private static void Save(PlayerMobile p,AssassinSentryBody body,BladeMissileTarget target,long bolt,int generation,Point3D origin,int map)
    {
        HoldBolt(bolt,TimedEffects.Now.Add(FixtureSpace.Retention));p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,target.Serial.Value,map,origin,LimitedSummonCorpses.Snapshot(),body.Prepared,AssassinSentryBolts.State(bolt),SkillHealth.Balance256(target),generation)));
    }
    private static void Check(NetState network)
    {
        var p=BladeBodyChecks.Fresh(reach:20);Pool(p);FixtureNetwork.Authenticate(network,p,"corpse-owner-");var origin=p.Location;
        var body=AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now.AddHours(1),SourceRandom.InitLow(19));var target=Target(p,new(body.X+8,body.Y,body.Z));
        // Three actual factory shots: one held for two reloads, two ordinary
        // flights prove actual dead-NPC-owned timed damage before the first save.
        long held=AssassinSentryBolts.Create(body,new(0,new(target.X*2,target.Y*2),ChargedBolts.SeedSubtile(target.X*2,0),77,77));
        long first=AssassinSentryBolts.Create(body,new(0,new(target.X*2,target.Y*2),ChargedBolts.SeedSubtile(target.X*2,0),77,77));
        long second=AssassinSentryBolts.Create(body,new(0,new(target.X*2,target.Y*2),ChargedBolts.SeedSubtile(target.X*2,0),77,77));
        HoldBolt(held,TimedEffects.Now.Add(FixtureSpace.Retention));AtomicBolt(AssassinSentryBolts.State(held));var creation=JsonSerializer.Serialize(body.Prepared.Creation);
        // The supplied controller clock is held an hour ahead to suppress new
        // volleys. Factory shots inherit that clock; explicitly release only
        // these two flights under the ordinary projectile timer.
        HoldBolt(first,TimedEffects.Now.AddMilliseconds(40));HoldBolt(second,TimedEffects.Now.AddMilliseconds(40));
        body.CompleteRetirement(TimedEffects.Now);
        Require(!body.Alive&&!body.Deleted&&body.RetainedCorpse&&body.Hidden&&!AssassinSentryActors.Actors().Contains(body)&&AssassinTrapRoster.For(p).All(x=>x.Actor!=body.Serial.Value)
            &&JsonSerializer.Serialize(body.Prepared.Creation)==creation,"Retirement changed identity/stats or left living AI/slot");
        var state=body.Prepared;body.AdvanceController(TimedEffects.Now.AddHours(2),true);Require(body.Prepared==state,"Dead source actor advanced/restarted");
        bool refused=false;try{AssassinSentryBolts.Dispatch(body,target.Serial.Value);}catch(InvalidOperationException){refused=true;}Require(refused,"Dead actor emitted another volley");
        int hp=SkillHealth.Balance256(target);long end=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                if(SkillHealth.Balance256(target)==hp){Require(Core.TickCount<end,"Already-issued corpse-owned bolts did not damage");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                bool attributed=false;foreach(var entry in target.DamageEntries){if(entry.Damager==body){attributed=true;break;}}
                Require(attributed&&!body.Alive&&LimitedSummonCorpses.Snapshot().Entries.Length==50,"Damage source was substituted or pool unbounded");
                var kill=Target(p,new(body.X+3,body.Y,body.Z));kill.NoKillAwards=false;kill.SetHits(1);kill.Hits=1;
                AssassinSentryBolts.Impact(body,kill,AssassinSentryBolts.State(held) with{Minimum256=2560,Maximum256=2560});Require(!kill.Alive&&kill.LastKiller==body&&ClassLoot.Killer(kill)==p,"Dead trap/original player kill credit");kill.Delete();
                Save(p,body,target,held,0,origin,p.Map.MapID);Log.Information("limited-corpse-result: passed");
            }
            catch(Exception e){Log.Error(e,"limited-corpse-result: failed");}
        }
        Observe();
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(AssassinSentryBody)World.FindMobile((Serial)saved.Actor);var target=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);
        Require(saved.Generation<2&&body is{Alive:false,Deleted:false}&&body.RetainedCorpse&&JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.Body)
            &&JsonSerializer.Serialize(LimitedSummonCorpses.Snapshot())==JsonSerializer.Serialize(saved.Pool)&&JsonSerializer.Serialize(AssassinSentryBolts.State(saved.Bolt.Id))==JsonSerializer.Serialize(saved.Bolt),"Original dead NPC/pool/held flight changed on reload");
        Require(!AssassinSentryActors.Actors().Contains(body)&&SkillHealth.Balance256(target)==saved.Health&&!ClassCombat.Available(p,CombatAbility.ChargedBoltSentry),"Reload restarted AI/damage or enabled unfinished skill");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);FixtureNetwork.Authenticate(network,p,"corpse-resume-");HoldBolt(saved.Bolt.Id,TimedEffects.Now.AddMilliseconds(40));long end=Core.TickCount+3000;
        void Observe()
        {
            try
            {
                var bolt=AssassinSentryBolts.State(saved.Bolt.Id);Require(bolt!=null&&bolt.Fault==null,"Original dead-owner flight disappeared/faulted before resume");
                if(bolt.Frame<saved.Bolt.Frame+2){Require(Core.TickCount<end,"Held corpse-owned flight did not resume");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(bolt.Actor==body.Serial.Value&&bolt.Owner==p.Serial.Value&&!body.Alive&&body.RetainedCorpse,"Resume changed actual dead source/caster");
                Save(p,body,target,saved.Bolt.Id,saved.Generation+1,saved.Origin,saved.Map);Log.Information("limited-corpse-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"limited-corpse-reload-result: failed");}
        }
        Observe();
    }
}
