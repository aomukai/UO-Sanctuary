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

public static class ReviveEventChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveEventChecks));
    private sealed record Fixture(uint Actor,string Kind,string Issue);
    private sealed record Saved(uint Owner,Fixture[] Actors,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-event-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-event-check",Check);Register("revive-event-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Frozen Revive registered callbacks and shared ordered contact",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public class Recipient:GraveyardZombie
    {
        internal int Notifications,AtNotification,DeathCalls;internal bool WoundedAtNotification;
        [Constructible] public Recipient(){CantWalk=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;AtNotification=SkillHealth.Balance256(this);WoundedAtNotification=TimedEffects.TryActor(this,out var actor)&&actor.OpenWounds!=null;base.OnDamage(amount,from,willKill);}
        public override bool OnBeforeDeath(){DeathCalls++;return base.OnBeforeDeath();}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static Fixture Create(PlayerMobile owner,string kind)
    {
        var b=new Skeleton{Name="PoisonDaggerFixtureReviveEvent",NoKillAwards=true};foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        string[] names=kind switch{"registration"=>["Razortine"],"numeric"=>["Baranar's Star"],_=>["The Gnasher","Swordback Hold","Goblin Toe"]};
        var gear=names.Select(Named).ToArray();foreach(var item in gear){b.AddItem(item);}Require(gear.All(i=>i.Parent==b),"Event gear slots collided");
        b.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,item)=>{died?.Invoke(who,item);if(who==b){corpse=item as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(b,true);b.Damage(b.Hits+1,owner);CombatDamage.Finish(b,owner);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Event original was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var setup=ReviveLife.Setup(owner,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")};setup=ReviveEquipmentDefenses.Prepare(setup,original);
        string issue=null;
        if(kind!="legacy")
        {try{setup=ReviveEquipmentEvents.Prepare(setup,original);}catch(NotSupportedException e){issue=e.Message;}}
        Require(kind=="events"?issue==null:kind=="legacy"?setup.EquipmentEventProfile==null:issue!=null,"Event capability was silently admitted or discarded: "+kind+"/"+issue);
        Require(ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,before.AiStatus.Value,setup,out var actual,out var why)&&actual==b,"Event activation failed: "+why);
        foreach(var item in gear){Require(item.Parent==corpse,"Event gear was not independent loot");owner.AddToBackpack(item);item.Delete();}
        return new(b.Serial.Value,kind,issue);
    }
    private static Recipient Target(PlayerMobile owner,BaseCreature source,int life=20000,int physical=0)
    {
        var target=new Recipient{NoKillAwards=true};target.SetHits(life);foreach(var r in Enum.GetValues<ResistanceType>()){target.SetResistance(r,r==ResistanceType.Physical?physical:0);}
        target.MoveToWorld(source.Location,owner.Map);target.AIObject?.AITimer.Stop();return target;
    }
    private static void Clean(Recipient target){target.Corpse?.Delete();target.Delete();}
    private static void Registry(PlayerMobile owner,BaseCreature source)
    {
        Require(NpcOpenWounds.Chance(source)==100&&NpcCrushing.Chance(source)==45&&NpcEquipmentRetaliation.Flat(source)==10,"Frozen multi-item event totals differ from live originals");
        foreach(bool ranged in new[]{false,true})
        {
            var target=Target(owner,source);try
            {
                SkillHealth.SetBalance256(target,1025);int calls=0;var seed=NpcEquipmentRandom.State(source);
                NpcCombatEvents.ForSource(source).Dispatch(ranged?"domissiledamage":"domeleedamage",null,source,target,TimedEffects.Now,n=>{Require(n==100,"Wrong event bound");return calls++==0?99:44;});
                Require(calls==2&&SkillHealth.Balance256(target)==1025-1025/(ranged?8:4)&&target.Notifications==0&&NpcEquipmentRandom.State(source)==seed,"Duplicate event subscription,missed callback or injected RNG escaped to source stream");
                Require(TimedEffects.TryActor(target,out var actor)&&actor.OpenWounds is{ElapsedMs:0} wound&&wound.CreditOwner==owner.Serial.Value&&actor.BleedSource==source.Serial,"Frozen wounds lost real source or durable owner");
            }
            finally{Clean(target);}
        }
        foreach(var rule in BalanceCatalog.Read<NpcItemEventStat>("npc-item-combat-events"))
        {
            bool refused=false;try{_=NpcCombatEvents.FromStats(new[]{new EquipmentStatValue(EquipmentStatKey.Pack(rule.Stat),1)});}catch(NotSupportedException){refused=true;}
            Require(refused==(rule.Stat is not(78 or 81 or 86 or 112 or 113 or 114 or 128 or 134 or 135 or 136 or 138 or 139)),"Unsupported registration was silently skipped: "+rule.Stat);
        }
    }
    private static void Contact(PlayerMobile owner,BaseCreature source)
    {
        foreach(var scene in new (int Flags,bool Range,int Life,int Resistance)[]{(0,true,20000,0),(16,true,20000,0),(1,false,20000,0),(1,true,20000,0),(1,true,20000,100),(1,true,1,0)})
        {
            var target=Target(owner,source,scene.Life,scene.Resistance);try
            {
                NpcEquipmentRandom.Set(source,SourceRandom.InitLow(173));var rules=IronGolemContact.Require(source,owner,target);
                var prepared=IronGolemContact.Prepare(source,target,rules,()=>scene.Flags);ulong expected=NpcEquipmentRandom.State(source).Seed;
                int life=SkillHealth.Balance256(target),loss=0;bool eventHit=scene.Range&&(scene.Flags&1)!=0;
                if(eventHit){SourceRandom.Roll(ref expected);if(SourceRandom.Roll(ref expected)%100<45){loss=scene.Resistance>=100?0:life/4;}}
                var at=TimedEffects.Now;int ranges=0;var result=IronGolemContact.Release(source,owner,target,prepared,bonus=>{Require(bonus==3,"Original NPC contact used player range bonus");ranges++;return scene.Range;},at);
                int after=eventHit?life-loss-prepared.Impact.Packet.Total256:life;if(after<256){after=0;}
                Require(prepared.Consumed&&ranges==1&&NpcEquipmentRandom.State(source).Seed==expected,"Contact consumption/range/RNG chronology changed");
                Require(SkillHealth.Balance256(target)==after&&target.Notifications==(eventHit&&prepared.Impact.Packet.Total256>0?1:0),"Shared original contact replayed native damage or misordered CB and weapon HP");
                if(target.Notifications>0){Require(target.AtNotification==life-loss&&target.WoundedAtNotification,"NPC registered callbacks did not precede the one weapon-health commit");}
                Require(target.DeathCalls==(after==0?1:0)&&result.DeathModeAttempted==(after==0),"Original NPC death was early,omitted or repeated");
                if(!scene.Range){Require(result.Exit==MonsterContactExit.OutOfRangeFreed,"Out-of-range packet was not freed");}
                bool refused=false;try{_=IronGolemContact.Release(source,owner,target,prepared,_=>true,at);}catch(InvalidOperationException){refused=true;}Require(refused,"Consumed original contact could replay");
            }
            finally{Clean(target);}
        }
    }
    private static void Exercise(PlayerMobile owner,Fixture[] fixtures)
    {
        owner.MoveToWorld(new(6500,3500,0),Map.Trammel);
        var source=World.FindMobile((Serial)fixtures[0].Actor)as BaseCreature;source.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);source.Blessed=false;source.AIObject?.AITimer.Stop();
        Registry(owner,source);Contact(owner,source);
        foreach(var f in fixtures.Skip(1))
        {
            var b=World.FindMobile((Serial)f.Actor)as BaseCreature;var seed=NpcEquipmentRandom.State(b);bool refused=false;
            using var target=new TargetScope(Target(owner,source));
            try{_=IronGolemContact.Require(b,(PlayerMobile)b.ControlMaster,target.Actor);}catch(NotSupportedException){refused=true;}
            Require(refused&&NpcEquipmentRandom.State(b)==seed&&NpcOpenWounds.Chance(b)==0&&NpcCrushing.Chance(b)==0&&NpcEquipmentRetaliation.Flat(b)==0,"Unadmitted frozen source enabled events or consumed RNG");
        }
        var rows=ReviveNativeActivation.Snapshot();var state=rows.Single(s=>s.Actor==source.Serial.Value);var before=ReviveBeforeDeath.Decode(state.Original.BeforeDeathJson);
        var items=ReviveBeforeDeath.Equipment(before);items[0]=items[0]with{FixedJson=JsonSerializer.Serialize(new[]{new FrozenBonus("openwounds",1)})};bool rejected=false;
        try{_=ReviveEquipmentEvents.Prepare(state.Setup,state.Original with{BeforeDeathJson=JsonSerializer.Serialize(before with{EquipmentJson=JsonSerializer.Serialize(items)})});}catch(NotSupportedException){rejected=true;}
        Require(rejected,"Unattached derived/fixed event stat was invented");
        rejected=false;try{ReviveNativeActivation.Restore(rows.Select(r=>r==state?r with{Setup=r.Setup with{EquipmentEventProfile="unknown"}}:r).ToArray());}catch(InvalidDataException){rejected=true;}
        Require(rejected&&ReviveNativeActivation.Snapshot().SequenceEqual(rows),"Unknown event profile replaced the roster");
        owner.Internalize();
    }
    private sealed class TargetScope(Recipient actor):IDisposable{internal Recipient Actor=>actor;public void Dispose()=>Clean(actor);}
    private static void Check()
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureReviveEventOwner";MagePetBuffChecks.Train(owner,"necromancer.revive",20);MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(owner,6500,24,3500);
        // Rank20 now permits three Revives. Keep the fourth independent
        // supplier on another real owner instead of pruning the first actor.
        var other=PoisonDaggerChecks.Owner();other.Name="PoisonDaggerFixtureReviveEventNumericOwner";
        MagePetBuffChecks.Train(other,"necromancer.revive",20);MagePetBuffChecks.Train(other,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(other,6500,24,3564);
        var fixtures=new[]{Create(owner,"events"),Create(owner,"legacy"),Create(owner,"registration"),Create(other,"numeric")};Exercise(owner,fixtures);other.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,fixtures)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
        Require(saved.Actors.All(f=>World.FindMobile((Serial)f.Actor)is BaseCreature{Alive:true,Deleted:false,Map:var map}&&map==Map.Internal),"Event original bodies were lost or failed to park");
        Exercise(owner,saved.Actors);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
