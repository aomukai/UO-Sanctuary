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
public static class WarCryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WarCryChecks));
    private sealed record Saved(uint Owner,uint Target,uint Far,uint Expired,Point3D Origin,int Map,int Mana256,ShoutRay Ray,WarcryDelay Delay,SourceShield Shield,CombatStunState Stun);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"warcry.json");
    private static bool _creating;
    public static void Configure()
    {
        Rates();Register("warcry-check",Check);Register("warcry-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(uint id in new[]{saved.Target,saved.Far,saved.Expired}){if(World.FindMobile((Serial)id) is BaseCreature actor){Stop(actor,true);}}};
    }
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static bool Fixture(Mobile actor)=>_creating || actor.Name?.StartsWith("WarCryFixture",StringComparison.Ordinal)==true;
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
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"warcry-result: failed");}});
    private static PlayerMobile Fresh(PlayerMobile near=null,DiabloClass cls=DiabloClass.Barbarian,int level=99)
    {
        PlayerMobile actor;_creating=true;try{actor=new PlayerMobile{Body=0x190,Name="WarCryFixture",Player=true};}finally{_creating=false;}
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
    {GraveyardSkeleton actor;_creating=true;try{actor=new GraveyardSkeleton{Name="WarCryFixtureSkeleton",NoKillAwards=true};}finally{_creating=false;}actor.SetHits(30000);actor.SetResistance(ResistanceType.Physical,0);actor.CurrentWayPoint=new WayPoint();if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(actor,difficulty);}actor.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);Stop(actor);return actor;}
    private static void Stop(BaseCreature actor,bool freeze=false){actor.AIObject?.AITimer.Stop();if(freeze){actor.Frozen=actor.CantWalk=true;}}
    private static void Delete(BaseCreature actor){actor.CurrentWayPoint?.Delete();actor.Delete();}
    private static void Ready(PlayerMobile actor){ClassCombat.Stop(actor);ClassCombat.For(actor).NextAction=ClassCombat.For(actor).RecoveryUntil=Core.TickCount;}
    private static void CancelRays(PlayerMobile actor){var wave=BarbarianShout.Snapshot();BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Owner!=actor.Serial.Value).ToArray()});}
    private static void Cast(PlayerMobile owner)
    {
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.WarCry,null,owner.Location,Core.TickCount),"War Cry begin");var action=ClassCombat.For(owner);
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt-1) && ClassCombat.ResolveAction(owner,action.EffectAt) && !ClassCombat.ResolveAction(owner,action.EffectAt),"War Cry single SC event");
    }
    private static void Goldens()
    {
        var definition=SkillMechanics.For(CombatAbility.WarCry);
        foreach(int rank in new[]{1,20,21})
        {var v=new SkillValues(definition,key=>key==definition.SourceKey?rank:0,_=>0,99);Require(v.Mana256==(40+3*(rank-1))*64 && v.Eval("calc4")==25+5*(rank-1),"Pinned fractional cost/stun");}
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT"),".local/class-skills/warcry-source-probe.json")));
        int[] Values(JsonElement row)=>row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        foreach(var row in source.RootElement.GetProperty("physicalCases").EnumerateArray())
        {
            var x=Values(row);var v=new SkillValues(definition,key=>key==definition.SourceKey?x[0]:0,key=>key=="Howl"?x[1]:0,99);
            Require(v.PhysicalDamage256(false)==x[3] && v.PhysicalDamage256(true)==x[4] && BarbarianWarCry.DamageRoll(x[3],x[4],x[2])==x[5],"390 independent source endpoints/rolls");
        }
        foreach(var row in source.RootElement.GetProperty("monsterBlockCases").EnumerateArray())
        {var x=Values(row);string key=x[0] switch{243=>"diablo",310=>"doomknight1",333=>"diabloclone",359=>"act3hire",_=>"skeleton1"};Require(SourceBlocking.Rate(x[3],x[1]!=0,key,x[2]==1)==x[4],"160 independent monster block cases");}
        Require(BarbarianWarCry.DamageRoll(256,256,9)==256 && BarbarianWarCry.DamageRoll(512,256,999)==511 && BarbarianWarCry.DamageRoll(0,512,0)==0,"Degenerate/reversed source damage ranges");
        foreach(var ability in new[]{CombatAbility.Shout,CombatAbility.BattleOrders,CombatAbility.BattleCommand,CombatAbility.Howl,CombatAbility.BattleCry})
        {var now=TimedEffects.Now;var old=new ShoutRay(1,1,0,1,0,1,1,0,0,0,0,now.AddSeconds(1),now,ability);Require(JsonSerializer.Deserialize<ShoutRay>(JsonSerializer.Serialize(old))==old,"Old ring payload defaults");}
    }
    private static int Life(Mobile actor)=>SkillHealth.Balance256(actor);
    private static ShoutRay Ray(PlayerMobile owner,int rank=1,int min=8192,int max=8193)=>new(1,owner.Serial.Value,owner.Map.MapID,rank,0,owner.X,owner.Y,owner.Z,0,2304,0,TimedEffects.Now.AddMilliseconds(640),TimedEffects.Now,CombatAbility.WarCry,owner.Serial.Value,null,min,max);
    private static void Shield(BaseCreature actor,int choice)
    {SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=actor.Serial.Value).Append(new SourceShield(actor.Serial.Value,CreatureProfiles.Definition(actor).Reference,choice)).ToArray());}
    private static void Synchronous(PlayerMobile owner)
    {
        Train(owner,CombatAbility.WarCry,1);Require(ClassAbilities.SelfTarget(CombatAbility.WarCry),"Self-target metadata");Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.WarCry) && SkillBindings.Instance.Assign(owner,false,CombatAbility.WarCry) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.WarCry),"War Cry right/quick bindings");
        owner.Mana=owner.ManaMax;int before=NecromancerEffects.ManaBalance256(owner);Cast(owner);Require(NecromancerEffects.ManaBalance256(owner)==before-2560,"Single ten-mana payment");
        var rays=BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==owner.Serial.Value).ToArray();var v=SkillMechanics.Values(owner,CombatAbility.WarCry);
        Require(rays.Length==64 && rays.All(x=>x.Ability==CombatAbility.WarCry && x.Velocity==2304 && x.LastCollision==owner.Serial.Value && x.MinDamage256==v.PhysicalDamage256(false) && x.MaxDamage256==v.PhysicalDamage256(true)),"Shared ring snapshots");CancelRays(owner);
        NecromancerEffects.SetMana256(owner,2559);Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.WarCry,null,owner.Location,Core.TickCount),"Insufficient fractional mana");
        owner.Mana=owner.ManaMax;Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.WarCry,null,owner.Location,Core.TickCount),"Interrupt start");var action=ClassCombat.For(owner);before=NecromancerEffects.ManaBalance256(owner);owner.Paralyzed=true;Require(!ClassCombat.ResolveAction(owner,action.EffectAt) && NecromancerEffects.ManaBalance256(owner)==before,"Interrupted cast spent mana");owner.Paralyzed=false;
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.WarCry,null,owner.Location,Core.TickCount),"Lost mana start");action=ClassCombat.For(owner);NecromancerEffects.SetMana256(owner,2559);Require(!ClassCombat.ResolveAction(owner,action.EffectAt),"Unpaid release");owner.Mana=owner.ManaMax;
        Train(owner,CombatAbility.WarCry,20);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Named +skill helm");ClassGear.Refresh(owner);
        before=NecromancerEffects.ManaBalance256(owner);Cast(owner);rays=BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==owner.Serial.Value).ToArray();Require(NecromancerEffects.ManaBalance256(owner)==before-6400 && rays.All(x=>x.Rank==21),"Soft21 cost");
        helm.Delete();ClassGear.Refresh(owner);Train(owner,CombatAbility.Howl,2);Require(BarbarianShout.Snapshot().Rays.Where(x=>x.Owner==owner.Serial.Value).SequenceEqual(rays),"Owner changes mutated launched endpoints/rank");CancelRays(owner);
        owner.Mana=owner.ManaMax;before=NecromancerEffects.ManaBalance256(owner);Cast(owner);Require(NecromancerEffects.ManaBalance256(owner)==before-6208,"Rank20 quarter-mana payment");CancelRays(owner);owner.Mana=owner.ManaMax;
        var wave=BarbarianShout.Snapshot();var now=TimedEffects.Now;var delay=new WarcryDelay(CombatAbility.WarCry,owner.Serial.Value,now.AddMilliseconds(240));BarbarianShout.Restore(wave with{Delays=wave.Delays.Append(delay).ToArray()});Require(BarbarianShout.Delayed(CombatAbility.WarCry,owner.Serial.Value,now.AddMilliseconds(239)) && !BarbarianShout.Delayed(CombatAbility.WarCry,owner.Serial.Value,now.AddMilliseconds(240)) && !BarbarianShout.Delayed(CombatAbility.BattleCry,owner.Serial.Value,now),"Six-frame independent delay");BarbarianShout.Restore(wave);
        var target=Enemy(owner,1);Stop(target,true);var oldRoll=SourceBlocking.BlockRoll;var oldDamage=BarbarianWarCry.Roll;
        try
        {
            Shield(target,1);Require(SourceBlocking.TryRate(target,out int chance,out _) && chance==10,"Skeleton shield block chance");SourceBlocking.BlockRoll=_=>0;BarbarianWarCry.Roll=_=>0;before=Life(target);Require(!BarbarianWarCry.Hit(owner,target,Ray(owner)) && Life(target)==before && CombatStuns.Remaining(target)==0,"Blocked damage/stun");
            Shield(target,0);Require(SourceBlocking.TryRate(target,out chance,out _) && chance==0,"Shieldless skeleton should not block");before=Life(target);Require(BarbarianWarCry.Hit(owner,target,Ray(owner)) && before-Life(target)==8192 && CombatStuns.Remaining(target)>0,"Guaranteed spell hit without AR");
            CombatStuns.Clear(target);target.SetResistance(ResistanceType.Physical,100);before=Life(target);Require(BarbarianWarCry.Hit(owner,target,Ray(owner)) && Life(target)==before && CombatStuns.Remaining(target)>0,"Physical immune stun success path");target.SetResistance(ResistanceType.Physical,0);
            var curse=NecromancerEffects.Profiles.Find(owner)??new();NecromancerEffects.Profiles.Store(owner,curse with{Curse=new(CombatAbility.Weaken,owner.Serial.Value,1,TimedEffects.Now.AddMinutes(1),owner.X,owner.Y)});before=Life(target);Require(BarbarianWarCry.Hit(owner,target,Ray(owner)) && before-Life(target)==8192,"Weapon damagepercent leaked into physical spell");NecromancerEffects.Profiles.Store(owner,curse);
            BarbarianWarCry.Roll=_=>1;before=Life(target);Require(BarbarianWarCry.Hit(owner,target,Ray(owner,min:8192,max:8194)) && before-Life(target)==8193,"Per-collision FP256 roll");
            int calls=0;SourceBlocking.ChoiceRoll=_=>{calls++;return 1;};var other=Enemy(owner,2);Require(SourceBlocking.TryRate(other,out chance,out _) && chance==10 && SourceBlocking.TryRate(other,out chance,out _) && calls==1,"Per-actor shield choice rerolled per hit");Delete(other);SourceBlocking.ChoiceRoll=Utility.Random;
            var zombie=new GraveyardZombie{Name="WarCryFixtureZombie",NoKillAwards=true};zombie.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Stop(zombie,true);Require(SourceBlocking.TryRate(zombie,out chance,out _) && chance==0,"Zombie ToBlock without shield");zombie.Delete();
            var unknown=new Ogre();unknown.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Require(!BarbarianWarCry.Hit(owner,unknown,Ray(owner)),"Unmapped target invented");unknown.Delete();
            var native=new EncounterCreature{Name="WarCryFixtureNative",NoKillAwards=true};var profile=EncounterProfiles.Profile("crypt-sentinel-90");native.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));native.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Stop(native,true);
            var oldStun=CombatStuns.Roll;try{Require(SourceBlocking.TryRate(native,out chance,out _) && chance==0 && CombatStuns.Rules(native,out bool boss,out int velocity) && !boss && velocity!=0,"Explicit native encounter block/movement eligibility");CombatStuns.Roll=_=>89;Require(!CombatStuns.Apply(native,25),"Champion should resist low stun roll");CombatStuns.Roll=_=>90;Require(CombatStuns.Apply(native,25),"Champion source ten-percent stun branch");}finally{CombatStuns.Roll=oldStun;native.Delete();}
        }
        finally{SourceBlocking.BlockRoll=oldRoll;BarbarianWarCry.Roll=oldDamage;SourceBlocking.ChoiceRoll=Utility.Random;Delete(target);}
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();Synchronous(owner);var target=Enemy(owner,1);Stop(target,true);Shield(target,0);var far=Enemy(owner,6);Stop(far,true);int hp=Life(target),farHp=Life(far);Cast(owner);
        Later(500,()=>{Require(Life(target)<hp && CombatStuns.Remaining(target)>0 && Life(far)==farHp,"Natural wave damage/stun/range");Later(5200,()=>{Require(CombatStuns.Remaining(target)==0 && !BarbarianShout.Snapshot().Rays.Any(x=>x.Owner==owner.Serial.Value),"Natural lifetime/stun expiry");Seed(owner,target,far);Log.Information("warcry-result: passed");});});
    }
    private static void Seed(PlayerMobile owner,GraveyardSkeleton target,GraveyardSkeleton far)
    {
        CancelRays(owner);var now=TimedEffects.Now;target.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Shield(target,1);
        BarbarianShout.Cast(owner,SkillMechanics.Values(owner,CombatAbility.WarCry,20),now,CombatAbility.WarCry);var ray=BarbarianShout.Snapshot().Rays.First(x=>x.Owner==owner.Serial.Value && x.Ray==0) with{Frame=6,X=owner.X+1.5,Velocity=1704,AccelerationCounter=1,Acceleration=-600,NextUtc=now.AddMinutes(10),UntilUtc=now.AddMinutes(10).AddMilliseconds(640)};
        var wave=BarbarianShout.Snapshot();var delay=new WarcryDelay(CombatAbility.WarCry,target.Serial.Value,now.AddMinutes(10));BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Owner!=owner.Serial.Value).Append(ray).ToArray(),Delays=wave.Delays.Where(x=>x.Ability!=CombatAbility.WarCry || x.Recipient!=target.Serial.Value).Append(delay).ToArray()});
        var expired=Enemy(owner,12);Stop(expired,true);var stun=new CombatStunState(target.Serial.Value,now.AddMinutes(10));CombatStuns.Restore(CombatStuns.Snapshot().Where(x=>x.Target!=target.Serial.Value).Append(stun).Append(new CombatStunState(expired.Serial.Value,now.AddMilliseconds(500))).ToArray());
        NecromancerEffects.SetMana256(owner,NecromancerEffects.ManaBalance256(owner)-17);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,far.Serial.Value,expired.Serial.Value,owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),ray,delay,SourceBlocking.Shield(target),stun)));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);var target=(GraveyardSkeleton)World.FindMobile((Serial)saved.Target);var far=(GraveyardSkeleton)World.FindMobile((Serial)saved.Far);var wave=BarbarianShout.Snapshot();
        Require(wave.Rays.Contains(saved.Ray) && wave.Delays.Contains(saved.Delay) && SourceBlocking.Shield(target)==saved.Shield && CombatStuns.Snapshot().Contains(saved.Stun) && NecromancerEffects.ManaBalance256(owner)==saved.Mana256 && ClassCombat.For(owner).PendingAbility==CombatAbility.None,"Saved snapshot/SH/stun/fractional mana/transient state");
        var expired=(BaseCreature)World.FindMobile((Serial)saved.Expired);Require(CombatStuns.Remaining(expired)==0,"Offline stun expiry");Delete(expired);CombatStuns.Clear(target);
        var now=TimedEffects.Now;Require(BarbarianShout.Delayed(CombatAbility.WarCry,target.Serial.Value,now) && !BarbarianShout.Delayed(CombatAbility.BattleCry,target.Serial.Value,now),"Restored skill-local delay");
        SourceBlocking.BlockRoll=_=>99;int before=Life(target),mana=NecromancerEffects.ManaBalance256(owner);BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(x=>x.Id!=saved.Ray.Id).Append(saved.Ray with{NextUtc=now,UntilUtc=now.AddMilliseconds(640)}).ToArray(),Delays=wave.Delays.Where(x=>x!=saved.Delay).ToArray()});
        Later(700,()=>{SourceBlocking.BlockRoll=Utility.Random;Require(Life(target)<before && CombatStuns.Remaining(target)>0 && NecromancerEffects.ManaBalance256(owner)==mana && !BarbarianShout.Snapshot().Rays.Any(x=>x.Owner==owner.Serial.Value),"Resumed ray damage/stun/no extra debit/lifetime");Seed(owner,target,far);Log.Information("warcry-result: passed");});
    }
}
