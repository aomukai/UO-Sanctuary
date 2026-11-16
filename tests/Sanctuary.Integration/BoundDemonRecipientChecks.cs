// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BoundDemonRecipientChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonRecipientChecks));
    private sealed record Pet(uint Serial,int Life,string Birth,NpcEquipmentSeed Random);
    private sealed record Saved(int Generation,uint Owner,uint Bound,uint Weapon,BoundDemonState State,int BoundLife,int Mana,int Stamina,Pet[] Summons);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-recipient-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name is"BoundDemonRecipientFixture"or"BoundDemonRecipientTarget"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-recipient-reload-check":"bound-demon-recipient-check";
            ConsoleInputHandler.RegisterCommand(name,"Shared original/bound demon recipient eligibility and actual Blood Boil",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,_=>throw new NotSupportedException()});
    private static Pet Save(SkeletonGuardian b)=>new(b.Serial.Value,SkillHealth.Balance256(b),Birth(b),NpcEquipmentRandom.State(b));
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,BaseCreature center)
    {
        var body=new GoatmanNpcContactChecks.Recipient{Name="BoundDemonRecipientTarget",Frozen=true,CantWalk=true,NoKillAwards=true};
        body.SetHits(50000);body.Hits=body.HitsMax;body.SetResistance(ResistanceType.Physical,0);body.SetResistance(ResistanceType.Fire,0);
        body.MoveToWorld(new(center.X-1,center.Y,center.Z),p.Map);body.AIObject?.AITimer.Stop();return body;
    }
    private static void Explosion(PlayerMobile p,Daemon body,SkeletonGuardian[] summons,GoatmanNpcContactChecks.Recipient target,DateTime at)
    {
        Ready(p);var all=new BaseCreature[]{summons[0],body,summons[1]};var roster=BloodBoilPrepared.Roster(p);
        Require(roster.SequenceEqual(all),"Shared recipient ordering lost original creation order across native body types");
        int[] before=all.Select(SkillHealth.Balance256).ToArray();int hp=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p),selfEvents=0;
        var state=BoundDemonHosts.State(body);var births=summons.Select(Birth).ToArray();var seeds=summons.Select(NpcEquipmentRandom.State).ToArray();
        void Notice(SkillDamageContact c){if(all.Contains(c.Target)){selfEvents++;}}
        SkillDamageEvents.BeforeDirectDamage+=Notice;BloodBoilRelease result;
        try{result=BloodBoilPrepared.ReleasePrepared(p,at);}finally{SkillDamageEvents.BeforeDirectDamage-=Notice;}
        var numbers=BloodBoilPrepared.Numbers(SkillMechanics.Values(p,CombatAbility.BloodBoil));
        Require(result is{Released:true,DemonCount:3,Bursts.Length:3,Fault:null}&&result.ManaPaid256==numbers.Mana256
            &&NecromancerEffects.ManaBalance256(p)==mana-numbers.Mana256&&result.Bursts.Select(b=>b.Demon).SequenceEqual(all.Select(b=>b.Serial.Value)),"Mixed roster release/payment/identity changed");
        for(int i=0;i<all.Length;i++)
        {Require(before[i]-SkillHealth.Balance256(all[i])==(long)SkillHealth.Maximum256(all[i])*5/100,"Mixed three-demon cost lost pinned5percent divisor");}
        Require(selfEvents==0&&result.Bursts.Sum(b=>b.Recipients.Length)==1&&hp-SkillHealth.Balance256(target)==result.Bursts[0].Physical256+result.Bursts[0].Fire256,
            $"Mixed roster:selfEvents{selfEvents},recipients[{string.Join(',',result.Bursts.Select(b=>b.Recipients.Length))}],HP{hp-SkillHealth.Balance256(target)},firstPacket{result.Bursts[0].Physical256}+{result.Bursts[0].Fire256},centers[{string.Join(';',result.Bursts.Select(b=>b.Center))}],target{target.Location}");
        Require(BoundDemonHosts.State(body)==state&&summons.Select(Birth).SequenceEqual(births)&&summons.Select(NpcEquipmentRandom.State).SequenceEqual(seeds),"Blood Boil rewrote original AI/birth/state/RNG");
    }
    private static void Eligibility(PlayerMobile p,Daemon body,SkeletonGuardian[] summons)
    {
        Require(WarlockDemonRecipients.Owned(p,body)&&WarlockDemonRecipients.Active(p,body),"Bound original not qualified");
        var point=body.Location;body.Location=new(p.X+20,p.Y,p.Z);
        Require(WarlockDemonRecipients.Active(p,body)&&!WarlockDemonRecipients.InRange(p,body,19)&&!BloodBoilPrepared.Roster(p).Contains(body),"Out-of-range bound body received an effect");body.Location=point;
        body.Blessed=true;Require(!WarlockDemonRecipients.Active(p,body),"Blessed bound body received an effect");body.Blessed=false;
        var original=summons[0];var clock=DemonNativeState.Controller(original);DemonNativeState.Controller(original,clock with{Fault="fixture refusal"});
        Require(!WarlockDemonRecipients.Owned(p,original)&&!EngorgeCasting.Recipient(p,original)&&!BloodBoilPrepared.Roster(p).Contains(original),"Shared original path discarded its controller fault guard");DemonNativeState.Controller(original,clock);
        var intruder=new PlayerMobile{Player=true};ClassCreation.Profiles.Assign(intruder,DiabloClass.Warlock);
        try{Require(!WarlockDemonRecipients.Owned(intruder,body)&&!WarlockDemonRecipients.Active(intruder,body),"Foreign owner adopted bound recipient");}finally{intruder.Delete();}
        PersistentMinions.Suspend(p);
        Require(WarlockDemonRecipients.Roster(p).Length==3&&BloodBoilPrepared.Roster(p).Length==0&&!WarlockDemonRecipients.Active(p,body),"Parking changed slots or admitted inactive recipients");
        PersistentMinions.Resume(p);Require(BloodBoilPrepared.Roster(p).Length==3,"Original and bound recipients did not resume");
    }
    private static void Scene(NetState network)
    {
        PlayerMobile p;Daemon body;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);body=new Daemon();}finally{Creating=false;}
        p.Female=true;p.Name=body.Name="BoundDemonRecipientFixture";p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.binddemon",1);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);ClassSkillChecks.Train(p,"warlock.bloodboil",1);
        ClassSkillChecks.Train(p,"warlock.summongoatman",6);ClassSkillChecks.Train(p,"warlock.summontainted",6);
        FixtureSpace.Corridor(p,7040,34,3500);var account=FixtureNetwork.Authenticate(network,p,"bound-recipient-");SkeletonGuardian[] summons=[];bool kept=false;GoatmanNpcContactChecks.Recipient target=null;
        try
        {
            var goat=WarlockGoatmanWorld.Create(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonGoatman);WarlockGoatmanWorld.Admit(goat);
            body.MoveToWorld(new(p.X+2,p.Y+1,p.Z),p.Map);body.AIObject?.AITimer.Stop();var weapon=new Dagger();body.AddItem(weapon);
            Require(BoundDemonHosts.BindPrepared(p,body,out var why),"Bound recipient setup: "+why);body.AIObject?.AITimer.Stop();
            var tainted=WarlockGoatmanWorld.Create(p,new(p.X+4,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonTainted);WarlockGoatmanWorld.Admit(tainted);summons=[goat,tainted];
            Eligibility(p,body,summons);target=Target(p,summons[0]);Explosion(p,body,summons,target,TimedEffects.Now);
            Require(BloodBoilPrepared.Approved&&WarlockCombat.Implemented(CombatAbility.BloodBoil),"Approved mixed Blood Boil unavailable");
            foreach(var b in new BaseCreature[]{goat,body,tainted}){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/2-17);}
            body.Mana=Math.Max(0,body.ManaMax-3);body.Stam=Math.Max(0,body.StamMax-5);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,body.Serial.Value,weapon.Serial.Value,BoundDemonHosts.State(body),SkillHealth.Balance256(body),body.Mana,body.Stam,summons.Select(Save).ToArray())));kept=true;
            Log.Information("Bound recipients:original Goatman/bound Daemon/original Tainted retain global creation order;range/blessing/controller-fault/foreign-owner/parking guards pass;actual mixed three-body Blood Boil pays5percent per body once with one shared area hit and no self attack event");
        }
        finally{target?.Delete();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){foreach(var b in summons){b.Delete();}body.Delete();p.Delete();}}
        Lethal(network);
    }
    private static void Lethal(NetState network)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Female=true;ClassSkillChecks.Train(p,"warlock.binddemon",1);ClassSkillChecks.Train(p,"warlock.bloodboil",1);
        FixtureSpace.Corridor(p,7100,30,3500);var account=FixtureNetwork.Authenticate(network,p,"bound-recipient-lethal-");var body=new Daemon();body.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);body.AIObject?.AITimer.Stop();GoatmanNpcContactChecks.Recipient target=null;
        try
        {
            Require(BoundDemonHosts.BindPrepared(p,body,out var why),"Lethal native bind: "+why);body.AIObject?.AITimer.Stop();target=Target(p,body);Ready(p);
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/10);int before=SkillHealth.Balance256(body),life=SkillHealth.Balance256(target);
            var result=BloodBoilPrepared.ReleasePrepared(p,TimedEffects.Now);
            Require(result is{Released:true,DemonCount:1,Bursts.Length:1,Fault:null}&&result.Bursts[0].LifePaid256==before&&(body.Deleted||!body.Alive)
                &&body.LastKiller==body&&life-SkillHealth.Balance256(target)==result.Bursts[0].Physical256+result.Bursts[0].Fire256,"Lethal bound cost lost original native death or captured explosion");
            BoundDemonHosts.Tick();Require(!WarlockDemonRecipients.Roster(p).Contains(body)&&BoundDemonHosts.State(body)==null,"Dead bound body remained a skill recipient");
            Log.Information("Bound recipients:single original Daemon at10percent pays lethal15percent resource cost;native death completes and captured explosion still hits before ledger cleanup");
        }
        finally{target?.Delete();body.Delete();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();p.Delete();BoundDemonHosts.Tick();}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many bound recipient reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Bound)as Daemon;
        var summons=saved.Summons.Select(s=>World.FindMobile((Serial)s.Serial)as SkeletonGuardian).ToArray();
        Require(body is{Alive:true,Deleted:false}&&body.Map==Map.Internal&&BoundDemonHosts.State(body)==saved.State&&SkillHealth.Balance256(body)==saved.BoundLife
            &&body.Mana==saved.Mana&&body.Stam==saved.Stamina&&World.FindItem((Serial)saved.Weapon)?.Parent==body,"Original bound recipient or wounds/resources/equipment changed on load");
        for(int i=0;i<summons.Length;i++){Require(summons[i]!=null&&Save(summons[i])==saved.Summons[i],"Original summon birth/wounds/RNG changed on load");}
        var account=FixtureNetwork.Authenticate(network,p,"bound-recipient-reload-");GoatmanNpcContactChecks.Recipient target=null;
        try
        {
            PersistentMinions.Resume(p);target=Target(p,summons[0]);Explosion(p,body,summons,target,TimedEffects.Now);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,State=BoundDemonHosts.State(body),BoundLife=SkillHealth.Balance256(body),Summons=summons.Select(Save).ToArray()}));
            Log.Information("Bound recipients:same injured original/bound mixed roster and creation order survive reload;actual Blood Boil pays new life once without rewriting snapshots,AI,items or summon RNG");
        }
        finally{target?.Delete();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
