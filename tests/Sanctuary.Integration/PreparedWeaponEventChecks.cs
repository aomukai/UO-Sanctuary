// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PreparedWeaponEventChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PreparedWeaponEventChecks));
    private sealed class NoCritical:Random{public override int Next(int n)=>Math.Max(0,n-1);}
    private sealed record Saved(uint Owner,uint Target,int OwnerLife,int TargetLife,Point3D Origin,PaladinAuraLease Thorns,NecromancerCurse Tap);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"prepared-weapon-event-fixtures.json");private static int _scene;private static bool _creating;
    public static void Configure(){Freeze();Register("prepared-weapon-event-check",Check);Register("prepared-weapon-event-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=actor=>_creating || actor.Name is "LifeTapOwner" or "LifeTapTarget"?TimeSpan.FromHours(1):old?.Invoke(actor)??Mobile.DefaultHitsRate;}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Life Tap/leech before return and pre-absorb clipping",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Name="LifeTapOwner",Player=true};}finally{_creating=false;}p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(p,99);
        FixtureSpace.Corridor(p,6500,24,2100+32*_scene++);return p;
    }
    private static GraveyardZombie Target(PlayerMobile p)
    {
        GraveyardZombie m;_creating=true;try{m=new GraveyardZombie{Name="LifeTapTarget",CantWalk=true,Frozen=true,NoKillAwards=true};}finally{_creating=false;}m.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);m.AIObject.AITimer.Stop();m.SetHits(100);m.SetResistance(ResistanceType.Physical,0);
        // Valid supplied source leases isolate the event consumers. Learned
        // Thorns and Life Tap application retain their independent fixtures.
        PaladinAuras.ElementalWeapon(new(m.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddDays(2),DamagePercent:250));
        NecromancerEffects.Profiles.Store(m,new(new(CombatAbility.LifeTap,p.Serial.Value,1,TimedEffects.Now.AddDays(2),p.X,p.Y)));
        return m;
    }
    private static void Reset(PlayerMobile p,BaseCreature m,int target=30)
    {SkillHealth.SetBalance256(p,10*256);SkillHealth.SetBalance256(m,target*256);}
    private static void End(PlayerMobile p,BaseCreature m)
    {var state=PaladinAuras.Snapshot();PaladinAuras.Restore(state with{Leases=state.Leases.Where(x=>x.Target!=m.Serial.Value).ToArray()});NecromancerEffects.Profiles.Clear(m);m.Corpse?.Delete();m.Delete();p.Delete();}
    private static int Expected(int incoming,int victim,int healing,int leech=0,int flat=4,int percent=31)
        =>10*256+healing+leech-flat*256-(int)((long)Math.Min(incoming,victim)*percent/100);
    private static void Cases(PlayerMobile p,GraveyardZombie m)
    {
        foreach(string route in new[]{"typed","native","gear","captured-leech"})
        {
            Reset(p,m);
            using(PaladinThorns.Attempt(p,m,false))
            {
                if(route=="typed"){SkillCombat.HitParts(p,m,new(20,0,0,0,0),false,weapon:true);}
                else if(route=="native"){AOS.Damage(m,p,20,false,100,0,0,0,0,weaponDamage:true);}
                else{GearEffects.Strike(p,m,0,new NoCritical(),wearWeapon:false,preparedDamage:new(20,0,0,0,0),sourceDrain:route=="captured-leech"?new PlayerWeaponDrain(10,0):null);}
            }
            int expected=Expected(5120,7680,2560,route=="captured-leech"?512:0);
            Require(p is{Alive:true,Deleted:false}&&SkillHealth.Balance256(p)==expected&&SkillHealth.Balance256(m)==2560,
                $"Lethal return prevented pre-return healing or repeated phase: {route} hp={SkillHealth.Balance256(p)} expected={expected} target={SkillHealth.Balance256(m)}");
        }
        // A saved aura/curse must produce the same phase after each real reload.
        Reset(p,m);SkillHealth.SetBalance256(m,5*256);int seen=-1;
        PreparedWeaponEvents.BeforeReturn(p,m,20*256,false,10*256,clipped=>seen=clipped);
        Require(seen==5*256&&SkillHealth.Balance256(m)==15*256&&SkillHealth.Balance256(p)==Expected(5120,1280,2560),"Absorb healing expanded the already-clipped return or Life Tap used clipped damage");
        Reset(p,m);PreparedWeaponEvents.BeforeReturn(p,m,20*256,true);
        Require(SkillHealth.Balance256(p)==20*256&&SkillHealth.Balance256(m)==30*256,"Ranged recovery generated melee return");
        Reset(p,m);using(PaladinThorns.Attempt(p,m,false)){}
        Require(SkillHealth.Balance256(p)==6*256&&SkillHealth.Balance256(m)==30*256,"Miss/no damage gained healing or lost flat attempt return");
        Reset(p,m);using(PaladinThorns.Attempt(p,m,false)){PreparedWeaponEvents.BeforeReturn(p,m,0,false);}
        Require(SkillHealth.Balance256(p)==6*256,"Zero physical recovered or returned flat twice");
        // A real item supplies default root-melee leech, not only the explicit drain route.
        var ring=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Manald Heal").Id,99,new NoCritical(),true);
        try
        {
            Require(p.EquipItem(ring),"Default melee leech item");ClassGear.Refresh(p);Reset(p,m);NecromancerEffects.SetMana256(p,1*256);
            int percent=WeaponLeech.Capture(p).ManaPercent;Require(percent>0,"Actual mana-steal item not active");
            using(PaladinThorns.Attempt(p,m,false)){GearEffects.Strike(p,m,0,new NoCritical(),wearWeapon:false,preparedDamage:new(20,0,0,0,0));}
            Require(NecromancerEffects.ManaBalance256(p)==256+20*256*percent/100&&SkillHealth.Balance256(p)==Expected(5120,7680,2560),"Default melee used post-hit integer leech or repeated Life Tap");
        }
        finally{ring.Delete();ClassGear.Refresh(p);}
    }
    private static void Save(PlayerMobile p,GraveyardZombie m)
    {
        Reset(p,m);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,m.Serial.Value,SkillHealth.Balance256(p),SkillHealth.Balance256(m),p.Location,PaladinAuras.Effect(m,CombatAbility.Thorns),NecromancerEffects.Curse(m))));
    }
    private static void Check()
    {
        var p=Owner();var m=Target(p);bool saved=false;
        try{Cases(p,m);Save(p,m);saved=true;Log.Information("Prepared recovery: native/typed/root melee lethal healing rescue,captured/default fractional leech,pre-absorb return clip,ranged/miss/zero controls pass; full HP/death event chronology remains separate");}
        finally{if(!saved){End(p,m);}}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var m=(GraveyardZombie)World.FindMobile((Serial)s.Target);
        Require(p is{Alive:true,Deleted:false}&&m is{Alive:true,Deleted:false}&&SkillHealth.Balance256(p)==s.OwnerLife&&SkillHealth.Balance256(m)==s.TargetLife
            &&PaladinAuras.Effect(m,CombatAbility.Thorns)==s.Thorns&&NecromancerEffects.Curse(m)==s.Tap,$"Saved recovery fixture changed: owner={SkillHealth.Balance256(p)}/{s.OwnerLife},target={SkillHealth.Balance256(m)}/{s.TargetLife},thorns={PaladinAuras.Effect(m,CombatAbility.Thorns)==s.Thorns},tap={NecromancerEffects.Curse(m)==s.Tap}");
        p.MoveToWorld(s.Origin,Map.Trammel);m.Frozen=m.CantWalk=true;m.AIObject.AITimer.Stop();
        Cases(p,m);Save(p,m);
    }
}
