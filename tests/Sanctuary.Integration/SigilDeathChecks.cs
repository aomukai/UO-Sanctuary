// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SigilDeathChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SigilDeathChecks));
    private sealed record Saved(int Generation,uint Owner,uint Target,int Life,SigilActor Actor,SigilDeathMark Mark);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sigil-death-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank)=>new(SkillMechanics.For(CombatAbility.SigilDeath),_=>rank,_=>0,99);
    private static SigilActor Actor(uint id)=>SigilActors.Snapshot().Actors.Single(a=>a.Marker==id);
    private static CleaveActionTarget Enemy(PlayerMobile p,int dx,int dy=0,int dz=0)
    {
        var t=new CleaveActionTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z+dz),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,t.HitsMax*256);
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(r,0);}return t;
    }
    private static void Detach(NetState n,PlayerMobile p){n.Mobile=null;p.NetState=null;}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"sigil-death-reload-check":"sigil-death-check";
            ConsoleInputHandler.RegisterCommand(name,"Complete prepared Death rule over actual Sigil/corpse services",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>Check(n,reload,name+"-result"));}));}
    }
    private static void Model()
    {
        foreach(int life in new[]{1,129,256,1024,10001,1000000})
        {
            ulong seed=SourceRandom.InitLow(173),expected=seed;int lo=(int)((long)life*70/100),hi=(int)((long)life*120/100);
            int total=SourceSpellPacketRoll.Range(ref expected,lo,hi);var packet=SigilDeathProposal.Packet(life,ref seed);
            Require(packet==new SigilDeathPacket(total/2,total/2)&&seed==expected,"Death packet percentage/split/stream or hidden scaling");
        }
        var raw=new GearDamage(10,10,0,0,0);var plain=DruidMoltenBoulder.Potion(new(0,50,0,0,0),raw);var enhanced=DruidMoltenBoulder.Potion(new(100,50,0,0,0),raw);
        Require(plain.Total==20&&enhanced.Total==110,"Death potion/mastery reuse changed unmastered base packet");
        Require(JsonSerializer.Deserialize<SigilActorSave>("{\"Actors\":[],\"Owners\":[]}").DeathMarks==null
            &&!JsonSerializer.Serialize(new SigilActorSave([],[])).Contains("DeathMarks"),"Legacy Sigil mark omission changed");
        Require(SigilDeathProposal.Approved&&WarlockCombat.Implemented(CombatAbility.SigilDeath),"Approved Death learned route missing");
    }
    private static void Exercise(NetState network,Action done,Action<Exception> failed)
    {
        var p=CleaveActionChecks.Fresh();FixtureNetwork.Authenticate(network,p,"DeathExercise");
        var targets=new List<CleaveActionTarget>();CleaveActionTarget E(int x,int y=0,int z=0){var t=Enemy(p,x,y,z);targets.Add(t);return t;}
        var victim=E(3);var edge=E(4);var outside=E(5);var high=E(3,1,16);var ally=E(3,-1);ally.ControlMaster=p;ally.Controlled=true;
        var outerPhysical=E(7);outerPhysical.SetResistance(ResistanceType.Fire,100);var physicalImmune=E(3,-2);physicalImmune.SetResistance(ResistanceType.Physical,100);
        void Put(params SigilDeathMark[] marks)
        {
            var saved=SigilActors.Snapshot();var ours=targets.Select(t=>t.Serial.Value).ToHashSet();
            SigilActors.Restore(saved with{DeathMarks=(saved.DeathMarks??[]).Where(m=>!ours.Contains(m.Target)).Concat(marks).ToArray()});
        }
        var corpses=new List<Corpse>();var prior=ClassLootHooks.CreatureDied;bool auto=false;int births=0;ulong initial=SourceRandom.InitLow(99);
        void Pulse(SigilActor actor,PlayerMobile owner,DateTime at){if(owner==p){SigilDeathProposal.PulsePrepared(actor,owner,at,()=>{births++;return initial;});}}
        void Death(BaseCreature actor,Container container)
        {
            prior?.Invoke(actor,container);
            if(SigilDeathProposal.State(actor)!=null&&container is Corpse c){c.DropItem(new Gold(1));corpses.Add(c);SigilDeathProposal.QueuePrepared(actor,c,TimedEffects.Now,auto);}
        }
        void Cleanup()
        {
            ClassLootHooks.CreatureDied=prior;SigilActors.Pulse-=Pulse;SigilDeathProposal.DrainPrepared(false);
            foreach(var t in targets){SigilDeathProposal.Clear(t);if(!t.Deleted){t.Delete();}}
            foreach(var c in corpses){if(!c.Deleted){c.Delete();}}SigilActors.Clear(p);Detach(network,p);p.Delete();
        }
        SigilActors.Pulse+=Pulse;ClassLootHooks.CreatureDied=Death;
        try
        {
            var now=TimedEffects.Now;var point=new Point3D(p.X+2,p.Y,p.Z);int mana=NecromancerEffects.ManaBalance256(p);
            var actor=SigilActors.CreatePaid(p,Values(1),point,now,Values(1).Mana256);Require(actor!=null&&mana-NecromancerEffects.ManaBalance256(p)==10*256,"Death once table mana/placement commit");
            SigilActors.Tick(now);var mark=SigilDeathProposal.State(victim);int after=births;
            Require(mark is{Rank:1}&&mark.Modifiers==actor.DeathModifiers&&SigilDeathProposal.State(edge)!=null&&SigilDeathProposal.State(outside)==null&&SigilDeathProposal.State(high)==null&&SigilDeathProposal.State(ally)==null,"Death primary radius/height/ally/captured birth");
            Require(!SigilDeathProposal.PulsePrepared(Actor(actor.Marker),p,now)&&births==after,"Death duplicate application cursor advanced seed");
            now=now.AddMilliseconds(240);SigilActors.Tick(now);Require(SigilDeathProposal.State(victim).Seed==mark.Seed&&births==after,"Equal refresh replaced stream");
            int savedMana=NecromancerEffects.ManaBalance256(p);NecromancerEffects.SetMana256(p,0);var before=JsonSerializer.Serialize(SigilActors.Snapshot());
            Require(SigilActors.CreatePaid(p,Values(1),point,now,Values(1).Mana256)==null&&JsonSerializer.Serialize(SigilActors.Snapshot())==before,"Underfunded Death replaced marker/state");NecromancerEffects.SetMana256(p,savedMana);
            // Real native corpse capture, then drain after native death cleanup.
            mark=SigilDeathProposal.State(victim);ulong stream=mark.Seed;int innerHp=SkillHealth.Balance256(edge),outerHp=SkillHealth.Balance256(outerPhysical),fireHp=SkillHealth.Balance256(physicalImmune);
            victim.LastKiller=p;victim.Kill();var corpse=corpses.Last();var life=CorpseSkillRules.Profiles.Find(corpse).BaseLife;
            var packet=SigilDeathProposal.Packet(life.Base256,ref stream);var bursts=SigilDeathProposal.DrainPrepared(true);
            Require(bursts.Length==1&&bursts[0].Exploded&&bursts[0].Packet==packet&&innerHp-SkillHealth.Balance256(edge)==packet.Physical256+packet.Fire256
                &&outerHp-SkillHealth.Balance256(outerPhysical)==packet.Physical256&&fireHp-SkillHealth.Balance256(physicalImmune)==packet.Fire256,$"Death actual packet/range: bursts={JsonSerializer.Serialize(bursts)} expected={packet} loss={innerHp-SkillHealth.Balance256(edge)}/{outerHp-SkillHealth.Balance256(outerPhysical)}/{fireHp-SkillHealth.Balance256(physicalImmune)} available={CorpseSkillRules.Available(corpse)} loot={corpse.CanLoot(p,null)} criminal={corpse.IsCriminalAction(p)}");
            Require(corpse.SanctuaryConsumed&&corpse.Items.OfType<Gold>().Single().Amount==1&&!corpse.CanLoot(p,null)&&SigilDeathProposal.State(victim)==null&&!SigilDeathProposal.QueuePrepared(victim,corpse,TimedEffects.Now),"Corpse/mark repeated or loot destroyed");
            // A stronger marker replaces rank/creator payload, preserving stream;
            // a later weaker marker cannot downgrade an active mark.
            var strong=SigilActors.CreatePrepared(p,Values(20),point,TimedEffects.Now);SigilActors.Tick(strong.BornUtc);var strongest=SigilDeathProposal.State(edge);Require(strongest.Rank==20,"Stronger Death mark missing");
            var weak=SigilActors.CreatePrepared(p,Values(1),point,TimedEffects.Now);SigilActors.Tick(weak.BornUtc);Require(SigilDeathProposal.State(edge).Rank==20,"Weaker Death marker downgraded recipient");
            SigilActors.Clear(p);foreach(var t in targets){SigilDeathProposal.Clear(t);}
            // Expired mark, missing corpse and owner inactivity never replay.
            var expired=strongest with{Target=edge.Serial.Value,UntilUtc=TimedEffects.Now.AddMilliseconds(-1)};
            Put(expired);edge.Kill();Require(!SigilDeathProposal.QueuePrepared(edge,edge.Corpse,TimedEffects.Now),"Expired Death mark triggered");
            // Recipient leases do not become deferred attacks on logout,
            // facet separation or owner death. Their death is claimed once.
            for(int mode=0;mode<3;mode++)
            {
                var blocked=E(8,-mode);var owner=mode==2?CleaveActionChecks.Fresh():p;var position=p.Location;
                Put(new SigilDeathMark(blocked.Serial.Value,owner.Serial.Value,p.Map.MapID,1,TimedEffects.Now.AddSeconds(10),new(0,0,0,0,0),SourceRandom.InitLow(7)));
                if(mode==0){Detach(network,p);}else if(mode==1){p.MoveToWorld(position,Map.Felucca);}else{owner.Kill();}
                blocked.Kill();var body=corpses.Last();
                if(mode==0)
                {
                    var pending=SigilActors.Snapshot();bool refused=false;try{SigilActors.Restore(pending);}catch(InvalidDataException){refused=true;}
                    Require(refused,"Pending Death queue allowed a replaying restore");
                }
                var refusedBurst=SigilDeathProposal.DrainPrepared(mode==2?true:null);
                Require(refusedBurst.Length==1&&!refusedBurst[0].Exploded&&!body.SanctuaryConsumed&&SigilDeathProposal.State(blocked)==null,"Inactive/dead owner consumed a corpse or retained a death replay");
                if(mode==0){network.Mobile=p;p.NetState=network;}else if(mode==1){p.MoveToWorld(position,Map.Trammel);}else{owner.Corpse?.Delete();owner.Delete();}
            }
            // Automatic post-loop FIFO: secondary deaths enqueue their own
            // existing marks while parent secondary HP events stay suppressed.
            var first=E(10);var next=E(11);var survivor=E(12);SkillHealth.SetBalance256(next,256);
            var seed=SourceRandom.InitLow(7);var mods=new FirestormModifiers(0,0,0,0,0);var until=TimedEffects.Now.AddSeconds(5);
            Put(new(first.Serial.Value,p.Serial.Value,p.Map.MapID,1,until,mods,seed),new(next.Serial.Value,p.Serial.Value,p.Map.MapID,1,until,mods,seed));
            int hp=SkillHealth.Balance256(survivor);auto=true;first.LastKiller=p;first.Kill();
            Core.LoopContext.Post(()=>
            {
                try
                {
                    Require(!next.Alive&&corpses.Count(c=>c.SanctuaryConsumed)>=3&&SkillHealth.Balance256(survivor)<hp&&SigilDeathProposal.State(first)==null&&SigilDeathProposal.State(next)==null,"Queued post-death FIFO did not consume/chain marked deaths");
                    Require(SigilDeathProposal.DrainPrepared(true).Length==0,"Death queue replayed");Cleanup();done();
                }
                catch(Exception e){Cleanup();failed(e);}
            });
        }
        catch(Exception e){Cleanup();failed(e);}
    }
    private static void Check(NetState network,bool reload,string result)
    {
        SigilActors.Pulse-=SigilDeathEvents.Pulse;ClassLootHooks.CreatureDied-=SigilDeathEvents.Death;
        void Restore(){SigilActors.Pulse+=SigilDeathEvents.Pulse;ClassLootHooks.CreatureDied+=SigilDeathEvents.Death;}
        try{Model();}catch(Exception e){Restore();Log.Error(e,"{Name}: failed",result);return;}
        Exercise(network,()=>
        {
            try
            {
                Saved old=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):null;
                var p=reload?(PlayerMobile)World.FindMobile((Serial)old.Owner):CleaveActionChecks.Fresh();var t=reload?(CleaveActionTarget)World.FindMobile((Serial)old.Target):Enemy(p,3);
                SigilActor actor;
                if(reload)
                {
                    actor=Actor(old.Actor.Marker);Require(actor.BornUtc==old.Actor.BornUtc&&actor.DeathModifiers==old.Actor.DeathModifiers&&actor.DeathPulse==old.Actor.DeathPulse
                        &&SigilDeathProposal.State(t)==old.Mark&&SkillHealth.Balance256(t)==old.Life,"Original Death actor/cursor/mark/stream/wounds changed");
                }
                else{actor=SigilActors.CreatePrepared(p,Values(200),new(p.X+2,p.Y,p.Z),TimedEffects.Now);}
                FixtureNetwork.Authenticate(network,p,"DeathSaved");int calls=0;
                void Pulse(SigilActor a,PlayerMobile owner,DateTime at){if(a.Marker==actor.Marker){Require(SigilDeathProposal.PulsePrepared(a,owner,at,()=>SourceRandom.InitLow(31337)),"Saved Death pulse not claimed");calls++;}}
                SigilActors.Pulse+=Pulse;
                try{int frame=Math.Max(actor.LastPulse+6,checked((int)((TimedEffects.Now-actor.BornUtc).TotalMilliseconds/240))*6+6);SigilActors.Tick(actor.BornUtc.AddMilliseconds(frame*40L));Require(calls==1,"Original Death marker did not resume one pulse");}
                finally{SigilActors.Pulse-=Pulse;}
                var mark=SigilDeathProposal.State(t);var held=mark with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
                var snapshot=SigilActors.Snapshot();SigilActors.Restore(snapshot with{DeathMarks=(snapshot.DeathMarks??[]).Where(m=>m.Target!=held.Target).Append(held).ToArray()});SkillHealth.SetBalance256(t,t.HitsMax*256-129);
                var good=SigilActors.Snapshot();string before=JsonSerializer.Serialize(good);bool refused=false;
                try{SigilActors.Restore(good with{DeathMarks=[held with{Seed=0}]});}catch(InvalidDataException){refused=true;}
                Require(refused&&JsonSerializer.Serialize(SigilActors.Snapshot())==before,"Malformed Death mark restore changed shared actor state");
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved((old?.Generation??-1)+1,p.Serial.Value,t.Serial.Value,SkillHealth.Balance256(t),Actor(actor.Marker),held)));
                Log.Information("Death candidate:shared paid marker/pulse,base corpse snapshot,full-circle split,loot retention,post-death FIFO chain,expiry/refresh/atomic old schema and original cursor/stream/wounds pass;prepared-model scope;learned route acceptance is checked separately.");
                Log.Information("{Name}: passed",result);
            }
            catch(Exception e){Log.Error(e,"{Name}: failed",result);}finally{Restore();}
        },e=>{Restore();Log.Error(e,"{Name}: failed",result);});
    }
}
