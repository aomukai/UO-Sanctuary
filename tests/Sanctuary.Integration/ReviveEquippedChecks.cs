// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ReviveEquippedChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveEquippedChecks));
    private sealed record Active(uint Actor,uint Corpse,int Life,string Binding,int Ai);
    private sealed record Saved(uint Owner,Active[] Actors,uint Pending,uint Original,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-equipped-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class UnconvertedGear:Item
    {
        [Constructible]public UnconvertedGear():base(0x1088){Layer=Layer.Neck;}
        public UnconvertedGear(Serial serial):base(serial){}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    public sealed class CustomSkeleton:Skeleton
    {
        [Constructible]public CustomSkeleton(){}
        public CustomSkeleton(Serial serial):base(serial){}
        public override void OnDamage(int amount,Mobile source,bool kill)=>base.OnDamage(amount,source,kill);
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    public static void Configure()
    {
        Register("revive-equipped-check",Check);Register("revive-equipped-reload-check",Reload);
        foreach(string mode in new[]{"right","quick"})
        {string label="revive-equipped-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Wire(n,label)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned equipped original admission and prepayment refusals",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    internal static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    internal static PlayerMobile Owner(int row=3600)
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureReviveEquippedOwner";MagePetBuffChecks.Train(owner,"necromancer.revive",20);MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",20);
        FixtureSpace.Corridor(owner,7100,24,row);Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Revive)&&SkillBindings.Instance.AssignQuick(owner,7,CombatAbility.Revive),"Equipped Revive bindings failed");return owner;
    }
    internal static Corpse Die(PlayerMobile owner,BaseCreature body,Item[] gear)
    {
        body.NoKillAwards=true;body.Name="PoisonDaggerFixtureReviveEquipped";foreach(var item in body.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        foreach(var item in gear){body.AddItem(item);}Require(gear.All(i=>i.Parent==body),"Equipped corpse fixture slots collided");
        body.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);body.AIObject.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==body){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(body,true);body.Damage(body.Hits+1,owner);CombatDamage.Finish(body,owner);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==body&&gear.All(i=>i.Parent==corpse),"Automatic equipped original/loot retention failed");corpse.BeginDecay(FixtureSpace.Retention);return corpse;
    }
    private static Item[] Gear()=>new[]{"The Gnasher","Swordback Hold","Goblin Toe"}.Select(Named).ToArray();
    private static Active Cast(PlayerMobile owner,Corpse corpse,bool release=true)
    {
        var original=ReviveOriginals.Find(corpse);var ai=original.AI;int mana=NecromancerEffects.ManaBalance256(owner),cost=SkillMechanics.Values(owner,CombatAbility.Revive).Mana256;
        var rng=NpcEquipmentRandom.State(original);string before=JsonSerializer.Serialize(ReviveNativeActivation.Snapshot());
        Require(ReviveCasts.Plan(owner,corpse,out var plan,out _,out var issue)&&plan.EquipmentSwingProfile==ReviveSwings.Profile,"Equipped public preflight: "+issue);
        Require(NpcEquipmentRandom.State(original)==rng&&JsonSerializer.Serialize(ReviveNativeActivation.Snapshot())==before&&!original.Alive,"Equipped preflight initialized RNG or activated a body");
        var action=ClassCombat.For(owner);action.NextAction=0;Require(SkillBindingNetwork.Use(owner,DiabloClass.Necromancer,7,CombatAbility.Revive,corpse.Serial,corpse.Location),"Equipped learned hotkey did not start");
        Require(NecromancerEffects.ManaBalance256(owner)==mana&&!original.Alive&&!ClassCombat.ResolveAction(owner,action.EffectAt-1),"Equipped windup paid or activated early");
        if(!release){return new(original.Serial.Value,corpse.Serial.Value,0,"",(int)ai);}
        Require(ClassCombat.ResolveAction(owner,action.EffectAt)&&original.Alive&&original.ControlMaster==owner&&original.AI==ai&&NecromancerEffects.ManaBalance256(owner)==mana-cost,"Equipped release lost identity/AI or once-only payment");
        var state=ReviveNativeActivation.State(original);Require(state.Setup.EquipmentSwingProfile==ReviveSwings.Profile&&ReviveEquipmentIncoming.Handles(original)&&CorpseSkillRules.NoSelect(corpse),"Equipped release omitted composed mechanics or consumption marker");
        int paid=NecromancerEffects.ManaBalance256(owner);Require(!ClassCombat.ResolveAction(owner,action.EffectAt)&&!ReviveCasts.Release(owner,corpse)&&NecromancerEffects.ManaBalance256(owner)==paid,"Equipped duplicate release charged again");
        foreach(var item in corpse.Items.Where(i=>i is not Gold).ToArray()){owner.AddToBackpack(item);item.Delete();}
        var snapshot=ReviveEquipmentOffense.Capture(original);Require(snapshot.Physical.Maximum>=snapshot.Physical.Minimum&&NpcOpenWounds.Chance(original)==100&&NpcCrushing.Chance(original)==45,"Equipped learned body lost frozen offense/events after loot deletion");
        SkillHealth.SetBalance256(original,SkillHealth.Maximum256(original)*2/3+17);return new(original.Serial.Value,corpse.Serial.Value,SkillHealth.Balance256(original),ReviveEquipmentLife.Identity(state),(int)ai);
    }
    private static void Refusal(PlayerMobile owner,Corpse corpse,string reason)
    {
        var actor=ReviveOriginals.Find(corpse);int mana=NecromancerEffects.ManaBalance256(owner);var rng=NpcEquipmentRandom.State(actor);string roster=JsonSerializer.Serialize(ReviveNativeActivation.Snapshot());var loot=corpse.Items.Select(i=>i.Serial.Value).Order().ToArray();
        Require(!ReviveCasts.Plan(owner,corpse,out var setup,out _,out var issue)&&setup==null&&issue.Contains(reason),"Equipped refusal expected "+reason+": "+issue);
        Require(!ReviveCasts.Release(owner,corpse)&&NecromancerEffects.ManaBalance256(owner)==mana&&NpcEquipmentRandom.State(actor)==rng&&!actor.Alive&&!CorpseSkillRules.NoSelect(corpse)
            &&JsonSerializer.Serialize(ReviveNativeActivation.Snapshot())==roster&&corpse.Items.Select(i=>i.Serial.Value).Order().SequenceEqual(loot),"Refused equipped cast changed mana/stream/body/roster/loot");
    }
    private static void Rejects(PlayerMobile owner)
    {
        foreach(var scene in new[]{"native","registration","numeric","missile","body","element"})
        {
            var body=scene=="body"?new CustomSkeleton():new Skeleton();Item[] gear=scene switch
            {"native"=>[new UnconvertedGear()],"registration"=>[Named("Tyrael's Might")],"numeric"=>[Named("Baranar's Star")],"missile"=>[new Bow()],_=>Gear()};
            if(scene=="missile"){ClassGear.Profiles.Ensure(gear[0]);}
            if(scene=="element"){body.SetDamageType(ResistanceType.Physical,0);body.SetDamageType(ResistanceType.Fire,100);}
            var corpse=Die(owner,body,gear);
            try{Refusal(owner,corpse,scene switch{"native"=>"Native item combat effects","registration"=>"consumer","numeric"=>"numeric","missile"=>"projectile","body"=>"override",_=>"elemental body"});}
            finally{corpse.Delete();ReviveOriginals.Prune();}
        }
        var pending=Die(owner,new Skeleton(),Gear());var all=ReviveOriginals.Snapshot();var saved=ReviveOriginals.State(pending);var before=ReviveBeforeDeath.Decode(saved.BeforeDeathJson);
        try
        {
            var equipment=ReviveBeforeDeath.Equipment(before).Select(e=>e with{CombatJson=JsonSerializer.Serialize(FrozenEquipmentEffects.Decode<FrozenCombatSnapshot>(e.CombatJson)with{Basis=FrozenEquipmentEffects.Decode<FrozenCombatSnapshot>(e.CombatJson).Basis with{WeaponSpeed=null}})}).ToArray();
            ReviveOriginals.Restore(all.Select(r=>r==saved?r with{BeforeDeathJson=JsonSerializer.Serialize(before with{EquipmentJson=JsonSerializer.Serialize(equipment)})}:r).ToArray());Refusal(owner,pending,"Historical frozen weapon");
        }
        finally{ReviveOriginals.Restore(all);pending.Delete();ReviveOriginals.Prune();}
    }
    private static void Check()
    {
        var owner=Owner();owner.Mana=owner.ManaMax;Rejects(owner);
        var corpses=new[]{Die(owner,new Skeleton(),Gear()),Die(owner,new Zombie(),Gear())};var active=corpses.Select(c=>Cast(owner,c)).ToArray();
        var pending=Die(owner,new Skeleton(),Gear());int cost=SkillMechanics.Values(owner,CombatAbility.Revive).Mana256;NecromancerEffects.SetMana256(owner,cost-1);
        var action=ClassCombat.For(owner);action.NextAction=0;Require(ReviveCasts.Plan(owner,pending,out _,out _,out _)&&!SkillBindingNetwork.Use(owner,DiabloClass.Necromancer,7,CombatAbility.Revive,pending.Serial,pending.Location)&&NecromancerEffects.ManaBalance256(owner)==cost-1,"Underfunded equipped cast paid/started");
        NecromancerEffects.SetMana256(owner,cost+17);_=Cast(owner,pending,false);int mana=NecromancerEffects.ManaBalance256(owner);owner.X++;
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt)&&NecromancerEffects.ManaBalance256(owner)==mana&&!CorpseSkillRules.NoSelect(pending),"Movement cancellation charged or consumed equipped corpse");owner.X--;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,active,pending.Serial.Value,ReviveOriginals.Find(pending).Serial.Value)));owner.Internalize();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
        foreach(var row in saved.Actors)
        {
            var actor=World.FindMobile((Serial)row.Actor)as BaseCreature;Require(actor is{Alive:true,Deleted:false}&&actor.Map==Map.Internal&&actor.ControlMaster==owner&&(int)actor.AI==row.Ai&&SkillHealth.Balance256(actor)==row.Life
                &&ReviveSwings.Handles(actor)&&ReviveEquipmentLife.Identity(ReviveNativeActivation.State(actor))==row.Binding,"Learned equipped original lost profile/body/wounds/immutable binding on reload");
        }
        var pending=World.FindItem((Serial)saved.Pending)as Corpse;owner.MoveToWorld(new(pending.X-2,pending.Y,pending.Z),pending.Map);owner.Mana=owner.ManaMax;
        Require(ReviveOriginals.Find(pending)?.Serial.Value==saved.Original&&ReviveCasts.Plan(owner,pending,out _,out _,out _),"Saved equipped corpse lost cast availability");owner.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void Wire(NetState net,string label)
    {
        var owner=Owner(3500);var account=new Account("revive-equipped-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=owner;net.Account=account;ReviveNativeActivationChecks.Attach(net,owner);
        var corpse=Die(owner,new Skeleton(),Gear());var actor=ReviveOriginals.Find(corpse);int cost=SkillMechanics.Values(owner,CombatAbility.Revive).Mana256;NecromancerEffects.SetMana256(owner,cost+17);
        var action=ClassCombat.For(owner);action.NextAction=0;var deadline=TimedEffects.Now.AddSeconds(15);
        bool began=false;
        void Inspect()
        {
            try
            {
                Require(TimedEffects.Now<deadline,"Equipped Revive input timeout");
                if(action.PendingAbility==CombatAbility.Revive&&!began){Require(!actor.Alive&&NecromancerEffects.ManaBalance256(owner)==cost+17,"Wire windup paid/activated early");began=true;Log.Information("{Label}: windup",label);}
                if(actor.Alive)
                {
                    Require(began&&actor.ControlMaster==owner&&ReviveSwings.Handles(actor)&&NecromancerEffects.ManaBalance256(owner)==17&&CorpseSkillRules.NoSelect(corpse),"Actual equipped input did not activate same composed original once");
                    actor.Delete();corpse.Delete();net.Mobile=null;owner.NetState=null;owner.Delete();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){Log.Error(e,"{Label}-result: failed",label);actor.Delete();corpse.Delete();net.Mobile=null;owner.NetState=null;owner.Delete();}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Revive,(int)DiabloClass.Necromancer,corpse.Serial.Value,corpse.X,corpse.Y,corpse.Z);Inspect();});
    }
}
