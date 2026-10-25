// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BattleOrdersChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BattleOrdersChecks));
    private sealed record SavedPool(uint Actor,int LifeMax,int ManaMax,int StamMax,int Life256,int Mana256,int Stam256,BattleOrdersState State,int? LifeSeed,int? ManaSeed);
    private sealed record Saved(uint Owner,uint Ally,uint Golem,uint Pet,uint Necro,uint Far,uint Expired,Point3D Origin,int Map,SavedPool[] Pools,ShoutRay Ray,WarcryDelay Delay);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"battle-orders.json");
    private static bool _creating;
    public static void Configure(){Rates();Register("battle-orders-check",Check);Register("battle-orders-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static bool Fixture(Mobile actor)=>_creating || actor.Name?.StartsWith("OrdersFixture",StringComparison.Ordinal)==true;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;var sp=Mobile.StamRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):hp?.Invoke(actor)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):mp?.Invoke(actor)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):sp?.Invoke(actor)??Mobile.DefaultStamRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Battle Orders resources, wave, pets and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"battle-orders-result: failed");}});
    private static PlayerMobile Fresh(PlayerMobile near=null,DiabloClass cls=DiabloClass.Barbarian)
    {
        PlayerMobile actor;_creating=true;try{actor=new PlayerMobile{Body=0x190,Name="OrdersFixture",Player=true};}finally{_creating=false;}
        actor.AddItem(new Backpack());ClassCreation.Profiles.Assign(actor,cls);ClassCreation.Profiles.AdvanceLevel(actor,99);
        FixtureSpace.Place(actor,near==null?new(6050,1680,0):new(near.X,near.Y+5,near.Z),near?.Map??Map.Felucca);
        if(near==null){actor.MoveToWorld(ClassSkillChecks.Corridor(actor,6050,1680),actor.Map);}
        Require(Mobile.ManaRegenRateHandler(actor)==TimeSpan.FromHours(1) && Mobile.StamRegenRateHandler(actor)==TimeSpan.FromHours(1),"Orders fixture recovery wrapper order");return actor;
    }
    private static void Train(PlayerMobile actor,CombatAbility ability,int rank)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Missing talent");
        foreach(string id in talent.Prerequisites){Train(actor,SkillMechanics.All.Single(x=>x.TalentId==id).Ability,1);}
        while(ClassCreation.Profiles.GetRank(actor.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(actor.Serial,row.TalentId,out var why),"Train: "+why);}
    }
    private static void Stop(BaseCreature actor){actor.Frozen=actor.CantWalk=true;actor.Combatant=null;actor.AIObject?.AITimer.Stop();}
    private static void Ready(PlayerMobile actor){ClassCombat.Stop(actor);ClassCombat.For(actor).NextAction=ClassCombat.For(actor).RecoveryUntil=Core.TickCount;}
    private static void CancelRays(PlayerMobile actor)
    {var save=BarbarianShout.Snapshot();BarbarianShout.Restore(save with{Rays=save.Rays.Where(x=>x.Owner!=actor.Serial.Value).ToArray()});}
    private static void Replace(BattleOrdersState state)
    {var save=BattleOrders.Snapshot();BattleOrders.Restore(save with{States=save.States.Where(x=>x.Recipient!=state.Recipient).Append(state).ToArray()});}
    private static SavedPool Pool(Mobile actor)=>new(actor.Serial.Value,actor.HitsMax,actor.ManaMax,actor.StamMax,SkillHealth.Balance256(actor),NecromancerEffects.ManaBalance256(actor),BattleOrders.Stamina256(actor),BattleOrders.State(actor),actor is BaseCreature c?c.HitsMaxSeed:null,actor is BaseCreature m?m.ManaMaxSeed:null);
    private static void Scaled(Mobile actor,SavedPool before,int manaDebit=0)
    {
        Require(SkillHealth.Balance256(actor)==BattleOrders.Rescale256(before.Life256,before.LifeMax*256,actor.HitsMax*256),"Life proportional transition");
        Require(NecromancerEffects.ManaBalance256(actor)==BattleOrders.Rescale256(before.Mana256,before.ManaMax*256,actor.ManaMax*256)-manaDebit,"Mana proportional transition/payment ordering");
        Require(BattleOrders.Stamina256(actor)==BattleOrders.Rescale256(before.Stam256,before.StamMax*256,actor.StamMax*256),"Stamina proportional transition");
    }
    private static void Full(Mobile actor)
    {SkillHealth.SetBalance256(actor,actor.HitsMax*256);NecromancerEffects.SetMana256(actor,actor.ManaMax*256);BattleOrders.SetStamina256(actor,actor.StamMax*256);}
    private static void Cast(PlayerMobile owner)
    {
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BattleOrders,null,owner.Location,Core.TickCount),"Orders begin");var action=ClassCombat.For(owner);
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt-1) && ClassCombat.ResolveAction(owner,action.EffectAt) && !ClassCombat.ResolveAction(owner,action.EffectAt),"Orders single timed release");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.BattleOrders);
        foreach(var (rank,hard,percent,frames) in new[]{(1,0,35,750),(20,40,92,10500),(21,40,95,10750)})
        {
            var values=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>hard/2,99);
            Require(values.Mana256==1792 && values.Eval("aurastatcalc1")==percent && values.Eval("aurastatcalc2")==percent && values.Eval("aurastatcalc3")==percent && values.Eval("auralencalc")==frames,"Pinned BO formula goldens");
        }
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT"),".local/class-skills/battle-orders-source-probe.json")));
        foreach(var rowCase in source.RootElement.GetProperty("poolCases").EnumerateArray())
        {int[] x=rowCase.EnumerateArray().Select(v=>v.GetInt32()).ToArray();Require(BattleOrders.Rescale256(x[4],x[2],x[3])==x[5],"408 independent compiled resource callbacks");}
        var legacy=JsonSerializer.SerializeToNode(new ShoutSave([], [new(1,1,0,1,0,1,1,0,0,5760,0,DateTime.UtcNow.AddSeconds(1),DateTime.UtcNow)]));
        legacy.AsObject().Remove("Delays");legacy["Rays"][0].AsObject().Remove("Ability");legacy["Rays"][0].AsObject().Remove("LastCollision");
        var decoded=legacy.Deserialize<ShoutSave>();Require(decoded.Rays[0].Ability==CombatAbility.Shout && decoded.Rays[0].LastCollision==0 && decoded.Delays==null,"Existing Shout JSON defaults changed");
        Require(ClassAbilities.SelfTarget(CombatAbility.BattleOrders) && NecromancerSummons.Source("battleorders","NextHit")==1 && NecromancerSummons.Source("battleorders","NextDelay")==4 && NecromancerSummons.Source("battleorders","LastCollide")==1,"BO missile/self metadata");
    }
    private static void Synchronous(PlayerMobile owner)
    {
        Train(owner,CombatAbility.BattleOrders,1);
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.BattleOrders) && SkillBindings.Instance.Assign(owner,false,CombatAbility.BattleOrders) && SkillBindings.Instance.AssignQuick(owner,3,CombatAbility.BattleOrders),"Orders right/quick bindings");
        SkillHealth.SetBalance256(owner,100*256+17);NecromancerEffects.SetMana256(owner,79*256+64);BattleOrders.SetStamina256(owner,80*256+73);var before=Pool(owner);
        Cast(owner);Require(owner.HitsMax==338 && owner.ManaMax==145 && owner.StamMax==256 && BattleOrders.State(owner).Percent==35,"Native BO maxima");Scaled(owner,before,1792);
        before=Pool(owner);Cast(owner);Require(SkillHealth.Balance256(owner)==before.Life256 && BattleOrders.Stamina256(owner)==before.Stam256 && NecromancerEffects.ManaBalance256(owner)==before.Mana256-1792,"Equal recast healed or paid twice");CancelRays(owner);
        before=Pool(owner);BattleOrders.Apply(owner,owner,20,TimedEffects.Now);Require(BattleOrders.State(owner).Percent==92,"Stronger replacement");Scaled(owner,before);
        before=Pool(owner);var high=BattleOrders.State(owner);BattleOrders.Apply(owner,owner,1,TimedEffects.Now);
        Require(BattleOrders.State(owner).Percent==35 && BattleOrders.State(owner).UntilUtc<high.UntilUtc,"Weaker/shorter replacement");Scaled(owner,before);
        var other=Fresh(owner);Train(other,CombatAbility.BattleOrders,1);BattleOrders.Apply(other,owner,1,TimedEffects.Now);Require(BattleOrders.State(owner).Creator==owner.Serial.Value && BattleOrders.State(owner).Supplier==other.Serial.Value,"Creator/supplier recast metadata");other.Delete();
        before=Pool(owner);BattleOrders.Clear(owner);Scaled(owner,before);Require(owner.HitsMax==251 && owner.ManaMax==108 && owner.StamMax==190,"BO removal maxima");
        NecromancerEffects.SetMana256(owner,6*256);Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.BattleOrders,null,owner.Location,Core.TickCount) && BattleOrders.State(owner)==null,"Unpaid begin granted buff");
        Full(owner);Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BattleOrders,null,owner.Location,Core.TickCount),"Interrupted start");var action=ClassCombat.For(owner);int mana=NecromancerEffects.ManaBalance256(owner);owner.Paralyzed=true;
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana && BattleOrders.State(owner)==null,"Interrupted Orders paid/applied");owner.Paralyzed=false;
        Full(owner);Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BattleOrders,null,owner.Location,Core.TickCount),"Mana loss start");action=ClassCombat.For(owner);NecromancerEffects.SetMana256(owner,6*256);
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && BattleOrders.State(owner)==null,"Mana loss during wind-up granted buff");
        var zero=Fresh(owner,DiabloClass.Sorceress);NecromancerEffects.SetMana256(zero,0);BattleOrders.SetStamina256(zero,0);BattleOrders.Apply(owner,zero,1,TimedEffects.Now);
        Require(zero.Mana==0 && zero.Stam==0 && SkillHealth.Balance256(zero)>0,"Percentage buff refilled zero pools");BattleOrders.Clear(zero);zero.Delete();
        Full(owner);Ready(owner);
    }
    private static void Gear(PlayerMobile owner)
    {
        var actor=Fresh(owner);Train(actor,CombatAbility.BattleOrders,1);for(int i=0;i<40;i++){Require(ClassCreation.Profiles.TrySpendStat(actor,CharacterStat.Strength),"Gear strength");}
        foreach(string key in new[]{"Lidless Wall","Frostburn","Harlequin Crest"})
        {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);Require(actor.EquipItem(item),"Orders real gear "+key);ClassGear.Refresh(actor);}
        Full(actor);Cast(actor);
        Require(BattleOrders.State(actor).Rank==4 && BattleOrders.State(actor).Percent==44 && actor.HitsMax==517 && actor.ManaMax==370 && actor.StamMax==275 && NecromancerEffects.ManaBalance256(actor)==363*256,$"Geared rank{BattleOrders.State(actor)?.Rank}, percent{BattleOrders.State(actor)?.Percent}, maxima{actor.HitsMax}/{actor.ManaMax}/{actor.StamMax}, mana{NecromancerEffects.ManaBalance256(actor)}");
        Require((BattleOrders.State(actor).UntilUtc-TimedEffects.Now).TotalSeconds is >64.8 and <65.1,"Soft Shout rank entered hard duration synergy");
        CancelRays(actor);BattleOrders.Clear(actor);actor.Delete();
    }
    private static void Vigor(PlayerMobile owner)
    {
        var saved=PaladinAuras.Snapshot();var paladin=Fresh(owner,DiabloClass.Paladin);Train(paladin,CombatAbility.Vigor,1);
        Ogre pet;_creating=true;try{pet=new Ogre{NoKillAwards=true,Name="OrdersFixtureVigorPet"};}finally{_creating=false;}
        pet.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Require(pet.SetControlMaster(owner),"Vigor pet master");pet.SetStam(80);Stop(pet);
        Require(SkillBindings.Instance.Assign(paladin,false,CombatAbility.Vigor) && PaladinVigor.StaminaPercent(owner)==50 && PaladinVigor.StaminaPercent(pet)==50,"Actual Vigor leases");
        Require(owner.StamMax==285 && pet.StamMax==120,"Vigor baseline maxima");var before=Pool(owner);var petBefore=Pool(pet);
        BattleOrders.Apply(owner,owner,1,TimedEffects.Now);BattleOrders.Apply(owner,pet,1,TimedEffects.Now);
        Require(owner.StamMax==351 && pet.StamMax==148,"BO multiplied Vigor instead of joining its percentage pool");Scaled(owner,before);Scaled(pet,petBefore);
        BattleOrders.Clear(owner);BattleOrders.Clear(pet);PaladinAuras.Restore(saved);PaladinVigor.Refresh(owner);paladin.Delete();pet.Delete();
    }
    private static void FractionalSummon(PlayerMobile owner)
    {
        var necro=Fresh(owner,DiabloClass.Necromancer);SkeletonGuardian pet;_creating=true;try{pet=new SkeletonGuardian();}finally{_creating=false;}
        pet.Initialize(necro,4,0);pet.Name="OrdersFixtureFractionalSkeleton";Require(pet.SetControlMaster(necro),"Fractional skeleton master");pet.MoveToWorld(new(owner.X+3,owner.Y+2,owner.Z),owner.Map);Stop(pet);
        Require(pet.HitsMax==31 && pet.SourceLife256==8064,"Source31.5-life skeleton base");
        BattleOrders.Apply(owner,pet,1,TimedEffects.Now);Require(pet.HitsMax==42 && pet.HitsMaxSeed==31,"BO rounded summon creation before applying its percentage");
        BattleOrders.Clear(pet);Require(pet.HitsMax==31,"Fractional summon removal changed species seed");pet.Delete();necro.Delete();
    }
    private static void OrderingEdge(PlayerMobile reference)
    {
        var owner=Fresh(reference);Train(owner,CombatAbility.BattleOrders,1);Full(owner);BattleOrders.Apply(owner,owner,20,TimedEffects.Now);
        NecromancerEffects.SetMana256(owner,7*256);int previous=owner.ManaMax;Cast(owner);
        Require(BattleOrders.State(owner).Percent==35 && NecromancerEffects.ManaBalance256(owner)==BattleOrders.Rescale256(7*256,previous*256,owner.ManaMax*256) && owner.Mana<7,"Supplementary consume-after-do insufficient replacement edge");
        CancelRays(owner);BattleOrders.Clear(owner);owner.Delete();
    }
    private static void Lifecycle(PlayerMobile owner)
    {
        foreach(int mode in new[]{0,1,2})
        {
            var actor=Fresh(owner);Train(actor,CombatAbility.BattleOrders,1);Full(actor);BattleOrders.Apply(actor,actor,1,TimedEffects.Now);
            if(mode==0){actor.Kill();actor.Resurrect();}
            else if(mode==1){Require(ClassCreation.Profiles.TryActivateHistory(actor,DiabloClass.Paladin),"BO history switch");}
            else{ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);}
            Require(BattleOrders.State(actor)==null && actor.Hits<=actor.HitsMax && actor.Mana<=actor.ManaMax && actor.Stam<=actor.StamMax,"BO lifecycle leaked state or excess pools");actor.Corpse?.Delete();actor.Delete();
        }
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();Synchronous(owner);Gear(owner);Lifecycle(owner);Vigor(owner);FractionalSummon(owner);OrderingEdge(owner);
        var ally=Fresh(owner,DiabloClass.Sorceress);ally.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);SkillHealth.SetBalance256(ally,51*256+17);NecromancerEffects.SetMana256(ally,64*256+128);BattleOrders.SetStamina256(ally,40*256+64);var allyBefore=Pool(ally);
        var necro=Fresh(owner,DiabloClass.Necromancer);necro.MoveToWorld(new(owner.X+20,owner.Y,owner.Z),owner.Map);Train(necro,CombatAbility.ClayGolem,1);Train(necro,CombatAbility.GolemMastery,1);
        NecromancerMinion golem;_creating=true;try{golem=new NecromancerMinion();}finally{_creating=false;}golem.Prepare(necro,CombatAbility.ClayGolem);golem.Name="OrdersFixtureGolem";Require(golem.SetControlMaster(necro),"Golem control");golem.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);Stop(golem);int golemBase=golem.HitsMax;var golemBefore=Pool(golem);
        Ogre pet;_creating=true;try{pet=new Ogre{NoKillAwards=true,Name="OrdersFixturePet"};}finally{_creating=false;}Require(pet.SetControlMaster(ally),"Ordinary pet control");pet.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Stop(pet);pet.SetMana(20);pet.SetStam(80);NecromancerEffects.SetMana256(pet,6*256+128);BattleOrders.SetStamina256(pet,30*256+73);var petBefore=Pool(pet);
        var far=Fresh(owner,DiabloClass.Paladin);far.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);var moving=Fresh(owner,DiabloClass.Sorceress);moving.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);
        var enemy=new Ogre{NoKillAwards=true};enemy.MoveToWorld(new(owner.X-4,owner.Y,owner.Z),owner.Map);Stop(enemy);int enemyHp=enemy.Hits;
        Full(owner);Cast(owner);var rays=BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==owner.Serial.Value).ToArray();Require(rays.Length==64 && rays.All(x=>x.Ability==CombatAbility.BattleOrders && x.LastCollision==owner.Serial.Value),"BO ring initial last collider/rank");
        Train(owner,CombatAbility.Shout,2);Train(owner,CombatAbility.BattleOrders,2); // Stored launch rank1, live hard Shout duration2.
        Later(120,()=>{far.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);moving.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);});
        Later(700,()=>
        {
            Require(BattleOrders.State(ally)?.Rank==1 && BattleOrders.State(golem)?.Rank==1 && BattleOrders.State(pet)?.Rank==1 && BattleOrders.State(moving)?.Rank==1 && BattleOrders.State(far)==null && BattleOrders.State(enemy)==null && enemy.Hits==enemyHp,"Natural Orders wave friendly/pet/moving/hostile/range filter");
            Scaled(ally,allyBefore);Scaled(pet,petBefore);Scaled(golem,golemBefore);
            Require(golem.HitsMax==(int)(golemBase+ClassResources.Percentage(golemBase,35)) && golem.HitsMaxSeed==golemBase && pet.ManaMax==27 && pet.ManaMaxSeed==20 && pet.StamMax==108,"Pet maxima changed saved species base or wrong percentage pool");
            Require((BattleOrders.State(ally).UntilUtc-TimedEffects.Now).TotalSeconds is >39 and <41,"Launched rank/live hard duration at wave hit");
            golem.SuspendActions();golem.Hits=golem.HitsMax-20;long time=Core.TickCount;golem.RecoverLife(time);int hp=golem.Hits;golem.RecoverLife(time+1000);
            int regen=(NecromancerSummons.Source("claygolem","DamageRegen")*golem.HitsMax)>>4;
            Require(BattleOrders.Regeneration256(golem)==regen && golem.Hits-hp==regen*25/256,"Source pet HP regeneration on changed maximum");
            BattleOrders.Clear(moving);moving.Delete();enemy.Delete();var previous=Pool(ally);Replace(BattleOrders.State(ally) with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});
            Later(180,()=>
            {
                Require(BattleOrders.State(ally)==null,"Natural BO expiry retained percentage");Scaled(ally,previous);
                BattleOrders.Apply(owner,ally,1,TimedEffects.Now);Seed(owner,ally,golem,pet,necro,far);Log.Information("battle-orders-result: passed");
            });
        });
    }
    private static void Seed(PlayerMobile owner,PlayerMobile ally,NecromancerMinion golem,BaseCreature pet,PlayerMobile necro,PlayerMobile far)
    {
        var at=TimedEffects.Now;var hold=at.Add(FixtureSpace.Retention);
        BarbarianShout.Cast(owner,SkillMechanics.Values(owner,CombatAbility.BattleOrders,2),at,CombatAbility.BattleOrders);
        var ray=BarbarianShout.Snapshot().Rays.First(x=>x.Owner==owner.Serial.Value && x.Ability==CombatAbility.BattleOrders && x.Ray==0);
        for(int frame=0;frame<6;frame++){var motion=BarbarianShout.Advance(0,ray.Velocity,ray.AccelerationCounter);ray=ray with{X=ray.X+motion.X,Y=ray.Y+motion.Y,Frame=frame+1,Velocity=motion.Velocity,AccelerationCounter=motion.Counter};}
        ray=ray with{NextUtc=hold,UntilUtc=hold.AddMilliseconds(600)};
        var delay=new WarcryDelay(CombatAbility.BattleOrders,ally.Serial.Value,hold.AddMilliseconds(80));var wave=BarbarianShout.Snapshot();
        BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Owner!=owner.Serial.Value).Append(ray).ToArray(),Delays=(wave.Delays??[]).Where(x=>x.Ability!=CombatAbility.BattleOrders || x.Recipient!=ally.Serial.Value).Append(delay).ToArray()});
        foreach(var actor in new Mobile[]{owner,ally,golem,pet}){Replace(BattleOrders.State(actor) with{UntilUtc=hold});Full(actor);}
        NecromancerEffects.SetMana256(pet,pet.ManaMax*256-128);BattleOrders.SetStamina256(pet,pet.StamMax*256-73);SkillHealth.SetBalance256(golem,golem.HitsMax*256-17);
        var expired=Fresh(owner,DiabloClass.Sorceress);expired.MoveToWorld(new(owner.X+20,owner.Y+3,owner.Z),owner.Map);Full(expired);BattleOrders.Apply(owner,expired,1,at);Replace(BattleOrders.State(expired) with{UntilUtc=at.AddMilliseconds(500)});
        var pools=new Mobile[]{owner,ally,golem,pet}.Select(Pool).ToArray();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ally.Serial.Value,golem.Serial.Value,pet.Serial.Value,necro.Serial.Value,far.Serial.Value,expired.Serial.Value,owner.Location,owner.Map.MapID,pools,ray,delay)));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);ally.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);ally.ClaimAutoStabledPets();var necro=(PlayerMobile)World.FindMobile((Serial)saved.Necro);necro.MoveToWorld(new(owner.X+20,owner.Y,owner.Z),owner.Map);necro.ClaimAutoStabledPets();
        var golem=(NecromancerMinion)World.FindMobile((Serial)saved.Golem);golem.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);Stop(golem);var pet=(BaseCreature)World.FindMobile((Serial)saved.Pet);pet.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Stop(pet);
        var far=(PlayerMobile)World.FindMobile((Serial)saved.Far);far.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);
        foreach(var expected in saved.Pools){Require(Pool(World.FindMobile((Serial)expected.Actor))==expected,"Saved buff/resource fractions/maximum/seed mismatch for "+expected.Actor);}
        Require(BarbarianShout.Snapshot().Rays.Any(x=>x==saved.Ray) && BarbarianShout.Snapshot().Delays.Any(x=>x==saved.Delay) && ClassCombat.For(owner).PendingAbility==CombatAbility.None,"Saved BO ray/delay or transient replay");
        var expired=World.FindMobile((Serial)saved.Expired);Require(BattleOrders.State(expired)==null && expired.Hits<=expired.HitsMax && expired.Mana<=expired.ManaMax && expired.Stam<=expired.StamMax,"Expired offline BO retained enlarged pools");expired.Delete();
        var before=Pool(ally);int casterMana=NecromancerEffects.ManaBalance256(owner);var at=TimedEffects.Now;var wave=BarbarianShout.Snapshot();
        BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Id!=saved.Ray.Id).Append(saved.Ray with{NextUtc=at,UntilUtc=at.AddMilliseconds(600)}).ToArray(),Delays=wave.Delays.Where(x=>x.Ability!=CombatAbility.BattleOrders || x.Recipient!=ally.Serial.Value).Append(saved.Delay with{UntilUtc=at.AddMilliseconds(80)}).ToArray()});
        Later(35,()=>{Require(BattleOrders.State(ally)==before.State,"Saved NextHit delay was bypassed");});
        Later(700,()=>
        {
            Require(BattleOrders.State(ally)?.Rank==2 && NecromancerEffects.ManaBalance256(owner)==casterMana && !BarbarianShout.Snapshot().Rays.Any(x=>x.Owner==owner.Serial.Value),"Resumed BO rank/payment/lifetime");Scaled(ally,before);
            Seed(owner,ally,golem,pet,necro,far);Log.Information("battle-orders-result: passed");
        });
    }
}
