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

public static class NpcResourceCallbackChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcResourceCallbackChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,int Life,int Mana,int Stamina,int CurrentLife,int CurrentMana);
    private sealed record Saved(uint Owner,Stored[] Actors,int Generation);
    private sealed record Legacy(uint Owner,uint Actor,uint Item,string Gear,int Life,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-resource-callbacks-fixtures.json");
    private static string LegacyManifest=>Path.Join(Core.BaseDirectory,"npc-resource-callbacks-legacy-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("npc-resource-callbacks-check",Check);Register("npc-resource-callbacks-reload-check",Reload);
        Register("npc-resource-callbacks-legacy-create-check",LegacyCreate);Register("npc-resource-callbacks-legacy-upgrade-check",LegacyContinue);Register("npc-resource-callbacks-legacy-reload-check",LegacyContinue);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"NPC current-resource maximum-change callbacks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-resource-callbacks-source-cases.json")));int n=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {Require(BattleOrders.Rescale256(row[4].GetInt32(),row[2].GetInt32(),row[3].GetInt32())==row[5].GetInt32(),"Unchanged source resource callback mismatch at "+n);n++;}
        Require(n==1080,"Missing resource callback controls");
    }
    private static Item Named(string id){Require(NamedGear.Available(id),"Unavailable callback original "+id);return NamedGear.Create(id,99,new Random(317),true);}
    private static PlayerMobile Owner()
    {
        var owner=new PlayerMobile{Name="NpcResourceCallbackOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Callback owner prerequisite failed");FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);return owner;
    }
    private static SkeletonGuardian Pet(PlayerMobile owner,Item item,bool zeroMana=false)
    {
        var actor=new SkeletonGuardian{Name="NPC resource callback holder [prepared]"};actor.Initialize(owner,4,0);actor.ControlSlots=0;actor.NoKillAwards=true;
        if(!zeroMana){actor.SetMana(20);}
        Require(actor.SetControlMaster(owner),"Callback holder ownership failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(actor);
        item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Callback original bind failed: "+why);return actor;
    }
    private static Stored Store(SkeletonGuardian actor)
    {
        var p=BattleOrders.Capture(actor);var binding=IronGolemItems.State(actor);
        return new(actor.Serial.Value,binding.Item,JsonSerializer.Serialize(ClassGear.State(World.FindItem((Serial)binding.Item))),p.ExactLifeMaximum256,p.ExactManaMaximum256,p.ExactStaminaMaximum256,p.Life256,p.Mana256);
    }
    private static void Partial(SkeletonGuardian actor)
    {
        SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)/2+1);
        NecromancerEffects.SetMana256(actor,NecromancerEffects.ManaMaximum256(actor)/2+1);
        BattleOrders.SetStamina256(actor,BattleOrders.StaminaMaximum256(actor)*11/12+1);
    }
    private static void Expected(SkeletonGuardian actor,BattleOrders.Pools before,string context)
    {
        var after=BattleOrders.Capture(actor);
        int life=BattleOrders.Rescale256(before.Life256,before.ExactLifeMaximum256,after.ExactLifeMaximum256);
        int mana=BattleOrders.Rescale256(before.Mana256,before.ExactManaMaximum256,after.ExactManaMaximum256);
        int stamina=BattleOrders.Rescale256(before.Stamina256,before.ExactStaminaMaximum256,after.ExactStaminaMaximum256);
        Require(after.Life256==Math.Clamp(life,0,after.ExactLifeMaximum256)&&after.Mana256==Math.Clamp(mana,0,after.ExactManaMaximum256)&&after.Stamina256==Math.Clamp(stamina,0,after.ExactStaminaMaximum256),context+": "+JsonSerializer.Serialize(new{before,after,life,mana,stamina}));
    }
    private static void Transitions(SkeletonGuardian actor,Item item)
    {
        var saved=ClassGear.State(item);Partial(actor);var before=BattleOrders.Capture(actor);
        ClassGear.Profiles.Store(item,saved with{Identified=false});Expected(actor,before,"Identification downscale");
        before=BattleOrders.Capture(actor);ClassGear.Profiles.Store(item,saved);Expected(actor,before,"Identification upscale");
        Partial(actor);before=BattleOrders.Capture(actor);
        if(item is BaseWeapon w)
        {int hp=w.HitPoints;w.HitPoints=0;Expected(actor,before,"Weapon break raw-balance callback");before=BattleOrders.Capture(actor);w.HitPoints=hp;Expected(actor,before,"Weapon repair callback");}
        else if(item is BaseArmor a)
        {int hp=a.HitPoints;a.HitPoints=0;Expected(actor,before,"Armor break raw-balance callback");before=BattleOrders.Capture(actor);a.HitPoints=hp;Expected(actor,before,"Armor repair callback");}
        else if(item is BaseClothing c)
        {int hp=c.HitPoints;c.HitPoints=0;Expected(actor,before,"Clothing break raw-balance callback");before=BattleOrders.Capture(actor);c.HitPoints=hp;Expected(actor,before,"Clothing repair callback");}
        else{throw new InvalidOperationException("Original omitted the native durability callback path: "+item.GetType().Name);}
        before=BattleOrders.Capture(actor);ClassGear.Profiles.Store(item,saved);Expected(actor,before,"Unchanged store");
        var observed=BattleOrders.ObservedNpcMaximums(actor);Require(observed!=null&&observed.Life256==SkillHealth.Maximum256(actor)&&observed.Mana256==NecromancerEffects.ManaMaximum256(actor)&&observed.Stamina256==BattleOrders.StaminaMaximum256(actor),"Exact maximum observation was lost");
        // Positive sub-HP input keeps the source minimum1FP;empty pools stay empty.
        SkillHealth.SetBalance256(actor,1);NecromancerEffects.SetMana256(actor,0);BattleOrders.SetStamina256(actor,0);before=BattleOrders.Capture(actor);
        ClassGear.Profiles.Store(item,saved with{Identified=false});Expected(actor,before,"Tiny/empty downscale");Require(actor.Alive&&SkillHealth.Balance256(actor)==1&&NecromancerEffects.ManaBalance256(actor)==0&&BattleOrders.Stamina256(actor)==0,"Tiny/empty callback invented a refill or killed its target");
        before=BattleOrders.Capture(actor);ClassGear.Profiles.Store(item,saved);Expected(actor,before,"Tiny/empty upscale");Partial(actor);
    }
    private static void Check()
    {
        SourceCases();var owner=Owner();var items=new[]{Named("unique/206"),Named("unique/106"),Named("unique/241"),Named("unique/173")};var actors=items.Select(x=>Pet(owner,x)).ToArray();
        Require(Store(actors[0]).Life==20864&&Store(actors[0]).Mana==55*256&&Store(actors[1]).Mana==28*256&&Store(actors[2]).Stamina==120*256&&Store(actors[3]).Life==23104,"Actual max-resource goldens differ");
        foreach(var actor in actors){Require(SkillHealth.Full(actor)&&NecromancerEffects.ManaBalance256(actor)==NecromancerEffects.ManaMaximum256(actor),"Fresh full pools did not remain full on attachment");}
        for(int i=0;i<actors.Length;i++){Transitions(actors[i],items[i]);}
        var zero=Pet(owner,Named("unique/206"),true);Require(NecromancerEffects.ManaMaximum256(zero)==35*256&&NecromancerEffects.ManaBalance256(zero)==0,"Native Int fallback invented source-zero mana on attachment");zero.Delete();
        var retired=Pet(owner,Named("unique/206"));Partial(retired);var before=BattleOrders.Capture(retired);IronGolemItems.DestroyBoundItem(retired);Expected(retired,before,"Original removal");retired.Delete();
        var rows=actors.Select(Store).ToArray();PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows,0)));
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        foreach(var row in saved.Actors)
        {
            var actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);var now=Store(actor);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&item.Parent==actor&&now.Gear==row.Gear&&now.Life==row.Life&&now.Mana==row.Mana&&now.Stamina==row.Stamina&&now.CurrentLife==row.CurrentLife&&now.CurrentMana==row.CurrentMana,"Reload changed exact pools/original identity/full rolls");
            actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();Transitions(actor,item);
        }
        var rows=saved.Actors.Select(x=>Store((SkeletonGuardian)World.FindMobile((Serial)x.Actor))).ToArray();PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Actors=rows,Generation=saved.Generation+1}));
    }
    private static void LegacyCreate()
    {
        var owner=Owner();var item=Named("unique/206");var actor=Pet(owner,item);Partial(actor);
        string ledger=JsonSerializer.Serialize(BattleOrders.Snapshot().Resources.Single(x=>x.Recipient==actor.Serial.Value));Require(!ledger.Contains("NpcMaximums"),"Old producer already has new resource observations");
        var saved=new Legacy(owner.Serial.Value,actor.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),SkillHealth.Balance256(actor),NecromancerEffects.ManaBalance256(actor),0);
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(LegacyManifest,JsonSerializer.Serialize(saved));
    }
    private static void LegacyContinue()
    {
        var saved=JsonSerializer.Deserialize<Legacy>(File.ReadAllText(LegacyManifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var actor=(SkeletonGuardian)World.FindMobile((Serial)saved.Actor);var item=World.FindItem((Serial)saved.Item);
        Require(actor.ControlMaster==owner&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==saved.Gear&&SkillHealth.Balance256(actor)==saved.Life&&NecromancerEffects.ManaBalance256(actor)==saved.Mana,"Old observation migration changed saved pools/original");
        Transitions(actor,item);PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(LegacyManifest,JsonSerializer.Serialize(saved with{Life=SkillHealth.Balance256(actor),Mana=NecromancerEffects.ManaBalance256(actor),Generation=saved.Generation+1}));
    }
}
