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

public static class NpcEquipmentResourceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentResourceChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,int Life,int Mana,int Stamina,int CurrentLife,int CurrentMana,int CurrentStamina);
    private sealed record Saved(uint Owner,Stored[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-resources-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-equipment-resources-check",Check);Register("npc-equipment-resources-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC exact life/mana/stamina maxima",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equipment-resources-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int[] i=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            Require(NpcResourceValues.Calculate(new(i[0],i[1],i[2],i[3],i[4],i[5]<<8,i[6],i[7],i[8]))==row[1].GetInt32(),"Full source resource attachment/query differs at "+count);count++;
        }
        Require(count==1152,"Missing full resource source cases");
    }
    private static Item Named(string id){Require(NamedGear.Available(id),"Named item unavailable: "+id);return NamedGear.Create(id,99,new Random(317),true);}
    private static SkeletonGuardian Pet(PlayerMobile owner)
    {
        var actor=new SkeletonGuardian{Name="NPC resource holder [prepared]"};actor.Initialize(owner,4,0);actor.ControlSlots=0;actor.NoKillAwards=true;
        Require(actor.SetControlMaster(owner),"Prepared holder ownership failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        PersistentMinions.Profiles.Track(actor);actor.AIObject.AITimer.Stop();return actor;
    }
    private static int Max(SkeletonGuardian actor,int resource)=>NpcEquipmentResources.Maximum256(actor,resource)??throw new InvalidOperationException("Missing live original resource maximum");
    private static Stored Store(SkeletonGuardian actor)=>new(actor.Serial.Value,IronGolemItems.State(actor).Item,JsonSerializer.Serialize(ClassGear.State(World.FindItem((Serial)IronGolemItems.State(actor).Item))),
        Max(actor,0),Max(actor,2),Max(actor,1),SkillHealth.Balance256(actor),NecromancerEffects.ManaBalance256(actor),BattleOrders.Stamina256(actor));
    private static void Fractions(SkeletonGuardian actor)
    {
        int life=Max(actor,0),mana=Max(actor,2),stamina=Max(actor,1);
        Require(actor.HitsMax==(life+255)/256&&actor.ManaMax==(mana+255)/256&&actor.StamMax==(stamina+255)/256,"Native maxima lost source fractions or double-applied a percentage");
        SkillHealth.SetBalance256(actor,life);NecromancerEffects.SetMana256(actor,mana);BattleOrders.SetStamina256(actor,stamina);
        Require(SkillHealth.Balance256(actor)==life&&NecromancerEffects.ManaBalance256(actor)==mana&&BattleOrders.Stamina256(actor)==stamina,"Full exact resource balances were clipped by native projection");
        SkillHealth.SetBalance256(actor,life/2+1);NecromancerEffects.SetMana256(actor,mana==0?0:mana/2+1);BattleOrders.SetStamina256(actor,stamina/2+1);
        Require(SkillHealth.Balance256(actor)==life/2+1&&NecromancerEffects.ManaBalance256(actor)==(mana==0?0:mana/2+1)&&BattleOrders.Stamina256(actor)==stamina/2+1,"Fractional current pools lost low bits");
    }
    private static void Orders(PlayerMobile owner,SkeletonGuardian actor)
    {
        Fractions(actor);var before=BattleOrders.Capture(actor);BattleOrders.Apply(owner,actor,1,TimedEffects.Now);
        int life=Max(actor,0),mana=Max(actor,2),stamina=Max(actor,1);
        Require(SkillHealth.Balance256(actor)==BattleOrders.Rescale256(before.Life256,before.ExactLifeMaximum256,life)
            &&NecromancerEffects.ManaBalance256(actor)==BattleOrders.Rescale256(before.Mana256,before.ExactManaMaximum256,mana)
            &&BattleOrders.Stamina256(actor)==BattleOrders.Rescale256(before.Stamina256,before.ExactStaminaMaximum256,stamina),"BO composition/rescale ignored exact resource maxima");
        var auras=PaladinAuras.Snapshot();
        try
        {
            PaladinAuras.ElementalWeapon(new(actor.Serial.Value,owner.Serial.Value,CombatAbility.Vigor,1,0,TimedEffects.Now.AddMinutes(1),StaminaPercent:40));
            Require(Max(actor,1)==175*256&&actor.StamMax==175,"Vigor and BO did not share the active stamina percentage pool exactly once");
        }
        finally{PaladinAuras.Restore(auras);}
        Fractions(actor);before=BattleOrders.Capture(actor);BattleOrders.Clear(actor);
        Require(SkillHealth.Balance256(actor)==BattleOrders.Rescale256(before.Life256,before.ExactLifeMaximum256,Max(actor,0))
            &&NecromancerEffects.ManaBalance256(actor)==BattleOrders.Rescale256(before.Mana256,before.ExactManaMaximum256,Max(actor,2)),"BO removal clipped resource fractions");
    }
    private static void Disabled(SkeletonGuardian actor,Item item)
    {
        var saved=ClassGear.State(item);ClassGear.Profiles.Store(item,saved with{Identified=false});
        Require(Max(actor,0)==actor.SourceLife256&&Max(actor,2)==0,"Unidentified resource/attribute bonus remained active: "+JsonSerializer.Serialize(new{Item=saved.NamedItemId,Life=Max(actor,0),Expected=actor.SourceLife256,Mana=Max(actor,2),actor.HitsMaxSeed,Profile=actor.CombatProfile.Life,StrOffset=actor.GetStatOffset(StatType.Str),Original=actor.GetStatMod(item.Serial+"Str")?.Offset}));
        Require(SkillHealth.Balance256(actor)<=Max(actor,0)&&NecromancerEffects.ManaBalance256(actor)==0,"Disabled item did not clamp old balances");
        ClassGear.Profiles.Store(item,saved);Fractions(actor);
        if(item is BaseWeapon weapon)
        {int hits=weapon.HitPoints;weapon.HitPoints=0;Require(Max(actor,0)==actor.SourceLife256&&Max(actor,2)==0,"Broken original kept resource bonuses");weapon.HitPoints=hits;}
        else if(item is BaseArmor armor)
        {int hits=armor.HitPoints;armor.HitPoints=0;Require(Max(actor,0)==actor.SourceLife256&&Max(actor,2)==0,"Broken armor kept resource bonuses");armor.HitPoints=hits;}
        Fractions(actor);
    }
    private static Stored Bind(PlayerMobile owner,Item item)
    {
        var actor=Pet(owner);int oldLife=SkillHealth.Balance256(actor);item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Original resource bind failed: "+why);
        Require(SkillHealth.Balance256(actor)==Math.Min(oldLife,Max(actor,0)),"Storage attachment invented a heal or lost more life than the new cap");
        Disabled(actor,item);Orders(owner,actor);return Store(actor);
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="NpcEquipmentResourceOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Holder history prerequisite failed");FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        var rows=new[]{Bind(owner,Named("unique/26")),Bind(owner,Named("unique/36")),Bind(owner,Named("unique/206")),Bind(owner,Named("unique/94")),Bind(owner,Named("unique/173")),Bind(owner,Named("unique/262")),Bind(owner,Named("unique/106"))};
        Require(rows[0].Life==8064+15*256&&rows[1].Life==8064-10*256&&rows[2].Life==8064+50*256&&rows[2].Mana==35*256
            &&rows[3].Life==8064&&rows[3].Mana==0&&rows[4].Life==8064+47*10*32&&rows[5].Mana==47*16*32&&rows[6].Mana==0,"Actual flat/negative/attribute/per-level/zero-base percentage resource values differ");
        var percentage=(SkeletonGuardian)World.FindMobile((Serial)rows[6].Actor);percentage.SetMana(20);
        Require(Max(percentage,2)==28*256,"Item mana percentage ignored a nonzero base");
        BattleOrders.Apply(owner,percentage,1,TimedEffects.Now);Require(Max(percentage,2)==35*256,"Item/BO mana percentages multiplied instead of sharing the source pool");
        BattleOrders.Clear(percentage);percentage.SetMana(0);
        var actor=(SkeletonGuardian)World.FindMobile((Serial)rows[4].Actor);var before=BattleOrders.Capture(actor);
        var old=SummonDifficulties.Facets();
        try
        {
            SummonDifficulties.SetFacets(old.Where(x=>x.Map!=owner.Map.MapID).Append(new(owner.Map.MapID,MonsterDifficulty.Hell)).ToArray());
            SummonDifficulties.Sync(owner);Require(SkillHealth.Balance256(actor)==SummonDifficulties.Life256(before.Life256,before.ExactLifeMaximum256,Max(actor,0)),"Facet transition ignored item contribution or relative FP256 life");
        }
        finally{SummonDifficulties.SetFacets(old);SummonDifficulties.Sync(owner);}
        foreach(var row in rows){actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);Fractions(actor);}
        rows=rows.Select(x=>Store((SkeletonGuardian)World.FindMobile((Serial)x.Actor))).ToArray();
        var retired=Pet(owner);var consumed=Named("unique/206");consumed.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(IronGolemItems.TryAttach(owner,retired,consumed,out _),"Removal resource bind failed");
        Fractions(retired);IronGolemItems.DestroyBoundItem(retired);Require(NpcEquipmentResources.Maximum256(retired,0)==null&&retired.Mana==0&&SkillHealth.Balance256(retired)<=SkillHealth.Maximum256(retired),"Removed original retained resource state or invalid balances");retired.Delete();
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows,0)));Report();
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        foreach(var row in saved.Actors)
        {
            var actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==row.Gear,"Reload lost original resource item identity/full rolls");
            Require(Max(actor,0)==row.Life&&Max(actor,2)==row.Mana&&Max(actor,1)==row.Stamina,"Reload changed exact maxima");
            Require(NecromancerEffects.ManaBalance256(actor)==row.CurrentMana,"Reload clipped exact mana balance");
            // Existing skeleton recovery and native stamina recovery run while
            // parked;verify bounds,not an invented frozen recovery policy.
            Require(SkillHealth.Balance256(actor) is >0&&SkillHealth.Balance256(actor)<=row.Life&&BattleOrders.Stamina256(actor)<=row.Stamina,"Reload produced out-of-bounds life/stamina");
            actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();Fractions(actor);Disabled(actor,item);Orders(owner,actor);
        }
        var rows=saved.Actors.Select(x=>Store((SkeletonGuardian)World.FindMobile((Serial)x.Actor))).ToArray();PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Actors=rows,Generation=saved.Generation+1}));Report();
    }
    private static void Report()=>Log.Information("NPC resources:1152full source graph contexts;actual flat/negative/per-level resources,attribute exclusion,zero mana,FP256 bounds,BO/facet composition,disabled/removal and exact saved originals;regen/events,legacy upgrade and complete learned Iron Golem remain unfinished");
}
