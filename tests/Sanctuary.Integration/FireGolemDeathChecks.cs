// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
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

public static class FireGolemDeathChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireGolemDeathChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Target,int Life,uint HistoricalOwner,uint Historical,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-golem-death-fixtures.json");
    private static readonly HashSet<uint> Restored=new();
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        FireGolemDeathBody.Restored=b=>{Require(!b.Alive&&!b.Deleted&&b.Retirement!=null,"Restored retirement must keep original dead registration");Restored.Add(b.Serial.Value);};
        Register("fire-golem-death-check",Check);Register("fire-golem-death-reload-check",Reload);Register("fire-golem-death-save",Save);
        ConsoleInputHandler.RegisterCommand("fire-golem-death-live","Connected natural/replacement Fire death bursts",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("fire-golem-death-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original Fire Golem death retirement, ownership and save controls",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureFireDeath";
        void Train(string id,int rank){TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
        Train("necromancer.firegolem",4);return p;
    }
    private static FireGolemDeathBody Pet(PlayerMobile p)
    {
        p.Mana=p.ManaMax;
        Require(NecromancerCombat.Release(p,CombatAbility.FireGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Learned Fire death subtype creation");
        var b=NecromancerGolems.For(p).OfType<FireGolemDeathBody>().Single();b.AIObject.AITimer.Stop();return b;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x,int y)
    {
        var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(x,y,p.Z),p.Map);t.SetHits(20000);t.Hits=t.HitsMax;
        t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Fire,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static void Check()
    {
        var p=Owner();var b=Pet(p);b.Delete();Require(b.Deleted,"Direct cleanup must not manufacture a death burst");p.Delete();
        var historicalOwner=Owner();var old=new FireGolemBody();old.Prepare(historicalOwner,CombatAbility.FireGolem);Require(old.SetControlMaster(historicalOwner),"Previous typed Fire owner");
        PersistentMinions.Profiles.Track(old);old.Internalize();old.AIObject.AITimer.Stop();historicalOwner.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,0,0,0,historicalOwner.Serial.Value,old.Serial.Value,0)));
    }
    private static void Save()
    {
        var previous=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=Owner();var b=Pet(p);var target=Enemy(p,b.X+1,b.Y);
        int life=SkillHealth.Balance256(target);b.Kill();
        Require(!b.Alive&&!b.Deleted&&World.FindMobile(b.Serial)==b&&b.ControlMaster==p&&NecromancerGolems.For(p).Length==0&&b.Frozen,"Death must retain the original source outside the live roster");
        Require(b.Retirement.DueUtc>TimedEffects.Now&&b.Retirement.DueUtc<=TimedEffects.Now.AddMilliseconds(160)&&SkillHealth.Balance256(target)==life,"Death burst was immediate or deadline exceeded four frames");
        // Fixture-only retention hold spans asynchronous heap preparation.
        // Keep the real saved UTC expiry untouched: it expires before reload.
        // The normal160ms deletion/damage deadline is checked by Live separately.
        var clock=typeof(FireGolemDeathBody).GetField("_retireTick",BindingFlags.NonPublic|BindingFlags.Instance);
        Require(clock?.FieldType==typeof(long),"Fixture retirement clock unavailable");clock.SetValue(b,Core.TickCount+60000);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(previous with{Owner=p.Serial.Value,Actor=b.Serial.Value,Target=target.Serial.Value,Life=life}));
        World.Save();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(Restored.Contains(saved.Actor)&&World.FindMobile((Serial)saved.Actor)==null,$"Original pending actor was not serialized/restored and expired once: actor={saved.Actor}, restored={Restored.Contains(saved.Actor)}, present={World.FindMobile((Serial)saved.Actor)!=null}");
        var target=(BaseCreature)World.FindMobile((Serial)saved.Target);Require(SkillHealth.Balance256(target)==saved.Life,"Expired offline retirement replayed damage");
        Require(NpcEquipmentRandom.Snapshot().All(x=>x.Actor!=saved.Actor)&&PaladinAuras.Snapshot().Leases.All(x=>x.Target!=saved.Actor),"Expired actor leaked RNG/aura state");
        var old=World.FindMobile((Serial)saved.Historical);Require(old?.GetType()==typeof(FireGolemBody)&&((FireGolemBody)old).ControlMaster?.Serial.Value==saved.HistoricalOwner,"Previous FireGolemBody v0 save was silently migrated");
        target.Delete();World.FindMobile((Serial)saved.Owner)?.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("fire-golem-death-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;network.Mobile=p;p.NetState=network;
        FixtureSpace.Corridor(p,6100,24,3600);
        var originalShields=SourceBlocking.Snapshot();var originalBlock=SourceBlocking.BlockRoll;
        var actors=new List<FireGolemDeathBody>();var enemies=new List<BaseCreature>();bool cleaned=false;
        void Clean(){if(cleaned){return;}cleaned=true;SourceBlocking.Restore(originalShields);SourceBlocking.BlockRoll=originalBlock;foreach(var b in actors){b.Delete();}foreach(var t in enemies){t.Corpse?.Delete();t.Delete();}foreach(var pet in NecromancerGolems.For(p)){pet.Delete();}p.Delete();account.Delete();}
        void Run(int phase)
        {
            try
            {
                var b=Pet(p);actors.Add(b);var a=Enemy(p,b.X+1,b.Y);var c=Enemy(p,b.X+2,b.Y);enemies.Add(a);enemies.Add(c);
                a.SetResistance(ResistanceType.Physical,50);c.SetResistance(ResistanceType.Fire,50);
                bool blocked=phase==2;SourceBlocking.BlockRoll=_=>blocked?0:99;
                if(blocked)
                {
                    var source=CreatureProfiles.Definition(a).Reference;SourceBlocking.Restore(originalShields.Append(new(a.Serial.Value,source,1)).Append(new(c.Serial.Value,source,0)).ToArray());
                    Require(SourceBlocking.TryRate(a,out int ar,out _)&&ar>0&&SourceBlocking.TryRate(c,out int cr,out _)&&cr==0,"Shield/no-shield controls");
                }
                int seed=phase==0?0:42;NpcEquipmentRandom.Set(b,SourceRandom.InitLow(seed));int first=SkillHealth.Balance256(a),second=SkillHealth.Balance256(c);long recovery=CreatureProfiles.Session(c).RecoveryUntil;
                if(phase==1){a.Hits=300;first=SkillHealth.Balance256(a);}
                bool early=false;
                if(phase==1)
                {
                    p.Mana=p.ManaMax;int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.ClayGolem).Mana256;
                    Require(NecromancerCombat.Release(p,CombatAbility.ClayGolem,Serial.Zero,new(p.X,p.Y+1,p.Z)),"Actual replacement cast");
                    Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&NecromancerGolems.For(p).Single() is ClayGolemBody,"Replacement must pay once and retain only the new active golem");
                }
                else{b.Damage(b.HitsMax+100,a);}
                Require(!b.Alive&&!b.Deleted&&World.FindMobile(b.Serial)==b&&b.GetMaster()==p&&b.Frozen&&b.PendingContact==null,"Natural/replacement death lost original identity/ownership/actions");
                Timer.StartTimer(TimeSpan.FromMilliseconds(100),()=>
                {try{Require(!b.Deleted&&SkillHealth.Balance256(a)==first&&SkillHealth.Balance256(c)==second,"Burst or deletion before the four-frame deadline");early=true;}catch(Exception e){Clean();Log.Error(e,"fire-golem-death-live-result: failed");}});
                Timer.StartTimer(TimeSpan.FromMilliseconds(280),()=>
                {
                    if(cleaned){return;}
                    try
                    {
                        int aLoss=blocked?0:phase==1?300*256:150*256;
                        Require(early&&b.Deleted&&World.FindMobile(b.Serial)==null&&NpcEquipmentRandom.State(b)==null,"Retired source did not finish/deallocate once");
                        Require(first-SkillHealth.Balance256(a)==aLoss&&second-SkillHealth.Balance256(c)==150*256,"Fixed mixed packet,per-recipient critical or blocked-recipient RNG order differs");
                        if(phase==1){Require(!a.Alive&&a.LastKiller==b&&ClassLoot.Killer(a)==p,"Replacement burst lost original-source owner kill credit");}
                        Require(CreatureProfiles.Session(c).RecoveryUntil==recovery,"Missile117 requested ordinary hit recovery");
                        SourceBlocking.Restore(originalShields);a.Corpse?.Delete();a.Delete();c.Corpse?.Delete();c.Delete();foreach(var pet in NecromancerGolems.For(p)){pet.Delete();}
                        if(phase<2){Run(phase+1);}else{Clean();Log.Information("fire-golem-death-live-result: passed");}
                    }
                    catch(Exception e){Clean();Log.Error(e,"fire-golem-death-live-result: failed");}
                });
            }
            catch(Exception e){Clean();Log.Error(e,"fire-golem-death-live-result: failed");}
        }
        FixtureClock.NextLoop("fire-golem-death-live-result",()=>{Log.Information("fire-golem-death-live: ready");Run(0);});
    }
}
