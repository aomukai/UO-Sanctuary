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

public static class NpcEquipmentPoisonChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentPoisonChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,NpcPoisonSnapshot Snapshot,WeaponPoisonRoll First,WeaponPoisonRoll Last);
    private sealed record Saved(uint Owner,Stored[] Actors,uint RemovedActor,uint RemovedItem,uint Target,int Balance,TimedEffectState Poison,int DamageCalls,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-poison-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-equipment-poison-check",Check);Register("npc-equipment-poison-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC poison rate/count/frames and durable credit",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"venom-damage-source-probe.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();var raw=new MissileSourceStats(PoisonMin:V(11)+V(3),PoisonMax:V(12)+V(3),PoisonFrames:V(4),PoisonCount:V(5));int bound=-1;
            var packet=WeaponPoisonPackets.Melee(raw,V(2),V(7),n=>{bound=n;return V(8)!=0?n-1:0;},V(9),V(10),V(6));
            Require(packet.Rate256==V(14)&&packet.Frames==V(15)&&bound==V(16),"Reused complete source poison lane differs at "+count);count++;
        }
        Require(count==10240,"Missing reused poison source contexts");
    }
    private static Item Named(string id){Require(NamedGear.Available(id),"Named item unavailable: "+id);return NamedGear.Create(id,99,new Random(317),true);}
    private static SkeletonGuardian Pet(PlayerMobile owner)
    {
        var actor=new SkeletonGuardian{Name="NPC poison holder [prepared]"};actor.Initialize(owner,7,2);actor.ControlSlots=0;actor.NoKillAwards=true;
        Require(actor.SetControlMaster(owner),"Prepared holder ownership failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        PersistentMinions.Profiles.Track(actor);actor.AIObject.AITimer.Stop();return actor;
    }
    private static NpcPoisonRecipient Target(PlayerMobile owner,int resistance=0)
    {
        var target=new NpcPoisonRecipient();target.SetResistance(ResistanceType.Physical,0);target.SetResistance(ResistanceType.Fire,0);target.SetResistance(ResistanceType.Poison,resistance);
        target.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();return target;
    }
    private static TimedEffectState State(Mobile target)=>TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value);
    private static Stored Bind(PlayerMobile owner,Item item,int minimum,int maximum,int frames,int count)
    {
        var actor=Pet(owner);item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Original bind failed: "+why);
        var snapshot=NpcEquipmentPoison.Capture(actor);var raw=snapshot.Stats;
        Require(raw.PoisonMin==minimum&&raw.PoisonMax==maximum&&raw.PoisonFrames==frames&&raw.PoisonCount==count
            &&snapshot.Actor==actor.Serial.Value&&snapshot.CreditOwner==owner.Serial.Value&&snapshot.Mastery==0&&snapshot.Pierce==0,"Original poison projection/count/ownership differs");
        var first=NpcEquipmentPoison.Roll(snapshot,_=>0);var last=NpcEquipmentPoison.Roll(snapshot,n=>n-1);
        Require(first.Rate256==minimum&&last.Rate256==(maximum>minimum?maximum-1:minimum)&&first.Frames==(count>1?frames/count:frames),"Exclusive rate roll or averaged frame duration differs");
        var original=ClassGear.State(item);string gear=JsonSerializer.Serialize(original);
        ClassGear.Profiles.Store(item,original with{Identified=false});Require(IronGolemItems.Equipment(actor,CreatureProfiles.Level(actor)).Poison()==new MissileSourceStats(),"Unidentified poison leaked");ClassGear.Profiles.Store(item,original);
        if(item is BaseWeapon weapon){int durability=weapon.HitPoints;weapon.HitPoints=0;Require(IronGolemItems.Equipment(actor,CreatureProfiles.Level(actor)).Poison()==new MissileSourceStats(),"Broken poison leaked");weapon.HitPoints=durability;}
        var target=Target(owner);
        try
        {
            Require(NpcEquipmentDamage.TryResolve(actor,target,out _,_=>0),"Actual equipped strike did not run");var state=State(target);DateTime at=state.UpdatedUtc;
            Require(state.PoisonSource==owner.Serial.Value&&state.PoisonMs==first.Frames*40&&state.PoisonClock is{} clock&&clock.Rate256==first.Rate256,"Actual strike omitted NPC poison or inherited owner stats");
            int before=SkillHealth.Balance256(target),calls=target.DamageCalls;
            TimedEffects.Settle(target,at.AddMilliseconds(39));Require(SkillHealth.Balance256(target)==before,"Poison damaged before40ms");
            TimedEffects.Settle(target,at.AddMilliseconds(40));Require(SkillHealth.Balance256(target)==before-first.Rate256,"First poison source frame differs");
            TimedEffects.Settle(target,at.AddMilliseconds(80));Require(SkillHealth.Balance256(target)==before-2*first.Rate256&&target.DamageCalls==calls,"Poison used ordinary damage callbacks or lost FP256");
            TimedEffects.Settle(target,at.AddMilliseconds(first.Frames*40));bool retained=TimedEffects.TryActor(target,out var expired);
            Require(SkillHealth.Balance256(target)==before-first.Rate256*first.Frames&&(!retained||expired.PoisonMs==0&&expired.PoisonClock==null)&&target.DamageCalls==calls,"Poison expiry repaid damage or invoked a hit");
        }
        finally{target.Delete();}
        foreach(int resistance in new[]{50,100})
        {
            target=Target(owner,resistance);
            try
            {
                Require(NpcEquipmentDamage.TryResolve(actor,target,out _,_=>0),"Resistant strike rejected");
                bool active=TimedEffects.TryActor(target,out var state)&&state.PoisonMs>0;
                if(resistance==100){Require(!active,"Item pierce broke poison immunity");}
                else{Require(active&&state.PoisonClock?.Rate256==SourcePercentage.Calculate(first.Rate256,50,100),"Poison resistance rate differs");}
            }
            finally{target.Delete();}
        }
        return new(actor.Serial.Value,item.Serial.Value,gear,snapshot,first,last);
    }
    private static Saved Park(Saved saved,NpcPoisonRecipient target)
    {
        var at=TimedEffects.Now.Add(FixtureSpace.Retention);TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==target.Serial.Value?x with{UpdatedUtc=at}:x).ToArray());
        target.Internalize();return saved with{Balance=SkillHealth.Balance256(target),Poison=State(target),DamageCalls=target.DamageCalls};
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="NpcEquipmentPoisonOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Holder history prerequisite failed");
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Owner Strength preparation failed");}
        FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        var ownerWeapon=Named("unique/4");owner.AddToBackpack(ownerWeapon);Require(owner.EquipItem(ownerWeapon),"Owner poison weapon equip failed");ClassGear.Refresh(owner);
        Require(ClassGear.Bonus(owner,"pois-min")==128,"Owner poison control missing");
        var combined=Named("unique/36");ClassGear.Profiles.Store(combined,ClassGear.State(combined) with{TinkerSockets=1});owner.AddToBackpack(combined);
        GemActivationChecks.Install(owner,GemActivationChecks.Gem(owner,"Emerald"),combined);
        var gemmed=RuneSocketChecks.Host(owner,typeof(Longsword),1);GemActivationChecks.Install(owner,GemActivationChecks.Gem(owner,"Emerald"),gemmed);
        var rows=new[]{Bind(owner,Named("unique/4"),128,128,75,1),Bind(owner,Named("unique/31"),48,96,150,1),Bind(owner,Named("unique/36"),192,192,150,1),Bind(owner,Named("unique/41"),460,460,100,1),Bind(owner,combined,338,338,325,2),Bind(owner,gemmed,146,146,175,1)};
        owner.AddToBackpack(ownerWeapon);var web=Named("unique/299");owner.AddToBackpack(web);Require(owner.EquipItem(web),"Owner pierce control equip failed");ClassGear.Refresh(owner);
        Require(ClassGear.Bonus(owner,"pierce-pois") is >=40 and <=50,"Real owner poison pierce control missing");
        var actor=(SkeletonGuardian)World.FindMobile((Serial)rows[0].Actor);var target=Target(owner,50);
        try
        {
            Require(NpcEquipmentPoison.Capture(actor)==rows[0].Snapshot,"Owner weapon change leaked into NPC poison snapshot");
            NpcEquipmentDamage.TryResolve(actor,target,out _,_=>0);Require(State(target).PoisonClock?.Rate256==64,"Owner pierce leaked through NPC mitigation");
        }
        finally{target.Delete();}
        var removed=Pet(owner);var consumed=Named("unique/41");consumed.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,removed,consumed,out _),"Removal fixture bind failed");target=Target(owner);
        Require(NpcEquipmentDamage.TryResolve(removed,target,out _,_=>0),"Removal source strike failed");
        uint removedId=removed.Serial.Value,consumedId=consumed.Serial.Value;IronGolemItems.DestroyBoundItem(removed);removed.Delete();
        var saved=Park(new(owner.Serial.Value,rows,removedId,consumedId,target.Serial.Value,0,null,0,0),target);
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));Report();
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        foreach(var row in saved.Actors)
        {
            var actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&actor.Map==Map.Internal&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==row.Gear,"Reload lost original identity/rolls");
            var snapshot=NpcEquipmentPoison.Capture(actor);Require(snapshot==row.Snapshot&&NpcEquipmentPoison.Roll(snapshot,_=>0)==row.First&&NpcEquipmentPoison.Roll(snapshot,n=>n-1)==row.Last,"Reload changed poison source pools/packet");
        }
        var retired=World.FindMobile((Serial)saved.RemovedActor);var reused=World.FindItem((Serial)saved.RemovedItem);var reusedGear=reused==null?null:ClassGear.State(reused);
        Require(!(retired is SkeletonGuardian{Deleted:false} pet&&pet.ControlMaster==owner)
            &&reusedGear?.NamedItemId!="unique/41"
            &&!IronGolemItems.Snapshot().Any(x=>x.Pet==saved.RemovedActor||x.Item==saved.RemovedItem),"Removed source identity/equipment binding replayed");
        Log.Information("Removed source IDs:mobile={Mobile};item={Item};named={Named};retired binding absent",retired?.GetType().FullName??"absent",reused?.GetType().FullName??"absent",reusedGear?.NamedItemId??"absent");
        var target=(NpcPoisonRecipient)World.FindMobile((Serial)saved.Target);
        Require(SkillHealth.Balance256(target)==saved.Balance&&JsonSerializer.Serialize(State(target))==JsonSerializer.Serialize(saved.Poison)&&target.DamageCalls==saved.DamageCalls,"Saved poison rate/frame/source/health changed");
        DateTime now=TimedEffects.Now;TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==saved.Target?x with{UpdatedUtc=now}:x).ToArray());
        if(saved.Generation==0)
        {
            TimedEffects.Settle(target,now.AddMilliseconds(80));Require(SkillHealth.Balance256(target)==saved.Balance-920&&State(target).PoisonSource==saved.Owner&&target.DamageCalls==saved.DamageCalls,"Poison failed after source removal/reload");
            saved=Park(saved with{Generation=1},target);
        }
        else
        {
            owner.MoveToWorld(new(6100,2300,0),Map.Trammel);target.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);SkillHealth.SetBalance256(target,256);
            TimedEffects.Settle(target,now.AddMilliseconds(40));Require((!target.Alive||target.Deleted)&&target.LastKiller==owner&&target.DamageCalls==saved.DamageCalls,"Poison kill credit did not survive summon removal/two reloads");
            owner.Internalize();saved=saved with{Generation=2,Balance=0,Poison=null};
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));Report();
    }
    private static void Report()=>Log.Information("NPC original poison:10240reused source lane contexts;actual scalar/range/socket counts,exclusive rate and averaged frames,40ms FP256 damage/expiry,resistance/immunity,owner-gear exclusion and durable kill credit after explicit source removal/two reloads pass;full item/learned Iron Golem remains unfinished");
}

public sealed class NpcPoisonRecipient:BaseCreature
{
    public int DamageCalls;
    [Constructible] public NpcPoisonRecipient():base(AIType.AI_Melee,FightMode.Closest,10,1)
    {Name="NPC poison recipient";Body=0xD7;NoKillAwards=true;Frozen=true;CantWalk=true;SetHits(2000);}
    public NpcPoisonRecipient(Serial serial):base(serial){}
    public override bool CanRegenHits=>false;
    public override void OnDamage(int amount,Mobile from,bool willKill){DamageCalls++;base.OnDamage(amount,from,willKill);}
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);writer.Write(DamageCalls);}
    public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Invalid poison recipient fixture version");}DamageCalls=reader.ReadInt();}
}
