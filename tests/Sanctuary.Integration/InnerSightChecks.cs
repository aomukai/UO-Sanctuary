// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary.Integration;

public static class InnerSightChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(InnerSightChecks));
    private sealed record Saved(uint Owner,uint Caster,uint Target,uint Expired,uint Helm,Point3D Origin,int Mana,InnerSightState State,NecromancerCurse Curse);
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"inner-sight-fixtures.json");
    public static void Configure(){Freeze();Register("inner-sight-check",Check);Register("inner-sight-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>_creating || actor.Name is "InnerSightOwner" or "InnerSightTarget"?TimeSpan.FromHours(1):hp?.Invoke(actor)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=actor=>_creating || actor.Name=="InnerSightOwner"?TimeSpan.FromHours(1):mp?.Invoke(actor)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Inner Sight cast, flat defense, rank priority and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"inner-sight-source-probe.json")));var at=DateTime.UnixEpoch.AddSeconds(16);int states=0,defenses=0,ranges=0;
        foreach(var row in data.RootElement.GetProperty("states").EnumerateArray())
        {
            if(row[3].GetInt32()!=0){continue;}int old=row[0].GetInt32(),rank=row[1].GetInt32(),duration=row[2].GetInt32();
            var previous=old>0?new InnerSightState(1,200,old,99,at.AddMilliseconds(4000)):null;
            // Real table duration is positive; zero source duration is separately retained as source-only semantics.
            if(duration==0){continue;}
            var next=AmazonInnerSight.Replacement(previous,1,100,rank,40,at.AddMilliseconds(duration*40),at);var current=next??previous;
            Require((next!=null)==(row[5].GetInt32()!=0) && current.Rank==row[6].GetInt32() && current.Creator==row[8].GetUInt32() && current.Reduction==-row[9].GetInt32()
                && current.UntilUtc==DateTime.UnixEpoch.AddMilliseconds(row[7].GetInt32()*40),"Compiled replacement/owner/value/deadline differential");states++;
        }
        foreach(var row in data.RootElement.GetProperty("defense").EnumerateArray())
        {Require(AmazonInnerSight.DefenseValue(row[0].GetInt32(),row[2].GetInt32(),row[1].GetInt32())==Math.Max(0,row[3].GetInt32()),"Compiled signed defense differential");defenses++;}
        foreach(var row in data.RootElement.GetProperty("radius").EnumerateArray())
        {Require(AmazonInnerSight.InRadius(row[0].GetInt32(),row[1].GetInt32(),27)==(row[2].GetInt32()!=0),"Compiled source radius differential");ranges++;}
        Require(states==36 && defenses==120 && ranges==961,"Incomplete Inner Sight differential fixture");
        int[] golden=[40,65,90,115,140,165,190,215,260,305,350,395,440,485,530,575,635,695,755,815];
        for(int rank=1;rank<=20;rank++)
        {var values=new SkillValues(SkillMechanics.For(CombatAbility.InnerSight),_=>rank,_=>rank,99);Require(values.Eval("aurastatcalc1")==-golden[rank-1] && values.Eval("auralencalc")==200+(rank-1)*100 && values.Eval("aurarangecalc")==27 && values.Mana256==1280,"Pinned rank golden "+rank);}
        Require(SourceAuraTargets.Rule("bonewall") is{NoAura:true} && SourceAuraTargets.Rule("tyrael1") is{Npc:true,Attackable:false} && SourceAuraTargets.Rule("zombie1") is{Npc:false,NoAura:false,Attackable:true},"Pinned aura target species guards");
        Log.Information("Inner Sight:36 applicable positive-duration refresh,120 defense and961 radius native comparisons;20 rank goldens passed");
    }
    private static PlayerMobile Fresh(DiabloClass characterClass=DiabloClass.Amazon)
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="InnerSightOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,characterClass);ClassCreation.Profiles.AdvanceLevel(owner,99);
        owner.MoveToWorld(new(2200,1000,0),Map.Trammel);bool found=false;
        for(int x=2200;x<=2700 && !found;x+=50)for(int y=1000;y<=1400 && !found;y+=50)
        {Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}if(SpellHelper.IsTown(point,owner)){continue;}
            bool empty=true;foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,20)){if(other!=owner){empty=false;break;}}if(empty){owner.MoveToWorld(point,owner.Map);found=true;}}
        Require(found,"No independent Inner Sight area");Mana(owner);return owner;
    }
    private static void Train(PlayerMobile owner,string id)
    {if(ClassCreation.Profiles.GetRank(owner.Serial,id)>0){return;}ClassCreation.Profiles.TryGet(owner.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var definition),"Talent "+id);foreach(string prerequisite in definition.Prerequisites){Train(owner,prerequisite);}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    private static void Mana(PlayerMobile owner)=>NecromancerEffects.SetMana256(owner,Math.Min(owner.ManaMax*256,40*256+17));
    private static void Ready(PlayerMobile owner){ClassCombat.Stop(owner);var s=ClassCombat.For(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;}
    private static BaseCreature Enemy(PlayerMobile owner,int dx=3,int dy=0,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {
        BaseCreature target;_creating=true;try{target=new GraveyardZombie{Name="InnerSightTarget"};}finally{_creating=false;}
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(target,difficulty);}target.Frozen=target.CantWalk=true;target.NoKillAwards=true;target.AIObject.AITimer.Stop();target.SetHits(5000);target.Hits=target.HitsMax;target.MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z),owner.Map);return target;
    }
    private static void Cast(PlayerMobile owner,int input=0)
    {
        Ready(owner);int before=NecromancerEffects.ManaBalance256(owner);long now=Core.TickCount;var point=new Point3D(owner.X+12,owner.Y,owner.Z);
        bool accepted=input==0?ClassCombat.ContinueCast(owner,CombatAbility.InnerSight,null,point,now):ClassCombat.Perform(owner,CombatAbility.InnerSight,null,point,now);
        Require(accepted,"Inner Sight cast input");var s=ClassCombat.For(owner);Require(s.PendingTarget==Serial.Zero && s.PendingPoint==owner.Location && NecromancerEffects.ManaBalance256(owner)==before,"Self target/start payment");
        ClassCombat.ReleaseCast(owner);Require(!ClassCombat.ResolveAction(owner,s.EffectAt-1),"Early SC action event");Require(ClassCombat.ResolveAction(owner,s.EffectAt),"Inner Sight SC release");
        Require(NecromancerEffects.ManaBalance256(owner)==before-1280 && !ClassCombat.ResolveAction(owner,s.EffectAt),"Once-only five-mana payment/action event");
    }
    private static void Cases(PlayerMobile owner)
    {
        Require(!ClassCombat.Available(owner,CombatAbility.InnerSight),"Unlearned Inner Sight available");Train(owner,"amazon.innersight");
        Require(ClassAbilities.SelfTarget(CombatAbility.InnerSight) && !SkillBindings.Instance.Assign(owner,true,CombatAbility.InnerSight) && SkillBindings.Instance.Assign(owner,false,CombatAbility.InnerSight) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.InnerSight),"Right/hotkey/self metadata");
        Cast(owner); // Empty cast still succeeds and pays.
        Ready(owner);NecromancerEffects.SetMana256(owner,4*256);Require(!ClassCombat.Perform(owner,CombatAbility.InnerSight,null,owner.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==4*256,"Insufficient mana");Mana(owner);
        Ready(owner);int before=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,CombatAbility.InnerSight,null,owner.Location,Core.TickCount),"Cancelled cast begin");long effect=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);Require(!ClassCombat.ResolveAction(owner,effect) && NecromancerEffects.ManaBalance256(owner)==before,"Cancelled SC cast charged mana");
        var origin=owner.Location;Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.InnerSight,null,owner.Location,Core.TickCount),"Movement cast begin");effect=ClassCombat.For(owner).EffectAt;owner.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(!ClassCombat.ResolveAction(owner,effect) && NecromancerEffects.ManaBalance256(owner)==before,"Movement did not cancel pending cast");owner.MoveToWorld(origin,owner.Map);Ready(owner);
        var near=Enemy(owner,difficulty:MonsterDifficulty.Hell);var edge=Enemy(owner,13);var outside=Enemy(owner,14);var diagonal=Enemy(owner,9,9);var beyond=Enemy(owner,10,10);var behind=Enemy(owner,-3);var high=Enemy(owner,4);high.MoveToWorld(new(high.X,high.Y,owner.Z+20),owner.Map);
        var shout=BarbarianShout.Snapshot();var caster=Fresh(DiabloClass.Necromancer);Train(caster,"necromancer.lifetap");try
        {
            int basis=CreatureProfiles.Defense(near);Require(basis>40,"Hell defense fixture");BarbarianShout.Restore(shout with{States=shout.States.Append(new(near.Serial.Value,owner.Serial.Value,owner.Serial.Value,1,100,TimedEffects.Now.AddMinutes(5))).ToArray()});
            int oldDefense=ClassStats.Defense(near);double oldChance=ClassStats.HitChance(ClassStats.AttackRating(owner),oldDefense,ClassStats.Level(owner),ClassStats.Level(near));
            Require(NecromancerEffects.Apply(caster,near,CombatAbility.LifeTap,1),"Coexisting ordinary curse");var curse=NecromancerEffects.Curse(near);Cast(owner,1);
            Require(AmazonInnerSight.State(near) is{Rank:1,Reduction:40} && NecromancerEffects.Curse(near)==curse,"Inner Sight replaced ordinary curse");
            Require(AmazonInnerSight.State(edge)!=null && AmazonInnerSight.State(diagonal)!=null && AmazonInnerSight.State(behind)!=null && AmazonInnerSight.State(outside)==null && AmazonInnerSight.State(beyond)==null && AmazonInnerSight.State(high)==null,"Source radius/self-centered/native height selection");
            int defense=ClassStats.Defense(near);Require(defense==AmazonInnerSight.DefenseValue(basis,100,40) && defense<oldDefense,"Flat reduction must precede the existing percentage pool");double chance=ClassStats.HitChance(ClassStats.AttackRating(owner),defense,ClassStats.Level(owner),ClassStats.Level(near));Require(chance>oldChance && ClassStats.CheckHit(owner,near,0,()=>(oldChance+chance)/2)==true,"Defense reduction did not reach actual hit consumer");
            var values=SkillMechanics.Values(owner,CombatAbility.InnerSight);var state=AmazonInnerSight.State(near);AmazonInnerSight.Apply(owner,near,values,TimedEffects.Now);Require(AmazonInnerSight.State(near).Reduction==40,"Equal-rank cast stacked");
            Require(NecromancerEffects.Apply(caster,near,CombatAbility.AmplifyDamage,1) && AmazonInnerSight.State(near)!=null,"Replacing curse removed independent Inner Sight");
            Require(NecromancerEffects.Apply(caster,near,CombatAbility.Attract,1),"Attract coexistence fixture");Cast(owner);Require(NecromancerEffects.Curse(near)?.Ability==CombatAbility.Attract && AmazonInnerSight.State(near)!=null,"Attract incorrectly blocked or was removed by Inner Sight");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);try
            {
                Require(owner.EquipItem(helm),"Actual Tarnhelm soft rank");ClassGear.Refresh(owner);Cast(owner);Require(AmazonInnerSight.State(near) is{Rank:2,Reduction:65},"Higher-rank cast did not replace");var strong=AmazonInnerSight.State(near);owner.AddToBackpack(helm);ClassGear.Refresh(owner);Cast(owner);Require(AmazonInnerSight.State(near)==strong,"Weaker-rank cast overwrote stronger state");
            }
            finally{helm.Delete();ClassGear.Refresh(owner);}
        }
        finally{BarbarianShout.Restore(shout);caster.Delete();foreach(var t in new[]{near,edge,outside,diagonal,beyond,behind,high}){AmazonInnerSight.Clear(t);t.Delete();}}
        var wall=new Static(0x80);var occluded=Enemy(owner);try
        {
            wall.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Require(!owner.Map.LineOfSight(new Point3D(owner.X,owner.Y,owner.Z+12),new Point3D(occluded.X,occluded.Y,occluded.Z+12)),"LOS obstruction fixture");Cast(owner);Require(AmazonInnerSight.State(occluded)!=null,"Unrequested LOS gate blocked source aura");
        }
        finally{wall.Delete();AmazonInnerSight.Clear(occluded);occluded.Delete();}
        Mana(owner);Ready(owner);owner.MoveToWorld(new(1428,1695,0),Map.Trammel);Require(SpellHelper.IsTown(owner.Location,owner) && !ClassCombat.Perform(owner,CombatAbility.InnerSight,null,owner.Location,Core.TickCount),"Town cast accepted");owner.MoveToWorld(origin,Map.Trammel);Ready(owner);
    }
    private static void Persist(PlayerMobile owner)
    {
        var target=Enemy(owner);var expired=Enemy(owner,5);var caster=Fresh(DiabloClass.Necromancer);Train(caster,"necromancer.lifetap");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Saved soft rank");ClassGear.Refresh(owner);Mana(owner);Cast(owner);owner.AddToBackpack(helm);ClassGear.Refresh(owner);
        Require(NecromancerEffects.Apply(caster,target,CombatAbility.LifeTap,1),"Saved independent curse");var state=NecromancerEffects.Profiles.Find(target);NecromancerEffects.Profiles.Store(target,state with{Curse=state.Curse with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}});
        var all=AmazonInnerSight.Snapshot();AmazonInnerSight.Restore(all.Select(x=>x.Target==target.Serial.Value?x with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}:x.Target==expired.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(500)}:x).ToArray());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,caster.Serial.Value,target.Serial.Value,expired.Serial.Value,helm.Serial.Value,owner.Location,NecromancerEffects.ManaBalance256(owner),AmazonInnerSight.State(target),NecromancerEffects.Curse(target))));
    }
    private static void Check()
    {
        Differential();var owner=Fresh();Cases(owner);var target=Enemy(owner);Mana(owner);Cast(owner);var state=AmazonInnerSight.State(target);Require(state.Rank==1 && Math.Abs((state.UntilUtc-TimedEffects.Now).TotalSeconds-8)<.2,"Rank1 eight-second duration");
        Timer.StartTimer(TimeSpan.FromMilliseconds(8200),()=>
        {try{Require(AmazonInnerSight.State(target)==null && ClassStats.Defense(target)==CreatureProfiles.Defense(target),"Natural Inner Sight expiration");target.Delete();Persist(owner);Log.Information("inner-sight-result: passed");}catch(Exception ex){Log.Error(ex,"inner-sight-result: failed");}});
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var target=World.FindMobile((Serial)saved.Target) as BaseCreature;var expired=World.FindMobile((Serial)saved.Expired) as BaseCreature;
        Require(owner!=null && target!=null && expired!=null && AmazonInnerSight.State(target)==saved.State && AmazonInnerSight.State(expired)==null && NecromancerEffects.Curse(target)==saved.Curse,"Saved retained/expired states or independent curse");
        Require(NecromancerEffects.ManaBalance256(owner)==saved.Mana && World.FindItem((Serial)saved.Helm)?.Parent==owner.Backpack && SkillMechanics.Rank(owner,CombatAbility.InnerSight)==1 && saved.State.Rank==2 && saved.State.Reduction==65,"Saved mana, actual gear removal or captured rank");
        Require(ClassStats.Defense(target)==AmazonInnerSight.DefenseValue(CreatureProfiles.Defense(target),0,65),"Saved defense consumer");
        target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();expired.Frozen=expired.CantWalk=true;expired.AIObject.AITimer.Stop();owner.MoveToWorld(saved.Origin,Map.Trammel);Mana(owner);var strong=AmazonInnerSight.State(target);Cast(owner);Require(AmazonInnerSight.State(target)==strong,"Weaker recast after real reload changed stronger state");
        Require(AmazonInnerSight.State(expired) is{Rank:1,Reduction:40},"Expired target did not accept a fresh cast");AmazonInnerSight.Clear(expired); // Retain the expired inspection branch for the second restart.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Mana=NecromancerEffects.ManaBalance256(owner)}));
    }
}
