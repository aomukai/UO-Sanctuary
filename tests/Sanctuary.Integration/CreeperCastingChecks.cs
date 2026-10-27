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

public static class CreeperCastingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperCastingChecks));
    private sealed record Saved(uint Owner,uint Body,uint Wolf,uint Raven,CreeperBodyState State,int Mana,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"creeper-casting-fixtures.json");private static int _scene;private static bool _creating;
    public static void Configure()
    {
        Register("creeper-casting-check",Check);Register("creeper-casting-reload-check",Reload);
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        // Use the existing native eligibility hook, which also suppresses a
        // queued first timer tick. Scope it to this inspection fixture only.
        var old=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateMana=p=>!(_creating||p.Name?.StartsWith("CreeperCastFixture",StringComparison.Ordinal)==true)&&old?.Invoke(p)!=false;
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared do115payment/placement/shared-vine transaction",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="CreeperCastFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);
        ClassCreation.Profiles.AdvanceLevel(p,99);Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.poisoncreeper",out _),"Train Creeper");
        FixtureSpace.Corridor(p,7040,24,3200+32*_scene++);NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static void End(PlayerMobile p){DruidCreeperGround.Cancel(p);PersistentMinions.Dismiss(p);p.Corpse?.Delete();p.Delete();}
    private static DruidCreeperBody Commit(PlayerMobile p,Serial target,Point3D point,DateTime at)
    {
        int mana=NecromancerEffects.ManaBalance256(p);Require(DruidCreeperCasting.Commit(p,target,point,at,out var body),"Prepared vine transaction");
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p&&NecromancerEffects.ManaBalance256(p)==mana-2048,"Exactly8mana or ownership");return body;
    }
    private static void AtomicAndPlacement()
    {
        var p=Fresh();var spawn=DruidCreeperCasting.Spawn;var at=TimedEffects.Now.AddDays(2);var aim=new Point3D(p.X+3,p.Y,p.Z);
        var enemy=new GraveyardZombie{CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(aim,p.Map);enemy.AIObject.AITimer.Stop();
        try
        {
            Require(!DruidCombat.Implemented(CombatAbility.PoisonCreeper)&&!ClassCombat.Available(p,CombatAbility.PoisonCreeper),"Prepared transaction exposed unfinished learned skill");
            Require(!p.Map.CanSpawnMobile(aim)&&PetGroundWorld.ZeroFootprint(p.Map,aim),"Occupied point fixture did not distinguish zero footprint");
            var first=Commit(p,enemy.Serial,p.Location,at);Require(first.Location==aim,"Zero-footprint vine was displaced from its selected unit");SkillHealth.SetBalance256(first,1201);
            int mana=NecromancerEffects.ManaBalance256(p);DruidCreeperCasting.Spawn=(_,_,_,_)=>null;
            Require(!DruidCreeperCasting.Commit(p,enemy.Serial,aim,at,out _)&&!first.Deleted&&SkillHealth.Balance256(first)==1201&&NecromancerEffects.ManaBalance256(p)==mana,"Allocation failure paid/deleted/healed the old vine");
            DruidCreeperBody failed=null;DruidCreeperCasting.Spawn=(owner,point,time,seed)=>
            {failed=spawn(owner,point,time,seed);NecromancerEffects.SetMana256(owner,2047);return failed;};
            Require(!DruidCreeperCasting.Commit(p,Serial.Zero,aim,at,out _)&&failed is{Deleted:true}&&!first.Deleted&&SkillHealth.Balance256(first)==1201,"Payment failure replaced the original vine");
            DruidCreeperCasting.Spawn=spawn;
            Require(!DruidCreeperCasting.Commit(p,Serial.Zero,aim,at,out _)&&NecromancerEffects.ManaBalance256(p)==2047&&!first.Deleted,"Fractional low mana did not preserve vine");
            NecromancerEffects.SetMana256(p,2048);var second=Commit(p,enemy.Serial,p.Location,at);Require(first.Deleted&&second.Location==aim&&NecromancerEffects.ManaBalance256(p)==0,"Successful one-vine replacement or exact low-mana payment");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);mana=NecromancerEffects.ManaBalance256(p);
            Require(!DruidCreeperCasting.Commit(p,Serial.Zero,new(65530,65530,0),at,out _)&&!second.Deleted&&NecromancerEffects.ManaBalance256(p)==mana,"Invalid placement paid or replaced");
            p.Frozen=true;Require(!DruidCreeperCasting.Commit(p,Serial.Zero,aim,at,out _),"Frozen actor committed vine");p.Frozen=false;
            enemy.MoveToWorld(new(aim.X+1,aim.Y,aim.Z),p.Map);var third=Commit(p,enemy.Serial,aim,at);Require(second.Deleted&&third.Location==enemy.Location,"Selected target did not update its destination");
            enemy.Delete();var fourth=Commit(p,enemy.Serial,aim,at);Require(third.Deleted&&fourth.Location==aim,"Deleted target did not use cached point");
            Require(DruidVines.Source("plaguepoppy")&&DruidVines.Source("cycleoflife")&&DruidVines.Source("vinecreature")&&!DruidVines.Source("spiritwolf"),"Shared source vine category");
        }
        finally{DruidCreeperCasting.Spawn=spawn;enemy.Delete();End(p);}
    }
    private static void Town()
    {
        var p=Fresh();try
        {
            p.MoveToWorld(new(1420,1698,0),Map.Trammel);p.Z=p.Map.GetAverageZ(p.X,p.Y);
            Require(SpellHelper.IsTown(p.Location,p),"Town fixture");var body=Commit(p,Serial.Zero,p.Location,TimedEffects.Now.AddDays(2));
            Require(body.Map==p.Map&&SpellHelper.IsTown(body.Location,p),"Current InTown1summon was refused");
        }
        finally{End(p);}
    }
    private static void Check()
    {
        AtomicAndPlacement();Town();var p=Fresh();var at=TimedEffects.Now.AddDays(2);
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.raven",out _),"Train Raven");
        if(ClassCreation.Profiles.GetRank(p.Serial,"druid.oaksage")==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.oaksage",out _),"Train Oak prerequisite");}
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.summonspiritwolf",out _),"Train Spirit Wolf");
        var wolf=DruidSpiritWolfWorld.Create(p,new(p.X+5,p.Y,p.Z),at,152);
        var raven=DruidRavenBody.CreatePrepared(p,new(p.X+5,p.Y,p.Z),at,153,enforce:false);
        var first=Commit(p,Serial.Zero,new(p.X+3,p.Y,p.Z),at);var second=Commit(p,Serial.Zero,new(p.X+4,p.Y,p.Z),at);
        Require(first.Deleted&&!wolf.Deleted&&!raven.Deleted,"Vine cap deleted another summon family");
        var point=p.Location;var map=p.Map.MapID;PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,second.Serial.Value,wolf.Serial.Value,raven.Serial.Value,second.Prepared,NecromancerEffects.ManaBalance256(p),map,point)));
        Log.Information("Creeper prepared cast:8mana, occupied zero-footprint point, allocation/payment rollback, fractional mana, selected/deleted targets, town, shared-vine category and other-family coexistence pass; learned input remains gated");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Body) as DruidCreeperBody;
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p,"Paid vine/owner did not survive reload");
        Require(body.SavedText==JsonSerializer.Serialize(saved.State),"Paid vine state changed across reload");
        Require(NecromancerEffects.ManaBalance256(p)==saved.Mana,$"Paid vine mana changed across reload: {NecromancerEffects.ManaBalance256(p)} vs {saved.Mana}");
        Require(World.FindMobile((Serial)saved.Wolf) is{Alive:true,Deleted:false},"Coexisting wolf was removed across reload");
        Require(World.FindMobile((Serial)saved.Raven) is{Alive:true,Deleted:false},"Coexisting raven was removed across reload");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        var next=Commit(p,Serial.Zero,new(p.X+4,p.Y,p.Z),TimedEffects.Now.AddDays(2));Require(body.Deleted,"Post-load paid replacement retained previous vine");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Body=next.Serial.Value,State=next.Prepared,Mana=NecromancerEffects.ManaBalance256(p)}));
    }
}
