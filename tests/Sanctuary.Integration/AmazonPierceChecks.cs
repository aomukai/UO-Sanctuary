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

public static class AmazonPierceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AmazonPierceChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Amulet,uint Belt,uint First,uint Second,int FirstHealth,int SecondHealth,Point3D Origin,WeaponFlight Flight);
    private sealed class Rolls(params int[] draws):Random
    {public int Calls;public override int Next(int maximum)=>Math.Clamp(draws[Math.Min(Calls++,draws.Length-1)],0,Math.Max(0,maximum-1));}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amazon-pierce-fixtures.json");
    public static void Configure(){Freeze();Register("amazon-pierce-check",Check);Register("amazon-pierce-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {var previous=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=actor=>actor.Name=="AmazonPierceTarget"?TimeSpan.FromHours(1):previous?.Invoke(actor)??Mobile.DefaultHitsRate;}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Pierce chance, live flight eligibility and persistence",_=>Core.LoopContext.Post(()=>
    {try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Health(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    private static void Controlled(Action action)
    {var damage=WeaponMissiles.DamageRoll;var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;
        try{WeaponMissiles.DamageRoll=_=>0;WeaponMissiles.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{WeaponMissiles.DamageRoll=damage;WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;}}
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"amazon-pierce-source-probe.json")));int count=0;
        int[][] patterns=[[99,99,99,99],[0,99,99,99],[0,0,99,99],[0,0,0,99],[0,0,0,0],[22,33,55,86]];
        foreach(var row in data.RootElement.EnumerateArray())
        {var rolls=new Rolls(patterns[row[2].GetInt32()]);int budget=NamedProjectiles.PierceBudget(row[0].GetInt32()+row[1].GetInt32(),rolls);
            Require(budget==row[3].GetInt32() && rolls.Calls==row[4].GetInt32(),"Compiled skill+item pierce/RNG differential");count++;}
        Require(count==144,"Incomplete source pierce fixture");int[] chances=[23,34,42,49,55,59,63,65,69,71,73,75,77,79,80,82,82,83,84,85];
        for(int rank=1;rank<=20;rank++){Require(new SkillValues(SkillMechanics.For(CombatAbility.Pierce),_=>rank,_=>rank,99).Eval("passivecalc1")==chances[rank-1],"Official Pierce rank golden "+rank);}
        Log.Information("Amazon Pierce:144 unchanged source budget/RNG cases and20 official chance goldens passed");
    }
    private static PlayerMobile Fresh()
    {
        var owner=new PlayerMobile{Body=0x190,Name="AmazonPierceFixture",Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);Stats(owner);
        owner.MoveToWorld(new(2200,1000,0),Map.Trammel);bool found=false;
        for(int x=2200;x<=2500 && !found;x+=50)for(int y=1000;y<=1300 && !found;y+=50)
        {Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            if(SpellHelper.IsTown(point,owner)){continue;}bool empty=true;
            foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,12)){if(other!=owner){empty=false;break;}}
            if(empty){owner.MoveToWorld(point,owner.Map);found=true;}}
        Require(found,"No independent Pierce corridor");owner.Hits=owner.HitsMax;owner.Mana=0;return owner;
    }
    private static void Stats(PlayerMobile owner)
    {while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}while(owner.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}}
    private static void Train(PlayerMobile owner,int rank)
    {foreach(var (id,wanted) in new[]{("amazon.criticalstrike",1),("amazon.penetrate",1),("amazon.pierce",rank)})
        {while(ClassCreation.Profiles.GetRank(owner.Serial,id)<wanted){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Cannot train "+id);}}}
    private static Item Named(PlayerMobile owner,string key)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);Require(owner.EquipItem(item),"Cannot equip "+key);ClassGear.Refresh(owner);return item;}
    private static BaseWeapon Equip(PlayerMobile owner,BaseWeapon weapon)
    {if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Weapon equip");ClassGear.Refresh(owner);return weapon;}
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int distance)
    {var target=new GraveyardSkeleton{Name="AmazonPierceTarget",NoKillAwards=true,Frozen=true,CantWalk=true};target.AIObject.AITimer.Stop();target.SetHits(50000);target.SetResistance(ResistanceType.Physical,0);target.MoveToWorld(new(owner.X+distance,owner.Y,owner.Z),owner.Map);return target;}
    private static long Launch(PlayerMobile owner,int? budget=null)=>WeaponMissiles.Launch(owner,CombatAbility.BasicAttack,new(20,0,2080,1),
        new(20*256,20*256,0,0,0,false,new()),WeaponMissiles.NewCast(),budget??NamedProjectiles.Budget(owner,new Rolls(0)));
    private static void Step(long id,int count=4)
    {for(int i=0;i<count;i++){if(WeaponMissiles.State(id) is not{} state){return;}WeaponMissiles.Tick(state.NextUtc);}}
    private static void Hold(long id,DateTime until)
    {var all=WeaponMissiles.Snapshot();var state=WeaponMissiles.State(id);var delta=until-state.NextUtc;
        WeaponMissiles.Restore(all with{Flights=all.Flights.Select(f=>f.Id==id?f with{NextUtc=f.NextUtc+delta}:f).ToArray(),
            Leases=all.Leases.Select(l=>l.Cast==state.Cast?l with{UntilUtc=l.UntilUtc+delta}:l).ToArray()});}
    private static void Save(PlayerMobile owner,Item weapon,Item helm,Item amulet,Item belt,Mobile first,Mobile second)
    {long id=Launch(owner);Require(id>0,"Saved launch rejected");int before=Health(first);Step(id);
        Require(Health(first)==before-20*256 && WeaponMissiles.State(id) is{Pierces:3},"Saved learned Pierce first impact/budget");Hold(id,TimedEffects.Now.AddMinutes(10));
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,amulet.Serial.Value,belt.Serial.Value,
            first.Serial.Value,second.Serial.Value,Health(first),Health(second),owner.Location,WeaponMissiles.State(id))));}
    private static void Check()
    {
        Differential();var owner=Fresh();Require(AmazonCombat.PiercePercent(owner)==0,"Unlearned Pierce");var helm=Named(owner,"Tarnhelm");Require(AmazonCombat.PiercePercent(owner)==0,"Allskills granted unlearned passive");Train(owner,1);
        Require(AmazonCombat.PiercePercent(owner)==34,"Real soft rank2");owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(AmazonCombat.PiercePercent(owner)==23,"Hard rank1");
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Pierce) && !SkillBindings.Instance.Assign(owner,false,CombatAbility.Pierce) && !SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Pierce),"Passive accepted binding");
        var bow=Equip(owner,new Bow());Require(NamedProjectiles.Budget(owner,new Rolls(22))==4 && NamedProjectiles.Budget(owner,new Rolls(23))==0,"Learned bow budget boundary");
        var belt=Named(owner,"Razortail");Require(ClassGear.Bonus(owner,"pierce")==33 && WeaponMissiles.CurrentPierce(owner)==56 && NamedProjectiles.Budget(owner,new Rolls(55))==4 && NamedProjectiles.Budget(owner,new Rolls(56))==0,"Real additive item+skill chance");
        var crossbow=Equip(owner,new Crossbow());Require(NamedProjectiles.Budget(owner,new Rolls(55))==4,"Crossbow passive");crossbow.Delete();var dagger=Equip(owner,new Dagger());var melee=new Rolls(0);
        Require(NamedProjectiles.Budget(owner,melee)==0 && melee.Calls==0 && WeaponMissiles.CurrentPierce(owner)==56,"Melee attack budget or global passive weapon restriction");dagger.Delete();Require(owner.EquipItem(bow),"Bow restore");
        Train(owner,20);Require(AmazonCombat.PiercePercent(owner)==85 && !ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.pierce",out _),"Hard20 cap");Require(owner.EquipItem(helm),"Helm restore");ClassGear.Refresh(owner);Require(AmazonCombat.PiercePercent(owner)==86,"Soft21");
        var amulet=Named(owner,"Highlord's Wrath");Require(AmazonCombat.PiercePercent(owner)==87 && WeaponMissiles.CurrentPierce(owner)==120 && NamedProjectiles.Budget(owner,new Rolls(99))==4,"Soft22/item100-percent budget");
        var first=Enemy(owner,3);var second=Enemy(owner,5);long id=Launch(owner);Step(id);int secondBefore=Health(second);owner.AddToBackpack(bow);
        Require(WeaponMissiles.CurrentPierce(owner)==120,"Bow removal erased global passive");Step(id,250);Require(Health(second)==secondBefore-20*256,"Learned pierced missile lost continuation after weapon switch");Require(owner.EquipItem(bow),"Flight bow restore");
        owner.AddToBackpack(belt);ClassGear.Refresh(owner);id=Launch(owner);Step(id);secondBefore=Health(second);
        ClassCreation.Profiles.TryGet(owner.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(owner,profile);Require(WeaponMissiles.CurrentPierce(owner)==0,"Respec retained current pierce");Step(id);
        Require(Health(second)==secondBefore-20*256 && WeaponMissiles.State(id)==null,"Lost live Pierce used remaining saved budget");
        Stats(owner);Train(owner,20);foreach(var item in new Item[]{bow,helm,amulet,belt}){Require(item.Parent==owner || owner.EquipItem(item),"Saved real gear restore");}ClassGear.Refresh(owner);
        var other=Fresh();Train(other,1);Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Paladin) && AmazonCombat.PiercePercent(other)==0,"Wrong-class Pierce");Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Amazon) && AmazonCombat.PiercePercent(other)==23,"History restore");other.Delete();
        Require(owner.Mana==0,"Passive consumed mana");Save(owner,bow,helm,amulet,belt,first,second);
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner!=null && ClassCreation.Profiles.GetRank(owner.Serial,"amazon.pierce")==20 && AmazonCombat.PiercePercent(owner)==87 && WeaponMissiles.CurrentPierce(owner)==120,"Saved ranks/effective Pierce");
        foreach(uint serial in new[]{saved.Weapon,saved.Helm,saved.Amulet,saved.Belt}){Require(World.FindItem((Serial)serial)?.Parent==owner,"Saved gear ownership");}
        var first=World.FindMobile((Serial)saved.First);var second=World.FindMobile((Serial)saved.Second);Require(Health(first)==saved.FirstHealth && Health(second)==saved.SecondHealth && WeaponMissiles.State(saved.Flight.Id)==saved.Flight,"Saved actual flight/payload/health replay");
        owner.MoveToWorld(saved.Origin,Map.Trammel);Hold(saved.Flight.Id,TimedEffects.Now);Step(saved.Flight.Id,250);
        Require(Health(first)==saved.FirstHealth && Health(second)==saved.SecondHealth-20*256 && WeaponMissiles.State(saved.Flight.Id)==null,"Resumed learned Pierce impact/replay");
        Save(owner,World.FindItem((Serial)saved.Weapon),World.FindItem((Serial)saved.Helm),World.FindItem((Serial)saved.Amulet),World.FindItem((Serial)saved.Belt),first,second);
    }
}
