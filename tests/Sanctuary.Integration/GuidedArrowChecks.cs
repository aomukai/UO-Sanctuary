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

public static class GuidedArrowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GuidedArrowChecks));
    private sealed class Minimum:Random{public int Calls;public override int Next(int max){Calls++;return 0;}}
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Ammo,Point3D Origin,WeaponFlight[] Flights,int Health,int Mana,int Quantity);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"guided-arrow.json");
    public static void Configure(){Register("guided-arrow-check",Check);Register("guided-arrow-reload-check",Reload);Register("guided-arrow-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Guided Arrow source modes, collision, input and saved flight",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {var p=MagicArrowChecks.Fresh("GuidedArrowOwner");while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}return p;}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Talent");
        foreach(string id in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var why),"Train "+why);}
    }
    private static void Controlled(Action action)
    {
        var random=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Guided Arrow attempted an AR roll");WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{AmazonBow.LaunchRandom=random;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;}
    }
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static void Hold(PlayerMobile p,params WeaponFlight[] flights)
    {var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(flights).ToArray()});}
    private static WeaponFlight Step(long id,int count=1)
    {for(int i=0;i<count;i++){var f=WeaponMissiles.State(id);Require(f!=null,"Flight removed before expected step");WeaponMissiles.Tick(f.NextUtc);}return WeaponMissiles.State(id);}
    private static void Finish(long id)
    {for(int i=0;i<260 && WeaponMissiles.State(id) is{} f;i++){WeaponMissiles.Tick(f.NextUtc);}Require(WeaponMissiles.State(id)==null,"Guided flight did not finish");}
    private static BowLaunchTarget Enemy(PlayerMobile p,int x,int y=0)
    {var t=MagicArrowChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);return t;}
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null,Point3D? aim=null,bool held=false,bool left=false)
    {
        MagicArrowChecks.Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var ammo=BowAmmunition.Stack(p,CombatTiming.Equipped(p));int quantity=ammo.Amount;
        var old=Own(p).Select(x=>x.Id).ToHashSet();var point=target?.Location??aim??MagicArrowChecks.Aim(p);
        bool start=held?(left?ClassCombat.ContinueAttack(p,CombatAbility.GuidedArrow,target,point,Core.TickCount):ClassCombat.ContinueCast(p,CombatAbility.GuidedArrow,target,point,Core.TickCount))
            :ClassCombat.Perform(p,CombatAbility.GuidedArrow,target,point,Core.TickCount);
        Require(start && NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.GuidedArrow).Mana256 && ammo.Amount==quantity,"Guided start payment");
        var s=ClassCombat.For(p);ClassCombat.ReleaseCast(p);Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Once-only bow event");
        var f=Own(p).Single(x=>!old.Contains(x.Id));Require(f.Missile=="guidedarrow" && f.Guided.Mode==(target==null?2:1) && f.Guided.Target==(target?.Serial.Value??0)
            && f.Frames==Math.Max(1,f.Guided.Remaining) && f.Guided.VelocityFp8==4608 && f.Flags==(target==null?0x420u:0x20u) && f.Pierces==0 && f.NextDelay==0
            && f.Damage.Conversion==null && f.Damage.PhysicalToMagicPercent==0 && !f.Damage.FreezeColdLength && (ammo.Deleted?0:ammo.Amount)==quantity-1
            && NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.GuidedArrow).Mana256,"Captured Guided mode/packet/quantity/resources");return f;
    }
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"guided-arrow-source-probe.json")));var root=doc.RootElement;
        int count=0;foreach(var r in root.GetProperty("launch").EnumerateArray())
        {
            Require(r[6].GetInt32()==1 && r[7].GetInt32()==(r[0].GetInt32()!=0?1:2) && r[9].GetInt32()==(r[0].GetInt32()!=0?128:GuidedArrows.InitialFrames(r[1].GetInt32(),r[2].GetInt32(),4608))
                && r[8].GetInt32()==unchecked((int)((uint)(ushort)r[1].GetInt32()+((uint)(ushort)r[2].GetInt32()<<16))) && r[10].GetInt32()==128 && r[11].GetInt32()==4608 && r[12].GetInt32()==r[3].GetInt32() && r[13].GetInt32()==0,"Launch/factory differential");count++;
        }Require(count==360,"Launch rows");count=0;
        foreach(var r in root.GetProperty("steer").EnumerateArray())
        {Require(GuidedArrows.Turn(r[0].GetInt32(),r[1].GetInt32(),r[2].GetInt32(),r[3].GetInt32()==1,r[4].GetInt32())==(r[6].GetInt32()!=0),"Remaining-frame steering differential");count++;}Require(count==2016,"Steer rows");count=0;
        foreach(var r in root.GetProperty("hit").EnumerateArray())
        {Require(GuidedArrows.Hit(r[0].GetInt32(),r[1].GetInt32(),r[2].GetInt32(),r[3].GetInt32())==r[5].GetInt32(),"Hit result differential");count++;}Require(count==288,"Hit rows");
        Log.Information("Guided Arrow:360 launch/factory,2016 steering and288 hit-state source/native rows passed; world path geometry remains a separate adapter");
    }
    private static void Resources()
    {
        var p=Fresh();try
        {
            var bow=MagicArrowChecks.Bow(p,typeof(Bow));Require(!ClassCombat.Available(p,CombatAbility.GuidedArrow),"Unlearned Guided");Train(p,CombatAbility.GuidedArrow);
            Require(CombatTiming.WeaponAttack(CombatAbility.GuidedArrow) && CombatTiming.For(p,CombatAbility.GuidedArrow)==CombatTiming.For(p,CombatAbility.MagicArrow)
                && SkillBindings.Instance.Assign(p,true,CombatAbility.GuidedArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.GuidedArrow) && SkillBindings.Instance.AssignQuick(p,9,CombatAbility.GuidedArrow),"A1 timing/bindings");
            var v=SkillMechanics.Values(p,CombatAbility.GuidedArrow);Require(v.Mana256==2048 && v.Eval("calc1")==12,"Rank1 hard Multi ED/mana");
            MagicArrowChecks.Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.GuidedArrow,null,MagicArrowChecks.Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"No ammo accepted/paid");
            var bolts=new Bolt(4);p.AddToBackpack(bolts);MagicArrowChecks.Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.GuidedArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Wrong ammo accepted");var arrows=new Arrow(60);p.AddToBackpack(arrows);
            Cast(p);WeaponMissiles.Cancel(p);Cast(p,held:true);WeaponMissiles.Cancel(p);Cast(p,held:true,left:true);WeaponMissiles.Cancel(p);Require(arrows.Amount==57 && bolts.Amount==4,"Once-only arrows");
            MagicArrowChecks.Bow(p,typeof(Crossbow));Cast(p);WeaponMissiles.Cancel(p);Require(arrows.Amount==57 && bolts.Amount==3,"Crossbow bolt");MagicArrowChecks.Bow(p,typeof(Bow));
            MagicArrowChecks.Ready(p);NecromancerEffects.SetMana256(p,2047);Require(!ClassCombat.Perform(p,CombatAbility.GuidedArrow,null,MagicArrowChecks.Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==2047,"Insufficient mana accepted/debited");
            MagicArrowChecks.Mana(p);MagicArrowChecks.Ready(p);mana=NecromancerEffects.ManaBalance256(p);int quantity=arrows.Amount;Require(ClassCombat.Perform(p,CombatAbility.GuidedArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at) && arrows.Amount==quantity && NecromancerEffects.ManaBalance256(p)==mana-2048,"Cancel refunded/used quantity");
            MagicArrowChecks.Ready(p);Require(ClassCombat.Perform(p,CombatAbility.GuidedArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid move start");at=ClassCombat.For(p).EffectAt;var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West) && p.Location==origin,"Return step");Require(!ClassCombat.ResolveAction(p,at) && arrows.Amount==quantity && NecromancerEffects.ManaBalance256(p)==mana-4096,"Moved shot revived/refunded");
            MagicArrowChecks.Ready(p);Require(ClassCombat.Perform(p,CombatAbility.GuidedArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid zero-mana release");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(ClassCombat.ResolveAction(p,at) && Own(p).Length==1 && arrows.Amount==quantity-1,"Second release debit");WeaponMissiles.Cancel(p);
            Train(p,CombatAbility.GuidedArrow,20);Train(p,CombatAbility.MultipleShot,20);Train(p,CombatAbility.Pierce,20);MagicArrowChecks.Mana(p);v=SkillMechanics.Values(p,CombatAbility.GuidedArrow);Require(v.Mana256==832 && v.Eval("calc1")==373 && SkillMechanics.Values(p,CombatAbility.GuidedArrow,29).Mana256==256 && SkillMechanics.Values(p,CombatAbility.GuidedArrow,40).Mana256==256,"Rank20 hard synergy/minimum mana");
            var actual=NamedGear.Create("unique/282",99,new Random(17),true);p.AddToBackpack(actual);p.AddToBackpack(CombatTiming.Equipped(p));Require(p.EquipItem(actual),"Actual Lycander");
            v=SkillMechanics.Values(p,CombatAbility.GuidedArrow);Require(v.Rank==24 && v.Mana256==576 && v.Eval("calc1")==401,"Soft rank/hard-only Multi synergy");
            var counter=new Minimum();var previous=AmazonBow.LaunchRandom;AmazonBow.LaunchRandom=counter;try{var f=Cast(p);Require(counter.Calls==1 && f.Pierces==0 && f.Damage.Critical && WeaponMissiles.CurrentPierce(p)>0,"No Pierce RNG/budget or lost critical");}finally{AmazonBow.LaunchRandom=previous;WeaponMissiles.Cancel(p);}
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void WorldCases()
    {
        var p=Fresh();Train(p,CombatAbility.GuidedArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(30));
        try
        {
            var target=Enemy(p,8);var other=Enemy(p,4);try
            {int hp=SkillHealth.Balance256(target),otherHp=SkillHealth.Balance256(other);var f=Cast(p,target);int damage=NamedElements.To256(f.Damage.Roll(target,_=>0).Total);Finish(f.Id);Require(SkillHealth.Balance256(target)==hp-damage && SkillHealth.Balance256(other)==otherHp,"Locked arrow interception/AR/once damage");}
            finally{target.Delete();other.Delete();}
            MagicArrowChecks.Mana(p);target=Enemy(p,9);try
            {var f=Cast(p,target);f=Step(f.Id,3);Require(f.Guided.Remaining==125 && f.Vy==0,"Initial remaining cadence");target.MoveToWorld(new(p.X+9,p.Y+3,p.Z),p.Map);f=Step(f.Id);Require(f!=null && f.Vy>0,"125-frame target steering");int hp=SkillHealth.Balance256(target);Finish(f.Id);Require(SkillHealth.Balance256(target)<hp,"Moving-target flight");}
            finally{target.Delete();}
            MagicArrowChecks.Mana(p);target=Enemy(p,9);other=Enemy(p,6);try
            {int hp=SkillHealth.Balance256(other);var f=Cast(p,target);target.Delete();Finish(f.Id);Require(SkillHealth.Balance256(other)==hp,"Dead selected target retargeted");}finally{target.Delete();other.Delete();}
            MagicArrowChecks.Mana(p);target=Enemy(p,9,1);other=Enemy(p,6);try
            {Require(target.Serial.Value<other.Serial.Value,"ID fixture order");int hp=SkillHealth.Balance256(target),otherHp=SkillHealth.Balance256(other);var f=Cast(p,aim:MagicArrowChecks.Aim(p,6));int initial=f.Guided.Remaining;f=Step(f.Id,initial);Require(f.Guided is{Mode:5,Remaining:128} && f.Guided.Target==target.Serial.Value && SkillHealth.Balance256(other)==otherHp && SkillHealth.Balance256(target)==hp,"Delayed lowest-ID search/ignored ground collision");Finish(f.Id);Require(SkillHealth.Balance256(target)<hp && SkillHealth.Balance256(other)==otherHp,"Acquired target/interceptor");}
            finally{target.Delete();other.Delete();}
            MagicArrowChecks.Mana(p);target=Enemy(p,13,1);other=Enemy(p,13);try
            {var f=Cast(p,aim:MagicArrowChecks.Aim(p,6));f=Step(f.Id,f.Guided.Remaining);Require(f.Guided.Target==other.Serial.Value,"Search rounded missile source center or radius boundary");WeaponMissiles.Cancel(p);}
            finally{target.Delete();other.Delete();}
            MagicArrowChecks.Mana(p);var ground=Cast(p,aim:MagicArrowChecks.Aim(p,6));ground=Step(ground.Id,ground.Guided.Remaining);Require(ground.Guided is{Mode:6,Remaining:128,Target:0},"Unacquired extension");target=Enemy(p,8);try
            {int hp=SkillHealth.Balance256(target);Finish(ground.Id);Require(SkillHealth.Balance256(target)<hp,"Mode6 ordinary collision");}finally{target.Delete();}
            MagicArrowChecks.Mana(p);ground=Cast(p,aim:MagicArrowChecks.Aim(p,6));ground=Step(ground.Id,ground.Guided.Remaining);ground=ground with{Guided=ground.Guided with{Remaining=1},Frames=ground.Frame+1};Hold(p,ground);Require(Step(ground.Id)==null,"Mode6 searched/extended twice");
            MagicArrowChecks.Mana(p);target=Enemy(p,9);var wall=new Static(0x80);try
            {var f=Cast(p,target);int hp=SkillHealth.Balance256(target);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);Finish(f.Id);Require(SkillHealth.Balance256(target)==hp,"Guided passed hard wall");}finally{wall.Delete();target.Delete();}
            MagicArrowChecks.Mana(p);target=Enemy(p,9);wall=new Static(0x80);try
            {var f=Cast(p,aim:MagicArrowChecks.Aim(p,6));f=Step(f.Id,6);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);Require(!p.InLOS(target),"Owner LOS fixture");f=Step(f.Id,f.Guided.Remaining);Require(f.Guided.Mode==6,"Search used missile LOS instead of owner LOS");WeaponMissiles.Cancel(p);}
            finally{wall.Delete();target.Delete();}
            MagicArrowChecks.Mana(p);target=Enemy(p,5);var block=SourceBlocking.BlockRoll;try
            {SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=target.Serial.Value).Append(new SourceShield(target.Serial.Value,CreatureProfiles.Definition(target).Reference,1)).ToArray());
                Require(SourceBlocking.TryRate(target,out int chance,out _) && chance==10,"Actual mapped skeleton shield fixture");
                SourceBlocking.BlockRoll=_=>0;int hp=SkillHealth.Balance256(target);var f=Cast(p,target);Finish(f.Id);Require(SkillHealth.Balance256(target)==hp,"Guided bypassed source shield block");}
            finally{SourceBlocking.BlockRoll=block;target.Delete();}
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.GuidedArrow,20);var bow=NamedGear.Create("unique/282",99,new Random(31),true);p.AddToBackpack(bow);Require(p.EquipItem(bow),"Saved real Lycander");var ammo=new Arrow(20);p.AddToBackpack(ammo);var target=Enemy(p,8);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.GuidedArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.GuidedArrow) && SkillBindings.Instance.AssignQuick(p,9,CombatAbility.GuidedArrow),"Saved bindings");
        var one=Cast(p,target);one=Step(one.Id,3) with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,one);
        var two=Cast(p,aim:MagicArrowChecks.Aim(p,6));two=Step(two.Id) with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,one,two);
        var three=Cast(p,aim:MagicArrowChecks.Aim(p,6));three=Step(three.Id,three.Guided.Remaining) with{NextUtc=TimedEffects.Now.AddMinutes(10)};Require(three.Guided.Mode==5,"Saved acquired phase");Hold(p,one,two,three);
        MagicArrowChecks.Ready(p);Require(ClassCombat.Perform(p,CombatAbility.GuidedArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Saved paid/unemitted windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,ammo.Serial.Value,p.Location,Own(p),SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammo.Amount)));
    }
    private static void Check()=>Controlled(()=>{Differential();Resources();WorldCases();Persist();});
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);var bow=World.FindItem((Serial)saved.Bow);var ammo=World.FindItem((Serial)saved.Ammo);
        Require(Own(p).SequenceEqual(saved.Flights) && SkillHealth.Balance256(target)==saved.Health && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity && ClassCombat.For(p).PendingAbility==CombatAbility.None
            && SkillBindings.Instance.Get(p)==(CombatAbility.GuidedArrow,CombatAbility.GuidedArrow) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,9)==CombatAbility.GuidedArrow,"Saved phases/velocity/damage/resources/bindings or replayed windup");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flights[0].Map]);p.AddToBackpack(bow);int expected=saved.Flights.Sum(f=>NamedElements.To256(f.Damage.Roll(target,_=>0).Total));
        foreach(var flight in saved.Flights){Hold(p,flight with{NextUtc=TimedEffects.Now});Finish(flight.Id);}Require(SkillHealth.Balance256(target)==saved.Health-expected && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity,"Resumed immutable weapon packet/repeated payment");
        var held=saved.Flights.Select(f=>f with{NextUtc=TimedEffects.Now.AddMinutes(10)}).ToArray();Hold(p,held);Require(p.EquipItem(bow),"Reload actual bow");MagicArrowChecks.Ready(p);Require(ClassCombat.Perform(p,CombatAbility.GuidedArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Next paid/unemitted windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flights=held,Health=SkillHealth.Balance256(target),Mana=NecromancerEffects.ManaBalance256(p)}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.GuidedArrow);MagicArrowChecks.Bow(p,typeof(Bow));MagicArrowChecks.Ready(p);var ammo=new Arrow(3);p.AddToBackpack(ammo);var target=Enemy(p,5);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.GuidedArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.GuidedArrow) && SkillBindings.Instance.AssignQuick(p,9,CombatAbility.GuidedArrow),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int before=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target);bool started=false,emitted=false;long deadline=Core.TickCount+5000;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Connected Guided AR roll");WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Clean(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);target.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected Guided did not complete");
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.GuidedArrow){started=true;Require(NecromancerEffects.ManaBalance256(p)==before-2048 && ammo.Amount==3 && Own(p).Length==0,"Natural start4 phase");}
                var flights=Own(p);if(!emitted && flights.Length>0){emitted=true;Require(started && flights.Length==1 && flights[0].Frame<3 && flights[0].Guided.Mode==2 && SkillHealth.Balance256(target)==health && ammo.Amount==2,"Natural ground emission/quantity phase");}
                if(!emitted || flights.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-2048 && ammo.Amount==2,"Natural seeking impact/resources");Clean();Log.Information("guided-arrow-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"guided-arrow-input-result: failed");}
        }
        var point=target.Location;Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("guided-arrow-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.GuidedArrow,point.X,point.Y,point.Z);Wait();});
    }
}
