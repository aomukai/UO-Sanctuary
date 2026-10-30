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

public static class NpcRegenBasisChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcRegenBasisChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,int Rate,int Maximum);
    private sealed record Saved(uint Owner,Stored[] Actors,int Generation);
    private sealed record Legacy(uint Owner,uint Actor,uint Item,string Gear,int Balance,int Rate,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-regen-basis-fixtures.json");
    private static string LegacyManifest=>Path.Join(Core.BaseDirectory,"npc-regen-basis-legacy-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("npc-regen-basis-check",Check);Register("npc-regen-basis-reload-check",Reload);
        Register("npc-regen-basis-legacy-create-check",LegacyCreate);Register("npc-regen-basis-legacy-upgrade-check",LegacyContinue);Register("npc-regen-basis-legacy-reload-check",LegacyContinue);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"NPC exact max-life regeneration callback basis",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-regen-basis-source-cases.json")));int n=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int effective=V(0)==1&&V(1)!=0&&V(2)!=0?V(3):0;
            Require(BattleOrders.RegenerationAfter(V(4),V(5),V(6),effective,V(7))==V(8),"Complete MAXHP callback branch regeneration mismatch at "+n);n++;
        }
        Require(n==3072,"Missing callback source controls");
    }
    private static Item Named(string id){Require(NamedGear.Available(id),"Unavailable basis original "+id);return NamedGear.Create(id,99,new Random(317),true);}
    private static PlayerMobile Owner()
    {
        var owner=new PlayerMobile{Name="NpcRegenBasisOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Basis owner prerequisite failed");FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);return owner;
    }
    private static SkeletonGuardian Pet(PlayerMobile owner,Item item)
    {
        var actor=new SkeletonGuardian{Name="NPC basis holder [prepared]"};actor.Initialize(owner,4,0);actor.ControlSlots=0;actor.NoKillAwards=true;
        Require(actor.SetControlMaster(owner),"Basis holder ownership failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(actor);
        item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Basis original bind failed: "+why);return actor;
    }
    private static int Rate(SkeletonGuardian actor)=>BattleOrders.Regeneration256(actor);
    private static Stored Store(SkeletonGuardian actor)=>new(actor.Serial.Value,IronGolemItems.State(actor).Item,
        JsonSerializer.Serialize(ClassGear.State(World.FindItem((Serial)IronGolemItems.State(actor).Item))),Rate(actor),NpcEquipmentResources.LifeForRegeneration(actor));
    private static void Updates(SkeletonGuardian actor,Item item,bool changesLife)
    {
        var saved=ClassGear.State(item);int active=changesLife?(4*(NpcEquipmentResources.LifeForRegeneration(actor)>>8)>>4):Rate(actor);
        ClassGear.Profiles.Store(item,saved with{Identified=false});Require(Rate(actor)==(changesLife?7:5),"Identification did not update/preserve the natural basis");
        ClassGear.Profiles.Store(item,saved);Require(Rate(actor)==active,"Reidentification basis differs");
        if(item is BaseWeapon w){int hp=w.HitPoints;w.HitPoints=0;Require(Rate(actor)==(changesLife?7:5),"Broken weapon basis differs");w.HitPoints=hp;}
        else if(item is BaseArmor a){int hp=a.HitPoints;a.HitPoints=0;Require(Rate(actor)==(changesLife?7:5),"Broken armor basis differs");a.HitPoints=hp;}
        Require(Rate(actor)==active,"Repair basis differs");ClassGear.Profiles.Store(item,saved);Require(Rate(actor)==active,"Unchanged store altered the basis");
        if(changesLife)
        {
            // Prepared observer control: exact maxima differ by1FP while their
            // native display is identical. This is not a new item-birth claim.
            var snapshot=BattleOrders.Snapshot();int exact=NpcEquipmentResources.LifeForRegeneration(actor);
            try
            {
                BattleOrders.Restore(snapshot with{Resources=snapshot.Resources.Select(x=>x.Recipient==actor.Serial.Value?x with{RegenerationLifeMaximum256=exact-1,Regeneration256=1}:x).ToArray()});
                Require((exact+255)/256==(exact-1+255)/256,"Prepared same-display control is invalid");BattleOrders.RefreshRegeneration(actor,exact);
                Require(Rate(actor)==active,"Exact changed maximum was lost behind native rounding");
            }
            finally{BattleOrders.Restore(snapshot);}
        }
    }
    private static void Orders(SkeletonGuardian actor)
    {
        var supplier=new PlayerMobile{Name="Actual Barbarian basis supplier",Body=0x190,Player=true};ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);
        int before=Rate(actor);
        try
        {
            BattleOrders.Apply(supplier,actor,1,TimedEffects.Now);Require(BattleOrders.Percent(actor)==35,"No real BO strength");
            int exact=NpcEquipmentResources.LifeForRegeneration(actor);Require(Rate(actor)==4*(exact>>8)>>4,"BO used a rounded display for natural regeneration");
            BattleOrders.Clear(actor);Require(Rate(actor)==before,"BO removal basis differs");
        }
        finally{BattleOrders.Clear(actor);supplier.Delete();}
    }
    private static void Check()
    {
        SourceCases();var owner=Owner();var items=new[]{Named("unique/95"),Named("unique/215"),Named("unique/36")};var actors=items.Select(x=>Pet(owner,x)).ToArray();
        Require(actors.Select(Rate).SequenceEqual(new[]{12,22,5}),"Actual source basis goldens differ");
        Require(actors[0].HitsMax==52&&actors[1].HitsMax==92&&4*actors[0].HitsMax>>4==13&&4*actors[1].HitsMax>>4==23,"Native ceiling trap missing");
        for(int i=0;i<actors.Length;i++){Updates(actors[i],items[i],i<2);if(i<2){Orders(actors[i]);}}
        var rows=actors.Select(Store).ToArray();var retired=Pet(owner,Named("unique/95"));IronGolemItems.DestroyBoundItem(retired);Require(Rate(retired)==7,"Removal did not recalculate source unbound life");retired.Delete();
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows,0)));
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        for(int i=0;i<saved.Actors.Length;i++)
        {
            var old=saved.Actors[i];var actor=(SkeletonGuardian)World.FindMobile((Serial)old.Actor);var item=World.FindItem((Serial)old.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&item.Parent==actor&&Store(actor)==old,"Saved basis/original identity/full rolls changed");
            var ledger=BattleOrders.Snapshot().Resources.Single(x=>x.Recipient==actor.Serial.Value);Require(ledger.RegenerationLifeMaximum256==old.Maximum,"Exact observed maximum did not persist");
            actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();Updates(actor,item,i<2);if(i<2){Orders(actor);}
        }
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void LegacyCreate()
    {
        var owner=Owner();var item=Named("unique/95");var actor=Pet(owner,item);Require(Rate(actor)==5,"Old producer already implements item basis callbacks");
        string ledger=JsonSerializer.Serialize(BattleOrders.Snapshot().Resources.Single(x=>x.Recipient==actor.Serial.Value));Require(!ledger.Contains("RegenerationLifeMaximum256"),"Old producer has new observation field");
        SkillHealth.SetBalance256(actor,20*256+17);var saved=new Legacy(owner.Serial.Value,actor.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),SkillHealth.Balance256(actor),Rate(actor),0);
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(LegacyManifest,JsonSerializer.Serialize(saved));
    }
    private static void LegacyContinue()
    {
        var saved=JsonSerializer.Deserialize<Legacy>(File.ReadAllText(LegacyManifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var actor=(SkeletonGuardian)World.FindMobile((Serial)saved.Actor);var item=World.FindItem((Serial)saved.Item);
        Require(actor.ControlMaster==owner&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==saved.Gear&&SkillHealth.Balance256(actor)==saved.Balance&&Rate(actor)==saved.Rate,"Old ledger upgrade changed original/balance or invented missing history");
        Updates(actor,item,true);Require(Rate(actor)==12,"First verified legacy max-life transition did not update basis");
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(LegacyManifest,JsonSerializer.Serialize(saved with{Rate=Rate(actor),Balance=SkillHealth.Balance256(actor),Generation=saved.Generation+1}));
    }
}
