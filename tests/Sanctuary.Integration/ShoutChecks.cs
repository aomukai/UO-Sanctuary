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

public static class ShoutChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ShoutChecks));
    private sealed record Saved(uint Owner,uint Ally,uint Pet,uint Enemy,uint Far,uint Expired,Point3D Origin,int Map,int Mana,ShoutState Self,ShoutState Friend,ShoutState PetState,ShoutRay Ray);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shout.json");
    private static bool _creating;
    public static void Configure(){Rate();Register("shout-check",Check);Register("shout-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rate();
    private static void Rate()
    {
        var prior=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=actor=>actor is PlayerMobile && (_creating || actor.Name=="ShoutFixture")?TimeSpan.FromHours(1):prior?.Invoke(actor)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Shout travelling rays, ally defense, payment and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"shout-result: failed");}});
    private static PlayerMobile Fresh(PlayerMobile near=null,DiabloClass cls=DiabloClass.Barbarian)
    {
        PlayerMobile actor;_creating=true;try{actor=new PlayerMobile{Body=0x190,Name="ShoutFixture",Player=true};}finally{_creating=false;}
        actor.AddItem(new Backpack());ClassCreation.Profiles.Assign(actor,cls);ClassCreation.Profiles.AdvanceLevel(actor,99);
        FixtureSpace.Place(actor,near==null?new(6050,1720,0):new(near.X,near.Y+4,near.Z),near?.Map??Map.Felucca);
        if(near==null){actor.MoveToWorld(ClassSkillChecks.Corridor(actor,6050,1720),actor.Map);}
        Require(Mobile.ManaRegenRateHandler(actor)==TimeSpan.FromHours(1),"Shout fixture recovery wrapper order");return actor;
    }
    private static void Train(PlayerMobile actor,CombatAbility ability,int rank)
    {
        string id=SkillMechanics.For(ability).TalentId;Require(TalentCatalog.TryGet(DiabloClass.Barbarian,id,out var talent),"Shout talent");
        foreach(string prior in talent.Prerequisites){var prerequisite=SkillMechanics.All.Single(x=>x.TalentId==prior);Train(actor,prerequisite.Ability,1);}
        while(ClassCreation.Profiles.GetRank(actor.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(actor.Serial,id,out var why),"Training: "+why);}
    }
    private static void Stop(BaseCreature actor){actor.Frozen=actor.CantWalk=true;actor.Combatant=null;actor.AIObject?.AITimer.Stop();}
    private static void Ready(PlayerMobile owner){ClassCombat.Stop(owner);ClassCombat.For(owner).NextAction=ClassCombat.For(owner).RecoveryUntil=Core.TickCount;}
    private static void Mana(PlayerMobile owner,int mana=-1){owner.Mana=mana<0?owner.ManaMax:mana;NecromancerEffects.Profiles.Store(owner,new());}
    private static void Replace(ShoutState state)
    {var save=BarbarianShout.Snapshot();BarbarianShout.Restore(save with{States=save.States.Where(x=>x.Recipient!=state.Recipient).Append(state).ToArray()});}
    private static void Replace(ShoutRay ray)
    {var save=BarbarianShout.Snapshot();BarbarianShout.Restore(save with{Rays=save.Rays.Where(x=>x.Owner!=ray.Owner).Append(ray).ToArray()});}
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.Shout);
        foreach(var (rank,hard,defense,frames) in new[]{(1,0,100,750),(20,0,290,5500),(21,40,300,10750)})
        {
            var values=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>hard/2,99);
            Require(values.Mana256==1536 && values.Eval("aurastatcalc1")==defense && values.Eval("auralencalc")==frames,"Pinned Shout formulas");
        }
        Require(ClassAbilities.SelfTarget(CombatAbility.Shout) && !ClassAbilities.Find(CombatAbility.Shout).Value.Left && ClassAbilities.Find(CombatAbility.Shout).Value.Right,"Shout right/self metadata");
        for(int ray=0;ray<64;ray++)
        {
            int velocity=5760,counter=0;double distance=0;
            for(int frame=1;frame<=14;frame++)
            {
                var move=BarbarianShout.Advance(ray,velocity,counter);velocity=move.Velocity;counter=move.Counter;
                Require(velocity==5760-500*(frame/5) && counter==frame%5,"Compiled source acceleration schedule");distance+=Math.Sqrt(move.X*move.X+move.Y*move.Y);
            }
            Require(distance>8.8 && distance<9.1,"Shout native provisional ray travel");
        }
    }
    private static void Synchronous(PlayerMobile owner,PlayerMobile ally)
    {
        Train(owner,CombatAbility.Shout,1);Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Shout) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Shout) && SkillBindings.Instance.AssignQuick(owner,2,CombatAbility.Shout),"Shout binding masks");
        Mana(owner,5);Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.Shout,null,owner.Location,Core.TickCount) && BarbarianShout.State(owner)==null,"Unpaid start");
        Mana(owner);Ready(owner);int before=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.Perform(owner,CombatAbility.Shout,null,owner.Location,Core.TickCount),"Shout wind-up");var action=ClassCombat.For(owner);long at=action.EffectAt;
        Require(NecromancerEffects.ManaBalance256(owner)==before && BarbarianShout.State(owner)==null && !ClassCombat.ResolveAction(owner,at-1),"Early Shout payment/buff");
        owner.Paralyzed=true;Require(!ClassCombat.ResolveAction(owner,at) && NecromancerEffects.ManaBalance256(owner)==before && BarbarianShout.State(owner)==null,"Interrupted Shout paid/applied");owner.Paralyzed=false;
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Shout,null,owner.Location,Core.TickCount),"Second wind-up");action=ClassCombat.For(owner);Mana(owner,5);
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && BarbarianShout.State(owner)==null && NecromancerEffects.ManaBalance256(owner)==1280,"Mana lost during wind-up granted Shout");
        Mana(owner);Ready(owner);
        // Source replacement does not prefer a stronger/longer existing statlist.
        var time=TimedEffects.Now;BarbarianShout.Apply(owner,ally,20,time);var high=BarbarianShout.State(ally);BarbarianShout.Apply(owner,ally,1,time.AddMilliseconds(40));
        Require(BarbarianShout.State(ally).DefensePercent==100 && BarbarianShout.State(ally).UntilUtc<high.UntilUtc,"Weaker/shorter Shout replacement");
        var other=Fresh(owner);Train(other,CombatAbility.Shout,1);BarbarianShout.Apply(other,ally,2,time.AddMilliseconds(80));
        Require(BarbarianShout.State(ally).Creator==owner.Serial.Value && BarbarianShout.State(ally).Supplier==other.Serial.Value && BarbarianShout.State(ally).Rank==2,"Recast creator/supplier distinction");other.Delete();BarbarianShout.Clear(ally);
        Train(owner,CombatAbility.IronSkin,1);int basis=owner.Dex/4;
        BarbarianShout.Apply(owner,owner,1,time);Require(ClassStats.Defense(owner)==ClassStats.ScaleDefense(basis,130),"Shout/Iron Skin additive defense");
        Train(owner,CombatAbility.Berserk,1);BarbarianBerserk.Begin(owner,SkillMechanics.Values(owner,CombatAbility.Berserk),Core.TickCount);
        Require(ClassStats.Defense(owner)==0 && BarbarianShout.Defense(owner)==100,"Berserk final override lost to Shout");BarbarianBerserk.Clear(owner);BarbarianShout.Clear(owner);
        var origin=owner.Location;owner.MoveToWorld(new(1496,1628,10),Map.Trammel);Ready(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Shout,null,owner.Location,Core.TickCount),"Shout allowed in town");owner.MoveToWorld(origin,Map.Felucca);
    }
    private static void Gear(PlayerMobile reference)
    {
        var actor=Fresh(reference);Train(actor,CombatAbility.Shout,1);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        Require(actor.EquipItem(helm),"Shout real Tarnhelm");ClassGear.Refresh(actor);Require(SkillMechanics.Rank(actor,CombatAbility.Shout)==2,"Shout effective gear rank");
        Mana(actor);Ready(actor);Require(ClassCombat.Perform(actor,CombatAbility.Shout,null,actor.Location,Core.TickCount) && ClassCombat.ResolveAction(actor,ClassCombat.For(actor).EffectAt),"Geared Shout event");
        var state=BarbarianShout.State(actor);actor.AddToBackpack(helm);ClassGear.Refresh(actor);
        Require(state.Rank==2 && state.DefensePercent==110 && SkillMechanics.Rank(actor,CombatAbility.Shout)==1 && BarbarianShout.State(actor)==state && BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==actor.Serial.Value).All(x=>x.Rank==2),"Launched/self Shout changed with gear");
        BarbarianShout.Clear(actor);actor.Delete();
    }
    private static void Lifecycle(PlayerMobile reference)
    {
        foreach(int transition in new[]{0,1,2})
        {
            var actor=Fresh(reference);Train(actor,CombatAbility.Shout,1);BarbarianShout.Apply(actor,actor,1,TimedEffects.Now);
            if(transition==0){actor.Kill();actor.Resurrect();}
            else if(transition==1){Require(ClassCreation.Profiles.TryActivateHistory(actor,DiabloClass.Paladin),"Shout history exit");}
            else{ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);}
            Require(BarbarianShout.State(actor)==null,"Shout death/history/respec cleanup");actor.Corpse?.Delete();actor.Delete();
        }
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();var ally=Fresh(owner,DiabloClass.Sorceress);Synchronous(owner,ally);Lifecycle(owner);Gear(owner);
        var pet=new GraveyardSkeleton();pet.MoveToWorld(new(owner.X,owner.Y+4,owner.Z),owner.Map);Require(pet.SetControlMaster(ally),"Shout pet master");Stop(pet);
        var enemy=new Ogre{NoKillAwards=true};enemy.MoveToWorld(new(owner.X-4,owner.Y,owner.Z),owner.Map);Stop(enemy);
        var far=Fresh(owner,DiabloClass.Paladin);far.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        var moving=Fresh(owner,DiabloClass.Sorceress);moving.MoveToWorld(new(owner.X+12,owner.Y+2,owner.Z),owner.Map);
        var expiring=Fresh(owner);expiring.MoveToWorld(new(owner.X+20,owner.Y,owner.Z),owner.Map);BarbarianShout.Apply(owner,expiring,1,TimedEffects.Now);Replace(BarbarianShout.State(expiring) with{UntilUtc=TimedEffects.Now.AddMilliseconds(50)});
        ally.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);int enemyHp=enemy.Hits,allyDefense=ClassStats.Defense(ally),petDefense=ClassStats.Defense(pet);
        Mana(owner);Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.Perform(owner,CombatAbility.Shout,null,owner.Location,Core.TickCount),"Shout cast start");var action=ClassCombat.For(owner);
        Require(ClassCombat.ResolveAction(owner,action.EffectAt) && !ClassCombat.ResolveAction(owner,action.EffectAt),"Shout single do-event");
        Require(NecromancerEffects.ManaBalance256(owner)==mana-1536 && BarbarianShout.State(owner)?.Rank==1 && BarbarianShout.State(ally)==null && BarbarianShout.Snapshot().Rays.Count(x=>x.Owner==owner.Serial.Value)==64,"Immediate self/payment/launched ring");
        Train(owner,CombatAbility.BattleOrders,1); // Live hard synergy changes after launch; launched rank remains1.
        Train(owner,CombatAbility.Shout,2);
        Later(120,()=>{far.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);moving.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);});
        Later(700,()=>
        {
            Require(BarbarianShout.State(moving)?.Rank==1 && BarbarianShout.State(expiring)==null,$"Moving ally rank{BarbarianShout.State(moving)?.Rank}, expired state{BarbarianShout.State(expiring)} at {owner.Location}");BarbarianShout.Clear(moving);moving.Delete();expiring.Delete();
            Require(BarbarianShout.State(ally)?.Rank==1 && BarbarianShout.State(pet)?.Rank==1 && BarbarianShout.State(enemy)==null && BarbarianShout.State(far)==null && enemy.Hits==enemyHp,"Natural travelling ally/pet/range/hostile filter");
            Require(ClassStats.Defense(ally)==ClassStats.ScaleDefense(ally.Dex/4,100) && ClassStats.Defense(ally)>allyDefense && ClassStats.Defense(pet)==ClassStats.ScaleDefense(CreatureProfiles.Defense(pet),100) && ClassStats.Defense(pet)>=petDefense,"Native recipient defense");
            var remaining=(BarbarianShout.State(ally).UntilUtc-TimedEffects.Now).TotalSeconds;
            Require(remaining>34 && remaining<36 && !BarbarianShout.Snapshot().Rays.Any(x=>x.Owner==owner.Serial.Value),"Live hard synergy duration/source lifetime");
            // Received timed state survives supplier death/deletion; recipient lifetime controls removal.
            var dead=Fresh(owner);Train(dead,CombatAbility.Shout,1);BarbarianShout.Apply(dead,far,1,TimedEffects.Now);dead.Delete();Require(BarbarianShout.State(far)!=null,"Caster deletion erased received Shout");BarbarianShout.Clear(far);
            Seed(owner,ally,pet,enemy,far);Log.Information("shout-result: passed");
        });
    }
    private static void Seed(PlayerMobile owner,PlayerMobile ally,BaseCreature pet,BaseCreature enemy,PlayerMobile far)
    {
        var hold=TimedEffects.Now.AddMinutes(10);foreach(var target in new Mobile[]{owner,ally,pet}){Replace(BarbarianShout.State(target) with{UntilUtc=hold});}
        var expired=Fresh(owner);BarbarianShout.Apply(owner,expired,1,TimedEffects.Now);Replace(BarbarianShout.State(expired) with{UntilUtc=TimedEffects.Now.AddMilliseconds(500)});
        BarbarianShout.Cast(owner,SkillMechanics.Values(owner,CombatAbility.Shout,2),TimedEffects.Now);
        foreach(var target in new Mobile[]{owner,ally,pet}){Replace(BarbarianShout.State(target) with{UntilUtc=hold});}
        var ray=BarbarianShout.Snapshot().Rays.First(x=>x.Owner==owner.Serial.Value && x.Ray==0);
        for(int frame=0;frame<6;frame++){var motion=BarbarianShout.Advance(0,ray.Velocity,ray.AccelerationCounter);ray=ray with{X=ray.X+motion.X,Y=ray.Y+motion.Y,Frame=frame+1,Velocity=motion.Velocity,AccelerationCounter=motion.Counter};}
        ray=ray with{NextUtc=hold,UntilUtc=hold.AddMilliseconds(600)};Replace(ray); // Explicit saved mid-flight inspection; natural travel is checked above.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ally.Serial.Value,pet.Serial.Value,enemy.Serial.Value,far.Serial.Value,expired.Serial.Value,owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),BarbarianShout.State(owner),BarbarianShout.State(ally),BarbarianShout.State(pet),ray)));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);ally.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);ally.ClaimAutoStabledPets();
        var pet=(BaseCreature)World.FindMobile((Serial)saved.Pet);pet.MoveToWorld(new(owner.X,owner.Y+4,owner.Z),owner.Map);Stop(pet);
        var enemy=(BaseCreature)World.FindMobile((Serial)saved.Enemy);Stop(enemy);var far=(PlayerMobile)World.FindMobile((Serial)saved.Far);far.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);
        Require(BarbarianShout.State(owner)==saved.Self && BarbarianShout.State(ally)==saved.Friend && BarbarianShout.State(pet)==saved.PetState && BarbarianShout.Snapshot().Rays.Any(x=>x==saved.Ray) && NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved Shout recipients/ray/payment");
        Require(BarbarianShout.State(World.FindMobile((Serial)saved.Expired))==null && ClassCombat.For(owner).PendingAbility==CombatAbility.None,"Expired/transient saved action");World.FindMobile((Serial)saved.Expired)?.Delete();
        BarbarianShout.Clear(ally);var at=TimedEffects.Now;Replace(saved.Ray with{NextUtc=at,UntilUtc=at.AddMilliseconds(600)});
        Later(700,()=>
        {
            Require(BarbarianShout.State(ally)?.Rank==2 && !BarbarianShout.Snapshot().Rays.Any(x=>x.Owner==owner.Serial.Value) && NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Resumed Shout ray/repeated payment");
            Seed(owner,ally,pet,enemy,far);Log.Information("shout-result: passed");
        });
    }
}
