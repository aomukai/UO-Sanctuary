// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class NpcIronMaidenChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcIronMaidenChecks));
    private sealed record Row(uint Source,int Life,ulong Seed,NecromancerCurse Curse);
    private sealed record Saved(uint Owner,uint Revive,uint Golem,uint Material,string Binding,Row[] Rows,int Generation=0);
    private sealed record Scene(PlayerMobile Owner,BaseCreature Revive,IronGolemBody Golem,Item Material);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-iron-maiden-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("npc-iron-maiden-check",Check);Register("npc-iron-maiden-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("npc-iron-maiden-live","Cursed original native A1",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("npc-iron-maiden-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Shared Iron Maiden on original summon contact",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Curse(Mobile actor,PlayerMobile owner,CombatAbility ability,int rank=3)
        =>NecromancerEffects.Profiles.Store(actor,new(Curse:new(ability,owner.Serial.Value,rank,TimedEffects.Now.Add(FixtureSpace.Retention),owner.X,owner.Y)));
    private static Scene Create(int row)
    {
        var owner=ReviveEquippedChecks.Owner(row);MagePetBuffChecks.Train(owner,"necromancer.irongolem",1);
        var loot=ReviveEquippedChecks.Named("The Diggler");var corpse=ReviveEquippedChecks.Die(owner,new Skeleton(),[loot]);var revive=ReviveOriginals.Find(corpse);
        NpcPreventHealChecks.Start(owner,CombatAbility.Revive,corpse);Require(revive.Alive&&loot.Parent==corpse,"Cursed Revive lost original/loot");loot.Delete();
        var material=ReviveEquippedChecks.Named("The Diggler");material.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        NpcPreventHealChecks.Start(owner,CombatAbility.IronGolem,material);var golem=NecromancerGolems.For(owner).OfType<IronGolemBody>().Single();
        foreach(var source in new BaseCreature[]{revive,golem})
        {source.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);source.Blessed=false;source.AIObject.AITimer.Stop();NpcEquipmentRandom.Set(source,1337);}
        return new(owner,revive,golem,material);
    }
    private static IronGolemOrderedContactChecks.Recipient Target(PlayerMobile owner,BaseCreature source)
    {
        var target=new IronGolemOrderedContactChecks.Recipient{Source=source,NoKillAwards=true,Frozen=true};target.SetHits(20000);
        foreach(var resistance in Enum.GetValues<ResistanceType>()){target.SetResistance(resistance,0);}
        target.MoveToWorld(new(source.X+1,source.Y,source.Z),owner.Map);target.AIObject.AITimer.Stop();TimedEffects.PreventHealing(target,TimedEffects.Now);return target;
    }
    private static void Clean(BaseCreature target){NecromancerEffects.Profiles.Clear(target);TimedEffects.Clear(target);target.Corpse?.Delete();target.Delete();}
    private static string Incoming(BaseCreature source)
    {
        var entry=source.FindMostRecentDamageEntry(true);
        return entry==null?"none":entry.Damager?.Serial.Value+":"+entry.DamageGiven+":"+entry.LastDamage.ToString("O");
    }
    private static int Reflected(BaseCreature source,Mobile target,int physical)
        =>checked((int)Math.Floor(SkillCombat.MitigatePrepared(target,source,new(IronMaiden.Returned256(physical,250)/256.0,0,0,0,0)).Physical*256));
    private static IronGolemContact.Prepared Prepare(PlayerMobile owner,BaseCreature source,BaseCreature target,int flags=1)
    {
        int life=SkillHealth.Balance256(source);var seed=NpcEquipmentRandom.State(source);var rules=IronGolemContact.Require(source,owner,target);
        Require(SkillHealth.Balance256(source)==life&&NpcEquipmentRandom.State(source)==seed,"Curse preflight mutated source/RNG");
        var impact=source is SkeletonGuardian guardian?NpcEquipmentImpact.Prepare(guardian,target,flags,n=>n-1,()=>uint.MaxValue):ReviveEquipmentImpact.Prepare(source,target,flags,n=>n-1,()=>uint.MaxValue);
        return new(rules,impact);
    }
    private static void Exercise(PlayerMobile owner,BaseCreature source)
    {
        foreach(string mode in new[]{"ordinary","fractional-tap","suppressed","miss","block","no-fill","cancel","removed","late"})
        {
            var target=Target(owner,source);
            // Keep these arithmetic/live-curse cases nonlethal without
            // altering the original summon. Separate real packets test death.
            target.SetResistance(ResistanceType.Physical,90);
            try
            {
                NecromancerEffects.Profiles.Clear(source);if(mode!="late"){Curse(source,owner,CombatAbility.IronMaiden);}
                int initial=SkillHealth.Maximum256(source)*3/4+17;SkillHealth.SetBalance256(source,initial);int targetLife=SkillHealth.Balance256(target);
                int flags=mode switch{"suppressed"=>33,"miss"=>0,"block"=>16,"no-fill"=>129,_=>1};var prepared=Prepare(owner,source,target,flags);
                if(mode=="fractional-tap")
                {
                    // An explicitly supplied fractional packet isolates event
                    // arithmetic; ordinary cases retain actual weapon rolls.
                    prepared=new(prepared.Rules,prepared.Impact with{Packet=new(257,257,0,0,1)});Curse(target,owner,CombatAbility.LifeTap);
                }
                int reflected=Reflected(source,target,prepared.Impact.Packet.Physical256);
                if(mode=="removed"){NecromancerEffects.Profiles.Clear(source);}
                if(mode=="late"){Curse(source,owner,CombatAbility.IronMaiden);}
                if(mode=="cancel")
                {prepared.Cancel();Require(SkillHealth.Balance256(source)==initial&&SkillHealth.Balance256(target)==targetLife,"Canceled packet dispatched curse");continue;}
                Log.Information("NPC curse contact: {Source} mode={Mode} initial={Life} physical={Physical} reflected={Reflected}",source.GetType().Name,mode,initial,prepared.Impact.Packet.Physical256,reflected);
                var result=IronGolemContact.Release(source,owner,target,prepared,_=>true,TimedEffects.Now);
                bool reflectedNow=mode is "ordinary" or "fractional-tap" or "late";
                int healing=mode=="fractional-tap"?NecromancerEffects.LifeTapHealing256(257,50):0;
                Require(SkillHealth.Balance256(source)==initial-(reflectedNow?reflected:0)+healing,"Iron Maiden missing,duplicated,quartered,stale or after Life Tap: "+mode);
                Require((result.Commit!=null)==((flags&1)!=0)&&target.Notifications==(NpcEquipmentImpact.Fills(flags)?1:0),"Curse changed ordinary contact qualification: "+mode);
                bool replay=false;try{IronGolemContact.Release(source,owner,target,prepared,_=>true,TimedEffects.Now);}catch(InvalidOperationException){replay=true;}
                Require(replay,"Cursed contact replayed");
            }
            finally{Clean(target);NecromancerEffects.Profiles.Clear(source);}
        }
    }
    private static void Death(bool iron,bool beforeRelease=false)
    {
        var scene=Create(3180+(iron?10:0));var source=iron?scene.Golem:scene.Revive;var target=Target(scene.Owner,source);
        try
        {
            Curse(source,scene.Owner,CombatAbility.IronMaiden);Curse(target,scene.Owner,CombatAbility.LifeTap);
            SkillHealth.SetBalance256(source,256);SkillHealth.SetBalance256(target,256);var prepared=Prepare(scene.Owner,source,target);
            Require(prepared.Impact.Packet.Physical256>256,"Mutual death needs a lethal actual weapon packet");
            if(beforeRelease)
            {
                source.Kill();var skipped=IronGolemContact.Release(source,scene.Owner,target,prepared,_=>true,TimedEffects.Now);
                Require(skipped.Exit==MonsterContactExit.DeathModeRetained&&skipped.Commit==null&&SkillHealth.Balance256(target)==256&&target.Notifications==0,
                    "Source death before release bypassed the existing no-contact branch");return;
            }
            var result=IronGolemContact.Release(source,scene.Owner,target,prepared,_=>true,TimedEffects.Now);
            Require(source is not{Alive:true,Deleted:false}&&target is not{Alive:true,Deleted:false}&&result.Commit is{AfterHealth256:0}
                &&target.SourceDeletedAtDeath==source.Deleted&&target.DeathCalls==1,"Reflected source death canceled original lethal hit or Life Tap revived it");
            Require(target.LastKiller==source&&ClassLoot.Killer(target)==scene.Owner&&target.FindDamageEntryFor(source)?.Responsible?.Any(e=>e.Damager==scene.Owner&&e.DamageGiven==1)==true,
                "Cursed original lost responsible-owner damage credit after retirement");
            if(iron){Require(scene.Material.Deleted,"Reflected Iron death left consumed material");}
        }
        finally{Clean(target);source.Corpse?.Delete();scene.Revive.Delete();scene.Golem.Delete();scene.Owner.Delete();}
    }
    private static Row Retain(BaseCreature source,PlayerMobile owner)
    {Curse(source,owner,CombatAbility.IronMaiden);SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)*2/3+17);return new(source.Serial.Value,SkillHealth.Balance256(source),NpcEquipmentRandom.State(source).Seed,NecromancerEffects.Curse(source));}
    private static void Check()
    {
        var scene=Create(3210);Exercise(scene.Owner,scene.Revive);Exercise(scene.Owner,scene.Golem);Death(false);Death(true);Death(false,true);Death(true,true);
        var rows=new[]{Retain(scene.Revive,scene.Owner),Retain(scene.Golem,scene.Owner)};
        var saved=new Saved(scene.Owner.Serial.Value,scene.Revive.Serial.Value,scene.Golem.Serial.Value,scene.Material.Serial.Value,ReviveEquipmentLife.Identity(ReviveNativeActivation.State(scene.Revive)),rows);
        PersistentMinions.Suspend(scene.Owner);scene.Owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(World.FindItem((Serial)saved.Material)?.Parent==World.FindMobile((Serial)saved.Golem)
            &&ReviveEquipmentLife.Identity(ReviveNativeActivation.State((BaseCreature)World.FindMobile((Serial)saved.Revive)))==saved.Binding,"Cursed original material/binding changed");
        foreach(var row in saved.Rows)
        {
            var source=(BaseCreature)World.FindMobile((Serial)row.Source);
            Require(source.Map==Map.Internal&&source.ControlMaster==owner&&SkillHealth.Balance256(source)==row.Life&&NpcEquipmentRandom.State(source).Seed==row.Seed
                &&NecromancerEffects.Curse(source)==row.Curse,"Saved curse,source,wounds or RNG changed");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void Live(NetState net)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var source=(BaseCreature)World.FindMobile((Serial)saved.Revive);var golem=(IronGolemBody)World.FindMobile((Serial)saved.Golem);bool frozen=golem.Frozen;source.Frozen=golem.Frozen=true;
        FixtureNetwork.Authenticate(net,owner,"iron-maiden-live-");owner.MoveToWorld(new(7100,3210,0),Map.Trammel);PersistentMinions.Resume(owner);
        golem.Frozen=true;source.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();source.Combatant=null;source.FocusMob=null;
        // This observer isolates reflection from normal source regeneration.
        // Ordinary recovery remains covered by the retained runtime fixtures.
        TimedEffects.PreventHealing(source,TimedEffects.Now);
        var target=Target(owner,source);target.SetResistance(ResistanceType.Physical,90);SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)*3/4+17);int life=SkillHealth.Balance256(source),hp=SkillHealth.Balance256(target),expected=-1,damage=-1;
        NpcEquipmentRandom.Set(source,ReviveNativeSwingChecks.HitSeed());ReviveSwings.Cancel(source,false);source.Frozen=false;source.IssueOrder(OrderType.Guard,null);source.AIObject.AITimer.Start();var deadline=TimedEffects.Now.AddSeconds(20);
        void Finish(){source.AIObject.AITimer.Stop();source.Combatant=null;source.FocusMob=null;Clean(target);TimedEffects.Clear(source);golem.Frozen=frozen;PersistentMinions.Suspend(owner);owner.Internalize();}
        void Inspect()
        {
            try
            {
                Require(net.Running&&TimedEffects.Now<deadline,"Cursed original A1 timeout");var pending=ReviveSwings.State(source)?.Pending;
                if(target.Notifications>0)
                {
                    Require(expected>0&&target.Notifications==1&&hp-SkillHealth.Balance256(target)==damage&&SkillHealth.Balance256(source)==life-expected,"Actual A1 did not damage and reflect exactly once at contact");Finish();
                    File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Rows=saved.Rows.Select(r=>r with{Life=SkillHealth.Balance256(World.FindMobile((Serial)r.Source)),Seed=NpcEquipmentRandom.State((BaseCreature)World.FindMobile((Serial)r.Source)).Seed}).ToArray()}));
                    Log.Information("npc-iron-maiden-live-result: passed");return;
                }
                if(pending!=null)
                {
                    Require(pending.Impact.Target==target.Serial.Value&&SkillHealth.Balance256(source)==life&&SkillHealth.Balance256(target)==hp,
                        "Allocation identity/life changed: source="+source.Serial.Value+" life="+SkillHealth.Balance256(source)+"/"+life+" max="+SkillHealth.Maximum256(source)
                        +", target="+target.Serial.Value+" life="+SkillHealth.Balance256(target)+"/"+hp+", notifications="+target.Notifications
                        +", pending="+JsonSerializer.Serialize(pending)+", incoming="+Incoming(source));
                    expected=Reflected(source,target,pending.Impact.Packet.Physical256);damage=pending.Impact.Packet.Total256;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){Finish();Log.Error(e,"npc-iron-maiden-live-result: failed");}
        }
        Log.Information("npc-iron-maiden-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
    }
}
