// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BoundDemonTypedChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonTypedChecks));
    private sealed record Saved(int Generation,uint Owner,uint Actor,uint Weapon,BoundDemonState State,int Life,int Mana,int Stamina,int Minimum,int Maximum,double Magery);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-typed-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name is"BoundDemonTypedFixture"or"BoundDemonTypedTarget"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-typed-reload-check":"bound-demon-typed-check";
            ConsoleInputHandler.RegisterCommand(name,"Bound original Imp typed native weapon/spell damage and saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="BoundDemonTypedTarget",Frozen=true,CantWalk=true,NoKillAwards=true};
        t.SetHits(50000);t.Hits=t.HitsMax;t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();Resists(t,0);return t;
    }
    private static void Resists(BaseCreature t,int amount)
    {foreach(var kind in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(kind,amount);}}
    private static void Packet(Imp body,BaseCreature target,int physical,int fire,int poison,int expected,int expectedPhysical,
        bool weapon=true,bool spell=false,bool ignore=false,bool ranged=false,bool recovery=true)
    {
        int before=SkillHealth.Balance256(target),calls=0;int? observed=null;int observedPhysical=-1;
        var previous=ClassCombatHooks.PreparedWeaponDamage;var recovered=ClassCombatHooks.CreatureDamaged;int recoveries=0;
        ClassCombatHooks.PreparedWeaponDamage=(from,to,p,r)=>{if(from==body&&to==target){calls++;observedPhysical=p;Require(r==ranged,"Native ranged context changed");}previous?.Invoke(from,to,p,r);};
        ClassCombatHooks.CreatureDamaged=(to,from,amount,w)=>{if(from==body&&to==target){recoveries++;Require(w==weapon,"Native recovery weapon context changed");}recovered?.Invoke(to,from,amount,w);};
        try
        {
            if(ignore)
            {var raw=SkillCombat.UnresistedDamage(target,body,100,physical,weapon,ranged);observed=raw?.Total256;}
            else{var raw=SkillCombat.ResistedDamage(target,body,100,physical,fire,0,poison,0,spell,weapon,ranged);observed=raw?.Total256;}
            Require(observed==expected,"Prepared typed total differs from literal native context");
            AOS.Damage(target,body,100,ignore,physical,fire,0,poison,0,archer:ranged,spellDamage:spell,weaponDamage:weapon);
            Require(before-SkillHealth.Balance256(target)==expected&&calls==(weapon?1:0)
                &&(!weapon||observedPhysical==expectedPhysical),$"Actual AOS typed context:loss{before-SkillHealth.Balance256(target)}/{expected},callbacks{calls},physical{observedPhysical}/{expectedPhysical},types{physical}/{fire}/{poison},weapon{weapon},spell{spell},ignore{ignore},ranged{ranged}");
            Require(recoveries==(expected>0&&recovery?1:0),"Native recovery ignored surviving prepared physical or promoted poison-only damage");
        }
        finally{ClassCombatHooks.PreparedWeaponDamage=previous;ClassCombatHooks.CreatureDamaged=recovered;}
    }
    private static void Damage(Imp body,PlayerMobile p,BaseCreature target)
    {
        Require(BoundDemonHosts.State(body).Birth is{Rank:2,FlatDamage:75,DamagePercent:290},"Typed literal context changed");
        // Native0/50/50 stays0/50/50;75flat physical*3.9adds292.5,not75split into fire/poison.
        Packet(body,target,0,50,50,100480,74880);
        Packet(body,target,20,80,0,115328,94848);
        Packet(body,target,0,50,50,100480,74880,ranged:true);
        Packet(body,target,0,50,50,25600,0,weapon:false,spell:true);
        Packet(body,target,0,0,100,100480,74880);
        Packet(body,target,0,0,100,25600,0,weapon:false,spell:true,recovery:false);
        target.SetResistance(ResistanceType.Physical,100);Packet(body,target,0,50,50,25600,0);
        Packet(body,target,0,0,100,25600,0,recovery:false);
        Resists(target,100);Packet(body,target,0,50,50,0,0);
        // Existing native armor-ignore bypass stays independent of D2 mitigation.
        Packet(body,target,0,50,50,100480,74880,ignore:true);Resists(target,0);
        int before=SkillHealth.Balance256(target);AOS.Damage(target,body,0,false,0,50,0,50,0,weaponDamage:true);
        Require(SkillHealth.Balance256(target)==before,"Zero native base packet invented flat contact damage");
        var dagger=(Dagger)body.FindItemOnLayer(Layer.OneHanded);int rawMinimum=body.DamageMin,rawMaximum=body.DamageMax;
        body.SetDamage(100,100);var hook=ClassCombatHooks.SkillResistedDamage;int calls=0,rolled=0;ClassSkillDamage? captured=null;
        ClassCombatHooks.SkillResistedDamage=(to,from,amount,physical,fire,cold,poison,lightning,spell,weapon,ranged)=>
        {
            var result=hook(to,from,amount,physical,fire,cold,poison,lightning,spell,weapon,ranged);
            if(from==body&&to==target){calls++;rolled=amount;captured=result;Require(physical==0&&fire==50&&poison==50&&cold==0&&lightning==0&&!spell&&weapon&&!ranged,"Native weapon changed original Imp type split");}return result;
        };
        try
        {
            before=SkillHealth.Balance256(target);dagger.OnHit(body,target);
            int physical=NamedElements.To256(75*3.9),expected=checked(physical+rolled*256);
            Require(calls==1&&rolled>0&&captured is{} packet&&packet.Physical256==physical&&packet.Total256==expected
                &&before-SkillHealth.Balance256(target)==expected,"Actual native Dagger.OnHit lost/duplicated physical bonus or scaled native elements");
        }
        finally{ClassCombatHooks.SkillResistedDamage=hook;body.SetDamage(rawMinimum,rawMaximum);}
        Log.Information("Bound typed:10literal native AOS contexts/physical and elemental immunity/armor-ignore/ranged/nonweapon spell/zero base;pure-poison packets gain recovery only with surviving added physical;actual Dagger.OnHit keeps0physical50fire50poison and adds captured75physical once before290percentED");
    }
    private static void Scene(NetState network)
    {
        PlayerMobile p;Imp body;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);body=new Imp();}finally{Creating=false;}
        p.Name=body.Name="BoundDemonTypedFixture";p.Female=true;p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.binddemon",1);ClassSkillChecks.Train(p,"warlock.demonicmastery",20);
        FixtureSpace.Corridor(p,7000,30,3500);var account=FixtureNetwork.Authenticate(network,p,"bound-demon-typed-");bool kept=false;GoatmanNpcContactChecks.Recipient target=null;
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");body.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);body.AIObject?.AITimer.Stop();
            int minimum=body.DamageMin,maximum=body.DamageMax;double magery=body.Skills[SkillName.Magery].Base;
            var weapon=new Dagger();body.AddItem(weapon);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/2-13);
            Require(BoundDemonHosts.BindPrepared(p,body,out var issue),"Original mixed Imp refused: "+issue);body.AIObject?.AITimer.Stop();
            Require(body.PhysicalDamage==0&&body.FireDamage==50&&body.PoisonDamage==50&&body.DamageMin==minimum&&body.DamageMax==maximum
                &&body.Skills[SkillName.Magery].Base==magery&&weapon.Parent==body,"Binding rewrote native split/endpoints/abilities/items");
            var state=BoundDemonHosts.State(body);target=Target(p);Damage(body,p,target);
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(BoundDemonHosts.State(body).Birth==state.Birth,"Gear removal rewrote captured typed bonuses");
            Packet(body,target,0,50,50,100480,74880);Require(BindDemonChannel.State(p)==null,"Typed adapter unexpectedly started a Bind channel");
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/2-17);body.Mana=Math.Max(0,body.ManaMax-3);body.Stam=Math.Max(0,body.StamMax-5);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,body.Serial.Value,weapon.Serial.Value,BoundDemonHosts.State(body),SkillHealth.Balance256(body),body.Mana,body.Stam,minimum,maximum,magery)));kept=true;
        }
        finally{target?.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){body.Delete();p.Delete();BoundDemonHosts.Tick();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many typed reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Actor)as Imp;
        Require(body is{Alive:true,Deleted:false}&&body.Map==Map.Internal&&body.ControlMaster==p&&BoundDemonHosts.State(body)==saved.State
            &&body.DamageMin==saved.Minimum&&body.DamageMax==saved.Maximum&&body.PhysicalDamage==0&&body.FireDamage==50&&body.PoisonDamage==50
            &&SkillHealth.Balance256(body)==saved.Life&&body.Mana==saved.Mana&&body.Stam==saved.Stamina
            &&body.Skills[SkillName.Magery].Base==saved.Magery&&World.FindItem((Serial)saved.Weapon)?.Parent==body,"Original mixed Imp/captured state/native fields/resources changed on load");
        var account=FixtureNetwork.Authenticate(network,p,"bound-demon-typed-reload-");GoatmanNpcContactChecks.Recipient target=null;
        try
        {
            PersistentMinions.Resume(p);Require(BoundDemonHosts.CanAct(body),"Original typed actor did not resume");target=Target(p);Packet(body,target,0,50,50,100480,74880);
            PersistentMinions.Suspend(p);Require(SkillHealth.Balance256(body)==saved.Life,"Typed native reload healed original wounds");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Bound typed:original Imp/native0-50-50split/base endpoints/weapon/Magery/immutable bonuses and injured resources survive reload;positive native AOS remains one physical addition without replay");
        }
        finally{target?.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
