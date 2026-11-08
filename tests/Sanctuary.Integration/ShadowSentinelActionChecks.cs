// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowSentinelActionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowSentinelActionChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-sentinel-action-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-sentinel-action-check",false);Register("shadow-sentinel-action-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Sentinel S2 release/cancellation over shared blade factory",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    private static BladeSentinelBody[] Blades(PlayerMobile p)=>AssassinTrapRoster.For(p).Select(e=>World.FindMobile((Serial)e.Actor)).OfType<BladeSentinelBody>().ToArray();
    private static void Clear(PlayerMobile p){foreach(var b in Blades(p)){b.Delete();}AssassinTrapRoster.Prune();}
    private static ShadowTailChecks.Witness Target(AssassinShadowBody b)
    {var t=new ShadowTailChecks.Witness{Actor=b.Serial.Value};t.SetHits(65000);t.MoveToWorld(new(b.X+6,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();return t;}
    private static void Cast(AssassinShadowBody b,PlayerMobile p,string mode)
    {
        Clear(p);var t=Target(b);var aim=t.Location;var at=TimedEffects.Now;var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();int mana=NecromancerEffects.ManaBalance256(p),own=NecromancerEffects.ManaBalance256(b);NecromancerEffects.SetMana256(p,0);NecromancerEffects.SetMana256(b,0);
        try
        {
            Require(ShadowActions.BeginBladeSentinel(b,mode=="ground"?null:t,aim,at),"Own Sentinel S2 entry refused");var action=ShadowActions.State(b);int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==2).Frame,end=action.Plan.Events.Single(e=>e.Type==2).Frame;
            Require(action.Cast.Skill==257&&release>0&&end>release&&action.Plan.Speed==ShadowAnimation.S2(b).Speed&&Blades(p).Length==0&&NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world,"Own S2 startup emitted/allocated/rolled or borrowed clock");
            ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true,b.Serial.Value);Require(Blades(p).Length==0&&NpcWorldAllocation.Capture()==world,"Own Sentinel allocated before release");
            if(mode=="moving"){t.MoveToWorld(new(t.X+1,t.Y+1,t.Z),t.Map);aim=t.Location;}
            if(mode=="deleted"){t.Delete();}
            if(mode=="rank"){b.ChangeSkills(s=>s.Assign(257,14));}
            ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);var blade=Blades(p).Single();var state=blade.Prepared;
            Require(state.Owner==p.Serial.Value&&state.Spawn.Caster==b.Serial.Value&&state.Creation.Rank==b.OwnedSkillRank(257)&&blade.Location==b.Location&&state.Motion.FinalTarget==new Point2D(aim.X*2,aim.Y*2)&&state.FirstMissile!=null,"S2 release lost live/cached aim,own rank,root owner or birth packet");
            Require(NecromancerEffects.ManaBalance256(p)==0&&NecromancerEffects.ManaBalance256(b)==0&&NpcEquipmentRandom.Require(b)==rng,"Intrinsic Sentinel spent player/NPC mana or actor attack RNG");
            var after=NpcWorldAllocation.Capture();ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);ShadowActions.Tick(at.AddMilliseconds(end*40),true,b.Serial.Value);
            Require(Blades(p).Length==1&&NpcWorldAllocation.Capture()==after&&ShadowActions.State(b)==null&&b.OwnedSkills.Used==null,"S2 duplicate/end replay or used skill retained");
            Require(ShadowBladeSentinels.AutomaticReady&&ShadowNpcCooldowns.Approved(256)&&ShadowNpcCooldowns.Record(256).DelayFormula==0&&ShadowNpcCooldowns.Record(257).DelayFormula==0,"Approved shared NPC cooldown rule was lost");
        }
        finally{ShadowActions.Cancel(b);Clear(p);t.Delete();b.ChangeSkills(s=>{s.Assign(257,12);s.Use(null);});NecromancerEffects.SetMana256(p,mana);NecromancerEffects.SetMana256(b,own);}
    }
    private static void Shock(AssassinShadowBody b,PlayerMobile p)
    {
        var saved=ShockWebs.Snapshot();var t=Target(b);var at=TimedEffects.Now;var world=NpcWorldAllocation.Capture();var rng=NpcEquipmentRandom.Require(b);int mana=NecromancerEffects.ManaBalance256(p),own=NecromancerEffects.ManaBalance256(b);
        try
        {
            b.ChangeSkills(s=>s.Assign(256,3));NecromancerEffects.SetMana256(p,0);NecromancerEffects.SetMana256(b,0);
            Require(ShadowActions.BeginShockWeb(b,t,t.Location,at),"Own Shock Web S2 entry refused");var action=ShadowActions.State(b);int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==2).Frame,end=action.Plan.Events.Single(e=>e.Type==2).Frame;
            ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true,b.Serial.Value);Require(ShockWebs.Snapshot().NextId==saved.NextId&&NpcWorldAllocation.Capture()==world,"Shock Web allocated before actual release");
            t.MoveToWorld(new(t.X+1,t.Y,t.Z),t.Map);ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);var shards=ShockWebs.Snapshot().Shards.Where(s=>s.Id>saved.NextId).ToArray();
            Require(shards.Length>0&&shards.All(s=>s.Npc?.Actor==b.Serial.Value&&s.Owner==p.Serial.Value&&s.Rank==b.OwnedSkillRank(256)&&s.NextUtc==at.AddMilliseconds((release+1)*40))&&NpcEquipmentRandom.Require(b)==rng,"Actual Shock Web release lost original NPC/rank/clock/private stream");
            var after=ShockWebs.Snapshot();var allocation=NpcWorldAllocation.Capture();ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);ShadowActions.Tick(at.AddMilliseconds(end*40),true,b.Serial.Value);
            Require(ShockWebs.Snapshot().NextId==after.NextId&&NpcWorldAllocation.Capture()==allocation&&ShadowActions.State(b)==null&&NecromancerEffects.ManaBalance256(p)==0&&NecromancerEffects.ManaBalance256(b)==0,"Shock Web duplicate/cost/recovery differs");
            Require(((INecromancerGroundBrain)b).GroundSkill(p,9,256,t,at.AddSeconds(2))>0,"Approved automatic Shock Web consumer refused");ShadowActions.Cancel(b);
        }
        finally{ShadowActions.Cancel(b);ShockWebs.Restore(saved);t.Delete();NecromancerEffects.SetMana256(p,mana);NecromancerEffects.SetMana256(b,own);b.ChangeSkills(s=>s.Use(null));}
    }
    private static void Cancel(AssassinShadowBody b,PlayerMobile p)
    {
        var t=Target(b);var origin=b.Location;var ownerPoint=p.Location;
        try
        {
            foreach(string why in new[]{"cancel","move","map","frozen","paralyzed","skill","offline","owner-map","recovery","stun"})
            {
                var at=TimedEffects.Now;var world=NpcWorldAllocation.Capture();var rng=NpcEquipmentRandom.Require(b);Require(ShadowActions.BeginBladeSentinel(b,t,t.Location,at),"Sentinel cancellation entry refused: "+why);
                switch(why)
                {
                    case"cancel":ShadowActions.Cancel(b);break;case"move":b.MoveToWorld(new(origin.X,origin.Y+1,origin.Z),p.Map);break;case"map":b.Internalize();break;case"frozen":b.Frozen=true;break;case"paralyzed":b.Paralyzed=true;break;case"skill":b.ChangeSkills(s=>s.Use(0));break;case"owner-map":p.Internalize();break;
                    case"recovery":b.InterruptForRecovery();break;
                    case"stun":Require(CombatStuns.ApplyPreparedDuration(b,25,TimedEffects.Now),"Supplied prepared stun duration refused");break;
                }
                ShadowActions.Tick(at.AddMilliseconds(40),why!="offline",b.Serial.Value);Require(ShadowActions.State(b)==null&&Blades(p).Length==0&&NpcWorldAllocation.Capture()==world&&NpcEquipmentRandom.Require(b)==rng,"Canceled Sentinel spawned/allocated/rolled or persisted: "+why);
                b.Frozen=false;b.Paralyzed=false;p.MoveToWorld(ownerPoint,Map.Trammel);b.ChangeSkills(s=>s.Use(null));b.MoveToWorld(origin,p.Map);CombatStuns.Clear(b);
            }
            int before=Blades(p).Length;var state=NpcWorldAllocation.Capture();int result=((INecromancerGroundBrain)b).GroundSkill(p,9,257,t,TimedEffects.Now);Require(result>0&&ShadowActions.State(b)?.Cast.Skill==257&&Blades(p).Length==before&&NpcWorldAllocation.Capture()==state,"Automatic Sentinel windup refused or allocated early");ShadowActions.Cancel(b);
            var at2=TimedEffects.Now;Require(!ShadowActions.BeginBladeSentinel(b,null,new(1438,1696,0),at2),"Far/town destination cast");
            var world2=NpcWorldAllocation.Capture();Require(ShadowActions.BeginBladeSentinel(b,t,t.Location,at2),"Failed release entry refused");var a=ShadowActions.State(b);t.MoveToWorld(new(b.X+100,b.Y,b.Z),b.Map);ShadowActions.Tick(at2.AddMilliseconds(a.Plan.Events.Single(e=>e.Type==1).Frame*40),true,b.Serial.Value);Require(Blades(p).Length==0&&NpcWorldAllocation.Capture()==world2,"Out-of-range release allocated a blade");ShadowActions.Cancel(b);
        }
        finally{ShadowActions.Cancel(b);Clear(p);b.Frozen=false;b.Paralyzed=false;CombatStuns.Clear(b);p.MoveToWorld(ownerPoint,Map.Trammel);b.ChangeSkills(s=>s.Use(null));b.MoveToWorld(origin,p.Map);t.Delete();}
    }
    private static void WorldAi(PlayerMobile p,int skill)
    {
        var at=TimedEffects.Now.AddMinutes(20);var b=AssassinShadowBody.CreateInitialized(p,CombatAbility.ShadowWarrior,at);ShadowTailChecks.Witness target=null;
        var bindings=SkillBindings.Instance.Get(p);var origin=p.Location;var shock=ShockWebs.Snapshot();
        int Emitted()=>skill==257?Blades(p).Length:ShockWebs.Snapshot().Shards.Count(s=>s.Npc?.Actor==b.Serial.Value);
        void ClearEmitted(){Clear(p);ShockWebs.Restore(shock);}
        try
        {
            ClearEmitted();GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.ChangeSkills(s=>s.Assign(skill,12));ShadowGroundMotion.Activate(b,p,at);target=Target(b);
            var before=NpcWorldAllocation.Capture();int duration=((INecromancerGroundBrain)b).GroundSkill(p,9,skill,target,at);Require(duration>0&&NpcWorldAllocation.Capture()==before&&Emitted()==0,"World Sentinel startup allocated/refused");
            for(int i=0;i<duration;i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"World S2 fault: "+b.WorldGround.State.Controller.Fault);}
            Require(Emitted()>0&&ShadowActions.State(b)==null,"World Sentinel did not release/recover exactly once");ClearEmitted();
            Require(SkillBindings.Instance.Assign(p,true,(skill==257?CombatAbility.BladeSentinel:CombatAbility.ShockWeb))&&SkillBindings.Instance.Assign(p,false,(skill==257?CombatAbility.BladeSentinel:CombatAbility.ShockWeb)),"Natural Sentinel bindings refused");var state=b.WorldGround.State.Controller;b.WorldGround.Store(state with{NextThink=state.Frame+1});bool selected=false;
            for(int i=0;i<1000&&Emitted()==0;i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Natural Sentinel AI fault: "+b.WorldGround.State.Controller.Fault);selected|=ShadowActions.State(b)?.Cast.Skill==skill;}
            Require(selected&&Emitted()>0&&(skill!=257||Blades(p)[0].Prepared.Spawn.Caster==b.Serial.Value),"Natural Warrior did not select/release own Sentinel");
            state=b.WorldGround.State.Controller;b.WorldGround.Store(state with{NextThink=int.MaxValue});for(int i=0;i<100&&ShadowActions.State(b)!=null;i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);}Require(ShadowActions.State(b)==null,"Natural Sentinel never finished recovery");
        }
        finally{ShadowActions.Cancel(b);ClearEmitted();target?.Delete();PersistentMinions.Park(b);b.Delete();p.MoveToWorld(origin,Map.Trammel);SkillBindings.Instance.Assign(p,true,bindings.Right);SkillBindings.Instance.Assign(p,false,bindings.Left);}
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));p=(PlayerMobile)World.FindMobile((Serial)s.Owner);generation=s.Generation+1;bodies=s.Bodies.Select(x=>(AssassinShadowBody)World.FindMobile((Serial)x.Actor)).ToArray();for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(s.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed own originals/resources/wounds/RNG or replayed cast");}p.MoveToWorld(new(6480,3570,0),Map.Trammel);
        }
        else
        {
            p=BladeBodyChecks.Fresh(6480,30,3570);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);var world=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];try{int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.ChangeSkills(s=>s.Assign(257,12));foreach(string mode in new[]{"ground","moving","deleted","rank"}){Cast(b,p,mode);}Shock(b,p);Cancel(b,p);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);
        }
        WorldAi(p,257);WorldAi(p,256);GeneratedNpcLiveResources.Snapshot();var held=bodies.Select(Capture).ToArray();PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,held,generation)));
        Log.Information("Own Sentinel S2:actual owned attack-rate plan,start0/do44,once-only root factory,ground/live/deleted target and live-rank contexts,free NPC resources,ten cancellations and failed range,original saves;approved no-extra NPC cooldown,world release/recovery and natural Warrior AI for both skills pass;learned Shadow admission remains separate.");
    }
}
