// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BindDemonChannelChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BindDemonChannelChecks));
    private sealed record Saved(int Generation,uint Owner,uint Bound,uint Target,BoundDemonState State,int Life,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bind-demon-channel-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BindChannelFixture"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;
        var mana=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=m=>Creating||m.Name=="BindChannelFixture"?TimeSpan.FromHours(1):mana?.Invoke(m)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bind-demon-channel-reload-check":"bind-demon-channel-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared pinned chance interpretation/native channel/late callbacks/saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static BindDemonSuppliers Birth(int hard=20,int effective=20)
    {
        var v=new SkillValues(SkillMechanics.For(CombatAbility.BindDemon),key=>key=="Bind Demon"?effective:0,key=>key=="Bind Demon"?hard:key=="Death Mark"?20:0,99);
        return BindDemonPrepared.Capture(v,_=>0);
    }
    private static void Numbers()
    {
        Require(SkillFormula.Evaluate("2+17%5*3",(_,_,_)=>0)==8&&SkillFormula.Evaluate("0?(1%0):7",(_,_,_)=>0)==7,"Shared remainder precedence/inactive division differs");
        var birth=Birth();Require(birth.ChanceSkillTerm==75,"Pinned rank20/hard DeathMark20 chance term");var rows=BindDemonPrepared.Catalog();
        foreach(var row in rows)
        {
            int threshold=int.Parse(Regex.Match(row.BindChance,@"hpct<(\d+)").Groups[1].Value);
            Require(BindDemonChance.Evaluate(row,birth,12,threshold,0,12345*256).Percent==0,"Strict health threshold changed: "+row.Monster);
            Require(BindDemonChance.Evaluate(row,birth,11,threshold-1,0,12345*256).Percent==0,"Off-cycle progress attempted: "+row.Monster);
            var result=BindDemonChance.Evaluate(row,birth,12,threshold-1,0,12345*256);
            int divisor=row.IndexScope=="MonsterClass"?5:int.Parse(Regex.Match(row.Calcs[3],@"/(\d+)").Groups[1].Value);
            Require(result.Percent==Math.Min(100,75+(101-threshold)/divisor)&&result.Calcs[4]==12345,"Pinned per-species chance/max-life context differs: "+row.Monster);
            if(row.IndexScope=="NamedEncounter"){Require(BindDemonChance.Evaluate(row,Birth(19),12,0,0,256).Percent==0,"Soft ranks bypassed named hard20 gate");}
        }
        var normal=rows.First(r=>r.IndexScope=="MonsterClass"&&r.BindChance.Contains("hpct<85"));
        foreach(var c in new[]{(0,85),(1,77),(2,80),(3,85),(4,77)})
        {Require(BindDemonChance.Evaluate(normal,birth,12,50,c.Item1,256).Percent==c.Item2,"Explicit grouped quality divisor differs");}
        foreach(var c in new[]{(9,2,false),(10,2,true),(14,1,false),(15,1,true),(14,4,false),(15,4,true)})
        {Require((BindDemonChance.Evaluate(normal,Birth(c.Item1),12,0,c.Item2,256).Percent>0)==c.Item3,"Hard champion/unique/minion gate differs");}
        Require(BindDemonChance.Evaluate(normal,Birth(20,255),12,0,0,256).Percent==100&&BindDemonChance.HealthPercent(12345*128-1,12345*256)==49,"Chance clamp or exact health floor differs");
        bool invalid=false;try{_=BindDemonChance.HealthPercent(257,256);}catch(ArgumentOutOfRangeException){invalid=true;}Require(invalid,"Invalid native health accepted");
        Log.Information("Bind channel:all171species strict health gates/offcycle/captured chance/maxhp controls;named hard20 and champion10/unique-minion15;explicit grouped health divisors85/77/80/85/77 at half life;clamp and exact health floor pass,not recovered gate parity");
    }
    private static Daemon Body(PlayerMobile p,int offset=4)
    {
        Daemon b;Creating=true;try{b=new Daemon();}finally{Creating=false;}
        b.Name="BindChannelFixture";b.SetHits(10000);b.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();
        MonsterVariantBaselines.Restore(MonsterVariantBaselines.Snapshot().Where(r=>r.Actor!=b.Serial.Value).ToArray());
        SkillHealth.SetBalance256(b,4000*256-7);return b;
    }
    private static void Mana(PlayerMobile p,int points=90)=>NecromancerEffects.SetMana256(p,points*256);
    private static void Begin(PlayerMobile p,Daemon b,DateTime at,int source=-1)
    {Require(BindDemonChannel.BeginPrepared(p,b,at,out var why,source),"Prepared begin refused: "+why);}
    private static void Scene(NetState network)
    {
        Numbers();Creating=true;PlayerMobile p;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.Name="BindChannelFixture";FixtureSpace.Corridor(p,7080,35,3300);var account=FixtureNetwork.Authenticate(network,p,"bind-channel-");
        var oldRoll=BindDemonChannel.Roll;var oldAura=BoundDemonAuras.Choose;int auraDraws=0;BoundDemonAuras.Choose=level=>{auraDraws++;return oldAura(level);};
        Daemon b=null,other=null;bool kept=false;int rolls=0;var at=TimedEffects.Now.AddMinutes(1);
        try
        {
            Require(!WarlockCombat.Implemented(CombatAbility.BindDemon),"Prepared test opened learned Bind");ClassSkillChecks.Train(p,"warlock.binddemon",20);Mana(p);
            b=Body(p);var baselines=MonsterVariantBaselines.Snapshot();int life=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(p);
            Require(BoundDemonHosts.Qualify(p,b,out var plan,out _)&&MonsterVariantBaselines.Snapshot().SequenceEqual(baselines)&&auraDraws==0&&!b.Controlled,"Preflight allocated baseline,aura RNG or ownership");
            Begin(p,b,at);Require(!BindDemonChannel.BeginPrepared(p,b,at,out _)&&NecromancerEffects.ManaBalance256(p)==mana,"Duplicate press charged/restarted");
            BindDemonChannel.Roll=n=>{rolls++;return 99;};
            Require(!BindDemonChannel.Advance(p,at.AddMilliseconds(999)).Attempted&&rolls==0&&SkillHealth.Balance256(b)==life,"Channel acted before first second");
            var miss=BindDemonChannel.Advance(p,at.AddSeconds(1));Require(miss is{Active:true,Attempted:true,Bound:false,Chance:>0}&&rolls==1&&NecromancerEffects.ManaBalance256(p)==mana-9*256&&!b.Controlled&&auraDraws==0,"Failed roll payment/ownership differs");
            Require(!BindDemonChannel.Advance(p,at.AddSeconds(1)).Attempted&&rolls==1,"Same deadline repeated an attempt");
            BindDemonChannel.Advance(p,at.AddSeconds(20));Require(rolls==2&&BindDemonChannel.State(p).Attempts==2&&NecromancerEffects.ManaBalance256(p)==mana-18*256,"Lag replayed missed attempts/payments");
            BindDemonChannel.Cancel(p);Require(!BindDemonChannel.Advance(p,at.AddSeconds(21)).Attempted,"Cancelled channel resumed");
            Log.Information("Bind channel:nonmutating preflight draws no aura RNG/baseline;press/duplicate/999ms free;failed attempt pays nine mana;one-second/same-frame/lag/no-catchup/cancel controls pass");
            Mana(p,8);Require(!BindDemonChannel.BeginPrepared(p,b,at,out _)&&NecromancerEffects.ManaBalance256(p)==8*256,"Underfunded start charged");Mana(p,9);Begin(p,b,at);Mana(p,8);
            Require(BindDemonChannel.Advance(p,at.AddSeconds(1))is{Active:false,Attempted:false}&&NecromancerEffects.ManaBalance256(p)==8*256,"Underfunded attempt fired/charged");
            Mana(p);Begin(p,b,at);var origin=p.Location;p.X++;Require(!BindDemonChannel.Advance(p,at.AddSeconds(1)).Active&&NecromancerEffects.ManaBalance256(p)==90*256,"Movement failed cancellation");p.Location=origin;
            Begin(p,b,at);p.Paralyzed=true;Require(!BindDemonChannel.Advance(p,at.AddSeconds(1)).Active,"Paralysis failed cancellation");p.Paralyzed=false;
            Begin(p,b,at);var targetPoint=b.Location;b.X=p.X+25;Require(!BindDemonChannel.Advance(p,at.AddSeconds(1)).Active,"Target range loss retargeted/continued");b.Location=targetPoint;
            Begin(p,b,at,1);Require(BindDemonChannel.RenewPrepared(p,1,at.AddMilliseconds(100))&&NecromancerEffects.ManaBalance256(p)==90*256,"Heartbeat spent mana");
            Require(!BindDemonChannel.Advance(p,at.AddSeconds(1)).Active,"Lost transport lease stayed active");
            Log.Information("Bind channel:insufficient start/attempt mana,movement/paralysis/target range and held lease expiry cancel without new debit;heartbeat is free;target remains locked");
            Mana(p);Begin(p,b,at);BindDemonChannel.Roll=n=>{BindDemonChannel.Cancel(p);return 0;};var late=BindDemonChannel.Advance(p,at.AddSeconds(1));Require(late is{Active:false,Attempted:true,Bound:false}&&!b.Controlled&&NecromancerEffects.ManaBalance256(p)==81*256,"Late cancellation captured target or refunded released attempt");
            other=Body(p,8);Mana(p);Begin(p,b,at);long nextId=0;
            BindDemonChannel.Roll=n=>{BindDemonChannel.Cancel(p);Begin(p,other,at.AddSeconds(1));nextId=BindDemonChannel.State(p).Id;return 0;};
            late=BindDemonChannel.Advance(p,at.AddSeconds(1));Require(!late.Bound&&!b.Controlled&&BindDemonChannel.State(p)?.Id==nextId&&BindDemonChannel.State(p).Target==other.Serial.Value,"Old callback removed or captured fresh channel");BindDemonChannel.Cancel(p);
            Mana(p);Begin(p,b,at,1);BindDemonChannel.Roll=n=>{BindDemonChannel.Cancel(p);Begin(p,other,at.AddSeconds(1),1);nextId=BindDemonChannel.State(p).Id;return 0;};
            foreach(int ms in new[]{200,400,600,800}){Require(BindDemonChannel.RenewPrepared(p,1,at.AddMilliseconds(ms)),"Held renewal failed before attempt");}
            Require(!BindDemonChannel.RenewPrepared(p,1,at.AddSeconds(1))&&BindDemonChannel.State(p)?.Id==nextId,"Old renewal removed fresh callback channel");BindDemonChannel.Cancel(p);
            Mana(p);Begin(p,b,at);int nested=0;BindDemonChannel.Roll=n=>{if(nested++==0){BindDemonChannel.Advance(p,at.AddSeconds(2));return 0;}return 99;};
            late=BindDemonChannel.Advance(p,at.AddSeconds(1));Require(!late.Bound&&!b.Controlled&&BindDemonChannel.State(p)?.Attempts==2&&NecromancerEffects.ManaBalance256(p)==72*256,"Old attempt captured target or removed newer attempt");BindDemonChannel.Cancel(p);
            Mana(p);Begin(p,b,at);BindDemonChannel.Roll=n=>100;Require(BindDemonChannel.Advance(p,at.AddSeconds(1))is{Active:false,Bound:false}&&!b.Controlled,"Invalid RNG result bound target");
            BindDemonChannel.Roll=n=>0;Mana(p);Begin(p,b,at);life=SkillHealth.Balance256(b);int maximum=SkillHealth.Maximum256(b);var done=BindDemonChannel.Advance(p,at.AddSeconds(1));b.AIObject.AITimer.Stop();
            Require(done is{Active:false,Attempted:true,Bound:true}&&BoundDemonHosts.For(p).Single()==b&&BindDemonChannel.State(p)==null&&NecromancerEffects.ManaBalance256(p)==81*256
                &&Math.Abs(SkillHealth.Balance256(b)-(long)life*SkillHealth.Maximum256(b)/maximum)<=1&&auraDraws==1,"Successful attempt failed original host snapshot/payment/wounds/aura RNG");
            Log.Information("Bind channel:late cancellation retains spent attempt;stale callback cannot delete fresh channel;invalid RNG refuses;successful attempt binds original wounded body once with one aura draw and no nine-mana upfront debit");
            Begin(p,other,at.AddSeconds(2));PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,other.Serial.Value,BoundDemonHosts.State(b),SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p))));kept=true;
        }
        finally
        {
            BindDemonChannel.Cancel(p);BindDemonChannel.Roll=oldRoll;BoundDemonAuras.Choose=oldAura;network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){b?.Delete();other?.Delete();p.Delete();BoundDemonHosts.Tick();}
        }
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many channel reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(Daemon)World.FindMobile((Serial)saved.Bound);var target=(Daemon)World.FindMobile((Serial)saved.Target);
        Require(BindDemonChannel.State(p)==null,"Reload restored held intent");
        Require(BoundDemonHosts.State(body)==saved.State,"Reload altered binding: "+JsonSerializer.Serialize(BoundDemonHosts.State(body))+" expected "+JsonSerializer.Serialize(saved.State));
        Require(SkillHealth.Balance256(body)==saved.Life,$"Reload altered bound wounds {SkillHealth.Balance256(body)} expected {saved.Life}");
        Require(NecromancerEffects.ManaBalance256(p)==saved.Mana,$"Reload altered owner mana {NecromancerEffects.ManaBalance256(p)} expected {saved.Mana}");
        Require(!target.Controlled,"Reload captured target without held intent");
        var account=FixtureNetwork.Authenticate(network,p,"bind-channel-reload-");
        try
        {
            PersistentMinions.Resume(p);body.AIObject.AITimer.Stop();target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
            Begin(p,target,TimedEffects.Now);BindDemonChannel.Cancel(p);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,State=BoundDemonHosts.State(body),Life=SkillHealth.Balance256(body),Mana=NecromancerEffects.ManaBalance256(p)}));
            Log.Information("Bind channel:saved original bound snapshot/wounds/resources survive reload;held intent absent,no offline roll/debit;fresh begin/cancel works without replacing existing demon");
        }
        finally{BindDemonChannel.Cancel(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
