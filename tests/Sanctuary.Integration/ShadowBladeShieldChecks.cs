// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowBladeShieldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowBladeShieldChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,BladeShieldState Shield,uint Target,int TargetLife);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-blade-shield-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-blade-shield-check",false);Register("shadow-blade-shield-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Blade Shield start/pulses,full NPC fill/contact and persistence",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static ShadowPsychicChecks.Witness Target(AssassinShadowBody b,int dx=2)
    {
        var t=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};t.SetHits(65000);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}
        t.MoveToWorld(new(b.X+dx,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static ulong HitSeed(SkillValues values)
    {
        for(int i=1;i<10000;i++)
        {ulong seed=SourceRandom.InitLow(i),copy=seed;BladeShields.RollSkill(values,n=>(int)SourceRandom.Limited(ref copy,n));if(SourceRandom.Limited(ref copy,100)<5){return seed;}}
        throw new InvalidOperationException("Missing minimum-hit-chance seed");
    }
    private static BladeShieldState Cast(AssassinShadowBody b,DateTime at)
    {
        var rng=NpcEquipmentRandom.Require(b);var values=ShadowSkillCasting.Values(b,b.OwnedSkillRank(277),277);
        Require(ShadowActions.BeginBladeShield(b,at),"Own Shield SC entry refused");var action=ShadowActions.State(b);var started=BladeShields.State(b);
        Require(started is{NextPulseUtc:null}&&started.Owner==b.Serial.Value&&started.Rank==values.Rank&&started.StartedUtc==at&&started.UntilUtc==at.AddMilliseconds(values.Eval("auralencalc")*40)&&NpcEquipmentRandom.Require(b)==rng,"Start28 did not attach its own captured state without pulse/RNG");
        ShadowActions.Tick(at.AddSeconds(1),true,b.Serial.Value);Require(BladeShields.State(b)?.NextPulseUtc!=null&&ShadowActions.State(b)==null,"Shield SC release/end did not activate periodic state");return started;
    }
    private static BaseCreature Exercise(AssassinShadowBody b,PlayerMobile owner)
    {
        var target=Target(b);var outside=Target(b,4);var at=TimedEffects.Now.AddMinutes(5);var phases=new List<int>();var observed=NpcOriginalItemProcs.Observed;
        NpcOriginalItemProcs.Observed=(source,v)=>{if(source==b){phases.Add(v.Phase);}observed?.Invoke(source,v);};
        try
        {
            CombatPotions.Apply(owner,3,at);int mana=NecromancerEffects.ManaBalance256(owner),ownMana=b.EquipmentNumbers.Read(8);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();
            var before=NpcEquipmentRandom.Require(b);int hp=SkillHealth.Balance256(target),farHp=SkillHealth.Balance256(outside);
            Require(ShadowActions.BeginBladeShield(b,at),"Initial Shield entry refused");var action=ShadowActions.State(b);var state=BladeShields.State(b);int castRank=state.Rank;
            b.ChangeSkills(book=>book.Assign(277,10));var live=ShadowSkillCasting.Values(b,b.OwnedSkillRank(277),277);NpcEquipmentRandom.Set(b,HitSeed(live));before=NpcEquipmentRandom.Require(b);
            int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame;
            ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true,b.Serial.Value);
            Require(SkillHealth.Balance256(target)==hp&&target.Notifications==0&&NpcEquipmentRandom.Require(b)==before&&BladeShields.State(b).NextPulseUtc==null,"Shield pulsed before SC marker");
            ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);
            Require(SkillHealth.Balance256(target)<hp&&target.Notifications>=1&&SkillHealth.Balance256(outside)==farHp&&NpcEquipmentRandom.Require(b)!=before,"Initial own NPC hit/fill/HP/radius did not release");
            Require(BladeShields.State(b)is{} active&&active.Rank==castRank&&active.Id==state.Id&&active.UntilUtc==state.UntilUtc&&active.NextPulseUtc==at.AddMilliseconds(release*40+1000),"Initial pulse rewrote cast-rank state or wrong recurring due time");
            Require(phases.All(p=>p==ItemEventDefinitions.Code("domeleeattack")),"Result32 dispatched unexpected source phases: "+Json(phases));
            if(b.Prepared.Capture.Ability==CombatAbility.ShadowMaster){Require(phases.Count>0,"Original on-attack fixture had no observed callback");}
            ShadowActions.Tick(at.AddSeconds(1),true,b.Serial.Value);Require(ShadowActions.State(b)==null,"Shield action did not finish");
            var recurring=ShadowSkillCasting.Values(b,state.Rank,277);var due=BladeShields.State(b).NextPulseUtc.Value;
            NpcEquipmentRandom.Set(b,HitSeed(recurring));before=NpcEquipmentRandom.Require(b);hp=SkillHealth.Balance256(target);BladeShields.Tick(due,true,b.Serial.Value);
            Require(SkillHealth.Balance256(target)<hp&&NpcEquipmentRandom.Require(b)!=before&&BladeShields.State(b).Rank==castRank&&BladeShields.State(b).NextPulseUtc==due.AddSeconds(1),"Captured-rank recurring NPC pulse failed");
            before=NpcEquipmentRandom.Require(b);hp=SkillHealth.Balance256(target);BladeShields.Tick(due,true,b.Serial.Value);
            Require(NpcEquipmentRandom.Require(b)==before&&SkillHealth.Balance256(target)==hp,"Duplicate due-time pulse replayed");
            due=BladeShields.State(b).NextPulseUtc.Value;var until=BladeShields.State(b).UntilUtc;BladeShields.Tick(due,false,b.Serial.Value);
            Require(NpcEquipmentRandom.Require(b)==before&&SkillHealth.Balance256(target)==hp&&BladeShields.State(b).UntilUtc==until,"Offline owner pulsed or reset lifetime");
            var position=b.Location;PersistentMinions.Park(b);due=BladeShields.State(b).NextPulseUtc.Value;BladeShields.Tick(due,true,b.Serial.Value);
            Require(NpcEquipmentRandom.Require(b)==before&&SkillHealth.Balance256(target)==hp&&BladeShields.State(b).UntilUtc==until,"Parked Shield pulsed or reset lifetime");b.MoveToWorld(position,owner.Map);
            b.MoveToWorld(new(1438,1696,0),owner.Map);Require(Server.Spells.SpellHelper.IsTown(b.Location,owner),"Town fixture");Require(ShadowBladeShields.Pulse(b,castRank,due)==0&&NpcEquipmentRandom.Require(b)==before,"Town pulse drew RNG");b.MoveToWorld(position,owner.Map);
            // A lower-rank replacement is installed at start;canceling before
            // its marker leaves the new state without periodic emissions.
            b.ChangeSkills(book=>book.Assign(277,1));Require(ShadowActions.BeginBladeShield(b,at.AddSeconds(10)),"Lower-rank recast refused");var replaced=BladeShields.State(b);
            Require(replaced.Id!=state.Id&&replaced.Rank==b.OwnedSkillRank(277)&&replaced.NextPulseUtc==null,"Start28 did not replace old periodic identity/rank");ShadowActions.Cancel(b);before=NpcEquipmentRandom.Require(b);BladeShields.Tick(at.AddSeconds(11),true,b.Serial.Value);
            Require(BladeShields.State(b)==replaced&&NpcEquipmentRandom.Require(b)==before,"Canceled recast replayed its periodic work");
            b.ChangeSkills(book=>book.Assign(278,3));GeneratedNpcLiveResources.ApplyShadowBuff(b,278,ShadowSkillCasting.Values(b,b.OwnedSkillRank(278),278),at.AddSeconds(12));
            NpcEquipmentRandom.Set(b,HitSeed(ShadowSkillCasting.Values(b,b.OwnedSkillRank(277),277)));Cast(b,at.AddSeconds(12));var poison=TimedEffects.Snapshot().SingleOrDefault(s=>s.Owner==target.Serial.Value&&s.PoisonMs>0);Require(poison!=null,"Shield full fill did not deliver own Venom poison");
            TimedEffects.CleansePoison(target,0,at.AddSeconds(13));var held=BladeShields.State(b);due=held.NextPulseUtc.Value;hp=SkillHealth.Balance256(target);before=NpcEquipmentRandom.Require(b);
            BladeShields.Tick(due.AddSeconds(10),false,b.Serial.Value);
            Require(SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.Require(b)==before&&BladeShields.State(b).NextPulseUtc==due.AddSeconds(11)&&BladeShields.State(b).UntilUtc==held.UntilUtc,"Late offline tick replayed intervals or refreshed expiry");
            var ledger=BladeShields.Snapshot();BladeShields.Prune(held.UntilUtc);
            Require(BladeShields.State(b)==null&&SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.Require(b)==before,"Absolute expiry emitted damage or retained NPC Shield");
            BladeShields.Restore(ledger); // Restore the independent inspection clock before preserving the live fixture.
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&b.EquipmentNumbers.Read(8)==ownMana&&BladeShields.State(owner)==null&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"NPC Shield borrowed owner mana/state or wore original items");
            PersistentMinions.Park(b);return target;
        }
        finally{NpcOriginalItemProcs.Observed=observed;outside.Delete();SorceressSpellMissiles.Cancel(b);ChainLightning.Cancel(b);ShadowActions.Cancel(b);}
    }
    private static void Warrior(PlayerMobile owner)
    {
        FixtureSpace.Corridor(owner,6260,24,3528);var b=AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowWarrior,TimedEffects.Now);BaseCreature target=null;
        try
        {
            b.ChangeSkills(book=>book.Assign(277,10));GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);ShadowGroundMotion.Activate(b,owner,TimedEffects.Now.AddMinutes(10));TimedEffects.PreventHealing(b,TimedEffects.Now);
            Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.BladeShield),"Owner Shield selection");target=Target(b,6);
            ulong chosen=0;
            for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;SourceRandom.Roll(ref copy);if(SourceRandom.Roll(ref copy)%100>35){continue;}if(SourceRandom.Roll(ref copy)%5<=SourceRandom.Roll(ref copy)%100){chosen=seed;break;}}
            Require(chosen!=0,"No Shield AI seed");NpcEquipmentRandom.Set(b,chosen);
            void Through(int frame){var s=b.WorldGround.State.Controller;IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),true);}
            Through(20);var action=ShadowActions.State(b);Require(action?.Cast.Skill==277&&BladeShields.State(b)is{NextPulseUtc:null},"Warrior did not choose/install own Shield startup");Through(20+action.Plan.Events.Single(e=>e.Type==2).Frame);
            Require(ShadowActions.State(b)==null&&BladeShields.State(b)is{NextPulseUtc:not null},"Warrior Shield did not release/end and schedule");
        }
        finally{target?.Delete();PersistentMinions.Park(b);b.Delete();BladeShields.Snapshot();}
    }
    private static Held Capture(AssassinShadowBody b,BaseCreature target)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),BladeShields.State(b),target.Serial.Value,SkillHealth.Balance256(target));
    private static AssassinShadowBody Master(PlayerMobile owner)
    {
        // Advance the actual allocation stream normally. Keep a whole,
        // unmodified original loadout with a primary-hand on-attack property;
        // unused candidates are deleted,never rerolled in place or stripped.
        for(int i=0;i<64;i++)
        {
            var b=AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowMaster,TimedEffects.Now);var binding=ShadowLoadout.Binding(b);
            b.EquipmentNumbers.Weapons(binding.Primary,binding.Secondary,true,true,0,0,0).Enter();
            if(b.EquipmentNumbers.Stats.Any(s=>s.Stat==195&&s.Value>0)){return b;}b.Delete();
        }
        throw new InvalidOperationException("No bounded original on-attack Master loadout fixture");
    }
    private static void Check(bool reload)
    {
        _=ShadowBladeShields.Row();NpcDamageFillChecks.SourceCases("npc-blade-shield-fill-cases.json",64);PlayerMobile owner;AssassinShadowBody[] bodies;BaseCreature[] targets;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();targets=saved.Bodies.Select(h=>(BaseCreature)World.FindMobile((Serial)h.Target)).ToArray();
            // Offline parked scheduling may move its next update;cast identity,
            // rank,original lifetime and all original actor state must persist.
            for(int i=0;i<bodies.Length;i++)
            {var current=Capture(bodies[i],targets[i]);Require(current.Shield is{NextPulseUtc:not null}&&Json(current with{Shield=current.Shield with{NextPulseUtc=saved.Bodies[i].Shield.NextPulseUtc}})==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload lost original Shield state/rank/lifetime/items/wounds/RNG or replayed action");}
            owner.MoveToWorld(new(6260,3480,0),Map.Trammel);
            foreach(var b in bodies)
            {
                b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var shield=BladeShields.State(b);var due=shield.NextPulseUtc.Value;
                int hp=targets.Sum(SkillHealth.Balance256);NpcEquipmentRandom.Set(b,HitSeed(ShadowSkillCasting.Values(b,shield.Rank,277)));
                BladeShields.Tick(due,true,b.Serial.Value);
                Require(targets.Sum(SkillHealth.Balance256)<hp&&BladeShields.State(b)is{} after&&after.Id==shield.Id&&after.Rank==shield.Rank&&after.StartedUtc==shield.StartedUtc&&after.UntilUtc==shield.UntilUtc&&after.NextPulseUtc==due.AddSeconds(1)&&ShadowActions.State(b)==null,"Reloaded NPC Shield did not resume damage on its existing lifetime/rank/cadence");
                foreach(var t in targets){TimedEffects.CleansePoison(t,0,due.AddMilliseconds(40));}SorceressSpellMissiles.Cancel(b);ChainLightning.Cancel(b);PersistentMinions.Park(b);
            }
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.bladeshield",1);
            bodies=[AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowWarrior,TimedEffects.Now),Master(owner)];targets=new BaseCreature[2];
            var skill=BladeShields.SkillRoll;var hit=BladeShields.HitRoll;var weapon=BladeShields.WeaponRoll;
            try
            {
                BladeShields.SkillRoll=_=>throw new InvalidOperationException("NPC used player skill RNG");BladeShields.HitRoll=()=>throw new InvalidOperationException("NPC used player hit RNG");BladeShields.WeaponRoll=_=>throw new InvalidOperationException("NPC used player weapon RNG");
                for(int i=0;i<bodies.Length;i++){var b=bodies[i];b.ChangeSkills(book=>{book.Assign(252,10);book.Assign(263,10);book.Assign(277,3);});GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);targets[i]=Exercise(b,owner);}
                Warrior(owner);
            }
            finally{BladeShields.SkillRoll=skill;BladeShields.HitRoll=hit;BladeShields.WeaponRoll=weapon;}
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select((b,i)=>Capture(b,targets[i])).ToArray(),generation)));
        Log.Information("Shadow Blade Shield:64full NPC SrcDam96 source controls;own start28/SC/do54,shared persistent rank/expiry/periodic state,full NPC hit/fill/contact/poison and event lanes,no owner/player RNG/mana/wear,duplicate/offline/park/town/cancel/recast and Warrior AI,original state through two reloads;current event/outer-range parity and remaining routes stay scoped");
    }
}
