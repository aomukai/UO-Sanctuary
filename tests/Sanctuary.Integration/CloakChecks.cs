// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class CloakChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CloakChecks));
    private sealed record Saved(uint Owner,uint Target,uint Helm,CloakCasterState State,NecromancerCurse Curse,int Mana,int OwnerDefense,int TargetDefense,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cloak-fixtures.json");
    private static bool _creating;
    private static void Rates(){var mp=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=x=>_creating || x.Name?.StartsWith("CloakFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;}
    public static void Configure(){Rates();Register("cloak-check",Check);Register("cloak-reload-check",Reload);Register("cloak-input-check",Input);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Cloak defense, curse/AI, gating, input and persistence",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"{Name}: failed",name);}});
    private static void Train(PlayerMobile p,CombatAbility ability,int rank){string id=SkillMechanics.For(ability).TalentId;while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+ability);}}
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x191,Name="CloakFixture",Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        FixtureSpace.Place(p,new(6000,1850,0),Map.Trammel);Train(p,CombatAbility.PsychicHammer,1);p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static GraveyardSkeleton Target(PlayerMobile p,int dx=4,int dy=0,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {var t=new GraveyardSkeleton{Name="CloakFixtureTarget",NoKillAwards=true};if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(t,difficulty);}t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;}
    private static EncounterCreature Elite(PlayerMobile p,EncounterGrade grade,int dx,int dy)
    {var t=new EncounterCreature{Name="CloakFixtureEncounter",NoKillAwards=true};var row=EncounterProfiles.Profile("crypt-sentinel-90");t.Assign(row,row.MinimumLevel,MonsterDifficulty.Normal,grade,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;}
    private static SkillValues Values(int rank){var row=SkillMechanics.For(CombatAbility.CloakOfShadows);return new(row,k=>k==row.SourceKey?rank:0,_=>0,99);}
    private static void Hold(CloakCasterState state)=>CloakShadows.Restore(CloakShadows.Snapshot().Where(x=>x.Owner!=state.Owner).Append(state).ToArray());
    private static void Cast(PlayerMobile p,bool held=false,Mobile hovered=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);bool start=held?ClassCombat.ContinueCast(p,CombatAbility.CloakOfShadows,hovered,new(65530,65530,0),Core.TickCount):ClassCombat.Perform(p,CombatAbility.CloakOfShadows,hovered,new(65530,65530,0),Core.TickCount);
        Require(start,$"Cloak begin: alive={p.Alive} overlap={AlliedOverlap.Allowed(p)} frozen={p.Frozen} paralyzed={p.Paralyzed} spell={p.Spell} recovery={HitRecovery.Active(p,Core.TickCount)} pending={ClassCombat.For(p).PendingAbility} available={ClassCombat.Available(p,CombatAbility.CloakOfShadows)} mana={mana} map={p.Map} point={p.Location}");ClassCombat.ReleaseCast(p);var s=ClassCombat.For(p);Require(s.PendingTarget==Serial.Zero && s.PendingPoint==p.Location && s.Animation==16 && NecromancerEffects.ManaBalance256(p)==mana,"Unpaid canonical SC self cast");Require(ClassCombat.ResolveAction(p,s.EffectAt) && NecromancerEffects.ManaBalance256(p)==mana-3328,"Exactly one13mana release");Require(!ClassCombat.ResolveAction(p,s.EffectAt),"Repeated payment");
    }
    private static void Refused(PlayerMobile p,string context)
    {
        Ready(p);var state=CloakShadows.State(p);int mana=NecromancerEffects.ManaBalance256(p);
        var session=ClassCombat.For(p);long effect=session.EffectAt;byte animation=session.Animation;
        p.Direction=Direction.West;var aim=new Point3D(p.X+5,p.Y,p.Z);
        Require(!ClassCombat.Perform(p,CombatAbility.CloakOfShadows,null,aim,Core.TickCount)
            && session.PendingAbility==CombatAbility.None && session.EffectAt==effect && session.Animation==animation
            && NecromancerEffects.ManaBalance256(p)==mana && CloakShadows.State(p)==state
            && p.Direction==p.GetDirectionTo(aim),context+": no new animation, pending cast, mana or defense refresh");
    }
    private static void RecastGates()
    {
        var p=Fresh();var other=Fresh();Train(p,CombatAbility.CloakOfShadows,1);Train(other,CombatAbility.CloakOfShadows,1);
        var first=Target(p);var last=Target(p,5,2);var fresh=Target(p,6,3);
        // Keep fresh outside the first caster-centered pulse, then move it in.
        fresh.MoveToWorld(new(p.X+20,p.Y,p.Z),p.Map);Cast(p);fresh.MoveToWorld(new(p.X+6,p.Y+3,p.Z),p.Map);
        var captured=CloakShadows.State(p);var original=NecromancerEffects.Curse(last);
        Refused(p,"Fresh group cannot bypass surviving cloaked targets");
        first.Kill();first.Corpse?.Delete();Require(!first.Alive,"First test target survived death");
        Refused(p,"One surviving target retains caster lock");
        last.MoveToWorld(new(p.X+50,p.Y,p.Z),p.Map);
        Refused(p,"Out-of-range surviving target retains caster lock");
        Require(CloakShadows.State(p)==captured && NecromancerEffects.Curse(last)==original && NecromancerEffects.Curse(fresh)==null,"Rejected casts changed buffs/targets");
        last.Kill();last.Corpse?.Delete();Require(!last.Alive,"Last test target survived death");
        Cast(p);Require(NecromancerEffects.Curse(fresh)?.Source==p.Serial.Value,"Last target death did not unlock real cast/fresh group");
        // The source-index is independent from the self-defense record. Existing
        // saves need no new lock field, even if a self buff was already cleared.
        CloakShadows.Clear(p);Refused(p,"Clearing self defense must not discard a living target lock");
        NecromancerEffects.Profiles.Clear(fresh);
        Require(CloakShadows.Apply(other,fresh,Values(1),TimedEffects.Now),"Cross-caster fixture");
        Cast(p);Require(NecromancerEffects.Curse(fresh)?.Source==other.Serial.Value,"Another caster's target locked this caster or was overwritten");
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);captured=CloakShadows.State(p);
        Require(ClassCombat.Perform(p,CombatAbility.CloakOfShadows,null,p.Location,Core.TickCount),"Unblocked windup setup");long at=ClassCombat.For(p).EffectAt;
        NecromancerEffects.Profiles.Clear(fresh);Require(CloakShadows.Apply(p,fresh,Values(1),TimedEffects.Now),"Release-time lock setup");
        Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana && CloakShadows.State(p)==captured,"Release-time lock paid mana or refreshed defense");
        var state=NecromancerEffects.Profiles.Find(fresh);NecromancerEffects.Profiles.Store(fresh,state with{Curse=state.Curse with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}});
        Cast(p);Require(NecromancerEffects.Curse(fresh)?.Source==p.Serial.Value,"Expired target curse retained a stale recast lock");
        first.Delete();last.Delete();fresh.Delete();p.Delete();other.Delete();
        Log.Information("Cloak corrected recast: fresh group, last survivor, range, death, source, release-time and expiry gates pass");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.CloakOfShadows);Require(row.SourceId==264 && row.Number("aurafilter")==57347 && !ClassAbilities.SelfTarget(row.Ability),"Pinned filter/cursor-aim input");
        foreach(var (rank,defense,penalty,frames) in new[]{(1,10,-15,200),(20,67,-72,675),(21,70,-75,700),(28,91,-95,875),(255,772,-95,6550)})
        {var v=Values(rank);var s=CloakShadows.Capture(1,v,DateTime.UnixEpoch);Require(s.Defense==defense && v.Eval("aurastatcalc1")==penalty && v.Eval("aurarangecalc")==30 && s.UntilUtc==DateTime.UnixEpoch.AddMilliseconds(frames*40) && v.Mana256==3328,"Cloak formula golden "+rank);}
        var old=new NecromancerCurse(CombatAbility.CloakOfShadows,1,20,DateTime.UnixEpoch.AddSeconds(20),0,0,CloakBlind:true);var next=old with{Source=2,Rank=19,UntilUtc=DateTime.UnixEpoch.AddSeconds(30)};
        Require(CloakShadows.Replacement(old,next)==null && CloakShadows.Replacement(old,next with{Rank=20})==null && CloakShadows.Replacement(old,next with{Rank=21})==null && CloakShadows.Replacement(old with{Ability=CombatAbility.Attract},next)==null,"Current observed active-target no-overwrite and Attract protection");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"cloak-source-probe.json")));int count=0;foreach(var r in doc.RootElement.EnumerateArray()){Require(r[8].GetInt32()==1 && r[9].GetInt32()==1,"Source independent Quickness/Fade slots");count++;}Require(count==192,"Complete dispatch/callback source fixture");
    }
    private static void Check()
    {
        Goldens();RecastGates();var p=Fresh();Require(!ClassCombat.Available(p,CombatAbility.CloakOfShadows),"Unlearned available");Train(p,CombatAbility.CloakOfShadows,1);
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.CloakOfShadows) && SkillBindings.Instance.Assign(p,false,CombatAbility.CloakOfShadows) && SkillBindings.Instance.AssignQuick(p,8,CombatAbility.CloakOfShadows),"Right/quick bindings");
        Ready(p);NecromancerEffects.SetMana256(p,3327);Require(!ClassCombat.Perform(p,CombatAbility.CloakOfShadows,null,p.Location,Core.TickCount),"Insufficient mana");Mana(p);
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.CloakOfShadows,null,p.Location,Core.TickCount),"Cancelled begin");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Cancelled cast paid");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.CloakOfShadows,null,p.Location,Core.TickCount),"Recovery begin");at=ClassCombat.For(p).EffectAt;Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at),"Recovery failed cancellation");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.CloakOfShadows,null,p.Location,Core.TickCount),"Lost mana begin");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(!ClassCombat.ResolveAction(p,at) && CloakShadows.State(p)==null,"Lost mana created state");Mana(p);
        var near=Target(p);var edge=Target(p,15);var far=Target(p,16);var behind=Target(p,-4);var unique=Elite(p,EncounterGrade.Unique,5,2);var champion=Elite(p,EncounterGrade.Champion,5,-2);int basis=ClassStats.Defense(p),enemyDefense=ClassStats.Defense(near);
        var friend=Fresh();Cast(p,true,friend);friend.Delete();var first=CloakShadows.State(p);Require(first is{Rank:1,Defense:10} && Math.Abs((first.UntilUtc-TimedEffects.Now).TotalSeconds-8)<.2,"Real owner payload");
        Require(ClassStats.Defense(p)==ClassStats.ScaleDefense(basis,10) && ClassStats.Defense(near)==ClassStats.ScaleDefense(enemyDefense,-15),"Raw owner and target defense consumers");
        Require(NecromancerEffects.Curse(edge)?.Ability==CombatAbility.CloakOfShadows && NecromancerEffects.Curse(behind)!=null && NecromancerEffects.Curse(far)==null,"Source radius and no facing restriction");
        Require(NamedControl.Blind(near) && !NamedControl.SpecialAllowed(near) && !NecromancerEffects.TargetAllowed(near,p),"Blind AI/target/special gates");
        Require(NecromancerEffects.Curse(unique) is{CloakBlind:false} && NecromancerEffects.DefenseStatPercent(unique)==-15 && !NamedControl.Blind(unique) && NecromancerEffects.Curse(champion) is{CloakBlind:true},"Independent unique/champion stats versus AI eligibility");
        Ready(p);mana=NecromancerEffects.ManaBalance256(p);p.Direction=Direction.West;var aim=new Point3D(p.X+5,p.Y,p.Z);
        Require(!ClassCombat.Perform(p,CombatAbility.CloakOfShadows,null,aim,Core.TickCount)
            && ClassCombat.For(p).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(p)==mana
            && CloakShadows.State(p)==first && p.Direction==p.GetDirectionTo(aim),"All-cloaked recast must turn without starting animation, payment or self refresh");
        var originalCurse=NecromancerEffects.Curse(near);var fresh=Target(p,6,3);
        Refused(p,"Corrected current-build fresh-group recast lock");
        Require(CloakShadows.State(p)==first && NecromancerEffects.Curse(near)==originalCurse && NecromancerEffects.Curse(fresh)==null,"Rejected fresh-group cast changed owner/targets");fresh.Delete();
        foreach(var difficulty in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell}){var t=Target(p,7,3,difficulty);var now=TimedEffects.Now;Require(CloakShadows.Apply(p,t,Values(1),now) && NecromancerEffects.Curse(t).UntilUtc==now.AddSeconds(8),"Cloak inherited Dim Vision difficulty divisor");t.Delete();}
        Train(p,CombatAbility.ClawMastery,1);Train(p,CombatAbility.BurstOfSpeed,1);AssassinBuffs.Cast(p,SkillMechanics.Values(p,CombatAbility.BurstOfSpeed));Require(CloakShadows.State(p)==first && AssassinBuffs.AttackRate(p)>0,"Quickness replaced independent Cloak");AssassinBuffs.Clear(p);
        var other=Fresh();Train(other,CombatAbility.CloakOfShadows,1);var now2=TimedEffects.Now;var old=NecromancerEffects.Curse(near);Require(!CloakShadows.Apply(other,near,Values(1),now2) && !CloakShadows.Apply(other,near,Values(3),now2) && !CloakShadows.Apply(p,near,Values(1),now2) && NecromancerEffects.Curse(near)==old,"Active cloak overwritten or refreshed");other.Delete();
        var necro=ClassSkillChecks.Player(DiabloClass.Necromancer);necro.Name="CloakFixtureNecro";necro.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(necro,99);Train(necro,CombatAbility.AmplifyDamage,1);Train(necro,CombatAbility.DimVision,1);Train(necro,CombatAbility.Confuse,1);Train(necro,CombatAbility.Attract,1);necro.MoveToWorld(p.Location,p.Map);
        Require(NecromancerEffects.Apply(necro,near,CombatAbility.AmplifyDamage,1) && !NamedControl.Blind(near) && NecromancerEffects.DefenseStatPercent(near)==0,"Replacement curse retained Cloak stats/AI");Require(NecromancerEffects.Apply(necro,near,CombatAbility.Attract,1) && !CloakShadows.Apply(p,near,Values(20),TimedEffects.Now),"Attract was replaced");NecromancerEffects.Profiles.Clear(near);necro.Delete();
        var place=p.Location;CloakShadows.Clear(p);p.MoveToWorld(new(1438,1696,0),Map.Trammel);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.CloakOfShadows,null,p.Location,Core.TickCount),"Town cast");p.MoveToWorld(place,p.Map);
        Hold(first with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});Require(CloakShadows.Apply(p,near,Values(1),TimedEffects.Now),"Expiry target setup");var cur=NecromancerEffects.Curse(near);NecromancerEffects.Profiles.Store(near,NecromancerEffects.Profiles.Find(near) with{Curse=cur with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)}});
        Later(180,"cloak-result",()=>
        {
            Require(CloakShadows.State(p)==null && !NamedControl.Blind(near) && NecromancerEffects.Profiles.Find(near)?.Curse==null && ClassStats.Defense(p)==basis,"Natural owner/curse expiry and AI cleanup");
            foreach(var t in new BaseCreature[]{edge,far,behind,unique,champion}){t.Delete();}
            Lifecycle();Train(p,CombatAbility.CloakOfShadows,20);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual soft-rank helm");ClassGear.Refresh(p);Mana(p);Cast(p);
            var state=CloakShadows.State(p);Require(state is{Rank:21,Defense:70} && Math.Abs((state.UntilUtc-TimedEffects.Now).TotalSeconds-28)<.2,"Effective21 capture");state=state with{UntilUtc=TimedEffects.Now.AddHours(1)};Hold(state);var curse=NecromancerEffects.Curse(near) with{UntilUtc=TimedEffects.Now.AddHours(1)};NecromancerEffects.Profiles.Store(near,NecromancerEffects.Profiles.Find(near) with{Curse=curse}); // Inspection hold after real duration/expiry checks.
            var pending=Fresh();Train(pending,CombatAbility.CloakOfShadows,1);Ready(pending);int pendingMana=NecromancerEffects.ManaBalance256(pending);Require(ClassCombat.Perform(pending,CombatAbility.CloakOfShadows,null,pending.Location,Core.TickCount),"Saved unpaid cast");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,near.Serial.Value,helm.Serial.Value,state,curse,NecromancerEffects.ManaBalance256(p),ClassStats.Defense(p),ClassStats.Defense(near),pending.Serial.Value,pendingMana)));Log.Information("cloak-result: passed");
        });
    }
    private static void Lifecycle()
    {
        var p=Fresh();Train(p,CombatAbility.CloakOfShadows,1);Hold(CloakShadows.Capture(p.Serial.Value,Values(1),TimedEffects.Now));p.Kill();p.Resurrect();Require(CloakShadows.State(p)==null,"Death restored owner Cloak");p.Corpse?.Delete();p.Delete();
        p=Fresh();Train(p,CombatAbility.CloakOfShadows,1);Hold(CloakShadows.Capture(p.Serial.Value,Values(1),TimedEffects.Now));Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Paladin) && CloakShadows.State(p)==null && ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Assassin),"History retained Cloak");
        Hold(CloakShadows.Capture(p.Serial.Value,Values(1),TimedEffects.Now));ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(CloakShadows.State(p)==null,"Respec retained Cloak");p.Delete();
    }
    private static void Reload()
    {
        Goldens();RecastGates();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var pending=(PlayerMobile)World.FindMobile((Serial)s.Pending);
        Require(CloakShadows.State(p)==s.State && NecromancerEffects.Curse(t)==s.Curse && NamedControl.Blind(t),"Saved owner/target/blind state");Require(World.FindItem((Serial)s.Helm)?.Parent==p && SkillMechanics.Rank(p,CombatAbility.CloakOfShadows)==21 && ClassCreation.Profiles.GetRank(p.Serial,"assassin.cloakofshadows")==20,"Saved real gear/ranks");
        Require(NecromancerEffects.ManaBalance256(p)==s.Mana && ClassStats.Defense(p)==s.OwnerDefense && ClassStats.Defense(t)==s.TargetDefense,"Saved mana/defense");Require(SkillBindings.Instance.Get(p).Right==CombatAbility.CloakOfShadows && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,8)==CombatAbility.CloakOfShadows,"Saved bindings");Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None && CloakShadows.State(pending)==null && NecromancerEffects.ManaBalance256(pending)==s.PendingMana,"Unpaid windup replayed");
        // Saved offline PlayerMobile actors load on Map.Internal. Re-enter the
        // saved target's world before exercising a real post-reload action.
        p.MoveToWorld(new(t.X-4,t.Y,t.Z),t.Map);
        Ready(p);var oldState=CloakShadows.State(p);Require(!ClassCombat.Perform(p,CombatAbility.CloakOfShadows,null,new(p.X+5,p.Y,p.Z),Core.TickCount)
            && NecromancerEffects.ManaBalance256(p)==s.Mana && CloakShadows.State(p)==oldState,"Reloaded all-cloaked cast must not pay/refresh/animate");
        var fresh=Target(p,6,3);Refused(p,"Reloaded surviving target blocks fresh group");Require(NecromancerEffects.Curse(t)==s.Curse && NecromancerEffects.Curse(fresh)==null,"Reloaded rejected cast changed targets");fresh.Delete();Hold(s.State with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});NecromancerEffects.Profiles.Store(t,NecromancerEffects.Profiles.Find(t) with{Curse=s.Curse with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)}});
        Later(180,"cloak-reload-result",()=>{Require(CloakShadows.State(p)==null && !NamedControl.Blind(t) && NecromancerEffects.DefenseStatPercent(t)==0,"Reloaded expiry/AI cleanup");Cast(p);Require(NecromancerEffects.Curse(t)?.Source==p.Serial.Value,"Reloaded expiry did not release lock");NecromancerEffects.SetMana256(p,s.Mana);Hold(s.State);NecromancerEffects.Profiles.Store(t,NecromancerEffects.Profiles.Find(t) with{Curse=s.Curse});Log.Information("cloak-reload-result: passed");});
    }
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.CloakOfShadows,1);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.CloakOfShadows) && SkillBindings.Instance.AssignQuick(p,8,CombatAbility.CloakOfShadows),"Connected binding");
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        var victim=Target(p);BaseCreature fresh=null;int mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;
        void Wait(){try{Require(Core.TickCount<deadline,"Connected Cloak timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.CloakOfShadows){began=true;Require(NecromancerEffects.ManaBalance256(p)==mana,"Premature connected payment");}if(CloakShadows.State(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}Require(began && CloakShadows.State(p) is{Rank:1,Defense:10} && NecromancerEffects.ManaBalance256(p)==mana-3328 && NecromancerEffects.Curse(victim)?.Ability==CombatAbility.CloakOfShadows,"Natural self cast/payment");
            fresh=Target(p,6,3);Ready(p);var active=CloakShadows.State(p);int paid=NecromancerEffects.ManaBalance256(p);p.Direction=Direction.West;var aim=new Point3D(p.X+5,p.Y,p.Z);
            Log.Information("cloak-input: reject-ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.CloakOfShadows,aim.X,aim.Y,aim.Z);
            Later(350,"cloak-input-result",()=>{try{Require(ClassCombat.For(p).Sequence==2 && ClassCombat.For(p).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(p)==paid && CloakShadows.State(p)==active && p.Direction==p.GetDirectionTo(aim),$"Rejected connected recast: sequence={ClassCombat.For(p).Sequence} pending={ClassCombat.For(p).PendingAbility} mana={NecromancerEffects.ManaBalance256(p)}/{paid} stateSame={CloakShadows.State(p)==active} direction={p.Direction}/{p.GetDirectionTo(aim)}");Require(NecromancerEffects.Curse(fresh)==null,"Rejected connected cast affected fresh group");
                victim.Kill();victim.Corpse?.Delete();Ready(p);
                Log.Information("cloak-input: unlocked-ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.CloakOfShadows,aim.X,aim.Y,aim.Z);
                long until=Core.TickCount+5000;
                void Unlocked()
                {
                    try
                    {
                        Require(Core.TickCount<until,"Connected unlocked cast timeout");
                        if(NecromancerEffects.Curse(fresh)?.Ability!=CombatAbility.CloakOfShadows){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Unlocked);return;}
                        Require(ClassCombat.For(p).Sequence==3 && NecromancerEffects.ManaBalance256(p)==paid-3328
                            && CloakShadows.State(p)!=active && NecromancerEffects.Curse(fresh)?.Source==p.Serial.Value,"Connected last-death recast/payment/fresh target");
                        Log.Information("cloak-input-result: passed");fresh.Delete();victim.Delete();p.Delete();
                    }
                    catch(Exception e){fresh?.Delete();victim.Delete();p.Delete();Log.Error(e,"cloak-input-result: failed");}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Unlocked);
            }catch(Exception e){fresh?.Delete();victim.Delete();p.Delete();Log.Error(e,"cloak-input-result: failed");}});}catch(Exception e){fresh?.Delete();victim.Delete();p.Delete();Log.Error(e,"cloak-input-result: failed");}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("cloak-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.CloakOfShadows,65530,65530,0);Wait();});
    }
}
