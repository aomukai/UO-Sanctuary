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

public static class HowlChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HowlChecks));
    private sealed record Saved(uint Owner,uint Target,uint Far,uint Expired,Point3D Origin,int Map,int Mana256,NecromancerCurse Curse,ShoutRay Ray);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"howl.json");
    private static bool _creating;
    public static void Configure()
    {
        Rates();Register("howl-check",Check);Register("howl-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(uint id in new[]{saved.Target,saved.Far,saved.Expired}){if(World.FindMobile((Serial)id) is BaseCreature actor){Stop(actor,true);}}};
    }
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static bool Fixture(Mobile actor)=>_creating || actor.Name?.StartsWith("HowlFixture",StringComparison.Ordinal)==true;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;var sp=Mobile.StamRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):hp?.Invoke(actor)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):mp?.Invoke(actor)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):sp?.Invoke(actor)??Mobile.DefaultStamRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Howl source ring and shared Terror AI phase/resource/persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"howl-result: failed");}});
    private static PlayerMobile Fresh(PlayerMobile near=null,DiabloClass cls=DiabloClass.Barbarian,int level=99)
    {
        PlayerMobile actor;_creating=true;try{actor=new PlayerMobile{Body=0x190,Name="HowlFixture",Player=true};}finally{_creating=false;}
        actor.AddItem(new Backpack());ClassCreation.Profiles.Assign(actor,cls);if(level>1){ClassCreation.Profiles.AdvanceLevel(actor,level);}
        if(near!=null){FixtureSpace.Place(actor,new(near.X,near.Y+5,near.Z),near.Map);return actor;}
        for(int x=2700;x<3050;x+=50)for(int y=1000;y<1350;y+=50)
        {
            Point3D point;try{FixtureSpace.Place(actor,new(x,y,0),Map.Felucca);point=ClassSkillChecks.Corridor(actor,x,y);}catch(InvalidOperationException){continue;}
            bool occupied=false;foreach(var other in actor.Map.GetMobilesInRange<Mobile>(point,20)){if(other!=actor){occupied=true;break;}}if(occupied){continue;}
            if(!Server.Spells.SpellHelper.CheckTown(point,actor)){continue;}actor.MoveToWorld(point,actor.Map);return actor;
        }
        throw new InvalidOperationException("No independent Howl corridor");
    }
    private static void Train(PlayerMobile actor,CombatAbility ability,int rank)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Missing talent");foreach(string id in talent.Prerequisites){Train(actor,SkillMechanics.All.Single(x=>x.TalentId==id).Ability,1);}
        while(ClassCreation.Profiles.GetRank(actor.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(actor.Serial,row.TalentId,out var why),"Train: "+why);}
    }
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int dx=3,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {GraveyardSkeleton actor;_creating=true;try{actor=new GraveyardSkeleton{Name="HowlFixtureSkeleton",NoKillAwards=true};}finally{_creating=false;}actor.CurrentWayPoint=new WayPoint();if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(actor,difficulty);}actor.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);Stop(actor);return actor;}
    private static void Stop(BaseCreature actor,bool freeze=false){actor.AIObject?.AITimer.Stop();if(freeze){actor.Frozen=actor.CantWalk=true;}}
    private static void Delete(BaseCreature actor){actor.CurrentWayPoint?.Delete();actor.Delete();}
    private static void Ready(PlayerMobile actor){ClassCombat.Stop(actor);ClassCombat.For(actor).NextAction=ClassCombat.For(actor).RecoveryUntil=Core.TickCount;}
    private static void CancelRays(PlayerMobile actor){var wave=BarbarianShout.Snapshot();BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Owner!=actor.Serial.Value).ToArray()});}
    private static void Replace(BaseCreature actor,NecromancerCurse curse)=>NecromancerEffects.Profiles.Store(actor,(NecromancerEffects.Profiles.Find(actor)??new()) with{Curse=curse});
    private static void Think(BaseCreature actor)
    {Stop(actor);var curse=NecromancerEffects.Curse(actor);Replace(actor,curse with{Terror=curse.Terror with{NextUtc=TimedEffects.Now}});actor.NextReacquireTime=Core.TickCount;actor.AIObject.NextMove=Core.TickCount;Require(NecromancerEffects.Think(actor),"Source Terror think was not consumed");Stop(actor);}
    private static void Cast(PlayerMobile actor)
    {
        Ready(actor);Require(ClassCombat.Perform(actor,CombatAbility.Howl,null,actor.Location,Core.TickCount),"Howl begin");var action=ClassCombat.For(actor);
        Require(!ClassCombat.ResolveAction(actor,action.EffectAt-1) && ClassCombat.ResolveAction(actor,action.EffectAt) && !ClassCombat.ResolveAction(actor,action.EffectAt),"Howl single do-event");
    }
    private static void Goldens()
    {
        var definition=SkillMechanics.For(CombatAbility.Howl);
        foreach(int rank in new[]{1,20,21}){var values=new SkillValues(definition,_=>rank,_=>0,99);Require(values.Mana256==1024 && values.Eval("calc1")==2*(rank-1) && values.Eval("calc3")==75+25*(rank-1),"Pinned Howl formula");}
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT"),".local/class-skills/howl-source-probe.json")));
        int[] Values(JsonElement row)=>row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        foreach(var row in source.RootElement.GetProperty("hitCases").EnumerateArray()){var x=Values(row);if(x[2]==0 && x[3]==1){Require(BarbarianHowl.LevelAllowed(x[0],30,x[1])==(x[5]>0),"Independent level gate");}}
        foreach(var row in source.RootElement.GetProperty("distanceCases").EnumerateArray()){var x=Values(row);Require(SourceTerror.Distance(x[0],x[1],x[2])==x[3],"147 source distance cases");}
        foreach(var row in source.RootElement.GetProperty("terrorAiCases").EnumerateArray())
        {var x=Values(row);var expected=x[7]>0?TerrorDecision.End:x[9]>0?TerrorDecision.Idle:x[10]>0?TerrorDecision.Attack:TerrorDecision.Escape;Require(SourceTerror.Decide(x[0]!=0,x[1]!=0,x[2],x[3]!=0,true)==expected,"384 source AI dispatch cases");if(expected==TerrorDecision.Escape){Require(SourceTerror.Velocity(8,x[6])==x[15],"Source run percentage cap");}}
        foreach(var row in source.RootElement.GetProperty("fallbackCases").EnumerateArray()){var x=Values(row);Require(SourceTerror.Fallback(x[0],x[1],x[2]!=0,x[3]!=0)==(x[4]/2,x[5]/2),"48 source fallback projection cases");}
        foreach(int rank in new[]{1,20,21,80})
        {
            int velocity=(12+2*(rank-1))*256*75/100,counter=0,accel=-1000;
            foreach(var row in source.RootElement.GetProperty("motionCases").EnumerateArray())
            {var x=Values(row);if(x[0]!=rank){continue;}var move=BarbarianShout.Advance(0,velocity,counter,CombatAbility.Howl,accel);velocity=move.Velocity;counter=move.Counter;accel=move.Acceleration;Require((velocity,counter,accel)==(x[2],x[3],x[4]) && move.X==x[5]/65536.0/2,"44 independent Howl acceleration/cap/disable cases");}
        }
        var legacy=JsonSerializer.SerializeToNode(new NecromancerState(new(CombatAbility.Terror,1,1,DateTime.UtcNow.AddSeconds(1),0,0)));legacy["Curse"].AsObject().Remove("Terror");Require(legacy.Deserialize<NecromancerState>().Curse.Terror==null,"Old curse JSON gained source AI metadata");
        Require(SourceTerror.Duration(75,-50)==112 && SourceTerror.Duration(75,50)==38 && SourceTerror.Duration(75,100)==0,"Source curse duration operation");
    }
    private static void Synchronous(PlayerMobile owner)
    {
        Train(owner,CombatAbility.Howl,1);Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Howl) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Howl) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Howl),"Howl right-only/quick binding");
        owner.Mana=owner.ManaMax;int balance=NecromancerEffects.ManaBalance256(owner);Cast(owner);Require(NecromancerEffects.ManaBalance256(owner)==balance-1024 && NecromancerEffects.Curse(owner)==null,"Howl payment/self fear");
        var rays=BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==owner.Serial.Value).ToArray();Require(rays.Length==64 && rays.All(x=>x.Ability==CombatAbility.Howl && x.LastCollision==owner.Serial.Value && x.Velocity==2304),"Howl source ring/velocity/collider");CancelRays(owner);
        NecromancerEffects.SetMana256(owner,3*256);Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.Howl,null,owner.Location,Core.TickCount),"Unpaid Howl begin");
        owner.Mana=owner.ManaMax;Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Howl,null,owner.Location,Core.TickCount),"Howl interrupt start");var action=ClassCombat.For(owner);balance=owner.Mana;owner.Paralyzed=true;Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && owner.Mana==balance,"Interrupted Howl paid");owner.Paralyzed=false;
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Howl,null,owner.Location,Core.TickCount),"Howl mana loss start");action=ClassCombat.For(owner);NecromancerEffects.SetMana256(owner,3*256);Require(!ClassCombat.ResolveAction(owner,action.EffectAt),"Howl mana loss released");owner.Mana=owner.ManaMax;Ready(owner);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Howl Tarnhelm equip");ClassGear.Refresh(owner);Cast(owner);
        Require(BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==owner.Serial.Value).All(x=>x.Rank==2 && x.Velocity==2688),"Howl real soft rank/launch velocity");CancelRays(owner);helm.Delete();ClassGear.Refresh(owner);Ready(owner);
    }
    private static void Phase(PlayerMobile owner)
    {
        var enemy=Enemy(owner,1);Require(SourceTerror.Supported(enemy) && BarbarianHowl.Hit(owner,enemy,20,TimedEffects.Now),"Mapped source Terror apply");Stop(enemy);
        var before=NecromancerEffects.Curse(enemy);Require(before.Ability==CombatAbility.Terror && before.Rank==1 && before.Terror.LaunchRank==20 && !BarbarianHowl.Hit(owner,enemy,1,TimedEffects.Now) && NecromancerEffects.Curse(enemy)==before,"Howl shared Terror rank/no-refresh");
        Think(enemy);Require(SourceTerror.State(enemy) is{Started:true,Override:0,Combat:false,Goal:not null},"Initial in-melee Howl failed to escape first");
        var alternative=Fresh(owner,level:1);alternative.MoveToWorld(new(enemy.X+1,enemy.Y,enemy.Z),owner.Map);Think(enemy);
        Require(SourceTerror.State(enemy) is{Combat:true} state && state.Focus==alternative.Serial.Value && enemy.Combatant==alternative && NecromancerEffects.TargetAllowed(enemy,alternative) && !NecromancerEffects.TargetAllowed(enemy,owner),"Howl later threat acquisition/A1 eligibility");
        Require(enemy.BeginMelee(alternative).TotalMilliseconds>40,"Source special A1 failed to enter native attack clock");enemy.InterruptForRecovery();
        var curse=NecromancerEffects.Curse(enemy);Replace(enemy,curse with{Terror=curse.Terror with{Started=false,Override=owner.Serial.Value,Combat=false,Goal=null}});enemy.MoveToWorld(new(owner.X+20,owner.Y,owner.Z),owner.Map);Think(enemy);
        Require(SourceTerror.State(enemy) is{Started:false,Combat:false,Goal:null,Override:not 0} idle && idle.NextUtc>TimedEffects.Now.AddMilliseconds(300),"Default30-distance/10-frame idle boundary");
        var necro=Fresh(owner,DiabloClass.Necromancer);Train(necro,CombatAbility.Weaken,1);Train(necro,CombatAbility.Attract,1);Train(necro,CombatAbility.Terror,1);enemy.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        Require(NecromancerEffects.Apply(necro,enemy,CombatAbility.Weaken,1) && SourceTerror.State(enemy)==null && !enemy.AIObject.TryGetMoveWake(out _),"Ordinary curse did not cancel shared fear intent");
        Require(NecromancerEffects.Apply(necro,enemy,CombatAbility.Attract,1),"Attract fixture");var attract=NecromancerEffects.Curse(enemy);Require(!BarbarianHowl.Hit(owner,enemy,20,TimedEffects.Now) && NecromancerEffects.Curse(enemy)==attract,"Howl replaced protected Attract");
        NecromancerEffects.Profiles.Clear(enemy);Require(NecromancerEffects.Apply(necro,enemy,CombatAbility.Terror,1),"Ordinary Terror fixture");var terror=NecromancerEffects.Curse(enemy);Require(!BarbarianHowl.Hit(owner,enemy,20,TimedEffects.Now) && NecromancerEffects.Curse(enemy)==terror,"Howl refreshed Necromancer Terror");
        NecromancerEffects.Profiles.Clear(enemy);var unknown=new Ogre();unknown.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);Stop(unknown);Require(!BarbarianHowl.Hit(owner,unknown,20,TimedEffects.Now),"Unknown native AI silently approximated");unknown.Delete();
        var low=Fresh(owner,level:2);Train(low,CombatAbility.Howl,1);var hell=Enemy(owner,6,MonsterDifficulty.Hell);Require(!BarbarianHowl.Hit(low,hell,1,TimedEffects.Now),"Howl ignored current caster level");ClassCreation.Profiles.AdvanceLevel(low,99);Require(BarbarianHowl.Hit(low,hell,1,TimedEffects.Now),"Howl failed live caster-level update");Stop(hell);Delete(hell);
        NecromancerEffects.Profiles.Clear(enemy);Delete(enemy);low.Delete();necro.Delete();alternative.Delete();
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();Synchronous(owner);Phase(owner);var target=Enemy(owner,1);var far=Enemy(owner,5);Stop(far,true);int hp=target.Hits;var start=target.Location;Cast(owner);
        Later(1100,()=>
        {
            Require(NecromancerEffects.Curse(target) is{Ability:CombatAbility.Terror,Rank:1,Terror:{Started:true,Override:0}} && target.Location!=start && target.Hits==hp && NecromancerEffects.Curse(far)==null,"Natural Howl wave/escape/range/non-damage");
            Require(!target.ReturningHome,"Fear loss of combatant incorrectly triggered native leash");
            Later(2400,()=>{Require(SourceTerror.State(target)==null,"Natural Howl expiry failed");Stop(target,true);Seed(owner,target,far);Log.Information("howl-result: passed");});
        });
    }
    private static void Seed(PlayerMobile owner,GraveyardSkeleton target,GraveyardSkeleton far)
    {
        Train(owner,CombatAbility.Howl,20);CancelRays(owner);target.Frozen=target.CantWalk=false;target.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);NecromancerEffects.Profiles.Clear(target);Require(BarbarianHowl.Hit(owner,target,20,TimedEffects.Now),"Seed Terror");Stop(target);
        Think(target);Stop(target,true);var curse=NecromancerEffects.Curse(target);var hold=TimedEffects.Now.Add(FixtureSpace.Retention);curse=curse with{UntilUtc=hold,Terror=curse.Terror with{NextUtc=hold}};Replace(target,curse);
        var now=TimedEffects.Now;BarbarianShout.Cast(owner,SkillMechanics.Values(owner,CombatAbility.Howl,20),now,CombatAbility.Howl);var ray=BarbarianShout.Snapshot().Rays.First(x=>x.Owner==owner.Serial.Value && x.Ray==0);
        for(int i=0;i<6;i++){var move=BarbarianShout.Advance(0,ray.Velocity,ray.AccelerationCounter,CombatAbility.Howl,ray.Acceleration);ray=ray with{X=ray.X+move.X,Y=ray.Y+move.Y,Velocity=move.Velocity,AccelerationCounter=move.Counter,Acceleration=move.Acceleration,Frame=i+1};}
        ray=ray with{NextUtc=hold,UntilUtc=hold.AddMilliseconds(480)};var wave=BarbarianShout.Snapshot();BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Owner!=owner.Serial.Value).Append(ray).ToArray()});
        Require(ray.Velocity==3072 && ray.Acceleration==0 && ray.AccelerationCounter==0,"Saved post-cap acceleration state");
        var expired=Enemy(owner,12);Require(BarbarianHowl.Hit(owner,expired,1,now),"Offline expiry seed");Stop(expired,true);var shortCurse=NecromancerEffects.Curse(expired);Replace(expired,shortCurse with{UntilUtc=now.AddMilliseconds(500)});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,far.Serial.Value,expired.Serial.Value,owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),curse,ray)));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);var target=(GraveyardSkeleton)World.FindMobile((Serial)saved.Target);var far=(GraveyardSkeleton)World.FindMobile((Serial)saved.Far);
        Require(NecromancerEffects.Curse(target)==saved.Curse && SourceTerror.State(target)?.Started==true && NecromancerEffects.ManaBalance256(owner)==saved.Mana256 && BarbarianShout.Snapshot().Rays.Any(x=>x==saved.Ray) && ClassCombat.For(owner).PendingAbility==CombatAbility.None,"Saved Howl resources/AI phase/ray/acceleration or transient replay");
        var expired=(BaseCreature)World.FindMobile((Serial)saved.Expired);Require(SourceTerror.State(expired)==null && NecromancerEffects.Curse(expired)==null,"Saved offline Howl expiry");Delete(expired);
        NecromancerEffects.Profiles.Clear(target);target.MoveToWorld(new(owner.X+6,owner.Y,owner.Z),owner.Map);Stop(target,true);var wave=BarbarianShout.Snapshot();var now=TimedEffects.Now;BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Id!=saved.Ray.Id).Append(saved.Ray with{NextUtc=now,UntilUtc=now.AddMilliseconds(480)}).ToArray()});int mana=owner.Mana;
        Later(80,()=>Require(BarbarianShout.Snapshot().Rays.Any(x=>x.Id==saved.Ray.Id && x.Acceleration==0 && x.AccelerationCounter==0 && x.Velocity==3072),"Resumed Howl acceleration re-enabled"));
        Later(550,()=>
        {
            Require(SourceTerror.State(target)?.LaunchRank==20 && owner.Mana==mana && !BarbarianShout.Snapshot().Rays.Any(x=>x.Owner==owner.Serial.Value),"Resumed Howl rank/mana/ray lifetime");
            target.Frozen=target.CantWalk=false;Think(target);Require(SourceTerror.State(target) is{Started:true,Override:0,Goal:not null},"Saved Howl source AI did not resume");Seed(owner,target,far);Log.Information("howl-result: passed");
        });
    }
}
