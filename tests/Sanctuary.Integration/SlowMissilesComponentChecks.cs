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

public static class SlowMissilesComponentChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SlowMissilesComponentChecks));
    private sealed record Saved(uint Owner,uint Target,uint Expired,uint Helm,SlowMissilesState State,int Mana,Point3D Origin,SlowMissilePayload Payload);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"slow-missiles-component-fixtures.json");
    private static bool _creating;
    private static void Freeze()
    {
        var regen=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="SlowMissilesComponentOwner"?TimeSpan.FromHours(1):regen?.Invoke(a)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    public static void Configure()
    {
        Freeze();
        Register("slow-missiles-component-check",Check);Register("slow-missiles-component-reload-check",Reload);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Slow Missiles state/launch component, not a complete skill",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile owner,string id)
    {
        if(ClassCreation.Profiles.GetRank(owner.Serial,id)>0){return;}
        Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var talent),"Talent "+id);
        foreach(var prerequisite in talent.Prerequisites){Train(owner,prerequisite);}
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Training "+id);
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="SlowMissilesComponentOwner",Player=true};}finally{_creating=false;}owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        owner.MoveToWorld(new(2000,1200,Map.Trammel.GetAverageZ(2000,1200)),Map.Trammel);owner.Mana=0;return owner;
    }
    private static BaseCreature Target(PlayerMobile owner)
    {
        var target=new GraveyardZombie{NoKillAwards=true,Frozen=true,CantWalk=true};target.AIObject.AITimer.Stop();
        target.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);return target;
    }
    private static void Differential()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"slow-missiles-source-probe.json")));int count=0;
        foreach(var a in source.RootElement.GetProperty("velocity").EnumerateArray())
        {
            bool enabled=a[5].GetInt32()!=0 && a[6].GetInt32()!=0 && a[7].GetInt32()!=0;
            Require(SlowMissilePayload.VelocityFp8(a[1].GetInt32(),a[2].GetInt32(),a[3].GetInt32(),a[0].GetInt32(),a[4].GetInt32(),enabled,a[8].GetInt32())==a[9].GetInt32(),"Compiled factory velocity differential");count++;
        }
        Require(count==1152,"Incomplete velocity matrix");
        int[] speed=[68,62,57,53,50,48,46,44,42,41,40,39,38,37,36,35,35,34,34,33];
        int[] damage=[94,90,86,83,80,78,77,76,74,73,72,71,70,70,69,68,68,68,67,67];
        var row=SkillMechanics.For(CombatAbility.SlowMissiles);
        for(int rank=1;rank<=20;rank++)
        {
            var values=new SkillValues(row,_=>rank,_=>rank,99);
            Require(values.Eval("aurastatcalc1")==speed[rank-1] && values.Eval("aurastatcalc2")==damage[rank-1]
                && values.Eval("auralencalc")==300+150*(rank-1) && values.Eval("aurarangecalc")==27 && values.Mana256==1280,"Pinned dual-stat rank golden "+rank);
        }
        // Original controlled D2R tests:100 damage becomes67 at rank20 for
        // physical/fire/cold/lightning/magic/poison, preserving cold/poison length.
        var payload=new SlowMissilePayload(33,67);
        var input=new MissileSourceStats(FireMin:25600,FireMax:25600,LightningMin:25600,LightningMax:25600,
            MagicMin:25600,MagicMax:25600,ColdMin:25600,ColdMax:25600,ColdFrames:50,PoisonMin:512,PoisonMax:512,
            PoisonFrames:98,PoisonCount:2,LifeDrain:7,ManaDrain:11);var scaled=payload.Scale(input);
        Require(payload.Damage256(25600)==17152 && scaled.FireMin==17152 && scaled.FireMax==17152
            && scaled.LightningMin==17152 && scaled.LightningMax==17152 && scaled.MagicMin==17152 && scaled.MagicMax==17152
            && scaled.ColdMin==17152 && scaled.ColdMax==17152 && scaled.PoisonMin==343 && scaled.PoisonMax==343
            && scaled.ColdFrames==50 && scaled.PoisonFrames==98 && scaled.PoisonCount==2 && scaled.PoisonImpactFrames==49
            && scaled.LifeDrain==7 && scaled.ManaDrain==11,"Typed candidate damage/rate/duration/count/drain structure");
        Require(payload.Scale(input with{PoisonMin=1,PoisonMax=1,PoisonFrames=400}).PoisonMin==0,"Poison was scaled as aggregate total instead of rate");
        bool burnRejected=false;try{payload.Scale(input with{BurnMin=1});}catch(NotSupportedException){burnRejected=true;}
        Require(burnRejected,"Unsupported burning fields accepted");
        var at=DateTime.UnixEpoch.AddSeconds(16);using var stateSource=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"inner-sight-source-probe.json")));int states=0;
        foreach(var a in stateSource.RootElement.GetProperty("states").EnumerateArray())
        {
            if(a[3].GetInt32()!=0 || a[2].GetInt32()==0){continue;}int old=a[0].GetInt32(),rank=a[1].GetInt32(),duration=a[2].GetInt32();
            var previous=old>0?new SlowMissilesState(1,200,old,99,95,at.AddMilliseconds(4000)):null;
            var next=AmazonSlowMissiles.Replacement(previous,1,100,rank,40,80,at.AddMilliseconds(duration*40),at);var current=next??previous;
            Require((next!=null)==(a[5].GetInt32()!=0) && current.Rank==a[6].GetInt32() && current.Creator==a[8].GetUInt32()
                && current.VelocityPercent==-a[9].GetInt32() && current.DamagePercent==(current.Creator==200?95:80)
                && current.UntilUtc==DateTime.UnixEpoch.AddMilliseconds(a[7].GetInt32()*40),"Shared state-helper rank/owner/expiry differential");states++;
        }
        Require(states==36,"Incomplete shared state comparison");
        Log.Information("Slow Missiles component:1152 compiled velocity and36 shared state comparisons,20 pinned rank goldens and bounded original six-channel observation pass; full skill unavailable");
    }
    private static void Cases(PlayerMobile owner)
    {
        Train(owner,"amazon.slowmissiles");Require(!ClassCombat.Available(owner,CombatAbility.SlowMissiles) && !AmazonCombat.Implemented(CombatAbility.SlowMissiles),"Incomplete skill enabled");
        var target=Target(owner);var other=Fresh();var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        try
        {
            Require(owner.EquipItem(helm),"Named soft rank item");ClassGear.Refresh(owner);int rank=SkillMechanics.Rank(owner,CombatAbility.SlowMissiles);Require(rank==2,"Actual hard1/soft2");
            var at=TimedEffects.Now;Require(AmazonSlowMissiles.Apply(owner,target,rank,at),"Apply captured dual-stat state");var initial=AmazonSlowMissiles.State(target);
            Require(initial is{Rank:2,VelocityPercent:62,DamagePercent:90} && initial.Creator==owner.Serial.Value && initial.UntilUtc==at.AddSeconds(18),"Effective rank/state duration");
            var slow=AmazonSlowMissiles.Capture(target,true);var fast=AmazonSlowMissiles.Capture(target,false);
            Require(slow==new SlowMissilePayload(62,90) && fast==new SlowMissilePayload(100,90),"canSlow incorrectly gates damage");
            owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(SkillMechanics.Rank(owner,CombatAbility.SlowMissiles)==1 && AmazonSlowMissiles.State(target)==initial,"Captured values changed with later gear");
            Require(!AmazonSlowMissiles.Apply(other,target,1,at.AddMilliseconds(1)) && AmazonSlowMissiles.State(target)==initial,"Weaker rank overwrote state");
            Require(AmazonSlowMissiles.Apply(other,target,2,at.AddMilliseconds(20)),"Equal-rank refresh");var refreshed=AmazonSlowMissiles.State(target);
            Require(refreshed==initial with{UntilUtc=at.AddMilliseconds(20).AddSeconds(18)},"Equal rank replaced original creator or values");
            Require(AmazonInnerSight.Apply(owner,target,SkillMechanics.Values(owner,CombatAbility.InnerSight),at),"Independent Inner Sight fixture");var sight=AmazonInnerSight.State(target);
            Require(AmazonSlowMissiles.Apply(other,target,3,at),"Stronger rank replacement");Require(AmazonSlowMissiles.State(target) is{Rank:3,VelocityPercent:57,DamagePercent:86} && AmazonInnerSight.State(target)==sight,"Independent state or higher rank failed");
            AmazonSlowMissiles.Clear(target);Require(AmazonSlowMissiles.Capture(target,true)==SlowMissilePayload.Normal && slow==new SlowMissilePayload(62,90),"Existing launch payload changed after clear");
            Require(AmazonSlowMissiles.Apply(owner,target,1,at),"Reapply state");owner.Delete();Require(AmazonSlowMissiles.State(target)?.Creator==owner.Serial.Value,"Creator deletion cancelled independent target debuff");
            target.Delete();AmazonSlowMissiles.Prune();Require(AmazonSlowMissiles.Capture(target,true)==SlowMissilePayload.Normal && !AmazonSlowMissiles.Snapshot().Any(x=>x.Target==target.Serial.Value),"Deleted target state retained");
        }
        finally{helm.Delete();other.Delete();owner.Delete();target.Delete();}
    }
    private static void Persist()
    {
        var owner=Fresh();Train(owner,"amazon.slowmissiles");var target=Target(owner);var expired=Target(owner);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(helm),"Saved named item");ClassGear.Refresh(owner);
        Require(AmazonSlowMissiles.Apply(owner,target,SkillMechanics.Rank(owner,CombatAbility.SlowMissiles),TimedEffects.Now),"Saved captured state");owner.AddToBackpack(helm);ClassGear.Refresh(owner);
        var state=AmazonSlowMissiles.State(target) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Where(x=>x.Target!=target.Serial.Value).Append(state).Append(state with{Target=expired.Serial.Value,UntilUtc=TimedEffects.Now.AddMilliseconds(500)}).ToArray());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,expired.Serial.Value,helm.Serial.Value,state,NecromancerEffects.ManaBalance256(owner),owner.Location,AmazonSlowMissiles.Capture(target,true))));
    }
    private static void Check()
    {
        Differential();var owner=Fresh();Cases(owner);owner=Fresh();var target=Target(owner);var at=TimedEffects.Now;int mana=NecromancerEffects.ManaBalance256(owner);
        Require(AmazonSlowMissiles.Apply(owner,target,1,at) && AmazonSlowMissiles.State(target).UntilUtc==at.AddSeconds(12),"Rank1 real12-second state");var captured=AmazonSlowMissiles.Capture(target,true);
        Timer.StartTimer(TimeSpan.FromMilliseconds(12300),()=>
        {
            try
            {
                Require(AmazonSlowMissiles.State(target)==null && AmazonSlowMissiles.Capture(target,true)==SlowMissilePayload.Normal && captured==new SlowMissilePayload(68,94)
                    && NecromancerEffects.ManaBalance256(owner)==mana,"Natural expiration/captured payload/component resource isolation: state="+AmazonSlowMissiles.State(target)+",captured="+captured+",mana="+NecromancerEffects.ManaBalance256(owner)+",before="+mana);
                target.Delete();owner.Delete();Persist();Log.Information("slow-missiles-component-result: passed");
            }
            catch(Exception e){Log.Error(e,"slow-missiles-component-result: failed");}
        });
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var target=World.FindMobile((Serial)saved.Target) as BaseCreature;var expired=World.FindMobile((Serial)saved.Expired) as BaseCreature;
        Require(owner!=null && target!=null && expired!=null && AmazonSlowMissiles.State(target)==saved.State && AmazonSlowMissiles.State(expired)==null,"Actual saved state/expiry");
        Require(World.FindItem((Serial)saved.Helm)?.Parent==owner.Backpack && SkillMechanics.Rank(owner,CombatAbility.SlowMissiles)==1
            && NecromancerEffects.ManaBalance256(owner)==saved.Mana && AmazonSlowMissiles.Capture(target,true)==saved.Payload && saved.Payload==new SlowMissilePayload(62,90),"Saved gear removal/resource/captured effective rank");
        owner.MoveToWorld(saved.Origin,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();expired.Frozen=expired.CantWalk=true;expired.AIObject.AITimer.Stop();
        Require(!AmazonSlowMissiles.Apply(owner,target,1,TimedEffects.Now) && AmazonSlowMissiles.State(target)==saved.State,"Weaker reapply after reload");
        Require(!ClassCombat.Available(owner,CombatAbility.SlowMissiles),"Reload enabled incomplete skill");
    }
}
