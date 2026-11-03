// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SlowMissilesWindupChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SlowMissilesWindupChecks));
    private sealed record Saved(uint Owner,uint Target,int Mana,SlowMissilesState State,Point3D Point,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"slow-missiles-windup-fixtures.json");
    private static bool _creating;
    private static void Freeze(){var mana=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="SlowWindupFixture"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    public static void Configure()
    {
        Freeze();Register("slow-missiles-windup-check",()=>Check(false));Register("slow-missiles-windup-reload-check",()=>Check(true));
        ConsoleInputHandler.RegisterCommand("slow-missiles-windup-live","Natural prepared SC/payment and actual hostile missile capture",_=>Core.LoopContext.Post(()=>
        FixtureNetwork.Connected("slow-missiles-windup-live-result",network=>
        {
            var p=Owner();var enemy=Enemy(p);var account=FixtureNetwork.Authenticate(network,p,"slow-windup-");Equip(p,"Magefist");Ready(p);
            int mana=NecromancerEffects.ManaBalance256(p);bool began=false,finished=false;long deadline=Core.TickCount+10000;
            void Cleanup(){EliteBolts.Cancel(enemy);AmazonSlowMissiles.Clear(enemy);enemy.Delete();p.Delete();account.Delete();}
            void Inspect()
            {
                if(finished){return;}
                try
                {
                    Require(Core.TickCount<deadline,"Natural prepared Slow Missiles timeout");var action=ClassCombat.For(p);
                    if(action.SlowMissilesCast!=null&&!began)
                    {Require(action.Timing==new ActionTiming(18,10)&&AmazonSlowMissiles.State(enemy)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Natural unpaid SC/FCR windup");began=true;}
                    if(AmazonSlowMissiles.State(enemy)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                    Require(began&&action.SlowMissilesCast==null&&action.PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(p)==mana-1280,"Natural once-only effect/payment");
                    Captured(enemy);finished=true;ClassCombat.Stop(p);Cleanup();Log.Information("slow-missiles-windup-live-result: passed");
                }
                catch(Exception e){finished=true;Cleanup();Log.Error(e,"slow-missiles-windup-live-result: failed");}
            }
            if(!AmazonSlowMissilesCasting.BeginPrepared(p,Core.TickCount)){Cleanup();throw new InvalidOperationException("Natural prepared Slow Missiles start refused");}
            Log.Information("slow-missiles-windup-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
        })));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Slow Missiles windup/cancel/save checks",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        // RawInt initialization can create the regen timer before the fixture is renamed.
        // Freeze its first interval as well as later name-based intervals.
        PlayerMobile p;_creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Amazon);}finally{_creating=false;}p.Name="SlowWindupFixture";
        if(p.Backpack==null){p.AddItem(new Backpack());}
        while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Source gear Strength");}
        FixtureSpace.Corridor(p,6200,26,3600);Ready(p);return p;
    }
    private static EncounterCreature Enemy(PlayerMobile p)
    {var c=new EncounterCreature{Frozen=true,CantWalk=true,NoKillAwards=true};c.AIObject.AITimer.Stop();c.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);return c;}
    private static Item Equip(PlayerMobile p,string key)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Equip "+key);ClassGear.Refresh(p);return item;}
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,50*256+17);}
    private static void Captured(EncounterCreature enemy)
    {
        var state=AmazonSlowMissiles.State(enemy);Require(state!=null,"Prepared release state absent");
        EliteBolts.Lightning(enemy);var shots=EliteBolts.Inspect(enemy);
        Require(shots.Length==8&&shots.All(x=>x.Slow==new SlowMissilePayload(state.VelocityPercent,state.DamagePercent)),"Prepared windup state did not reach actual missile factory");
        AmazonSlowMissiles.Clear(enemy);Require(EliteBolts.Inspect(enemy).SequenceEqual(shots),"Clearing target state mutated original launched missiles");EliteBolts.Cancel(enemy);
    }
    private static void Stage(PlayerMobile p)
    {
        Require(AmazonSlowMissilesCasting.BeginPrepared(p,Core.TickCount),"Prepared unpaid save windup");
        var action=ClassCombat.For(p);action.EffectAt+=FixtureSpace.RetentionMilliseconds;action.NextAction+=FixtureSpace.RetentionMilliseconds;
    }
    private static void Check(bool reload)
    {
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=World.FindMobile((Serial)s.Target);
            Require(p!=null&&target!=null&&AmazonSlowMissiles.State(target)==s.State&&NecromancerEffects.ManaBalance256(p)==s.Mana
                &&ClassCombat.For(p).SlowMissilesCast==null&&ClassCombat.For(p).PendingAbility==CombatAbility.None,$"Saved paid state/resources or unpaid transient context replayed: owner={p?.Serial},target={target?.Serial},actualState={JsonSerializer.Serialize(AmazonSlowMissiles.State(target))},expectedState={JsonSerializer.Serialize(s.State)},mana={(p==null?0:NecromancerEffects.ManaBalance256(p))}/{s.Mana},pending={(p==null?CombatAbility.None:ClassCombat.For(p).PendingAbility)},prepared={(p!=null&&ClassCombat.For(p).SlowMissilesCast!=null)}");
            p.MoveToWorld(s.Point,Map.Maps[s.Map]);Ready(p);Stage(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
            Log.Information("Slow Missiles windup:generation{Generation},same paid recipient/resources;unpaid context transient",s.Generation+1);return;
        }
        var owner=Owner();var enemy=Enemy(owner);
        Require(!ClassCombat.Available(owner,CombatAbility.SlowMissiles)&&!SkillBindings.Instance.Assign(owner,false,CombatAbility.SlowMissiles)
            &&!ClassCombat.Perform(owner,CombatAbility.SlowMissiles,null,owner.Location,Core.TickCount),"Prepared start bypassed public unavailable gate");
        int mana=NecromancerEffects.ManaBalance256(owner);long now=Core.TickCount;
        Require(AmazonSlowMissilesCasting.BeginPrepared(owner,now),"Prepared SC start");var action=ClassCombat.For(owner);
        Require(action.Timing==new ActionTiming(20,11)&&action.PendingPoint==owner.Location&&action.PendingTarget==Serial.Zero
            &&NecromancerEffects.ManaBalance256(owner)==mana&&AmazonSlowMissiles.State(enemy)==null,"Prepared source SC/self target/unpaid windup");
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt-1)&&ClassCombat.ResolveAction(owner,action.EffectAt)
            &&!ClassCombat.ResolveAction(owner,action.EffectAt)&&action.SlowMissilesCast==null,"Prepared early/duplicate release");
        Require(NecromancerEffects.ManaBalance256(owner)==mana-1280&&AmazonSlowMissiles.State(enemy) is{Rank:1,VelocityPercent:68,DamagePercent:94},"Prepared once-only do6 effect/payment");
        Captured(enemy);Require(!AmazonSlowMissilesCasting.BeginPrepared(owner,action.NextAction-1),"Prepared SC ignored animation tail");
        Ready(owner);Require(AmazonSlowMissilesCasting.BeginPrepared(owner,Core.TickCount),"Cancelled begin");long effect=action.EffectAt;ClassCombat.Stop(owner);
        Require(!ClassCombat.ResolveAction(owner,effect)&&NecromancerEffects.ManaBalance256(owner)==50*256+17&&AmazonSlowMissiles.State(enemy)==null,"Stop paid or retained pending context");
        Ready(owner);Require(AmazonSlowMissilesCasting.BeginPrepared(owner,Core.TickCount),"Movement begin");effect=action.EffectAt;var point=owner.Location;
        owner.MoveToWorld(new(point.X,point.Y+1,point.Z),owner.Map);
        Require(action.SlowMissilesCast==null&&!ClassCombat.ResolveAction(owner,effect)&&AmazonSlowMissiles.State(enemy)==null,"Movement retained prepared context");owner.MoveToWorld(point,owner.Map);
        Ready(owner);Require(AmazonSlowMissilesCasting.BeginPrepared(owner,Core.TickCount),"Low-release begin");effect=action.EffectAt;NecromancerEffects.SetMana256(owner,0);
        Require(!ClassCombat.ResolveAction(owner,effect)&&action.SlowMissilesCast==null&&AmazonSlowMissiles.State(enemy)==null,"Failed release retained/replayed context");
        Ready(owner);Require(AmazonSlowMissilesCasting.BeginPrepared(owner,Core.TickCount),"Frozen begin");effect=action.EffectAt;owner.Frozen=true;
        Require(!ClassCombat.ResolveAction(owner,effect)&&action.SlowMissilesCast==null&&NecromancerEffects.ManaBalance256(owner)==50*256+17,"Frozen caster paid");owner.Frozen=false;
        Ready(owner);Require(AmazonSlowMissilesCasting.BeginPrepared(owner,Core.TickCount),"Changed weapon begin");effect=action.EffectAt;var dagger=Equip(owner,"Gull");
        Require(!ClassCombat.ResolveAction(owner,effect)&&action.SlowMissilesCast==null&&AmazonSlowMissiles.State(enemy)==null,"Changed weapon retained windup");dagger.Delete();ClassGear.Refresh(owner);
        Ready(owner);Require(AmazonSlowMissilesCasting.BeginPrepared(owner,Core.TickCount),"Current rank begin");effect=action.EffectAt;var helm=Equip(owner,"Tarnhelm");
        Require(ClassCombat.ResolveAction(owner,effect)&&AmazonSlowMissiles.State(enemy)?.Rank==2,"Effect used a stale rank");helm.Delete();ClassGear.Refresh(owner);
        var state=AmazonSlowMissiles.State(enemy) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Where(x=>x.Target!=enemy.Serial.Value).Append(state).ToArray());
        enemy.Internalize();Ready(owner);Stage(owner);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,enemy.Serial.Value,NecromancerEffects.ManaBalance256(owner),state,owner.Location,owner.Map.MapID,0)));
        Log.Information("Slow Missiles windup:source SC/self target,unpaid start,once-only live-rank release,stop/move/mana/frozen/weapon cancellation,8captured bolts and saved transient pass;public talent remains gated");
    }
}
