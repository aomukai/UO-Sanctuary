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

public static class BoundDemonOathChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonOathChecks));
    private sealed record Pet(uint Serial,string Birth,int Life,NpcEquipmentSeed Random);
    private sealed record Saved(int Generation,uint Owner,uint Enemy,int OwnerLife,uint Bound,uint Weapon,BoundDemonState State,int Life,int Mana,int Stamina,Pet[] Summons);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-oath-fixtures.json");
    private static bool Creating;
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BoundDemonOathFixture"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-oath-reload-check":"bound-demon-oath-check";
            ConsoleInputHandler.RegisterCommand(name,"Bound native Blood Oath identity scalar/mixed settled damage and saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static PlayerMobile Owner(int column)
    {
        PlayerMobile p;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}p.Name="BoundDemonOathFixture";p.Female=true;p.AddItem(new Backpack());
        foreach(var x in new[]{("warlock.binddemon",1),("warlock.demonicmastery",10),("warlock.bloodoath",20),("warlock.summongoatman",6),("warlock.summontainted",6)}){ClassSkillChecks.Train(p,x.Item1,x.Item2);}
        FixtureSpace.Corridor(p,column,34,3300);return p;
    }
    private static (Daemon Bound,SkeletonGuardian[] Summons,Dagger Weapon) Crew(PlayerMobile p)
    {
        var goat=WarlockGoatmanWorld.Create(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now);WarlockGoatmanWorld.Admit(goat);
        Daemon body;Creating=true;try{body=new Daemon();}finally{Creating=false;}body.Name="BoundDemonOathFixture";body.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);body.AIObject?.AITimer.Stop();
        var weapon=new Dagger();body.AddItem(weapon);Require(BoundDemonHosts.BindPrepared(p,body,out var why),"Oath bound setup: "+why);body.AIObject?.AITimer.Stop();
        var tainted=WarlockGoatmanWorld.Create(p,new(p.X+6,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonTainted);WarlockGoatmanWorld.Admit(tainted);
        return(body,[goat,tainted],weapon);
    }
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch{WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,_=>throw new NotSupportedException()});
    private static Pet Save(SkeletonGuardian b)=>new(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),NpcEquipmentRandom.State(b));
    private static BaseCreature[] All(Daemon body,SkeletonGuardian[] summons)=>[summons[0],body,summons[1]];
    private static int[] Life(BaseCreature[] bodies)=>bodies.Select(SkillHealth.Balance256).ToArray();
    private static void Fill(PlayerMobile p,BaseCreature[] bodies){SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));foreach(var b in bodies){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b));}}
    private static int Loss(PlayerMobile p,Action action){int before=SkillHealth.Balance256(p);action();return before-SkillHealth.Balance256(p);}
    private static GoatmanNpcContactChecks.Recipient Enemy(PlayerMobile p)
    {var e=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};e.SetHits(50000);e.Hits=e.HitsMax;e.MoveToWorld(new(p.X+20,p.Y,p.Z),p.Map);e.AIObject?.AITimer.Stop();return e;}
    private static void Whole(PlayerMobile p,Daemon body,SkeletonGuardian[] summons,Mobile enemy)
    {
        var all=All(body,summons);var before=Life(all);int ownerEvents=0,petEvents=0;
        void Notice(SkillDamageContact c){if(c.Target==p){ownerEvents++;}if(all.Contains(c.Target)){petEvents++;}}
        SkillDamageEvents.BeforeDirectDamage+=Notice;
        try{Require(Loss(p,()=>SkillHealth.Damage(enemy,p,25601))==19201,"Mixed actual owner pipeline did not retain one25percent budget");}
        finally{SkillDamageEvents.BeforeDirectDamage-=Notice;}
        Require(before.Zip(Life(all),(a,b)=>a-b).SequenceEqual(new[]{2134,2133,2133})&&ownerEvents==1&&petEvents==0,"Mixed native whole/FP8 loss or ordered remainder/attack events differs");
    }
    private static void Scene(NetState network)
    {
        var p=Owner(7040);var account=FixtureNetwork.Authenticate(network,p,"bound-oath-");Daemon body=null;SkeletonGuardian[] summons=[];var enemy=Enemy(p);bool kept=false;
        try
        {
            var crew=Crew(p);body=crew.Bound;summons=crew.Summons;var all=All(body,summons);var birth=BoundDemonHosts.State(body).Birth;
            Require(body.BonusPetDamageScalar==1&&WarlockDemonRecipients.Members(p).SequenceEqual(all),"Actual ordinary Daemon scalar or global member order differs");
            bool refused=false;try{PreparedMonsterNativeDamage.RequireBaseModifiers(enemy,body);}catch(NotSupportedException){refused=true;}
            Require(refused,"General native controlled-pet guard was weakened");PreparedMonsterNativeDamage.RequireBaseModifiers(enemy,body,controlledIdentity:true);
            var unbound=new Daemon{Controlled=true};try{refused=false;try{PreparedMonsterNativeDamage.RequireBaseModifiers(enemy,unbound,controlledIdentity:true);}catch(NotSupportedException){refused=true;}Require(refused,"Identity flag adopted an unregistered controlled NPC");}finally{unbound.Delete();}
            Fill(p,all);var before=Life(all);Require(Loss(p,()=>SkillHealth.Damage(enemy,p,1001))==751&&before.Zip(Life(all),(a,b)=>a-b).SequenceEqual(new[]{84,83,83}),"Learned mixed roster did not gain automatic cover");

            {
                Fill(p,all);Whole(p,body,summons,enemy);Fill(p,all);before=Life(all);
                var split=BloodOathDamageProposal.ReducePrepared(enemy,p,1001,SkillDamageKind.Direct);
                Require(split is{Remaining256:751,Fault:null}&&split.Transfers.Select(t=>t.Demon).SequenceEqual(all.Select(b=>b.Serial.Value))
                    &&split.Transfers.Select(t=>t.Amount256).SequenceEqual(new[]{84,83,83})&&before.Zip(Life(all),(a,b)=>a-b).SequenceEqual(new[]{84,83,83}),"Mixed direct result lost saved order/fractional split");
                Fill(p,all);before=Life(all);Require(Loss(p,()=>AOS.Damage(p,enemy,100,false,0,0,100,0,0,spellDamage:true))==19200&&before.Zip(Life(all),(a,b)=>a-b).Sum()==6400,"Mixed native resisted owner route failed");
                Fill(p,all);before=Life(all);Require(Loss(p,()=>AOS.Damage(p,enemy,40,true,100,0,0,0,0))==7680&&before.Zip(Life(all),(a,b)=>a-b).Sum()==2560,"Mixed native armor-ignore owner route failed");
                Fill(p,all);var mod=new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50);body.AddSkillMod(mod);before=Life(all);
                var fault=BloodOathDamageProposal.ReducePrepared(enemy,p,1000,SkillDamageKind.Direct);
                Require(fault is{Remaining256:1000,Fault:not null,Transfers.Length:0}&&Life(all).SequenceEqual(before),"Bound native modifier refusal was not preflighted before all shares");body.RemoveSkillMod(mod);
                var clock=DemonNativeState.Controller(summons[0]);DemonNativeState.Controller(summons[0],clock with{Fault="fixture refusal"});
                fault=BloodOathDamageProposal.ReducePrepared(enemy,p,1000,SkillDamageKind.Direct);
                Require(fault is{Remaining256:1000,Fault:not null,Transfers.Length:0}&&Life(all).SequenceEqual(before),"Faulted member was filtered away to grant unverified protection");DemonNativeState.Controller(summons[0],clock);
                var point=body.Location;body.MoveToWorld(point,Map.Felucca);Fill(p,all);before=Life(all);
                Require(Loss(p,()=>SkillHealth.Damage(enemy,p,1000))==750&&before[1]==SkillHealth.Balance256(body)&&before[0]-SkillHealth.Balance256(summons[0])==125&&before[2]-SkillHealth.Balance256(summons[1])==125,"Other-facet bound member entered redirect divisor");body.MoveToWorld(point,p.Map);body.AIObject?.AITimer.Stop();
                Fill(p,all);before=Life(all);foreach(var kind in new[]{SkillDamageKind.Periodic,SkillDamageKind.Return,SkillDamageKind.SelfCost})
                {Require(Loss(p,()=>SkillHealth.Damage(enemy,p,777,kind))==777&&Life(all).SequenceEqual(before),"Excluded mixed damage kind redirected");}
                Require(BoundDemonHosts.State(body).Birth==birth&&BloodOathDamageProposal.Approved&&WarlockCombat.Implemented(CombatAbility.BloodOath),"Mixed incoming rewrote birth or lost approved learned Oath");
                foreach(var b in all){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/2-17);}body.Mana=Math.Max(0,body.ManaMax-3);body.Stam=Math.Max(0,body.StamMax-5);PersistentMinions.Suspend(p);before=Life(all);
                Require(Loss(p,()=>SkillHealth.Damage(enemy,p,777))==777&&Life(all).SequenceEqual(before),"Parked mixed roster covered owner or recovered");
            }
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,enemy.Serial.Value,SkillHealth.Balance256(p),body.Serial.Value,crew.Weapon.Serial.Value,BoundDemonHosts.State(body),SkillHealth.Balance256(body),body.Mana,body.Stam,summons.Select(Save).ToArray())));kept=true;
            Log.Information("Bound Oath:actual mixed Goatman/Daemon/Tainted one25percent budget,whole and FP8 ordered shares,native resisted/armor-ignore routes,no recursive attack events;identity native scalar retains original damage path while default/unregistered/modifier/fault guards refuse;facet/kind/park exclusions pass");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){enemy.Delete();body?.Delete();foreach(var b in summons){b.Delete();}p.Delete();BoundDemonHosts.Tick();}}
        Lethal(network);
    }
    private static void Lethal(NetState network)
    {
        var p=Owner(7100);var account=FixtureNetwork.Authenticate(network,p,"bound-oath-lethal-");var crew=Crew(p);var all=All(crew.Bound,crew.Summons);var enemy=Enemy(p);
        try
        {
            Fill(p,all);SkillHealth.SetBalance256(crew.Bound,512);var before=Life(all);
            int loss=Loss(p,()=>SkillHealth.Damage(enemy,p,51200));
            Require(loss==42155&&(crew.Bound.Deleted||!crew.Bound.Alive)&&crew.Bound.LastKiller==enemy&&before[0]-SkillHealth.Balance256(all[0])==4267
                &&before[2]-SkillHealth.Balance256(all[2])==4266,"Lethal native bound share lost cap/no redistribution/actual attacker credit");
            BoundDemonHosts.Tick();Require(BoundDemonHosts.State(crew.Bound)==null,"Dead bound Oath recipient remained hosted");
            Log.Information("Bound Oath:native middle Daemon dies to capped512FP8 share with actual enemy credit;unabsorbed amount stays on owner without redistribution and host cleanup completes");
        }
        finally{enemy.Delete();foreach(var b in all){b.Delete();}network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();p.Delete();BoundDemonHosts.Tick();}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many bound Oath reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var enemy=World.FindMobile((Serial)s.Enemy);var body=World.FindMobile((Serial)s.Bound)as Daemon;var summons=s.Summons.Select(x=>World.FindMobile((Serial)x.Serial)as SkeletonGuardian).ToArray();
        Require(body is{Alive:true,Deleted:false}&&body.Map==Map.Internal&&BoundDemonHosts.State(body)==s.State&&SkillHealth.Balance256(body)==s.Life&&body.Mana==s.Mana&&body.Stam==s.Stamina
            &&World.FindItem((Serial)s.Weapon)?.Parent==body&&SkillHealth.Balance256(p)==s.OwnerLife&&!BloodOathDamageProposal.ManagedPrepared(p),"Original bound actor/items/resources/owner wounds changed or disconnected protection enabled");
        for(int i=0;i<summons.Length;i++){Require(summons[i]!=null&&Save(summons[i])==s.Summons[i],"Original Oath summon birth/wounds/RNG changed on load");}
        var account=FixtureNetwork.Authenticate(network,p,"bound-oath-reload-");
        try
        {
            PersistentMinions.Resume(p);{Whole(p,body,summons,enemy);}PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,OwnerLife=SkillHealth.Balance256(p),State=BoundDemonHosts.State(body),Life=SkillHealth.Balance256(body),Summons=summons.Select(Save).ToArray()}));
            Log.Information("Bound Oath:original bound body/weapon/birth/resources,owner and mixed summon wounds/order survive reload;automatic learned reaction redirects one exact whole/FP8 budget again without replay");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
