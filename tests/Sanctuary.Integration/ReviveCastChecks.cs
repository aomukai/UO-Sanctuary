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

public static class ReviveCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveCastChecks));
    private sealed record Saved(uint Owner,uint Active,uint ActiveCorpse,uint Pending,uint PendingActor,int[] Origin,int Life256,int Mana,int Stamina,int Ai);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-cast-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-cast-check",Check);Register("revive-cast-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Revive windup/payment and automatic original retention",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Corpse Die(PlayerMobile owner,BaseCreature actor=null)
    {
        actor??=new Skeleton();actor.NoKillAwards=true;actor.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();
        actor.Mana=Math.Min(7,actor.ManaMax);actor.Stam=Math.Min(13,actor.StamMax);
        Corpse corpse=null;var original=actor;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==original){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(actor,true);actor.Damage(actor.Hits+1,owner);CombatDamage.Finish(actor,owner);}
        finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==actor,"Ordinary death hooks did not retain the actual monster");
        Require(ReviveBeforeDeath.Decode(ReviveOriginals.State(corpse).BeforeDeathJson).AiStatus!=null,"Automatic accepted-death eligibility missing");
        corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    private static void Begin(PlayerMobile owner,Corpse corpse)
    {
        var action=ClassCombat.For(owner);action.PendingAbility=CombatAbility.None;action.NextAction=0;
        Require(ReviveCasts.Plan(owner,corpse,out _,out _,out var issue),"Learned cast preflight: "+issue);
        Require(SkillBindings.Instance.GetQuick(owner.Serial,DiabloClass.Necromancer,0)==CombatAbility.Revive,"Revive hotkey binding missing");
        Require(SkillBindingNetwork.Use(owner,DiabloClass.Necromancer,0,CombatAbility.Revive,corpse.Serial,corpse.Location),
            $"Learned Revive did not begin:mana={NecromancerEffects.ManaBalance256(owner)},cost={SkillMechanics.Values(owner,CombatAbility.Revive).Mana256},available={NecromancerCombat.Available(owner,CombatAbility.Revive)},frozen={owner.Frozen},spell={owner.Spell},recovery={HitRecovery.Active(owner,Core.TickCount)}");
    }
    private static bool Finish(PlayerMobile owner)
    {
        long effect=ClassCombat.For(owner).EffectAt;bool result=ClassCombat.ResolveAction(owner,effect);
        int mana=NecromancerEffects.ManaBalance256(owner);
        Require(!ClassCombat.ResolveAction(owner,effect)&&NecromancerEffects.ManaBalance256(owner)==mana,"Duplicate release charged or created a second original");
        return result;
    }
    private static BaseCreature Cast(PlayerMobile owner,Corpse corpse)
    {
        var actor=ReviveOriginals.Find(corpse);var before=ReviveBeforeDeath.Decode(ReviveOriginals.State(corpse).BeforeDeathJson);
        int mana=NecromancerEffects.ManaBalance256(owner),cost=SkillMechanics.Values(owner,CombatAbility.Revive).Mana256;
        Begin(owner,corpse);
        Require(!ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt-1)&&NecromancerEffects.ManaBalance256(owner)==mana,"Windup released or charged early");
        Require(Finish(owner),"Learned release failed");
        Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&ReviveNativeActivation.State(actor)!=null
            &&actor.Mana==before.Mana&&actor.Stam==before.Stamina&&NecromancerEffects.ManaBalance256(owner)==mana-cost,
            "Original identity, captured resources or once-only cost failed");
        Require(CorpseSkillRules.Profiles.Find(corpse) is{UnitReactivated:true,NoSelect:true}&&corpse.GetAmount(typeof(Gold))==1
            &&PersistentMinions.Profiles.Find(actor).ExpiresUtc==null,"Loot separated incorrectly or expiration added");
        Require(!NecromancerCombat.Release(owner,CombatAbility.Revive,corpse.Serial,corpse.Location)
            &&NecromancerEffects.ManaBalance256(owner)==mana-cost,"Consumed corpse charged twice");
        return actor;
    }
    private static void Check()
    {
        var owner=ReviveNativeActivationChecks.Owner();FixtureSpace.Corridor(owner,6450,35,2600);owner.RawInt=200;owner.Mana=owner.ManaMax;
        Require(NecromancerCombat.Implemented(CombatAbility.Revive)&&NecromancerCombat.Handles(CombatAbility.Revive)
            &&NecromancerCombat.Available(owner,CombatAbility.Revive),"Learned registration/dispatch missing");
        Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Revive)&&SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Revive),"Right/quick Revive binding unavailable");
        var corpse=Die(owner);var active=Cast(owner,corpse);
        owner.Mana=owner.ManaMax;var interrupted=Die(owner);int mana=NecromancerEffects.ManaBalance256(owner);var origin=owner.Location;
        Begin(owner,interrupted);owner.MoveToWorld(new(origin.X,origin.Y+1,origin.Z),owner.Map);
        Require(!Finish(owner)&&NecromancerEffects.ManaBalance256(owner)==mana&&!CorpseSkillRules.NoSelect(interrupted)
            &&ReviveOriginals.Find(interrupted) is{Alive:false},"Movement interruption consumed mana/corpse");owner.MoveToWorld(origin,owner.Map);interrupted.Delete();
        var reserved=Die(owner);Begin(owner,reserved);Require(CorpseSkillRules.Reserve(reserved),"Competing corpse consumer");
        Require(!Finish(owner)&&NecromancerEffects.ManaBalance256(owner)==mana,"Competing reservation charged Revive");reserved.Delete();
        var pending=Die(owner);Begin(owner,pending);NecromancerEffects.SetMana256(owner,0);
        Require(!Finish(owner)&&NecromancerEffects.ManaBalance256(owner)==0&&!CorpseSkillRules.NoSelect(pending)
            &&ReviveOriginals.Find(pending) is{Alive:false},"Release with insufficient mana consumed the original");
        Require(!NecromancerCombat.Begin(owner,CombatAbility.Revive,pending.Serial,pending.Location,Core.TickCount+100000),"Mana-free windup started");
        owner.Mana=owner.ManaMax;
        var originals=ReviveOriginals.Snapshot();var saved=ReviveOriginals.State(pending);var captured=ReviveBeforeDeath.Decode(saved.BeforeDeathJson);
        ReviveOriginals.Restore(originals.Select(r=>r==saved?r with{BeforeDeathJson=JsonSerializer.Serialize(captured with{AiStatus=null})}:r).ToArray());
        Require(!ReviveCasts.Plan(owner,pending,out _,out _,out var legacy)&&legacy.Contains("older corpse"),"Legacy eligibility fabricated");ReviveOriginals.Restore(originals);
        var dragon=Die(owner,new Dragon());Require(!ReviveCasts.Plan(owner,dragon,out _,out _,out var classification)&&classification.Contains("classification"),"Unclassified monster silently converted");dragon.Delete();
        var equipped=new Skeleton();equipped.AddItem(new GoldRing());var gear=Die(owner,equipped);
        Require(!ReviveCasts.Plan(owner,gear,out _,out _,out var gearIssue)&&gearIssue.Contains("equipped combat effects")
            &&gear.Items.Any(i=>i is GoldRing),"Unsupported equipped effects omitted or gear loot lost");gear.Delete();
        foreach(bool consumed in new[]{false,true})
        {
            var retired=Die(owner);var original=ReviveOriginals.Find(retired);var row=CorpseSkillRules.Profiles.Find(retired);
            if(consumed){Require(CorpseSkillRules.Reserve(retired),"Retirement reservation");}
            else{CorpseSkillRules.Profiles.Store(row with{BaseLife=null});}
            ReviveOriginals.Prune();
            Require(original.Deleted&&ReviveOriginals.Find(retired)==null&&!retired.Deleted&&retired.GetAmount(typeof(Gold))==1,
                "Consumed/legacy projection did not retire its dead original independently of loot");retired.Delete();
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,active.Serial.Value,corpse.Serial.Value,pending.Serial.Value,
            ReviveOriginals.Find(pending).Serial.Value,[origin.X,origin.Y,origin.Z],SkillHealth.Balance256(active),active.Mana,active.Stam,(int)active.AI)));
        owner.Internalize();Report();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var active=(BaseCreature)World.FindMobile((Serial)saved.Active);var corpse=(Corpse)World.FindItem((Serial)saved.ActiveCorpse);
        var pending=(Corpse)World.FindItem((Serial)saved.Pending);
        Require(active is{Alive:true,Deleted:false}&&active.ControlMaster==owner&&active.Map==Map.Internal&&(int)active.AI==saved.Ai
            &&SkillHealth.Balance256(active)==saved.Life256&&active.Mana>=saved.Mana&&active.Mana<=active.ManaMax
            &&active.Stam>=saved.Stamina&&active.Stam<=active.StamMax
            &&corpse.GetAmount(typeof(Gold))==1,
            $"Actual reload:alive={active.Alive},deleted={active.Deleted},owner={active.ControlMaster?.Serial.Value}/{saved.Owner},map={active.Map},ai={(int)active.AI}/{saved.Ai},life={SkillHealth.Balance256(active)}/{saved.Life256},mana={active.Mana}/{saved.Mana},stamina={active.Stam}/{saved.Stamina},loot={corpse.GetAmount(typeof(Gold))}");
        Require(ReviveOriginals.Find(pending)?.Serial.Value==saved.PendingActor&&!CorpseSkillRules.NoSelect(pending),"Actual reload lost pending original");
        owner.MoveToWorld(new(saved.Origin[0],saved.Origin[1],saved.Origin[2]),pending.Map);owner.Mana=owner.ManaMax;
        Require(ReviveCasts.Plan(owner,pending,out _,out _,out _),"Saved pending original cannot be cast");
        // Use a fresh ordinary automatic death/cast each boot; keep the saved
        // pending original for both actual reload checks.
        var fresh=Die(owner);var revived=Cast(owner,fresh);revived.Delete();fresh.Delete();owner.Internalize();Report();
    }
    private static void Report()=>Log.Information("Learned Revive:automatic accepted-death retention,hotkey windup/release,original actor/exact activation resources,once-only mana,duplicate refusal,movement/reservation/lost-mana cancellation,explicit legacy/classification/item refusals,consumed/legacy original retirement with loot intact and active/pending persistence pass;native mana/stamina may regenerate while parked;native repertoire retained,world spell conversion independent");
}
