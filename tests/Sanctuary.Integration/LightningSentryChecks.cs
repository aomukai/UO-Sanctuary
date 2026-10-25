// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class LightningSentryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LightningSentryChecks));
    private sealed record Saved(uint Owner,uint Actor,uint First,uint Second,SentryBodyState Body,SentryLightningState Bolt,int Health1,int Health2,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lightning-sentry-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("lightning-sentry-check",Check);Register("lightning-sentry-reload-check",Reload);Register("lightning-sentry-live-check",Live);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared real Lightning Sentry/bolt persistence; learned input and retirement gated",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,CombatAbility ability,int rank)
    {string id=SkillMechanics.For(ability).TalentId;while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static AssassinSentryBody Actor(PlayerMobile p,DateTime at)=>AssassinSentryBody.CreateEmerging(p,new(p.X+2,p.Y,p.Z),at,SourceRandom.InitLow(19),CombatAbility.LightningSentry);
    private static BladeMissileTarget Target(AssassinSentryBody b,int dx=2)
    {
        var t=new BladeMissileTarget{Name="LightningSentryTarget",NoKillAwards=true};t.MoveToWorld(new(b.X+dx,b.Y,b.Z),b.Map);t.AIObject?.AITimer.Stop();
        t.SetHits(100000);t.Hits=t.HitsMax;foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static void Frames(long id,int count,bool actions)
    {for(int i=0;i<count&&AssassinSentryLightning.State(id) is{} s;i++){AssassinSentryLightning.TickFrame(s.NextUtc,actions);}}
    private static void Drain(AssassinSentryBody b)
    {foreach(var s in AssassinSentryLightning.Snapshot().Bolts.Where(s=>s.Actor==b.Serial.Value)){Frames(s.Id,s.Frames,false);}}
    private static void Creation()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"lightning-sentry-damage-source-probe.json")));
        var fields=doc.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();
        int min=Array.IndexOf(fields,"nLightMinDamage"),max=Array.IndexOf(fields,"nLightMaxDamage"),count=0;
        var row=SkillMechanics.For(CombatAbility.LightningSentry);
        foreach(var r in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();if(V(5)!=0){continue;}
            int Rank(string key)=>key==row.SourceKey?V(0):key=="Shock Field"?V(1):key=="Charged Bolt Sentry"?V(2):0;
            var stats=AssassinSentryCreation.Prepare(new(row,Rank,Rank,99),99,MonsterDifficulty.Normal,
                name=>name=="passive_ltng_mastery"?V(3):0);
            Require(stats.Ability==CombatAbility.LightningSentry&&stats.Skill("sentry lightning")==V(0)
                &&stats.Skill("Shock Field")==V(1)&&stats.Skill("Charged Bolt Sentry")==V(2)
                &&AssassinSentryCreation.Shots(stats)==10&&AssassinSentryCreation.Values(stats).Rank==V(0),"Captured Lightning Sentry actor/hard synergies/fixed10shots");
            Require(AssassinSentryCreation.Lightning(stats)==(r[7][min].GetInt32(),r[7][max].GetInt32()),"Captured Lightning Sentry source packet "+count);count++;
        }
        Require(count==1983,"Lightning Sentry source damage inventory");
        foreach(int rank in new[]{1,3,20,21,255})foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {
            var stats=AssassinSentryCreation.Prepare(new(row,_=>rank,_=>20,99),99,difficulty,
                key=>key=="passive_ltng_mastery"?55:key=="passive_ltng_pierce"?-11:0);
            var next=AssassinSentryCreation.Rescale(stats,MonsterDifficulty.Hell);
            Require(stats.Passive("passive_ltng_mastery")==55&&stats.Passive("passive_ltng_pierce")==-11
                &&stats.Passives==next.Passives&&stats.Skills==next.Skills&&AssassinSentryCreation.Lightning(stats)==AssassinSentryCreation.Lightning(next),"Difficulty altered captured source packet");
            bool refused=false;try{AssassinSentryCreation.Bolts(stats);}catch(NotSupportedException){refused=true;}
            Require(refused,"Lightning Sentry borrowed CBS fan count");
        }
        Log.Information("Lightning Sentry creation:1983 supported full source packet comparisons and15 rank/difficulty contexts; fixed minimum, hard synergies, inherited mastery/pierce and fixed10shots");
    }
    private static void Damage(PlayerMobile p,AssassinSentryBody b,BladeMissileTarget t)
    {
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p);
        foreach(int pierce in new[]{20,-20})
        {
            t.SetResistance(ResistanceType.Energy,0);t.Hits=t.HitsMax;SkillHealth.Clear(t);int before=SkillHealth.Balance256(t);
            var hit=AssassinSentryBolts.Impact(b,t,"sentrylightningbolt",2560,2560,pierce,SourceRandom.InitLow(19));
            Require(hit.WholeLoss==10*(100+pierce)/100&&before-SkillHealth.Balance256(t)==2560*(100+pierce)/100,$"Captured signed resistance pierce/FP256: pierce={pierce}, loss={hit.WholeLoss}, actual={before-SkillHealth.Balance256(t)}, expected={2560*(100+pierce)/100}, res={t.EnergyResistance}, life={before}");
        }
        t.SetResistance(ResistanceType.Energy,100);int hp=SkillHealth.Balance256(t);
        Require(AssassinSentryBolts.Impact(b,t,"sentrylightningbolt",2560,2560,90,1).WholeLoss==0&&SkillHealth.Balance256(t)==hp,"Ordinary sentry pierce broke immunity");
        t.SetResistance(ResistanceType.Energy,0);
        var kill=Target(b,7);kill.SetHits(1);kill.Hits=1;AssassinSentryBolts.Impact(b,kill,"sentrylightningbolt",2560,2560,0,1);
        Require(!kill.Alive&&kill.LastKiller==b&&ClassLoot.Killer(kill)==p,"Actual NPC/original owner kill credit");kill.Delete();
        Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==life,"Source-zero bolt inherited player payment/leech");
    }
    private static void Save(PlayerMobile p,AssassinSentryBody b,BladeMissileTarget first,BladeMissileTarget second,long id)
    {
        first.Blessed=second.Blessed=true;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,first.Serial.Value,second.Serial.Value,b.Prepared,
            AssassinSentryLightning.State(id),SkillHealth.Balance256(first),SkillHealth.Balance256(second),NecromancerEffects.ManaBalance256(p))));
    }
    private static void Check()
    {
        Creation();var p=BladeBodyChecks.Fresh(6640,45);p.Name="LightningSentryFixture";
        Train(p,CombatAbility.ShockWeb,15);Train(p,CombatAbility.ChargedBoltSentry,20);
        while(p.RawStr<60){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength for real gear");}
        var eye=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Griffon's Eye").Id,99,new Random(173),true);
        p.AddToBackpack(eye);Require(p.EquipItem(eye),"Real inherited lightning equipment");ClassGear.Refresh(p);
        var b=Actor(p,DateTime.UtcNow.AddDays(1));var first=Target(b);var second=Target(b);int mana=NecromancerEffects.ManaBalance256(p);
        Require(b.Prepared.Creation is{Ability:CombatAbility.LightningSentry,Rank:2,Life256:25600,Level:76}
            &&b.Prepared.Control is{Mode:8,EmergenceEnd:15,NextThink:25}&&b.Passive("passive_ltng_mastery")>0
            &&b.Passive("passive_ltng_pierce")>0&&!b.BlocksFit&&!b.CanBeDamaged()&&b.Name.Contains("[placeholder]"),$"Actual source actor/S1/soft-rank/passive context: {JsonSerializer.Serialize(b.Prepared)}");
        var ends=AssassinSentryCreation.Lightning(b.Prepared.Creation);p.AddToBackpack(eye);ClassGear.Refresh(p);
        Require(AssassinSentryCreation.Lightning(b.Prepared.Creation)==ends&&b.Prepared.Creation.Skill("Shock Field")==15
            &&b.Prepared.Creation.Skill("Charged Bolt Sentry")==20,"Actor birth stats followed later owner gear");
        Damage(p,b,first);
        first.Blessed=second.Blessed=true;Wall(b);first.Blessed=second.Blessed=false;
        Require(AssassinSentryLightning.Create(b,new(0,b.Y*2))==0&&AssassinSentryLightning.Create(b,new(b.X*2+100,b.Y*2))==0,"Generic zero-coordinate/absolute-range guards");
        var self=AssassinSentryLightning.Create(b,new(b.X*2,b.Y*2));var selfState=AssassinSentryLightning.State(self);
        Require(selfState.Vx>0&&selfState.Vy>0,"Factory same-point diagonal fallback");Frames(self,20,false);
        for(int i=0;i<28;i++){b.AdvanceController(b.Prepared.NextFrameUtc,true);}
        var emitted=AssassinSentryLightning.Snapshot().Bolts.Where(x=>x.Actor==b.Serial.Value).ToArray();
        Require(emitted.Length==1&&b.Prepared.Control is{Dispatches:1,Attempts:1,Ai.RemainingShots:9},"One real straight bolt per sequence shot");
        var state=emitted.Single();Require(state.Frames==14&&state.VelocityFp8==5760&&state.Minimum256==ends.Min&&state.Maximum256==ends.Max
            &&state.Actor==b.Serial.Value&&state.Owner==p.Serial.Value&&state.Vy==0&&state.Vx>0,"Actual generic launch/range/velocity/captured pure lightning");
        int hp1=SkillHealth.Balance256(first),hp2=SkillHealth.Balance256(second);
        for(int i=0;i<8&&AssassinSentryLightning.State(state.Id) is{LastCollision:0};i++){Frames(state.Id,1,true);}
        var one=AssassinSentryLightning.State(state.Id);
        Require(one!=null&&one.Fault==null&&one.LastCollision!=0&&((SkillHealth.Balance256(first)<hp1)^(SkillHealth.Balance256(second)<hp2)),"First source phase did not hit exactly one overlap");
        Frames(state.Id,1,true);var two=AssassinSentryLightning.State(state.Id);
        Require(two!=null&&two.LastCollision!=one.LastCollision&&SkillHealth.Balance256(first)<hp1&&SkillHealth.Balance256(second)<hp2
            &&two.Seed!=one.Seed,"LastCollide hid overlapping second victim or RNG did not advance");
        // Keep a genuine travelling/history-bearing projectile for actual saves.
        Require(NecromancerEffects.ManaBalance256(p)==mana,"Actor shot replayed player mana cost");
        Lifetimes(p);Save(p,b,first,second,state.Id);Require(!AssassinCombat.Implemented(CombatAbility.LightningSentry),"Unfinished Lightning Sentry learned handler enabled");
        Log.Information("Lightning Sentry actual actor/sequence/one-bolt launch, travelling Size2 overlap penetration/history/RNG, captured gear, signed pierce/immunity, pure damage/credit and saved packet pass; retirement remains an explicit preparation boundary");
    }
    private static void Wall(AssassinSentryBody body)
    {
        var target=Target(body,8);var wall=new Static(0x80);wall.MoveToWorld(new(body.X+2,body.Y,body.Z),body.Map);
        try
        {
            int health=SkillHealth.Balance256(target);var id=AssassinSentryLightning.Create(body,new((body.X+15)*2,body.Y*2));
            Frames(id,8,true);Require(AssassinSentryLightning.State(id)==null&&SkillHealth.Balance256(target)==health,"Straight bolt crossed native wall or damaged through it");
        }
        finally{target.Delete();wall.Delete();}
    }
    private static void Lifetimes(PlayerMobile owner)
    {
        var b=Actor(owner,DateTime.UtcNow.AddDays(2));var t=Target(b,5);
        var facets=SummonDifficulties.Facets();var map=owner.Map;var point=owner.Location;
        SkillHealth.SetBalance256(b,12850);var captured=b.Prepared.Creation;
        SummonDifficulties.SetFacets([new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
        owner.MoveToWorld(point,Map.Felucca);SummonDifficulties.Sync(owner);
        Require(b.Map==map&&b.Prepared.Creation.Difficulty==MonsterDifficulty.Hell&&SkillHealth.Balance256(b)==12850
            &&b.Prepared.Creation.Skills==captured.Skills&&b.Prepared.Creation.Passives==captured.Passives,"Lightning Sentry old-facet scaling/injury/captured stats");
        var selector=AssassinSentryActors.SelectTarget;AssassinSentryActors.SelectTarget=_=>new(t.Serial.Value,10);
        try
        {
            for(int i=0;i<335;i++){b.AdvanceController(b.Prepared.NextFrameUtc,true);}
            Require(!b.Deleted&&b.Prepared.Control is{Mode:0,Attempts:10,Dispatches:10,Ai.RemainingShots:0}
                &&b.Prepared.ControllerFault.Contains("retirement")&&AssassinSentryLightning.Snapshot().Bolts.Count(s=>s.Actor==b.Serial.Value)==10,"Fixed ten shots or explicit retirement boundary");
            var s=AssassinSentryLightning.Snapshot().Bolts.First(s=>s.Actor==b.Serial.Value);Frames(s.Id,1,false);var offline=AssassinSentryLightning.State(s.Id);
            Require(offline.Frame==1&&offline.X==s.X&&offline.Seed==s.Seed,"Offline expiry replayed movement/RNG");Drain(b);
            Require(!AssassinSentryLightning.Snapshot().Bolts.Any(s=>s.Actor==b.Serial.Value),"Offline projectile lifetime froze");
        }
        finally{AssassinSentryActors.SelectTarget=selector;owner.MoveToWorld(point,map);SummonDifficulties.SetFacets(facets);SummonDifficulties.Sync(owner);t.Delete();b.Delete();}
    }
    private static void Reload()
    {
        Creation();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var b=(AssassinSentryBody)World.FindMobile((Serial)saved.Actor);var first=(BladeMissileTarget)World.FindMobile((Serial)saved.First);var second=(BladeMissileTarget)World.FindMobile((Serial)saved.Second);
        Require(JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(saved.Body)&&b.SummonMaster==p
            &&JsonSerializer.Serialize(AssassinSentryLightning.State(saved.Bolt.Id))==JsonSerializer.Serialize(saved.Bolt)
            &&first.Blessed&&second.Blessed&&SkillHealth.Balance256(first)==saved.Health1&&SkillHealth.Balance256(second)==saved.Health2
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Saved actor/bolt/history/packet/seed/resources/inspection protection");
        first.Blessed=second.Blessed=false;
        // Position the two real colliders at the resumed path, independently
        // of their prior cell; preserve the saved most-recent identity.
        var point=new Point3D((int)Math.Round(saved.Bolt.X+saved.Bolt.Vx),(int)Math.Round(saved.Bolt.Y),saved.Bolt.Z);
        first.MoveToWorld(point,b.Map);second.MoveToWorld(point,b.Map);
        Frames(saved.Bolt.Id,1,true);var next=AssassinSentryLightning.State(saved.Bolt.Id);
        Require(next!=null&&next.Fault==null&&next.Frame==saved.Bolt.Frame+1&&next.X>saved.Bolt.X&&next.LastCollision!=saved.Bolt.LastCollision
            &&next.Minimum256==saved.Bolt.Minimum256&&next.Pierce==saved.Bolt.Pierce&&next.Seed!=saved.Bolt.Seed,"Saved bolt did not resume on other overlapping victim once");
        int dispatches=b.Prepared.Control.Dispatches;b.AdvanceController(b.Prepared.NextFrameUtc,true);
        Require(b.Prepared.Control.Dispatches==dispatches&&b.Prepared.Control.Frame==saved.Body.Control.Frame+1,"Reload replayed already-dispatched sequence event");
        Save(p,b,first,second,next.Id);Require(!AssassinCombat.Implemented(CombatAbility.LightningSentry),"Reload enabled learned Lightning Sentry");
    }
    private static void Live()
    {
        var p=BladeBodyChecks.Fresh(6700,32);p.Name="LightningSentryLiveFixture";var b=Actor(p,TimedEffects.Now);var t=Target(b);
        var net=NetState.Instances.Single(x=>x.Mobile==null&&IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();
        int health=SkillHealth.Balance256(t);
        Timer.StartTimer(TimeSpan.FromMilliseconds(2600),()=>
        {
            try
            {
                Require(b.Prepared.Control.Dispatches>=2&&SkillHealth.Balance256(t)<health,"Connected source conductor did not emit real damaging straight bolts");
                var flights=AssassinSentryLightning.Snapshot().Bolts.Where(s=>s.Actor==b.Serial.Value).Select(s=>s.Id).ToArray();Require(flights.Length>0,"No active source-range bolt at disconnect");
#pragma warning disable CS0618
                net.Dispose();
#pragma warning restore CS0618
                Require(p.NetState==null,"Fixture disconnect did not suppress owner");int remaining=SkillHealth.Balance256(t),attempts=b.Prepared.Control.Attempts;
                Timer.StartTimer(TimeSpan.FromMilliseconds(1000),()=>
                {
                    try{Require(flights.All(id=>AssassinSentryLightning.State(id)==null)&&SkillHealth.Balance256(t)==remaining
                        &&b.Prepared.Control.Attempts==attempts,"Offline conductor attacked or froze lightning lifetime");Log.Information("lightning-sentry-live-result: passed");}
                    catch(Exception e){Log.Error(e,"lightning-sentry-live-result: failed");}finally{t.Delete();b.Delete();p.Delete();}
                });
            }
            catch(Exception e){Log.Error(e,"lightning-sentry-live-result: failed");t.Delete();b.Delete();p.Delete();}
        });
    }
}
