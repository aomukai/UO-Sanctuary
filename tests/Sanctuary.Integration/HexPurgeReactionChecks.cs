// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class HexPurgeReactionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(HexPurgeReactionChecks));
    private sealed record Saved(int Generation,uint Owner,uint Weapon,uint Target,int X,int Y,int Life256,HexPurgeTarget Mark,HexPurgeBuff Buff);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"purge-reaction-fixtures.json");
    private static readonly FirestormModifiers Zero=new(0,0,0,0,0);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Purge charge/explosion rule",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        Register("purge-reaction-check",false);Register("purge-reaction-reload-check",true);
    }
    private static SkillValues Values(int rank,int bane=0,int blast=0,int sigil=0)=>new(SkillMechanics.For(CombatAbility.HexPurge),k=>k=="Hex Purge"?rank:0,k=>k=="Hex Bane"?bane:k=="Eldritch Blast"?blast:k=="Sigil Death"?sigil:0,99);
    private static ulong Seed(int roll)
    {for(int i=0;i<10000;i++){ulong s=SourceRandom.InitLow(i),next=s;if(SourceRandom.Limited(ref next,100)==roll){return s;}}throw new InvalidOperationException("Purge fixture chance seed");}
    private static HexPurgeBuff Registration(PlayerMobile p,int rank)
    {
        var b=new HexPurgeBuff(p.Serial.Value,rank,TimedEffects.Now.Add(FixtureSpace.Retention),HexPurge.Capture(Values(rank)));
        var s=HexPurge.Snapshot();HexPurge.Restore(s with{Buffs=s.Buffs.Where(x=>x.Owner!=p.Serial.Value).Append(b).ToArray()});return b;
    }
    private static PlayerMobile Fresh(bool female,PlayerMobile near=null)
    {
        var p=new PlayerMobile{Player=true,Female=female,Body=female?0x191:0x190,Name="PurgeReactionFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.hexpurge");while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Purge fixture Strength");}
        if(near==null){FixtureSpace.Corridor(p,6560,32,3500);}else{FixtureSpace.Place(p,new(near.X+20,near.Y,near.Z),near.Map);}
        var w=new Broadsword();p.AddToBackpack(w);Require(p.EquipItem(w),"Purge fixture sword");ClassGear.Refresh(p);Registration(p,20);return p;
    }
    private static ShockWaveTarget Enemy(PlayerMobile owner,int offset,int dy=0)
    {
        var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(owner.X+offset,owner.Y+dy,owner.Z),owner.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,t.HitsMax*256);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}
        Require(NamedElements.MagicResistance(t)==0,"Purge fixture magic resistance");return t;
    }
    private static HexPurgeTarget Attach(PlayerMobile p,BaseCreature t,int roll=0,int mastery=0,FirestormModifiers mods=null,int rank=20)
    {
        HexPurge.ClearDebuff(t);var b=Registration(p,rank);var plan=HexPurge.Plan(Values(rank));
        Require(HexPurgeReactionProposal.AttachPrepared(p,t,b,plan,HexPurgeReactionProposal.Capture(plan,mastery,mods??Zero,Seed(roll)),true,TimedEffects.Now),"Purge prepared attachment");return HexPurge.Debuff(t);
    }
    private static void Model()
    {
        foreach(int rank in new[]{1,9,10,19,20,40,255})foreach(int mastery in new[]{0,50})
        {
            var plan=HexPurge.Plan(Values(rank,20,20,20));var r=HexPurgeReactionProposal.Capture(plan,mastery,Zero,Seed(0));
            Require(r.RemainingCharges==Math.Min(1+rank/10,3)&&HexPurgeReactionProposal.Enhanced(plan.Minimum256,mastery)==plan.Minimum256+(long)plan.Minimum256*mastery/100,"Purge capture rank/mastery boundary");
        }
        Require(!HexPurgeReactionProposal.Approved&&!WarlockCombat.Implemented(CombatAbility.HexPurge),"Prepared Purge enabled learned input");
    }
    private static void Exercise(PlayerMobile owner)
    {
        var targets=new[]{Enemy(owner,8),Enemy(owner,10),Enemy(owner,11),Enemy(owner,10,1),Enemy(owner,8,2),Enemy(owner,16),Enemy(owner,8,1)};
        var t=targets[0];var near=targets[1];var attacker=targets[5];var witch=Fresh(true,owner);
        var walls=new List<Item>();HexPurgeBurst last=default;int notifications=0;bool actions=true;
        void Contact(SkillDamageContact c){notifications++;last=HexPurgeReactionProposal.OnDamagePrepared(c,TimedEffects.Now,actions);}
        SkillDamageEvents.BeforeDirectDamage+=Contact;
        try
        {
            // Inclusive two-tile circle; diagonal exterior, height and ally
            // exclusion. The independent NPC is outside the burst radius.
            targets[4].Controlled=true;targets[4].ControlMaster=owner;
            targets[6].MoveToWorld(new(targets[6].X,targets[6].Y,owner.Z+16),owner.Map);
            ClassSkillChecks.Train(owner,"warlock.hexbane");NecromancerEffects.SetMana256(owner,owner.ManaMax*256);
            Require(HexBane.CastPrepared(owner,replaceGroup:true)&&HexBane.ApplyPrepared(owner,t,HexBane.Buff(owner),true,TimedEffects.Now,replaceGroup:true),"Purge other-Hex recipient setup");
            HexBane.ClearBuff(owner);var priorHex=HexBane.Debuff(t);var registration=Registration(owner,20);var initialPlan=HexPurge.Plan(Values(20));
            Require(!HexPurgeReactionProposal.AttachPrepared(owner,t,registration,initialPlan,HexPurgeReactionProposal.Capture(initialPlan,0,Zero,Seed(0)),false,TimedEffects.Now)&&HexBane.Debuff(t)==priorHex,"Failed Purge attachment removed another Hex");
            var mark=Attach(owner,t);var hp=targets.Select(SkillHealth.Balance256).ToArray();
            Require(HexBane.Debuff(t)==null&&HexPurge.Debuff(t)==mark,"Purge attachment did not replace recipient Hex group");
            AOS.Damage(t,attacker,1,false,100,0,0,0,0);
            Require(last.Triggered&&last.Recipients==2&&last.RemainingCharges==2&&notifications==1,"Purge NPC direct event/circle or recursive burst");
            Require(hp[0]-SkillHealth.Balance256(t)==last.Magic256+256&&hp[1]-SkillHealth.Balance256(near)==last.Magic256
                &&targets.Skip(2).Select(SkillHealth.Balance256).SequenceEqual(hp.Skip(2)),"Purge shared packet/radius/ally exclusion");
            var spent=HexPurge.Debuff(t);var newPlan=HexPurge.Plan(Values(20,20,20,20));
            Require(HexPurgeReactionProposal.AttachPrepared(owner,t,HexPurge.Buff(owner),newPlan,HexPurgeReactionProposal.Capture(newPlan,50,new(100,50,50,50,50),Seed(99)),true,TimedEffects.Now.AddMilliseconds(100)),"Purge same-owner refresh refused");
            var refreshed=HexPurge.Debuff(t);Require(refreshed.Plan==spent.Plan&&refreshed.Reaction==spent.Reaction&&refreshed.UntilUtc>spent.UntilUtc,"Purge refresh refilled charges or rewrote snapshot");
            var other=HexPurge.Buff(witch);var otherPlan=HexPurge.Plan(Values(20));
            Require(!HexPurgeReactionProposal.AttachPrepared(witch,t,other,otherPlan,HexPurgeReactionProposal.Capture(otherPlan,0,Zero,Seed(0)),true,TimedEffects.Now)&&HexPurge.Debuff(t)==refreshed,"Equal-rank different caster stole mark");
            Registration(witch,19);otherPlan=HexPurge.Plan(Values(19));Require(!HexPurgeReactionProposal.AttachPrepared(witch,t,HexPurge.Buff(witch),otherPlan,HexPurgeReactionProposal.Capture(otherPlan,0,Zero,Seed(0)),true,TimedEffects.Now),"Weaker Purge replaced mark");
            Registration(witch,21);otherPlan=HexPurge.Plan(Values(21));Require(HexPurgeReactionProposal.AttachPrepared(witch,t,HexPurge.Buff(witch),otherPlan,HexPurgeReactionProposal.Capture(otherPlan,0,Zero,Seed(0)),true,TimedEffects.Now)&&HexPurge.Debuff(t)is{Creator:var creator,Reaction.RemainingCharges:3}&&creator==witch.Serial.Value,"Stronger Purge did not replace/reset");

            mark=Attach(owner,t,78);ulong expectedSeed=mark.Reaction.Seed;Require(SourceRandom.Limited(ref expectedSeed,100)==78,"Chance boundary fixture");
            int before=SkillHealth.Balance256(t);SkillHealth.Damage(attacker,t,129);
            Require(!last.Triggered&&HexPurge.Debuff(t).Reaction.RemainingCharges==3&&HexPurge.Debuff(t).Reaction.Seed==expectedSeed&&before-SkillHealth.Balance256(t)==129,"Failed chance consumed charge,extra draw or fractional base damage");
            Attach(owner,t,77);SkillHealth.Damage(attacker,t,1);Require(last.Triggered&&last.RemainingCharges==2,"Chance boundary 77/78 wrong");
            Attach(owner,t,99,rank:255);SkillHealth.Damage(attacker,t,1);Require(last.Triggered,"Raw chance above100 not capped");

            // Exclusions do real damage but do not alter the registered mark.
            mark=Attach(owner,t);notifications=0;before=SkillHealth.Balance256(t);
            SkillHealth.Damage(attacker,t,256,SkillDamageKind.Periodic);SkillHealth.Damage(attacker,t,256,SkillDamageKind.Return);SkillHealth.Damage(attacker,t,256,SkillDamageKind.SelfCost);
            using(CombatItemEvents.Suppress(attacker,t)){SkillHealth.Damage(attacker,t,256);}
            SkillHealth.Damage(attacker,t,0);t.SetResistance(ResistanceType.Fire,100);AOS.Damage(t,attacker,10,false,0,100,0,0,0);t.SetResistance(ResistanceType.Fire,0);
            Require(notifications==0&&HexPurge.Debuff(t)==mark&&before-SkillHealth.Balance256(t)==4*256,"Purge periodic/return/selfcost/suppressed/zero/immune exclusion");
            actions=false;AOS.Damage(t,attacker,1,false,100,0,0,0,0);Require(!last.Triggered&&HexPurge.Debuff(t)==mark,"Offline mark triggered");actions=true;
            var position=owner.Location;owner.MoveToWorld(position,Map.Felucca);AOS.Damage(t,attacker,1,false,100,0,0,0,0);Require(!last.Triggered&&HexPurge.Debuff(t)==mark,"Cross-facet mark triggered");owner.MoveToWorld(position,Map.Trammel);
            HexPurge.ClearBuff(owner); // Recipient group is independent of the owner's weapon buff.
            before=SkillHealth.Balance256(t);AOS.Damage(t,attacker,1,true,100,0,0,0,0);
            Require(last.Triggered&&before-SkillHealth.Balance256(t)==last.Magic256+256,"Armor-ignore native contact bypassed Purge/fixed-point health");

            // Two independently marked neighbors must not trigger one another.
            Attach(owner,t);var adjacent=Attach(owner,near);notifications=0;
            AOS.Damage(t,attacker,1,false,100,0,0,0,0);
            Require(notifications==1&&last.Triggered&&HexPurge.Debuff(near)==adjacent,"Purge explosion chained another mark");HexPurge.ClearDebuff(near);
            // One charge can be consumed by the application hit, then a fresh
            // later attachment is needed. Depletion occurs before HP callbacks.
            Attach(owner,t,rank:1);AOS.Damage(t,attacker,1,false,100,0,0,0,0);
            Require(last.Triggered&&last.RemainingCharges==0&&HexPurge.Debuff(t)==null,"Purge depletion left spent mark");
            before=SkillHealth.Balance256(t);SkillHealth.Damage(owner,t,129);Require(!last.Triggered&&before-SkillHealth.Balance256(t)==129,"Exhausted Purge repeated without attachment");

            // Captured mastery/potion packet,mitigated independently per element.
            var mods=new FirestormModifiers(100,50,0,0,0);Attach(owner,t,mastery:50,mods:mods);before=SkillHealth.Balance256(t);
            SkillHealth.Damage(owner,t,1);int total=last.Magic256+(last.Magic256*3/2)+last.Magic256*3;
            Require(last.Triggered&&before-SkillHealth.Balance256(t)==total+1,"Purge captured magic/potion arithmetic or double mastery");

            // Wall LOS is centered on the marked victim,not on the Warlock.
            for(int dy=-3;dy<=3;dy++){var wall=new Static(0x80);wall.MoveToWorld(new(t.X+1,t.Y+dy,t.Z),t.Map);walls.Add(wall);}
            Attach(owner,t);before=SkillHealth.Balance256(near);SkillHealth.Damage(owner,t,1);
            Require(last.Triggered&&last.Recipients==1&&SkillHealth.Balance256(near)==before,"Purge burst ignored center LOS");foreach(var wall in walls){wall.Delete();}walls.Clear();

            // Unsupported receivers are reported before spending the charge or
            // changing seed/HP. The ordinary triggering hit remains independent.
            mark=Attach(owner,t);t.IsParagon=true;before=SkillHealth.Balance256(t);
            var refused=HexPurgeReactionProposal.OnDamagePrepared(new(attacker,t,1),TimedEffects.Now,true);
            Require(!refused.Triggered&&refused.Fault!=null&&HexPurge.Debuff(t)==mark&&SkillHealth.Balance256(t)==before,"Unsupported receiver silently spent Purge state");t.IsParagon=false;
            var expired=mark with{UntilUtc=TimedEffects.Now.AddTicks(-1)};HexPurge.StoreReactionPrepared(expired);SkillHealth.Damage(owner,t,1);Require(!last.Triggered,"Expired Purge triggered");
            mark=Attach(owner,t);mark=mark with{Creator=witch.Serial.Value};HexPurge.StoreReactionPrepared(mark);witch.Kill();
            Require(!HexPurgeReactionProposal.OnDamagePrepared(new(attacker,t,1),TimedEffects.Now,true).Triggered&&HexPurge.Debuff(t)==mark,"Dead owner triggered or reset mark");

            // A lethal original hit must still explode around its living
            // center;secondary damage may finish it before the original commit.
            Attach(owner,t);SkillHealth.SetBalance256(t,256);before=SkillHealth.Balance256(near);SkillHealth.Damage(owner,t,512);
            Require(last.Triggered&&t is not{Alive:true,Deleted:false}&&SkillHealth.Balance256(near)<before,"Lethal incoming hit lost Purge explosion");
            Log.Information("Purge candidate:chance77/78,cap100,charge claim/deplete/refresh/rank competition,single burst packet/circle/LOS/ally,FP256/nativeNPC/ignorearmor,exclusions/ownership/expiry,mastery+potion,no chain recursion and lethal center pass.");
        }
        finally
        {
            SkillDamageEvents.BeforeDirectDamage-=Contact;foreach(var wall in walls){wall.Delete();}
            foreach(var target in targets){target.Delete();SkillHealth.Clear(target);}witch.Delete();HexPurge.Prune();
        }
    }
    private static void Check(bool reload)
    {
        Model();PlayerMobile owner;ShockWaveTarget target;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;
            owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;target=World.FindMobile((Serial)saved.Target)as ShockWaveTarget;
            Require(owner!=null&&target!=null&&CombatTiming.Equipped(owner)?.Serial.Value==saved.Weapon&&SkillHealth.Balance256(target)==saved.Life256&&HexPurge.Debuff(target)==saved.Mark&&HexPurge.Buff(owner)==saved.Buff,"Original Purge reaction/snapshot/seed/charges/wounds lost");
            FixtureSpace.Place(owner,new(saved.X,saved.Y,0),Map.Trammel);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
            ulong nextSeed=saved.Mark.Reaction.Seed;bool expected=SourceRandom.Limited(ref nextSeed,100)<Math.Clamp(saved.Mark.Plan.ChanceRaw,0,100);
            if(expected){_=SourceSpellPacketRoll.Range(ref nextSeed,HexPurgeReactionProposal.Enhanced(saved.Mark.Plan.Minimum256,saved.Mark.Reaction.MagicMastery),HexPurgeReactionProposal.Enhanced(saved.Mark.Plan.Maximum256,saved.Mark.Reaction.MagicMastery));}
            var result=HexPurgeReactionProposal.OnDamagePrepared(new(owner,target,1),TimedEffects.Now,true);
            Require(result.Fault==null&&result.Triggered==expected&&HexPurge.Debuff(target).Reaction.Seed==nextSeed&&HexPurge.Debuff(target).Reaction.RemainingCharges==saved.Mark.Reaction.RemainingCharges-(expected?1:0),"Saved Purge stream or next charge replayed/reset");
        }
        else{owner=Fresh(false);target=Enemy(owner,1);}
        Exercise(owner);Attach(owner,target,mastery:50,mods:new(25,10,20,30,40));
        var spent=HexPurgeReactionProposal.OnDamagePrepared(new(owner,target,1),TimedEffects.Now,true);
        Require(spent.Triggered&&spent.RemainingCharges==2,"Saved Purge control did not actually spend a charge");var mark=HexPurge.Debuff(target);
        // Hold only the persistence control;natural expiry was checked above.
        mark=mark with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};HexPurge.StoreReactionPrepared(mark);
        SkillHealth.SetBalance256(target,target.HitsMax*256-129);
        var legacy=mark with{Reaction=null};string oldJson=JsonSerializer.Serialize(legacy);Require(!oldJson.Contains("Reaction")&&JsonSerializer.Deserialize<HexPurgeTarget>(oldJson)==legacy,"Old-schema Purge JSON changed");
        var held=HexPurge.Snapshot();string original=JsonSerializer.Serialize(held);
        try{HexPurge.Restore(held with{Targets=held.Targets.Select(x=>x.Target==target.Serial.Value?x with{Reaction=x.Reaction with{RemainingCharges=0}}:x).ToArray()});throw new InvalidOperationException("Malformed saved Purge accepted");}catch(InvalidDataException){}
        Require(JsonSerializer.Serialize(HexPurge.Snapshot())==original,"Failed Purge restore changed live state");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(generation,owner.Serial.Value,CombatTiming.Equipped(owner).Serial.Value,target.Serial.Value,owner.X,owner.Y,SkillHealth.Balance256(target),mark,HexPurge.Buff(owner))));
    }
}
