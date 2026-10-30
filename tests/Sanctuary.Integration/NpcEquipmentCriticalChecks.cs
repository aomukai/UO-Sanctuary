// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcEquipmentCriticalChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentCriticalChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,NpcCriticalChances Chances,NpcEquipmentSeed Random);
    private sealed record Saved(uint Owner,Stored[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-critical-fixtures.json");
    private static string UpgradeManifest=>Path.Join(Core.BaseDirectory,"npc-equipment-critical-upgrade-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("npc-equipment-critical-check",Check);Register("npc-equipment-critical-reload-check",Reload);
        Register("npc-equipment-critical-upgrade-check",Upgrade);Register("npc-equipment-critical-upgrade-reload-check",UpgradeReload);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC critical stages and saved source damage stream",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private sealed class Rolls:Random
    {
        private readonly int[] _values;internal int Count;
        internal Rolls(int[] values)=>_values=values;
        public override int Next(int maxValue){Require(maxValue==100,"Player critical draw bound changed");return _values[Count++];}
    }
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equipment-critical-source-cases.json")));int count=0;
        int[][] modes=[[0,0,0,0],[99,0,99,0],[35,50,45,5],[99,99,99,99]];
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int[] i=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray(),o=row[1].EnumerateArray().Select(x=>x.GetInt32()).ToArray();int draws=0;
            uint Raw()=>checked((uint)modes[i[6]][draws++]);
            var chances=new NpcCriticalChances(i[2],i[3],i[4],i[5]);
            bool physical=NpcEquipmentCritical.Physical(chances,Raw,i[1]!=0,i[0]!=0);int before=draws;
            var packet=NpcEquipmentCritical.ApplyIntrinsic((i[0]!=0?257:513)*(physical?2:1),new(0,23/256.0,71/256.0,37/256.0,0,ColdMs:47*40,Magic:53/256.0),new(769,103),NpcEquipmentCritical.Intrinsic(chances,Raw),i[7],i[8]!=0);
            int[] actual=[packet.Physical256,NamedElements.To256(packet.Elements.Fire),NamedElements.To256(packet.Elements.Lightning),NamedElements.To256(packet.Elements.Magic),NamedElements.To256(packet.Elements.Cold),packet.Poison.Rate256,packet.Elements.ColdMs/40,packet.Poison.Frames,packet.HitClass,packet.Overlay,physical?1:0,before,draws,i[0]!=0?0:1];
            Require(actual.SequenceEqual(o),"Complete source critical stage differs at "+count);
            if(i[0]==0)
            {
                var compatibility=new Rolls(modes[i[6]]);
                Require(NamedCombat.MeleeCritical(i[1]!=0?0:i[2],i[3],i[4],compatibility)==physical&&compatibility.Count==before,"Existing player Random values/count changed");
            }
            count++;
        }
        Require(count==3456,"Missing source critical cases");
        using var seeds=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equipment-seed-source-cases.json")));count=0;ulong seed=0;
        foreach(var row in seeds.RootElement.EnumerateArray())
        {
            if(row[1].GetInt32()==0){seed=SourceRandom.InitLow(row[0].GetInt32());}
            int bound=row[2].GetInt32();uint result=bound==-1?SourceRandom.Roll(ref seed):SourceRandom.Limited(ref seed,bound);
            Require(result==row[3].GetUInt32()&&seed==row[4].GetUInt64(),"Complete source RNG differs at "+count);count++;
        }
        Require(count==150,"Missing source seed cases");
    }
    private static Item Named(string id){Require(NamedGear.Available(id),"Named item unavailable: "+id);return NamedGear.Create(id,99,new Random(317),true);}
    private static SkeletonGuardian Pet(PlayerMobile owner)
    {
        var actor=new SkeletonGuardian{Name="NPC critical holder [prepared]"};actor.Initialize(owner,7,2);actor.ControlSlots=0;actor.NoKillAwards=true;
        Require(actor.SetControlMaster(owner),"Prepared holder ownership failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        PersistentMinions.Profiles.Track(actor);actor.AIObject.AITimer.Stop();return actor;
    }
    private static NpcPoisonRecipient Target(PlayerMobile owner)
    {
        var target=new NpcPoisonRecipient();target.SetHits(20000);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){target.SetResistance(type,0);}
        target.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();return target;
    }
    private static void Controlled(SkeletonGuardian actor,PlayerMobile owner)
    {
        var chances=NpcEquipmentCritical.Capture(actor);Require(chances.Mastery==0&&chances.Generic==0&&chances.Intrinsic==5,"Mapped NPC critical source changed or owner passives leaked");
        for(int physical=0;physical<=(chances.Deadly>0?1:0);physical++)
        for(int intrinsic=0;intrinsic<2;intrinsic++)
        {
            var target=Target(owner);
            try
            {
                int rawCount=0;uint Raw(){return rawCount++==0&&chances.Deadly>0?(physical!=0?0u:99u):(intrinsic!=0?0u:99u);}
                int p=NpcEquipmentDamage.Roll256(NpcEquipmentDamage.Capture(actor,target).Value,_=>0);
                var e=NpcEquipmentElements.Roll(NpcEquipmentElements.Capture(actor),_=>0);var poison=NpcEquipmentPoison.Roll(NpcEquipmentPoison.Capture(actor),_=>0);
                int expected=checked((p*(physical!=0?2:1)+NamedElements.To256(e.Total))*(intrinsic!=0?2:1));int before=SkillHealth.Balance256(target);
                Require(NpcEquipmentDamage.TryResolve(actor,target,out _,_=>0,Raw)&&before-SkillHealth.Balance256(target)==expected,"Actual critical physical/typed health loss differs");
                Require(rawCount==(chances.Deadly>0?2:1),"Actual stage order/short circuit draw count differs");
                if(poison.Rate256>0)
                {
                    Require(TimedEffects.TryActor(target,out var state)&&state.PoisonClock?.Rate256==poison.Rate256*(intrinsic!=0?2:1)&&state.PoisonMs==poison.Frames*40,"Critical doubled poison duration,missed rate or inherited owner stats");
                    int balance=SkillHealth.Balance256(target);TimedEffects.Settle(target,TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value).UpdatedUtc.AddMilliseconds(40));
                    Require(SkillHealth.Balance256(target)==balance-poison.Rate256*(intrinsic!=0?2:1),"Actual critical poison first frame differs");
                }
                if(e.ColdMs>0){Require(TimedEffects.TryActor(target,out var state)&&(state.ChillUntil-TimedEffects.Now).TotalMilliseconds<=e.ColdMs+100,"Critical doubled cold duration");}
            }
            finally{target.Delete();}
        }
    }
    private static void StreamStrike(SkeletonGuardian actor,PlayerMobile owner,ulong? initialSeed=null)
    {
        ulong seed=initialSeed??NpcEquipmentRandom.State(actor).Seed;int draws=0;
        uint Raw(){draws++;return SourceRandom.Roll(ref seed);}int Limited(int n)=>n<=0?0:(int)(Raw()%(uint)n);
        var target=Target(owner);
        try
        {
            var chances=NpcEquipmentCritical.Capture(actor);int physical=NpcEquipmentDamage.Roll256(NpcEquipmentDamage.Capture(actor,target).Value,Limited);
            if(NpcEquipmentCritical.Physical(chances,Raw)){physical=checked(physical*2);}
            var elements=NpcEquipmentElements.Roll(NpcEquipmentElements.Capture(actor),Limited);var poison=NpcEquipmentPoison.Roll(NpcEquipmentPoison.Capture(actor),Limited);
            bool intrinsic=NpcEquipmentCritical.Intrinsic(chances,Raw);int expected=checked((physical+NamedElements.To256(elements.Total))*(intrinsic?2:1)),before=SkillHealth.Balance256(target);
            Require(NpcEquipmentDamage.TryResolve(actor,target,out _)&&before-SkillHealth.Balance256(target)==expected,"Default stream packet differs from ordered source draws");
            Require(draws>=2&&NpcEquipmentRandom.State(actor).Seed==seed,"Default packet did not advance the saved source stream exactly");
            if(poison.Rate256>0){Require(TimedEffects.TryActor(target,out var state)&&state.PoisonClock?.Rate256==poison.Rate256*(intrinsic?2:1)&&state.PoisonMs==poison.Frames*40,"Default stream poison differs");}
        }
        finally{target.Delete();}
    }
    private static Stored Store(SkeletonGuardian actor)=>new(actor.Serial.Value,IronGolemItems.State(actor).Item,JsonSerializer.Serialize(ClassGear.State(World.FindItem((Serial)IronGolemItems.State(actor).Item))),NpcEquipmentCritical.Capture(actor),NpcEquipmentRandom.State(actor));
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="NpcEquipmentCriticalOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Holder history prerequisite failed");
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Owner Strength preparation failed");}
        FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        var ownerGear=Named("unique/49");owner.AddToBackpack(ownerGear);Require(owner.EquipItem(ownerGear),"Owner Deadly control equip failed");ClassGear.Refresh(owner);
        Require(ClassGear.Bonus(owner,"deadly")==30,"Owner Deadly control missing");
        var socketed=Named("unique/49");ClassGear.Profiles.Store(socketed,ClassGear.State(socketed) with{TinkerSockets=3});owner.AddToBackpack(socketed);
        foreach(string gem in new[]{"Ruby","Sapphire","Emerald"}){GemActivationChecks.Install(owner,GemActivationChecks.Gem(owner,gem),socketed);}
        var actors=new List<Stored>();int index=0;
        foreach(var item in new[]{socketed,Named("unique/31"),Named("unique/333"),Named("unique/256")})
        {
            var actor=Pet(owner);item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
            Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Original binding failed: "+why);
            Require(NpcEquipmentRandom.State(actor)==null,"Binding drew a seed before damage");Controlled(actor,owner);
            Require(NpcEquipmentRandom.State(actor)==null,"Controlled injection consumed live random stream");
            NpcEquipmentRandom.Set(actor,SourceRandom.InitLow(1234+index++));StreamStrike(actor,owner);actors.Add(Store(actor));
        }
        var existing=NpcEquipmentRandom.Snapshot();
        try{NpcEquipmentRandom.Restore([existing[0],existing[0]]);throw new InvalidOperationException("Duplicate stream accepted");}
        catch(InvalidDataException){Require(NpcEquipmentRandom.Snapshot().SequenceEqual(existing),"Invalid restore partially mutated saved streams");}
        var retired=Pet(owner);var retiredItem=Named("unique/31");retiredItem.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,retired,retiredItem,out _),"Retired binding failed");NpcEquipmentRandom.Set(retired,SourceRandom.InitLow(9));
        Require(NpcEquipmentRandom.Limited(retired,0)==0&&NpcEquipmentRandom.State(retired).Seed==SourceRandom.InitLow(9),"Nonpositive bound advanced the stream");
        IronGolemItems.DestroyBoundItem(retired);Require(NpcEquipmentRandom.State(retired)==null&&!NpcEquipmentRandom.Snapshot().Any(x=>x.Actor==retired.Serial.Value),"Removed original kept its stream");retired.Delete();
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actors.ToArray(),0)));Report();
    }
    private static Saved Continue(Saved saved)
    {
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);var rows=new List<Stored>();
        foreach(var row in saved.Actors)
        {
            var actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==row.Gear,"Reload changed original identity/rolls");
            Require(NpcEquipmentRandom.State(actor)==row.Random&&NpcEquipmentCritical.Capture(actor)==row.Chances,"Reload reset source stream or critical chances");
            actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();Controlled(actor,owner);StreamStrike(actor,owner);rows.Add(Store(actor));
        }
        PersistentMinions.Suspend(owner);owner.Internalize();return saved with{Actors=rows.ToArray(),Generation=saved.Generation+1};
    }
    private static void Reload(){SourceCases();File.WriteAllText(Manifest,JsonSerializer.Serialize(Continue(JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)))));Report();}
    private static void Upgrade()
    {
        Require(NpcEquipmentRandom.Snapshot().Length==0,"Pre-stream world unexpectedly acquired random state");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equipment-poison-fixtures.json")));var root=doc.RootElement;
        var owner=(PlayerMobile)World.FindMobile((Serial)root.GetProperty("Owner").GetUInt32());FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        var rows=new List<Stored>();var originalSeedLow=NpcEquipmentRandom.SeedLow;int calls=0;
        try
        {
            NpcEquipmentRandom.SeedLow=()=>{calls++;return 1234;};
            foreach(var row in root.GetProperty("Actors").EnumerateArray())
            {
                var actor=(SkeletonGuardian)World.FindMobile((Serial)row.GetProperty("Actor").GetUInt32());var item=World.FindItem((Serial)row.GetProperty("Item").GetUInt32());
                Require(item.Parent==actor&&actor.ControlMaster==owner&&JsonSerializer.Serialize(ClassGear.State(item))==row.GetProperty("Gear").GetString()&&NpcEquipmentRandom.State(actor)==null,"Old world lost original binding/rolls");
                actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();StreamStrike(actor,owner,SourceRandom.InitLow(1234));
                Require(calls==rows.Count+1,"Lazy seed initialization repeated or skipped");StreamStrike(actor,owner);Require(calls==rows.Count+1,"Second packet reseeded old binding");rows.Add(Store(actor));
            }
        }
        finally{NpcEquipmentRandom.SeedLow=originalSeedLow;}
        Require(rows.Count==6&&calls==6,"Missing pre-stream original fixtures");PersistentMinions.Suspend(owner);owner.Internalize();
        File.WriteAllText(UpgradeManifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows.ToArray(),0)));Report();
    }
    private static void UpgradeReload(){File.WriteAllText(UpgradeManifest,JsonSerializer.Serialize(Continue(JsonSerializer.Deserialize<Saved>(File.ReadAllText(UpgradeManifest)))));Report();}
    private static void Report()=>Log.Information("NPC critical:3456source stage/150seed cases;real original Deadly then intrinsic physical/typed/poison rates,unchanged durations,owner exclusion and persisted damage stream;complete AI seed ownership,item events and learned Iron Golem remain unfinished");
}
