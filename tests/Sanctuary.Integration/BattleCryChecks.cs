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

public static class BattleCryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BattleCryChecks));
    private sealed record Saved(uint Owner,uint Target,uint Far,uint Expired,Point3D Origin,int Map,int Mana256,NecromancerCurse Curse,ShoutRay Ray,WarcryDelay Delay,int Defense);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"battlecry.json");
    private static bool _creating;
    public static void Configure()
    {
        Rates();Register("battlecry-check",Check);Register("battlecry-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(uint id in new[]{saved.Target,saved.Far,saved.Expired}){if(World.FindMobile((Serial)id) is BaseCreature actor){Stop(actor,true);}}};
    }
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static bool Fixture(Mobile actor)=>_creating || actor.Name?.StartsWith("BattleCryFixture",StringComparison.Ordinal)==true;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;var sp=Mobile.StamRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):hp?.Invoke(actor)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):mp?.Invoke(actor)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):sp?.Invoke(actor)??Mobile.DefaultStamRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Battle Cry shared curse/stat pools/wave/persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"battlecry-result: failed");}});
    private static PlayerMobile Fresh(PlayerMobile near=null,DiabloClass cls=DiabloClass.Barbarian,int level=99)
    {
        PlayerMobile actor;_creating=true;try{actor=new PlayerMobile{Body=0x190,Name="BattleCryFixture",Player=true};}finally{_creating=false;}
        actor.AddItem(new Backpack());ClassCreation.Profiles.Assign(actor,cls);if(level>1){ClassCreation.Profiles.AdvanceLevel(actor,level);}
        if(near!=null){FixtureSpace.Place(actor,new(near.X,near.Y+5,near.Z),near.Map);return actor;}
        for(int x=2700;x<3050;x+=50)for(int y=1000;y<1350;y+=50)
        {
            Point3D point;try{FixtureSpace.Place(actor,new(x,y,0),Map.Felucca);point=ClassSkillChecks.Corridor(actor,x,y);}catch(InvalidOperationException){continue;}
            bool occupied=false;foreach(var other in actor.Map.GetMobilesInRange<Mobile>(point,20)){if(other!=actor){occupied=true;break;}}if(occupied){continue;}
            if(!Server.Spells.SpellHelper.CheckTown(point,actor)){continue;}actor.MoveToWorld(point,actor.Map);return actor;
        }
        throw new InvalidOperationException("No independent Battle Cry corridor");
    }
    private static void Train(PlayerMobile actor,CombatAbility ability,int rank)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Missing talent");foreach(string id in talent.Prerequisites){Train(actor,SkillMechanics.All.Single(x=>x.TalentId==id).Ability,1);}
        while(ClassCreation.Profiles.GetRank(actor.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(actor.Serial,row.TalentId,out var why),"Train: "+why);}
    }
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int dx=3,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {GraveyardSkeleton actor;_creating=true;try{actor=new GraveyardSkeleton{Name="BattleCryFixtureSkeleton",NoKillAwards=true};}finally{_creating=false;}actor.CurrentWayPoint=new WayPoint();if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(actor,difficulty);}actor.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);Stop(actor);return actor;}
    private static void Stop(BaseCreature actor,bool freeze=false){actor.AIObject?.AITimer.Stop();if(freeze){actor.Frozen=actor.CantWalk=true;}}
    private static void Delete(BaseCreature actor){actor.CurrentWayPoint?.Delete();actor.Delete();}
    private static void Ready(PlayerMobile actor){ClassCombat.Stop(actor);ClassCombat.For(actor).NextAction=ClassCombat.For(actor).RecoveryUntil=Core.TickCount;}
    private static void CancelRays(PlayerMobile actor){var wave=BarbarianShout.Snapshot();BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Owner!=actor.Serial.Value).ToArray()});}
    private static void Replace(BaseCreature actor,NecromancerCurse curse)=>NecromancerEffects.Profiles.Store(actor,(NecromancerEffects.Profiles.Find(actor)??new()) with{Curse=curse});
    private static void Cast(PlayerMobile actor)
    {
        Ready(actor);Require(ClassCombat.Perform(actor,CombatAbility.BattleCry,null,actor.Location,Core.TickCount),"Battle Cry begin");var action=ClassCombat.For(actor);
        Require(!ClassCombat.ResolveAction(actor,action.EffectAt-1) && ClassCombat.ResolveAction(actor,action.EffectAt) && !ClassCombat.ResolveAction(actor,action.EffectAt),"Battle Cry single do-event");
    }
    private static void Goldens()
    {
        var definition=SkillMechanics.For(CombatAbility.BattleCry);
        foreach(int rank in new[]{1,20,21})
        {var values=new SkillValues(definition,key=>key==definition.SourceKey?rank:0,_=>0,99);Require(values.Mana256==1280 && values.Eval("auralencalc")==300+60*(rank-1) && values.Eval("aurastatcalc1")==-50-2*(rank-1) && values.Eval("aurastatcalc2")==-25-(rank-1),"Pinned Battle Cry formulas");}
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT"),".local/class-skills/battlecry-source-probe.json")));
        int[] Values(JsonElement row)=>row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        foreach(var row in source.RootElement.GetProperty("damageCases").EnumerateArray())
        {var x=Values(row);var range=ClassStats.PhysicalEndpoints256(x[0],x[0]+2,x[1]+x[2]);Require(range.Min+Math.Clamp(x[3],0,range.Max-range.Min-1)==x[4],"135 independent additive weapon ED cases");}
        foreach(var row in source.RootElement.GetProperty("defenseCases").EnumerateArray())
        {var x=Values(row);int result=ClassStats.ScaleDefense(x[0],x[1]+x[2]);Require(BarbarianBerserk.ApplyOverride(result,x[3])==Math.Max(0,x[4]),"120 independent additive defense/final override cases");}
    }
    private static void Synchronous(PlayerMobile owner)
    {
        Train(owner,CombatAbility.BattleCry,1);Require(ClassAbilities.SelfTarget(CombatAbility.BattleCry),"Shared client/server self-target metadata");Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.BattleCry) && SkillBindings.Instance.Assign(owner,false,CombatAbility.BattleCry) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.BattleCry),"Battle Cry bindings");
        owner.Mana=owner.ManaMax;int balance=NecromancerEffects.ManaBalance256(owner);Cast(owner);Require(NecromancerEffects.ManaBalance256(owner)==balance-1280 && NecromancerEffects.Curse(owner)==null,"Battle Cry payment/self curse");
        var rays=BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==owner.Serial.Value).ToArray();Require(rays.Length==64 && rays.All(x=>x.Ability==CombatAbility.BattleCry && x.LastCollision==owner.Serial.Value && x.Velocity==2304),"Shared ring launch");CancelRays(owner);
        var wave=BarbarianShout.Snapshot();var now=TimedEffects.Now;var delay=new WarcryDelay(CombatAbility.BattleCry,owner.Serial.Value,now.AddMilliseconds(160));
        BarbarianShout.Restore(wave with{Delays=wave.Delays.Append(delay).ToArray()});Require(NecromancerSummons.Source("battlecry","NextDelay")==4 && BarbarianShout.Delayed(CombatAbility.BattleCry,owner.Serial.Value,now.AddMilliseconds(159)) && !BarbarianShout.Delayed(CombatAbility.BattleCry,owner.Serial.Value,now.AddMilliseconds(160)) && !BarbarianShout.Delayed(CombatAbility.BattleOrders,owner.Serial.Value,now),"Four-frame exact delay boundary/skill isolation");BarbarianShout.Restore(wave);
        NecromancerEffects.SetMana256(owner,4*256);Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.BattleCry,null,owner.Location,Core.TickCount),"Unpaid Battle Cry begin");
        owner.Mana=owner.ManaMax;Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BattleCry,null,owner.Location,Core.TickCount),"Interrupt start");var action=ClassCombat.For(owner);balance=owner.Mana;owner.Paralyzed=true;Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && owner.Mana==balance,"Interrupted cry payment");owner.Paralyzed=false;
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BattleCry,null,owner.Location,Core.TickCount),"Mana loss start");action=ClassCombat.For(owner);NecromancerEffects.SetMana256(owner,4*256);Require(!ClassCombat.ResolveAction(owner,action.EffectAt),"Mana loss released cry");owner.Mana=owner.ManaMax;Ready(owner);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Tarnhelm equip");ClassGear.Refresh(owner);Cast(owner);Require(BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==owner.Serial.Value).All(x=>x.Rank==2),"Effective launched rank");CancelRays(owner);helm.Delete();ClassGear.Refresh(owner);Ready(owner);
    }
    private static void Replacement(PlayerMobile owner)
    {
        var enemy=Enemy(owner,1);Stop(enemy,true);var other=Fresh(owner);var now=TimedEffects.Now;
        int defense=ClassStats.Defense(enemy);BarbarianShout.Apply(owner,enemy,1,now);Require(BarbarianBattleCry.Hit(owner,enemy,1,now) && ClassStats.Defense(enemy)==ClassStats.ScaleDefense(defense,BarbarianShout.Defense(enemy)-50),"Battle Cry compounded instead of joining Shout armor pool");BarbarianShout.Clear(enemy);
        foreach(int oldRank in new[]{0,1,20,21})foreach(int rank in new[]{1,20,21})foreach(bool different in new[]{false,true})foreach(bool attract in new[]{false,true})
        {
            NecromancerCurse old=oldRank==0?null:new(attract?CombatAbility.Attract:different?CombatAbility.Terror:CombatAbility.BattleCry,other.Serial.Value,oldRank,now.AddMinutes(5),other.X,other.Y);Replace(enemy,old);
            bool accepted=BarbarianBattleCry.Hit(owner,enemy,rank,now);bool expected=old==null || !attract && (different || rank>=oldRank);Require(accepted==expected,"Source curse allocation/rank/Attract acceptance");
            var current=NecromancerEffects.Curse(enemy);if(!accepted){Require(current==old,"Rejected cry mutated curse");continue;}
            uint creator=old?.Ability==CombatAbility.BattleCry && old.Rank==rank?other.Serial.Value:owner.Serial.Value;
            Require(current.Ability==CombatAbility.BattleCry && current.Rank==rank && current.Source==creator && current.UntilUtc==now.AddMilliseconds((300+60*(rank-1))*40L),"Same rank creator/refresh or replacement metadata");
        }
        Replace(enemy,null);Require(BarbarianHowl.Hit(owner,enemy,1,now),"Fear replacement fixture");Stop(enemy,true);Require(BarbarianBattleCry.Hit(owner,enemy,1,now) && SourceTerror.State(enemy)==null && !enemy.AIObject.TryGetMoveWake(out _),"Cry did not release shared fear intent");
        var necro=Fresh(owner,DiabloClass.Necromancer);Train(necro,CombatAbility.Weaken,1);Require(NecromancerEffects.Apply(necro,enemy,CombatAbility.Weaken,1) && NecromancerEffects.Curse(enemy)?.Ability==CombatAbility.Weaken,"Necro curse did not replace Battle Cry");
        Replace(enemy,new(CombatAbility.BattleCry,owner.Serial.Value,1,now.AddMilliseconds(-1),owner.X,owner.Y));Require(NecromancerEffects.DefenseStatPercent(enemy)==0 && NecromancerEffects.DamageStatPercent(enemy)==0,"Expired stat consumers");
        var unknown=new Ogre();unknown.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);Stop(unknown,true);Require(!BarbarianBattleCry.Hit(owner,unknown,1,now),"Unmapped monster silently approximated");unknown.Delete();necro.Delete();other.Delete();Delete(enemy);
    }
    private sealed class NoCritical:Random{public override int Next(int max)=>max-1;}
    private static void DamagePools(PlayerMobile owner)
    {
        var enemy=Enemy(owner,1);Stop(enemy,true);var weapon=new Dagger();ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Physical pool dagger");ClassGear.Refresh(owner);
        var raw=ClassStats.WeaponBaseDamage(owner);int basis=ClassStats.DamageBonus(owner,weapon);var normal=GearEffects.Damage(owner,0,enemy,200,_=>0);
        foreach(var ability in new[]{CombatAbility.BattleCry,CombatAbility.Weaken,CombatAbility.Decrepify})
        {
            ReplaceOwner(new(ability,enemy.Serial.Value,20,TimedEffects.Now.AddMinutes(5),enemy.X,enemy.Y));int penalty=NecromancerEffects.DamageStatPercent(owner);
            var range=ClassStats.SkillPhysicalRange256(owner,200,0);Require(range==ClassStats.PhysicalEndpoints256(raw.Min,raw.Max,basis+200+penalty),"Player skill ED was not additive");
            var basic=ClassStats.PhysicalDamageRange(owner);Require(basic.Min==Math.Max(1,raw.Min*Math.Max(10,100+basis+penalty)/100),"Basic attack additive pool");
            var packet=GearEffects.Damage(owner,0,enemy,200,_=>0);Require(packet.Physical<normal.Physical && packet.Fire==normal.Fire && packet.Cold==normal.Cold && packet.Lightning==normal.Lightning,"Curse physical/element separation");
            var expected=SkillCombat.MitigatePrepared(owner,enemy,packet);var prepared=GearEffects.PrepareWeaponImpact(owner,enemy,0,new NoCritical(),packet);Require(prepared.Parts.Physical==expected.Physical,"Prepared weapon applied curse twice");
            foreach(bool spell in new[]{false,true})
            {
                SkillHealth.SetBalance256(enemy,enemy.HitsMax*256);var curse=NecromancerEffects.Curse(owner);int before=SkillHealth.Balance256(enemy);SkillCombat.ResolveHitParts(owner,enemy,new(1,0,0,0,0),false,spell:spell);int lost=before-SkillHealth.Balance256(enemy);ReplaceOwner(null);
                SkillHealth.SetBalance256(enemy,enemy.HitsMax*256);int control=SkillHealth.Balance256(enemy);SkillCombat.ResolveHitParts(owner,enemy,new(1,0,0,0,0),false,spell:spell);Require(lost==control-SkillHealth.Balance256(enemy),$"Physical spell/retaliation inherited weapon ED: {ability} spell={spell} cursed={lost} plain={control-SkillHealth.Balance256(enemy)} remaining={SkillHealth.Balance256(enemy)}");ReplaceOwner(curse);
            }
            ReplaceOwner(null);
        }
        Replace(enemy,new(CombatAbility.BattleCry,owner.Serial.Value,20,TimedEffects.Now.AddMinutes(5),owner.X,owner.Y));Require(PaladinAuras.NativeWeaponMultiplier(enemy,true)==.56 && PaladinAuras.NativeWeaponMultiplier(enemy,false)==1,"NPC weapon/spell gate");
        var pet=new SkeletonGuardian();pet.MoveToWorld(new(owner.X+1,owner.Y+1,owner.Z),owner.Map);Stop(pet,true);var uncursed=SkeletonStats.For(pet.Level,pet.Rank,pet.Mastery);Replace(pet,new(CombatAbility.Weaken,owner.Serial.Value,1,TimedEffects.Now.AddMinutes(5),owner.X,owner.Y));var cursed=SkeletonStats.For(pet.Level,pet.Rank,pet.Mastery,NecromancerEffects.DamageStatPercent(pet));Require(pet.SkillWeaponDamage==(cursed.MinDamage,cursed.MaxDamage) && PaladinAuras.NativeWeaponMultiplier(pet,true)==1,"Summon live pool/double application");pet.Delete();
        weapon.Delete();ClassGear.Refresh(owner);Delete(enemy);
        void ReplaceOwner(NecromancerCurse curse)=>NecromancerEffects.Profiles.Store(owner,(NecromancerEffects.Profiles.Find(owner)??new()) with{Curse=curse});
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();Synchronous(owner);Replacement(owner);DamagePools(owner);var target=Enemy(owner,1);Stop(target,true);var far=Enemy(owner,5);Stop(far,true);int hp=target.Hits,defense=ClassStats.Defense(target);Cast(owner);
        Later(1100,()=>
        {
            Require(NecromancerEffects.Curse(target) is{Ability:CombatAbility.BattleCry,Rank:1} && ClassStats.Defense(target)==ClassStats.ScaleDefense(defense,-50) && target.Hits==hp && NecromancerEffects.Curse(far)==null,"Natural wave/defense/non-damage/range");
            Later(12000,()=>{Require(NecromancerEffects.Curse(target)==null && ClassStats.Defense(target)==defense && NecromancerEffects.DamageStatPercent(target)==0,"Natural Battle Cry expiry");Seed(owner,target,far);Log.Information("battlecry-result: passed");});
        });
    }
    private static void Seed(PlayerMobile owner,GraveyardSkeleton target,GraveyardSkeleton far)
    {
        CancelRays(owner);var now=TimedEffects.Now;target.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Replace(target,null);Require(BarbarianBattleCry.Hit(owner,target,20,now),"Seed curse");Stop(target,true);var curse=NecromancerEffects.Curse(target) with{UntilUtc=now.Add(FixtureSpace.Retention)};Replace(target,curse);
        BarbarianShout.Cast(owner,SkillMechanics.Values(owner,CombatAbility.BattleCry,20),now,CombatAbility.BattleCry);var ray=BarbarianShout.Snapshot().Rays.First(x=>x.Owner==owner.Serial.Value && x.Ray==0) with{Frame=6,X=owner.X+1.5,Velocity=1304,AccelerationCounter=1,Acceleration=-1000,NextUtc=now.Add(FixtureSpace.Retention),UntilUtc=now.Add(FixtureSpace.Retention).AddMilliseconds(480)};
        var wave=BarbarianShout.Snapshot();var delay=new WarcryDelay(CombatAbility.BattleCry,target.Serial.Value,now.Add(FixtureSpace.Retention));BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Owner!=owner.Serial.Value).Append(ray).ToArray(),Delays=wave.Delays.Where(x=>x.Ability!=CombatAbility.BattleCry || x.Recipient!=target.Serial.Value).Append(delay).ToArray()});
        var expired=Enemy(owner,12);Require(BarbarianBattleCry.Hit(owner,expired,1,now),"Offline expiry seed");Stop(expired,true);Replace(expired,NecromancerEffects.Curse(expired) with{UntilUtc=now.AddMilliseconds(500)});
        NecromancerEffects.SetMana256(owner,NecromancerEffects.ManaBalance256(owner)-17);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,far.Serial.Value,expired.Serial.Value,owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),curse,ray,delay,ClassStats.Defense(target))));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);var target=(GraveyardSkeleton)World.FindMobile((Serial)saved.Target);var far=(GraveyardSkeleton)World.FindMobile((Serial)saved.Far);var wave=BarbarianShout.Snapshot();
        Require(NecromancerEffects.Curse(target)==saved.Curse && NecromancerEffects.ManaBalance256(owner)==saved.Mana256 && ClassStats.Defense(target)==saved.Defense && wave.Rays.Contains(saved.Ray) && wave.Delays.Contains(saved.Delay) && ClassCombat.For(owner).PendingAbility==CombatAbility.None,"Saved curse/rank/creator/defense/mana/ray/delay/transient state");
        var expired=(BaseCreature)World.FindMobile((Serial)saved.Expired);Require(NecromancerEffects.Curse(expired)==null && NecromancerEffects.DamageStatPercent(expired)==0,"Offline expiry consumers");Delete(expired);
        Replace(target,null);var now=TimedEffects.Now;Require(BarbarianShout.Delayed(CombatAbility.BattleCry,target.Serial.Value,now) && !BarbarianShout.Delayed(CombatAbility.BattleOrders,target.Serial.Value,now),"Skill-local four-frame gate");
        BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Id!=saved.Ray.Id).Append(saved.Ray with{NextUtc=now,UntilUtc=now.AddMilliseconds(480)}).ToArray(),Delays=wave.Delays.Where(x=>x!=saved.Delay).ToArray()});int mana=owner.Mana;
        Later(550,()=>{Require(NecromancerEffects.Curse(target) is{Ability:CombatAbility.BattleCry,Rank:20} && owner.Mana==mana && !BarbarianShout.Snapshot().Rays.Any(x=>x.Owner==owner.Serial.Value),"Resumed ray/collision/mana/lifetime");Seed(owner,target,far);Log.Information("battlecry-result: passed");});
    }
}
