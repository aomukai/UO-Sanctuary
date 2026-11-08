// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;
public static class RingCastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RingCastChecks));
    private const CombatAbility Ability=CombatAbility.RingOfFire;
    private sealed record Saved(uint Owner,Point3D Point,int Map,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ring-cast-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved learned Ring SC/payment/self/input",_=>Core.LoopContext.Post(()=>
    {try{Log.Information("{Name}: passed",name);action();}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            bool again=reload;string name=again?"ring-cast-reload-check":"ring-cast-check";
            Register(name,()=>FixtureNetwork.Connected(name+"-result",network=>{try{if(again){Reload(network);}else{Check(network);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}}));
        }
        foreach(string suffix in new[]{"left","right","quick"}){string choice=suffix;Register("ring-cast-"+choice+"-check",()=>FixtureNetwork.Connected("ring-cast-"+choice+"-result",network=>Input(network,choice)));}
    }
    private static RingOfFireFlight[] Fields(PlayerMobile p)=>RingOfFireWorld.Snapshot().Flights.Where(s=>s.Owner==p.Serial.Value).ToArray();
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;}
    private static PlayerMobile Owner()
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);if(p.Backpack==null){p.AddItem(new Server.Items.Backpack());}
        ClassSkillChecks.Train(p,"warlock.ringoffire",1);FixtureSpace.Corridor(p,6920,24,3440);Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static void Check(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"RingCast");int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256;
        Require(cost==2048&&ClassAbilities.SelfTarget(Ability)&&ClassCombat.Available(p,Ability)&&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Ring learned rank1/bindings/self contract");
        var oldSeed=RingOfFireWorld.SeedLow;var oldAllocation=RingOfFireWorld.Allocate;int seeds=0;RingOfFireWorld.SeedLow=()=>{seeds++;return 42;};
        var ignored=new Point3D(-100,-100,0);
        try
        {
            Require(ClassCombat.Perform(p,Ability,null,ignored,Core.TickCount),"Self-centered Ring SC start");var s=ClassCombat.For(p);long effect=s.EffectAt;
            Require(s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana&&Fields(p).Length==0,"Ring windup paid/allocated/used cursor or wrong SC clock");
            ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&Fields(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana&&seeds==0,"Canceled Ring windup paid or allocated");
            Ready(p);NecromancerEffects.SetMana256(p,cost-1);Require(!ClassCombat.Perform(p,Ability,null,ignored,Core.TickCount)&&Fields(p).Length==0,"Underfunded Ring started");
            Ready(p);NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.Perform(p,Ability,null,ignored,Core.TickCount),"Ring late resource start");effect=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
            Require(!ClassCombat.ResolveAction(p,effect)&&Fields(p).Length==0&&seeds==0,"Late mana loss emitted Ring");
            Ready(p);NecromancerEffects.SetMana256(p,mana);RingOfFireWorld.Allocate=_=>false;Require(ClassCombat.Perform(p,Ability,null,ignored,Core.TickCount),"Ring failed allocation start");
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&Fields(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana&&seeds==0,"All-failed release paid/emitted/drew child seeds");RingOfFireWorld.Allocate=oldAllocation;
            Ready(p);Require(ClassCombat.Perform(p,Ability,null,ignored,Core.TickCount),"Ring movement control start");effect=ClassCombat.For(p).EffectAt;var original=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            Require(!ClassCombat.ResolveAction(p,effect)&&Fields(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation paid/emitted Ring");p.MoveToWorld(original,p.Map);
            Ready(p);Require(ClassCombat.Perform(p,Ability,null,ignored,Core.TickCount),"Ring learned release start");effect=ClassCombat.For(p).EffectAt;
            Require(ClassCombat.ResolveAction(p,effect)&&!ClassCombat.ResolveAction(p,effect)&&Fields(p).Length==16&&Fields(p).All(f=>f.Birth.Rank==1&&f.Motion.Frame==0&&f.Motion.Request.X==p.X*2&&f.Motion.Request.Y==p.Y*2)
                &&NecromancerEffects.ManaBalance256(p)==mana-cost&&seeds==16,"Ring once-only release/payment/self origin/child seeds");
            var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Magefist").Id,99,new Random(173),true);p.RawStr=1000;p.AddToBackpack(gloves);Require(p.EquipItem(gloves),"Ring birth gear fixture");ClassGear.Refresh(p);
            Require(SkillMechanics.Rank(p,Ability)==2&&Fields(p).All(f=>f.Birth.Rank==1),"Gear rewrote issued Ring birth rank");
            int paid=NecromancerEffects.ManaBalance256(p);var target=FireBoltChecks.Target(p,3);int life=SkillHealth.Balance256(target);long cast=Fields(p)[0].Cast;
            RingOfFireWorld.Tick(TimedEffects.Now.AddSeconds(1),true,cast);
            Require(Fields(p).Length==0&&SkillHealth.Balance256(target)<life&&NecromancerEffects.ManaBalance256(p)==paid,"Learned Ring did not damage/expire independently or paid twice");target.Delete();
            Ready(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,p.Location,p.Map.MapID)));
        }
        finally{RingOfFireWorld.SeedLow=oldSeed;RingOfFireWorld.Allocate=oldAllocation;}
        Log.Information("Learned Ring:self-centered shared SC,free windup/cancel/movement/allocation/late-mana failure,once8mana release,16original child seeds and frozen birth after +skills;actual native HP/expiry and three bindings. Approved contact rules;current hit1 remains unverified.");
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);Require(p!=null&&saved.Generation<2,"Saved Ring caster missing");
        Require(Fields(p).Length==0&&ClassCombat.For(p).PendingAbility==CombatAbility.None&&SkillBindings.Instance.Get(p).Left==Ability&&SkillBindings.Instance.Get(p).Right==Ability
            &&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability&&SkillMechanics.Rank(p,Ability)==2,"Saved Ring bindings/rank or pending intent replay");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);FixtureNetwork.Authenticate(network,p,"RingReload");File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void Input(NetState network,string choice)
    {
        var p=Owner();p.Female=choice=="quick";
        if(p.Female){p.RawStr=1000;var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(gloves);Require(p.EquipItem(gloves),"Witch FCR gear");ClassGear.Refresh(p);}
        Require(SkillBindings.Instance.Assign(p,choice=="left",Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Ring wire bindings");var account=FixtureNetwork.Authenticate(network,p,"RingWire");Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(ClassProfessions.Ready(p)&&p.Target==null,"Ring quick-use actor needs ordinary backpack/ready context");
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,rank=SkillMechanics.Rank(p,Ability);var target=FireBoltChecks.Target(p,3);int life=SkillHealth.Balance256(target);
        string label="ring-cast-"+choice;long deadline=Core.TickCount+6000;bool began=false,emitted=false,finished=false;int after=0;
        void Cleanup(){ClassCombat.Stop(p);RingOfFireWorld.Clear(p);target.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(finished){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Ring wire timeout");}var s=ClassCombat.For(p);
                if(s.PendingAbility==Ability&&!began){Require(s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana&&Fields(p).Length==0,"Ring socket windup timing/self/payment");began=true;Log.Information("{Label}: windup",label);}
                var fields=Fields(p);if(!emitted&&fields.Length>0){Require(began&&fields.Length==16&&fields.All(f=>f.Birth.Rank==rank)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Ring socket release count/rank/payment");emitted=true;after=NecromancerEffects.ManaBalance256(p);}
                if(!emitted||fields.Length!=0||SkillHealth.Balance256(target)>=life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));return;}
                Require(NecromancerEffects.ManaBalance256(p)>=after,"Ring flight billed again");finished=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,target.Serial.Value,target.X,target.Y,target.Z);Inspect();}));
    }
}
