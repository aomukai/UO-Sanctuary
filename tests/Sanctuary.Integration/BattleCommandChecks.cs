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

public static class BattleCommandChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BattleCommandChecks));
    private sealed record Pool(uint Actor,int LifeMax,int ManaMax,int StamMax,int Life256,int Mana256,int Stam256);
    private sealed record Saved(uint Owner,uint Ally,uint Necro,uint Far,uint Expired,Point3D Origin,int Map,BattleCommandState[] States,Pool[] Pools,CommandPet[] Pets,ShoutRay Ray,WarcryDelay Delay);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"battle-command.json");
    private static bool _creating;
    public static void Configure(){Rates();Register("battle-command-check",Check);Register("battle-command-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static bool Fixture(Mobile actor)=>_creating || actor.Name?.StartsWith("CommandFixture",StringComparison.Ordinal)==true;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;var sp=Mobile.StamRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):hp?.Invoke(actor)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):mp?.Invoke(actor)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):sp?.Invoke(actor)??Mobile.DefaultStamRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Battle Command ranks, passives, pets and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"battle-command-result: failed");}});
    private static PlayerMobile Fresh(PlayerMobile near=null,DiabloClass cls=DiabloClass.Barbarian)
    {
        PlayerMobile actor;_creating=true;try{actor=new PlayerMobile{Body=0x190,Name="CommandFixture",Player=true};}finally{_creating=false;}
        actor.AddItem(new Backpack());ClassCreation.Profiles.Assign(actor,cls);ClassCreation.Profiles.AdvanceLevel(actor,99);
        FixtureSpace.Place(actor,near==null?new(6050,1640,0):new(near.X,near.Y+5,near.Z),near?.Map??Map.Felucca);
        if(near==null){actor.MoveToWorld(ClassSkillChecks.Corridor(actor,6050,1640),actor.Map);}
        Require(Mobile.ManaRegenRateHandler(actor)==TimeSpan.FromHours(1),"Command fixture recovery wrapper order");return actor;
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
    private static void Replace(BattleCommandState state)
    {var save=BattleCommand.Snapshot();BattleCommand.Restore(save with{States=save.States.Where(x=>x.Recipient!=state.Recipient).Append(state).ToArray()});}
    private static Pool Pools(Mobile actor)=>new(actor.Serial.Value,actor.HitsMax,actor.ManaMax,actor.StamMax,SkillHealth.Balance256(actor),NecromancerEffects.ManaBalance256(actor),BattleOrders.Stamina256(actor));
    private static void Scaled(Mobile actor,Pool before,int manaDebit=0)
    {
        Require(SkillHealth.Balance256(actor)==BattleOrders.Rescale256(before.Life256,before.LifeMax*256,actor.HitsMax*256),"Command changed life fraction");
        Require(NecromancerEffects.ManaBalance256(actor)==BattleOrders.Rescale256(before.Mana256,before.ManaMax*256,actor.ManaMax*256)-manaDebit,"Command changed mana/payment");
        Require(BattleOrders.Stamina256(actor)==BattleOrders.Rescale256(before.Stam256,before.StamMax*256,actor.StamMax*256),"Command passive stamina callback");
    }
    private static void Full(Mobile actor)
    {SkillHealth.SetBalance256(actor,actor.HitsMax*256);NecromancerEffects.SetMana256(actor,actor.ManaMax*256);BattleOrders.SetStamina256(actor,actor.StamMax*256);}
    private static void Cast(PlayerMobile actor)
    {
        Ready(actor);Require(ClassCombat.Perform(actor,CombatAbility.BattleCommand,null,actor.Location,Core.TickCount),"Command begin");var action=ClassCombat.For(actor);
        Require(!ClassCombat.ResolveAction(actor,action.EffectAt-1) && ClassCombat.ResolveAction(actor,action.EffectAt) && !ClassCombat.ResolveAction(actor,action.EffectAt),"Command timed single release");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.BattleCommand);
        foreach(var (rank,hard,frames) in new[]{(1,0,750),(20,40,10500),(21,40,10750)})
        {var values=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>hard/2,99);Require(values.Mana256==2816 && values.Eval("aurastatcalc1")==1 && values.Eval("auralencalc")==frames,"Pinned Command formulas");}
        Require(NecromancerSummons.Source("battlecommand","LastCollide")==1 && NecromancerSummons.Source("battlecommand","NextDelay")==4,"Command own missile flags");
    }
    private static void Synchronous(PlayerMobile owner)
    {
        Train(owner,CombatAbility.BattleCommand,1);foreach(var ability in new[]{CombatAbility.IncreasedStamina,CombatAbility.IronSkin,CombatAbility.IncreasedSpeed,CombatAbility.NaturalResistance,CombatAbility.SwordMastery}){Train(owner,ability,1);}
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.BattleCommand) && SkillBindings.Instance.Assign(owner,false,CombatAbility.BattleCommand) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.BattleCommand),"Command right-only/quick binding");
        SkillHealth.SetBalance256(owner,101*256+17);NecromancerEffects.SetMana256(owner,80*256+128);BattleOrders.SetStamina256(owner,65*256+73);var before=Pools(owner);
        int defense=ClassStats.Defense(owner),resist=BarbarianCombat.Resistance(owner,ResistanceType.Fire);Cast(owner);Scaled(owner,before,2816);
        Require(BattleCommand.State(owner).Rank==1 && BattleCommand.Bonus(owner)==1 && SkillMechanics.Rank(owner,CombatAbility.BattleCommand)==2 && SkillMechanics.Rank(owner,CombatAbility.BattleOrders)==2,"Command self rank snapshot/soft level");
        Require(ClassStats.Defense(owner)>defense && BarbarianCombat.Resistance(owner,ResistanceType.Fire)>resist && owner.FireResistance==BarbarianCombat.Resistance(owner,ResistanceType.Fire),"Command passive defense/resistance refresh");
        Require(ClassCreation.Profiles.GetRank(owner.Serial,"barbarian.battlecommand")==1 && SkillMechanics.Rank(owner,CombatAbility.WarCry)==0,"Command changed hard ranks/unlocked unlearned skill");
        var values=SkillMechanics.Values(owner,CombatAbility.BattleOrders);Require(values.Eval("auralencalc")==1250,"Soft Shout/Command leaked into hard BO duration synergy");
        before=Pools(owner);Cast(owner);Require(BattleCommand.State(owner).Rank==2 && BattleCommand.Bonus(owner)==1 && SkillMechanics.Rank(owner,CombatAbility.BattleCommand)==2,"Command stacked or failed second-cast rank");Scaled(owner,before,2816);CancelRays(owner);
        BattleCommand.Apply(owner,owner,20,TimedEffects.Now);var high=BattleCommand.State(owner);before=Pools(owner);var other=Fresh(owner);Train(other,CombatAbility.BattleCommand,1);BattleCommand.Apply(other,owner,1,TimedEffects.Now);
        Require(BattleCommand.State(owner).UntilUtc<high.UntilUtc && BattleCommand.State(owner).Creator==owner.Serial.Value && BattleCommand.State(owner).Supplier==other.Serial.Value && BattleCommand.Bonus(owner)==1,"Command weaker replace/creator/supplier");Scaled(owner,before);other.Delete();
        before=Pools(owner);BattleCommand.Clear(owner);Scaled(owner,before);Require(SkillMechanics.Rank(owner,CombatAbility.IncreasedStamina)==1 && owner.FireResistance==resist,"Command removal left soft passive");
        NecromancerEffects.SetMana256(owner,10*256);Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.BattleCommand,null,owner.Location,Core.TickCount),"Unpaid Command begin");
        Full(owner);Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BattleCommand,null,owner.Location,Core.TickCount),"Command interrupt start");var action=ClassCombat.For(owner);int mana=owner.Mana;owner.Paralyzed=true;
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && BattleCommand.State(owner)==null && owner.Mana==mana,"Interrupted Command paid/applied");owner.Paralyzed=false;
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BattleCommand,null,owner.Location,Core.TickCount),"Command lost-mana start");action=ClassCombat.For(owner);NecromancerEffects.SetMana256(owner,10*256);Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && BattleCommand.State(owner)==null,"Command wind-up mana loss");
        Full(owner);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Command Tarnhelm");ClassGear.Refresh(owner);Cast(owner);
        Require(BattleCommand.State(owner).Rank==2 && SkillMechanics.Rank(owner,CombatAbility.BattleCommand)==3,"Real gear/Command additive ranks");CancelRays(owner);BattleCommand.Clear(owner);helm.Delete();ClassGear.Refresh(owner);
        // BO stays stored at its cast rank. Command changes later BO casts only.
        BattleOrders.Apply(owner,owner,1,TimedEffects.Now);var orders=BattleOrders.State(owner);BattleCommand.Apply(owner,owner,1,TimedEffects.Now);Require(BattleOrders.State(owner)==orders && BattleOrders.Percent(owner)==35,"Command rewrote stored BO state");
        BattleOrders.Clear(owner);BattleCommand.Clear(owner);Full(owner);Ready(owner);
    }
    private static void OtherClasses(PlayerMobile owner)
    {
        var sorc=Fresh(owner,DiabloClass.Sorceress);Train(sorc,CombatAbility.Warmth,1);Train(sorc,CombatAbility.FireMastery,1);Train(sorc,CombatAbility.FireBolt,1);var old=SkillMechanics.Values(sorc,CombatAbility.FireBolt);int warmth=SorceressCombat.Passive(sorc,CombatAbility.Warmth);int mastery=SorceressCombat.Passive(sorc,CombatAbility.FireMastery);
        BattleCommand.Apply(owner,sorc,1,TimedEffects.Now);Require(SorceressCombat.Passive(sorc,CombatAbility.Warmth)>warmth && SorceressCombat.Passive(sorc,CombatAbility.FireMastery)>mastery && SkillMechanics.Values(sorc,CombatAbility.FireBolt).Damage256(false)>old.Damage256(false) && old.Rank==1,"Sorc passive/new spell ranks or prior snapshot");BattleCommand.Clear(sorc);sorc.Delete();
        var paladin=Fresh(owner,DiabloClass.Paladin);Train(paladin,CombatAbility.ResistFire,1);Train(paladin,CombatAbility.BlessedAim,1);int cap=PaladinResistance.Maximum(paladin,ResistanceType.Fire);BattleCommand.Apply(owner,paladin,1,TimedEffects.Now);
        Require(SkillMechanics.Rank(paladin,CombatAbility.ResistFire)==2 && PaladinResistance.Maximum(paladin,ResistanceType.Fire)==cap,"Command entered hard-only resist passive");BattleCommand.Clear(paladin);paladin.Delete();
        foreach(var cls in new[]{DiabloClass.Amazon,DiabloClass.Assassin,DiabloClass.Druid,DiabloClass.Warlock})
        {var actor=Fresh(owner,cls);var ability=SkillMechanics.All.First(x=>x.CharacterClass==cls && TalentCatalog.TryGet(cls,x.TalentId,out var t) && t.RequiredLevel==1 && t.Prerequisites.Length==0).Ability;Train(actor,ability,1);BattleCommand.Apply(owner,actor,1,TimedEffects.Now);Require(SkillMechanics.Rank(actor,ability)==2,"Command rank routing "+cls);BattleCommand.Clear(actor);Require(SkillMechanics.Rank(actor,ability)==1,"Command removal "+cls);actor.Delete();}
    }
    private static SkeletonGuardian Summon(PlayerMobile owner,bool mage)
    {
        var victim=new GraveyardSkeleton{NoKillAwards=true};victim.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(victim);victim.Damage(10000,owner);if(victim.Alive){victim.Kill();}var corpse=(Corpse)victim.Corpse;
        Require(SkeletonPets.Usable(owner,corpse),"Command corpse fixture");Full(owner);bool result=mage?NecromancerSummons.Create(owner,CombatAbility.RaiseSkeletalMage,corpse.Serial,corpse.Location):SkeletonPets.Raise(owner,corpse);Require(result,$"Command actual summon mage={mage}, rank={SkeletonPets.Rank(owner)}, mana={owner.Mana}, spawn={owner.Map.CanSpawnMobile(corpse.X,corpse.Y,corpse.Z)}, followers={owner.Followers}/{owner.FollowersMax}, pets={SkeletonPets.Count(owner)}, corpse={corpse.Location}");
        var pet=owner.AllFollowers.OfType<SkeletonGuardian>().Where(x=>x.ControlMaster==owner && (mage?x is NecromancerMinion{IsMage:true}:x.GetType()==typeof(SkeletonGuardian))).OrderByDescending(BattleCommand.PetOrder).First();pet.Name="CommandFixturePet";Stop(pet);pet.MoveToWorld(new(owner.X-3,owner.Y+5,owner.Z),owner.Map);corpse.Delete();return pet;
    }
    private static void PetCaps(PlayerMobile owner)
    {
        var necro=Fresh(owner,DiabloClass.Necromancer);Train(necro,CombatAbility.RaiseSkeleton,2);Train(necro,CombatAbility.RaiseSkeletalMage,2);Train(necro,CombatAbility.SkeletonMastery,1);Train(necro,CombatAbility.SummonResist,1);
        var oldest=Summon(necro,false);var middle=Summon(necro,false);var mageOld=Summon(necro,true);Summon(necro,true);int oldLife=oldest.HitsMax;
        BattleCommand.Apply(owner,necro,1,TimedEffects.Now);Require(SkeletonPets.Limit(SkeletonPets.Rank(necro))==3 && oldest.HitsMax==oldLife && oldest.Rank==2 && oldest.Mastery==1,"Command changed old summon creation snapshot");
        var newest=Summon(necro,false);var mageNew=Summon(necro,true);Require(newest.Rank==3 && newest.Mastery==2 && mageNew.Rank==3 && newest.HitsMax>oldLife,"Command missing new summon snapshot");
        // Distinguish creation ledger from serial ordering explicitly.
        var save=BattleCommand.Snapshot();long a=BattleCommand.PetOrder(oldest),b=BattleCommand.PetOrder(newest);BattleCommand.Restore(save with{Pets=save.Pets.Select(x=>x.Pet==oldest.Serial.Value?x with{Order=b}:x.Pet==newest.Serial.Value?x with{Order=a}:x).ToArray()});
        BattleCommand.Clear(necro);Require(!oldest.Deleted && !middle.Deleted && newest.Deleted && mageOld.Deleted && !mageNew.Deleted && SkeletonPets.Count(necro)==2,"Command cap/FIFO or independent mage type");
        PersistentMinions.Dismiss(necro);necro.Delete();
    }
    private static void Lifecycle(PlayerMobile owner)
    {
        foreach(int mode in new[]{0,1,2})
        {var actor=Fresh(owner);Train(actor,CombatAbility.BattleCommand,1);Train(actor,CombatAbility.IncreasedStamina,1);BattleCommand.Apply(actor,actor,1,TimedEffects.Now);
         if(mode==0){actor.Kill();actor.Resurrect();}else if(mode==1){Require(ClassCreation.Profiles.TryActivateHistory(actor,DiabloClass.Paladin),"Command history");}else{ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);}
         Require(BattleCommand.State(actor)==null && actor.Stam<=actor.StamMax,"Command lifecycle");actor.Corpse?.Delete();actor.Delete();}
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();Synchronous(owner);OtherClasses(owner);PetCaps(owner);Lifecycle(owner);
        var ally=Fresh(owner,DiabloClass.Sorceress);Train(ally,CombatAbility.FireBolt,1);ally.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        var necro=Fresh(owner,DiabloClass.Necromancer);Train(necro,CombatAbility.RaiseSkeleton,2);necro.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        var far=Fresh(owner,DiabloClass.Paladin);far.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);var moving=Fresh(owner,DiabloClass.Sorceress);moving.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);
        var enemy=new Ogre{NoKillAwards=true};enemy.MoveToWorld(new(owner.X-4,owner.Y,owner.Z),owner.Map);Stop(enemy);int hp=enemy.Hits;Full(owner);Cast(owner);
        var rays=BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==owner.Serial.Value).ToArray();Require(rays.Length==64 && rays.All(x=>x.Ability==CombatAbility.BattleCommand && x.Rank==1 && x.LastCollision==owner.Serial.Value),"Command source rank/ring/collider");
        Train(owner,CombatAbility.Shout,2);Train(owner,CombatAbility.BattleCommand,2);
        Later(120,()=>{far.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);moving.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);});
        Later(700,()=>
        {
            Require(BattleCommand.State(ally)?.Rank==1 && BattleCommand.State(necro)?.Rank==1 && BattleCommand.State(moving)?.Rank==1 && BattleCommand.State(far)==null && BattleCommand.State(enemy)==null && enemy.Hits==hp && SkillMechanics.Rank(ally,CombatAbility.FireBolt)==2,"Natural Command wave friendly/moving/hostile/range");
            Require((BattleCommand.State(ally).UntilUtc-TimedEffects.Now).TotalSeconds is >44 and <46,"Command stored rank/live hard duration");BattleCommand.Clear(moving);moving.Delete();enemy.Delete();
            Summon(necro,false);Summon(necro,false);var newest=Summon(necro,false);Replace(BattleCommand.State(necro) with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});
            Later(180,()=>{Require(BattleCommand.State(necro)==null && SkeletonPets.Count(necro)==2 && !newest.Deleted,"Natural Command expiry pet cap");Seed(owner,ally,necro,far);Log.Information("battle-command-result: passed");});
        });
    }
    private static void Seed(PlayerMobile owner,PlayerMobile ally,PlayerMobile necro,PlayerMobile far)
    {
        var at=TimedEffects.Now;var hold=at.AddMinutes(10);BarbarianShout.Cast(owner,SkillMechanics.Values(owner,CombatAbility.BattleCommand,2),at,CombatAbility.BattleCommand);
        BattleCommand.Apply(owner,ally,2,at);BattleCommand.Apply(owner,necro,2,at);if(SkeletonPets.Count(necro)<3){Summon(necro,false);}
        var ray=BarbarianShout.Snapshot().Rays.First(x=>x.Owner==owner.Serial.Value && x.Ability==CombatAbility.BattleCommand && x.Ray==0);
        for(int i=0;i<6;i++){var motion=BarbarianShout.Advance(0,ray.Velocity,ray.AccelerationCounter);ray=ray with{X=ray.X+motion.X,Y=ray.Y+motion.Y,Frame=i+1,Velocity=motion.Velocity,AccelerationCounter=motion.Counter};}
        ray=ray with{NextUtc=hold,UntilUtc=hold.AddMilliseconds(600)};var delay=new WarcryDelay(CombatAbility.BattleCommand,ally.Serial.Value,hold.AddMilliseconds(80));var wave=BarbarianShout.Snapshot();
        BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Owner!=owner.Serial.Value).Append(ray).ToArray(),Delays=(wave.Delays??[]).Where(x=>x.Ability!=CombatAbility.BattleCommand || x.Recipient!=ally.Serial.Value).Append(delay).ToArray()});
        foreach(var actor in new[]{owner,ally,necro}){Replace(BattleCommand.State(actor) with{UntilUtc=hold});Full(actor);}
        BattleOrders.SetStamina256(owner,owner.StamMax*256-73);NecromancerEffects.SetMana256(owner,owner.ManaMax*256-128);
        var expired=Fresh(owner);Train(expired,CombatAbility.IncreasedStamina,1);expired.MoveToWorld(new(owner.X+20,owner.Y+3,owner.Z),owner.Map);Full(expired);BattleCommand.Apply(owner,expired,1,at);Replace(BattleCommand.State(expired) with{UntilUtc=at.AddMilliseconds(500)});
        var pets=BattleCommand.Snapshot().Pets.Where(x=>necro.AllFollowers.Any(p=>p.Serial.Value==x.Pet)).ToArray();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ally.Serial.Value,necro.Serial.Value,far.Serial.Value,expired.Serial.Value,owner.Location,owner.Map.MapID,new[]{owner,ally,necro}.Select(BattleCommand.State).ToArray(),new[]{owner,ally,necro}.Select(Pools).ToArray(),pets,ray,delay)));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);ally.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);var necro=(PlayerMobile)World.FindMobile((Serial)saved.Necro);necro.MoveToWorld(new(owner.X+20,owner.Y,owner.Z),owner.Map);necro.ClaimAutoStabledPets();
        foreach(var pool in saved.Pools){Require(Pools(World.FindMobile((Serial)pool.Actor))==pool,"Command saved pools/maximum/fraction");}foreach(var state in saved.States){Require(BattleCommand.State(World.FindMobile((Serial)state.Recipient))==state,"Command saved rank/creator/expiry");}
        Require(saved.Pets.All(x=>BattleCommand.Snapshot().Pets.Contains(x)) && SkeletonPets.Count(necro)==3 && SkillMechanics.Rank(necro,CombatAbility.RaiseSkeleton)==3,"Command saved FIFO/cap/ranks");
        Require(BarbarianShout.Snapshot().Rays.Any(x=>x==saved.Ray) && BarbarianShout.Snapshot().Delays.Any(x=>x==saved.Delay) && ClassCombat.For(owner).PendingAbility==CombatAbility.None,"Command saved collider/delay or action replay");
        var expired=World.FindMobile((Serial)saved.Expired);Require(BattleCommand.State(expired)==null && expired.Stam==expired.StamMax,"Expired offline Command passive resource callback");expired.Delete();
        var original=BattleCommand.State(ally);int mana=NecromancerEffects.ManaBalance256(owner);var at=TimedEffects.Now;var wave=BarbarianShout.Snapshot();
        BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Id!=saved.Ray.Id).Append(saved.Ray with{NextUtc=at,UntilUtc=at.AddMilliseconds(600)}).ToArray(),Delays=wave.Delays.Where(x=>x.Ability!=CombatAbility.BattleCommand || x.Recipient!=ally.Serial.Value).Append(saved.Delay with{UntilUtc=at.AddMilliseconds(80)}).ToArray()});
        Later(35,()=>Require(BattleCommand.State(ally)==original,"Command saved NextHit bypass"));
        Later(700,()=>
        {
            Require(BattleCommand.State(ally)?.Rank==2 && BattleCommand.State(ally).UntilUtc<original.UntilUtc && NecromancerEffects.ManaBalance256(owner)==mana && !BarbarianShout.Snapshot().Rays.Any(x=>x.Owner==owner.Serial.Value),"Command resumed ray rank/deadline/payment");
            var oldest=necro.AllFollowers.OfType<SkeletonGuardian>().Where(x=>!x.Deleted).OrderBy(BattleCommand.PetOrder).First();BattleCommand.Clear(necro);Require(oldest.Deleted && SkeletonPets.Count(necro)==2,"Reload Command FIFO cap removal");Seed(owner,ally,necro,(PlayerMobile)World.FindMobile((Serial)saved.Far));Log.Information("battle-command-result: passed");
        });
    }
}
