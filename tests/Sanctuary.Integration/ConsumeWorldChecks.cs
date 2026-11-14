// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class ConsumeWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ConsumeWorldChecks));
    private sealed class Rolls(int value):Random
    {public override int Next(int max)=>max<=0?0:Math.Clamp(value,0,max-1);public override double NextDouble()=>.999999;}
    private sealed record Pet(uint Serial,string Birth,int Life,NpcEquipmentSeed Random);
    private sealed record Saved(int Generation,uint Owner,int Life,int Maximum,ConsumeEffect Effect,Pet[] Pets);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"consume-world-fixtures.json");
    private static bool Creating;
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="ConsumeWorldFixture"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()});
    private static Pet Save(SkeletonGuardian b)=>new(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),NpcEquipmentRandom.State(b));
    private static void Ratio(PlayerMobile p,int before,int maximum,string why)
    {long expected=(long)before*SkillHealth.Maximum256(p)/maximum;Require(Math.Abs(SkillHealth.Balance256(p)-expected)<=1,why);}
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static SkeletonGuardian Summon(PlayerMobile p,CombatAbility ability,int x)
    {
        var point=Enumerable.Range(2,17).SelectMany(dx=>Enumerable.Range(-2,5).Select(dy=>new Point3D(p.X+dx,p.Y+dy,p.Z)))
            .OrderBy(q=>Math.Abs(q.X-p.X-x)+Math.Abs(q.Y-p.Y)).ThenBy(q=>q.X).ThenBy(q=>q.Y)
            .First(q=>p.Map.CanSpawnMobile(q)&&p.InLOS(q));
        var b=WarlockGoatmanWorld.Create(p,point,TimedEffects.Now,ability);WarlockGoatmanWorld.Admit(b);WarlockGoatmanWorld.Enforce(p);return b;
    }
    private static ConsumeRelease Eat(PlayerMobile p,SkeletonGuardian body)
    {
        Ready(p);int life=SkillHealth.Balance256(p),maximum=SkillHealth.Maximum256(p),mana=NecromancerEffects.ManaBalance256(p);long order=BattleCommand.PetOrder(body);
        var result=ConsumeCasting.ReleasePrepared(p,body,TimedEffects.Now);
        Require(result is{Consumed:true,Fault:null}&&body.Deleted&&result.Effect.Order==order&&result.Effect.OriginalPet==body.Serial.Value
            &&NecromancerEffects.ManaBalance256(p)==mana-result.ManaPaid256,"Consume retirement/payment/slot identity");
        Ratio(p,life,maximum,"Consume changed injured life percentage");return result;
    }
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"consume-world-reload-check":"consume-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared Consume actual stats,slot,town picker and saved owner buff",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p)
    {
        var t=new GoatmanNpcContactChecks.Recipient{CantWalk=true,Frozen=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Fire,0);return t;
    }
    private static PlayerWeaponContactPrepared Packet(PlayerMobile p,GoatmanNpcContactChecks.Recipient t,int roll)
        =>PlayerWeaponContact.PrepareOrdinary(p,t,0,0,()=>true,()=>0,()=>0,n=>Math.Max(0,n-1),_=>0,new Rolls(roll));
    private static void Weapon(PlayerMobile p,GoatmanNpcContactChecks.Recipient t)
    {
        void Check(int roll,bool crush)
        {
            SkillHealth.SetBalance256(t,SkillHealth.Maximum256(t));var packet=Packet(p,t,roll);int before=SkillHealth.Balance256(t),expected=NamedElements.To256(packet.Impact.Parts.Total);
            if(crush){expected+=NamedCombat.CrushingLoss256(t,false);}
            var result=PlayerWeaponContact.Release(p,t,packet,TimedEffects.Now);
            Require(result.Commit!=null&&before-SkillHealth.Balance256(t)==expected,$"Actual weapon CB aggregate/once-only mismatch,roll{roll}");
        }
        Require(ClassGear.Bonus(p,"crush")==0&&ConsumeEffects.Crushing(p)==9,"Intrinsic Consume CB control");Check(0,true);Check(9,false);
        while(p.RawStr<60){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Goblin Toe strength");}
        var boots=PoisonDaggerChecks.Named(p,"Goblin Toe");ClassGear.Refresh(p);Require(ClassGear.Bonus(p,"crush")==25,"Real boots CB");
        Check(30,true);Check(34,false);p.AddToBackpack(boots);ClassGear.Refresh(p);
        Log.Information("Prepared Consume:actual ordered weapon CB9with no item;real Goblin Toe25+skill9makes one34percent roll,not independent or duplicated procs");
    }
    private static void Scene(NetState network)
    {
        PlayerMobile p;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.Name="ConsumeWorldFixture";p.Female=true;p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.consume",19);ClassSkillChecks.Train(p,"warlock.deathmark",4);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        foreach(var key in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,key,6);}
        FixtureSpace.Corridor(p,6900,40,3300);var account=FixtureNetwork.Authenticate(network,p,"consume-world-");var facets=SummonDifficulties.Facets();GuardedRegion town=null;bool kept=false;GoatmanNpcContactChecks.Recipient target=null;
        try
        {
            Require(!ConsumeEffects.Approved&&!WarlockCombat.Implemented(CombatAbility.Consume),"Unapproved Consume enabled");
            PoisonDaggerChecks.Named(p,"Tarnhelm");PoisonDaggerChecks.Dagger(p);ClassGear.Refresh(p);
            var a=Summon(p,CombatAbility.SummonGoatman,3);var b=Summon(p,CombatAbility.SummonTainted,6);var c=Summon(p,CombatAbility.SummonDefiler,9);
            NecromancerEffects.SetMana256(p,1);Require(!ConsumeCasting.ReleasePrepared(p,a,TimedEffects.Now).Consumed&&!a.Deleted&&ConsumeEffects.State(p)==null,"Low mana deleted a demon/granted state");Ready(p);
            var cancel=ConsumeCasting.BeginPrepared(p,a.Serial,Core.TickCount);Require(cancel!=null,"SC entry");ClassCombat.Stop(p);Require(cancel.Cancelled&&!a.Deleted,"Stop committed Consume");
            Ready(p);var move=ConsumeCasting.BeginPrepared(p,a.Serial,Core.TickCount);Require(move!=null,"Movement entry");var point=p.Location;p.Location=new(p.X,p.Y+1,p.Z);p.Location=point;Require(move.Cancelled&&!a.Deleted,"Out-and-back Consume cancellation");
            target=Target(p);double physical=Packet(p,target,99).Impact.Parts.Physical;int walking=ClassCombatHooks.WalkingDelay(p,1000);
            BattleOrders.Apply(p,p,1,TimedEffects.Now);int priorPercent=ClassResources.Parts(p,0).SkillPercent;SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2-13);
            var goat=Eat(p,a);Require(goat.Effect.Suppliers is{Rank:20,HardDeathMark:4,LifePercent:24,VelocityPercent:40}
                &&ClassResources.Parts(p,0).SkillPercent==priorPercent+24&&ClassCombatHooks.WalkingDelay(p,1000)<walking
                &&Packet(p,target,99).Impact.Parts.Physical>physical,"Actual additive life/velocity/weapon ED consumers");BattleOrders.Clear(p);
            Weapon(p,target);
            var start=ConsumeEffects.State(p);SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new FacetDifficulty(p.Map.MapID,MonsterDifficulty.Hell)).ToArray());p.Location=new(p.X,p.Y+1,p.Z);
            Require(ConsumeEffects.State(p)is{Suppliers.Difficulty:MonsterDifficulty.Hell,Suppliers.Bonus.DamagePercent:120} hell
                &&hell.Order==start.Order&&hell.UntilUtc==start.UntilUtc&&hell.Suppliers.Rank==start.Suppliers.Rank,"Facet rescale rewrote rank/deadline or ignored difficulty");
            SummonDifficulties.SetFacets(facets);p.Location=point;Require(ConsumeEffects.DamagePercent(p)==40,"Facet restoration");
            int life=SkillHealth.Balance256(p),maximum=SkillHealth.Maximum256(p);var d=Summon(p,CombatAbility.SummonGoatman,3);
            Require(ConsumeEffects.State(p)==null&&!b.Deleted&&!c.Deleted&&!d.Deleted,"Oldest logical slot did not lose buff before younger bodies");Ratio(p,life,maximum,"Pool eviction healed/reset owner");
            Eat(p,b);target.SetResistance(ResistanceType.Fire,50);int before=SkillHealth.Balance256(target);SkillCombat.HitParts(p,target,new(0,100,0,0,0),true,spell:true);
            Require(before-SkillHealth.Balance256(target)==78*256,"Actual fire mastery20/pierce15 expected78against50resist");target.SetResistance(ResistanceType.Fire,100);before=SkillHealth.Balance256(target);SkillCombat.HitParts(p,target,new(0,100,0,0,0),true,spell:true);Require(SkillHealth.Balance256(target)==before,"Consume item-like pierce broke immunity");
            Eat(p,c);Require(NamedElements.MagicResistance(target)==0,"Magic baseline");before=SkillHealth.Balance256(target);SkillCombat.HitParts(p,target,new(0,0,0,0,0,Magic:100),true,spell:true);
            Require(before-SkillHealth.Balance256(target)==132*256&&ConsumeEffects.ElementBonus(p,"extra-fire")==0,"Actual magic mastery20/pierce10 or stale fire bonus");
            var next=Summon(p,CombatAbility.SummonDefiler,9);var helm=p.FindItemOnLayer(Layer.Helm);p.AddToBackpack(helm);ClassGear.Refresh(p);Eat(p,next);
            Require(ConsumeEffects.State(p).Suppliers.Rank==19&&ConsumeEffects.LifePercent(p)==23,"Lower recast did not replace snapshot");p.EquipItem(helm);ClassGear.Refresh(p);Require(ConsumeEffects.State(p).Suppliers.Rank==19,"Gear change rewrote active buff");
            var e=Summon(p,CombatAbility.SummonTainted,6);var f=Summon(p,CombatAbility.SummonGoatman,3);
            Require(d.Deleted&&ConsumeEffects.State(p)!=null&&!e.Deleted&&!f.Deleted,"Oldest live-body pool eviction wrong");
            var intact=ConsumeEffects.Snapshot();bool invalid=false;try{ConsumeEffects.Restore([intact[0]with{Order=0}]);}catch(InvalidDataException){invalid=true;}
            Require(invalid&&ConsumeEffects.State(p)==intact[0],"Malformed restored buff partially replaced state");
            life=SkillHealth.Balance256(p);maximum=SkillHealth.Maximum256(p);ConsumeEffects.Prune(intact[0].UntilUtc);Require(ConsumeEffects.State(p)==null&&ConsumeEffects.ElementBonus(p,"extra-mag")==0,"Expiry retained buff");Ratio(p,life,maximum,"Expiry lost old maximum before percentage transition");
            var final=Summon(p,CombatAbility.SummonDefiler,9);town=new GuardedRegion("ConsumeTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(p.X+18,p.Y-2,6,5));town.Register();p.Location=new(p.X+20,p.Y,p.Z);
            Require(MinionSafeAreas.Contains(p)&&WarlockGoatmanWorld.Demons(p).All(MinionSafeAreas.Parked),"Actual town did not park roster");Ready(p);Require(ConsumeCasting.OpenPrepared(p),"Town roster picker did not open");
            var menu=p.FindGump<ConsumeRosterGump>();var roster=ConsumeCasting.Roster(p);int button=Array.IndexOf(roster,final)+1;Require(menu!=null&&button>0,"Parked target absent from actual sent gump");
            menu.OnResponse(network,new RelayInfo(button,[],[],[],[]));var action=ClassCombat.For(p).Consume;Require(action!=null,"Town gump response did not enter cast");
            int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt-1)&&!final.Deleted&&NecromancerEffects.ManaBalance256(p)==mana,"Town windup paid/consumed early");
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&action.Result is{Consumed:true,ManaPaid256:11264,Fault:null}&&final.Deleted,"Town release failed");
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt+1)&&NecromancerEffects.ManaBalance256(p)==mana-11264,"Town cast replayed");menu.OnResponse(network,new RelayInfo(button,[],[],[],[]));Require(ClassCombat.For(p).Consume==null,"Stale menu revived consumed body");
            var state=ConsumeEffects.State(p);Require(state!=null&&state.Suppliers.Monster=="warputriddefiler"&&ConsumeCasting.Roster(p).Length==2,"Consumed town body remained targetable/roster count");
            town.Unregister();town=null;SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-13);foreach(var pet in WarlockGoatmanWorld.Demons(p)){SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)-513);}PersistentMinions.Suspend(p);BattleCommand.AfterLoad();
            Require(BattleCommand.Snapshot().Pets.All(pet=>pet.Order<state.Order),"Fixture failed to exercise highest retired slot-order reservation");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,SkillHealth.Balance256(p),SkillHealth.Maximum256(p),state,WarlockGoatmanWorld.Demons(p).Select(Save).ToArray())));kept=true;
            Log.Information("Prepared Consume:actual owner life/movement/weaponED,one aggregateCB,fire/magic damage/pierce,caps/immune,replace/expiry/FIFO/difficulty and actual parked-town gump SC/payment/no replay pass");
        }
        finally
        {
            SummonDifficulties.SetFacets(facets);town?.Unregister();target?.Delete();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){ConsumeEffects.Clear(p);foreach(var b in WarlockGoatmanWorld.Demons(p)){b.Delete();}p.Delete();}
        }
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many Consume reloads");var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(ConsumeEffects.State(p)==saved.Effect&&SkillHealth.Maximum256(p)==saved.Maximum&&SkillHealth.Balance256(p)==saved.Life,"Saved owner buff/deadline/over-base life lost or recovered");
        foreach(var pet in saved.Pets){var b=(SkeletonGuardian)World.FindMobile((Serial)pet.Serial);Require(b.Map==Map.Internal&&Birth(b)==pet.Birth&&SkillHealth.Balance256(b)==pet.Life
            &&NpcEquipmentRandom.State(b)==pet.Random&&DemonNativeState.Controller(b)is{Fault:null},"Original parked roster changed before new action");}
        var retired=World.FindMobile((Serial)saved.Effect.OriginalPet)as SkeletonGuardian;
        Require(retired==null||retired.ControlMaster!=p||BattleCommand.PetOrder(retired)!=saved.Effect.Order,"Consumed source body reappeared after load");
        var account=FixtureNetwork.Authenticate(network,p,"consume-world-reload-");
        try
        {
            PersistentMinions.Resume(p);WarlockGoatmanWorld.Tick(TimedEffects.Now);var point=p.Location;p.Location=new(6900,point.Y,point.Z);var old=WarlockGoatmanWorld.Demons(p).OrderBy(BattleCommand.PetOrder).First();
            var created=Summon(p,CombatAbility.SummonGoatman,14+saved.Generation*3);Require(BattleCommand.PetOrder(created)>saved.Effect.Order&&old.Deleted&&ConsumeEffects.State(p)==saved.Effect,"Retired highest slot order reused or FIFO state lost after load");
            PersistentMinions.Suspend(p);BattleCommand.AfterLoad();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,Life=SkillHealth.Balance256(p),Maximum=SkillHealth.Maximum256(p),Effect=ConsumeEffects.State(p),Pets=WarlockGoatmanWorld.Demons(p).Select(Save).ToArray()}));
            Log.Information("Prepared Consume:saved owner optional buff/deadline/over-base fractional life and original parked roster retain through reload;consumed body never returns;reserved logical order survives new summon FIFO");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
