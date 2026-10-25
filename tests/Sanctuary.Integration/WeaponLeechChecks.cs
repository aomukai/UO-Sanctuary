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

public static class WeaponLeechChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WeaponLeechChecks));
    private sealed record Saved(uint Owner,uint Bow,uint Ring,uint Belt,uint First,uint Second,int Life,int Mana,int FirstLife,int SecondLife,int FirstMana,int SecondMana,Point3D Origin,WeaponFlight Flight,WeaponFlightLease[] Leases);
    private sealed class Maximum:Random
    {public override int Next(int maximum)=>Math.Max(0,maximum-1);public override int Next(int minimum,int maximum)=>maximum-1;public override double NextDouble()=>.999999;}
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"weapon-leech-fixtures.json");
    public static void Configure(){Freeze();Register("weapon-leech-check",Check);Register("weapon-leech-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hits=Mobile.HitsRegenRateHandler;var mana=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>(_creating && actor is PlayerMobile || actor.Name is "WeaponLeechOwner" or "WeaponLeechTarget")?TimeSpan.FromHours(1):hits?.Invoke(actor)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=actor=>(_creating && actor is PlayerMobile || actor.Name is "WeaponLeechOwner" or "WeaponLeechTarget")?TimeSpan.FromHours(1):mana?.Invoke(actor)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Captured fractional weapon leech and persistence",_=>Core.LoopContext.Post(()=>
    {try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Life(Mobile actor)=>SkillHealth.Balance256(actor);
    private static int Mana(Mobile actor)=>NecromancerEffects.ManaBalance256(actor);
    private static void Controlled(Action action)
    {var damage=WeaponMissiles.DamageRoll;var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;
        try{WeaponMissiles.DamageRoll=_=>0;WeaponMissiles.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{WeaponMissiles.DamageRoll=damage;WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;}}
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"weapon-leech-source-probe.json")));int count=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {var result=WeaponLeech.Calculate(row[0].GetInt32(),row[1].GetInt32(),1337,row[2].GetInt32(),row[3].GetInt32(),row[4].GetInt32(),row[5].GetInt32(),row[6].GetInt32());
            Require(result==new WeaponLeechResult(row[7].GetInt32(),row[8].GetInt32(),row[9].GetInt32()),"Compiled leech/cap/victim-mana differential");count++;}
        Require(count==7680,"Incomplete source fixture");Log.Information("Weapon leech:7680 compiled source/native FP256 cases passed");
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;bool previous=_creating;_creating=true;
        try{owner=new PlayerMobile{Body=0x190,Name="WeaponLeechOwner",Player=true};}finally{_creating=previous;}
        owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}while(owner.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        owner.MoveToWorld(new(2200,1000,0),Map.Trammel);bool found=false;
        for(int x=2200;x<=2500 && !found;x+=50)for(int y=1000;y<=1300 && !found;y+=50)
        {Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}if(SpellHelper.IsTown(point,owner)){continue;}
            bool empty=true;foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,12)){if(other!=owner){empty=false;break;}}if(empty){owner.MoveToWorld(point,owner.Map);found=true;}}
        Require(found,"No independent leech corridor");Resources(owner);return owner;
    }
    private static void Resources(PlayerMobile owner){SkillHealth.SetBalance256(owner,20*256+128);NecromancerEffects.SetMana256(owner,10*256+64);}
    private static BaseCreature Enemy(PlayerMobile owner,int distance=3,MonsterDifficulty difficulty=MonsterDifficulty.Normal,bool skeleton=false)
    {
        BaseCreature target=skeleton?new GraveyardSkeleton():new GraveyardZombie();
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(target,difficulty);}
        target.Name="WeaponLeechTarget";target.NoKillAwards=true;target.Frozen=true;target.CantWalk=true;target.AIObject.AITimer.Stop();
        target.MoveToWorld(new(owner.X+distance,owner.Y,owner.Z),owner.Map);target.SetHits(5000);target.Hits=target.HitsMax;target.SetMana(100);NecromancerEffects.SetMana256(target,100*256);
        target.SetResistance(ResistanceType.Physical,0);target.SetResistance(ResistanceType.Fire,0);Require(ClassCombat.IsHostile(owner,target),"Nonhostile leech fixture");return target;
    }
    private static WeaponMissileDamage Payload(MissileSourceStats elements=null,bool critical=false)=>new(12864,12864,0,0,0,critical,
        elements??new(FireMin:2880,FireMax:2880,LifeDrain:7,ManaDrain:5));
    private static long Launch(PlayerMobile owner,WeaponMissileDamage damage=null,int budget=0,uint flags=2080)=>WeaponMissiles.Launch(owner,CombatAbility.BasicAttack,new(20,0,flags,1),damage??Payload(),WeaponMissiles.NewCast(),budget);
    private static void Step(long id,int count=250)
    {for(int i=0;i<count;i++){if(WeaponMissiles.State(id) is not{} state){return;}WeaponMissiles.Tick(state.NextUtc);}}
    private static Item Named(PlayerMobile owner,string key)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Maximum(),true);Require(owner.EquipItem(item),"Real leech gear "+key);ClassGear.Refresh(owner);return item;}
    private static void Delete(BaseCreature target){target.Corpse?.Delete();target.Delete();}
    private static void Delivery()
    {
        var owner=Fresh();try
        {
            foreach(var (difficulty,drain,life,mana) in new[]{(MonsterDifficulty.Normal,100,900,643),(MonsterDifficulty.Nightmare,75,337,241),(MonsterDifficulty.Hell,50,149,106)})
            {
                var target=Enemy(owner,difficulty:difficulty);try
                {Require(GemCombat.Drain(target)==drain,"Pinned zombie drain variant");Resources(owner);int h=Life(owner),m=Mana(owner),victim=Life(target),mp=Mana(target);long id=Launch(owner);Step(id);
                    int divisor=difficulty==MonsterDifficulty.Normal?1:difficulty==MonsterDifficulty.Nightmare?2:3;
                    Require(Life(owner)==h+life && Mana(owner)==m+mana && Life(target)==victim-15744 && Mana(target)==mp-320/divisor,"Fractional physical-only leech/difficulty/victim-mana delivery");}
                finally{Delete(target);}
            }
            foreach(uint flags in new uint[]{2080,2080|0x10000})
            {var target=Enemy(owner);try{Resources(owner);int h=Life(owner),m=Mana(owner);Step(Launch(owner,flags:flags));Require(Life(owner)==h+900 && Mana(owner)==m+643,"Outer-arrow event suppression incorrectly suppressed leech");}finally{Delete(target);}}
            var converted=Enemy(owner);try
            {
                Resources(owner);int h=Life(owner),m=Mana(owner),victim=Life(converted);
                Step(Launch(owner,Payload() with{PhysicalToMagicPercent=50}));
                Require(Life(owner)==h+450 && Mana(owner)==m+321 && Life(converted)==victim-15744,
                    "Converted magic contributed to physical-only life/mana leech or changed total raw damage");
            }finally{Delete(converted);}
            var emptyMana=Enemy(owner);try
            {NecromancerEffects.SetMana256(emptyMana,0);Resources(owner);int h=Life(owner),m=Mana(owner);Step(Launch(owner));Require(Life(owner)==h+900 && Mana(owner)==m+643 && Mana(emptyMana)==0,"Mana recovery incorrectly capped to victim mana");}finally{Delete(emptyMana);}
            var cutoff=Enemy(owner);try
            {NecromancerEffects.SetMana256(cutoff,575);Resources(owner);Step(Launch(owner));Require(Mana(cutoff)==0,"Legacy victim mana below-one cutoff");}finally{Delete(cutoff);}
            var noDrain=Enemy(owner,skeleton:true);try
            {Require(GemCombat.Drain(noDrain)==0,"Zero-drain source species");Resources(owner);int h=Life(owner),m=Mana(owner),mp=Mana(noDrain);Step(Launch(owner));Require(Life(owner)==h && Mana(owner)==m && Mana(noDrain)==mp-5,"Zero drain must suppress recovery and preserve literal unshifted victim-mana field");}finally{Delete(noDrain);}
            var immune=Enemy(owner);try
            {immune.SetResistance(ResistanceType.Physical,100);Resources(owner);int h=Life(owner),m=Mana(owner),mp=Mana(immune);Step(Launch(owner));Require(Life(owner)==h && Mana(owner)==m && Mana(immune)==mp-320,"Legacy zero-physical recovery/victim-mana distinction");}finally{Delete(immune);}
            var resistant=Enemy(owner);try
            {Resources(owner);long id=Launch(owner);resistant.SetResistance(ResistanceType.Physical,50);Step(id);Require(Life(owner)==20*256+128+450 && Mana(owner)==10*256+64+321,"Current impact physical resistance did not control leech");}finally{Delete(resistant);}
            var critical=Enemy(owner);try
            {Resources(owner);int h=Life(owner),m=Mana(owner);Step(Launch(owner,Payload(critical:true)));Require(Life(owner)==h+1800 && Mana(owner)==m+1286,"Critical physical must reach leech once");}finally{Delete(critical);}
            var overkill=Enemy(owner);try
            {SkillHealth.SetBalance256(overkill,257);Resources(owner);int h=Life(owner),m=Mana(owner);Step(Launch(owner));Require(Life(owner)==h+17 && Mana(owner)==m+12,"Overkill leech ignored current fractional victim life cap");}finally{Delete(overkill);}
            var capped=Enemy(owner);try
            {SkillHealth.SetBalance256(owner,owner.HitsMax*256-1);NecromancerEffects.SetMana256(owner,owner.ManaMax*256-1);Step(Launch(owner));Require(Life(owner)==owner.HitsMax*256 && Mana(owner)==owner.ManaMax*256 && SkillHealth.Remainder256(owner)==0,"Fractional native resource caps");}finally{Delete(capped);}
            var miss=Enemy(owner);try
            {Resources(owner);WeaponMissiles.HitRoll=()=>1;int h=Life(owner),m=Mana(owner),mp=Mana(miss),hp=Life(miss);Step(Launch(owner));Require(Life(owner)==h && Mana(owner)==m && Mana(miss)==mp && Life(miss)==hp,"Miss delivered leech or damage");}finally{WeaponMissiles.HitRoll=()=>0;Delete(miss);}
            var blocked=Enemy(owner,skeleton:true);try
            {SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=blocked.Serial.Value).Append(new(blocked.Serial.Value,CreatureProfiles.Definition(blocked).Reference,1)).ToArray());
                Require(SourceBlocking.TryRate(blocked,out int rate,out _) && rate>0,"Real source shield block");SourceBlocking.BlockRoll=_=>0;Resources(owner);int h=Life(owner),m=Mana(owner),mp=Mana(blocked),hp=Life(blocked);Step(Launch(owner));Require(Life(owner)==h && Mana(owner)==m && Mana(blocked)==mp && Life(blocked)==hp,"Blocked missile delivered leech");}
            finally{SourceBlocking.BlockRoll=_=>99;Delete(blocked);}
        }
        finally{WeaponMissiles.Cancel(owner);owner.Delete();}
    }
    private static void Hold(WeaponFlight flight,WeaponFlightLease[] leases,DateTime until,bool add=false)
    {var all=WeaponMissiles.Snapshot();var delta=until-flight.NextUtc;var held=flight with{NextUtc=until};
        WeaponMissiles.Restore(all with{Flights=add?all.Flights.Where(f=>f.Id!=flight.Id).Append(held).ToArray():all.Flights.Select(f=>f.Id==flight.Id?held:f).ToArray(),
            Leases=all.Leases.Where(l=>l.Cast!=flight.Cast).Concat(leases.Select(l=>l with{UntilUtc=l.UntilUtc+delta})).ToArray()});}
    private static void Write(PlayerMobile owner,Item bow,Item ring,Item belt,Mobile first,Mobile second,WeaponFlight flight)
    {var leases=WeaponMissiles.Snapshot().Leases.Where(l=>l.Cast==flight.Cast).ToArray();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,bow.Serial.Value,ring.Serial.Value,belt.Serial.Value,
        first.Serial.Value,second.Serial.Value,Life(owner),Mana(owner),Life(first),Life(second),Mana(first),Mana(second),owner.Location,flight,leases)));}
    private static void Check()
    {
        Differential();Delivery();var owner=Fresh();var bow=Named(owner,"Riphook");var ring=Named(owner,"Manald Heal");var belt=Named(owner,"Razortail");
        int life=ClassGear.Bonus(owner,"life-steal%"),mana=ClassGear.Bonus(owner,"mana-steal%");Require(life==10 && mana==7,"Maximum rolled real leech properties");
        var saved=new MissileSourceStats(FireMin:3840,FireMax:3840,LifeDrain:life,ManaDrain:mana).Scale(96);Require(saved.LifeDrain==7 && saved.ManaDrain==5,"SrcDam drain percent truncation");
        var first=Enemy(owner,3);var second=Enemy(owner,5);Resources(owner);long id=Launch(owner,Payload(saved),2);Step(id,4);
        Require(WeaponMissiles.State(id) is{Pierces:1} && Life(owner)==20*256+128+900 && Mana(owner)==10*256+64+643,"First captured leech impact");
        owner.AddToBackpack(bow);owner.AddToBackpack(ring);ClassGear.Refresh(owner);Require(ClassGear.Bonus(owner,"life-steal%")==0 && ClassGear.Bonus(owner,"mana-steal%")==0,"Live gear leech not removed");
        var flight=WeaponMissiles.State(id);var leases=WeaponMissiles.Snapshot().Leases.Where(l=>l.Cast==flight.Cast).ToArray();Hold(flight,leases,TimedEffects.Now.Add(FixtureSpace.Retention));Write(owner,bow,ring,belt,first,second,WeaponMissiles.State(id));
        Require(Launch(owner,Payload(saved with{LifeDrain=-1}))==0,"Negative drain accepted");Require(Launch(owner,Payload(saved with{BurnMin=1}))==0,"Unimplemented burn accepted");
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var first=World.FindMobile((Serial)saved.First);var second=World.FindMobile((Serial)saved.Second);
        Require(owner!=null && Life(owner)==saved.Life && Mana(owner)==saved.Mana && Life(first)==saved.FirstLife && Life(second)==saved.SecondLife && Mana(first)==saved.FirstMana && Mana(second)==saved.SecondMana,$"Saved fractional resources/offline replay: owner HP {Life(owner)}/{saved.Life}, MP {Mana(owner)}/{saved.Mana}; first HP {Life(first)}/{saved.FirstLife}, MP {Mana(first)}/{saved.FirstMana}; second HP {Life(second)}/{saved.SecondLife}, MP {Mana(second)}/{saved.SecondMana}");
        Require(World.FindItem((Serial)saved.Bow)?.Parent==owner.Backpack && World.FindItem((Serial)saved.Ring)?.Parent==owner.Backpack && World.FindItem((Serial)saved.Belt)?.Parent==owner
            && ClassGear.Bonus(owner,"life-steal%")==0 && ClassGear.Bonus(owner,"mana-steal%")==0,"Saved equipment/live leech values");
        Require(WeaponMissiles.State(saved.Flight.Id)==saved.Flight && saved.Flight.Damage.Elements is{LifeDrain:7,ManaDrain:5},"Actual deserialized captured leech payload");
        owner.MoveToWorld(saved.Origin,Map.Trammel);Hold(saved.Flight,saved.Leases,TimedEffects.Now);Step(saved.Flight.Id);
        Require(Life(owner)==saved.Life+900 && Mana(owner)==saved.Mana+643 && Life(first)==saved.FirstLife && Mana(first)==saved.FirstMana
            && Life(second)==saved.SecondLife-15744 && Mana(second)==saved.SecondMana-320,"Resumed captured leech used current gear or replayed first impact");
        Hold(saved.Flight,saved.Leases,TimedEffects.Now.Add(FixtureSpace.Retention),true);Write(owner,World.FindItem((Serial)saved.Bow),World.FindItem((Serial)saved.Ring),World.FindItem((Serial)saved.Belt),first,second,WeaponMissiles.State(saved.Flight.Id));
    }
}
