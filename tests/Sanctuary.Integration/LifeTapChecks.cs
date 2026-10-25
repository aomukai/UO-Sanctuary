// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary.Integration;

public static class LifeTapChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LifeTapChecks));
    private sealed record Saved(uint Caster,uint Owner,uint Target,int Life,int TargetLife,Point3D Origin,Point3D CasterOrigin,NecromancerCurse Curse,WeaponFlight Flight);
    private sealed class Zero:Random{public override int Next(int maximum)=>0;}
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"life-tap-fixtures.json");
    public static void Configure(){Freeze();Register("life-tap-check",Check);Register("life-tap-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=actor=>_creating || actor.Name is "LifeTapOwner" or "LifeTapTarget"?TimeSpan.FromHours(1):old?.Invoke(actor)??Mobile.DefaultHitsRate;}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Life Tap fractional defender event and persistence",_=>Core.LoopContext.Post(()=>
    {try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
    private static int Life(Mobile actor)=>SkillHealth.Balance256(actor);
    private static void Controlled(Action action)
    {var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;var effects=WeaponMissiles.ItemEffects;
        try{WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;WeaponMissiles.ItemEffects=new Zero();action();}
        finally{WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.ItemEffects=effects;}}
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"life-tap-source-probe.json")));int count=0,accepted=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {int hp=row[1].GetInt32(),maximum=row[2].GetInt32();bool success=row[7].GetInt32()==1;
            int expected=success?Math.Min(maximum,hp+NecromancerEffects.LifeTapHealing256(row[0].GetInt32(),row[3].GetInt32())):hp;
            Require(expected==row[6].GetInt32(),"Compiled Life Tap FP256/cap differential");count++;if(success){accepted++;}}
        Require(count==9360 && accepted==480,"Incomplete source Life Tap fixture");
        foreach(int rank in new[]{1,7,20,40}){Require(new SkillValues(SkillMechanics.For(CombatAbility.LifeTap),_=>rank,_=>rank,99).Eval("calc1")==50,"Pinned Life Tap percent");}
        Log.Information("Life Tap:9360 compiled callback rows;480 accepted fractional/cap cases matched native helper");
    }
    private static PlayerMobile Fresh(DiabloClass characterClass)
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="LifeTapOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,characterClass);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        owner.MoveToWorld(new(2200,1000,0),Map.Trammel);bool found=false;
        for(int x=2200;x<=2500 && !found;x+=50)for(int y=1000;y<=1300 && !found;y+=50)
        {Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}if(SpellHelper.IsTown(point,owner)){continue;}
            bool empty=true;foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,12)){if(other!=owner){empty=false;break;}}if(empty){owner.MoveToWorld(point,owner.Map);found=true;}}
        Require(found,"No independent Life Tap corridor");return owner;
    }
    private static void Train(PlayerMobile owner,string id)
    {if(ClassCreation.Profiles.GetRank(owner.Serial,id)>0){return;}ClassCreation.Profiles.TryGet(owner.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var definition),"Talent "+id);foreach(string prerequisite in definition.Prerequisites){Train(owner,prerequisite);}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    private static BaseCreature Enemy(PlayerMobile owner,int distance=3,bool skeleton=false,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {
        BaseCreature target;_creating=true;try{target=skeleton?new GraveyardSkeleton():new GraveyardZombie();target.Name="LifeTapTarget";}finally{_creating=false;}
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(target,difficulty);}target.Frozen=target.CantWalk=true;target.NoKillAwards=true;target.AIObject.AITimer.Stop();target.MoveToWorld(new(owner.X+distance,owner.Y,owner.Z),owner.Map);target.SetHits(5000);target.Hits=target.HitsMax;target.SetResistance(ResistanceType.Physical,0);target.SetResistance(ResistanceType.Fire,0);return target;
    }
    private static void Cast(PlayerMobile caster,Mobile target)
    {Require(NecromancerEffects.Apply(caster,target,CombatAbility.LifeTap,SkillMechanics.Rank(caster,CombatAbility.LifeTap)),"Actual Life Tap curse application");}
    private static void Reset(PlayerMobile owner)=>SkillHealth.SetBalance256(owner,10*256+1);
    private static long Launch(PlayerMobile owner,int physical,uint flags=2080,int fire=0,int drain=0)
        =>WeaponMissiles.Launch(owner,CombatAbility.BasicAttack,new(20,0,flags,1),new(physical,physical,0,0,0,false,new(FireMin:fire,FireMax:fire,LifeDrain:drain)),WeaponMissiles.NewCast());
    private static void Step(long id){Require(id>0,"Life Tap missile launch");for(int i=0;i<100 && WeaponMissiles.State(id) is{} flight;i++){WeaponMissiles.Tick(flight.NextUtc);}}
    private static void Cases(PlayerMobile caster,PlayerMobile owner)
    {
        foreach(string mode in new[]{"central","outer","overkill","leech","crushing","hell-zero-drain","immune","elemental","miss","blocked","unaffected","replaced","expired"})
        {
            Item boots=null;var target=Enemy(owner,skeleton:mode is "hell-zero-drain" or "blocked",difficulty:mode=="hell-zero-drain"?MonsterDifficulty.Hell:MonsterDifficulty.Normal);try
            {
                Cast(caster,target);Reset(owner);int physical=257;
                if(mode is "overkill" or "crushing"){physical=12800;SkillHealth.SetBalance256(target,257);}
                if(mode=="crushing"){boots=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Goblin Toe").Id,99,new Random(173),true);Require(owner.EquipItem(boots),"Life Tap/CB real gear");ClassGear.Refresh(owner);Require(ClassGear.Bonus(owner,"crush")==25,"Real CB chance");}
                if(mode=="immune"){target.SetResistance(ResistanceType.Physical,100);}
                if(mode=="elemental"){physical=0;}
                if(mode=="unaffected"){NecromancerEffects.ClearOrdinaryCurse(target);}
                if(mode=="replaced"){Require(NecromancerEffects.Apply(caster,target,CombatAbility.AmplifyDamage,1),"Curse replacement");}
                if(mode=="expired"){var state=NecromancerEffects.Profiles.Find(target);NecromancerEffects.Profiles.Store(target,state with{Curse=state.Curse with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}});}
                if(mode=="miss"){WeaponMissiles.HitRoll=()=>1;}
                if(mode=="blocked")
                {SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=target.Serial.Value).Append(new(target.Serial.Value,CreatureProfiles.Definition(target).Reference,1)).ToArray());Require(SourceBlocking.TryRate(target,out int rate,out _) && rate>0,"Life Tap source shield");SourceBlocking.BlockRoll=_=>0;}
                Step(Launch(owner,physical,mode=="outer"?2080u|0x10000:2080u,mode=="elemental"?257:0,mode is "leech" or "crushing"?7:0));
                int healing=mode is "immune" or "elemental" or "miss" or "blocked" or "unaffected" or "replaced" or "expired"?0:mode=="overkill"?6400:mode=="crushing"?6415:mode=="leech"?145:128;
                Require(Life(owner)==10*256+1+healing,"Life Tap projectile event/overkill/leech/difficulty branch "+mode);
                if(mode=="hell-zero-drain"){Require(GemCombat.Drain(target)==0,"Zero-drain Hell fixture");}
            }
            finally{WeaponMissiles.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;boots?.Delete();ClassGear.Refresh(owner);target.Corpse?.Delete();target.Delete();}
        }
        var native=Enemy(owner);try
        {
            Cast(caster,native);Reset(owner);AOS.Damage(native,owner,1,false,50,0,0,0,0,weaponDamage:true);
            Require(Life(owner)==10*256+1+64,"Native fractional prepared Life Tap missing or repeated at post-hit hook");
            Reset(owner);AOS.Damage(native,owner,10,false,100,0,0,0,0,archer:true,weaponDamage:true);Require(Life(owner)==10*256+1+1280,"Native ranged Life Tap");
            Reset(owner);AOS.Damage(native,owner,10,false,100,0,0,0,0,spellDamage:true);Require(Life(owner)==10*256+1,"Spell incorrectly generated Life Tap");
            Reset(owner);SkillCombat.HitParts(owner,native,new(257/256.0,0,0,0,0),false,weapon:true);Require(Life(owner)==10*256+1+128,"Shared pet/skill weapon Life Tap missing or doubled");
            Reset(owner);GearEffects.Strike(owner,native,0,new Zero(),wearWeapon:false,preparedDamage:new(257/256.0,0,0,0,0));Require(Life(owner)==10*256+1+128,"Prepared player melee Life Tap missing or doubled");
            SkillHealth.SetBalance256(owner,owner.HitsMax*256-1);NecromancerEffects.LifeTap(owner,native,257);Require(Life(owner)==owner.HitsMax*256 && SkillHealth.Remainder256(owner)==0,"Fractional recipient maximum cap");
            var pet=Enemy(owner,6);try{SkillHealth.SetBalance256(pet,10*256+1);Require(NecromancerEffects.LifeTap(pet,native,257)==128 && Life(pet)==10*256+129,"Monster recipient Life Tap");}finally{pet.Delete();}
        }
        finally{native.Delete();}
        var rankTarget=Enemy(owner);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);try
        {
            Cast(caster,rankTarget);Require(NecromancerEffects.Curse(rankTarget).Rank==1,"Initial captured curse rank");Require(caster.EquipItem(helm),"Actual soft-rank Tarnhelm");ClassGear.Refresh(caster);Require(SkillMechanics.Rank(caster,CombatAbility.LifeTap)==2 && NecromancerEffects.Curse(rankTarget).Rank==1,"Current gear mutated existing curse rank");
            Cast(caster,rankTarget);Require(NecromancerEffects.Curse(rankTarget).Rank==2 && Math.Abs((NecromancerEffects.Curse(rankTarget).UntilUtc-TimedEffects.Now).TotalSeconds-18.4)<.2,"Soft-rank recast duration");
            caster.AddToBackpack(helm);ClassGear.Refresh(caster);Require(NecromancerEffects.Curse(rankTarget).Rank==2,"Removing soft rank changed existing curse");
        }
        finally{helm.Delete();ClassGear.Refresh(caster);rankTarget.Delete();}
        var paladin=Fresh(DiabloClass.Paladin);var smite=Enemy(paladin,1);try
        {
            Train(paladin,"paladin.smite");var shield=new Buckler();ClassGear.Profiles.Convert(shield,1,GearRarity.Normal,new Random(173));Require(paladin.EquipItem(shield),"Smite active shield");ClassGear.Refresh(paladin);Cast(caster,smite);Reset(paladin);
            var values=SkillMechanics.Values(paladin,CombatAbility.Smite);int physical=PaladinSmite.DamageRange256(paladin,values).Min;var roll=PaladinSmite.Roll;var effects=PaladinSmite.Effects;
            try{PaladinSmite.Roll=_=>0;PaladinSmite.Effects=new Zero();PaladinSmite.Strike(paladin,smite,values);}finally{PaladinSmite.Roll=roll;PaladinSmite.Effects=effects;}
            Require(Life(paladin)==10*256+1+physical/2,"Smite Life Tap while ordinary leech remains excluded");
        }
        finally{smite.Delete();paladin.Delete();}
    }
    private static void Save(PlayerMobile caster,PlayerMobile owner,BaseCreature target)
    {
        Reset(owner);Cast(caster,target);var state=NecromancerEffects.Profiles.Find(target);NecromancerEffects.Profiles.Store(target,state with{Curse=state.Curse with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}});
        long id=Launch(owner,257,2080|0x10000);var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Select(f=>f.Id==id?f with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}:f).ToArray()});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(caster.Serial.Value,owner.Serial.Value,target.Serial.Value,Life(owner),Life(target),owner.Location,caster.Location,NecromancerEffects.Curse(target),WeaponMissiles.State(id))));
    }
    private static void Check()
    {
        Differential();var caster=Fresh(DiabloClass.Necromancer);Train(caster,"necromancer.lifetap");var owner=Fresh(DiabloClass.Amazon);Cases(caster,owner);var expiry=Enemy(owner);Cast(caster,expiry);var curse=NecromancerEffects.Curse(expiry);Require(Math.Abs((curse.UntilUtc-TimedEffects.Now).TotalSeconds-16)<.2,"Natural rank1 Life Tap16seconds");
        Timer.StartTimer(TimeSpan.FromMilliseconds(16200),()=>
        {try{Controlled(()=>{Reset(owner);Require(NecromancerEffects.Curse(expiry)==null && NecromancerEffects.LifeTap(owner,expiry,257)==0 && Life(owner)==10*256+1,"Natural Life Tap expiry still healed");expiry.Delete();Save(caster,owner,Enemy(owner));});Log.Information("life-tap-result: passed");}catch(Exception ex){Log.Error(ex,"life-tap-result: failed");}});
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var caster=World.FindMobile((Serial)saved.Caster) as PlayerMobile;var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var target=World.FindMobile((Serial)saved.Target) as BaseCreature;
        Require(caster!=null && owner!=null && target!=null && Life(owner)==saved.Life && Life(target)==saved.TargetLife && NecromancerEffects.Curse(target)==saved.Curse && WeaponMissiles.State(saved.Flight.Id)==saved.Flight,"Saved curse owner/rank/deadline, fractional resources and outer travelling missile");
        target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();owner.MoveToWorld(saved.Origin,Map.Trammel);var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Select(f=>f.Id==saved.Flight.Id?f with{NextUtc=TimedEffects.Now}:f).ToArray()});Step(saved.Flight.Id);
        Require(Life(owner)==saved.Life+128 && Life(target)==saved.TargetLife-257,"Resumed outer missile Life Tap missing, duplicated or resource snapshot changed");
        caster.MoveToWorld(saved.CasterOrigin,Map.Trammel);Save(caster,owner,target); // Inspection copy/deadline held for the second restart only.
    }
}
