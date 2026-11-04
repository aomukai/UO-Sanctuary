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

public static class NpcPreventHealChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcPreventHealChecks));
    private sealed record Saved(uint Owner,uint Revive,uint Golem,uint Material,uint Weapon,int ReviveLife,int GolemLife,string Binding,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-prevent-heal-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-prevent-heal-check",Check);Register("npc-prevent-heal-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Ordinary NPC-held Prevent Monster Heal is inert",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        int count=0;
        foreach(string name in new[]{"classic","ubers"})
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-prevent-heal-"+name+"-cases.json")));
            Require(doc.RootElement.GetArrayLength()==1980,"Missing complete PMH source matrix");
            foreach(var row in doc.RootElement.EnumerateArray())
            {
                int a=row[0].GetInt32(),d=row[1].GetInt32(),value=row[3].GetInt32(),reads=row[4].GetInt32(),applied=row[5].GetInt32();
                Require(reads==(a==0&&d==1?1:0)&&row[6].GetInt32()==(applied==1?120000:0),"Source PMH guard/duration changed");
                if(a!=0||d!=1||value==0){Require(applied==0,"Source applies PMH outside its player/monster/nonzero lane");}
                if(a==1)
                {Require(NpcCombatEvents.FromStats([new(EquipmentStatKey.Pack(117),value)]).Subscriptions().Length==0&&applied==0&&reads==0,"NPC stat117 created a callback or source stat read");}
                count++;
            }
        }
        Require(count==3960,"Missing PMH source controls");
        Require(NpcCombatEvents.FromStats([new(EquipmentStatKey.Pack(117),1),new(EquipmentStatKey.Pack(78),2),new(EquipmentStatKey.Pack(135),3),new(EquipmentStatKey.Pack(136),4)]).Subscriptions().SequenceEqual(new[]{78,135,136}),"Dormant PMH changed other event subscriptions");
        foreach(int stat in new[]{66,115,116,120})
        {bool no=false;try{_=NpcCombatEvents.FromStats([new(EquipmentStatKey.Pack(stat),1)]);}catch(NotSupportedException){no=true;}Require(no,"Unrelated conditional hit property was admitted: "+stat);}
    }
    private static void Start(PlayerMobile owner,CombatAbility ability,Item selected)
    {
        Require(SkillBindings.Instance.AssignQuick(owner,7,ability),"PMH fixture quick binding failed");ClassCombat.Stop(owner);var action=ClassCombat.For(owner);action.NextAction=0;
        int before=NecromancerEffects.ManaBalance256(owner),cost=SkillMechanics.Values(owner,ability).Mana256;
        Require(SkillBindingNetwork.Use(owner,DiabloClass.Necromancer,7,ability,selected.Serial,selected.Location),"PMH learned cast refused");
        Require(NecromancerEffects.ManaBalance256(owner)==before&&!ClassCombat.ResolveAction(owner,action.EffectAt-1),"PMH cast paid/activated early");
        Require(ClassCombat.ResolveAction(owner,action.EffectAt)&&NecromancerEffects.ManaBalance256(owner)==before-cost,"PMH learned cast failed or did not pay once");
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt)&&NecromancerEffects.ManaBalance256(owner)==before-cost,"PMH learned cast replayed payment");
    }
    private static void Contact(PlayerMobile owner,BaseCreature source)
    {
        Require(NpcCombatEvents.ForSource(source).Subscriptions().Length==0,"NPC PMH retained a damage-event subscription");
        foreach(bool blocked in new[]{false,true})
        {
            var target=new ReviveEventChecks.Recipient{NoKillAwards=true,Frozen=true,CantWalk=true};target.SetHits(60000);
            foreach(var r in Enum.GetValues<ResistanceType>()){target.SetResistance(r,0);}
            target.MoveToWorld(new(source.X+1,source.Y,source.Z),source.Map);target.AIObject.AITimer.Stop();
            try
            {
                var at=TimedEffects.Now;if(blocked){TimedEffects.PreventHealing(target,at);}
                var prior=TimedEffects.TryActor(target,out var effect)?effect.HealBlockedUntil:default;
                var rules=IronGolemContact.Require(source,owner,target);NpcEquipmentRandom.Set(source,SourceRandom.InitLow(173));
                var prepared=IronGolemContact.Prepare(source,target,rules,()=>1);var seed=NpcEquipmentRandom.State(source);int hp=SkillHealth.Balance256(target);
                _=IronGolemContact.Release(source,owner,target,prepared,_=>true,at);
                Require(prepared.Consumed&&target.Alive&&SkillHealth.Balance256(target)<hp&&target.Notifications==1&&NpcEquipmentRandom.State(source)==seed,"PMH changed ordinary NPC contact,notifications or release RNG");
                var after=TimedEffects.TryActor(target,out effect)?effect.HealBlockedUntil:default;
                Require(after==prior&&TimedEffects.CanRegenerateHits(target)==!blocked,"NPC item/owner PMH created/refreshed/removed healing prevention");
                if(!blocked)
                {
                    // Same actual owner/item keeps the existing player effect.
                    NamedCombat.AfterStrike(owner,target,false,new Random(173));
                    Require(TimedEffects.TryActor(target,out effect)&&effect.HealBlockedUntil>TimedEffects.Now.AddMinutes(79)&&!TimedEffects.CanRegenerateHits(target),"Player PMH positive control stopped working");
                }
            }
            finally{TimedEffects.Clear(target);target.Corpse?.Delete();target.Delete();}
        }
    }
    private static void Exercise(PlayerMobile owner,BaseCreature revived,IronGolemBody golem)
    {
        owner.MoveToWorld(new(7100,3440,0),Map.Trammel);PersistentMinions.Resume(owner);
        foreach(var source in new BaseCreature[]{revived,golem})
        {source.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();source.Blessed=false;Contact(owner,source);}
        var state=ReviveNativeActivation.State(revived);
        Require(ReviveEquipmentGraph.Current(state).Read(117)==1&&NpcEquipmentCombat.Context(golem).Value.Bonus("noheal")==1&&ClassGear.Bonus(owner,"noheal")==1,"PMH raw/frozen/original/player supplier was discarded");
    }
    private static void Check()
    {
        Source();var owner=ReviveEquippedChecks.Owner(3440);MagePetBuffChecks.Train(owner,"necromancer.irongolem",1);
        var held=ReviveEquippedChecks.Named("The Grim Reaper");owner.AddToBackpack(held);Require(owner.EquipItem(held),"Player PMH control weapon equip failed");ClassGear.Refresh(owner);
        var loot=ReviveEquippedChecks.Named("The Grim Reaper");var corpse=ReviveEquippedChecks.Die(owner,new Skeleton(),[loot]);var revived=ReviveOriginals.Find(corpse);
        Require(ReviveCasts.Plan(owner,corpse,out var plan,out _,out var issue)&&plan.EquipmentSwingProfile==ReviveSwings.Profile,"Dormant PMH still blocks learned Revive: "+issue);
        Start(owner,CombatAbility.Revive,corpse);Require(revived.Alive&&revived.ControlMaster==owner&&loot.Parent==corpse,"PMH Revive lost original or independent loot");loot.Delete();
        var material=ReviveEquippedChecks.Named("The Grim Reaper");material.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        Require(IronGolemCasting.Plan(owner,material,out _,out issue),"Dormant PMH still blocks learned Iron material: "+issue);Start(owner,CombatAbility.IronGolem,material);
        var golem=NecromancerGolems.For(owner).OfType<IronGolemBody>().Single();Require(material.Parent==golem,"PMH material was lost or cloned");
        Exercise(owner,revived,golem);SkillHealth.SetBalance256(revived,SkillHealth.Maximum256(revived)*2/3+17);SkillHealth.SetBalance256(golem,SkillHealth.Maximum256(golem)*2/3+19);
        var saved=new Saved(owner.Serial.Value,revived.Serial.Value,golem.Serial.Value,material.Serial.Value,held.Serial.Value,SkillHealth.Balance256(revived),SkillHealth.Balance256(golem),ReviveEquipmentLife.Identity(ReviveNativeActivation.State(revived)));
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
        var revived=World.FindMobile((Serial)saved.Revive)as BaseCreature;var golem=World.FindMobile((Serial)saved.Golem)as IronGolemBody;
        Require(revived is{Alive:true}&&golem is{Alive:true}&&revived.Map==Map.Internal&&golem.Map==Map.Internal&&revived.ControlMaster==owner&&golem.ControlMaster==owner
            &&SkillHealth.Balance256(revived)==saved.ReviveLife&&SkillHealth.Balance256(golem)==saved.GolemLife&&ReviveEquipmentLife.Identity(ReviveNativeActivation.State(revived))==saved.Binding
            &&World.FindItem((Serial)saved.Material)?.Parent==golem&&World.FindItem((Serial)saved.Weapon)?.Parent==owner,"PMH originals,frozen stats or wounds changed on reload");
        Exercise(owner,revived,golem);PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
