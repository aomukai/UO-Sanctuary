// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class NpcRestInPeaceChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcRestInPeaceChecks));
    private sealed record Held(uint Owner,uint Source,uint Item,string Gear,string Numeric,int Life,int Mana,int OwnerLife,int OwnerMana,ulong Seed,uint Corpse,uint Loot,uint Pending,int PendingLife);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-rest-in-peace-fixtures.json");
    private static void Require(bool ok,string message){if(!ok){throw new InvalidOperationException(message);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static Item Original()=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Tyrael's Might").Id,99,new Random(173),true);
    private static void SourceCases()
    {
        uint[] original=NpcRestInPeace.Snapshot();var source=new Skeleton();var target=new Skeleton();
        try
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-rest-in-peace-cases.json")));int count=0;
            foreach(var row in doc.RootElement.EnumerateArray())
            {
                int[] a=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();NpcRestInPeace.Restore(original);
                if(a[2]!=0){NpcRestInPeace.Apply(source,target);}
                bool result=NpcRestInPeace.Apply(a[0]!=0?source:null,a[1]!=0?target:null);
                Require(result==(a[5]!=0)&&NpcRestInPeace.Marked(target)==(a[6]!=0)&&a[7]==(result?1:0),"Complete RIP callback control differs: "+count);count++;
            }
            Require(count==128,"Incomplete RIP source controls");NpcRestInPeace.Restore(original);
            foreach(uint[] invalid in new uint[][]{null,[0],[0x40000001],[source.Serial.Value,source.Serial.Value]})
            {
                bool refused=false;try{NpcRestInPeace.Restore(invalid);}catch(InvalidDataException){refused=true;}
                Require(refused&&NpcRestInPeace.Snapshot().SequenceEqual(original),"Invalid RIP state mutated the saved unit set");
            }
            Require(NpcCombatEvents.RestoreSubscriptions([108]).Subscriptions().SequenceEqual(new[]{108}),"RIP subscription restore missing");
        }
        finally{NpcRestInPeace.Restore(original);source.Delete();target.Delete();}
    }
    private static PlayerMobile Owner()=>NpcItemKnockbackChecks.Owner(new NpcKillRecoveryChecks.RecoveryOwner());
    private static GraveyardSkeleton Target(PlayerMobile p,BaseCreature source,int life=256)
    {
        var t=new GraveyardSkeleton{Frozen=true,CantWalk=true};t.MoveToWorld(new(source.X+1,source.Y,source.Z),p.Map);t.AIObject?.AITimer.Stop();
        foreach(var kind in Enum.GetValues<ResistanceType>()){t.SetResistance(kind,0);}
        SkillHealth.SetBalance256(t,life);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static Corpse Kill(PlayerMobile p,BaseCreature source,bool expected)
    {
        var target=Target(p,source);var oldBlock=SourceBlocking.BlockRoll;var oldChoice=SourceBlocking.ChoiceRoll;
        int ownerMana=NecromancerEffects.ManaBalance256(p),ownerLife=SkillHealth.Balance256(p);
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=n=>Math.Min(1,n-1);
            var rules=IronGolemContact.Require(source,p,target);
            var miss=IronGolemContact.Prepare(source,target,rules,()=>0);IronGolemContact.Release(source,p,target,miss,_=>true,TimedEffects.Now);
            Require(target.Alive&&!NpcRestInPeace.Marked(target),"A miss applied RIP");
            var hit=IronGolemContact.Prepare(source,target,rules,()=>5);var seed=NpcEquipmentRandom.State(source);
            var result=IronGolemContact.Release(source,p,target,hit,_=>true,TimedEffects.Now);
            var corpse=target.Corpse as Corpse;
            Require(!target.Alive&&target.LastKiller==source&&result.DeathModeAttempted&&corpse is{Deleted:false}
                &&corpse.SanctuaryRestInPeace==expected&&!NpcRestInPeace.Marked(target)&&ClassLoot.Killer(target)==p
                &&corpse.SanctuaryNoRewards&&NpcEquipmentRandom.State(source)==seed
                &&NecromancerEffects.ManaBalance256(p)==ownerMana&&SkillHealth.Balance256(p)==ownerLife,"Actual RIP kill lost direct credit,corpse flag,reward ordering,resources or RNG");
            corpse.BeginDecay(FixtureSpace.Retention);return corpse;
        }
        finally{SourceBlocking.BlockRoll=oldBlock;SourceBlocking.ChoiceRoll=oldChoice;}
    }
    private static void RejectConsumers(PlayerMobile p,Corpse corpse)
    {
        var row=CorpseSkillRules.Profiles.Find(corpse);var loot=corpse.Items.Select(i=>i.Serial.Value).Order().ToArray();int mana=NecromancerEffects.ManaBalance256(p);
        Require(row is{Owned:false,Selectable:true,NoSelect:false}&&!corpse.SanctuaryConsumed&&!corpse.SanctuaryHarvested&&!corpse.Animated,"RIP changed corpse ownership,selection metadata or consumption");
        Require(!CorpseSkillRules.Usable(p,corpse,SkeletonPets.Range)&&!SkeletonPets.Usable(p,corpse)
            &&!CorpseSkillRules.DireWolfUsableAt(p,corpse,p,SkeletonPets.Range)
            &&!CorpseCastTargets.Usable(p,CombatAbility.CorpseExplosion,corpse)&&!CorpseCastTargets.Usable(p,CombatAbility.PoisonExplosion,corpse)
            &&!ReviveCasts.Plan(p,corpse,out _,out _,out _),"A shared corpse consumer accepted RIP");
        Require(loot.SequenceEqual(corpse.Items.Select(i=>i.Serial.Value).Order())&&CorpseSkillRules.Profiles.Find(corpse)==row
            &&NecromancerEffects.ManaBalance256(p)==mana,"RIP refusal altered loot,row or mana");
        // Clear only the fixture's flag briefly to prove this same corpse has
        // usable underlying metadata,access and geometry rather than a false positive.
        corpse.SanctuaryRestInPeace=false;
        try{Require(CorpseSkillRules.Usable(p,corpse,SkeletonPets.Range)&&SkeletonPets.Usable(p,corpse),"RIP control corpse is unusable independently of its RIP flag");}
        finally{corpse.SanctuaryRestInPeace=true;}
    }
    private static Held Hold(PlayerMobile p,BaseCreature source,Item item)
    {
        var alive=Target(p,source,20000*256);try
        {
            var packet=new PreparedMonsterDamagePacket(256,256,0,0,1);var seed=NpcEquipmentRandom.State(source);
            Require(NpcCombatEvents.ForSource(source).Dispatch("domeleedamage",packet,source,alive,TimedEffects.Now)==packet&&!NpcRestInPeace.Marked(alive)
                &&NpcEquipmentRandom.State(source)==seed,"RIP fired on a damage event instead of a kill");
        }
        finally{alive.Delete();}
        var corpse=Kill(p,source,true);var loot=new Gold(17);corpse.DropItem(loot);RejectConsumers(p,corpse);
        var pending=Target(p,source,1237);pending.NoKillAwards=true;
        Require(NpcCombatEvents.ForSource(source).Dispatch("kill",null,source,pending,TimedEffects.Now)==null&&NpcRestInPeace.Marked(pending),"Packet-independent pending RIP state missing");
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);TimedEffects.PreventHealing(source,TimedEffects.Now);
        var saved=new Held(p.Serial.Value,source.Serial.Value,item.Serial.Value,Json(ClassGear.State(item)),Json(NpcItemStatCapture.Capture(item)),SkillHealth.Balance256(source),NecromancerEffects.ManaBalance256(source),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),NpcEquipmentRandom.State(source).Seed,corpse.Serial.Value,loot.Serial.Value,pending.Serial.Value,SkillHealth.Balance256(pending));
        PersistentMinions.Suspend(p);p.Internalize();pending.Internalize();corpse.Internalize();return saved;
    }
    private static void ControlOwner()
    {
        var p=Owner();var own=Original();p.AddItem(own);Require(ClassGear.Bonus(p,"rip")==1,"Owner RIP gear control absent");
        var bare=new Broadsword();ClassGear.Profiles.Ensure(bare);var source=NpcFreezeHitChecks.Cast(p,bare,"rip",0);source.SuspendActions();
        var corpse=Kill(p,source,false);Require(CorpseSkillRules.Usable(p,corpse,SkeletonPets.Range),"Owner-only RIP forwarded to a pet kill or control corpse unusable");
        corpse.Delete();source.Delete();own.Delete();p.Delete();
    }
    private static void Check(NetState net,bool reload)
    {
        SourceCases();
        if(!reload)
        {
            ControlOwner();var rows=new List<Held>();
            foreach(bool graph in new[]{false,true})
            {
                var p=Owner();var item=Original();var source=NpcFreezeHitChecks.Cast(p,item,"rip");source.SuspendActions();
                if(graph){NpcItemProcs.Create(source);NpcItemProcs.Refresh(source,NpcCombatEvents.LegacyForSource(source));Require(NpcItemProcs.Get(source).Capture().Events.Entries.Count(e=>e.Function==29)==1,"RIP graph kill subscription missing");}
                rows.Add(Hold(p,source,item));
            }
            var q=Owner();FixtureNetwork.Authenticate(net,q,"npc-rip-");var revived=NpcFreezeHitChecks.Revive(q,out var gear,"Tyrael's Might","rip");
            rows.Add(Hold(q,revived,gear));File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"npc-rip-reload-");
            foreach(var row in saved.Actors)
            {
                var source=(BaseCreature)World.FindMobile((Serial)row.Source);var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var item=World.FindItem((Serial)row.Item);var corpse=World.FindItem((Serial)row.Corpse)as Corpse;var pending=World.FindMobile((Serial)row.Pending)as BaseCreature;
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&SkillHealth.Balance256(source)==row.Life&&NecromancerEffects.ManaBalance256(source)==row.Mana
                    &&SkillHealth.Balance256(p)==row.OwnerLife&&NecromancerEffects.ManaBalance256(p)==row.OwnerMana&&NpcEquipmentRandom.State(source).Seed==row.Seed
                    &&Json(ClassGear.State(item))==row.Gear&&Json(NpcItemStatCapture.Capture(item))==row.Numeric
                    &&corpse is{Deleted:false,SanctuaryRestInPeace:true,SanctuaryConsumed:false}&&World.FindItem((Serial)row.Loot)?.Parent==corpse
                    &&pending is{Alive:true,Deleted:false}&&SkillHealth.Balance256(pending)==row.PendingLife&&NpcRestInPeace.Marked(pending),"RIP original actor/item/wounds/resources/RNG,loot,corpse flag or pending state changed on reload");
                // A restored event registry still executes on a fresh original unit.
                var after=new Skeleton();try{NpcCombatEvents.ForSource(source).Dispatch("kill",null,source,after,TimedEffects.Now);Require(NpcRestInPeace.Marked(after),"Restored RIP subscription did not execute");}finally{after.Delete();}
                if(saved.Generation==1)
                {
                    pending.MoveToWorld(new(6103,2802,0),Map.Trammel);pending.LastKiller=source;pending.Kill();
                    Require(pending.Corpse is Corpse{SanctuaryRestInPeace:true}&&!NpcRestInPeace.Marked(pending),"Saved pre-death RIP state was lost during eventual native corpse creation");pending.Corpse.Delete();
                }
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("NPC Rest in Peace:128source controls;legacy/graph original Tyrael Iron/frozen Revive kill,reward/corpse transfer,no owner forwarding,shared corpse refusals,loot and original wounds/resources/RNG;pending state and saved corpse flags survive two reloads;modern callback parity unverified");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"npc-rest-in-peace-check","npc-rest-in-peace-reload-check"})ConsoleInputHandler.RegisterCommand(name,"NPC RIP through original kill and corpse paths",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
