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

public static class ReviveImpactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveImpactChecks));
    private sealed record Saved(uint Owner,uint Source,ReviveNativeState State,int Pierce,NpcWeaponImpact[] Packets);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-impact-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-impact-check",Check);Register("revive-impact-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original-body qualified packet allocation with captured pierce",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static BaseCreature Source(PlayerMobile owner,out int pierce)
    {
        var b=new Skeleton{Name="PoisonDaggerFixtureReviveImpact",NoKillAwards=true};foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        var gear=new[]{"Baranar's Star","Venom Grip","Snakecord","Gorerider","Griffon's Eye","Raven Frost"}.Select(Named).ToArray();foreach(var item in gear){b.AddItem(item);}
        pierce=gear.Sum(item=>ClassGear.Bonus(item,"pierce-ltng",1));Require(pierce>0,"Positive original pierce fixture missing");
        b.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,item)=>{died?.Invoke(who,item);if(who==b){corpse=item as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(b,true);b.Damage(b.Hits+1,owner);CombatDamage.Finish(b,owner);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Impact original was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var setup=ReviveLife.Setup(owner,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")};setup=ReviveEquipmentCombat.Prepare(setup,original);
        Require(ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,before.AiStatus.Value,setup,out var actual,out var issue)&&actual==b,"Impact original activation failed: "+issue);
        foreach(var item in gear){Require(item.Parent==corpse,"Impact gear did not remain independent loot");owner.AddToBackpack(item);item.Delete();}return b;
    }
    private static NpcPoisonRecipient Target(PlayerMobile owner,int lightning=80,int poison=75)
    {
        var b=new NpcPoisonRecipient();b.SetHits(20000);b.SetResistance(ResistanceType.Physical,0);b.SetResistance(ResistanceType.Fire,25);b.SetResistance(ResistanceType.Cold,50);b.SetResistance(ResistanceType.Energy,lightning);b.SetResistance(ResistanceType.Poison,poison);
        b.MoveToWorld(new(owner.X+6,owner.Y,owner.Z),owner.Map);b.AIObject?.AITimer.Stop();return b;
    }
    private static NpcWeaponImpact Allocate(BaseCreature source,BaseCreature target,int pierce,bool high=false)
    {
        var capture=ReviveEquipmentOffense.Capture(source,target);Require(capture.Pierce.Lightning==pierce&&capture.Pierce.Fire==0&&capture.Pierce.Cold==0&&capture.Pierce.Magic==0,"Captured original pierce inherited owner gear or lost a channel");
        int Limited(int n)=>high?n-1:0;var rolled=ReviveEquipmentOffense.Roll(capture,Limited,()=>99);
        int life=SkillHealth.Balance256(target),armor=NecromancerEffects.Armor256(target);var stream=NpcEquipmentRandom.State(source);int items=World.Items.Count;
        var impact=ReviveEquipmentImpact.Prepare(source,target,1,Limited,()=>99);var packet=impact.Packet;
        int physical=NamedElements.Reduce256(Math.Max(0,rolled.Damage.Physical256-armor),target.PhysicalResistance,0,0,0).Damage256;
        int Channel(double value,int resistance)=>NamedElements.Reduce256(NamedElements.To256(value),resistance,0,0,0).Damage256;
        int resistance=target.EnergyResistance<100?Math.Max(-100,target.EnergyResistance-pierce):target.EnergyResistance;
        var poison=PoisonPackets.Mitigate(rolled.Damage.Poison.Rate256,rolled.Damage.Poison.Frames,false,target.PoisonResistance,0,capture.Poison.Pierce,0,0);
        Require(packet.Physical256==physical&&packet.Fire256==Channel(rolled.Damage.Elements.Fire,target.FireResistance)
            &&packet.Cold256==Channel(rolled.Damage.Elements.Cold,target.ColdResistance)&&packet.Lightning256==Channel(rolled.Damage.Elements.Lightning,resistance)
            &&packet.Poison256==poison.Rate256&&impact.Poison==poison&&packet.ColdFrames==ColdStates.LengthFrames(rolled.ColdFrames,target.ColdResistance,false,false),"Prepared original mitigation double-scaled damage/pierce or changed poison/cold metadata");
        Require(packet.Total256==packet.Physical256+packet.Fire256+packet.Cold256+packet.Lightning256+packet.Magic256+packet.Poison256
            &&packet.AbsorbedLife256==0&&impact.CreditOwner==source.ControlMaster.Serial.Value&&SkillHealth.Balance256(target)==life&&NpcEquipmentRandom.State(source)==stream&&World.Items.Count==items
            &&target is NpcPoisonRecipient{DamageCalls:0}&&!TimedEffects.TryActor(target,out _),"Allocation delivered HP/states/native replay or rewrote actor stream");
        if(armor>0){Require(NecromancerEffects.Armor256(target)==Math.Max(0,armor-rolled.Damage.Physical256),"Fractional Bone Armor was not consumed exactly once during allocation");}
        return impact;
    }
    private sealed class Delivery(BaseCreature source,NpcPoisonRecipient target,NpcWeaponImpact impact,DateTime at):MonsterReturnChecks.Release(new MonsterReturnChecks.WorldServices(target))
    {
        internal override void ApplyCold(int frames)
        {
            if(frames>0){ColdStates.Commit(source,target,frames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at));}
            if(impact.Poison is{Rate256:>0,Frames:>0}){Require(NpcEquipmentPoison.Commit(new(impact.Actor,impact.CreditOwner,impact.Poison),target,at),"Allocated original poison state omitted");}
        }
    }
    private static NpcWeaponImpact[] Exercise(PlayerMobile owner,BaseCreature source,int pierce)
    {
        var packets=new System.Collections.Generic.List<NpcWeaponImpact>();
        using(var target=new TargetScope(Target(owner)))
        {
            var before=NpcEquipmentRandom.State(source);int calls=0;
            foreach(int flags in new[]{0,1|128,1|256,1|512,1|32768})
            {var empty=ReviveEquipmentImpact.Prepare(source,target.Actor,flags,n=>{calls++;return 0;},()=>{calls++;return 0;});Require(empty.Packet.Total256==0&&empty.Poison==default&&NpcEquipmentRandom.State(source)==before&&calls==0,"No-fill result consumed offensive RNG or allocated damage");}
            var impact=Allocate(source,target.Actor,pierce);packets.Add(impact);string saved=JsonSerializer.Serialize(impact);target.Actor.SetResistance(ResistanceType.Energy,100);target.Actor.SetResistance(ResistanceType.Poison,100);
            Require(JsonSerializer.Serialize(impact)==saved,"Live resistance changes rewrote allocated packet");
            int life=SkillHealth.Balance256(target.Actor);var at=TimedEffects.Now;
            var result=PreparedMonsterDamage.Release(source,target.Actor,impact.Packet,new(false,()=>false,()=>5,()=>false),_=>0,new Delivery(source,target.Actor,impact,at),at);
            Require(!result.DeathModeAttempted&&SkillHealth.Balance256(target.Actor)==life-impact.Packet.Total256&&target.Actor.DamageCalls==0,"Original allocation was remitigated or replayed native damage on qualified HP commit");
            int after=SkillHealth.Balance256(target.Actor);TimedEffects.Settle(target.Actor,at.AddMilliseconds(39));Require(SkillHealth.Balance256(target.Actor)==after,"Poison repeated its impact before first frame");
            TimedEffects.Settle(target.Actor,at.AddMilliseconds(40));Require(SkillHealth.Balance256(target.Actor)==after-impact.Poison.Rate256,"Original poison impact omitted/duplicated next frame");
        }
        using(var target=new TargetScope(Target(owner,120,120)))
        {var impact=Allocate(source,target.Actor,pierce);Require(impact.Packet.Lightning256==0&&impact.Poison.Rate256==0,"Item pierce broke an unbroken monster immunity");packets.Add(impact);}
        using(var target=new TargetScope(Target(owner)))
        {int armor=6*256+17;NecromancerEffects.Profiles.Store(target.Actor,new(BoneArmor:(armor+255)/256,BoneArmorPool:new(null,armor,null)));packets.Add(Allocate(source,target.Actor,pierce,true));}
        using(var target=new TargetScope(Target(owner)))
        {
            NpcEquipmentRandom.Set(source,SourceRandom.InitLow(173));ulong seed=NpcEquipmentRandom.State(source).Seed;
            uint Raw()=>SourceRandom.Roll(ref seed);int Limited(int n)=>(int)(Raw()%(uint)n);
            var expected=ReviveEquipmentImpact.Prepare(source,target.Actor,1,Limited,Raw);var actual=ReviveEquipmentImpact.Prepare(source,target.Actor,1);
            Require(actual==expected&&NpcEquipmentRandom.State(source).Seed==seed,"Default allocator used another actor stream or rolled twice");packets.Add(actual);
        }
        var blocked=NpcEquipmentRandom.State(source);bool refused=false;
        try{_=ReviveEquipmentImpact.Prepare(source,source,1);}catch(NotSupportedException){refused=true;}Require(refused&&NpcEquipmentRandom.State(source)==blocked,"Unimplemented frozen recipient defense consumed RNG");
        return packets.ToArray();
    }
    private sealed class TargetScope(NpcPoisonRecipient actor):IDisposable
    {internal NpcPoisonRecipient Actor=>actor;public void Dispose()=>actor.Delete();}
    private static void Check()
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureReviveImpactOwner";MagePetBuffChecks.Train(owner,"necromancer.revive",20);MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(owner,6420,24,3400);
        var helm=Named("Griffon's Eye");owner.AddToBackpack(helm);Require(owner.EquipItem(helm),"Owner pierce control equip failed");ClassGear.Refresh(owner);Require(ClassGear.Bonus(owner,"pierce-ltng")>0,"Owner positive pierce control missing");
        var source=Source(owner,out int pierce);var packets=Exercise(owner,source,pierce);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,source.Serial.Value,ReviveNativeActivation.State(source),pierce,packets)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;var source=World.FindMobile((Serial)saved.Source)as BaseCreature;
        Require(source is{Alive:true,Deleted:false}&&source.Map==Map.Internal&&source.ControlMaster==owner&&ReviveNativeActivation.State(source)==saved.State&&saved.Packets.Length==4,"Saved impact source or packet value payload changed");
        string packets=JsonSerializer.Serialize(saved.Packets);owner.MoveToWorld(new(6420,3400,0),Map.Trammel);_=Exercise(owner,source,saved.Pierce);owner.Internalize();Require(JsonSerializer.Serialize(saved.Packets)==packets,"Rechecking source rewrote saved allocated packets");
    }
}
