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

public static class NpcImpactAllocationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcImpactAllocationChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Target,uint Item,int Life,string Gear,NpcWeaponImpact Impact);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-impact-allocation-fixtures.json");
    public static void Configure(){Register("npc-impact-allocation-check",Check);Register("npc-impact-allocation-reload-check",Reload);}
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Allocated NPC channels/poison impact and separate periodic delivery",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement value)=>value.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Source()
    {
        int count=0;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-impact-allocation-source-probe.json")));
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int[] i=N(row[0]),o=N(row[1]);int flags=i[0],physical=i[1],typed=i[2],rate=i[3],frames=rate>0?i[4]:0,res=i[5],plr=i[6],variant=i[7],life=i[8];
            int dr=variant is 1 or 3?256:0,mdr=variant>=2?256:0;
            var p=NamedElements.Reduce256(physical,res,dr,0,0);var f=NamedElements.Reduce256(typed*512,res,mdr,0,variant==3?256:0);
            var l=NamedElements.Reduce256(typed*640,res,mdr,0,0);var m=NamedElements.Reduce256(typed*257,res,mdr,0,0);var c=NamedElements.Reduce256(typed*257,res,mdr,0,0);
            var poison=PoisonPackets.Mitigate(rate,frames,false,res,plr,0,0,0);int cold=ColdStates.LengthFrames(typed*31,res,false,false);
            var rolled=new NpcCriticalPacket(physical,new(),new(rate,frames),0,0,false);
            var packet=NpcEquipmentImpact.Packet(flags,life,rolled,new(p.Damage256/256.0,f.Damage256/256.0,c.Damage256/256.0,l.Damage256/256.0,0,Magic:m.Damage256/256.0),cold,poison,f.Absorbed256);
            int[] actual=[packet.Physical256,packet.Fire256,packet.Lightning256,packet.Magic256,packet.Cold256,packet.Poison256,packet.ColdFrames,NpcEquipmentImpact.Fills(flags)?poison.Frames:0,packet.Total256,packet.AbsorbedLife256,packet.ResultFlags];
            Require(actual.SequenceEqual(o),"Prefilled source mitigation/total/prediction differs at "+count+" actual="+string.Join(',',actual)+" expected="+string.Join(',',o));count++;
        }
        Require(count==27000,"Incomplete allocated packet source contexts");
    }
    private static PlayerMobile Owner()
    {var p=new PlayerMobile{Name="NPC allocation owner [fixture]",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,47);Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.raiseskeleton",out _),"Summon history");FixtureSpace.Corridor(p,6100,24,1900);return p;}
    private static SkeletonGuardian Actor(PlayerMobile owner,string id,out Item original)
    {
        var actor=new SkeletonGuardian{ControlSlots=0};actor.Initialize(owner,7,2);Require(actor.SetControlMaster(owner),"Allocation holder owner");actor.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();
        original=id==null?new Broadsword():NamedGear.Create(id,99,new Random(184),true);if(id==null){ClassGear.Profiles.Ensure(original);}original.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,actor,original,out string why),"Allocation original "+why);PersistentMinions.Profiles.Track(actor);return actor;
    }
    private static NpcPoisonRecipient Target(PlayerMobile owner,int resistance=0)
    {
        var target=new NpcPoisonRecipient();target.SetHits(20000);target.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){target.SetResistance(type,0);}target.SetResistance(ResistanceType.Poison,resistance);return target;
    }
    private sealed class Delivery(BaseCreature target,SkeletonGuardian actor,NpcWeaponImpact impact,DateTime at):MonsterReturnChecks.Release(new MonsterReturnChecks.WorldServices(target))
    {
        internal override void ApplyCold(int frames)
        {
            Require(frames==impact.Packet.ColdFrames,"Allocated cold length changed");
            if(frames>0){ColdStates.Commit(actor,target,frames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),Core.TickCount);}
            if(impact.Poison is{Rate256:>0,Frames:>0})
            {Require(NpcEquipmentPoison.Commit(new(impact.Actor,impact.CreditOwner,impact.Poison),target,at),"Allocated poison state omitted");}
        }
    }
    private static void Actual(PlayerMobile owner)
    {
        int count=0;
        foreach(string id in new string[]{null,"unique/4","unique/36","unique/41","unique/46","unique/28","unique/256"})foreach(int resistance in new[]{0,50,100})
        {
            var actor=Actor(owner,id,out var original);var target=Target(owner,resistance);
            try
            {
                NpcEquipmentRandom.Set(actor,17);int life=SkillHealth.Balance256(target);var impact=NpcEquipmentImpact.Prepare(actor,target,1);string frozen=JsonSerializer.Serialize(impact);
                Require(impact.Actor==actor.Serial.Value&&impact.Target==target.Serial.Value&&impact.CreditOwner==owner.Serial.Value&&life==SkillHealth.Balance256(target)&&target.DamageCalls==0&&!TimedEffects.TryActor(target,out _),"Allocation delivered HP/states or lost identity");
                var gear=ClassGear.State(original);ClassGear.Profiles.Store(original,gear with{Identified=false});target.SetResistance(ResistanceType.Poison,100);target.SetResistance(ResistanceType.Physical,100);target.SetResistance(ResistanceType.Cold,100);
                Require(JsonSerializer.Serialize(impact)==frozen,"Live gear/resistance changes rewrote allocated packet");
                var at=TimedEffects.Now;var result=PreparedMonsterDamage.Release(actor,target,impact.Packet,new(false,()=>false,()=>5,()=>false),_=>0,new Delivery(target,actor,impact,at),at);
                Require(!result.DeathModeAttempted&&SkillHealth.Balance256(target)==life-impact.Packet.Total256&&target.DamageCalls==0,"Allocated total remitigated or replayed incoming native damage");
                if(impact.Poison is{Rate256:>0,Frames:>0})
                {
                    int before=SkillHealth.Balance256(target);TimedEffects.Settle(target,at.AddMilliseconds(39));Require(SkillHealth.Balance256(target)==before,"Periodic poison repeated the impact before its first frame");
                    TimedEffects.Settle(target,at.AddMilliseconds(40));Require(SkillHealth.Balance256(target)==before-impact.Poison.Rate256,"Initial impact removed or duplicated the next poison frame");
                }
                else{Require(!target.Poisoned,"Zero/immune poison created a periodic state");}
                ClassGear.Profiles.Store(original,gear);count++;
            }
            finally{target.Delete();actor.Delete();}
        }
        Require(count==21,"Incomplete actual original allocation/delivery controls");
        var refused=Actor(owner,"unique/31",out _);var recipient=Target(owner);
        try
        {
            NpcEquipmentRandom.Set(refused,77123);ulong before=NpcEquipmentRandom.State(refused).Seed;bool blocked=false;
            try{NpcEquipmentImpact.Prepare(refused,recipient,1);}catch(NotSupportedException e){blocked=e.Message.Contains("drain lane");}
            Require(blocked&&NpcEquipmentRandom.State(refused).Seed==before&&!recipient.Poisoned&&recipient.DamageCalls==0,"Unsupported NPC drain silently omitted or partially rolled");
            foreach(int flags in new[]{0,16,129})
            {var miss=NpcEquipmentImpact.Prepare(refused,recipient,flags);Require(miss.Packet.Total256==0&&miss.Packet.ResultFlags==flags&&NpcEquipmentRandom.State(refused).Seed==before,"Miss/NoFill consumed an offensive roll or defensive pool");}
        }
        finally{recipient.Delete();refused.Delete();}
    }
    private static void Check()
    {
        Source();var owner=Owner();bool saved=false;
        try
        {
            Actual(owner);var actor=Actor(owner,"unique/4",out var original);var target=Target(owner,50);NpcEquipmentRandom.Set(actor,77123);
            var impact=NpcEquipmentImpact.Prepare(actor,target,1);Require(impact.Packet.Poison256==impact.Poison.Rate256&&impact.Poison.Rate256>0,"Saved poison impact packet");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actor.Serial.Value,target.Serial.Value,original.Serial.Value,SkillHealth.Balance256(target),JsonSerializer.Serialize(ClassGear.State(original)),impact)));
            target.Internalize();PersistentMinions.Suspend(owner);owner.Internalize();saved=true;
            Log.Information("NPC allocation:27000complete-source mitigation/total/prediction comparisons,21actual original/captured defense/initial poison/next-frame deliveries,unsupported drain preflight and3miss-NoFill controls pass;live actor/event registry/current applicability remain separate");
        }
        finally{if(!saved){foreach(var pet in(owner.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Delete();}owner.Delete();}}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var actor=World.FindMobile((Serial)saved.Actor) as SkeletonGuardian;var target=World.FindMobile((Serial)saved.Target) as NpcPoisonRecipient;var original=World.FindItem((Serial)saved.Item);
        Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner&&original?.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(original))==saved.Gear
            &&target is{Alive:true,Deleted:false}&&SkillHealth.Balance256(target)==saved.Life&&target.DamageCalls==0&&!target.Poisoned
            &&saved.Impact.Actor==saved.Actor&&saved.Impact.Target==saved.Target&&saved.Impact.CreditOwner==saved.Owner&&saved.Impact.Packet.Poison256==saved.Impact.Poison.Rate256,"Saved diagnostic allocated packet/original changed or replayed");
    }
}
