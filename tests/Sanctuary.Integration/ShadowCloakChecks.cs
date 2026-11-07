// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowCloakChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowCloakChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,uint Target,NecromancerCurse Curse,int Defense,int TargetDefense);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-cloak-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-cloak-check",false);Register("shadow-cloak-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Intrinsic Shadow Cloak posted armor/blind/recast/AI and saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static GraveyardSkeleton Target(AssassinShadowBody b,int dx=3,int dy=0,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {
        var t=new GraveyardSkeleton{NoKillAwards=true,Frozen=true,CantWalk=true};if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(t,difficulty);}
        t.MoveToWorld(new(b.X+dx,b.Y+dy,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static ShadowPassiveList Cloak(AssassinShadowBody b)=>b.EquipmentNumbers.Passives.Capture().Lists.SingleOrDefault(l=>l.Skill==264);
    private static SkillValues Values(AssassinShadowBody b,int id=264)=>ShadowSkillCasting.Values(b,b.OwnedSkillRank(id),id);
    private static void Cast(AssassinShadowBody b,DateTime at)
    {
        var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();var before=Cloak(b);
        Require(ShadowActions.BeginBuff(b,null,at,264),"Own Cloak SC entry refused");var action=ShadowActions.State(b);int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame;
        ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true,b.Serial.Value);Require(Json(Cloak(b))==Json(before)&&NpcEquipmentRandom.Require(b)==rng,"Cloak applied before SC marker");
        ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);var expected=ShadowDefensiveBuffs.Capture(264,Values(b),at.AddMilliseconds(release*40));
        Require(Json(Cloak(b))==Json(expected),"Own Cloak list did not capture current values at release");
        ShadowActions.Tick(at.AddSeconds(10),true,b.Serial.Value);
        Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world,"Cloak end replayed or drew actor/world RNG");
    }
    private static void Rejected(AssassinShadowBody b,DateTime at)
    {
        var state=Json(b.EquipmentNumbers.Passives.Capture());var rng=NpcEquipmentRandom.Require(b);
        Require(!ShadowActions.BeginBuff(b,null,at,264)&&ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&NpcEquipmentRandom.Require(b)==rng&&Json(b.EquipmentNumbers.Passives.Capture())==state,"Locked Cloak started,refreshed state or consumed RNG");
    }
    private static void Refusals(AssassinShadowBody b,PlayerMobile owner)
    {
        var point=b.Location;var at=TimedEffects.Now.AddMinutes(5);var rng=NpcEquipmentRandom.Require(b);string state=Json(b.EquipmentNumbers.Passives.Capture());
        b.MoveToWorld(new(1438,1696,0),b.Map);Require(!ShadowActions.BeginBuff(b,null,at,264)&&NpcEquipmentRandom.Require(b)==rng,"Town Cloak cast or drew RNG");b.MoveToWorld(point,owner.Map);
        Require(ShadowActions.BeginBuff(b,null,at,264),"Parked windup fixture refused");PersistentMinions.Park(b);ShadowActions.Tick(at.AddSeconds(10),true,b.Serial.Value);
        Require(ShadowActions.State(b)==null&&Cloak(b)==null&&Json(b.EquipmentNumbers.Passives.Capture())==state&&NpcEquipmentRandom.Require(b)==rng,"Parking released/reset Cloak or other posted lists");b.MoveToWorld(point,owner.Map);
    }
    private static BaseCreature Exercise(AssassinShadowBody b,PlayerMobile owner)
    {
        var at=TimedEffects.Now.AddMinutes(5);GeneratedNpcLiveResources.ApplyShadowBuff(b,258,Values(b,258),at);GeneratedNpcLiveResources.ApplyShadowBuff(b,278,Values(b,278),at);Refusals(b,owner);
        var near=Target(b);var behind=Target(b,-4);var edge=Target(b,15);var outside=Target(b,-16);var unique=new EncounterCreature{NoKillAwards=true,Frozen=true,CantWalk=true};var profile=EncounterProfiles.Profile("crypt-sentinel-90");unique.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Normal,EncounterGrade.Unique,ChampionKind.Champion,[],EliteAura.None,null,new Random(1));unique.MoveToWorld(new(b.X+5,b.Y+2,b.Z),b.Map);unique.AIObject.AITimer.Stop();
        var all=new BaseCreature[]{near,behind,edge,outside,unique};BaseCreature held=null;
        try
        {
            int mana=NecromancerEffects.ManaBalance256(owner),ownMana=b.EquipmentNumbers.Read(8),ownLife=SkillHealth.Balance256(b),ownerDefense=ClassStats.Defense(owner),baseArmor=b.EquipmentNumbers.Read(16),nativeArmor=b.EquipmentNumbers.Read(31),targetDefense=ClassStats.Defense(near);
            var oldBuffs=b.EquipmentNumbers.Passives.Capture().Lists.Where(l=>l.UntilUtc.HasValue).Select(l=>Json(l)).Order().ToArray();var values=Values(b);
            Cast(b,at);int bonus=(int)values.Eval("passivecalc1"),penalty=(int)values.Eval("aurastatcalc1");
            Require(b.EquipmentNumbers.Read(16)==baseArmor+bonus&&b.EquipmentNumbers.Read(31)==nativeArmor&&ClassStats.Defense(b)==ClassStats.ScaleDefense(b.CombatProfile.Defense,baseArmor+bonus),"Own Cloak armor operation/actual defense failed");
            Require(NecromancerEffects.Curse(near)is{Ability:CombatAbility.CloakOfShadows,CloakBlind:true} c&&c.Source==b.Serial.Value&&c.Rank==values.Rank&&c.OriginX==b.X&&c.OriginY==b.Y&&NamedControl.Blind(near)&&!NamedControl.SpecialAllowed(near)&&!NecromancerEffects.TargetAllowed(near,owner),"NPC curse source/rank/center or blind gates failed");
            Require(ClassStats.Defense(near)==ClassStats.ScaleDefense(targetDefense,penalty)&&NecromancerEffects.Curse(behind)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(outside)==null,"NPC target armor/radius/facing used wrong caster center");
            Require(NecromancerEffects.Curse(unique)is{CloakBlind:false}&&!NamedControl.Blind(unique)&&NecromancerEffects.DefenseStatPercent(unique)==penalty,"Unique armor effect was incorrectly tied to blindness");
            Require(b.EquipmentNumbers.Passives.Capture().Lists.Length==5&&oldBuffs.SequenceEqual(b.EquipmentNumbers.Passives.Capture().Lists.Where(l=>l.UntilUtc.HasValue&&l.Skill!=264).Select(l=>Json(l)).Order()),"Cloak replaced Quickness/Venom or lost the five-list frame");
            Require(CloakShadows.State(owner)==null&&CloakShadows.State(b)==null&&ClassStats.Defense(owner)==ownerDefense&&NecromancerEffects.ManaBalance256(owner)==mana&&b.EquipmentNumbers.Read(8)==ownMana&&SkillHealth.Balance256(b)==ownLife,"Cloak borrowed owner state/defense/mana or reset own pools");
            var old=Cloak(b);var curse=NecromancerEffects.Curse(near);near.MoveToWorld(new(b.X+100,b.Y,b.Z),b.Map);var fresh=Target(b,6,3);
            Rejected(b,at.AddSeconds(1));Require(Json(Cloak(b))==Json(old)&&NecromancerEffects.Curse(near)==curse&&NecromancerEffects.Curse(fresh)==null,"Fresh group/distant living target bypassed global lock");fresh.Delete();
            GeneratedNpcLiveResources.ApplyShadowBuff(b,267,Values(b,267),at);Require(Json(Cloak(b))==Json(old)&&b.EquipmentNumbers.Passives.State(31)&&b.EquipmentNumbers.Passives.State(159)&&!b.EquipmentNumbers.Passives.State(157)&&b.EquipmentNumbers.Read(16)==baseArmor+bonus,"Fade replaced independent Cloak/Venom or lost armor");
            foreach(var t in all){t.Delete();}CloakShadows.Prune();Require(CloakShadows.RecastAllowed(b)&&Json(Cloak(b))==Json(old),"Last affected target death changed the independent caster list or failed to unlock");
            b.ChangeSkills(book=>book.Assign(264,3));held=Target(b);Cast(b,at.AddSeconds(3));
            Require(Cloak(b).Level==b.OwnedSkillRank(264)&&b.EquipmentNumbers.Read(16)==baseArmor+(int)Values(b).Eval("passivecalc1"),"Lower-rank active recast stacked old armor or ignored new own rank");
            // Expire just the caster list;the still-living target curse owns
            // recast admission independently of the caster's armor lifetime.
            b.EquipmentNumbers.Passives.ApplyBuff(Cloak(b) with{UntilUtc=TimedEffects.Now.AddSeconds(-1)});
            Require(b.EquipmentNumbers.Read(16)==baseArmor&&Cloak(b)==null&&NamedControl.Blind(held),"Own Cloak expiry changed target blindness or retained armor");Rejected(b,at.AddSeconds(4));
            held.Delete();held=null;CloakShadows.Prune();Require(CloakShadows.RecastAllowed(b),"Last affected target death did not unlock NPC Cloak");
            held=Target(b);Cast(b,at.AddSeconds(5));Require(b.EquipmentNumbers.Read(16)==baseArmor+(int)Values(b).Eval("passivecalc1"),"Post-expiry recast retained old armor");
            // Difficulty does not apply Dim Vision's duration divisor.
            foreach(var difficulty in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {var t=Target(b,7,3,difficulty);try{Require(CloakShadows.Apply(b,t,Values(b),at)&&NecromancerEffects.Curse(t).UntilUtc==at.AddMilliseconds(Values(b).Eval("auralencalc")*40),"NPC Cloak incorrectly divided duration by difficulty");}finally{t.Delete();}}
            return held;
        }
        finally{foreach(var t in all){t.Delete();}if(held==null){ShadowActions.Cancel(b);}CloakShadows.Prune();}
    }
    private static void Warrior(PlayerMobile owner)
    {
        var b=AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowWarrior,TimedEffects.Now);BaseCreature t=null;
        try
        {
            b.ChangeSkills(book=>book.Assign(264,10));GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);ShadowGroundMotion.Activate(b,owner,TimedEffects.Now.AddMinutes(10));TimedEffects.PreventHealing(b,TimedEffects.Now);
            Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.CloakOfShadows),"Owner Cloak selection refused");t=Target(b,6);
            ulong chosen=0;for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;SourceRandom.Roll(ref copy);if(SourceRandom.Roll(ref copy)%100>70){continue;}if(SourceRandom.Roll(ref copy)%5<=SourceRandom.Roll(ref copy)%100){chosen=seed;break;}}
            Require(chosen!=0,"No Cloak AI seed");NpcEquipmentRandom.Set(b,chosen);
            void Through(int frame){var s=b.WorldGround.State.Controller;IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),true);}
            Through(20);var action=ShadowActions.State(b);Require(action?.Cast.Skill==264&&ShadowGroundMotion.Mode(b)==7,"Warrior did not select own Cloak SC");
            Through(20+action.Plan.Events.Single(e=>e.Type==2).Frame);Require(Cloak(b)!=null&&NecromancerEffects.Curse(t)?.Source==b.Serial.Value&&ShadowActions.State(b)==null,"Warrior Cloak did not release/end");
        }
        finally{t?.Delete();PersistentMinions.Park(b);b.Delete();CloakShadows.Prune();}
    }
    private static Held Capture(AssassinShadowBody b,BaseCreature target)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),target.Serial.Value,NecromancerEffects.Curse(target),ClassStats.Defense(b),ClassStats.Defense(target));
    private static void Check(bool reload)
    {
        _=ShadowCloaks.Row();PlayerMobile owner;AssassinShadowBody[] bodies;BaseCreature[] targets;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();targets=saved.Bodies.Select(h=>(BaseCreature)World.FindMobile((Serial)h.Target)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i],targets[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null&&!CloakShadows.RecastAllowed(bodies[i])&&NamedControl.Blind(targets[i]),"Reload lost own Cloak list/armor/curse/global lock/original state or replayed action");}
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.cloakofshadows",1);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();targets=new BaseCreature[2];
            for(int i=0;i<bodies.Length;i++)
            {var b=bodies[i];b.ChangeSkills(book=>{book.Assign(252,10);book.Assign(263,10);book.Assign(258,5);book.Assign(267,6);book.Assign(278,3);book.Assign(264,10);});GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);targets[i]=Exercise(b,owner);PersistentMinions.Park(b);}
            Warrior(owner);
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select((b,i)=>Capture(b,targets[i])).ToArray(),generation)));
        Log.Information("Shadow Cloak:both species own SC/do47,timed armor list and actual defense,shared curse source/rank/center/blind/unique gates,global target-derived lock,independent Quickness/Fade/Venom,expiry/death/recast,owner/pool/RNG exclusion,Warrior AI and original five-list/items/wounds/RNG/curse/lock through two reloads;remaining consumers/Master/death/learned routes pending");
    }
}
