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

public static class BoundDemonDeathMarkChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonDeathMarkChecks));
    private sealed record Pet(uint Actor,int Life,string Birth,long Order);
    private sealed record Saved(int Generation,uint Owner,uint Enemy,uint Bound,uint Weapon,BoundDemonState State,int Life,int Mana,int Stamina,Pet[] Summons);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-death-mark-fixtures.json");
    private static bool Creating;
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BoundDeathMarkFixture"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-death-mark-reload-check":"bound-demon-death-mark-check";
            ConsoleInputHandler.RegisterCommand(name,"Native bound Death Mark movement/commands/controls/mixed roster/saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch{WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,_=>throw new NotSupportedException()});
    private static Pet Capture(SkeletonGuardian b)=>new(b.Serial.Value,SkillHealth.Balance256(b),Birth(b),BattleCommand.PetOrder(b));
    private static void Ready(PlayerMobile p)
    {
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        var fire=FireGround.Snapshot();FireGround.Restore(fire with{Cooldowns=fire.Cooldowns.Where(c=>c.Owner!=p.Serial.Value||c.Ability!=CombatAbility.DeathMark).ToArray()});
        NecromancerEffects.SetMana256(p,p.ManaMax*256);
    }
    private static DeathMarkCasting.Pending Cast(PlayerMobile p,Mobile enemy,Point3D point,bool success)
    {
        Ready(p);var a=DeathMarkCasting.BeginPrepared(p,enemy?.Serial??Serial.Zero,point,Core.TickCount,0);Require(a!=null,"Mixed Death Mark startup refused");
        int mana=NecromancerEffects.ManaBalance256(p);long at=ClassCombat.For(p).EffectAt;
        Require(!ClassCombat.ResolveAction(p,at-1)&&NecromancerEffects.ManaBalance256(p)==mana,"Mixed windup spent mana");
        Require(ClassCombat.ResolveAction(p,at)==success&&a.Released&&!ClassCombat.ResolveAction(p,at+1),"Mixed action result/replay changed");
        Require(a.ManaPaid256==(success?SkillMechanics.Values(p,CombatAbility.DeathMark).Mana256:0)&&NecromancerEffects.ManaBalance256(p)==mana-a.ManaPaid256,"Mixed release payment differs");return a;
    }
    private static DeathMarkDirective Directive(PlayerMobile p,Mobile enemy,bool clear=true)=>new()
    {X=(p.X+12)*2,Y=p.Y*2,Remaining=1,TargetType=enemy==null?0:2,TargetId=enemy==null?-1:checked((int)enemy.Serial.Value),ClearControl=clear};
    private static void Controls(PlayerMobile p,Daemon body,Mobile enemy)
    {
        var at=TimedEffects.Now;long tick=Core.TickCount;
        Require(CombatStuns.ApplyPreparedDuration(body,200,at)&&FreezeStates.Commit(enemy,body,500,true,1,tick)&&ColdStates.Commit(enemy,body,500,true,-50,1,tick),"Bound control fixture missing");
        Require(TimedEffects.PoisonFrames(enemy,body,1,1500,at),"Bound poison fixture missing");
        var timed=TimedEffects.Actor(body,at);timed.Control=timed.Control with{FearUntil=at.AddMinutes(1),FearSource=enemy.Serial.Value,FearX=enemy.X,FearY=enemy.Y};
        int life=SkillHealth.Balance256(body),mana=body.Mana,poison=timed.PoisonMs;var clock=timed.PoisonClock;var state=BoundDemonHosts.State(body);var ai=body.AIObject;
        var d=Directive(p,enemy,false);Require(DeathMarkBoundWorld.ApplyPrepared(p,body,d,at)&&d.Remaining==0&&CombatStuns.Remaining(body)>0&&FreezeStates.Shattered(body)&&ColdStates.OtherRate(body)==-50,"Unrequested bound controls cleared");
        d=Directive(p,enemy);Require(DeathMarkBoundWorld.ApplyPrepared(p,body,d,at)&&d.Remaining==0&&CombatStuns.Remaining(body)==0&&!FreezeStates.Shattered(body)&&ColdStates.OtherRate(body)==0,"Bound stun/freeze/chill not cleared");
        Require(BoundDemonHosts.State(body)==state&&body.AIObject==ai&&SkillHealth.Balance256(body)==life&&body.Mana==mana&&timed.Control.FearUntil==at.AddMinutes(1)&&timed.PoisonMs==poison&&timed.PoisonClock==clock,"Bound control clearing rewrote identity/resources/poison/fear");
        var point=body.Location;body.Paralyzed=true;bool refused=false;try{DeathMarkBoundWorld.ApplyPrepared(p,body,Directive(p,enemy),at);}catch(NotSupportedException){refused=true;}finally{body.Paralyzed=false;}
        Require(refused&&body.Location==point,"Untagged native paralysis was cleared or moved");
        TimedEffects.CleansePoison(body,0,TimedEffects.Now);timed.Control=timed.Control with{FearUntil=default};
        body.AIObject.AITimer.Stop();
    }
    private static void Scene(NetState network)
    {
        PlayerMobile p;Daemon body;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);body=new Daemon();}finally{Creating=false;}
        p.Name=body.Name="BoundDeathMarkFixture";p.AddItem(new Backpack());
        foreach(var c in new[]{("warlock.binddemon",1),("warlock.demonicmastery",10),("warlock.deathmark",6)}){ClassSkillChecks.Train(p,c.Item1,c.Item2);}
        FixtureSpace.Corridor(p,7040,45,3450);var account=FixtureNetwork.Authenticate(network,p,"bound-mark-");bool kept=false;SkeletonGuardian[] summons=[];
        var enemy=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;enemy.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();
        try
        {
            var goat=WarlockGoatmanWorld.Create(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now);WarlockGoatmanWorld.Admit(goat);
            body.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);body.AIObject?.AITimer.Stop();var ai=body.AIObject;var weapon=new Dagger();body.AddItem(weapon);
            Require(BoundDemonHosts.BindPrepared(p,body,out var why),"Bound Mark setup: "+why);body.AIObject.AITimer.Stop();
            var tainted=WarlockGoatmanWorld.Create(p,new(p.X+6,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonTainted);WarlockGoatmanWorld.Admit(tainted);summons=[goat,tainted];
            var all=new BaseCreature[]{goat,body,tainted};Require(DeathMarkNativeRelease.Roster(p).SequenceEqual(all),"Mixed roster creation order differs");
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/2-17);foreach(var b in summons){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-333);}
            Controls(p,body,enemy);int life=SkillHealth.Balance256(body);var state=BoundDemonHosts.State(body);var births=summons.Select(Birth).ToArray();
            var action=Cast(p,enemy,enemy.Location,true);Require(action.Result is{Result:1,EligibleDemons:3}&&body.ControlOrder==OrderType.Attack&&body.ControlTarget==enemy&&body.Combatant==enemy&&DeathMarkDebuffs.State(enemy)is{Rank:6,CastId:0},"Mixed native command/mark/action failed");
            Require(body.Location!=enemy.Location&&body.InRange(enemy,2)&&Teleportation.Space(body.Map,body.Location,body,true)&&body.AIObject==ai&&BoundDemonHosts.State(body)==state&&body.FindItemOnLayer(Layer.OneHanded)==weapon&&SkillHealth.Balance256(body)==life&&summons.Select(Birth).SequenceEqual(births),"Landing overlapped enemy or changed native identity/wounds/birth/equipment");
            Cast(p,null,new(p.X+8,p.Y+2,p.Z),true);Require(body.ControlOrder==OrderType.Guard&&body.ControlTarget==null&&body.Combatant==null,"Ground mark did not use native guard");
            Require(DeathMarkDebuffs.ApplyPrepared(p,enemy,255,TimedEffects.Now),"Stronger mark setup");enemy.MoveToWorld(new(p.X+16,p.Y,p.Z),p.Map);var before=body.Location;
            action=Cast(p,enemy,enemy.Location,false);Require(action.Result is{Result:0,ServerDoRan:true}&&before!=body.Location&&body.ControlTarget==enemy&&DeathMarkDebuffs.State(enemy)is{Rank:255}&&FireGround.Ready(p,CombatAbility.DeathMark),"Late mark failure rolled back native warp or paid/started delay");
            body.Blessed=true;bool refused=false;try{DeathMarkNativeRelease.Roster(p);}catch(NotSupportedException){refused=true;}finally{body.Blessed=false;}Require(refused,"Unqualified bound member silently skipped");
            var map=body.Map;var point=body.Location;body.MoveToWorld(point,Map.Felucca);Require(DeathMarkNativeRelease.Roster(p).SequenceEqual(summons),"Other-facet bound entered roster");body.MoveToWorld(point,map);body.AIObject.AITimer.Stop();
            PersistentMinions.Suspend(p);Require(DeathMarkNativeRelease.Roster(p).Length==0,"Parked roster became eligible");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,enemy.Serial.Value,body.Serial.Value,weapon.Serial.Value,state,SkillHealth.Balance256(body),body.Mana,body.Stam,summons.Select(Capture).ToArray())));kept=true;
            Log.Information("Bound Death Mark:mixed saved-order roster/shared SC/once-only mana/cooldown;actual original Daemon nearby fit and native attack/guard;late mark failure preserves warps unpaid;stun/freeze/chill share existing managers while wounds/AI/equipment/poison/fear persist;untagged paralysis/faulted/other-facet/parked guards verified");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){body.Delete();foreach(var b in summons){b.Delete();}enemy.Delete();p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many bound Death Mark reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var body=(Daemon)World.FindMobile((Serial)s.Bound);var enemy=(BaseCreature)World.FindMobile((Serial)s.Enemy);
        Require(BoundDemonHosts.State(body)==s.State&&body.Map==Map.Internal&&SkillHealth.Balance256(body)==s.Life&&body.Mana==s.Mana&&body.Stam==s.Stamina&&body.FindItemOnLayer(Layer.OneHanded)?.Serial.Value==s.Weapon,"Saved bound original changed/recovered");
        var summons=s.Summons.Select(x=>(SkeletonGuardian)World.FindMobile((Serial)x.Actor)).ToArray();Require(summons.Select(Capture).SequenceEqual(s.Summons),"Saved original summon birth/wounds/order changed");
        var account=FixtureNetwork.Authenticate(network,p,"bound-mark-reload-");try
        {
            PersistentMinions.Resume(p);WarlockGoatmanWorld.Tick(TimedEffects.Now);body.AIObject.AITimer.Stop();Require(DeathMarkNativeRelease.Roster(p).Select(b=>b.Serial.Value).SequenceEqual(new[]{s.Summons[0].Actor,s.Bound,s.Summons[1].Actor}),"Reloaded mixed creation order differs");
            DeathMarkDebuffs.Clear(enemy);enemy.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);var a=Cast(p,enemy,enemy.Location,true);body.AIObject.AITimer.Stop();
            Require(a.Result is{EligibleDemons:3}&&body.ControlTarget==enemy&&body.Combatant==enemy&&BoundDemonHosts.State(body)==s.State&&SkillHealth.Balance256(body)==s.Life,"Reloaded native command changed original/wounds");
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Summons=summons.Select(Capture).ToArray()}));
            Log.Information("Bound Death Mark:original bound type/serial/weapon/stats/wounds/resources and mixed creation order survive reload;shared player action resumes native attack and original source controllers without adoption or reset");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
