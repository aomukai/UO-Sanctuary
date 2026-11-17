// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;
public static class PierceActionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PierceActionChecks));
    private sealed record Saved(uint Owner,uint Bow,uint[] Gear,Point3D Point,uint Index,WeaponFlight Flight,string Items,string Procs,int Life,int Mana,int Ammo,int Generation);
    private sealed class High:Random{internal int Calls;public override int Next(int n){Calls++;return n-1;}}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pierce-action-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static void Ready(PlayerMobile p){AmazonStrafe.Cancel(p);MagicArrowContactChecks.Ready(p);p.Backpack.FindItemByType<Arrow>().Amount=100;}
    private static void Clear(PlayerMobile p){AmazonStrafe.Cancel(p);ClassCombat.Stop(p);WeaponMissiles.Cancel(p);}
    private static PlayerMobile Owner(int row,out BaseWeapon bow,out Item[] gear)
    {
        var p=MagicArrowContactChecks.Owner(row,false,out bow,out var gloves,out var helm);var belt=DruidSetupItemChecks.Named(p,"Razortail");gear=[gloves,helm,belt];
        foreach(var ability in new[]{CombatAbility.MultipleShot,CombatAbility.Strafe,CombatAbility.Pierce}){ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId,ability==CombatAbility.MultipleShot?5:1);}return p;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"pierce-action-reload-check":"pierce-action-check";ConsoleInputHandler.RegisterCommand(name,"Candidate saved Pierce action index,fan/series capture and reload",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="pierce-action-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated candidate Multiple Shot index and independent critical stream",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static WeaponFlight[] Release(PlayerMobile p,CombatAbility ability,BaseCreature target,bool candidate)
    {
        Ready(p);uint before=PlayerPierceActions.Index(p);var world=NpcWorldAllocation.Capture();string procs=Json(PlayerItemProcs.Capture());
        Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount),"Action entry: "+ability);ClassCombat.ReleaseCast(p);Require(PlayerPierceActions.Index(p)==before,"Windup spent index");
        Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Release: "+ability);var flights=Flights(p);Require(flights.Length>0,"Missing released missiles");
        uint index=unchecked(before+1);Require(PlayerPierceActions.Index(p)==(candidate?index:before),"Release committed wrong index: "+ability);
        if(candidate){int expected=ability==CombatAbility.GuidedArrow?0:NamedProjectiles.SeededPierceBudget(WeaponMissiles.CurrentPierce(p),unchecked((int)index)).Budget;Require(flights.All(f=>f.Pierces==expected),"Fan/parent budget differs: "+ability);}
        Require(NpcWorldAllocation.Capture()==world&&Json(PlayerItemProcs.Capture())==procs,"Pierce release borrowed world or item-proc stream");return flights;
    }
    private static void Scenarios(PlayerMobile p,BaseWeapon bow,Item[] gear)
    {
        var target=MagicArrowContactChecks.Target(p,8);var old=AmazonBow.LaunchRandom;var high=new High();AmazonBow.LaunchRandom=high;
        try
        {
            Require(!PlayerPierceActions.Approved&&PlayerPierceActions.Capture(p)==null,"Unapproved mapping admitted ordinary gameplay");
            high.Calls=0;var ordinary=Release(p,CombatAbility.MultipleShot,target,false);Require(high.Calls==ordinary.Length*2&&ordinary.All(f=>f.Pierces==0),"Unapproved fallback changed its independent RNG");Clear(p);
            using var scope=PlayerPierceActions.Candidate(p);
            var first=PlayerPierceActions.Capture(p);var stale=PlayerPierceActions.Capture(p);first.Commit();uint once=PlayerPierceActions.Index(p);first.Commit();bool refused=false;try{stale.Commit();}catch(InvalidOperationException){refused=true;}Require(refused&&PlayerPierceActions.Index(p)==once,"Stale or repeated ticket overwrote counter");
            var rows=PlayerPierceActions.Snapshot();string original=Json(rows);refused=false;try{PlayerPierceActions.Restore(rows.Append(rows.Single(x=>x.Owner==p.Serial.Value)).ToArray());}catch(InvalidDataException){refused=true;}Require(refused&&Json(PlayerPierceActions.Snapshot())==original,"Invalid restore changed live index store");
            foreach(var ability in new[]{CombatAbility.MagicArrow,CombatAbility.MultipleShot,CombatAbility.FireArrow,CombatAbility.ColdArrow,CombatAbility.ExplodingArrow,CombatAbility.IceArrow,CombatAbility.GuidedArrow,CombatAbility.FreezingArrow,CombatAbility.ImmolationArrow})
            {
                ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);var ground=FireGround.Snapshot();FireGround.Restore(ground with{Cooldowns=ground.Cooldowns.Where(x=>x.Owner!=p.Serial.Value).ToArray()});high.Calls=0;var fan=Release(p,ability,target,true);Require(high.Calls==fan.Length,"Pierce consumed critical-stream draws: "+ability);Clear(p);
            }
            Ready(p);uint before=PlayerPierceActions.Index(p);Require(ClassCombat.Perform(p,CombatAbility.MultipleShot,target,target.Location,Core.TickCount),"Canceled windup");ClassCombat.Stop(p);Require(PlayerPierceActions.Index(p)==before,"Canceled windup spent index");
            Ready(p);Require(!AmazonBow.Begin(p,CombatAbility.MultipleShot,Serial.Zero,p.Location,Core.TickCount)&&PlayerPierceActions.Index(p)==before,"Invalid aim spent index");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.Strafe,target,target.Location,Core.TickCount),"Strafe entry");ClassCombat.ReleaseCast(p);uint seriesIndex=unchecked(before+1);int released=0;while(ClassCombat.For(p).Strafe is{Finished:false,Remaining:>0} series)
            {
                var ids=Flights(p).Select(f=>f.Id).ToHashSet();Require(ClassCombat.ResolveAction(p,series.EffectAt),"Strafe event");var shot=Flights(p).Single(f=>!ids.Contains(f.Id));Require(PlayerPierceActions.Index(p)==seriesIndex&&shot.Pierces==NamedProjectiles.SeededPierceBudget(WeaponMissiles.CurrentPierce(p),unchecked((int)seriesIndex)).Budget,"Strafe rerolled/advanced its action index");released++;Require(released<=10,"Strafe failed to terminate");
            }
            Require(released>=2,"Missing multi-arrow Strafe control");Clear(p);
            p.AddToBackpack(bow);var javelin=(BaseWeapon)PlayerItemProcWorldChecks.Carrier(p,new SoulGlaive());ClassGear.Refresh(p);
            foreach(var ability in new[]{CombatAbility.PoisonJavelin,CombatAbility.LightningBolt,CombatAbility.PlagueJavelin,CombatAbility.LightningFury})
            {ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);var ground=FireGround.Snapshot();FireGround.Restore(ground with{Cooldowns=ground.Cooldowns.Where(x=>x.Owner!=p.Serial.Value).ToArray()});high.Calls=0;var flights=Release(p,ability,target,true);Require(flights.Length==1&&high.Calls==1,"Javelin index/critical stream");Clear(p);}
            p.AddToBackpack(javelin);Require(p.EquipItem(bow),"Restore original bow");javelin.Delete();ClassGear.Refresh(p);
            rows=PlayerPierceActions.Snapshot();PlayerPierceActions.Restore(rows.Where(x=>x.Owner!=p.Serial.Value).Append(new(p.Serial.Value,uint.MaxValue)).ToArray());Release(p,CombatAbility.MultipleShot,target,true);Require(PlayerPierceActions.Index(p)==0&&PlayerPierceActions.Snapshot().Any(x=>x.Owner==p.Serial.Value&&x.Index==0),"UInt wrap lost zero index row");Clear(p);
        }
        finally{Clear(p);target.Delete();AmazonBow.LaunchRandom=old;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();PlayerMobile p;BaseWeapon bow;Item[] gear;int generation=0;
        if(!reload){p=Owner(3550,out bow,out gear);Scenarios(p,bow,gear);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bow=(BaseWeapon)World.FindItem((Serial)s.Bow);gear=s.Gear.Select(x=>World.FindItem((Serial)x)).ToArray();
            Require(generation<=2&&PlayerPierceActions.Index(p)==s.Index&&bow.Parent==p&&gear.All(x=>x.Parent==p)&&Json(new[]{(Item)bow}.Concat(gear).Select(ClassGear.State))==s.Items&&Json(MagicArrowContactChecks.Own(p,bow))==s.Procs&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&p.Backpack.FindItemByType<Arrow>().Amount==s.Ammo&&Json(Flights(p).Single())==Json(s.Flight)&&ClassCombat.For(p).Strafe==null,"Original saved index/flight/gear/procs/wounds/resources changed");
            p.MoveToWorld(s.Point,Map.Trammel);var until=TimedEffects.Now;MagicArrowContactChecks.Hold(p,s.Flight with{NextUtc=until});MagicArrowContactChecks.Drain(p);Require(PlayerPierceActions.Index(p)==s.Index&&NecromancerEffects.ManaBalance256(p)==s.Mana&&p.Backpack.FindItemByType<Arrow>().Amount==s.Ammo,"Resumed flight advanced/replayed index or resources");
        }
        Require(PlayerPierceActions.Capture(p)==null,"Saved counter enabled unapproved gameplay");using var scope=PlayerPierceActions.Candidate(p);var t=MagicArrowContactChecks.Target(p,8);var fan=Release(p,CombatAbility.MultipleShot,t,true);var held=fan[0] with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};MagicArrowContactChecks.Hold(p,held);t.Delete();SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,bow.Serial.Value,gear.Select(x=>x.Serial.Value).ToArray(),point,PlayerPierceActions.Index(p),held,Json(new[]{(Item)bow}.Concat(gear).Select(ClassGear.State)),Json(MagicArrowContactChecks.Own(p,bow)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),p.Backpack.FindItemByType<Arrow>().Amount,generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new MagicArrowContactChecks.Rolls();var p=Owner(3650,out var bow,out _);var scope=PlayerPierceActions.Candidate(p);var t=MagicArrowContactChecks.Target(p,8);var account=FixtureNetwork.Authenticate(n,p,"pierce-action-");Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.MultipleShot)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.MultipleShot),"Candidate input bindings");int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.MultipleShot).Mana256,ammo=p.Backpack.FindItemByType<Arrow>().Amount;long deadline=Core.TickCount+6000;bool began=false;
        void Close(){Clear(p);t.Delete();scope.Dispose();rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Candidate input timeout");if(!began&&ClassCombat.For(p).PendingAbility==CombatAbility.MultipleShot){began=true;Require(PlayerPierceActions.Index(p)==0&&Flights(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Input windup spent index or lost startup mana");Log.Information("{Label}: windup",label);}var fan=Flights(p);if(fan.Length==0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&PlayerPierceActions.Index(p)==1&&fan.Length==(int)SkillMechanics.Values(p,CombatAbility.MultipleShot).Eval("calc1")&&fan.All(f=>f.Pierces==NamedProjectiles.SeededPierceBudget(WeaponMissiles.CurrentPierce(p),1).Budget)&&p.Backpack.FindItemByType<Arrow>().Amount==ammo-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Natural fan/index/resource delivery");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.MultipleShot,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
