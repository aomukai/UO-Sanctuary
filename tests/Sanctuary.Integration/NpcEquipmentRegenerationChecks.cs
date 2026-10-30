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

public static class NpcEquipmentRegenerationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentRegenerationChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,int ItemRate,int Balance,TimedEffectState Poison);
    private sealed record Saved(uint Owner,uint Source,Stored[] Actors,int Generation);
    private sealed record Legacy(uint Owner,uint Source,uint Actor,uint Item,string Gear,int Balance,string Poison,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-regeneration-fixtures.json");
    private static string LegacyManifest=>Path.Join(Core.BaseDirectory,"npc-equipment-regeneration-legacy-fixtures.json");
    public static void Configure()
    {
        Register("npc-equipment-regeneration-check",Check);Register("npc-equipment-regeneration-reload-check",Reload);
        Register("npc-equipment-regeneration-legacy-create-check",LegacyCreate);Register("npc-equipment-regeneration-legacy-upgrade-check",LegacyContinue);Register("npc-equipment-regeneration-legacy-reload-check",LegacyContinue);
    }
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"NPC item replenish and signed poison regeneration",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equipment-regeneration-source-cases.json")));int n=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var i=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray();int delta=NpcEquipmentRegeneration.Delta(i[1],i[2],i[3],i[4]!=0,i[5]!=0);
            int hp=delta<0?PoisonStates.BalanceAfter(i[0],-(long)delta,false,i[6]!=0):Math.Min(1024,i[0]+delta);
            Require(hp==row[1].GetInt32(),"Full source signed HP regeneration differs at "+n);n++;
        }
        Require(n==1920,"Missing full source regeneration cases");
    }
    private static Item Named(string id){Require(NamedGear.Available(id),"Unavailable regeneration original "+id);return NamedGear.Create(id,99,new Random(317),true);}
    private static PlayerMobile Owner()
    {
        var owner=new PlayerMobile{Name="NpcRegenOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Regen holder prerequisite failed");FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);return owner;
    }
    private static SkeletonGuardian Pet(PlayerMobile owner,Item item)
    {
        var actor=new SkeletonGuardian{Name="NPC regeneration holder [prepared]"};actor.Initialize(owner,7,2);actor.ControlSlots=0;actor.NoKillAwards=true;
        Require(actor.SetControlMaster(owner),"Regen holder ownership failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);PersistentMinions.Profiles.Track(actor);actor.AIObject.AITimer.Stop();
        item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Regen original bind failed: "+why);return actor;
    }
    private static TimedEffectState State(Mobile actor)=>TimedEffects.Snapshot().Single(x=>x.Owner==actor.Serial.Value);
    private static void Healthy(SkeletonGuardian actor,Item item)
    {
        int rate=NpcEquipmentRegeneration.ItemRate(actor);var saved=ClassGear.State(item);
        ClassGear.Profiles.Store(item,saved with{Identified=false});Require(NpcEquipmentRegeneration.ItemRate(actor)==0,"Unidentified replenish remained active");ClassGear.Profiles.Store(item,saved);
        if(item is BaseWeapon w){int hp=w.HitPoints;w.HitPoints=0;Require(NpcEquipmentRegeneration.ItemRate(actor)==0,"Broken replenish remained active");w.HitPoints=hp;}
        if(item is BaseArmor a){int hp=a.HitPoints;a.HitPoints=0;Require(NpcEquipmentRegeneration.ItemRate(actor)==0,"Broken armor replenish remained active");a.HitPoints=hp;}
        TimedEffects.Clear(actor);SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)-1024);actor.ResetLifeRecovery();long now=Core.TickCount;int before=SkillHealth.Balance256(actor),basis=BattleOrders.Regeneration256(actor);
        actor.RecoverLife(now);actor.RecoverLife(now+39);Require(SkillHealth.Balance256(actor)==before,"Replenishment healed before40ms");actor.RecoverLife(now+40);
        Require(SkillHealth.Balance256(actor)==before+basis+rate,"Natural and item replenish did not combine as FP256 per frame");
        SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)-1);actor.RecoverLife(now+80);Require(SkillHealth.Full(actor),"Replenishment failed exact maximum clamp");
        actor.ResetLifeRecovery();
    }
    private static void Signed(SkeletonGuardian actor,Mobile source)
    {
        int item=NpcEquipmentRegeneration.ItemRate(actor);
        foreach(int poison in new[]{Math.Max(1,item-4),item,item+20})foreach(bool blocked in new[]{false,true})
        {
            TimedEffects.Clear(actor);int before=SkillHealth.Maximum256(actor)-512;SkillHealth.SetBalance256(actor,before);DateTime at=TimedEffects.Now;
            if(blocked){TimedEffects.PreventHealing(actor,at);}
            Require(TimedEffects.PoisonFrames(source,actor,poison,13,at),"Signed poison application refused");TimedEffects.Settle(actor,at.AddMilliseconds(39));Require(SkillHealth.Balance256(actor)==before,"Signed sum committed before40ms");
            TimedEffects.Settle(actor,at.AddMilliseconds(40));int delta=NpcEquipmentRegeneration.Delta(0,item,poison,true,blocked);
            Require(SkillHealth.Balance256(actor)==before+delta,"Poison/replenishment/prevent-heal did not commit one signed frame");
        }
        TimedEffects.Clear(actor);SkillHealth.SetBalance256(actor,25);DateTime edge=TimedEffects.Now;
        Require(TimedEffects.PoisonFrames(source,actor,item+20,13,edge),"Lethal edge poison refused");TimedEffects.Settle(actor,edge.AddMilliseconds(40));
        Require(actor.Alive&&SkillHealth.Balance256(actor)==5,"Poison killed before the replenish offset was considered");TimedEffects.Clear(actor);
    }
    private static Stored Stage(SkeletonGuardian actor,Mobile source,bool aggregate)
    {
        TimedEffects.Clear(actor);SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)-512);DateTime at=TimedEffects.Now;
        Require(aggregate?TimedEffects.Poison(source,actor,13,520,at):TimedEffects.PoisonFrames(source,actor,40,13,at),"Staged poison refused");
        int before=SkillHealth.Balance256(actor);TimedEffects.Settle(actor,at.AddMilliseconds(39));var state=State(actor);
        Require(SkillHealth.Balance256(actor)==before&&(aggregate?state.NpcPoisonElapsedMs:state.PoisonClock.ElapsedMs)==39,"Pending source/aggregate frame phase lost");
        foreach(var invalid in new[]{state with{NpcPoisonElapsedMs=-1},state with{NpcPoisonElapsedMs=40},state with{PoisonMs=0,PoisonClock=null,NpcPoisonElapsedMs=0},state with{PoisonClock=new(1,0),NpcPoisonElapsedMs=0}})
        {bool refused=false;try{TimedEffectProfiles.Validate(invalid);}catch(InvalidDataException){refused=true;}Require(refused,"Invalid/dual/inactive poison phase accepted");}
        var held=state with{UpdatedUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?held:x).ToArray());
        var binding=IronGolemItems.State(actor);return new(actor.Serial.Value,binding.Item,JsonSerializer.Serialize(ClassGear.State(World.FindItem((Serial)binding.Item))),NpcEquipmentRegeneration.ItemRate(actor),before,held);
    }
    private static void Check()
    {
        SourceCases();var owner=Owner();owner.AddItem(Named("unique/121"));Require(ClassGear.Bonus(owner,"replenish-life")>0,"Positive owner exclusion control missing");var source=new NpcPoisonRecipient();source.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();
        var items=new[]{Named("unique/134"),Named("unique/228"),Named("unique/176")};var actors=items.Select(x=>Pet(owner,x)).ToArray();
        Require(actors.Select(NpcEquipmentRegeneration.ItemRate).SequenceEqual(new[]{20,15,20}),"Actual replenish item goldens differ");
        for(int i=0;i<actors.Length;i++){Healthy(actors[i],items[i]);Signed(actors[i],source);}
        var rows=actors.Select((x,i)=>Stage(x,source,i==1)).ToArray();
        var retired=Pet(owner,Named("unique/134"));TimedEffects.Clear(retired);SkillHealth.SetBalance256(retired,SkillHealth.Maximum256(retired)-512);DateTime removal=TimedEffects.Now;
        Require(TimedEffects.Poison(source,retired,13,520,removal),"Removal poison refused");TimedEffects.Settle(retired,removal.AddMilliseconds(39));int before=SkillHealth.Balance256(retired);
        IronGolemItems.DestroyBoundItem(retired);Require(!NpcEquipmentRegeneration.Handles(retired)&&NpcEquipmentRegeneration.ItemRate(retired)==0,"Removal retained replenishment");
        TimedEffects.Settle(retired,removal.AddMilliseconds(40));Require(SkillHealth.Balance256(retired)==before-256&&State(retired).NpcPoisonElapsedMs==0,"Removal erased pending poison frame or kept item offset");
        TimedEffects.Settle(retired,removal.AddMilliseconds(41));Require(SkillHealth.Balance256(retired)==before-256,"Removed original reverted an active frame clock to continuous poison");retired.Delete();
        PersistentMinions.Suspend(owner);owner.Internalize();source.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,source.Serial.Value,rows,0)));
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var source=World.FindMobile((Serial)saved.Source);FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);source.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        var rows=new Stored[saved.Actors.Length];
        for(int i=0;i<rows.Length;i++)
        {
            var old=saved.Actors[i];var actor=(SkeletonGuardian)World.FindMobile((Serial)old.Actor);var item=World.FindItem((Serial)old.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==old.Gear&&NpcEquipmentRegeneration.ItemRate(actor)==old.ItemRate,"Regen original identity/full rolls changed");
            Require(SkillHealth.Balance256(actor)==old.Balance&&JsonSerializer.Serialize(State(actor))==JsonSerializer.Serialize(old.Poison),"Pending poison/regeneration phase or balance changed on reload");
            DateTime at=TimedEffects.Now;var resumed=old.Poison with{UpdatedUtc=at};TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?resumed:x).ToArray());TimedEffects.Settle(actor,at.AddMilliseconds(1));
            int rate=old.Poison.PoisonClock?.Rate256??256;Require(SkillHealth.Balance256(actor)==old.Balance+old.ItemRate-rate,"Reload repaid/lost pending combined frame");
            actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();Healthy(actor,item);Signed(actor,source);rows[i]=Stage(actor,source,i==1);
        }
        PersistentMinions.Suspend(owner);owner.Internalize();source.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Actors=rows,Generation=saved.Generation+1}));
    }
    private static void LegacyCreate()
    {
        var owner=Owner();var item=Named("unique/134");var actor=Pet(owner,item);var source=new NpcPoisonRecipient();source.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();
        TimedEffects.Clear(actor);SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)-512);DateTime at=TimedEffects.Now;Require(TimedEffects.Poison(source,actor,13,520,at),"Old aggregate poison refused");TimedEffects.Settle(actor,at.AddMilliseconds(39));
        var held=State(actor) with{UpdatedUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};string json=JsonSerializer.Serialize(held);Require(!json.Contains("NpcPoisonElapsedMs"),"Old producer is not old timed-state format");
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?held:x).ToArray());
        var saved=new Legacy(owner.Serial.Value,source.Serial.Value,actor.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),SkillHealth.Balance256(actor),json,0);
        PersistentMinions.Suspend(owner);owner.Internalize();source.Internalize();File.WriteAllText(LegacyManifest,JsonSerializer.Serialize(saved));
    }
    private static void LegacyContinue()
    {
        var saved=JsonSerializer.Deserialize<Legacy>(File.ReadAllText(LegacyManifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var actor=(SkeletonGuardian)World.FindMobile((Serial)saved.Actor);var item=World.FindItem((Serial)saved.Item);var source=World.FindMobile((Serial)saved.Source);var state=State(actor);
        Require(item.Parent==actor&&actor.ControlMaster==owner&&JsonSerializer.Serialize(ClassGear.State(item))==saved.Gear&&SkillHealth.Balance256(actor)==saved.Balance,"Old world changed original/rolls/exact balance");
        var expected=JsonSerializer.Deserialize<TimedEffectState>(saved.Poison);Require(JsonSerializer.Serialize(state)==JsonSerializer.Serialize(expected)&&state.NpcPoisonElapsedMs==(saved.Generation==0?(int?)null:39),"Old omitted field or saved new phase did not load correctly");
        DateTime at=TimedEffects.Now;var resumed=state with{UpdatedUtc=at};TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?resumed:x).ToArray());int elapsed=saved.Generation==0?40:1;
        TimedEffects.Settle(actor,at.AddMilliseconds(elapsed));Require(SkillHealth.Balance256(actor)==saved.Balance-236,"Old world combined poison/replenish resumed incorrectly");
        var held=Stage(actor,source,true);PersistentMinions.Suspend(owner);owner.Internalize();source.Internalize();File.WriteAllText(LegacyManifest,JsonSerializer.Serialize(saved with{Balance=held.Balance,Poison=JsonSerializer.Serialize(held.Poison),Generation=saved.Generation+1}));
    }
}
