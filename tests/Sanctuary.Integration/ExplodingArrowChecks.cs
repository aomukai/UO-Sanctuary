// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ExplodingArrowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ExplodingArrowChecks));
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);public override int Next(int min,int max)=>Math.Max(min,max-1);}
    private sealed class Minimum:Random{public override int Next(int max)=>0;}
    private sealed record Saved(uint Owner,uint First,uint Second,uint Bow,uint Ammo,Point3D Origin,WeaponFlight Flight,BowExplosion Blast,int FirstHealth,int SecondHealth,int Mana,int Quantity);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"exploding-arrow.json");
    public static void Configure(){Register("exploding-arrow-check",Check);Register("exploding-arrow-reload-check",Reload);Register("exploding-arrow-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Exploding Arrow parent/child fire split, real input, gear and saved state",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()=>MagicArrowChecks.Fresh("ExplodingArrowOwner");
    private static void Ready(PlayerMobile p)=>MagicArrowChecks.Ready(p);
    private static void Mana(PlayerMobile p)=>MagicArrowChecks.Mana(p);
    private static Point3D Aim(PlayerMobile p)=>MagicArrowChecks.Aim(p);
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Talent");
        foreach(string id in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var why),"Train "+why);}
    }
    private static void Differential()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"exploding-arrow-damage-source-probe.json")));int count=0;
        var fields=source.RootElement.GetProperty("creatorFields").EnumerateArray().Select(x=>x.GetString()).ToArray();
        foreach(var row in source.RootElement.GetProperty("creator").EnumerateArray())
        {
            bool child=row[0].GetInt32()!=0;int lo=row[1].GetInt32(),fire=row[2].GetInt32()*256,add=child?row[3].GetInt32():0;
            var actual=BowLaunch.FromSource(lo*256,(lo+5)*256,0,0,128,79,0,0,0,row[4].GetInt32()!=0,new(FireMin:fire+add,FireMax:fire+31*256+add+(child?512:0)));
            int Expected(string key)=>row[5][Array.IndexOf(fields,key)].GetInt32();Require(actual.Min256==Expected("nMinDamage") && actual.Max256==Expected("nMaxDamage") && actual.Percent==Expected("nDamagePercent")
                && actual.Elements.FireMin==Expected("nFireMinDamage") && actual.Elements.FireMax==Expected("nFireMaxDamage"),"Compiled parent/child creator differential");count++;
        }
        Require(count==72,"Incomplete creator fixture");count=0;
        foreach(var row in source.RootElement.GetProperty("getter").EnumerateArray())
        {int fire=WeaponMissileDamage.RollValue(row[0].GetInt32(),row[0].GetInt32()+512,row[1].GetInt32(),_=>0);Require(row[5].GetInt32()==fire && row[6].GetInt32()==0 && row[7].GetInt32()==fire && row[8].GetInt32()==0 && row[9].GetInt32()==0 && row[10].GetInt32()==0,"Compiled fire-only getter/critical/species isolation");count++;}
        Require(count==72,"Incomplete fire getter fixture");Log.Information("Exploding Arrow:72 creator/72 fire-only source/native rows passed;256 parent/4320 child-area callback source rows separate");
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null,bool held=false,bool left=false)
    {
        Ready(p);int before=NecromancerEffects.ManaBalance256(p);var ammo=BowAmmunition.Stack(p,CombatTiming.Equipped(p));int quantity=ammo.Amount;var old=WeaponMissiles.Snapshot().Flights.Select(x=>x.Id).ToHashSet();
        bool start=held?(left?ClassCombat.ContinueAttack(p,CombatAbility.ExplodingArrow,target,target?.Location??Aim(p),Core.TickCount):ClassCombat.ContinueCast(p,CombatAbility.ExplodingArrow,target,target?.Location??Aim(p),Core.TickCount))
            :ClassCombat.Perform(p,CombatAbility.ExplodingArrow,target,target?.Location??Aim(p),Core.TickCount);
        Require(start && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.ExplodingArrow).Mana256 && ammo.Amount==quantity,"Exploding Arrow start payment");var s=ClassCombat.For(p);ClassCombat.ReleaseCast(p);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Single Exploding Arrow event");
        var f=WeaponMissiles.Snapshot().Flights.Single(x=>!old.Contains(x.Id));Require(f.Ability==CombatAbility.ExplodingArrow && f.Missile=="explodingarrow" && f.Frames==40 && f.NextDelay==0 && f.Damage.PhysicalToMagicPercent==0
            && f.Damage.Conversion==null && f.SkillRank==SkillMechanics.Rank(p,CombatAbility.ExplodingArrow) && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.ExplodingArrow).Mana256 && (ammo.Deleted?0:ammo.Amount)==quantity-1,"Captured weapon parent/once quantity/no repeated mana");return f;
    }
    private static void Controlled(Action action)
    {
        var launch=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{AmazonBow.LaunchRandom=launch;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;}
    }
    private static void Finish(long id)
    {for(int i=0;i<250 && WeaponMissiles.State(id) is{} f;i++){WeaponMissiles.Tick(f.NextUtc);}Require(WeaponMissiles.State(id)==null,"Parent projectile did not finish");}
    private static BowLaunchTarget Target(PlayerMobile p,int physical=0,int fire=0)
    {var t=MagicArrowChecks.Target(p);t.SetResistance(ResistanceType.Physical,physical);t.SetResistance(ResistanceType.Fire,fire);return t;}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static BowExplosion[] Blasts(PlayerMobile p)=>BowExplosions.Snapshot().Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static BowExplosion Contact(PlayerMobile p)
    {
        for(int i=0;i<100 && Blasts(p).Length==0 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}
        Require(Blasts(p).Length==1,"Parent did not create one delayed child");return Blasts(p)[0];
    }
    private static BowLaunchTarget Enemy(PlayerMobile p,int dx=5,int dy=0)
    {var t=Target(p);t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);return t;}
    private static void Cases()
    {
        var p=Fresh();try
        {
            var bow=MagicArrowChecks.Bow(p,typeof(Bow));Require(!ClassCombat.Available(p,CombatAbility.ExplodingArrow),"Unlearned exploding arrow available");Train(p,CombatAbility.ExplodingArrow);
            Require(CombatTiming.WeaponAttack(CombatAbility.ExplodingArrow) && CombatTiming.For(p,CombatAbility.ExplodingArrow)==CombatTiming.For(p,CombatAbility.MagicArrow)
                && SkillBindings.Instance.Assign(p,true,CombatAbility.ExplodingArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.ExplodingArrow) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ExplodingArrow),"Bow timing/left/right/quick bindings");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.ExplodingArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"No ammo accepted/paid");
            var bolts=new Bolt(4);p.AddToBackpack(bolts);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.ExplodingArrow,null,Aim(p),Core.TickCount),"Wrong ammo accepted");
            var arrows=new Arrow(30);p.AddToBackpack(arrows);var f=Cast(p);Require(f.Damage.Elements.FireMin==0 && f.Damage.Elements.FireMax==0,"Parent incorrectly added skill fire");WeaponMissiles.Cancel(p);
            Cast(p,held:true);WeaponMissiles.Cancel(p);Cast(p,held:true,left:true);WeaponMissiles.Cancel(p);Require(arrows.Amount==27 && bolts.Amount==4,"Once-only arrow consumption");
            bow=MagicArrowChecks.Bow(p,typeof(Crossbow));Cast(p);WeaponMissiles.Cancel(p);Require(arrows.Amount==27 && bolts.Amount==3,"Crossbow bolt selection");bow=MagicArrowChecks.Bow(p,typeof(Bow));
            Ready(p);NecromancerEffects.SetMana256(p,1279);Require(!ClassCombat.Perform(p,CombatAbility.ExplodingArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==1279,"Insufficient start mana accepted/debited");
            Mana(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);int amount=arrows.Amount;Require(ClassCombat.Perform(p,CombatAbility.ExplodingArrow,null,Aim(p),Core.TickCount),"Paid cancel start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-1280 && arrows.Amount==amount,"Cancelled paid arrow refunded or used ammo");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ExplodingArrow,null,Aim(p),Core.TickCount),"Paid zero-mana release start");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
            Require(ClassCombat.ResolveAction(p,at) && Own(p).Length==1 && arrows.Amount==amount-1,"Paid arrow charged twice");WeaponMissiles.Cancel(p);
            Train(p,CombatAbility.ExplodingArrow,20);var v=SkillMechanics.Values(p,CombatAbility.ExplodingArrow);
            Require(v.Mana256==2496 && v.Damage256(false)==58076 && v.Damage256(true)==65955,"Rank20 source tiers/hard Fire synergy/fractional mana");
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Area()
    {
        var p=Fresh();var random=WeaponMissiles.DamageRoll;try
        {
            Train(p,CombatAbility.ExplodingArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(12));
            foreach(int mode in new[]{0,1,2})
            {
                Mana(p);var first=Enemy(p);var second=Enemy(p,5,1);var outside=Enemy(p,5,3);var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;
                try
                {
                    if(mode==1){WeaponMissiles.HitRoll=()=>.99999;}
                    if(mode==2)
                    {
                        SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=first.Serial.Value).Append(new SourceShield(first.Serial.Value,CreatureProfiles.Definition(first).Reference,1)).ToArray());
                        Require(SourceBlocking.TryRate(first,out int chance,out _) && chance==10,"Actual shield fixture");SourceBlocking.BlockRoll=_=>0;
                    }
                    int hp=SkillHealth.Balance256(first),two=SkillHealth.Balance256(second),far=SkillHealth.Balance256(outside),calls=0;WeaponMissiles.DamageRoll=n=>{calls++;return 0;};Cast(p,first);var blast=Contact(p);
                    int after=SkillHealth.Balance256(first);Require((after<hp)==(mode==0) && SkillHealth.Balance256(second)==two && SkillHealth.Balance256(outside)==far,"Parent damage versus delayed area/miss/block");
                    Require(blast.Min256==1459 && blast.Max256==3793 && blast.NextUtc>TimedEffects.Now.AddMilliseconds(-500),"Hard Fire source fire-only child snapshot");int oldCalls=calls;
                    BowExplosions.Tick(blast.NextUtc.AddTicks(-1));Require(SkillHealth.Balance256(second)==two,"Child hit before its source update");BowExplosions.Tick(blast.NextUtc);
                    Require(calls==oldCalls+1 && SkillHealth.Balance256(first)==after-blast.Min256 && SkillHealth.Balance256(second)==two-blast.Min256 && SkillHealth.Balance256(outside)==far,"One area fire roll/primary overlap/radius/no shield or AR on child");
                }finally{WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);first.Delete();second.Delete();outside.Delete();}
            }
        }finally{WeaponMissiles.DamageRoll=random;WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void LiveGear()
    {
        var p=Fresh();try
        {
            Train(p,CombatAbility.ExplodingArrow);var plain=MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(8));var target=Enemy(p);
            try
            {
                var parent=Cast(p,target);p.AddToBackpack(plain);var hell=NamedGear.Create("unique/65",99,new Maximum(),true);p.AddToBackpack(hell);Require(p.EquipItem(hell),"Actual Hellclap after launch");
                var blast=Contact(p);Require(parent.SkillRank==1 && parent.Damage.Elements.FireMin==0 && blast.Rank==1 && blast.Min256==1459+15*256,"Child lost parent rank or failed live item fire collection");WeaponMissiles.Cancel(p);Mana(p);
                parent=Cast(p,target);Require(parent.SkillRank==2 && parent.Damage.Elements.FireMin==15*256,"Actual parent weapon-only fire");p.AddToBackpack(hell);
                blast=Contact(p);Require(blast.Rank==2 && blast.Min256==3210 && blast.Max256==5836,"Removed weapon erased captured rank or left stale child item fire");WeaponMissiles.Cancel(p);
                Require(p.EquipItem(plain),"Plain bow restore");Mana(p);parent=Cast(p,target);Train(p,CombatAbility.FireArrow,2);blast=Contact(p);
                Require(blast.Min256==1638 && blast.Max256==4259,"Child creation did not read current hard synergy");WeaponMissiles.Cancel(p);
            }finally{target.Delete();}
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void TerrainAndPierce()
    {
        var p=Fresh();try
        {
            Train(p,CombatAbility.ExplodingArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(8));Cast(p);var wall=new Static(0x80);var behind=Enemy(p,4);
            try
            {
                wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);Require(!p.Map.LineOfSight(new Point3D(p.X,p.Y,p.Z+12),new Point3D(behind.X,behind.Y,behind.Z+12)),"Inserted wall fixture");int hp=SkillHealth.Balance256(behind);var blast=Contact(p);
                Require(Own(p).Length==0 && SkillHealth.Balance256(behind)==hp,$"Wall parent crossed geometry or dealt damage: flights={Own(p).Length} hp={SkillHealth.Balance256(behind)}/{hp} center={blast.X},{blast.Y} owner={p.X},{p.Y}");BowExplosions.Tick(blast.NextUtc);Require(SkillHealth.Balance256(behind)==hp-blast.Min256,"Source area incorrectly required LOS");
            }finally{wall.Delete();behind.Delete();WeaponMissiles.Cancel(p);}
            Mana(p);var lifetime=Cast(p);var end=Contact(p);Require(Own(p).Length==0 && Math.Abs(end.X-(p.X+lifetime.Vx*lifetime.Frames))<.01,$"Lifetime explosion position: end={end.X},{end.Y} expected={p.X+lifetime.Vx*lifetime.Frames},{p.Y+lifetime.Vy*lifetime.Frames}");WeaponMissiles.Cancel(p);
            Train(p,CombatAbility.Pierce);Mana(p);var first=Enemy(p,5);var second=Enemy(p,8);try
            {
                var shot=Cast(p);Require(shot.Pierces==4,"Pierce preroll cap");var ids=new System.Collections.Generic.HashSet<long>();
                for(int i=0;i<100 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p)[0].NextUtc);foreach(var b in Blasts(p)){ids.Add(b.Id);}}
                Require(ids.Count==3,"Piercing contacts/lifetime did not create separate children");
            }finally{WeaponMissiles.Cancel(p);first.Delete();second.Delete();}
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void ElementsAndEvents()
    {
        var p=Fresh();var effects=WeaponMissiles.ItemEffects;try
        {
            Train(p,CombatAbility.ExplodingArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(8));
            foreach(string id in new[]{"unique/207","unique/88"})
            {var item=NamedGear.Create(id,99,new Random(71),true);p.AddToBackpack(item);Require(p.EquipItem(item) && ClassGear.Active(p,item),"Real parent event gear");}
            WeaponMissiles.ItemEffects=new Minimum();var first=Enemy(p);var second=Enemy(p,5,1);try
            {
                int hp=SkillHealth.Balance256(second);Cast(p,first);var blast=Contact(p);BowExplosions.Tick(blast.NextUtc);
                Require(TimedEffects.Slow(first,TimedEffects.Now)==20 && !TimedEffects.CanRegenerateHits(first) && TimedEffects.Snapshot().Single(x=>x.Owner==first.Serial.Value).BleedMs>0,"Parent weapon events lost");
                Require(SkillHealth.Balance256(second)==hp-blast.Min256 && TimedEffects.Slow(second,TimedEffects.Now)==0 && TimedEffects.CanRegenerateHits(second)
                    && !TimedEffects.Snapshot().Any(x=>x.Owner==second.Serial.Value && x.BleedMs>0),"Fire-only child inherited physical/attacker item events");
            }finally{WeaponMissiles.Cancel(p);TimedEffects.Clear(first);TimedEffects.Clear(second);first.Delete();second.Delete();}
            foreach(int resist in new[]{50,100})
            {
                Mana(p);first=Enemy(p);second=Enemy(p,5,1);try
                {second.SetResistance(ResistanceType.Fire,resist);int hp=SkillHealth.Balance256(second);Cast(p,first);var blast=Contact(p);BowExplosions.Tick(blast.NextUtc);
                    Require(SkillHealth.Balance256(second)==hp-(resist==100?0:blast.Min256/2),"Individual child fire resistance/immunity");}
                finally{WeaponMissiles.Cancel(p);TimedEffects.Clear(first);TimedEffects.Clear(second);first.Delete();second.Delete();}
            }
        }finally{WeaponMissiles.ItemEffects=effects;WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Hold(PlayerMobile p,WeaponFlight flight,BowExplosion blast)
    {
        var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(flight).ToArray(),
            Explosions=all.Explosions.Where(x=>x.Owner!=p.Serial.Value).Append(blast).ToArray()});
    }
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.ExplodingArrow);var bow=NamedGear.Create("unique/65",99,new Maximum(),true);p.AddToBackpack(bow);Require(p.EquipItem(bow),"Saved Hellclap");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ExplodingArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.ExplodingArrow)
            && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ExplodingArrow),"Saved exploding bindings");
        var ammo=new Arrow(10);p.AddToBackpack(ammo);var first=Enemy(p);Cast(p,first);var blast=Contact(p) with{NextUtc=TimedEffects.Now.AddMinutes(10)};
        first.MoveToWorld(new(p.X+5,p.Y+2,p.Z),p.Map);var second=Enemy(p);var flight=Cast(p,second) with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,flight,blast);
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ExplodingArrow,null,Aim(p),Core.TickCount),"Saved paid/unemitted windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,first.Serial.Value,second.Serial.Value,bow.Serial.Value,ammo.Serial.Value,p.Location,flight,blast,
            SkillHealth.Balance256(first),SkillHealth.Balance256(second),NecromancerEffects.ManaBalance256(p),ammo.Amount)));
        var legacy=JsonSerializer.SerializeToNode(new WeaponFlightSave([],[],0,0));legacy.AsObject().Remove("Explosions");
        Require(legacy.Deserialize<WeaponFlightSave>().Explosions.Length==0,"Prior save missing optional explosions");
    }
    private static void Check()=>Controlled(()=>{Differential();Cases();Area();LiveGear();TerrainAndPierce();ElementsAndEvents();Persist();});
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var first=(BowLaunchTarget)World.FindMobile((Serial)saved.First);var second=(BowLaunchTarget)World.FindMobile((Serial)saved.Second);var ammo=World.FindItem((Serial)saved.Ammo);var bow=World.FindItem((Serial)saved.Bow);
        Require(Own(p).Single()==saved.Flight && Blasts(p).Single()==saved.Blast && SkillHealth.Balance256(first)==saved.FirstHealth && SkillHealth.Balance256(second)==saved.SecondHealth
            && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity && ClassCombat.For(p).PendingAbility==CombatAbility.None
            && SkillBindings.Instance.Get(p)==(CombatAbility.ExplodingArrow,CombatAbility.ExplodingArrow) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,7)==CombatAbility.ExplodingArrow,"Saved parent/child payload/resources/bindings or replayed windup");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);p.AddToBackpack(bow);int parent=NamedElements.To256(saved.Flight.Damage.Roll(second,_=>0).Total);
        Hold(p,saved.Flight with{NextUtc=TimedEffects.Now},saved.Blast);
        for(int i=0;i<100 && Blasts(p).Length<2 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}
        var next=Blasts(p).Single(x=>x.Id!=saved.Blast.Id);Require(next.Rank==2 && next.Min256==3210 && next.Max256==5836,"Resumed parent lost launch rank or read removed item fire");
        BowExplosions.Restore(BowExplosions.Snapshot().Select(x=>x.Owner==p.Serial.Value?x with{NextUtc=TimedEffects.Now}:x).ToArray());BowExplosions.Tick(TimedEffects.Now);
        Require(SkillHealth.Balance256(first)==saved.FirstHealth-saved.Blast.Min256-next.Min256 && SkillHealth.Balance256(second)==saved.SecondHealth-parent-saved.Blast.Min256-next.Min256
            && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity,"Resumed parent/fire-only children or resource repayment");
        var held=saved.Flight with{NextUtc=TimedEffects.Now.AddMinutes(10)};var blast=saved.Blast with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,held,blast);Require(p.EquipItem(bow),"Reload bow");Ready(p);
        Require(ClassCombat.Perform(p,CombatAbility.ExplodingArrow,null,Aim(p),Core.TickCount),"Next paid/unemitted windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Blast=blast,FirstHealth=SkillHealth.Balance256(first),SecondHealth=SkillHealth.Balance256(second),Mana=NecromancerEffects.ManaBalance256(p)}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.ExplodingArrow);MagicArrowChecks.Bow(p,typeof(Bow));Ready(p);var ammo=new Arrow(3);p.AddToBackpack(ammo);var target=Enemy(p);var second=Enemy(p,5,1);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ExplodingArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.ExplodingArrow) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ExplodingArrow),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int before=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target),two=SkillHealth.Balance256(second);bool started=false,emitted=false;long deadline=Core.TickCount+5000;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Clean(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);target.Delete();second.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected Exploding Arrow did not complete");
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.ExplodingArrow)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before-1280 && ammo.Amount==3 && Own(p).Length==0,"Natural start4 phase");}
                var flights=Own(p);if(!emitted && flights.Length>0)
                {emitted=true;Require(started && flights.Length==1 && flights[0].Frame<3 && flights[0].Missile=="explodingarrow" && SkillHealth.Balance256(target)==health && SkillHealth.Balance256(second)==two && ammo.Amount==2,"Natural single-arrow/ammunition phase");}
                if(!emitted || flights.Length>0 || Blasts(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && SkillHealth.Balance256(second)==two-1459 && NecromancerEffects.ManaBalance256(p)==before-1280 && ammo.Amount==2,"Natural parent/child fire and once-only payment");Clean();Log.Information("exploding-arrow-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"exploding-arrow-input-result: failed");}
        }
        var point=Aim(p);Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("exploding-arrow-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.ExplodingArrow,point.X,point.Y,point.Z);Wait();});
    }
}
