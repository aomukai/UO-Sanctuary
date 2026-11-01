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

public static class TelekinesisCombatChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TelekinesisCombatChecks));
    private sealed record Saved(uint Owner,uint Target,int Map,Point3D Point,int Mana,int Life,SourceKnockbackState Motion,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"telekinesis-combat-fixtures.json");
    public static void Configure()
    {
        var old=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="TelekinesisCombatFixture"&&old?.Invoke(p)!=false;
        Register("telekinesis-combat-check",Check);
        Register("telekinesis-combat-delivery-check",Delivery);
        Register("telekinesis-combat-reload-check",()=>FixtureClock.NextLoop("telekinesis-combat-reload-result",()=>FixtureNetwork.Connected("telekinesis-combat-reload-result",Reload)));
        foreach(bool q in new[]{false,true}){bool quick=q;string label=q?"telekinesis-combat-quick":"telekinesis-combat-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Telekinesis source packet/ordered NPC return/saved trajectory",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,5870);p.Name="TelekinesisCombatFixture";
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.telekinesis",out _),"Learn TK");
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Telekinesis)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Telekinesis),"TK combat binding");return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx=4)
    {var t=FireBoltChecks.Target(p,dx);t.Frozen=t.CantWalk=false;t.AIObject?.AITimer.Stop();return t;}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {foreach(var t in targets){SourceKnockbacks.Clear(t);CombatStuns.Clear(t);}FireBoltChecks.Clean(p,targets);}
    private static void Check(){Source();Delivery();Controls();Save();Log.Information("Telekinesis combat:7680complete-source roll comparisons,2560native NPC HP/return compositions,255rank goldens;actual payment/typed potion/mastery/slow independence/immune knockback/frozen suspension and saved original trajectory pass");}
    private static void Source()
    {
        var p=Owner();var t=Target(p);var prior=TelekinesisCombat.DefenderRoll;int rolls=0,deliveries=0,deaths=0,pushes=0;try
        {
            var row=SkillMechanics.For(CombatAbility.Telekinesis);
            for(int rank=1;rank<=255;rank++)
            {
                var v=new SkillValues(row,k=>k==row.SourceKey?rank:255,_=>20,99);var prepared=TelekinesisCombat.Prepare(p,v);
                Require(prepared.Rank==rank&&prepared.Minimum256==rank*256&&prepared.Maximum256==(rank+1)*256&&v.Mana256==1792,"Independent TK endpoints "+rank);
            }
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"telekinesis-combat-source-cases.json")));
            foreach(var entry in doc.RootElement.EnumerateArray())
            {
                var i=entry[0];var o=entry[1];int V(int n)=>i[n].GetInt32();int rank=V(0);uint low=i[1].GetUInt32();ulong seed=SourceRandom.InitLow(unchecked((int)low));
                var raw=TelekinesisCombat.Roll(new(rank,rank*256,(rank+1)*256,new(0,0,0,0,0)),ref seed);
                Require(raw.Lightning256==o[1].GetInt32()&&raw.ResultFlags==o[3].GetInt32()&&seed==o[9].GetUInt64()&&raw.HitFlags==2&&raw.HitClass==109&&raw.StunFrames==0,"Complete-source TK roll "+rolls);rolls++;
                if(t.Deleted||!t.Alive){t.Corpse?.Delete();t.Delete();t=Target(p);}
                int modes=NativeMonsterReturn.Metadata(t).Modes&((1<<3)|(1<<13));if(V(7)!=modes){continue;}
                SourceKnockbacks.Clear(t);CombatStuns.Clear(t);CreatureProfiles.Session(t).RecoveryUntil=0;
                t.SetHits(V(3)/256);SkillHealth.SetBalance256(t,V(4));t.SetResistance(ResistanceType.Energy,V(2));t.Frozen=V(5)!=0;
                if(V(6)!=0){Require(CombatStuns.ApplyPreparedDuration(t,250,TimedEffects.Now),"Existing source stun setup");}
                ulong defender=SourceRandom.InitLow(unchecked((int)(low+1)));TelekinesisCombat.DefenderRoll=()=>SourceRandom.Roll(ref defender);
                var state=NativeMonsterReturn.Session(t);state.LastMode=-1;int packets=state.Packets;
                var result=TelekinesisCombat.Deliver(p,t,raw,new(0,0,0,0,0),TimedEffects.Now);
                Require(result.Packet.Total256==o[2].GetInt32()&&result.Packet.ResultFlags==o[5].GetInt32()&&result.AfterHealth256==o[6].GetInt32()
                    &&state.LastMode==o[7].GetInt32()&&result.DeathModeAttempted==(o[8].GetInt32()!=0)&&defender==o[10].GetUInt64()
                    &&state.Packets==packets+1&&result.Packet.HitClass==109&&result.Packet.HitFlags==2,"Native TK source HP/return composition "+deliveries+" input="+i.GetRawText());
                if(result.DeathModeAttempted){Require(!t.Alive||t.Deleted,"TK native death mode omitted");deaths++;}
                if(state.LastMode==13){Require(SourceKnockbacks.Snapshot().Single(s=>s.Target==t.Serial.Value).PauseFrozen,"Prepared source trajectory missing");pushes++;}
                deliveries++;
            }
            Require(rolls==7680&&deliveries==2560&&deaths>0&&pushes>0,"Incomplete TK source/native contexts");
            Log.Information("TK source/native:rolls={Rolls} deliveries={Deliveries} deaths={Deaths} pushes={Pushes}",rolls,deliveries,deaths,pushes);
        }finally{TelekinesisCombat.DefenderRoll=prior;Clean(p,t);}
    }
    private static void Delivery()
    {
        var p=Owner();var t=Target(p);var old=TelekinesisCombat.SeedLow;TelekinesisCombat.SeedLow=()=>0;try
        {
            int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t),ownerLife=SkillHealth.Balance256(p);
            Require(SorceressCombat.Release(p,CombatAbility.Telekinesis,t.Serial,t.Location)&&life-SkillHealth.Balance256(t)==410&&NecromancerEffects.ManaBalance256(p)==mana-1792,"Actual TK FP roll/one payment");
            Require(SkillHealth.Balance256(p)==ownerLife&&CombatStuns.Remaining(t)==0,"TK reflected melee damage or invented timed stun");SourceKnockbacks.Clear(t);
            Require(CombatPotions.Apply(p,3),"TK potion");life=SkillHealth.Balance256(t);Require(SorceressCombat.Release(p,CombatAbility.Telekinesis,t.Serial,t.Location)&&life-SkillHealth.Balance256(t)==574,"TK separate potion lanes");TimedEffects.Clear(p);SourceKnockbacks.Clear(t);
            var plain=TelekinesisCombat.Prepare(p,SkillMechanics.Values(p,CombatAbility.Telekinesis));
            AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddMinutes(1))).ToArray());
            Require(TelekinesisCombat.Prepare(p,SkillMechanics.Values(p,CombatAbility.Telekinesis))==plain,"TK imported a missile-factory slow payload");AmazonSlowMissiles.Clear(p);
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightningmastery",out _),"TK lightning mastery");var mastered=TelekinesisCombat.Prepare(p,SkillMechanics.Values(p,CombatAbility.Telekinesis));Require(mastered.Minimum256==384&&mastered.Maximum256==768,"TK live mastery");
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Require(TelekinesisCombat.Prepare(p,SkillMechanics.Values(p,CombatAbility.Telekinesis)).Minimum256>mastered.Minimum256,"TK actual gear");p.Backpack.DropItem(orb);ClassGear.Refresh(p);
            t.SetResistance(ResistanceType.Energy,100);bool push=false;
            for(int seed=0;seed<64&&!push;seed++)
            {TelekinesisCombat.SeedLow=()=>seed;life=SkillHealth.Balance256(t);Require(SorceressCombat.Release(p,CombatAbility.Telekinesis,t.Serial,t.Location)&&SkillHealth.Balance256(t)==life,"TK immunity");push=SourceKnockbacks.Snapshot().Any(s=>s.Target==t.Serial.Value);}
            Require(push,"Immune TK never requested knockback");var s=SourceKnockbacks.Snapshot().Single(s=>s.Target==t.Serial.Value);var position=t.Location;
            t.Frozen=true;SourceKnockbacks.Tick(s.NextUtc);var held=SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value);Require(t.Location==position&&held.Travel16==s.Travel16&&held.Budget16==s.Budget16,"Frozen prepared trajectory advanced");t.Frozen=false;
            Through(t);var end=SourceKnockbacks.Snapshot().SingleOrDefault(x=>x.Target==t.Serial.Value);var returned=NativeMonsterReturn.Session(t);
            Log.Information("TK motion:from={From} to={To} mode={Mode} class={Class} frozen={Frozen} paralyzed={Paralyzed} state={State}",position,t.Location,returned.LastMode,returned.LastHitClass,t.Frozen,t.Paralyzed,end);
            Require(t.Location!=position&&returned.LastHitClass==160&&returned.LastMode==PreparedMonsterReturn.GetHit,"Prepared TK displacement/post-knockback GH class");
        }finally{TelekinesisCombat.SeedLow=old;Clean(p,t);}
    }
    private static void Through(BaseCreature t)
    {for(int frame=0;frame<20;frame++){var s=SourceKnockbacks.Snapshot().SingleOrDefault(x=>x.Target==t.Serial.Value);if(s==null||s.Recovering){return;}SourceKnockbacks.Tick(s.NextUtc);}}
    private static void Controls()
    {
        var p=Owner();var t=Target(p);try
        {
            NecromancerEffects.SetMana256(p,1791);int life=SkillHealth.Balance256(t);Require(!SorceressCombat.Release(p,CombatAbility.Telekinesis,t.Serial,t.Location)&&NecromancerEffects.ManaBalance256(p)==1791&&SkillHealth.Balance256(t)==life,"Insufficient TK mana");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(SorceressCombat.Begin(p,CombatAbility.Telekinesis,t.Serial,t.Location,Core.TickCount),"TK windup");long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==life,"Canceled TK paid/hit");
            t.IsParagon=true;Require(!TelekinesisCombat.Supported(p,t,out var reason)&&reason.Contains("Paragon")&&!SorceressCombat.Release(p,CombatAbility.Telekinesis,t.Serial,t.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Unsupported native modifier silently substituted");t.IsParagon=false;
            var other=Owner();try{Require(!TelekinesisCombat.Supported(p,other,out _),"Player return silently used NPC policy");}finally{Clean(other);}
            var historical=SourceKnockbacks.Snapshot();var at=TimedEffects.Now;var legacy=new SourceKnockbackState(t.Serial.Value,t.Map.MapID,t.Location,new(t.X+1,t.Y,t.Z),98304,0,0,at,false,default);
            var json=JsonSerializer.Serialize(legacy);Require(!json.Contains("PauseFrozen"),"Legacy source trajectory format changed");SourceKnockbacks.Restore([legacy]);Require(!SourceKnockbacks.Snapshot()[0].PauseFrozen,"Legacy trajectory gained new policy");SourceKnockbacks.Restore(historical);
        }finally{Clean(p,t);}
    }
    private static void Save()
    {
        var p=Owner();var t=Target(p);var old=TelekinesisCombat.SeedLow;bool begun=false;try
        {
            for(int seed=0;seed<64&&!begun;seed++){TelekinesisCombat.SeedLow=()=>seed;Require(SorceressCombat.Release(p,CombatAbility.Telekinesis,t.Serial,t.Location),"Saved actual TK cast");begun=SourceKnockbacks.Snapshot().Any(s=>s.Target==t.Serial.Value);}
        }finally{TelekinesisCombat.SeedLow=old;}
        Require(begun,"Saved TK source trajectory missing");var motion=SourceKnockbacks.Snapshot().Single(s=>s.Target==t.Serial.Value);var hold=motion with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};SourceKnockbacks.Restore(SourceKnockbacks.Snapshot().Select(s=>s.Target==t.Serial.Value?hold:s).ToArray());
        var saved=new Saved(p.Serial.Value,t.Serial.Value,p.Map.MapID,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),hold,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value)==s.Motion&&SkillHealth.Balance256(t)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Original TK motion/resources/wounds did not persist");
        var account=FixtureNetwork.Authenticate(network,p,"telekinesis-combat-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var position=t.Location;SourceKnockbacks.Restore(SourceKnockbacks.Snapshot().Select(x=>x.Target==t.Serial.Value?x with{NextUtc=TimedEffects.Now.AddMilliseconds(100)}:x).ToArray());long deadline=Core.TickCount+3000;
        void Finish(SourceKnockbackState motion)
        {
            p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Motion=motion,Mana=NecromancerEffects.ManaBalance256(p),Life=SkillHealth.Balance256(t),Generation=s.Generation+1}));Log.Information("telekinesis-combat-reload-result: passed");
        }
        void Observe()
        {
            if(t.Location==position&&Core.TickCount<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
            try
            {
                Require(t.Location!=position&&SkillHealth.Balance256(t)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Actual resumed TK trajectory changed resources/wounds or failed displacement");
                if(s.Generation==0)
                {
                    // One native-tile path cannot move twice. Make another
                    // real paid cast on the same wounded original recipient;
                    // never rewind its position, life or resources for reload2.
                    var prepared=TelekinesisCombat.Prepare(p,SkillMechanics.Values(p,CombatAbility.Telekinesis));int low=0;
                    for(;low<100;low++){ulong seed=SourceRandom.InitLow(low);if((TelekinesisCombat.Roll(prepared,ref seed).ResultFlags&8)!=0){break;}}
                    Require(low<100,"No deterministic KB seed");var old=TelekinesisCombat.SeedLow;TelekinesisCombat.SeedLow=()=>low;
                    try{Require(SorceressCombat.Release(p,CombatAbility.Telekinesis,t.Serial,t.Location),"Second actual saved TK cast");}finally{TelekinesisCombat.SeedLow=old;}
                    Require(NecromancerEffects.ManaBalance256(p)==s.Mana-1792&&SkillHealth.Balance256(t)<s.Life,"Second saved TK restored resources/wounds");
                    var motion=SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
                    SourceKnockbacks.Restore(SourceKnockbacks.Snapshot().Select(x=>x.Target==t.Serial.Value?motion:x).ToArray());Finish(motion);return;
                }
                void Expired()
                {
                    try
                    {
                        Require(Core.TickCount<deadline,"TK final recovery expiry timeout");
                        if(SourceKnockbacks.Snapshot().Any(x=>x.Target==t.Serial.Value)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Expired);return;}
                        Require(SkillHealth.Balance256(t)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Completed TK changed saved resources/wounds");Finish(null);
                    }catch(Exception e){Log.Error(e,"telekinesis-combat-reload-result: failed");}
                }
                Expired();
            }catch(Exception e){Log.Error(e,"telekinesis-combat-reload-result: failed");}
        }
        FixtureClock.NextLoop("telekinesis-combat-reload-result",Observe);
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p);var account=FixtureNetwork.Authenticate(network,p,"telekinesis-combat-input-");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"TK combat input timeout");var a=ClassCombat.For(p);
                if(a.PendingAbility==CombatAbility.Telekinesis&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==life,"TK paid/damaged at windup");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-1792&&NativeMonsterReturn.Session(t).LastHitClass==109&&CombatStuns.Remaining(t)==0,"Actual TK input packet/cost/stun");Clean(p,t);account.Delete();Log.Information("{Label}-result: passed",label);
            }catch(Exception e){Clean(p,t);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Telekinesis,(int)DiabloClass.Sorceress,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
