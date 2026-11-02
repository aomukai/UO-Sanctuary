// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class HydraWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HydraWorldChecks));
    private sealed record Saved(uint Owner,uint Target,uint Gear,int Mana,int Life,int TargetLife,int Map,Point3D Point,HydraHead[] Heads,HydraBolt[] Bolts,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"hydra-world-fixtures.json");
    public static void Configure()
    {
        Register("hydra-world-check",Check);Register("hydra-world-reload-check",()=>FixtureClock.NextLoop("hydra-world-reload-result",()=>FixtureNetwork.Connected("hydra-world-reload-result",Reload)));
        foreach(string mode in new[]{"right","quick"}){string label="hydra-world-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="HydraWorldFixture"&&prior?.Invoke(p)!=false;
    }
    [CallPriority(int.MaxValue)]public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="HydraWorldFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Hydra NPC source/captured clock/flight/resources/input and original world reload",_=>Core.LoopContext.Post(()=>{try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,6730);p.Name="HydraWorldFixture";
        foreach(var a in new[]{CombatAbility.Warmth,CombatAbility.Fireball,CombatAbility.Enchant,CombatAbility.Hydra,CombatAbility.FireMastery}){EnchantCastChecks.Train(p,a,1);}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Hydra)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Hydra),"Hydra bindings");return p;
    }
    private static HydraHead[] Heads(PlayerMobile p)=>Hydras.Snapshot().Heads.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static HydraBolt[] Bolts(PlayerMobile p)=>Hydras.Snapshot().Bolts.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static void Clean(PlayerMobile p,params Mobile[] others){Hydras.Cancel(p);FireBoltChecks.Clean(p,others);}
    private static Point3D Aim(PlayerMobile p)=>new(p.X+4,p.Y,p.Z);
    private static void Check(){Hydras.Route();Admission();WorldChecks();Damage();FacetReturn();Save();Log.Information("Hydra world:actual source bodies/captured hard/mastery/ordinary clock/slow source bolts/typed ordered HP and credit/partial FIFO/inactive/facet/retirement/atomic graph/legacy/input/original saved actors");}
    private static void Admission()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,4);try
        {
            int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.ContinueCast(p,CombatAbility.Hydra,t,t.Location,Core.TickCount),"Hydra windup");long action=ClassCombat.For(p).EffectAt;
            Require(Heads(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Hydra paid/created before release");ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,action),"Cancelled Hydra emitted");
            NecromancerEffects.SetMana256(p,5119);Require(!SorceressCombat.Release(p,CombatAbility.Hydra,t.Serial,t.Location)&&Heads(p).Length==0&&NecromancerEffects.ManaBalance256(p)==5119,"Insufficient Hydra emitted/paid");NecromancerEffects.SetMana256(p,mana);
            Require(ClassCombat.ContinueCast(p,CombatAbility.Hydra,t,t.Location,Math.Max(Core.TickCount,ClassCombat.For(p).NextAction)),"Second Hydra windup");action=ClassCombat.For(p).EffectAt;t.MoveToWorld(new(p.X+7,p.Y+1,p.Z),p.Map);EnchantCastChecks.Train(p,CombatAbility.Hydra,2);
            Require(ClassCombat.ResolveAction(p,action)&&Heads(p).Length==3&&Heads(p).All(x=>x.Rank==2&&x.Source!=null)&&Heads(p).Any(x=>x.X==t.X&&x.Y==t.Y)
                &&NecromancerEffects.ManaBalance256(p)==mana-5248,"Live Hydra target/rank/once-only fractional20.5mana");
            Require(FireGround.Ready(p,CombatAbility.Hydra),"Hydra local delay returned");mana=NecromancerEffects.ManaBalance256(p);
            Require(!SorceressCombat.Release(p,CombatAbility.Hydra,Serial.Zero,new(-1,-1,0))&&NecromancerEffects.ManaBalance256(p)==mana,"Invalid aim spent Hydra mana");
            var before=Hydras.Snapshot();bool refused=false;try{Hydras.Restore(before with{Heads=before.Heads.Append(Heads(p)[0]).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&Heads(p).SequenceEqual(before.Heads.Where(x=>x.Owner==p.Serial.Value))&&Heads(p).All(x=>World.FindMobile((Serial)x.Source.Actor) is HydraBody{Deleted:false}),"Malformed Hydra restore was destructive");Hydras.ValidateWorld();
        }
        finally{ClassCombat.Stop(p);Clean(p,t);}
    }
    private static void WorldChecks()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,7);var oldSeed=Blazes.SeedLow;
        try
        {
            Blazes.SeedLow=()=>1;var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Hydras.Cast(p,Aim(p),SkillMechanics.Values(p,CombatAbility.Hydra));var initial=Heads(p);var first=initial.Single(x=>x.Index==1);var body=(HydraBody)World.FindMobile((Serial)first.Source.Actor);
            Require(initial.Length==3&&initial.All(x=>x.Source.Control is{Frame:0,Mode:9,NextThink:25,EndAt:12})&&!body.BlocksFit&&!body.CanBeDamaged()&&body.SummonMaster==p,"Native Hydra emergence/body/ownership");
            var birth=first.Source.Birth;p.Backpack.DropItem(orb);ClassGear.Refresh(p);EnchantCastChecks.Train(p,CombatAbility.FireBolt,20);
            Hydras.Advance(p,first.Source.NextUtc.AddMilliseconds(23*40),true);Require(Heads(p).All(x=>x.Source.Control is{Frame:24,Attempts:0})&&Bolts(p).Length==0,"Hydra fired before first AI25");
            Hydras.Advance(p,first.Source.NextUtc.AddMilliseconds(24*40),true);Require(Heads(p).All(x=>x.Source.Control is{Frame:25,Mode:4,ActionAt:33,EndAt:37})&&Bolts(p).Length==0,"Hydra AI25 did not enter ordinary A1");
            Hydras.Advance(p,first.Source.NextUtc.AddMilliseconds(32*40),true);var bolts=Bolts(p);var emitted=bolts.Single(x=>x.Head==first.Id);var ends=HydraCreation.Fire(birth,SlowMissilePayload.Normal);
            Require(bolts.Length==3&&emitted is{Frame:0}&&emitted.Source.Actor==body.Serial.Value&&emitted.Source.LastCollision==body.Serial.Value&&emitted.Source.VelocityFp8==3072
                &&emitted.Min256==ends.Min&&emitted.Max256==ends.Max&&Heads(p).All(x=>x.Source.Birth==birth),"Hydra captured head stats or NPC-owned frame33 bolt");
            Hydras.Advance(p,first.Source.NextUtc.AddMilliseconds(36*40),true);Require(Heads(p).All(x=>x.Source.Control is{Frame:37,Mode:1,NextThink:52}),"Ordinary Hydra end/neutral handoff");
            int life=SkillHealth.Balance256(t);Hydras.Advance(p,first.Source.NextUtc.AddMilliseconds(60*40),true);Require(SkillHealth.Balance256(t)<life,"Actual Hydra bolts did not damage target");
            Hydras.Cancel(p);Require(initial.All(x=>World.FindMobile((Serial)x.Source.Actor)==null),"Cancelled native Hydra body leaked");
            Hydras.Cast(p,Aim(p),SkillMechanics.Values(p,CombatAbility.Hydra));first=Heads(p)[0];body=(HydraBody)World.FindMobile((Serial)first.Source.Actor);
            var slows=AmazonSlowMissiles.Snapshot();AmazonSlowMissiles.Restore(slows.Append(new(body.Serial.Value,p.Serial.Value,1,50,25,TimedEffects.Now.AddMinutes(1))).ToArray());
            Hydras.Advance(p,first.Source.NextUtc.AddMilliseconds(32*40),true);var slowed=Bolts(p).Single(x=>x.Head==first.Id);ends=HydraCreation.Fire(first.Source.Birth,new(50,25));
            Require(slowed.Source is{VelocityFp8:1536,Slow.VelocityPercent:50,Slow.DamagePercent:25}&&slowed.Min256==ends.Min&&slowed.Max256==ends.Max,"Slow Missiles did not capture the actual head's state");
            AmazonSlowMissiles.Clear(body);Require(Bolts(p).Single(x=>x.Id==slowed.Id)==slowed,"In-flight Hydra reread cleared Slow Missiles");Hydras.Cancel(p);
            Hydras.Cast(p,Aim(p),SkillMechanics.Values(p,CombatAbility.Hydra));first=Heads(p)[0];ulong seed=first.Source.Control.Seed;
            Hydras.Advance(p,first.Source.NextUtc.AddMilliseconds(29*40),false);Require(Heads(p).All(x=>x.Source.Control is{Frame:30,Mode:1,Attempts:0}&&x.Source.Control.Seed==seed)&&Bolts(p).Length==0,"Offline Hydra replayed combat/RNG");
            Hydras.Advance(p,first.UntilUtc.AddMilliseconds(80),false);Require(Heads(p).Length==0&&Bolts(p).Length==0,"Offline Hydra lifetime reset");Hydras.Advance(p,first.UntilUtc.AddSeconds(1),false);Require(Hydras.Snapshot().Retired?.All(x=>x.Owner!=p.Serial.Value)!=false,"Retired Hydra actor never released");
            Hydras.Cast(p,Aim(p),SkillMechanics.Values(p,CombatAbility.Hydra));first=Heads(p)[0];var map=p.Map;var position=p.Location;p.MoveToWorld(position,Map.Maps[map==Map.Felucca?1:0]);
            Hydras.Advance(p,first.Source.NextUtc.AddMilliseconds(32*40),true);Require(Heads(p).All(x=>x.Map==map.MapID)&&Bolts(p).Length>0,"Old-facet Hydra followed owner or lost local targets");p.MoveToWorld(position,map);
            var all=Heads(p);for(int i=0;i<6;i++){Hydras.Cast(p,Aim(p),SkillMechanics.Values(p,CombatAbility.Hydra));}
            Require(Heads(p).Length==18&&Heads(p).All(x=>all.All(y=>y.Id!=x.Id))&&Hydras.Snapshot().Retired?.Any(x=>x.Owner==p.Serial.Value)==true,"Per-head native cap/retirement");
            Hydras.Advance(p,TimedEffects.Now.AddSeconds(4),false);Require(Hydras.Snapshot().Retired?.Any(x=>x.Owner==p.Serial.Value)!=true,"Bounded retired emitters leaked after their last flight");
        }
        finally{Blazes.SeedLow=oldSeed;Clean(p,t);}
    }
    private static void Damage()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,7);try
        {
            Hydras.Cast(p,Aim(p),SkillMechanics.Values(p,CombatAbility.Hydra));var h=Heads(p).First();var body=(HydraBody)World.FindMobile((Serial)h.Source.Actor);t.SetHits(2000);SkillHealth.SetBalance256(t,2000*256);
            foreach(var (resistance,loss) in new[]{(0,25600),(50,12800),(100,0)})
            {
                t.SetResistance(ResistanceType.Fire,resistance);int life=SkillHealth.Balance256(t);PlayerHydras.Deliver(body,t,25600,TimedEffects.Now);
                Require(life-SkillHealth.Balance256(t)==loss&&MonsterUninterruptibles.State(t)?.LastAttacker==body.Serial.Value,"Hydra typed fire mitigation/actual emitter identity");
            }
            t.SetResistance(ResistanceType.Fire,0);int before=SkillHealth.Balance256(t);Require(CombatPotions.Apply(p,3),"Hydra potion fixture");PlayerHydras.Deliver(body,t,25600,TimedEffects.Now);
            Require(before-SkillHealth.Balance256(t)==25600,"Hydra incorrectly recaptured player potion lanes");TimedEffects.Clear(p);
            Require(PreparedMonsterCredit.Owner(t,body)==p&&t.FindDamageEntryFor(body)?.Responsible?.Any(r=>r.Damager==p&&r.DamageGiven>0)==true,"Hydra head damage did not credit its original owner");
            for(int i=0;i<6;i++){Hydras.Cast(p,Aim(p),SkillMechanics.Values(p,CombatAbility.Hydra));}
            before=SkillHealth.Balance256(t);Require(body.Head.Source.Control.Mode==0,"Old Hydra emitter did not retire");PlayerHydras.Deliver(body,t,25600,TimedEffects.Now);
            Require(before-SkillHealth.Balance256(t)==25600&&PreparedMonsterCredit.Owner(t,body)==p,"Registered retired emitter lost its retained flight damage/owner credit");
            SkillHealth.SetBalance256(t,2560);PlayerHydras.Deliver(body,t,25600,TimedEffects.Now);Require(!t.Alive&&t.LastKiller==body,"Hydra ordered death lost NPC source");
        }
        finally{Clean(p,t);}
        foreach(var (pierce,resistance,expected) in new[]{(20,50,17920),(20,100,0),(-20,50,7680)})
        {
            p=Owner();t=FireBoltChecks.Target(p,7);try
            {
                var birth=HydraCreation.Prepare(SkillMechanics.Values(p,CombatAbility.Hydra),ClassStats.Level(p),name=>name=="passive_fire_pierce"?pierce:0);
                Hydras.Commit(p,Aim(p),birth,TimedEffects.Now);var body=(HydraBody)World.FindMobile((Serial)Heads(p)[0].Source.Actor);t.SetResistance(ResistanceType.Fire,resistance);int before=SkillHealth.Balance256(t);
                PlayerHydras.Deliver(body,t,25600,TimedEffects.Now);Require(before-SkillHealth.Balance256(t)==expected,"Captured signed NPC pierce/immunity gate");
            }
            finally{Clean(p,t);}
        }
    }
    private static void Hold(PlayerMobile p)
    {
        var snapshot=Hydras.Snapshot();var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        Hydras.Restore(snapshot with{Heads=snapshot.Heads.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=x.UntilUtc+(at-x.Source.NextUtc),Source=x.Source with{NextUtc=at}}:x).ToArray(),
            Bolts=snapshot.Bolts.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=x.UntilUtc+(at-x.NextUtc),NextUtc=at}:x).ToArray()});
    }
    private static void FacetReturn()
    {
        var p=Owner();var target=FireBoltChecks.Target(p,7);GuardedRegion town=null;
        try
        {
            Hydras.Cast(p,Aim(p),SkillMechanics.Values(p,CombatAbility.Hydra));var body=(HydraBody)World.FindMobile((Serial)Heads(p)[0].Source.Actor);
            p.MoveToWorld(p.Location,Map.Maps[p.Map==Map.Felucca?1:0]);
            town=new GuardedRegion("HydraOtherFacetTown",p.Map,1000,new Rectangle2D(target.X-1,target.Y-1,3,3));town.Register();
            var services=new NativeMonsterReturn(body,p,target,NativeMonsterReturn.Metadata(target),()=>{});
            Require(!services.RoomInTown(),"Hydra return used owner's facet for the target town check");
            SkillHealth.SetBalance256(target,2560);PlayerHydras.Deliver(body,target,25600,TimedEffects.Now);
            Require(!target.Alive&&target.LastKiller==body&&PreparedMonsterCredit.Owner(target,body)==p,"Other-facet town suppressed Hydra lethal return/owner credit");
        }
        finally{town?.Unregister();Clean(p,target);}
    }
    private static Saved Capture(PlayerMobile p,BaseCreature t,Item gear,int generation,int map,Point3D point)=>new(p.Serial.Value,t.Serial.Value,gear.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(t),map,point,Heads(p),Bolts(p),generation);
    private static void Save()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,7);t.SetHits(20000);SkillHealth.SetBalance256(t,20000*256);var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var seed=Blazes.SeedLow;
        try{Blazes.SeedLow=()=>1;Require(SorceressCombat.Release(p,CombatAbility.Hydra,Serial.Zero,Aim(p)),"Saved learned Hydra");var head=Heads(p)[0];Hydras.Advance(p,head.Source.NextUtc.AddMilliseconds(32*40),true);Require(Bolts(p).Length==3,"Saved Hydra did not create source bolts");}
        finally{Blazes.SeedLow=seed;}
        Hold(p);SkillHealth.Damage(p,p,5*256+17);var saved=Capture(p,t,orb,0,p.Map.MapID,p.Location);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var t=(BaseCreature)World.FindMobile((Serial)saved.Target);var gear=World.FindItem((Serial)saved.Gear);
        Require(saved.Generation<2&&gear?.Parent==p&&Heads(p).SequenceEqual(saved.Heads)&&Bolts(p).SequenceEqual(saved.Bolts)&&Heads(p).All(x=>World.FindMobile((Serial)x.Source.Actor) is HydraBody b&&b.SummonMaster==p)
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life&&SkillHealth.Balance256(t)==saved.TargetLife,"Original saved Hydra graph/gear/resources/wounds changed");Hydras.ValidateWorld();
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);t.Frozen=true;t.CantWalk=true;t.AIObject.AITimer.Stop();t.SetResistance(ResistanceType.Fire,0);var account=FixtureNetwork.Authenticate(network,p,"hydra-reload-");var at=TimedEffects.Now.AddMilliseconds(40);var all=Hydras.Snapshot();
        Hydras.Restore(all with{Heads=all.Heads.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=x.UntilUtc+(at-x.Source.NextUtc),Source=x.Source with{NextUtc=at}}:x).ToArray(),
            Bolts=all.Bolts.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=x.UntilUtc+(at-x.NextUtc),NextUtc=at}:x).ToArray()});long deadline=Core.TickCount+6000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Original Hydra natural resume timeout");if(SkillHealth.Balance256(t)>=saved.TargetLife){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(saved.Heads.All(old=>Heads(p).Any(x=>x.Id==old.Id&&x.Source.Actor==old.Source.Actor&&x.Source.Control.Frame>old.Source.Control.Frame&&x.Source.Birth==old.Source.Birth))
                    &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life,"Original Hydra reset head identity/payload/frame/resources");
                Hold(p);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(Capture(p,t,gear,saved.Generation+1,saved.Map,saved.Point)));Log.Information("hydra-world-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"hydra-world-reload-result: failed");}
        }
        Observe();
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,4);t.SetHits(20000);SkillHealth.SetBalance256(t,20000*256);var account=FixtureNetwork.Authenticate(network,p,"hydra-input-");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+8000;
        void Finish(){ClassCombat.Stop(p);Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Hydra actual input/natural damage timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.Hydra&&!began){Require(Heads(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Input Hydra paid during windup");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)>=life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-5120&&Heads(p).Length==3&&Heads(p).Any(x=>x.Source.Control.Dispatches>0),"Actual Hydra input payment/source head dispatch");Finish();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Finish();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Hydra,(int)DiabloClass.Sorceress,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
