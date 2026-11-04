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

public static class NpcDefenseConditionsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcDefenseConditionsChecks));
    private sealed record Row(uint Owner,uint Revive,uint Golem,uint Material,uint Weapon,string Name,int Stat,int Value,int ReviveLife,int GolemLife,string Binding);
    private sealed record Saved(Row[] Rows,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-defense-conditions-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-defense-conditions-check",Check);Register("npc-defense-conditions-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"NPC-held player-only defense modifiers",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Registry()
    {
        foreach(int stat in new[]{115,116,117})foreach(int value in new[]{-1,0,1,33,99,255})
        {Require(NpcCombatEvents.FromStats([new(EquipmentStatKey.Pack(stat),value)]).Subscriptions().Length==0,"Player-only NPC stat created a subscription");}
        Require(NpcCombatEvents.FromStats([new(EquipmentStatKey.Pack(115),1),new(EquipmentStatKey.Pack(116),33),new(EquipmentStatKey.Pack(135),50)]).Subscriptions().SequenceEqual(new[]{135}),"Defense admission changed actual wound registration");
        foreach(int stat in new[]{66,120})
        {bool no=false;try{_=NpcCombatEvents.FromStats([new(EquipmentStatKey.Pack(stat),1)]);}catch(NotSupportedException){no=true;}Require(no,"Unreviewed conditional stat was admitted");}
    }
    private static void FieldsAndContact(PlayerMobile owner,BaseCreature source,int stat,int value)
    {
        var target=new ReviveEventChecks.Recipient{NoKillAwards=true,Frozen=true,CantWalk=true};target.SetHits(60000);
        foreach(var resistance in Enum.GetValues<ResistanceType>()){target.SetResistance(resistance,0);}
        target.MoveToWorld(new(source.X+1,source.Y,source.Z),source.Map);target.AIObject.AITimer.Stop();
        try
        {
            Require(!NamedCombat.Elite(target)&&!target.Controlled&&SourceBlocking.TryRate(target,out int block,out _)&&block==0,"Defense fixture requires an ordinary unshielded target");
            foreach(int defense in new[]{0,100,1000,5000})
            {
                Require(NamedCombat.DefenseFor(source,target,defense,false)==defense,"NPC item or player owner changed target defense");
                int expected=stat==115?0:defense*(100-value)/100;
                Require(NamedCombat.DefenseFor(owner,target,defense,false)==expected,"Real player item stopped applying its defense modifier");
            }
            int basis=ClassStats.Defense(target),attack=ClassStats.AttackRatingAgainst(source,target);
            double chance=ClassStats.HitChance(attack,basis,ClassStats.Level(source),ClassStats.Level(target));
            var unchanged=NpcEquipmentRandom.State(source);
            for(int draw=0;draw<100;draw++)
            {
                int captured=draw,calls=0;
                int result=NpcMeleeResult.Resolve(source,target,_=>true,()=>true,bound=>{Require(bound==100,"Wrong NPC hit bound");calls++;return captured;});
                Require(((result&1)!=0)==(draw/100.0<chance)&&calls==1&&NpcEquipmentRandom.State(source)==unchanged,"NPC defense condition changed current hit thresholds or source draws");
            }
            NpcEquipmentRandom.Set(source,SourceRandom.InitLow(173));var rules=IronGolemContact.Require(source,owner,target);
            var prepared=IronGolemContact.Prepare(source,target,rules,()=>1);ulong expectedSeed=NpcEquipmentRandom.State(source).Seed;
            bool wounds=stat==116&&SourceRandom.Roll(ref expectedSeed)%100<50;int life=SkillHealth.Balance256(target);
            _=IronGolemContact.Release(source,owner,target,prepared,_=>true,TimedEffects.Now);
            Require(prepared.Consumed&&target.Alive&&SkillHealth.Balance256(target)<life&&target.Notifications==1&&NpcEquipmentRandom.State(source).Seed==expectedSeed,"Defense item changed ordinary contact/event/RNG delivery");
            Require((TimedEffects.TryActor(target,out var effect)&&effect.OpenWounds!=null)==wounds,"Razoredge's independent wound effect was lost or duplicated");
        }
        finally{TimedEffects.Clear(target);target.Corpse?.Delete();target.Delete();}
    }
    private static void Exercise(Row row)
    {
        var owner=(PlayerMobile)World.FindMobile((Serial)row.Owner);var revived=(BaseCreature)World.FindMobile((Serial)row.Revive);var golem=(IronGolemBody)World.FindMobile((Serial)row.Golem);
        owner.MoveToWorld(new(7100,3380,0),Map.Trammel);PersistentMinions.Resume(owner);
        string code=row.Stat==115?"ignore-ac":"reduce-ac";
        Require(ClassGear.Bonus(owner,code)==row.Value&&ReviveEquipmentGraph.Current(ReviveNativeActivation.State(revived)).Read(row.Stat)==row.Value
            &&NpcEquipmentCombat.Context(golem).Value.Bonus(code)==row.Value,"Player/frozen/original defense property was discarded");
        foreach(var source in new BaseCreature[]{revived,golem})
        {source.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();source.Blessed=false;FieldsAndContact(owner,source,row.Stat,row.Value);}
    }
    private static Row Create(string name,int stat,int value)
    {
        var owner=ReviveEquippedChecks.Owner(3380);MagePetBuffChecks.Train(owner,"necromancer.irongolem",1);
        while(owner.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Actual weapon requirement Strength allocation");}
        var weapon=ReviveEquippedChecks.Named(name);owner.AddToBackpack(weapon);Require(owner.EquipItem(weapon),"Player defense item control equip failed: "+name);ClassGear.Refresh(owner);
        var loot=ReviveEquippedChecks.Named(name);var corpse=ReviveEquippedChecks.Die(owner,new Skeleton(),[loot]);var revived=ReviveOriginals.Find(corpse);
        Require(ReviveCasts.Plan(owner,corpse,out var plan,out _,out var issue)&&plan.EquipmentSwingProfile==ReviveSwings.Profile,"Player-only defense property still blocks public Revive: "+issue);
        NpcPreventHealChecks.Start(owner,CombatAbility.Revive,corpse);Require(revived.Alive&&revived.ControlMaster==owner&&loot.Parent==corpse,"Defense-item Revive lost original or independent loot");loot.Delete();
        var material=ReviveEquippedChecks.Named(name);material.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        Require(IronGolemCasting.Plan(owner,material,out _,out issue),"Player-only defense property still blocks Iron material: "+issue);NpcPreventHealChecks.Start(owner,CombatAbility.IronGolem,material);
        var golem=NecromancerGolems.For(owner).OfType<IronGolemBody>().Single();Require(material.Parent==golem,"Defense-item material was lost or cloned");
        var row=new Row(owner.Serial.Value,revived.Serial.Value,golem.Serial.Value,material.Serial.Value,weapon.Serial.Value,name,stat,value,0,0,ReviveEquipmentLife.Identity(ReviveNativeActivation.State(revived)));
        Exercise(row);SkillHealth.SetBalance256(revived,SkillHealth.Maximum256(revived)*2/3+17);SkillHealth.SetBalance256(golem,SkillHealth.Maximum256(golem)*2/3+19);
        row=row with{ReviveLife=SkillHealth.Balance256(revived),GolemLife=SkillHealth.Balance256(golem)};
        PersistentMinions.Suspend(owner);owner.Internalize();return row;
    }
    private static void Check()
    {Registry();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved([Create("The Diggler",115,1),Create("Razoredge",116,33)])));}
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Rows.Length==2,"Missing saved defense-item cohort");
        foreach(var row in saved.Rows)
        {
            var owner=World.FindMobile((Serial)row.Owner)as PlayerMobile;var revived=World.FindMobile((Serial)row.Revive)as BaseCreature;var golem=World.FindMobile((Serial)row.Golem)as IronGolemBody;
            Require(revived is{Alive:true}&&golem is{Alive:true}&&revived.Map==Map.Internal&&golem.Map==Map.Internal&&revived.ControlMaster==owner&&golem.ControlMaster==owner
                &&SkillHealth.Balance256(revived)==row.ReviveLife&&SkillHealth.Balance256(golem)==row.GolemLife&&ReviveEquipmentLife.Identity(ReviveNativeActivation.State(revived))==row.Binding
                &&World.FindItem((Serial)row.Material)?.Parent==golem&&World.FindItem((Serial)row.Weapon)?.Parent==owner,"Saved defense originals,stats or wounds changed");
            Exercise(row);PersistentMinions.Suspend(owner);owner.Internalize();
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
