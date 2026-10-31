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

public static class NpcWeaponPreparationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcWeaponPreparationChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Target,uint Item,int Life,int Credit,int Responsible,string Gear,string Roll);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-weapon-preparation-fixtures.json");
    public static void Configure(){Register("npc-weapon-preparation-check",Check);Register("npc-weapon-preparation-reload-check",Reload);}
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Offensive NPC snapshots and ordered native damage ledger",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile owner,string id)
    {Require(TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent),"Talent "+id);foreach(var prior in talent.Prerequisites){Train(owner,prior);}if(ClassCreation.Profiles.GetRank(owner.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}}
    private static PlayerMobile Owner()
    {var owner=new PlayerMobile{Name="NPC preparation owner [fixture]",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);Train(owner,"necromancer.irongolem");FixtureSpace.Corridor(owner,6100,24,2000);return owner;}
    private static Item Item(PlayerMobile owner,string id=null)
    {Item item=id==null?new Broadsword():NamedGear.Create(id,99,new Random(179),true);if(id==null){ClassGear.Profiles.Ensure(item);}item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);return item;}
    private static NpcPoisonRecipient Target(PlayerMobile owner)
    {var target=new NpcPoisonRecipient();target.SetHits(100);target.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();target.SetResistance(ResistanceType.Physical,0);return target;}
    private static int Credit(BaseCreature target,Mobile source)=>target.FindDamageEntryFor(source)?.DamageGiven??0;
    private static int Responsible(BaseCreature target,Mobile source,PlayerMobile owner)=>target.FindDamageEntryFor(source)?.Responsible?.SingleOrDefault(x=>x.Damager==owner)?.DamageGiven??0;
    private static PreparedMonsterDamageResult Release(BaseCreature source,BaseCreature target,int damage,int absorbed=0)
    {
        var world=new MonsterReturnChecks.WorldServices(target);
        return PreparedMonsterDamage.Release(source,target,new(damage,damage,0,0,1,AbsorbedLife256:absorbed),
            new(false,()=>false,()=>5,()=>false),_=>0,new MonsterReturnChecks.Release(world),TimedEffects.Now);
    }
    private static void CreditCases(PlayerMobile owner,IronGolemBody source)
    {
        int count=0;
        foreach(int life in new[]{257,1001,5000,100*256})foreach(int damage in new[]{1,17,255,256,257,300,2000,1000*256})foreach(int absorb in new[]{0,3*256})
        {
            var ordinary=Target(owner);var prepared=Target(owner);
            try
            {
                SkillHealth.SetBalance256(ordinary,life);SkillHealth.SetBalance256(prepared,life);SkillHealth.Heal(ordinary,absorb);
                SkillHealth.Damage(source,ordinary,damage);
                var result=Release(source,prepared,damage,absorb);
                Require(SkillHealth.Balance256(ordinary)==SkillHealth.Balance256(prepared)&&(!ordinary.Alive||ordinary.Deleted)==result.DeathModeAttempted,"Native/prepared health or death differs "+count);
                Require(Credit(ordinary,source)==Credit(prepared,source)&&Responsible(ordinary,source,owner)==Responsible(prepared,source,owner)
                    &&Responsible(prepared,source,owner)==Credit(prepared,source),"Native ledger/master amount differs "+count);
                Require(prepared.DamageCalls==0,"Ordered HP replayed incoming native Damage/OnDamage");count++;
            }
            finally{ordinary.Corpse?.Delete();prepared.Corpse?.Delete();ordinary.Delete();prepared.Delete();}
        }
        Require(count==64,"Incomplete whole/fraction/overkill/absorb credit controls");
    }
    private static void Rolls(PlayerMobile owner)
    {
        int count=0,changed=0;
        foreach(string id in new string[]{null,"unique/4","unique/31","unique/36","unique/41","unique/46"})
        {
            var actor=new SkeletonGuardian{ControlSlots=0};actor.Initialize(owner,7,2);Require(actor.SetControlMaster(owner),"Roll holder owner");actor.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();
            var original=Item(owner,id);Require(IronGolemItems.TryAttach(owner,actor,original,out string why),"Roll original "+why);
            try
            {
                foreach(ulong seed in new ulong[]{17,77123})
                {
                    var target=Target(owner);target.SetHits(20000);target.SetResistance(ResistanceType.Fire,0);target.SetResistance(ResistanceType.Cold,0);target.SetResistance(ResistanceType.Energy,0);target.SetResistance(ResistanceType.Poison,0);
                    try
                    {
                        NpcEquipmentRandom.Set(actor,seed);int life=SkillHealth.Balance256(target);var rolled=NpcEquipmentDamage.Roll(actor,target);var after=NpcEquipmentRandom.State(actor).Seed;
                        string frozen=JsonSerializer.Serialize(rolled);
                        Require(rolled.Actor==actor.Serial.Value&&rolled.Target==target.Serial.Value&&rolled.Poison.CreditOwner==owner.Serial.Value&&SkillHealth.Balance256(target)==life
                            &&target.DamageCalls==0&&!target.Poisoned&&!TimedEffects.TryActor(target,out _),"Offensive roll committed health/states or lost ownership");
                        var gear=ClassGear.State(original);ClassGear.Profiles.Store(original,gear with{Identified=false});var disabled=NpcEquipmentDamage.Roll(actor,target);
                        Require(JsonSerializer.Serialize(rolled)==frozen,"Item changes rewrote an offensive snapshot");if(disabled.Damage!=rolled.Damage){changed++;}
                        ClassGear.Profiles.Store(original,gear);NpcEquipmentRandom.Set(actor,seed);
                        Require(NpcEquipmentDamage.TryResolve(actor,target,out _)&&NpcEquipmentRandom.State(actor).Seed==after,"Delivery changed offensive roll order/count");
                        Require(SkillHealth.Balance256(target)<life,"Existing delivery did not consume rolled weapon damage");count++;
                    }
                    finally{target.Delete();}
                }
            }
            finally{actor.Delete();}
        }
        Require(count==12&&changed>0,"Incomplete snapshot/activation/stream controls");
    }
    private static void Reward(PlayerMobile owner,IronGolemBody source)
    {
        var target=new GraveyardZombie{CantWalk=true};target.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();SkillHealth.SetBalance256(target,256);
        try
        {
            ClassCreation.Profiles.TryGet(owner.Serial,out var before);int xp=MonsterExperience.Award(CreatureProfiles.Experience(target),CreatureProfiles.Level(target),before.Level);Require(xp>0,"Nonzero reward fixture");
            var result=Release(source,target,256);
            ClassCreation.Profiles.TryGet(owner.Serial,out var after);
            Require(result.DeathModeAttempted&&after.Experience==before.Experience+xp&&ClassLoot.Killer(target)==owner&&Credit(target,source)==1&&Responsible(target,source,owner)==1,"Ordered pet kill lost reward/master/ledger");
            Require(target.Corpse is Corpse{SanctuaryNoRewards:true} corpse&&!RewardLife.Process(target,corpse),"Reward entitlement replayed or no corpse");
            ClassCreation.Profiles.TryGet(owner.Serial,out var repeated);Require(repeated.Experience==after.Experience,"Reward replay duplicated XP");
        }
        finally{target.Corpse?.Delete();target.Delete();}
    }
    private static void Check()
    {
        var owner=Owner();var original=Item(owner);var source=IronGolemBody.CreatePrepared(owner,original,original.Location);bool saved=false;
        try
        {
            CreditCases(owner,source);Rolls(owner);Reward(owner,source);
            var target=Target(owner);SkillHealth.SetBalance256(target,100*256-3);Release(source,target,20*256+19);var rolled=NpcEquipmentDamage.Roll(source,target);
            var state=new Saved(owner.Serial.Value,source.Serial.Value,target.Serial.Value,original.Serial.Value,SkillHealth.Balance256(target),Credit(target,source),Responsible(target,source,owner),JsonSerializer.Serialize(ClassGear.State(original)),JsonSerializer.Serialize(rolled));
            Require(state.Credit==20&&state.Responsible==20&&state.Life==80*256-22,"Saved credit fixture");
            target.Internalize();PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(state));saved=true;
            Log.Information("NPC preparation:12offensive snapshot/activation/stream controls,64native damage-ledger/master comparisons,actual Iron owner XP/once-only rewards and saved fractional life/original/credit pass;native damage notifications and live ordered actor migration remain separate");
        }
        finally{if(!saved){foreach(var pet in(owner.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Delete();}owner.Delete();}}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var source=World.FindMobile((Serial)saved.Actor) as IronGolemBody;var target=World.FindMobile((Serial)saved.Target) as NpcPoisonRecipient;var original=World.FindItem((Serial)saved.Item);
        Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==owner&&source.Map==Map.Internal&&original?.Parent==source&&JsonSerializer.Serialize(ClassGear.State(original))==saved.Gear
            &&target is{Alive:true,Deleted:false}&&SkillHealth.Balance256(target)==saved.Life&&target.DamageCalls==0,"Saved ordered life/original identity changed");
        // Native Mobile deliberately omits its temporary damage list from
        // serialization. Match that policy; do not invent a persistent ledger.
        Require(Credit(target,source)==0&&Responsible(target,source,owner)==0,"Native transient credit replayed across a world reload");
        var roll=JsonSerializer.Deserialize<NpcWeaponRoll>(saved.Roll);Require(roll.Actor==source.Serial.Value&&roll.Target==target.Serial.Value&&roll.Poison.CreditOwner==owner.Serial.Value,"Saved diagnostic offensive snapshot identity changed");
        Release(source,target,273);
        Require(SkillHealth.Balance256(target)==saved.Life-273&&Credit(target,source)==1&&Responsible(target,source,owner)==1&&target.DamageCalls==0,"Fresh post-reload credit was lost,duplicated or replayed native damage");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Life=saved.Life-273,Credit=1,Responsible=1}));
    }
}
