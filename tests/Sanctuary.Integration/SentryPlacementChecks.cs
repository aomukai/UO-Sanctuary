// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;
namespace Sanctuary.Integration;

public static class SentryPlacementChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SentryPlacementChecks));
    private static readonly int[] Skills=[261,262,271,272,276];
    private sealed record Trap(uint Actor,SentryBodyState State,int Life);
    private sealed record Saved(uint Owner,Point3D Origin,int Mana,Trap[] Traps,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-placement-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static bool Near(Point3D a,Point3D b,int range)=>Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y))<=range;
    public static void Configure(){Register("sentry-placement-check",false);Register("sentry-placement-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Approved shared bounded sentry placement and actual player/Shadow inputs",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    internal static Static[] Block(Map map,Point3D point,int radius=4)
    {
        var walls=new List<Static>();for(int x=-radius;x<=radius;x++)for(int y=-radius;y<=radius;y++)
        {var wall=new Static(0x6);wall.MoveToWorld(new(point.X+x,point.Y+y,point.Z),map);walls.Add(wall);}return walls.ToArray();
    }
    private static void Clear(PlayerMobile p)
    {foreach(var entry in AssassinTrapRoster.For(p)){World.FindMobile((Serial)entry.Actor)?.Delete();}AssassinTrapRoster.Prune();}
    private static AssassinSentryBody Last(PlayerMobile p)=>(AssassinSentryBody)World.FindMobile((Serial)AssassinTrapRoster.For(p).Last().Actor);
    private static BaseCreature Enemy(PlayerMobile p)
    {var t=new GraveyardZombie{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(65000);t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;}
    private static void Ordering()
    {
        var center=new Point3D(30,40,7);var seen=new List<Point3D>();
        Require(!AssassinSentryPlacement.Resolve(center,p=>{seen.Add(p);return false;},out var failed)&&failed==center&&seen.Count==81&&seen.Distinct().Count()==81,"Placement escaped bounded unique81 cells or replaced failed point");
        Point2D[] first=[new(0,0),new(-1,0),new(0,-1),new(0,1),new(1,0),new(-1,-1),new(-1,1),new(1,-1),new(1,1),new(-2,0),new(0,-2),new(0,2),new(2,0)];
        Require(seen.Take(first.Length).Select(p=>new Point2D(p.X-center.X,p.Y-center.Y)).SequenceEqual(first)
            &&seen.All(p=>p.Z==7&&Math.Max(Math.Abs(p.X-30),Math.Abs(p.Y-40))<=4),"Placement band/tie order or retained Z differs");
        Require(AssassinSentryPlacement.Resolve(center,p=>p==new Point3D(31,40,7)||p==new Point3D(30,41,7),out var tie)&&tie==new Point3D(30,41,7),"Equal-distance coordinate tie differs");
        Require(!AssassinSentryPlacement.Resolve(center,p=>p==new Point3D(35,40,7),out _),"Placement searched beyond four UO tiles");
    }
    private static void Bounds(PlayerMobile p)
    {
        var point=new Point3D(p.X+6,p.Y,p.Z);var ring=Block(p.Map,point,1);
        try{Require(AssassinSentryCasts.Destination(p,Serial.Zero,point,out var farther)&&!Near(farther,point,1)&&Near(farther,point,4),"Placement did not leave fully occupied inner band");}
        finally{foreach(var wall in ring){wall.Delete();}}
        var town=new GuardedRegion("SentryPlacementTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(point.X-1,point.Y-1,3,3));town.Register();
        try{Require(AssassinSentryCasts.Destination(p,Serial.Zero,point,out var edge)&&!SpellHelper.IsTown(edge,p)&&Near(edge,point,4),"Candidate bypassed town edge or did not find legal outside tile");}
        finally{town.Unregister();}
        town=new GuardedRegion("SentryPlacementCasterTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(p.X-1,p.Y-1,3,3));town.Register();
        try{Require(!AssassinSentryCasts.Destination(p,Serial.Zero,point,out _),"Fallback bypassed caster town");}finally{town.Unregister();}
        var wallLine=new List<Static>();for(int y=-8;y<=8;y++){var wall=new Static(0x6);wall.MoveToWorld(new(p.X+3,p.Y+y,p.Z),p.Map);wallLine.Add(wall);}
        try
        {
            Require(!p.InLOS(point),"LOS wall fixture did not block request");
            Require(!AssassinSentryCasts.Destination(p,Serial.Zero,point,out var visible)||p.InLOS(visible)&&visible.X<p.X+3,"Fallback crossed LOS wall");
        }
        finally{foreach(var wall in wallLine){wall.Delete();}}
    }
    private static AssassinSentryBody PlayerCast(PlayerMobile p,int skill,BaseCreature target)
    {
        var ability=ShadowSentries.Ability(skill);Clear(p);SentryLearnedChecks.Ready(p);int cost=SkillMechanics.Values(p,ability).Mana256;NecromancerEffects.SetMana256(p,cost+127);
        int draws=0;var old=BladeSentinelSummons.SeedLow;BladeSentinelSummons.SeedLow=()=>{draws++;return 19;};
        try
        {
            Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount),"Occupied player sentry startup refused: "+skill);var s=ClassCombat.For(p);var cached=s.PendingPoint;
            Require(draws==0&&NecromancerEffects.ManaBalance256(p)==cost+127&&AssassinTrapRoster.For(p).Length==0,"Player placement allocated/paid during windup");
            target.MoveToWorld(new(p.X+9,p.Y,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt),"Player sentry release failed");
            var trap=Last(p);Require(trap.Location!=target.Location&&trap.Location!=cached&&trap.InRange(target,4)&&draws==1
                &&NecromancerEffects.ManaBalance256(p)==127&&trap.Prepared.Creation.Ability==ability,"Player release did not use live occupied target/pay once");
            Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&draws==1&&NecromancerEffects.ManaBalance256(p)==127,"Duplicate player input allocated or paid twice");return trap;
        }
        finally{BladeSentinelSummons.SeedLow=old;}
    }
    private static void BlockedRelease(PlayerMobile p,BaseCreature target)
    {
        foreach(int skill in Skills)
        {
            var ability=ShadowSentries.Ability(skill);SentryLearnedChecks.Ready(p);target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);int mana=SkillMechanics.Values(p,ability).Mana256+127;
            NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount),"Failed placement windup refused too early");var s=ClassCombat.For(p);var walls=Block(p.Map,target.Location);
            int seeds=0;var old=BladeSentinelSummons.SeedLow;BladeSentinelSummons.SeedLow=()=>{seeds++;return 19;};var order=AssassinTrapRoster.For(p);
            try{ClassCombat.ResolveAction(p,s.EffectAt);Require(seeds==0&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinTrapRoster.For(p).SequenceEqual(order),"Fully blocked release paid,allocated or replaced a trap");}
            finally{BladeSentinelSummons.SeedLow=old;foreach(var wall in walls){wall.Delete();}ClassCombat.Stop(p);}
        }
    }
    private static void Shadow(PlayerMobile p,AssassinShadowBody b,BaseCreature target)
    {
        b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);var at=TimedEffects.Now.AddMinutes(20);
        foreach(int skill in Skills)
        {
            Clear(p);target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);var seed=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();ulong expected=world.Seed;SourceRandom.Roll(ref expected);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ShadowActions.BeginSentry(b,target,target.Location,at,skill),"Occupied Shadow startup refused: "+skill);var action=ShadowActions.State(b);int release=action.Plan.Events.Single(e=>e.Type==1).Frame;
            target.MoveToWorld(new(p.X+9,p.Y,p.Z),p.Map);ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);var trap=Last(p);
            Require(trap.InRange(target,4)&&trap.Location!=target.Location&&trap.Prepared.Creation.Ability==ShadowSentries.Ability(skill)
                &&NpcWorldAllocation.Capture().Seed==expected&&NpcEquipmentRandom.Require(b)==seed&&NecromancerEffects.ManaBalance256(p)==mana,"Shadow placement used wrong rank,seed,payment or destination");
            ShadowActions.Tick(at.AddSeconds(2),true,b.Serial.Value);Clear(p);at=at.AddSeconds(3);
        }
        target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);var line=new List<Static>();
        for(int y=-8;y<=8;y++){var wall=new Static(0x6);wall.MoveToWorld(new(p.X+3,p.Y+y,p.Z),p.Map);line.Add(wall);}
        try
        {
            Require(!b.InLOS(target)&&ShadowSentries.Destination(b,target,target.Location,out var visible)&&b.InLOS(visible)&&visible.X<p.X+3,"Shadow LOS fallback witness missing");
            Require(ShadowActions.BeginSentry(b,target,target.Location,at,261),"Shadow startup rechecked LOS to blocked request instead of resolved point");ShadowActions.Tick(at.AddSeconds(2),true,b.Serial.Value);
            Require(Last(p).X<p.X+3&&b.InLOS(Last(p)),"Shadow release crossed wall instead of revalidating candidate LOS");
        }
        finally{foreach(var wall in line){wall.Delete();}ShadowActions.Cancel(b);Clear(p);at=at.AddSeconds(3);}
        if(b.Prepared.Capture.Ability==CombatAbility.ShadowWarrior)
        {
            target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);ShadowGroundMotion.Activate(b,p,at);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(p,false,CombatAbility.ChargedBoltSentry),"Warrior right-sentry bindings refused");
            NpcEquipmentRandom.Set(b,SourceRandom.InitLow(1234));bool selected=false;
            for(int i=0;i<800&&AssassinTrapRoster.For(p).Length==0;i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Natural sentry AI fault: "+b.WorldGround.State.Controller.Fault);selected|=ShadowActions.State(b)?.Cast.Skill==261;}
            Require(selected&&AssassinTrapRoster.For(p).Length>0,"Natural Warrior AI failed occupied-target trap casting");ShadowActions.Cancel(b);Clear(p);
        }
        PersistentMinions.Park(b);
    }
    private static void Check(bool reload)
    {
        Ordering();PlayerMobile p;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
            Require(NecromancerEffects.ManaBalance256(p)==saved.Mana,$"Reload changed payment balance: {NecromancerEffects.ManaBalance256(p)}/{saved.Mana},whole={p.Mana},fraction={NecromancerEffects.Profiles.Find(p)?.ManaFraction},rate={Mobile.GetManaRegenRate(p)}");
            foreach(var h in saved.Traps){var trap=(AssassinSentryBody)World.FindMobile((Serial)h.Actor);Require(Json(trap.Prepared)==Json(h.State)&&SkillHealth.Balance256(trap)==h.Life,"Reload moved,recast,rerolled or healed bounded-placement trap");}
            p.MoveToWorld(saved.Origin,Map.Trammel);Clear(p);
        }
        else{p=SentryLearnedChecks.Fresh(6380);p.Name="SentryCastFixturePlacement";foreach(int id in Skills){ClassSkillChecks.Train(p,SkillMechanics.All.Single(s=>s.SourceId==id).TalentId);}ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);}
        Bounds(p);var target=Enemy(p);var world=NpcWorldAllocation.Capture();var bodies=new List<AssassinShadowBody>();
        try
        {
            foreach(int skill in Skills){target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);_=PlayerCast(p,skill,target);}BlockedRelease(p,target);Clear(p);
            foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})
            {
                NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));var b=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);bodies.Add(b);
                b.ChangeSkills(book=>{foreach(int id in Skills){book.Assign(id,3);}});GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);Shadow(p,b,target);
            }
        }
        finally{target.Delete();foreach(var b in bodies){ShadowActions.Cancel(b);PersistentMinions.Park(b);b.Delete();}NpcWorldAllocation.Restore(world);Clear(p);}
        // Five real paid casts at occupied coordinates,retained with injuries.
        foreach(int skill in Skills)
        {
            var occupied=Enemy(p);try{ClassCombat.For(p).PendingPoint=occupied.Location;var ability=ShadowSentries.Ability(skill);NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,ability).Mana256+127);
                Require(AssassinSentryCasts.Release(p,occupied.Serial,TimedEffects.Now.AddDays(1),ability),"Saved occupied release refused");SkillHealth.SetBalance256(Last(p),12850);}finally{occupied.Delete();}
        }
        var traps=AssassinTrapRoster.For(p).Select(e=>{var b=(AssassinSentryBody)World.FindMobile((Serial)e.Actor);return new Trap(e.Actor,b.Prepared,SkillHealth.Balance256(b));}).ToArray();
        Require(Mobile.GetManaRegenRate(p)==TimeSpan.FromHours(1),"Placement fixture mana regeneration was not guarded");
        var origin=p.Location;int balance=NecromancerEffects.ManaBalance256(p);p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,origin,balance,traps,generation)));
        Log.Information("Approved sentry placement:81ordered retained-Z cells,inner/outer bounds,LOS/town,all five real player actions/live-target releases/once-only pay/free blocked failure,both own Shadows/world seed/no mana,natural Warrior AI and saved original paid traps through two reloads;exact D2R search order remains an explicit adaptation.");
    }
}
