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
using Server.Spells;

namespace Sanctuary.Integration;

public static class StrafeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(StrafeChecks));
    private sealed class Minimum:Random{public override int Next(int max)=>0;public override int Next(int min,int max)=>min;}
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Ring,uint Ammo,Point3D Origin,WeaponFlight Flight,WeaponFlightLease Lease,int Health,int Mana,int Quantity);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"strafe.json");
    public static void Configure(){Register("strafe-check",Check);Register("strafe-reload-check",Reload);Register("strafe-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Strafe resource, rollback, target, flight, input and reload inspection",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Talent");foreach(string id in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var why),"Train "+why);}}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value && x.Ability==CombatAbility.Strafe).ToArray();
    private static void Ready(PlayerMobile p){AmazonStrafe.Cancel(p);MagicArrowChecks.Ready(p);MagicArrowChecks.Mana(p);}
    private static void Clean(PlayerMobile p){AmazonStrafe.Cancel(p);ClassCombat.Stop(p);WeaponMissiles.Cancel(p);}
    private static BowLaunchTarget Enemy(PlayerMobile p,int x=5,int y=0)
    {var target=MagicArrowChecks.Target(p);target.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);return target;}
    private static void Controlled(Action action)
    {var random=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;var items=WeaponMissiles.ItemEffects;try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;WeaponMissiles.ItemEffects=new Minimum();action();}finally{AmazonBow.LaunchRandom=random;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.ItemEffects=items;}}
    private static void Begin(PlayerMobile p,Mobile target=null)
    {Ready(p);var ammo=BowAmmunition.Stack(p,CombatTiming.Equipped(p));int quantity=ammo.Amount,mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.Strafe,target,target?.Location??MagicArrowChecks.Aim(p),Core.TickCount),"Strafe start");ClassCombat.ReleaseCast(p);Require((ammo.Deleted?0:ammo.Amount)==quantity-1 && NecromancerEffects.ManaBalance256(p)==mana-2816 && Own(p).Length==0,"Once-only start ammunition then mana");}
    private static WeaponFlight Emit(PlayerMobile p)
    {var s=ClassCombat.For(p);long at=s.Strafe.EffectAt;var before=Own(p).Select(x=>x.Id).ToHashSet();Require(!ClassCombat.ResolveAction(p,at-1) && ClassCombat.ResolveAction(p,at) && !ClassCombat.ResolveAction(p,at),"Single scheduled event/no duplicate burst");return Own(p).Single(x=>!before.Contains(x.Id));}
    private static void Finish(PlayerMobile p)
    {int guard=20;while(ClassCombat.For(p).Strafe is{Finished:false,Remaining:>0} && guard-->0){Emit(p);}Require(guard>0,"Nonterminating sequence");if(ClassCombat.For(p).Strafe is{} series){ClassCombat.ResolveAction(p,series.Until);}Require(ClassCombat.For(p).Strafe==null && ClassCombat.For(p).PendingAbility==CombatAbility.None,"Final recovery did not finish");}
    private static PlayerMobile Fresh()
    {
        var p=MagicArrowChecks.Fresh("StrafeOwner");bool found=false;
        for(int x=2500;x<3300 && !found;x+=4)for(int y=1004;y<1800 && !found;y+=4)
        {
            int z=p.Map.GetAverageZ(x,y);bool clear=true;
            for(int dx=0;dx<14 && clear;dx++)for(int dy=-3;dy<=3 && clear;dy++)
            {var at=new Point3D(x+dx,y+dy,z);clear=p.Map.GetAverageZ(at.X,at.Y)==z && p.Map.CanFit(at,16,false,false,true) && !SpellHelper.IsTown(at,p)
                && p.Map.LineOfSight(new Point3D(x+5,y,z+12),new Point3D(at.X,at.Y,z+12));}
            if(!clear){continue;}foreach(var other in p.Map.GetMobilesInRange<Mobile>(new Point3D(x,y,z),20)){if(other!=p){clear=false;break;}}
            if(!clear){continue;}p.MoveToWorld(new(x,y,z),p.Map);found=true;
        }
        Require(found,"No open two-dimensional Strafe fixture");return p;
    }
    private static void Differential()
    {
        var row=SkillMechanics.For(CombatAbility.Strafe);
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"strafe-source-probe.json"))))
        {int count=0;foreach(var r in doc.RootElement.GetProperty("start").EnumerateArray()){if(r[0].GetInt32()==0 || r[1].GetInt32()==0 || r[2].GetInt32()==0){continue;}int rank=r[5].GetInt32();var values=new SkillValues(row,k=>k==row.SourceKey?rank:0,_=>0,99);Require(AmazonStrafe.Count(values,r[4].GetInt32(),r[3].GetInt32()==0)==r[7].GetInt32(),"Complete source shot count differential");count++;}Require(count==56,"Source count coverage");}
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"strafe-timing-source-probe.json"))))
        {int count=0;foreach(var r in doc.RootElement.EnumerateArray()){var family=r[0].GetInt32()==1?AttackFamily.Bow:AttackFamily.Crossbow;int rate=r[1].GetInt32();foreach(int hits in new[]{4,5}){var original=r[hits==5?4:5].EnumerateArray().Select(x=>x.GetInt32()).ToArray();var actual=AmazonStrafe.Schedule(family,rate,hits);int at=0;for(int i=0;i<hits;i++){at+=original[i];Require(actual.Events[i]==at,"Original rollback event differential");}Require(actual.End==original.Take(hits-1).Sum()+original[^1],"Original last cycle differential");}count++;}Require(count==12,"Timing coverage");}
        Log.Information("Strafe:56 source count and24 original rollback schedule comparisons passed;6144 wrapper/432 do/4224 mode source cases have separate boundaries");
    }
    private static void ResourcesAndTargets()
    {
        foreach(Type type in new[]{typeof(Bow),typeof(HeavyCrossbow)})
        {
            var p=Fresh();Train(p,CombatAbility.Strafe);MagicArrowChecks.Bow(p,type);var target=Enemy(p);var ammo=type==typeof(Bow)?(Item)new Arrow(1):new Bolt(1);p.AddToBackpack(ammo);Ready(p);
            var values=SkillMechanics.Values(p,CombatAbility.Strafe);Require(values.Mana256==2816 && values.Eval("calc2")==20 && values.Formula("toht")==30,"Rank1 resource/ED/AR goldens");int before=NecromancerEffects.ManaBalance256(p);NecromancerEffects.SetMana256(p,2815);Require(!ClassCombat.Perform(p,CombatAbility.Strafe,target,target.Location,Core.TickCount) && ammo.Amount==1 && NecromancerEffects.ManaBalance256(p)==2815,"Low mana consumed ammunition");NecromancerEffects.SetMana256(p,before);Begin(p,target);
            Require(ammo.Deleted && !ClassCombatHooks.MovementAllowed(p) && !HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Last ammunition/start movement/recovery lock");ClassCombat.Stop(p);Require(ClassCombat.For(p).Strafe!=null,"Stop cancelled paid burst");NecromancerEffects.SetMana256(p,0);var first=Emit(p);var second=Emit(p);Require(first.Cast==second.Cast && first.NextDelay==4 && first.Frames==50 && first.Flags==0x21 && first.Damage.SkillAttackPercent==30 && first.Damage.Conversion==null && second.Damage==first.Damage && NecromancerEffects.ManaBalance256(p)==0,"Captured full-source missiles or extra per-shot resources");
            Finish(p);Require(ClassCombatHooks.MovementAllowed(p),"Final movement recovery");Clean(p);target.Delete();p.Delete();
        }
        var caster=Fresh();Train(caster,CombatAbility.Strafe,20);MagicArrowChecks.Bow(caster,typeof(Bow));caster.AddToBackpack(new Arrow(20));var targets=Enumerable.Range(0,12).Select(i=>Enemy(caster,5+i%4,i/4-1)).ToArray();Begin(caster,targets[4]);Require(ClassCombat.For(caster).Strafe.Remaining==10 && SkillMechanics.Values(caster,CombatAbility.Strafe).Eval("calc2")==115,"Rank20 cap/ED");var firstTarget=ClassCombat.For(caster).Strafe.Target;Emit(caster);Require(firstTarget==targets[4].Serial && ClassCombat.For(caster).Strafe.Target==AmazonStrafe.Target(caster,firstTarget,true).Serial,"ID target succession");var removed=World.FindMobile(ClassCombat.For(caster).Strafe.Target);removed.Delete();Emit(caster);Finish(caster);Require(Own(caster).Length==10,"Rank20 emitted count");Clean(caster);
        Begin(caster);Require(ClassCombat.For(caster).Strafe.Remaining==7,"Ground minimum source convention");Finish(caster);Require(Own(caster).Length==7,"Ground series count");Clean(caster);foreach(var t in targets)t.Delete();
        Begin(caster);var empty=ClassCombat.For(caster).Strafe;Require(ClassCombat.ResolveAction(caster,empty.EffectAt) && Own(caster).Length==0 && ClassCombat.For(caster).Strafe.Remaining==7,"Empty do altered source count or emitted a replacement");Finish(caster);Clean(caster);caster.Delete();
        var p2=Fresh();Train(p2,CombatAbility.Strafe);var bow=MagicArrowChecks.Bow(p2,typeof(Bow));p2.AddToBackpack(new Arrow(4));var enemy=Enemy(p2);Begin(p2,enemy);int paid=NecromancerEffects.ManaBalance256(p2);p2.AddToBackpack(bow);Require(!ClassCombat.ResolveAction(p2,Core.TickCount+1000) && Own(p2).Length==0 && NecromancerEffects.ManaBalance256(p2)==paid && BowAmmunition.Stack(p2,bow)==null,"Weapon-loss windup refund/replay");Clean(p2);enemy.Delete();p2.Delete();
    }
    private static void NextHit()
    {
        var p=Fresh();Train(p,CombatAbility.Strafe);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(3));var target=Enemy(p);Begin(p,target);var first=Emit(p);int before=SkillHealth.Balance256(target);var state=WeaponMissiles.Snapshot();var at=TimedEffects.Now;
        var a=first with{X=target.X,Y=target.Y,Vx=0,Vy=0,NextUtc=at};WeaponMissiles.Restore(state with{Flights=state.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(a).ToArray()});WeaponMissiles.Tick(at);int hit=SkillHealth.Balance256(target);Require(hit<before && Own(p).Length==0 && WeaponMissiles.Snapshot().Leases.Any(x=>x.Cast==a.Cast && x.RetainWithoutFlight),"NextHit lease vanished in the empty-flight gap");
        var second=Emit(p);Finish(p);state=WeaponMissiles.Snapshot();var b=second with{X=target.X,Y=target.Y,Vx=0,Vy=0,NextUtc=at.AddMilliseconds(120)};WeaponMissiles.Restore(state with{Flights=state.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(b).ToArray()});WeaponMissiles.Tick(at.AddMilliseconds(120));Require(SkillHealth.Balance256(target)==hit,"Four-frame gate failed between individual arrows");WeaponMissiles.Tick(at.AddMilliseconds(160));Require(SkillHealth.Balance256(target)<hit,"Four-frame gate did not expire");Clean(p);target.Delete();p.Delete();
    }
    private static void LiveGear()
    {
        var p=Fresh();Train(p,CombatAbility.Strafe);MagicArrowChecks.Bow(p,typeof(Bow));var definition=NamedGear.Catalog.Items.Single(x=>x.Name=="The Stone of Jordan");var ring=NamedGear.Create(definition.Id,99,new Minimum(),true);p.AddToBackpack(ring);Require(p.EquipItem(ring),"Real soft-rank item");p.AddToBackpack(new Arrow(3));var target=Enemy(p);Begin(p,target);var first=Emit(p);var baseline=BowLaunch.Capture(p,CombatAbility.MagicArrow,false);Require(first.SkillRank==2 && first.Damage.SkillAttackPercent==39 && first.Damage.Percent-baseline.Percent==25,"Soft rank or hard-only ED synergy");p.AddToBackpack(ring);Train(p,CombatAbility.GuidedArrow,2);var second=Emit(p);Require(second.SkillRank==1 && second.Damage.SkillAttackPercent==30 && second.Damage.Percent==first.Damage.Percent+5 && Own(p).Single(x=>x.Id==first.Id)==first,"Each-shot live hard/soft rank or earlier immutable packet");Finish(p);Clean(p);target.Delete();p.Delete();
    }
    private static void AttackRating()
    {
        var p=Fresh();Train(p,CombatAbility.Strafe);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(3));var target=Enemy(p);Begin(p,target);Emit(p);Emit(p);Finish(p);
        int defense=NamedCombat.DefenseFor(p,target,ClassStats.Defense(target),true);double normal=0,enhanced=0;for(int dex=1;dex<=100 && enhanced<=normal;dex++){p.RawDex=dex;normal=ClassStats.HitChance(ClassStats.AttackRatingAgainst(p,target),defense,ClassStats.Level(p),ClassStats.Level(target));enhanced=ClassStats.HitChance(ClassStats.AttackRatingAgainst(p,target,30),defense,ClassStats.Level(p),ClassStats.Level(target));}Require(enhanced>normal,$"AR fixture has no measurable skill bonus: defense={defense} normal={normal} enhanced={enhanced}");var roll=WeaponMissiles.HitRoll;WeaponMissiles.HitRoll=()=>(normal+enhanced)/2;
        try{var state=WeaponMissiles.Snapshot();var own=Own(p);var at=TimedEffects.Now;var control=own[0] with{X=target.X,Y=target.Y,Vx=0,Vy=0,NextUtc=at,Damage=own[0].Damage with{SkillAttackPercent=0}};var actual=own[1] with{X=target.X,Y=target.Y,Vx=0,Vy=0,NextUtc=at.AddMilliseconds(200)};WeaponMissiles.Restore(state with{Flights=state.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(new[]{control,actual}).ToArray()});int before=SkillHealth.Balance256(target);WeaponMissiles.Tick(at);Require(SkillHealth.Balance256(target)==before,"No-bonus control unexpectedly hit");WeaponMissiles.Tick(at.AddMilliseconds(200));Require(SkillHealth.Balance256(target)<before,"Strafe AR bonus did not reach the actual missile hit check");}finally{WeaponMissiles.HitRoll=roll;Clean(p);target.Delete();p.Delete();}
    }
    private static void Hold(PlayerMobile p,WeaponFlight f,WeaponFlightLease lease)
    {var state=WeaponMissiles.Snapshot();WeaponMissiles.Restore(state with{Flights=state.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(f).ToArray(),Leases=state.Leases.Where(x=>x.Cast!=f.Cast).Append(lease).ToArray()});}
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.Strafe);var bow=MagicArrowChecks.Bow(p,typeof(Bow));var definition=NamedGear.Catalog.Items.Single(x=>x.Name=="The Stone of Jordan");var ring=NamedGear.Create(definition.Id,99,new Minimum(),true);p.AddToBackpack(ring);Require(p.EquipItem(ring),"Real Stone of Jordan");var ammo=new Arrow(20);p.AddToBackpack(ammo);var target=Enemy(p);Begin(p,target);Emit(p);Emit(p);Finish(p);Require(Own(p).All(x=>x.SkillRank==2 && x.Damage.SkillAttackPercent==39),"Real soft-rank launch snapshot");
        int hp=SkillHealth.Balance256(target);for(int i=0;i<60 && SkillHealth.Balance256(target)==hp;i++){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}Require(Own(p).Length==1,"Saved first-hit/next-hit fixture");var f=Own(p).Single();var lease=WeaponMissiles.Snapshot().Leases.Single(x=>x.Cast==f.Cast && x.Target==target.Serial.Value);var until=TimedEffects.Now.AddMinutes(10);f=f with{NextUtc=until};lease=lease with{UntilUtc=until.AddMilliseconds(160)};Hold(p,f,lease);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.Strafe,target,target.Location,Core.TickCount),"Paid unemitted sequence fixture");Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Strafe) && SkillBindings.Instance.Assign(p,false,CombatAbility.Strafe) && SkillBindings.Instance.AssignQuick(p,10,CombatAbility.Strafe),"Saved bindings");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,ring.Serial.Value,ammo.Serial.Value,p.Location,f,lease,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammo.Amount)));
    }
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);var bow=World.FindItem((Serial)saved.Bow);var ring=World.FindItem((Serial)saved.Ring);var ammo=World.FindItem((Serial)saved.Ammo);
        Require(Own(p).Single()==saved.Flight && WeaponMissiles.Snapshot().Leases.Contains(saved.Lease) && ClassCombat.For(p).Strafe==null && ClassCombat.For(p).PendingAbility==CombatAbility.None && ammo.Amount==saved.Quantity && NecromancerEffects.ManaBalance256(p)==saved.Mana && SkillHealth.Balance256(target)==saved.Health && SkillBindings.Instance.Get(p)==(CombatAbility.Strafe,CombatAbility.Strafe) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,10)==CombatAbility.Strafe,"Saved flight/lease/paid windup/resources/bindings");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);p.AddToBackpack(bow);p.AddToBackpack(ring);var resumed=saved.Flight with{NextUtc=TimedEffects.Now};var gate=saved.Lease with{UntilUtc=resumed.NextUtc.AddMilliseconds(160)};Hold(p,resumed,gate);WeaponMissiles.Tick(resumed.NextUtc);Require(SkillHealth.Balance256(target)==saved.Health && Own(p).Single().Damage==saved.Flight.Damage,"Resumed next-hit gate/captured gear payload");target.MoveToWorld(MagicArrowChecks.Aim(p,9),p.Map);for(int i=0;i<40 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}Require(SkillHealth.Balance256(target)<saved.Health && ammo.Amount==saved.Quantity && NecromancerEffects.ManaBalance256(p)==saved.Mana, $"Resumed flight AR/resources after gear removal: hp={SkillHealth.Balance256(target)}/{saved.Health} mana={NecromancerEffects.ManaBalance256(p)}/{saved.Mana} quantity={ammo.Amount}/{saved.Quantity}");target.MoveToWorld(MagicArrowChecks.Aim(p,5),p.Map);
        Require(p.EquipItem(bow) && p.EquipItem(ring),"Restore actual gear");var until=TimedEffects.Now.AddMinutes(10);var held=saved.Flight with{NextUtc=until};var lease=saved.Lease with{UntilUtc=until.AddMilliseconds(160)};Hold(p,held,lease);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.Strafe,target,target.Location,Core.TickCount),"Next paid unemitted sequence");File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Lease=lease,Mana=NecromancerEffects.ManaBalance256(p),Quantity=ammo.Amount,Health=SkillHealth.Balance256(target)}));
    });
    private static void Check()=>Controlled(()=>{Differential();ResourcesAndTargets();NextHit();AttackRating();LiveGear();Persist();});
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.Strafe);MagicArrowChecks.Bow(p,typeof(Bow));var ammo=new Arrow(3);p.AddToBackpack(ammo);var target=Enemy(p);Ready(p);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Strafe) && SkillBindings.Instance.Assign(p,false,CombatAbility.Strafe) && SkillBindings.Instance.AssignQuick(p,10,CombatAbility.Strafe),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target);bool started=false,emitted=false;long deadline=Core.TickCount+6000;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void FinishInput(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;Clean(p);target.Delete();p.Delete();}
        void Wait()
        {try{Require(Core.TickCount<deadline,"Connected Strafe did not complete");if(!started && ClassCombat.For(p).Strafe!=null){started=true;Require(ammo.Amount==2 && NecromancerEffects.ManaBalance256(p)==mana-2816 && Own(p).Length==0,"Connected start quantity/mana phases");}if(Own(p).Length>0){emitted=true;}if(!started || !emitted || ClassCombat.For(p).Strafe!=null || Own(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);return;}Require(ammo.Amount==2 && NecromancerEffects.ManaBalance256(p)==mana-2816 && SkillHealth.Balance256(target)<health,"Natural Strafe resources/hit/final recovery");FinishInput();Log.Information("strafe-input-result: passed");}catch(Exception e){FinishInput();Log.Error(e,"strafe-input-result: failed");}}
        var point=MagicArrowChecks.Aim(p);Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("strafe-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.Strafe,point.X,point.Y,point.Z);Wait();});
    }
}
