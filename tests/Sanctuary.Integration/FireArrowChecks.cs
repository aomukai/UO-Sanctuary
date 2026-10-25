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

public static class FireArrowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireArrowChecks));
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);public override int Next(int min,int max)=>Math.Max(min,max-1);}
    private sealed class Minimum:Random{public override int Next(int max)=>0;}
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Ammo,Point3D Origin,WeaponFlight Flight,int Health,int Mana,int Quantity);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-arrow.json");
    public static void Configure(){Register("fire-arrow-check",Check);Register("fire-arrow-reload-check",Reload);Register("fire-arrow-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Fire Arrow source conversion, actual skill/gear/payment and saved projectile",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()=>MagicArrowChecks.Fresh("FireArrowOwner");
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
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"elemental-arrow-source-probe.json")));int count=0;
        foreach(var row in source.RootElement.GetProperty("conversion").EnumerateArray())
        {
            string element=new[]{"fire","cold","mag"}[row[0].GetInt32()];int raw=row[1].GetInt32(),original=row[2].GetInt32(),pct=row[3].GetInt32();
            var packet=new WeaponMissileDamage(raw,raw,0,0,0,false,new(FireMin:original,FireMax:original,ColdMin:original,ColdMax:original,MagicMin:original,MagicMax:original)){Conversion=new(element,pct)};
            var d=packet.Roll(false,false,false,_=>0);Require(NamedElements.To256(d.Physical)==row[4].GetInt32() && NamedElements.To256(d.Fire)==row[5].GetInt32()
                && NamedElements.To256(d.Cold)==row[6].GetInt32() && NamedElements.To256(d.Magic)==row[7].GetInt32(),"Compiled conversion channel differential");count++;
        }
        Require(count==648,"Incomplete elemental conversion fixture");count=0;
        foreach(var row in source.RootElement.GetProperty("impact").EnumerateArray())
        {
            string element=new[]{"fire","cold","mag"}[row[0].GetInt32()];int raw=row[1].GetInt32(),mastery=row[5].GetInt32(),kind=row[6].GetInt32();
            var packet=new WeaponMissileDamage(raw,raw,row[2].GetInt32(),31,47,row[3].GetInt32()!=0,new(FireMin:257,FireMax:513,ColdMin:769,ColdMax:1025,MagicMin:1281,MagicMax:1537),FireMastery:mastery,ColdMastery:mastery,MagicMastery:mastery){Conversion=new(element,row[4].GetInt32())};
            var d=packet.Roll(kind!=0,kind==2,kind==3,_=>0);Require(NamedElements.To256(d.Physical)==row[7].GetInt32() && NamedElements.To256(d.Fire)==row[8].GetInt32()
                && NamedElements.To256(d.Cold)==row[9].GetInt32() && NamedElements.To256(d.Magic)==row[10].GetInt32(),"Compiled complete impact/conversion/mastery differential");count++;
        }
        Require(count==4608,"Incomplete elemental impact fixture");count=0;var fields=source.RootElement.GetProperty("creatorFields").EnumerateArray().Select(x=>x.GetString()).ToArray();
        foreach(var row in source.RootElement.GetProperty("creator").EnumerateArray())
        {
            int lo=row[0].GetInt32(),skill=row[1].GetInt32();int Expected(string key)=>row[4][Array.IndexOf(fields,key)].GetInt32();
            var d=BowLaunch.FromSource(lo*256,(lo+5)*256,0,0,128,row[2].GetInt32()+57,0,0,0,row[3].GetInt32()!=0,new(FireMin:skill+10*256,FireMax:skill+512+31*256));
            Require(d.Min256==Expected("nMinDamage") && d.Max256==Expected("nMaxDamage") && d.Percent==Expected("nDamagePercent")
                && d.Elements.FireMin==Expected("nFireMinDamage") && d.Elements.FireMax==Expected("nFireMaxDamage") && d.Critical==((Expected("nFlags")&2)!=0),"Compiled elemental creator differential");count++;
        }
        Require(count==24,"Incomplete Fire creator fixture");Log.Information("Fire Arrow:648 conversion/4608 impact/24 complete creator source-native cases passed");
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null,bool held=false,bool left=false)
    {
        Ready(p);int before=NecromancerEffects.ManaBalance256(p);var ammo=BowAmmunition.Stack(p,CombatTiming.Equipped(p));int quantity=ammo.Amount;var old=WeaponMissiles.Snapshot().Flights.Select(x=>x.Id).ToHashSet();
        bool start=held?(left?ClassCombat.ContinueAttack(p,CombatAbility.FireArrow,target,target?.Location??Aim(p),Core.TickCount):ClassCombat.ContinueCast(p,CombatAbility.FireArrow,target,target?.Location??Aim(p),Core.TickCount))
            :ClassCombat.Perform(p,CombatAbility.FireArrow,target,target?.Location??Aim(p),Core.TickCount);
        Require(start && NecromancerEffects.ManaBalance256(p)==before-768 && ammo.Amount==quantity,"Fire Arrow start payment");var s=ClassCombat.For(p);ClassCombat.ReleaseCast(p);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Single Fire Arrow event");
        var f=WeaponMissiles.Snapshot().Flights.Single(x=>!old.Contains(x.Id));Require(f.Ability==CombatAbility.FireArrow && f.Missile=="firearrow" && f.Frames==40 && f.NextDelay==0 && f.Damage.PhysicalToMagicPercent==0
            && f.Damage.Conversion==new WeaponElementConversion("fire",Math.Min(100,3+2*(SkillMechanics.Rank(p,CombatAbility.FireArrow)-1))) && NecromancerEffects.ManaBalance256(p)==before-768 && (ammo.Deleted?0:ammo.Amount)==quantity-1,"Captured Fire conversion/once quantity/no repeated mana");return f;
    }
    private static void Controlled(Action action)
    {
        var launch=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{AmazonBow.LaunchRandom=launch;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;}
    }
    private static void Finish(long id)
    {for(int i=0;i<250 && WeaponMissiles.State(id) is{} f;i++){WeaponMissiles.Tick(f.NextUtc);}Require(WeaponMissiles.State(id)==null,"Fire projectile did not finish");}
    private static BowLaunchTarget Target(PlayerMobile p,int physical=0,int fire=0)
    {var t=MagicArrowChecks.Target(p);t.SetResistance(ResistanceType.Physical,physical);t.SetResistance(ResistanceType.Fire,fire);return t;}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Cases()
    {
        var p=Fresh();try
        {
            var bow=MagicArrowChecks.Bow(p,typeof(Bow));Require(!ClassCombat.Available(p,CombatAbility.FireArrow),"Unlearned Fire Arrow available");Train(p,CombatAbility.FireArrow);
            Require(ClassCombat.Available(p,CombatAbility.FireArrow) && CombatTiming.WeaponAttack(CombatAbility.FireArrow)
                && CombatTiming.For(p,CombatAbility.FireArrow)==CombatTiming.For(p,CombatAbility.MagicArrow)
                && SkillBindings.Instance.Assign(p,true,CombatAbility.FireArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.FireArrow)
                && SkillBindings.Instance.AssignQuick(p,5,CombatAbility.FireArrow),"Learned bow timing/bindings");
            var values=SkillMechanics.Values(p,CombatAbility.FireArrow);Require(values.Mana256==768 && values.Damage256(false)==256 && values.Damage256(true)==1024,"Pinned rank1 Fire Arrow");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"No ammo accepted or paid");
            var bolts=new Bolt(4);p.AddToBackpack(bolts);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount),"Bow accepted bolts");
            var arrows=new Arrow(20);p.AddToBackpack(arrows);var f=Cast(p);Require(f.Damage.Elements.FireMin==256 && f.Damage.Elements.FireMax==1024,"Skill fire missing from actual capture");WeaponMissiles.Cancel(p);
            Cast(p,held:true);WeaponMissiles.Cancel(p);Cast(p,held:true,left:true);WeaponMissiles.Cancel(p);Require(arrows.Amount==17 && bolts.Amount==4,"Bow one-arrow consumption");
            bow=MagicArrowChecks.Bow(p,typeof(Crossbow));Cast(p);WeaponMissiles.Cancel(p);Require(arrows.Amount==17 && bolts.Amount==3,"Crossbow one-bolt consumption");bow=MagicArrowChecks.Bow(p,typeof(Bow));
            Ready(p);NecromancerEffects.SetMana256(p,767);Require(!ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==767,"Insufficient mana accepted/debited");
            Mana(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);int quantity=arrows.Amount;
            Require(ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount),"Paid cancel start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-768 && arrows.Amount==quantity,"Cancelled start refunded or used ammo");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount),"Movement start");at=ClassCombat.For(p).EffectAt;var origin=p.Location;
            p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West) && p.Location==origin,"Return step");
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-1536 && arrows.Amount==quantity,"Movement revived/refunded paid shot");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount),"Equipment start");at=ClassCombat.For(p).EffectAt;p.AddToBackpack(bow);Require(p.EquipItem(bow),"Reequip");
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-2304 && arrows.Amount==quantity,"Equipment revived/refunded paid shot");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount),"Recovery start");at=ClassCombat.For(p).EffectAt;
            Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-3072 && arrows.Amount==quantity,"Hit recovery failed paid interruption");ClassCombat.For(p).RecoveryUntil=Core.TickCount;
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount),"Mana-loss start");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
            Require(ClassCombat.ResolveAction(p,at) && Own(p).Length==1 && arrows.Amount==quantity-1 && NecromancerEffects.ManaBalance256(p)==0,"Paid shot charged mana again");WeaponMissiles.Cancel(p);
            Train(p,CombatAbility.FireArrow,20);Mana(p);values=SkillMechanics.Values(p,CombatAbility.FireArrow);
            Require(values.Mana256==768 && values.Damage256(false)==63*256 && values.Damage256(true)==70*256,"Current rank20 fixed mana/damage");
            Train(p,CombatAbility.ExplodingArrow);values=SkillMechanics.Values(p,CombatAbility.FireArrow);
            Require(values.Damage256(false)==18063 && values.Damage256(true)==20070,"Hard Exploding Arrow synergy/FP256");
            var helm=NamedGear.Create("unique/72",99,new Random(17),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual Tarnhelm");
            Require(SkillMechanics.Rank(p,CombatAbility.FireArrow)==21 && SkillMechanics.Rank(p,CombatAbility.ExplodingArrow)==2,"Actual soft ranks");values=SkillMechanics.Values(p,CombatAbility.FireArrow);
            Require(values.Damage256(false)==69*256+69*256*12/100 && values.Damage256(true)==77*256+77*256*12/100,"Soft synergy incorrectly counted");p.AddToBackpack(helm);
            foreach(bool fireImmune in new[]{false,true})
            {
                Mana(p);var target=Target(p,fireImmune?0:100,fireImmune?100:0);try
                {
                    int before=SkillHealth.Balance256(target);f=Cast(p,target);var roll=f.Damage.Roll(target,_=>0);int expected=NamedElements.To256(fireImmune?roll.Physical:roll.Fire);Finish(f.Id);
                    Require(expected>0 && SkillHealth.Balance256(target)==before-expected,"Physical/fire conversion resistance split");
                }finally{target.Delete();}
            }
            // Old optional Magic field remains readable; invalid mixed channels
            // must fail closed instead of silently changing a saved packet.
            Mana(p);f=Cast(p);var all=WeaponMissiles.Snapshot();
            foreach(var invalid in new[]{f.Damage with{PhysicalToMagicPercent=5},f.Damage with{Conversion=new("ltng",5)}})
            {bool rejected=false;try{WeaponMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Id==f.Id?x with{Damage=invalid}:x).ToArray()});}catch(InvalidDataException){rejected=true;}finally{WeaponMissiles.Restore(all);}Require(rejected,"Invalid conversion save accepted");}
            WeaponMissiles.Cancel(p);
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Mastery()
    {
        // Component-only skill context on an actual Sorceress. This grants no
        // foreign talent: it verifies collector order with genuine owner mastery.
        var p=MagicArrowChecks.Fresh("FireArrowOwner",DiabloClass.Sorceress);try
        {
            Train(p,CombatAbility.FireMastery);var bow=NamedGear.Create("unique/65",99,new Maximum(),true);p.AddToBackpack(bow);Require(p.EquipItem(bow) && ClassGear.Active(p,bow),"Real Hellclap");
            var prepared=new SkillValues(SkillMechanics.For(CombatAbility.FireArrow),id=>id==SkillMechanics.For(CombatAbility.FireArrow).SourceKey?20:1,id=>id==SkillMechanics.For(CombatAbility.ExplodingArrow).SourceKey?1:0,99);
            int mastery=NamedElements.MasteryPercent(p,"fire");var d=BowLaunch.Capture(p,CombatAbility.FireArrow,false,preparedValues:prepared);
            Require(mastery>0 && !ClassCombat.Available(p,CombatAbility.FireArrow) && d.FireMastery==0
                && d.Elements.FireMin==AmazonMelee.EnhanceEndpoint(prepared.Damage256(false),mastery)+NamedElements.To256(NamedElements.Enhance(p,15,"fire"))
                && d.Elements.FireMax==AmazonMelee.EnhanceEndpoint(prepared.Damage256(true),mastery)+NamedElements.To256(NamedElements.Enhance(p,50,"fire")),"Separate skill/weapon mastery preparation or double enhancement");
        }finally{p.Delete();}
    }
    private static void Hold(PlayerMobile p,WeaponFlight f)
    {var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(f).ToArray()});}
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.FireArrow,20);Train(p,CombatAbility.ExplodingArrow);var bow=NamedGear.Create("unique/65",99,new Random(53),true);p.AddToBackpack(bow);Require(p.EquipItem(bow),"Saved Hellclap");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FireArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.FireArrow) && SkillBindings.Instance.AssignQuick(p,5,CombatAbility.FireArrow),"Saved bindings");
        var ammo=new Arrow(10);p.AddToBackpack(ammo);var target=Target(p);var flight=Cast(p,target) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(p,flight);
        Require(SkillHealth.Balance256(target)==50000*256,"Instant Fire damage");Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount),"Paid/unemitted save windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,ammo.Serial.Value,p.Location,flight,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammo.Amount)));
    }
    private static void Check()=>Controlled(()=>{Differential();Cases();Mastery();Persist();});
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);var ammo=World.FindItem((Serial)saved.Ammo);
        Require(Own(p).Single()==saved.Flight && SkillHealth.Balance256(target)==saved.Health && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity
            && ClassCombat.For(p).PendingAbility==CombatAbility.None && SkillBindings.Instance.Get(p)==(CombatAbility.FireArrow,CombatAbility.FireArrow)
            && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,5)==CombatAbility.FireArrow && SkillMechanics.Rank(p,CombatAbility.FireArrow)==21,"Saved Fire payload/paid resources/ranks/bindings or replayed windup");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);var bow=World.FindItem((Serial)saved.Bow);p.AddToBackpack(bow);
        int expected=NamedElements.To256(saved.Flight.Damage.Roll(target,_=>0).Total);Hold(p,saved.Flight with{NextUtc=TimedEffects.Now});Finish(saved.Flight.Id);
        Require(SkillHealth.Balance256(target)==saved.Health-expected && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity,"Resumed Fire payload or resource repayment");
        var held=saved.Flight with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(p,held);Require(p.EquipItem(bow),"Reload bow re-equip");Ready(p);
        Require(ClassCombat.Perform(p,CombatAbility.FireArrow,null,Aim(p),Core.TickCount),"Next paid/unemitted save windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Health=SkillHealth.Balance256(target),Mana=NecromancerEffects.ManaBalance256(p)}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.FireArrow);MagicArrowChecks.Bow(p,typeof(Bow));Ready(p);var ammo=new Arrow(3);p.AddToBackpack(ammo);var target=Target(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FireArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.FireArrow) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.FireArrow),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int before=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target);bool started=false,emitted=false;long deadline=Core.TickCount+5000;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Clean(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);target.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected fan did not complete");
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.FireArrow)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before-768 && ammo.Amount==3 && Own(p).Length==0,"Natural start4 phase");}
                var fan=Own(p);if(!emitted && fan.Length>0)
                {emitted=true;Require(started && fan.Length==1 && fan.All(x=>x.Frame<3 && x.Missile=="firearrow") && SkillHealth.Balance256(target)==health && ammo.Amount==2,"Natural do8/ammunition phase");}
                if(!emitted || fan.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-768 && ammo.Amount==2,"Natural travelling fan/resource accounting");Clean();Log.Information("fire-arrow-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"fire-arrow-input-result: failed");}
        }
        var point=Aim(p);Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("fire-arrow-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.FireArrow,point.X,point.Y,point.Z);Wait();});
    }
}
