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

public static class DecoyComponentChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DecoyComponentChecks));
    private sealed record Saved(uint Owner,uint Decoy,uint Expired,uint Helm,DecoyStats Stats,DateTime Until,Point3D Point,int Health,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"decoy-component-fixtures.json");
    private static bool _creating;
    private static int? _loadedHealth;
    public static void Configure(){Freeze();Register("decoy-component-check",Check);Register("decoy-component-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()
    {
        Freeze();if(File.Exists(Manifest))
        {var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));if(World.FindMobile((Serial)saved.Decoy) is AmazonDecoy pet){_loadedHealth=SkillHealth.Balance256(pet);}}
    }
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name=="DecoyComponentOwner"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="DecoyComponentOwner"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Decoy entity snapshots, health, static behavior and reload",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"decoy-source-probe.json")));int cases=0;
        foreach(var row in json.RootElement.GetProperty("summons").EnumerateArray())
        {
            int level=row[0].GetInt32(),hp=row[1].GetInt32(),rank=row[2].GetInt32();
            Require(AmazonDecoys.Life256(hp,50,rank*10)==row[4].GetInt32() && AmazonDecoys.Level(level,rank)==row[5].GetInt32(),"Compiled Decoy life/base-level differential");
            if(row[4].GetInt32()>=256){Require(AmazonDecoys.Regeneration256(row[4].GetInt32())==row[13].GetInt32(),"Compiled maxHP callback regeneration differential");}cases++;
        }
        Require(cases==960,"Missing compiled Decoy rows");
        foreach(int rank in Enumerable.Range(1,40))
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.Decoy),_=>rank,_=>1,99);
            Require(v.Mana256==Math.Max(256,(76-3*(rank-1))*64) && v.Eval("calc2")==250+125*(rank-1) && v.Eval("aurastatcalc1")==Math.Min(85,rank*4),"Pinned Decoy mana/duration/resistance formulas");
        }
        Log.Information("Decoy entity:960 compiled life/level and40 pinned rank rows passed");
    }
    private static void Train(PlayerMobile owner,string id)
    {if(ClassCreation.Profiles.GetRank(owner.Serial,id)>0){return;}Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var talent),"Talent "+id);foreach(var prerequisite in talent.Prerequisites){Train(owner,prerequisite);}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x191,Female=true,Name="DecoyComponentOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);Train(owner,"amazon.decoy");
        owner.MoveToWorld(new(2200,1000,0),Map.Trammel);bool found=false;
        for(int x=2200;x<2900 && !found;x+=10)for(int y=1000;y<1450 && !found;y+=10)
        {
            int z=owner.Map.GetAverageZ(x,y);var point=new Point3D(x,y,z);owner.MoveToWorld(point,Map.Trammel);
            if(SpellHelper.IsTown(point,owner) || !Enumerable.Range(1,8).All(i=>owner.Map.GetAverageZ(x+i,y)==z && owner.Map.CanSpawnMobile(x+i,y,z))){continue;}
            bool occupied=false;foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,20)){if(other!=owner){occupied=true;break;}}if(!occupied){found=true;}
        }
        Require(found,"No independent Decoy area");owner.Mana=0;return owner;
    }
    private static Point3D Point(PlayerMobile owner,int dx=2)=>new(owner.X+dx,owner.Y,owner.Z);
    private static AmazonDecoy Create(PlayerMobile owner)=>AmazonDecoys.Create(owner,Point(owner,AmazonDecoys.For(owner).Any(x=>x.Location==Point(owner))?4:2),SkillMechanics.Values(owner,CombatAbility.Decoy));
    private static void Cases(PlayerMobile owner)
    {
        Require(!ClassCombat.Available(owner,CombatAbility.Decoy) && !AmazonCombat.Implemented(CombatAbility.Decoy),"Entity component enabled incomplete Decoy cast");
        var pet=Create(owner);Require(pet!=null,"Decoy entity creation");
        try
        {
            var stats=pet.Snapshot;Require(stats.Rank==1 && stats.Level==75 && stats.Defense==591 && stats.Resistance==4 && stats.Regeneration256==126 && stats.DurationFrames==250,"Native source Decoy snapshot");
            Require(SkillHealth.Balance256(pet)==stats.Life256 && SkillHealth.Maximum256(pet)==stats.Life256 && pet.HitsMax==(stats.Life256+255)/256,"Fractional maximum/current life projection");
            SkillHealth.Damage(owner,pet,257);int hurt=SkillHealth.Balance256(pet);Require(hurt==stats.Life256-257,"Fractional injured Decoy");
            pet.Advance(TimedEffects.Now,Core.TickCount+40);Require(SkillHealth.Balance256(pet)==hurt+126,"Source maxHP callback regeneration per frame");
            SkillHealth.Heal(pet,int.MaxValue);Require(SkillHealth.Balance256(pet)==stats.Life256,"Fractional maximum healing cap");
            Require(pet.SummonMaster==owner && pet.GetMaster()==owner && pet.Summoned && !pet.Controlled && !pet.Commandable && pet.CantWalk && pet.ControlSlots==0
                && !pet.CanRegenHits && !pet.CanRegenMana && !pet.IsEnemy(owner) && !pet.CanBeHarmful(owner,false) && pet.Body==0x191,"Stationary summon owner/no-command/no-attack role");
            var position=pet.Location;pet.Combatant=owner;pet.Warmode=true;pet.OnThink();Require(pet.Location==position && pet.Combatant==null && !pet.Warmode,"Decoy acquired action or moved");
            Require(ClassStats.Level(pet)==75 && ClassStats.Defense(pet)==591 && ClassCombat.Friendly(owner,pet),"Shared life/level/defense/allegiance consumers");
            int before=SkillHealth.Balance256(pet);SkillCombat.Hit(owner,pet,10,"fire");Require(SkillHealth.Balance256(pet)==before,"Friendly skill damaged Decoy");
            var enemy=new GraveyardZombie{CantWalk=true,Frozen=true,NoKillAwards=true};enemy.AIObject.AITimer.Stop();enemy.MoveToWorld(Point(owner,6),owner.Map);
            try
            {
                Require(enemy.IsEnemy(pet) && enemy.CanBeHarmful(pet,false),"Native enemy cannot target stationary Decoy");
                Require(ClassStats.CheckHit(enemy,pet,0,()=>0)==true && ClassStats.CheckHit(enemy,pet,0,()=>1)==false,"Actual incoming hit route omitted Decoy source defense/level");
                SkillCombat.Hit(enemy,pet,10,"fire");Require(SkillHealth.Balance256(pet)==before-2457,"Decoy elemental resistance did not mitigate fire");
            }
            finally{enemy.Delete();}
            var blocked=new Static(0x80);try{blocked.MoveToWorld(Point(owner,3),owner.Map);Require(AmazonDecoys.Create(owner,blocked.Location,SkillMechanics.Values(owner,CombatAbility.Decoy))==null && !pet.Deleted,"Failed placement replaced existing Decoy");}finally{blocked.Delete();}
            owner.Hits=Math.Max(1,owner.HitsMax/2);var values=AmazonDecoys.Values(owner,SkillMechanics.Values(owner,CombatAbility.Decoy));Require(values==stats,"Current caster injury changed max-life snapshot");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);try
            {
                Require(owner.EquipItem(helm),"Actual Tarnhelm effective rank");ClassGear.Refresh(owner);Require(pet.Snapshot==stats,"Later item changed an existing Decoy");var next=Create(owner);Require(next!=null && pet.Deleted && AmazonDecoys.For(owner).Single()==next && next.Snapshot.Rank==2 && next.Snapshot.Resistance==8 && next.Snapshot.DurationFrames==375,"One-Decoy replacement/soft-rank snapshot");pet=next;
                owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(pet.Snapshot.Rank==2,"Removing gear changed saved Decoy rank");
            }
            finally{helm.Delete();ClassGear.Refresh(owner);}
            pet.Advance(pet.ExpiresUtc,Core.TickCount);Require(pet.Deleted && AmazonDecoys.For(owner).Length==0,"Decoy deadline did not remove entity");
            Require(NecromancerEffects.ManaBalance256(owner)==0,"Internal component charged mana without a cast");
        }
        finally{pet?.Delete();}
    }
    private static void Persist(PlayerMobile owner)
    {
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Saved Decoy soft rank");ClassGear.Refresh(owner);
        var pet=Create(owner);Require(pet!=null,"Saved entity");owner.AddToBackpack(helm);ClassGear.Refresh(owner);pet.HoldUntil(TimedEffects.Now.AddMinutes(10));SkillHealth.Damage(owner,pet,100*256+257);pet.Pause();
        // Separate caster avoids the one-Decoy replacement cap. Its short saved
        // deadline tests offline expiry; production skill durations are unchanged.
        var other=Fresh();var expired=Create(other);expired.HoldUntil(TimedEffects.Now.AddMilliseconds(100));
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,pet.Serial.Value,expired.Serial.Value,helm.Serial.Value,pet.Snapshot,pet.ExpiresUtc,pet.Location,SkillHealth.Balance256(pet),NecromancerEffects.ManaBalance256(owner))));
    }
    private static void Natural(PlayerMobile owner)
    {
        var pet=Create(owner);Require(pet!=null,"Natural expiry creation");long deadline=Core.TickCount+13000;Point3D start=pet.Location;
        void Observe()
        {
            try
            {
                if(!pet.Deleted && Core.TickCount<deadline){Require(pet.Location==start,"Idle Decoy moved naturally");Timer.StartTimer(TimeSpan.FromMilliseconds(100),Observe);return;}
                Require(pet.Deleted,"Natural ten-second Decoy expiry");Persist(owner);Log.Information("decoy-component-result: passed");
            }
            catch(Exception e){pet.Delete();Log.Error(e,"decoy-component-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(100),Observe);
    }
    private static void Check(){Differential();var owner=Fresh();Cases(owner);Natural(owner);}
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var pet=World.FindMobile((Serial)saved.Decoy) as AmazonDecoy;
        Require(owner!=null && pet!=null && pet.SummonMaster==owner && pet.Snapshot==saved.Stats && pet.ExpiresUtc==saved.Until && pet.Location==saved.Point && pet.Map==Map.Trammel,"Saved Decoy identity/owner/stats/deadline/location");
        // Saved injured Decoy remains in its fixed world position and regenerates
        // normally after load; it never heals during downtime or replays a cast.
        Require(_loadedHealth==saved.Health && SkillHealth.Balance256(pet)>=saved.Health && SkillHealth.Balance256(pet)<=saved.Stats.Life256 && NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved fractional injury/resources");
        var expired=World.FindMobile((Serial)saved.Expired);Require(expired==null || expired.Deleted,"Offline-expired Decoy retained");Require(World.FindItem((Serial)saved.Helm) is Item{Parent:Backpack},"Saved unequipped soft-rank item");
        owner.MoveToWorld(new(saved.Point.X-2,saved.Point.Y,saved.Point.Z),Map.Trammel);pet.Delete();Cases(owner);Natural(owner);
    }
}
