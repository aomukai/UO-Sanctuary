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

public static class NpcEquipmentDurationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentDurationChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,int PoisonLength,bool Cannot,bool Half);
    private sealed record Saved(uint Owner,Stored[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-durations-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-equipment-durations-check",Check);Register("npc-equipment-durations-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC poison/cold duration defenses",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var poison=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-mitigation-source-probe.json")));int n=0;
        foreach(var row in poison.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int penalty=V(1)==0?0:V(1)==1?-40:-100;
            var actual=PoisonPackets.Mitigate(V(2),V(3),V(0)==0,V(4),V(5),V(6),V(7),penalty,V(8)!=0);
            Require(actual.Rate256==V(10)&&actual.Frames==V(11),"Reused full poison rate/length source comparison "+n);n++;
        }
        Require(n==72000,"Missing poison source contexts");n=0;
        using var cold=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"freeze-state-source-probe.json")));
        foreach(var row in cold.RootElement.GetProperty("length").EnumerateArray())
        {
            int frames=ColdStates.LengthFrames(row[0].GetInt32(),row[1].GetInt32(),row[2].GetInt32()!=0,row[3].GetInt32()!=0);
            Require(frames==row[4].GetInt32()&&frames==row[5].GetInt32(),"Reused full cold/freeze length source comparison");n++;
        }
        Require(n==224,"Missing cold/freeze length controls");
    }
    private static Item Named(string id){Require(NamedGear.Available(id),"Unavailable original: "+id);return NamedGear.Create(id,99,new Random(317),true);}
    private static SkeletonGuardian Pet(PlayerMobile owner)
    {
        var actor=new SkeletonGuardian{Name="NPC duration holder [prepared]"};actor.Initialize(owner,7,2);actor.ControlSlots=0;actor.NoKillAwards=true;
        Require(actor.SetControlMaster(owner),"Duration holder ownership failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        actor.SetResistance(ResistanceType.Cold,0);actor.SetResistance(ResistanceType.Poison,0);
        PersistentMinions.Profiles.Track(actor);actor.AIObject.AITimer.Stop();return actor;
    }
    private static Stored Store(SkeletonGuardian actor)=>new(actor.Serial.Value,IronGolemItems.State(actor).Item,
        JsonSerializer.Serialize(ClassGear.State(World.FindItem((Serial)IronGolemItems.State(actor).Item))),
        ItemDurationDefenses.PoisonLength(actor),ItemDurationDefenses.CannotFreeze(actor),ItemDurationDefenses.HalfFreeze(actor));
    private static TimedEffectState State(Mobile actor)=>TimedEffects.Snapshot().Single(x=>x.Owner==actor.Serial.Value);
    private static void Verify(SkeletonGuardian actor,Item item)
    {
        var expected=Store(actor);var original=ClassGear.State(item);
        ClassGear.Profiles.Store(item,original with{Identified=false});
        Require(ItemDurationDefenses.PoisonLength(actor)==0&&!ItemDurationDefenses.CannotFreeze(actor)&&!ItemDurationDefenses.HalfFreeze(actor),"Unidentified duration defense remained active or inherited owner equipment");
        ClassGear.Profiles.Store(item,original);
        void Broken(){Require(ItemDurationDefenses.PoisonLength(actor)==0&&!ItemDurationDefenses.CannotFreeze(actor)&&!ItemDurationDefenses.HalfFreeze(actor),"Broken duration defense remained active");}
        if(item is BaseWeapon w){int hp=w.HitPoints;w.HitPoints=0;Broken();w.HitPoints=hp;}
        else if(item is BaseArmor a){int hp=a.HitPoints;a.HitPoints=0;Broken();a.HitPoints=hp;}
        item.MoveToWorld(actor.Location,actor.Map);
        Require(!ItemDurationDefenses.HasEquipment(actor)&&ItemDurationDefenses.PoisonLength(actor)==0&&!ItemDurationDefenses.CannotFreeze(actor)&&!ItemDurationDefenses.HalfFreeze(actor),"Moved original kept passive defenses");
        bool refused=false;try{_=IronGolemItems.Equipment(actor,CreatureProfiles.Level(actor));}catch(NotSupportedException){refused=true;}
        Require(refused,"Explicit reader accepted moved binding");actor.AddItem(item);
        Require(Store(actor)==expected,"Repair/identification/movement changed original rolls or defenses");
        var source=new PlayerMobile{Name="Unallied duration source",Body=0x190,Player=true};source.AddItem(new Backpack());source.MoveToWorld(new(actor.X+1,actor.Y,actor.Z),actor.Map);
        try
        {
            int length=expected.PoisonLength;
            int resistance=NamedElements.Resistance(source,actor,"pois",actor.PoisonResistance,pierceOverride:0);
            var packet=PoisonPackets.Mitigate(257,13,false,resistance,length,0,0,0);DateTime at=TimedEffects.Now;
            TimedEffects.Clear(actor);SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor));
            Require(PoisonJavelins.Poison(source,actor,257,13,at),"Actual poison receiver rejected unallied source");
            var state=State(actor);Require(state.PoisonMs==packet.Frames*40&&state.PoisonClock?.Rate256==packet.Rate256,"Recipient item length reduction changed rate or was omitted");
            int before=SkillHealth.Balance256(actor);TimedEffects.Settle(actor,at.AddMilliseconds(40));
            Require(SkillHealth.Balance256(actor)==before-packet.Rate256,"Poison first-frame rate changed with length defense");
            TimedEffects.Clear(actor);SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor));
            // Legacy aggregate callers must retain rate while shortening total
            // damage and duration. Use exact source-frame input here.
            double legacyRate=NamedElements.Damage(source,actor,13,"pois",enhance:false)/(13*40);
            Require(GearEffects.ApplyPoison(source,actor,13,Core.TickCount,13*40,false),"Aggregate poison rejected");
            state=State(actor);Require(state.PoisonMs==packet.Frames*40&&Math.Abs(state.PoisonRate-legacyRate)<1e-9,"Aggregate NPC poison duration defense altered rate");
            TimedEffects.Clear(actor);long tick=Core.TickCount;at=TimedEffects.TimeAt(tick);
            int frames=ColdStates.LengthFrames(13,actor.ColdResistance,expected.Cannot,expected.Half);
            int coldFrames=ColdStates.StateFrames(frames,true,ColdStates.Effect(actor),NecromancerEffects.DifficultyDivisor(actor));
            Require(ColdStates.Apply(source,actor,13,tick)==(coldFrames>0),"Cold item defense acceptance differs");
            if(coldFrames>0){Require(State(actor).ChillUntilUtc==at.AddMilliseconds(coldFrames*40),"Cold half-before-resistance duration differs");}
            TimedEffects.Clear(actor);
            Require(GearEffects.ApplyChill(actor,tick,13*40,source)==(coldFrames>0),"Legacy chill omitted NPC defense");
            if(coldFrames>0){Require(State(actor).ChillUntilUtc==at.AddMilliseconds(coldFrames*40),"Legacy chill skipped source duration order/division");}
            TimedEffects.Clear(actor);
            bool frozen=FreezeStates.Apply(source,actor,13,tick);
            Require(frozen==(frames>0),"Freeze item defense acceptance differs");
            if(frozen){Require(State(actor).Control.FreezeUntil==at.AddMilliseconds(Math.Max(1,frames/NecromancerEffects.DifficultyDivisor(actor))*40),"Freeze did not share recipient duration defenses");}
        }
        finally{TimedEffects.Clear(actor);SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor));source.Delete();}
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="NpcDurationOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Duration owner prerequisite failed");FixtureSpace.Place(owner,new(6100,2320,0),Map.Trammel);
        owner.AddItem(Named("unique/84"));owner.AddItem(Named("piece/47"));
        Require(ClassGear.Bonus(owner,"nofreeze")>0&&ClassGear.Bonus(owner,"res-pois-len")==75,"Positive owner exclusion control missing");
        string[] ids=["unique/32","unique/86","piece/7","unique/77","unique/41"];var rows=new Stored[ids.Length];
        for(int i=0;i<ids.Length;i++)
        {
            var actor=Pet(owner);var item=Named(ids[i]);item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
            Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Duration original bind failed: "+why);Verify(actor,item);rows[i]=Store(actor);
        }
        Require(rows.Select(x=>x.PoisonLength).SequenceEqual(new[]{50,50,75,0,0})&&rows[3].Half&&!rows[3].Cannot&&rows[4].Cannot&&!rows[4].Half,"Actual original duration goldens differ");
        var removed=Pet(owner);var consumed=Named("unique/41");consumed.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(IronGolemItems.TryAttach(owner,removed,consumed,out _),"Removal bind failed");
        IronGolemItems.DestroyBoundItem(removed);Require(!ItemDurationDefenses.CannotFreeze(removed),"Removed original retained freeze defense");removed.Delete();
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows,0)));Report();
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        FixtureSpace.Place(owner,new(6100,2320,0),Map.Trammel);
        foreach(var row in saved.Actors)
        {
            var actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&item.Parent==actor&&Store(actor)==row,"Saved original identity/rolls/duration defenses changed");
            actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();Verify(actor,item);
        }
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));Report();
    }
    private static void Report()=>Log.Information("NPC durations:72000reused poison/224cold-freeze source controls;actual originals,packet/aggregate poison,cold/freeze,disabled/removal,owner exclusion and two reloads;offensive freeze events and complete Iron Golem remain separate");
}
