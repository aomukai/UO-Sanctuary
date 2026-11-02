// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BladeCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeCastChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,uint Helm,Point3D Origin,int Map,int Mana,int Health,BladeBodyState Actor,BladeMissileState Missile,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-cast-fixtures.json");
    private static bool _creating;
    private static void Rates(){var mp=Mobile.ManaRegenRateHandler;var hp=Mobile.HitsRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name?.StartsWith("BladeCastFixture")==true?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;Mobile.HitsRegenRateHandler=p=>_creating||p.Name?.StartsWith("BladeCastFixture")==true?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Blade Sentinel S2/payment/input and original actor reload",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Rates();Register("blade-cast-availability-check",Availability);Register("blade-cast-mechanics-check",Mechanics);Register("blade-cast-check",()=>FixtureNetwork.Connected("blade-cast-result",Save));Register("blade-cast-reload-check",()=>FixtureNetwork.Connected("blade-cast-reload-result",Reload));
        foreach(string mode in new[]{"left","right","quick"}){string label="blade-cast-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=BladeBodyChecks.Fresh();}finally{_creating=false;}p.Name="BladeCastFixture";p.Mana=p.ManaMax;return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=Core.TickCount;s.RecoveryUntil=0;p.NextSkillTime=p.NextSpellTime=0;}
    private static Point3D Aim(PlayerMobile p)=>new(p.X+4,p.Y,p.Z);
    private static void Clean(PlayerMobile p)
    {foreach(var entry in AssassinTrapRoster.For(p)){World.FindMobile((Serial)entry.Actor)?.Delete();}ClassCombat.Stop(p);p.Delete();}
    private static BladeSentinelBody Cast(PlayerMobile p)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.BladeSentinel,null,Aim(p),Core.TickCount),"Learned cast begin");var s=ClassCombat.For(p);long next=s.NextAction;
        Require(s.Animation==9&&s.Timing==BladeSentinelCasts.Timing(p)&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinTrapRoster.For(p).Length==0,"S2 windup paid/spawned early");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-1792,"Release did not pay exactly7 once");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&s.NextAction==next,"Duplicate release/reset deadline");return (BladeSentinelBody)World.FindMobile((Serial)AssassinTrapRoster.For(p).Single().Actor);
    }
    private static void Availability()
    {
        var p=Fresh();try
        {
            Require(ClassCombat.Available(p,CombatAbility.BladeSentinel),"Learned Blade Sentinel is unavailable");
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BladeSentinel)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BladeSentinel)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BladeSentinel),"Learned blade binding acquisition");
            Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Barbarian)&&!ClassCombat.Available(p,CombatAbility.BladeSentinel),"Inactive Assassin retained blade availability");
            Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Assassin)&&ClassCombat.Available(p,CombatAbility.BladeSentinel)
                &&SkillBindings.Instance.Get(p)==(CombatAbility.BladeSentinel,CombatAbility.BladeSentinel)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==CombatAbility.BladeSentinel,"Assassin history lost learned skill/bindings");
        }
        finally{Clean(p);}
    }
    private static void Mechanics()
    {
        var p=Fresh();try
        {
            Require(ClassCombat.Available(p,CombatAbility.BladeSentinel)&&BladeSentinelCasts.Supported(p),"Learned handler unavailable");
            var basis=CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.BladeSentinel,new(AttackFamily.Unarmed,0));
            Require(basis==new ActionTiming(15,8)&&CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.BladeSentinel,new(AttackFamily.Unarmed,0),fcr:200)==basis
                &&CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.BladeSentinel,new(AttackFamily.Unarmed,0),ias:200)==new ActionTiming(9,5),"S2 IAS/FCR policy");
            NecromancerEffects.SetMana256(p,1791);Ready(p);long next=ClassCombat.For(p).NextAction;Require(!ClassCombat.Perform(p,CombatAbility.BladeSentinel,null,Aim(p),Core.TickCount)&&ClassCombat.For(p).NextAction==next&&NecromancerEffects.ManaBalance256(p)==1791,"Low-mana begin changed state");
            NecromancerEffects.SetMana256(p,1792);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.BladeSentinel,null,Aim(p),Core.TickCount),"Cancellation begin");var s=ClassCombat.For(p);long effect=s.EffectAt;next=s.NextAction;
            ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==1792&&AssassinTrapRoster.For(p).Length==0&&s.NextAction==next,"Cancellation created/paid/refunded deadline");
            var seed=BladeSentinelSummons.SeedLow;int calls=0;try{BladeSentinelSummons.SeedLow=()=>{calls++;return 19;};var body=Cast(p);Require(calls==1&&!body.Prepared.Creation.MissileArmed&&!FireGround.Ready(p,CombatAbility.BladeSentinel)&&FireGround.Ready(p,CombatAbility.FireBlast),"Factory seed/first-arm/local-delay order");Atomic(body);}
            finally{BladeSentinelSummons.SeedLow=seed;}
        }
        finally{Clean(p);}
        p=Fresh();try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var origin=p.Location;Require(ClassCombat.Perform(p,CombatAbility.BladeSentinel,null,Aim(p),Core.TickCount),"Move cancellation begin");long effect=ClassCombat.For(p).EffectAt;
            p.Direction=Direction.West;Require(p.Move(Direction.West)&&p.Location!=origin,"Accepted cancellation step");Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinTrapRoster.For(p).Length==0,"Move cancellation paid/created blade");
        }
        finally{Clean(p);}
        Log.Information("Learned blade:S2 IAS/no FCR,unpaid windup,exact7mana,duplicate/low-mana/stop/movement,one factory seed,independent local delay and atomic saved pools");
    }
    private static void Atomic(BladeSentinelBody body)
    {
        // Arm at the real actor's due time, using its actual captured owner.
        body.AdvanceController(body.Prepared.NextFrameUtc);var before=BladeSentinelMissiles.Snapshot();Require(before.Missiles.Any(x=>x.Owner==body.Serial.Value),"Missing actual saved missile");
        string json=JsonSerializer.Serialize(before);var row=before.Missiles.Single(x=>x.Owner==body.Serial.Value);
        try
        {
            foreach(var bad in new[]{before with{Missiles=before.Missiles.Where(x=>x.Id!=row.Id).Append(row with{Rank=0}).ToArray()},before with{Missiles=before.Missiles.Append(row).ToArray()},before with{Missiles=before.Missiles.Prepend(null).ToArray()},before with{Leases=before.Leases.Append(new WeaponFlightLease(row.Id,body.Serial.Value,DateTime.SpecifyKind(DateTime.Now,DateTimeKind.Unspecified))).ToArray()}})
            {bool refused=false;try{BladeSentinelMissiles.Restore(bad);}catch(InvalidDataException){refused=true;}Require(refused&&JsonSerializer.Serialize(BladeSentinelMissiles.Snapshot())==json,"Blade two-pool restore was not atomic");}
        }
        finally{BladeSentinelMissiles.Restore(before);}
    }
    private static BladeMissileState Missile(BladeSentinelBody body)=>BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==body.Serial.Value);
    private static void Clock(BladeSentinelBody body,DateTime at)
    {
        var state=body.Prepared with{NextFrameUtc=at};typeof(BladeSentinelBody).GetMethod("Store",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(body,[state]);
        var pool=BladeSentinelMissiles.Snapshot();BladeSentinelMissiles.Restore(pool with{Missiles=pool.Missiles.Select(x=>x.Owner==body.Serial.Value?x with{NextUtc=at}:x).ToArray()});
    }
    private static void Hold(PlayerMobile p,BladeSentinelBody body,BladeMissileTarget target,Item helm,Point3D origin,int map,int generation)
    {
        Clock(body,TimedEffects.Now.Add(FixtureSpace.Retention));p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,target.Serial.Value,helm.Serial.Value,origin,map,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),body.Prepared,Missile(body),generation)));
    }
    private static void Save(NetState network)
    {
        var p=Fresh();while(ClassCreation.Profiles.GetRank(p.Serial,"assassin.bladesentinel")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"assassin.bladesentinel",out _),"Train lifetime");}
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(p.EquipItem(helm),"Equip real soft-rank helm");ClassGear.Refresh(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BladeSentinel)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BladeSentinel)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BladeSentinel),"Learned left/right/quick bindings");
        FixtureNetwork.Authenticate(network,p,"blade-cast-");var body=Cast(p);var origin=p.Location;int map=p.Map.MapID;var target=new BladeMissileTarget{Frozen=true,CantWalk=true,NoKillAwards=true};target.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;p.MoveToWorld(new(p.X-2,p.Y,p.Z),p.Map);origin=p.Location;
        long end=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                Require(!body.Deleted&&body.Prepared.ControllerFault==null,"Paid actor expired/faulted before natural observation");
                if(!body.Prepared.Creation.MissileArmed||SkillHealth.Balance256(target)==50000*256){Require(Core.TickCount<end,"Natural paid blade did not arm/damage");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(Missile(body).Fault==null&&body.Prepared.Creation.Rank==21&&body.Prepared.Control.Creates==1,"Natural effective rank/first-arm fault");Hold(p,body,target,helm,origin,map,0);Log.Information("blade-cast-result: passed");
            }
            catch(Exception e){Clean(p);target.Delete();Log.Error(e,"blade-cast-result: failed");}
        }
        Observe();
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(BladeSentinelBody)World.FindMobile((Serial)saved.Body);var target=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);var helm=World.FindItem((Serial)saved.Helm);
        Require(saved.Generation<2&&p!=null&&body!=null&&target!=null&&helm?.Parent==p&&JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.Actor)&&JsonSerializer.Serialize(Missile(body))==JsonSerializer.Serialize(saved.Missile)
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(target)==saved.Health,"Original paid actor/missile/gear/resources changed on reload");
        Require(SkillBindings.Instance.Get(p)==(CombatAbility.BladeSentinel,CombatAbility.BladeSentinel)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==CombatAbility.BladeSentinel,"Original binding reload");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);FixtureNetwork.Authenticate(network,p,"blade-resume-");int frame=body.Prepared.Control.Frame;Clock(body,TimedEffects.Now.AddMilliseconds(40));
        long end=Core.TickCount+3000;
        void Observe()
        {
            try
            {
                Require(!body.Deleted&&body.Prepared.ControllerFault==null&&Missile(body).Fault==null,"Original actor resume fault/death");
                if(body.Prepared.Control.Frame<=frame+3){Require(Core.TickCount<end,"Original controller did not naturally resume");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(body.Prepared.Control.Creates==1&&Missile(body).Id==saved.Missile.Id&&JsonSerializer.Serialize(Missile(body).Damage)==JsonSerializer.Serialize(saved.Missile.Damage)&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Resume recast/rearmed/recollected/paid");
                Hold(p,body,target,helm,saved.Origin,saved.Map,saved.Generation+1);Log.Information("blade-cast-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"blade-cast-reload-result: failed");}
        }
        Observe();
    }
    private static void Input(NetState network,string label)
    {
        var p=Fresh();Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BladeSentinel)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BladeSentinel)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BladeSentinel),"Input bindings");FixtureNetwork.Authenticate(network,p,"blade-input-");Ready(p);var aim=Aim(p);int mana=NecromancerEffects.ManaBalance256(p);bool windup=false;long end=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                var session=ClassCombat.For(p);if(!windup&&session.PendingAbility==CombatAbility.BladeSentinel){Require(session.Animation==9&&session.Timing==BladeSentinelCasts.Timing(p)&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinTrapRoster.For(p).Length==0,"Input paid/created before S2 release");windup=true;Log.Information("{Label}: windup",label);}
                var traps=AssassinTrapRoster.For(p);if(traps.Length==0){Require(Core.TickCount<end,"Input did not create blade");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(windup&&traps.Length==1&&NecromancerEffects.ManaBalance256(p)==mana-1792&&!FireGround.Ready(p,CombatAbility.BladeSentinel),"Input count/payment/local delay");Clean(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.BladeSentinel,(int)DiabloClass.Assassin,aim.X,aim.Y,aim.Z);Observe();});
    }
}
