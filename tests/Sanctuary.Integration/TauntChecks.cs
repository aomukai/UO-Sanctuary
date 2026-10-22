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

public static class TauntChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TauntChecks));
    private sealed record Saved(uint Owner,uint Target,uint Far,uint Expired,Point3D Origin,int Map,int Mana256,NecromancerCurse Curse,int Attack);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"taunt.json");
    private static bool _creating;
    public static void Configure()
    {
        Rates();Register("taunt-check",Check);Register("taunt-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(uint id in new[]{saved.Target,saved.Far,saved.Expired}){if(World.FindMobile((Serial)id) is BaseCreature actor){Stop(actor,true);}}};
    }
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static bool Fixture(Mobile actor)=>_creating || actor.Name?.StartsWith("TauntFixture",StringComparison.Ordinal)==true;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;var sp=Mobile.StamRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):hp?.Invoke(actor)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):mp?.Invoke(actor)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):sp?.Invoke(actor)??Mobile.DefaultStamRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Taunt shared curse/stat pools/wave/persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"taunt-result: failed");}});
    private static PlayerMobile Fresh(PlayerMobile near=null,DiabloClass cls=DiabloClass.Barbarian,int level=99)
    {
        PlayerMobile actor;_creating=true;try{actor=new PlayerMobile{Body=0x190,Name="TauntFixture",Player=true};}finally{_creating=false;}
        actor.AddItem(new Backpack());ClassCreation.Profiles.Assign(actor,cls);if(level>1){ClassCreation.Profiles.AdvanceLevel(actor,level);}
        if(near!=null){FixtureSpace.Place(actor,new(near.X,near.Y+5,near.Z),near.Map);return actor;}
        for(int x=2700;x<3050;x+=50)for(int y=1000;y<1350;y+=50)
        {
            Point3D point;try{FixtureSpace.Place(actor,new(x,y,0),Map.Felucca);point=ClassSkillChecks.Corridor(actor,x,y);}catch(InvalidOperationException){continue;}
            bool occupied=false;foreach(var other in actor.Map.GetMobilesInRange<Mobile>(point,20)){if(other!=actor){occupied=true;break;}}if(occupied){continue;}
            if(!Server.Spells.SpellHelper.CheckTown(point,actor)){continue;}actor.MoveToWorld(point,actor.Map);return actor;
        }
        throw new InvalidOperationException("No independent Taunt corridor");
    }
    private static void Train(PlayerMobile actor,CombatAbility ability,int rank)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Missing talent");foreach(string id in talent.Prerequisites){Train(actor,SkillMechanics.All.Single(x=>x.TalentId==id).Ability,1);}
        while(ClassCreation.Profiles.GetRank(actor.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(actor.Serial,row.TalentId,out var why),"Train: "+why);}
    }
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int dx=3,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {GraveyardSkeleton actor;_creating=true;try{actor=new GraveyardSkeleton{Name="TauntFixtureSkeleton",NoKillAwards=true};}finally{_creating=false;}actor.CurrentWayPoint=new WayPoint();if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(actor,difficulty);}actor.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);Stop(actor);return actor;}
    private static void Stop(BaseCreature actor,bool freeze=false){actor.AIObject?.AITimer.Stop();if(freeze){actor.Frozen=actor.CantWalk=true;}}
    private static void Delete(BaseCreature actor){actor.CurrentWayPoint?.Delete();actor.Delete();}
    private static void Ready(PlayerMobile actor){ClassCombat.Stop(actor);ClassCombat.For(actor).NextAction=ClassCombat.For(actor).RecoveryUntil=Core.TickCount;}
    private static void Replace(BaseCreature actor,NecromancerCurse curse)=>NecromancerEffects.Profiles.Store(actor,(NecromancerEffects.Profiles.Find(actor)??new()) with{Curse=curse});
    private static void Cast(PlayerMobile actor,Mobile target=null,Point3D? point=null)
    {
        Ready(actor);Require(ClassCombat.Perform(actor,CombatAbility.Taunt,target,point??target?.Location??actor.Location,Core.TickCount),"Taunt begin");var action=ClassCombat.For(actor);
        Require(!ClassCombat.ResolveAction(actor,action.EffectAt-1) && ClassCombat.ResolveAction(actor,action.EffectAt) && !ClassCombat.ResolveAction(actor,action.EffectAt),"Taunt single do-event");
    }
    private static void Think(BaseCreature actor)
    {
        Stop(actor);var curse=NecromancerEffects.Curse(actor);Replace(actor,curse with{Terror=curse.Terror with{NextUtc=TimedEffects.Now}});actor.NextReacquireTime=Core.TickCount;actor.AIObject.NextMove=Core.TickCount;NecromancerEffects.Think(actor);Stop(actor);
    }
    private static void Goldens()
    {
        var definition=SkillMechanics.For(CombatAbility.Taunt);
        foreach(int rank in new[]{1,20,21}){var values=new SkillValues(definition,key=>key==definition.SourceKey?rank:0,_=>0,99);Require(values.Mana256==768 && values.Eval("auralencalc")==0 && values.Eval("aurastatcalc1")==-5-2*(rank-1) && values.Eval("aurastatcalc2")==-5-2*(rank-1),"Pinned untimed Taunt formulas");}
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT"),".local/class-skills/taunt-source-probe.json")));
        foreach(var row in source.RootElement.GetProperty("aiCases").EnumerateArray())
        {
            var x=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();var expected=x[6]>0?TauntDecision.End:x[7]>0?TauntDecision.Approach:x[11]>0?TauntDecision.AttackNearby:TauntDecision.AttackTaunter;
            Require(SourceTerror.DecideTaunt(x[0]!=0,x[1]!=0,x[2]!=0,x[3]!=0,x[4]!=0 && x[5]!=0)==expected,"64 independent source AI transitions");
        }
        Require(!ClassAbilities.SelfTarget(CombatAbility.Taunt),"Taunt must retain enemy/automatic acquisition metadata");
    }
    private static void Synchronous(PlayerMobile owner)
    {
        Train(owner,CombatAbility.Taunt,1);Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Taunt) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Taunt) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Taunt),"Taunt bindings");
        owner.Mana=owner.ManaMax;int balance=NecromancerEffects.ManaBalance256(owner);Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.Taunt,null,owner.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==balance,"No-target Taunt payment");
        var near=Enemy(owner,2);Stop(near,true);var far=Enemy(owner,5);Stop(far,true);Require(BarbarianTaunt.Target(owner,Serial.Zero)==near && BarbarianTaunt.Target(owner,far.Serial)==far,"Caster-nearest fallback/explicit target");
        Cast(owner,null,far.Location);Require(NecromancerEffects.ManaBalance256(owner)==balance-768 && NecromancerEffects.Curse(near) is{Ability:CombatAbility.Taunt,Rank:1,UntilUtc:var until} && until==DateTime.MaxValue && NecromancerEffects.Curse(far)==null,"Ground cast chose cursor-nearest or invented timed curse");
        NecromancerEffects.SetMana256(owner,2*256);Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.Taunt,far,far.Location,Core.TickCount),"Unpaid Taunt begin");owner.Mana=owner.ManaMax;
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Taunt,far,far.Location,Core.TickCount),"Interrupted start");var action=ClassCombat.For(owner);balance=owner.Mana;owner.Paralyzed=true;Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && owner.Mana==balance,"Interrupted Taunt payment");owner.Paralyzed=false;
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Taunt,far,far.Location,Core.TickCount),"Mana loss start");action=ClassCombat.For(owner);NecromancerEffects.SetMana256(owner,2*256);Require(!ClassCombat.ResolveAction(owner,action.EffectAt),"Mana loss released Taunt");owner.Mana=owner.ManaMax;
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Taunt soft gear");ClassGear.Refresh(owner);Cast(owner,far);Require(NecromancerEffects.Curse(far) is{Rank:2,Terror:{LaunchRank:2}},"Effective rank not used");helm.Delete();ClassGear.Refresh(owner);
        Delete(near);Delete(far);var a=Enemy(owner,3);var b=Enemy(owner,-3);Stop(a,true);Stop(b,true);Require(BarbarianTaunt.Target(owner,Serial.Zero)==null && BarbarianTaunt.Target(owner,b.Serial)==b,"Unverified automatic tie was silently invented");Delete(a);Delete(b);
        var unknown=new Ogre();unknown.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);Stop(unknown,true);Require(BarbarianTaunt.Target(owner,unknown.Serial)==null,"Unmapped Taunt target silently approximated");unknown.Delete();Ready(owner);
    }
    private sealed class AttackProbe:GraveyardSkeleton
    {
        public AttackProbe(){}
        public AttackProbe(Serial serial):base(serial){}
        public override NormalMonsterStats NormalStats=>base.NormalStats with{AttackChance=0,Attack1Chance=0,StallFrames=1000};
    }
    private static void Phases(PlayerMobile owner)
    {
        var target=Enemy(owner,1);Stop(target,true);int ar=ClassStats.AttackRating(target),defense=ClassStats.Defense(target);var now=TimedEffects.Now;
        Require(BarbarianTaunt.Apply(owner,target,20,now),"Initial Taunt allocation");Think(target);Require(SourceTerror.State(target) is{Started:true,Combat:false,Goal:not null},"Initial in-melee Taunt did not approach first");Think(target);target.Frozen=false;
        Require(SourceTerror.State(target) is{Combat:true} phase && phase.Override==owner.Serial.Value && target.Combatant==owner && !NamedControl.SpecialAllowed(target),"Taunt direct A1/control");target.InterruptForRecovery();Stop(target,true);
        Require(ClassStats.AttackRating(target)==ar+(int)((long)ar*-43/100) && ClassStats.Defense(target)==defense && NecromancerEffects.DamageStatPercent(target)==-43 && Math.Abs(PaladinAuras.NativeWeaponMultiplier(target,true)-.57)<.000001,"Additive AR/weapon damage without invented armor loss");
        var attackProbe=new AttackProbe{Name="TauntFixtureA1",NoKillAwards=true};attackProbe.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(attackProbe);Require(BarbarianTaunt.Apply(owner,attackProbe,1,now),"A1 probe apply");Think(attackProbe);Think(attackProbe);
        Require(attackProbe.BeginMelee(owner,Core.TickCount+10000).TotalMilliseconds==attackProbe.NormalStats.Attack1.DurationMs+attackProbe.NormalStats.AiDelayMs,"Source control did not force A1");attackProbe.InterruptForRecovery();Delete(attackProbe);
        var alternative=Fresh(owner,level:1);target.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);alternative.MoveToWorld(new(target.X-1,target.Y,target.Z),owner.Map);Think(target);
        Require(SourceTerror.State(target) is{Started:false,Combat:true} nearby && nearby.Override==alternative.Serial.Value && nearby.Focus==alternative.Serial.Value && target.Combatant==alternative,$"Acquired melee target did not replace dynamic target/reset phase: {JsonSerializer.Serialize(SourceTerror.State(target))}, alt={alternative.Serial.Value}, visible={target.CanSee(alternative)}, los={target.InLOS(alternative)}, enemy={target.IsEnemy(alternative)}, harm={target.CanBeHarmful(alternative,false)}");Think(target);
        Require(SourceTerror.State(target) is{Started:true,Combat:false} next && next.Override==alternative.Serial.Value,"After alternate attack approach reverted silently to original caster");
        var second=Fresh(owner);Require(BarbarianTaunt.Apply(second,target,20,now),"Equal rank recast");Require(NecromancerEffects.Curse(target).Source==owner.Serial.Value && SourceTerror.State(target) is{Started:false} equal && equal.Override==second.Serial.Value,"Equal-rank creator/restart");Think(target);
        var before=NecromancerEffects.Curse(target);Require(!BarbarianTaunt.Apply(owner,target,1,now) && NecromancerEffects.Curse(target).Rank==20 && NecromancerEffects.Curse(target).Source==before.Source && SourceTerror.State(target).Started && SourceTerror.State(target).Override==owner.Serial.Value,"Lower-rank stats/phase/override path");
        int mana=NecromancerEffects.ManaBalance256(owner);Cast(owner,target);Require(NecromancerEffects.ManaBalance256(owner)==mana-768 && NecromancerEffects.Curse(target).Rank==20,"Do71 success must pay despite weaker curse allocation rejection");
        Require(BarbarianTaunt.Apply(second,target,21,now) && NecromancerEffects.Curse(target).Source==second.Serial.Value && NecromancerEffects.Curse(target).Rank==21,"Stronger rank did not replace creator");second.Delete();Think(target);
        Require(SourceTerror.State(target)==null && NecromancerEffects.Curse(target) is{Ability:CombatAbility.Taunt,Rank:21} && NecromancerEffects.AttackStatPercent(target)==-45 && NamedControl.SpecialAllowed(target),"Missing dynamic target removed untimed stats or retained special AI");
        var necro=Fresh(owner,DiabloClass.Necromancer);Train(necro,CombatAbility.Weaken,1);Require(NecromancerEffects.Apply(necro,target,CombatAbility.Weaken,1) && NecromancerEffects.Curse(target).Ability==CombatAbility.Weaken,"Ordinary curse replacement");
        NecromancerEffects.Profiles.Clear(target);Require(BarbarianHowl.Hit(owner,target,1,now) && BarbarianTaunt.Apply(owner,target,1,now) && SourceTerror.State(target)?.Skill==CombatAbility.Taunt,"Taunt did not replace shared source fear");Stop(target,true);
        NecromancerEffects.Profiles.Clear(target);Train(necro,CombatAbility.Attract,1);Require(NecromancerEffects.Apply(necro,target,CombatAbility.Attract,1),"Attract seed");Ready(owner);mana=owner.Mana;Require(!ClassCombat.Perform(owner,CombatAbility.Taunt,target,target.Location,Core.TickCount) && owner.Mana==mana,"Unsupported Attract branch was silently approximated");
        alternative.Delete();necro.Delete();Delete(target);Ready(owner);
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();Synchronous(owner);Phases(owner);var target=Enemy(owner,6);var start=target.Location;int hp=target.Hits;Cast(owner,target);
        Later(2200,()=>{Stop(target,true);Require(NecromancerEffects.Curse(target) is{Ability:CombatAbility.Taunt,UntilUtc:var until} && until==DateTime.MaxValue && target.Location!=start && target.Hits==hp && !target.ReturningHome,"Natural untimed approach/non-damage/leash");Seed(owner,target);Log.Information("taunt-result: passed");});
    }
    private static void Seed(PlayerMobile owner,GraveyardSkeleton target)
    {
        var now=TimedEffects.Now;Train(owner,CombatAbility.Taunt,20);owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Clear(target);target.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);Cast(owner,target);Require(NecromancerEffects.Curse(target)?.Rank==20,"Saved learned-rank Taunt");Stop(target,true);Think(target);
        var curse=NecromancerEffects.Curse(target);curse=curse with{Terror=curse.Terror with{NextUtc=now.AddMinutes(10)}};Replace(target,curse);
        var statsOnly=Enemy(owner,12);Stop(statsOnly,true);var vanished=Fresh(owner);Require(BarbarianTaunt.Apply(vanished,statsOnly,1,now),"Saved stat-only seed");vanished.Delete();Think(statsOnly);Require(SourceTerror.State(statsOnly)==null && NecromancerEffects.Curse(statsOnly)?.UntilUtc==DateTime.MaxValue,"AI exit stat preservation seed");
        var control=Enemy(owner,15);Stop(control,true);NecromancerEffects.SetMana256(owner,NecromancerEffects.ManaBalance256(owner)-17);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,control.Serial.Value,statsOnly.Serial.Value,owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),curse,ClassStats.AttackRating(target))));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);var target=(GraveyardSkeleton)World.FindMobile((Serial)saved.Target);var control=(GraveyardSkeleton)World.FindMobile((Serial)saved.Far);var statsOnly=(GraveyardSkeleton)World.FindMobile((Serial)saved.Expired);
        Require(NecromancerEffects.Curse(target)==saved.Curse && ClassStats.AttackRating(target)==saved.Attack && NecromancerEffects.ManaBalance256(owner)==saved.Mana256 && ClassCombat.For(owner).PendingAbility==CombatAbility.None,"Saved untimed curse/AI phase/AR/fractional mana/transient cast");
        Require(SourceTerror.State(statsOnly)==null && NecromancerEffects.Curse(statsOnly)?.UntilUtc==DateTime.MaxValue && NecromancerEffects.AttackStatPercent(statsOnly)==-5,"Saved ended AI lost persistent stats");
        var start=target.Location;target.Frozen=target.CantWalk=false;Think(target);target.AIObject.AITimer.Start();int mana=NecromancerEffects.ManaBalance256(owner);
        Later(1200,()=>{Stop(target,true);Require(target.Location!=start && NecromancerEffects.Curse(target)?.Rank==20 && NecromancerEffects.ManaBalance256(owner)==mana,"Saved native approach failed or paid again");Delete(control);Delete(statsOnly);Seed(owner,target);Log.Information("taunt-result: passed");});
    }
}
