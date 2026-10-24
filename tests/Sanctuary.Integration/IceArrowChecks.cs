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

public static class IceArrowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IceArrowChecks));
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);public override int Next(int min,int max)=>Math.Max(min,max-1);}
    private sealed class Minimum:Random{public override int Next(int max)=>0;}
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Helm,uint Ring,uint Ammo,uint Corpse,Point3D Origin,WeaponFlight Flight,int Health,int Mana,int Quantity,NamedControlState Control,uint Thawed,NamedControlState Thaw);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ice-arrow.json");
    public static void Configure(){Register("ice-arrow-check",Check);Register("ice-arrow-reload-check",Reload);Register("ice-arrow-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Ice Arrow captured damage/freeze, actual input and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()=>MagicArrowChecks.Fresh("IceArrowOwner");
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
        using var creator=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"ice-arrow-creator-source-probe.json")));
        var fields=creator.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int count=0;
        foreach(var row in creator.RootElement.GetProperty("cases").EnumerateArray())
        {
            int lo=row[0].GetInt32(),added=row[1].GetInt32();int Expected(string key)=>row[6][Array.IndexOf(fields,key)].GetInt32();
            var d=BowLaunch.FromSource(lo*256,(lo+5)*256,0,0,128,row[4].GetInt32()+57,0,0,0,row[5].GetInt32()!=0,new(ColdMin:added+7*256,ColdMax:added+512+13*256,ColdFrames:row[2].GetInt32()+row[3].GetInt32()));
            Require(d.Min256==Expected("nMinDamage") && d.Max256==Expected("nMaxDamage") && d.Percent==Expected("nDamagePercent")
                && d.Elements.ColdMin==Expected("nColdMinDamage") && d.Elements.ColdMax==Expected("nColdMaxDamage") && d.Elements.ColdFrames==Expected("nColdLength"),"Complete cold creator/length differential");count++;
        }
        Require(count==360,"Incomplete Ice creator fixture");Log.Information("Ice Arrow:360 complete creator source/native rows passed; freeze state matrix is separate");
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null,bool held=false,bool left=false)
    {
        Ready(p);int before=NecromancerEffects.ManaBalance256(p);var ammo=BowAmmunition.Stack(p,CombatTiming.Equipped(p));int quantity=ammo.Amount;var old=WeaponMissiles.Snapshot().Flights.Select(x=>x.Id).ToHashSet();
        bool start=held?(left?ClassCombat.ContinueAttack(p,CombatAbility.IceArrow,target,target?.Location??Aim(p),Core.TickCount):ClassCombat.ContinueCast(p,CombatAbility.IceArrow,target,target?.Location??Aim(p),Core.TickCount))
            :ClassCombat.Perform(p,CombatAbility.IceArrow,target,target?.Location??Aim(p),Core.TickCount);
        Require(start && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.IceArrow).Mana256 && ammo.Amount==quantity,"Ice Arrow start payment");var s=ClassCombat.For(p);ClassCombat.ReleaseCast(p);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Single Ice Arrow event");
        var f=WeaponMissiles.Snapshot().Flights.Single(x=>!old.Contains(x.Id));Require(f.Ability==CombatAbility.IceArrow && f.Missile=="icearrow" && f.Frames==40 && f.NextDelay==0 && f.Damage.PhysicalToMagicPercent==0
            && f.Damage.Conversion==null && f.Damage.FreezeColdLength && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.IceArrow).Mana256 && (ammo.Deleted?0:ammo.Amount)==quantity-1,"Captured Cold conversion/once quantity/no repeated mana");return f;
    }
    private static void Controlled(Action action)
    {
        var launch=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{AmazonBow.LaunchRandom=launch;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;}
    }
    private static void Finish(long id)
    {for(int i=0;i<250 && WeaponMissiles.State(id) is{} f;i++){WeaponMissiles.Tick(f.NextUtc);}Require(WeaponMissiles.State(id)==null,"Cold projectile did not finish");}
    private static BowLaunchTarget Target(PlayerMobile p,int physical=0,int cold=0)
    {var t=MagicArrowChecks.Target(p);t.SetResistance(ResistanceType.Physical,physical);t.SetResistance(ResistanceType.Cold,cold);return t;}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Cases()
    {
        var p=Fresh();try
        {
            var bow=MagicArrowChecks.Bow(p,typeof(Bow));Require(!ClassCombat.Available(p,CombatAbility.IceArrow),"Unlearned Ice Arrow available");Train(p,CombatAbility.IceArrow);
            Require(CombatTiming.WeaponAttack(CombatAbility.IceArrow) && CombatTiming.For(p,CombatAbility.IceArrow)==CombatTiming.For(p,CombatAbility.MagicArrow)
                && SkillBindings.Instance.Assign(p,true,CombatAbility.IceArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.IceArrow) && SkillBindings.Instance.AssignQuick(p,8,CombatAbility.IceArrow),"Ice bow timing/bindings");
            var values=SkillMechanics.Values(p,CombatAbility.IceArrow);Require(values.Mana256==1024 && values.Damage256(false)==1658 && values.Damage256(true)==2764 && values.DurationFrames==50 && NecromancerSummons.Source("icearrow","dParam1")==100,"Pinned rank1 Ice/hard Cold synergy");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.IceArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"No ammo accepted/paid");
            var bolts=new Bolt(4);p.AddToBackpack(bolts);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.IceArrow,null,Aim(p),Core.TickCount),"Wrong ammo accepted");var arrows=new Arrow(20);p.AddToBackpack(arrows);
            var shot=Cast(p);Require(shot.Damage.Elements is{ColdMin:1658,ColdMax:2764,ColdFrames:50},"Ice skill capture");WeaponMissiles.Cancel(p);
            Cast(p,held:true);WeaponMissiles.Cancel(p);Cast(p,held:true,left:true);WeaponMissiles.Cancel(p);Require(arrows.Amount==17 && bolts.Amount==4,"Once-only arrows");
            bow=MagicArrowChecks.Bow(p,typeof(Crossbow));Cast(p);WeaponMissiles.Cancel(p);Require(arrows.Amount==17 && bolts.Amount==3,"Crossbow bolt");bow=MagicArrowChecks.Bow(p,typeof(Bow));
            Ready(p);NecromancerEffects.SetMana256(p,1023);Require(!ClassCombat.Perform(p,CombatAbility.IceArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==1023,"Insufficient mana accepted/debited");
            Mana(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);int quantity=arrows.Amount;Require(ClassCombat.Perform(p,CombatAbility.IceArrow,null,Aim(p),Core.TickCount),"Paid start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-1024 && arrows.Amount==quantity,"Cancel refunded/used ammo");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.IceArrow,null,Aim(p),Core.TickCount),"Paid move start");at=ClassCombat.For(p).EffectAt;var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West) && p.Location==origin,"Return step");
            Require(!ClassCombat.ResolveAction(p,at) && arrows.Amount==quantity && NecromancerEffects.ManaBalance256(p)==mana-2048,"Moved shot revived/refunded");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.IceArrow,null,Aim(p),Core.TickCount),"Paid zero mana release");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(ClassCombat.ResolveAction(p,at) && Own(p).Length==1 && arrows.Amount==quantity-1,"Second debit");WeaponMissiles.Cancel(p);
            Train(p,CombatAbility.IceArrow,20);values=SkillMechanics.Values(p,CombatAbility.IceArrow);Require(values.Mana256==2240 && values.Damage256(false)==59719 && values.Damage256(true)==64143 && values.DurationFrames==145,"Rank20 Ice tiers/fractions/length");
            Train(p,CombatAbility.FreezingArrow);Require(SkillMechanics.Values(p,CombatAbility.IceArrow).DurationFrames==152,"Hard Freezing length synergy");
            var helm=NamedGear.Create("unique/343",99,new Maximum(),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Real Nightwing");var damage=BowLaunch.Capture(p,CombatAbility.IceArrow,false);
            Require(SkillMechanics.Rank(p,CombatAbility.IceArrow)==22 && SkillMechanics.Rank(p,CombatAbility.ColdArrow)==3 && SkillMechanics.Rank(p,CombatAbility.FreezingArrow)==3
                && damage.Elements is{ColdMin:80122,ColdMax:85846,ColdFrames:162} && damage.ColdMastery==0 && damage.Conversion==null && damage.FreezeColdLength,$"Hard-only damage/length, soft rank and independent mastery: {JsonSerializer.Serialize(damage)}");
            var ring=NamedGear.Create("unique/275",99,new Maximum(),true);p.AddToBackpack(ring);Require(p.EquipItem(ring),"Real Raven Frost");damage=BowLaunch.Capture(p,CombatAbility.IceArrow,false);
            Require(damage.Elements is{ColdMin:84538,ColdMax:99094,ColdFrames:262},"Additive real item cold/freezing length");p.AddToBackpack(helm);p.AddToBackpack(ring);
            foreach(bool immune in new[]{false,true})
            {
                Mana(p);var target=Target(p,immune?0:100,immune?100:0);try
                {
                    int before=SkillHealth.Balance256(target);shot=Cast(p,target);var roll=shot.Damage.Roll(target,_=>0);int expected=NamedElements.To256(immune?roll.Physical:roll.Cold);Finish(shot.Id);
                    Require(expected>0 && SkillHealth.Balance256(target)==before-expected && (NamedControl.FrozenRemaining(target)>0)==!immune && TimedEffects.Chill(target,TimedEffects.Now)==0,"Physical/cold immunity split or duplicate chill");
                }finally{TimedEffects.Clear(target);target.Delete();}
            }
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static uint Corpse(PlayerMobile p)
    {
        var victim=Target(p);victim.MoveToWorld(new(p.X+2,p.Y+2,p.Z),p.Map);Require(FreezeStates.Apply(p,victim,50,Core.TickCount),"Corpse freeze");AOS.Damage(victim,p,100000,false,100,0,0,0,0,weaponDamage:false);
        var corpse=victim.Corpse as Corpse;Require(corpse!=null && CorpseSkillRules.Shattered(corpse) && !CorpseSkillRules.Usable(p,corpse,20) && !SkeletonPets.Usable(p,corpse),"Frozen corpse remained selectable");
        // Inspection fixture must outlive the full three-boot suite; native corpse decay remains seven minutes.
        corpse.BeginDecay(TimeSpan.FromHours(1));
        var gold=new Gold(777);corpse.DropItem(gold);Require(gold.IsChildOf(corpse) && !corpse.SanctuaryConsumed && !corpse.Animated,"Freeze destroyed ordinary loot");return corpse.Serial.Value;
    }
    private static void Hold(PlayerMobile p,WeaponFlight f)
    {var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(f).ToArray()});}
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.IceArrow,20);Train(p,CombatAbility.FreezingArrow);var bow=MagicArrowChecks.Bow(p,typeof(Bow));var helm=NamedGear.Create("unique/343",99,new Maximum(),true);var ring=NamedGear.Create("unique/275",99,new Maximum(),true);p.AddToBackpack(helm);p.AddToBackpack(ring);Require(p.EquipItem(helm) && p.EquipItem(ring),"Saved real cold gear");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.IceArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.IceArrow) && SkillBindings.Instance.AssignQuick(p,8,CombatAbility.IceArrow),"Saved Ice bindings");
        var ammo=new Arrow(10);p.AddToBackpack(ammo);var target=Target(p);var first=Cast(p,target);Require(first.Damage.Elements.ColdFrames==262,"Actual saved raw freeze length");Finish(first.Id);
        Require(TimedEffects.TryActor(target,out var state) && state.Control?.Freeze!=null,"Actual Ice Arrow did not freeze");var until=TimedEffects.Now.AddMinutes(10);state.Control=state.Control with{FreezeUntil=until,Freeze=state.Control.Freeze with{AiUntilUtc=until.AddMilliseconds(40)}};
        var thawed=Target(p);thawed.MoveToWorld(new(p.X+8,p.Y+2,p.Z),p.Map);Require(FreezeStates.Apply(p,thawed,1,Core.TickCount),"Saved thaw-phase seed");TimedEffects.TryActor(thawed,out var thawState);var rawUntil=thawState.Control.FreezeUntil;FreezeStates.Settle(thawed,thawState,rawUntil);
        Require(thawState.Control.Freeze.Removed && thawState.Control.Freeze.AiUntilUtc==rawUntil.AddMilliseconds(600),"Saved thaw source delay");thawState.Control=thawState.Control with{Freeze=thawState.Control.Freeze with{AiUntilUtc=TimedEffects.Now.AddMinutes(10)}};
        var flight=Cast(p,target) with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,flight);uint corpse=Corpse(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.IceArrow,null,Aim(p),Core.TickCount),"Saved paid/unemitted windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,helm.Serial.Value,ring.Serial.Value,ammo.Serial.Value,corpse,p.Location,flight,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammo.Amount,state.Control,thawed.Serial.Value,thawState.Control)));
    }
    private static void NaturalExpiry()
    {
        var p=Fresh();var target=Target(p);Require(FreezeStates.Apply(p,target,1,Core.TickCount),"Natural one-frame freeze");
        Timer.StartTimer(TimeSpan.FromMilliseconds(850),()=>
        {
            try{Require(NamedControl.FrozenRemaining(target)==0 && !FreezeStates.Waiting(target) && !TimedEffects.TryActor(target,out _),"Natural freeze/thaw AI expiry");Log.Information("ice-arrow-state-result: passed");}
            catch(Exception e){Log.Error(e,"ice-arrow-state-result: failed");}finally{TimedEffects.Clear(target);target.Delete();p.Delete();}
        });
    }
    private static void Check()=>Controlled(()=>{Differential();Cases();Persist();NaturalExpiry();});
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);var ammo=World.FindItem((Serial)saved.Ammo);var bow=World.FindItem((Serial)saved.Bow);var helm=World.FindItem((Serial)saved.Helm);var ring=World.FindItem((Serial)saved.Ring);var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);
        Require(Own(p).Single()==saved.Flight && SkillHealth.Balance256(target)==saved.Health && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity && ClassCombat.For(p).PendingAbility==CombatAbility.None
            && SkillBindings.Instance.Get(p)==(CombatAbility.IceArrow,CombatAbility.IceArrow) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,8)==CombatAbility.IceArrow
            && TimedEffects.TryActor(target,out var state) && state.Control==saved.Control && CorpseSkillRules.Shattered(corpse) && corpse.Items.Count>0 && !CorpseSkillRules.Usable(p,corpse,20),$"Saved Ice persistence: flight={Own(p).Single()==saved.Flight}, hp={SkillHealth.Balance256(target)}/{saved.Health}, mana={NecromancerEffects.ManaBalance256(p)}/{saved.Mana}, ammo={ammo.Amount}/{saved.Quantity}, pending={ClassCombat.For(p).PendingAbility}, control={NamedControl.State(target)==saved.Control}, corpse={corpse?.Serial}, deleted={corpse?.Deleted}, shattered={CorpseSkillRules.Shattered(corpse)}, loot={corpse?.Items.Count}");
        var thawed=(BowLaunchTarget)World.FindMobile((Serial)saved.Thawed);Require(TimedEffects.TryActor(thawed,out var thawState) && thawState.Control==saved.Thaw && NamedControl.FrozenRemaining(thawed)==0 && FreezeStates.Waiting(thawed) && NamedControl.Think(thawed),"Saved post-thaw AI state/control");
        TimedEffects.Settle(thawed,saved.Thaw.Freeze.AiUntilUtc);Require(!TimedEffects.TryActor(thawed,out _),"Resumed post-thaw AI state expiry");thawState=TimedEffects.Actor(thawed,TimedEffects.Now);thawState.Control=saved.Thaw with{Freeze=saved.Thaw.Freeze with{AiUntilUtc=TimedEffects.Now.AddMinutes(10)}};
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);p.AddToBackpack(bow);p.AddToBackpack(helm);p.AddToBackpack(ring);int expected=NamedElements.To256(saved.Flight.Damage.Roll(target,_=>0).Total);Hold(p,saved.Flight with{NextUtc=TimedEffects.Now});Finish(saved.Flight.Id);
        Require(SkillHealth.Balance256(target)==saved.Health-expected && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity,"Resumed captured Ice damage or repayment");TimedEffects.TryActor(target,out state);
        Require(state.Control.FreezeUntil==saved.Control.FreezeUntil && state.Control.Freeze.Source==saved.Control.Freeze.Source,"Resumed shorter freeze replaced expiry/original owner");var until=TimedEffects.Now.AddMinutes(10);state.Control=state.Control with{FreezeUntil=until,Freeze=state.Control.Freeze with{AiUntilUtc=until.AddMilliseconds(40)}};
        var held=saved.Flight with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,held);Require(p.EquipItem(bow) && p.EquipItem(helm) && p.EquipItem(ring),"Reload real cold gear");Ready(p);Require(ClassCombat.Perform(p,CombatAbility.IceArrow,null,Aim(p),Core.TickCount),"Next paid/unemitted Ice windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Health=SkillHealth.Balance256(target),Mana=NecromancerEffects.ManaBalance256(p),Control=state.Control,Thaw=thawState.Control}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.IceArrow);MagicArrowChecks.Bow(p,typeof(Bow));Ready(p);var ammo=new Arrow(3);p.AddToBackpack(ammo);var target=Target(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.IceArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.IceArrow) && SkillBindings.Instance.AssignQuick(p,8,CombatAbility.IceArrow),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int before=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target);bool started=false,emitted=false;long deadline=Core.TickCount+5000;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Clean(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);target.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected Ice Arrow did not complete");
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.IceArrow)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.IceArrow).Mana256 && ammo.Amount==3 && Own(p).Length==0,"Natural start4 phase");}
                var fan=Own(p);if(!emitted && fan.Length>0)
                {emitted=true;Require(started && fan.Length==1 && fan.All(x=>x.Frame<3 && x.Missile=="icearrow") && SkillHealth.Balance256(target)==health && ammo.Amount==2,"Natural single-arrow/ammunition phase");}
                if(!emitted || fan.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.IceArrow).Mana256 && ammo.Amount==2,"Natural travelling cold arrow/resource accounting");Require(NamedControl.FrozenRemaining(target)>0 && NamedControl.State(target).Freeze?.Source==p.Serial.Value && TimedEffects.Chill(target,TimedEffects.Now)==0,"Natural freeze state without duplicate chill");Clean();Log.Information("ice-arrow-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"ice-arrow-input-result: failed");}
        }
        var point=Aim(p);Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("ice-arrow-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.IceArrow,point.X,point.Y,point.Z);Wait();});
    }
}
