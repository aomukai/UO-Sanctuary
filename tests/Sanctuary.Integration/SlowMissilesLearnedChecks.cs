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

public static class SlowMissilesLearnedChecks
{
    private const CombatAbility Ability=CombatAbility.SlowMissiles;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SlowMissilesLearnedChecks));
    private sealed record Saved(int Generation,uint Owner,uint Weapon,uint Target,Point3D Origin,int Mana,int Life,SlowMissilesState State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"slow-missiles-learned-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name?.StartsWith("SlowLearned",StringComparison.Ordinal)==true)&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name?.StartsWith("SlowLearned",StringComparison.Ordinal)==true)&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        // Frozen and stopped AI are fixture controls, not persistent creature properties.
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));if(World.FindMobile((Serial)saved.Target)is BaseCreature t){t.Frozen=t.CantWalk=true;t.Combatant=null;t.AIObject?.AITimer.Stop();}}};
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"slow-missiles-learned-reload-check":"slow-missiles-learned-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved learned Slow Missiles admission/bindings/lifecycle/saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
        foreach(string choice in new[]{"right","quick","fcr-quick"})
        {
            string mode=choice,label="slow-missiles-learned-"+choice;
            ConsoleInputHandler.RegisterCommand(label+"-check","Actual self-cast packets and automatic hostile D2 missile delivery",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,mode));Log.Information("{Name}: passed",label+"-check");}catch(Exception e){Log.Error(e,"{Name}: failed",label+"-check");}}));
        }
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Name="SlowLearnedFixture",Player=true,Female=true,Body=0x191};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.slowmissiles",20);
        FixtureSpace.Corridor(p,6300,30,3500);var w=new Dagger();p.AddToBackpack(w);Require(p.EquipItem(w),"Original Slow Missiles dagger");ClassGear.Refresh(p);Ready(p);return p;
    }
    private static GraveyardLich Enemy(PlayerMobile p)
    {var t=new GraveyardLich{Name="SlowLearnedLich",Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();return t;}
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,80*256+17);}
    private static SlowMissilesState Cast(PlayerMobile p,GraveyardLich t)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount),"Public self cast start");var s=ClassCombat.For(p);
        Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&AmazonCastClock.TryTiming(p,out var timing)&&s.Timing==timing&&NecromancerEffects.ManaBalance256(p)==mana,"Public caster-centered/unpaid SC");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-1280,"One public release/payment");return AmazonSlowMissiles.State(t);
    }
    private static void Scene(NetState network)
    {
        var p=Fresh();var t=Enemy(p);var account=FixtureNetwork.Authenticate(network,p,"slow-learned-");bool kept=false;
        try
        {
            Require(SlowMissilesRules.Approved&&ClassCombat.Available(p,Ability)&&ClassAbilities.SelfTarget(Ability)&&!SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Right/quick/self metadata");
            var state=Cast(p,t);Require(state is{Rank:20,VelocityPercent:33,DamagePercent:67},"Public state missed native target");ClassCombat.Stop(p);Require(AmazonSlowMissiles.State(t)==state,"Stop recalled released effect");AmazonSlowMissiles.Clear(t);
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Cancellation start");long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,effect)&&AmazonSlowMissiles.State(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled public cast paid/applied");
            Ready(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Late mana start");effect=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,1279);Require(!ClassCombat.ResolveAction(p,effect)&&AmazonSlowMissiles.State(t)==null,"Underfunded release applied");
            state=Cast(p,t);ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(AmazonSlowMissiles.State(t)==state&&!ClassCombat.Available(p,Ability),"Respec recalled state or kept learned admission");ClassSkillChecks.Train(p,"amazon.slowmissiles",20);
            var w=CombatTiming.Equipped(p);p.AddToBackpack(w);Ready(p);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Sorceress)&&AmazonSlowMissiles.State(t)==state,"Class switch recalled released state");Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Amazon)&&p.EquipItem(w),"Restore original Amazon/dagger");
            Cast(p,t);state=AmazonSlowMissiles.State(t);p.Kill();Require(!p.Alive&&AmazonSlowMissiles.State(t)==state,"Creator death recalled recipient debuff");p.Resurrect();if(w.Parent!=p){p.AddToBackpack(w);Require(p.EquipItem(w),"Restore original dagger after death");}
            state=state with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Where(n=>n.Target!=t.Serial.Value).Append(state).ToArray());Ready(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,w.Serial.Value,t.Serial.Value,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),state)));kept=true;
            Log.Information("Slow Missiles learned:right/quick/self/SC,current-rank state,one payment,cancel/late mana,respec/Stop/class/death retention and original saved resource/binding preparation pass");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){AmazonSlowMissiles.Clear(t);t.Delete();p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var t=(GraveyardLich)World.FindMobile((Serial)saved.Target);var account=FixtureNetwork.Authenticate(network,p,"slow-learned-reload-");
        try
        {
            var b=SkillBindings.Instance.Get(p);Require(p.Female&&CombatTiming.Equipped(p)?.Serial.Value==saved.Weapon&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life&&AmazonSlowMissiles.State(t)==saved.State&&ClassCombat.For(p).SlowMissilesCast==null
                &&b.Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,7)==Ability,$"Saved Amazon/gear/state/fractional resources/bindings or replay: gen={saved.Generation},female={p.Female},weapon={CombatTiming.Equipped(p)?.Serial.Value}/{saved.Weapon},mana={NecromancerEffects.ManaBalance256(p)}/{saved.Mana},life={SkillHealth.Balance256(p)}/{saved.Life},state={JsonSerializer.Serialize(AmazonSlowMissiles.State(t))}/{JsonSerializer.Serialize(saved.State)},pending={ClassCombat.For(p).SlowMissilesCast!=null},right={b.Right},quick={SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,7)},targetAlive={t.Alive},ownerMap={p.Map},targetMap={t.Map}");FixtureSpace.Place(p,saved.Origin,Map.Trammel);t.AIObject?.AITimer.Stop();Require(Cast(p,t)is{Rank:20,DamagePercent:67},"Fresh public cast after load");
            AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Where(n=>n.Target!=t.Serial.Value).Append(saved.State).ToArray());Ready(p);NecromancerEffects.SetMana256(p,saved.Mana);SkillHealth.SetBalance256(p,saved.Life);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Slow Missiles learned:original Amazon/dagger/recipient payload,UTC expiry,fractional wounds+mana and right/quick bindings retain through actual reload;fresh public cast works without replay");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
    private static void Input(NetState network,string mode)
    {
        var p=Fresh();p.Name="SlowLearnedWire";if(mode=="quick"){p.AddToBackpack(CombatTiming.Equipped(p));}
        if(mode=="fcr-quick")
        {
            while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Magefist Strength");}
            var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(n=>n.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(gloves);Require(p.EquipItem(gloves),"Original Magefist FCR");ClassGear.Refresh(p);
        }
        Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Wire right/quick bindings");var t=Enemy(p);var account=FixtureNetwork.Authenticate(network,p,"slow-wire-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(p);bool began=false,released=false,done=false;long bolt=0,deadline=Core.TickCount+10000;string label="slow-missiles-learned-"+mode;
        void Cleanup(){WeaponMissiles.Cancel(t);AmazonSlowMissiles.Clear(t);ClassCombat.Stop(p);t.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                Require(Core.TickCount<deadline,"Wire self cast/projectile timeout");var s=ClassCombat.For(p);
                if(!began&&s.SlowMissilesCast!=null)
                {Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana&&WeaponAnimationClasses.Equipped(p)==(mode=="quick"?"HTH":"1HT")&&s.Timing==(mode=="fcr-quick"?new ActionTiming(18,12):mode=="quick"?new ActionTiming(20,11):new ActionTiming(20,13)),"Wire self/FCR/unpaid windup");began=true;Log.Information("{Label}: windup",label);}
                if(!released&&AmazonSlowMissiles.State(t)is{} state)
                {
                    Require(began&&NecromancerEffects.ManaBalance256(p)==mana-1280&&state is{Rank:20,VelocityPercent:33,DamagePercent:67},"Wire one release/native state");released=true;bolt=WeaponMissiles.LaunchHostileFirebolt(t,p.Location,2049,2049);
                    Require(bolt>0&&WeaponMissiles.Snapshot().Flights.Single(n=>n.Id==bolt).Slow==new SlowMissilePayload(33,67),"Natural incoming missile capture");
                }
                if(released&&SkillHealth.Balance256(p)<hp)
                {Require(NecromancerEffects.ManaBalance256(p)==mana-1280&&!WeaponMissiles.Snapshot().Flights.Any(n=>n.Id==bolt),"Natural delivery/payment replay");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Amazon,p.X,p.Y,p.Z);Inspect();}));
    }
}
