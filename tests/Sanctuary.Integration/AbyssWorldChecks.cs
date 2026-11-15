// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class AbyssWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(AbyssWorldChecks));
    private sealed record Saved(int Generation,uint Owner,uint Helm,uint Weapon,uint Target,int TargetLife,Point3D Origin,int Mana,int Life,MiasmaWorldSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"abyss-world-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank,int bolt=0,int chain=0,int entropy=0)=>new(SkillMechanics.For(CombatAbility.Abyss),k=>k=="Abyss"?rank:k=="Enhanced Entropy"?entropy:0,
        k=>k=="Miasma Bolt"?bolt:k=="Miasma Chains"?chain:k=="Enhanced Entropy"?Math.Min(20,entropy):0,99);
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name?.StartsWith("Abyss",StringComparison.Ordinal)==true)&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name?.StartsWith("Abyss",StringComparison.Ordinal)==true)&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"abyss-world-reload-check":"abyss-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Complete prepared Abyss numeric/action/pull/pulse/terminal/save candidate",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static void Numbers()
    {
        var n=AbyssBirths.Numbers(Values(1));Require(n is{Minimum256:5120,Maximum256:10240,BurstMinimum256:2560,BurstMaximum256:3840,Mana256:5888,Radius:7,PeriodFrames:12,LifetimeFrames:50,BurstFrames:1,CooldownFrames:30},"Rank1 owned source golden");
        n=AbyssBirths.Numbers(Values(20,1,1));Require(n.Minimum256==150*256&&n.Maximum256==216*256&&n.BurstMinimum256==138*256&&n.BurstMaximum256==186*256&&n.Mana256==42*256,"Rank20 distinct center/terminal synergy goldens");
        foreach(var pair in new[]{new[]{0,7},new[]{4,7},new[]{5,8},new[]{10,9},new[]{15,10},new[]{20,11},new[]{255,11}})
        {Require(AbyssBirths.Numbers(Values(1,entropy:pair[0])).Radius==pair[1],"Captured effective Entropy radius threshold");}
        bool refused=false;try{AbyssBirths.Missile("abysscenter",Values(1)).Eval("Radius");}catch(Exception e)when(e is NotSupportedException or InvalidDataException){refused=true;}
        Require(refused,"General sksrc semantics silently aliased to skill");
    }
    private static void Ready(PlayerMobile p)
    {
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);
        var s=FireGround.Snapshot();FireGround.Restore(s with{Cooldowns=s.Cooldowns.Where(c=>c.Owner!=p.Serial.Value||c.Ability!=CombatAbility.Abyss).ToArray()});
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,Point3D point)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="AbyssTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;t.MoveToWorld(point,p.Map);t.AIObject?.AITimer.Stop();
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(r,0);}return t;
    }
    private static AbyssAdvance Pump(PlayerMobile p,DateTime at,bool actions=true)
    {var r=AbyssWorld.TickPrepared(at,actions,p.Serial.Value);Require(r.Fault==null,"Abyss fault: "+r.Fault);return r;}
    private static AbyssState Release(PlayerMobile p,Point3D point,DateTime at)
    {Ready(p);long id=AbyssWorld.ReleasePrepared(p,point,at);Require(id>0,"Prepared Abyss release");return AbyssWorld.For(p).Single(n=>n.Id==id);}
    private static void Scene(NetState network)
    {
        Numbers();var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Name="AbyssFixture";p.Female=true;p.Body=0x191;p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.abyss");ClassSkillChecks.Train(p,"warlock.enhancedentropy",4);FixtureSpace.Corridor(p,6500,60,3550);
        var w=new Dagger();p.AddToBackpack(w);Require(p.EquipItem(w),"Abyss original dagger");var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(i=>i.SourceKey=="Tarnhelm").Id,99,new Random(719),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Abyss original rank item");ClassGear.Refresh(p);
        var account=FixtureNetwork.Authenticate(network,p,"abyss-world-");var point=new Point3D(p.X+6,p.Y,p.Z);var t=Target(p,new(point.X+2,point.Y,point.Z));bool kept=false;
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var a=AbyssCasting.BeginPrepared(p,Serial.Zero,point,Core.TickCount);var s=ClassCombat.For(p);
            Require(a!=null&&mana==NecromancerEffects.ManaBalance256(p)&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Unpaid ordinary SC/FCR windup");ClassCombat.Stop(p);Require(a.Cancelled,"Cancel retained pending cast");
            Ready(p);a=AbyssCasting.BeginPrepared(p,Serial.Zero,point,Core.TickCount);NecromancerEffects.SetMana256(p,1);Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&a.Released&&AbyssWorld.For(p).Length==0,"Late mana paid/allocated");
            Ready(p);a=AbyssCasting.BeginPrepared(p,Serial.Zero,point,Core.TickCount);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&a.Cancelled,"Movement retained pending cast");p.MoveToWorld(origin,p.Map);
            Ready(p);a=AbyssCasting.BeginPrepared(p,Serial.Zero,point,Core.TickCount);p.AddToBackpack(w);Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&a.Cancelled&&p.EquipItem(w),"Equipment retained pending cast");
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);a=AbyssCasting.BeginPrepared(p,Serial.Zero,point,Core.TickCount);Require(ClassCombat.ResolveAction(p,s.EffectAt)&&a.Field>0&&a.ManaPaid256==24*256&&NecromancerEffects.ManaBalance256(p)==mana-24*256&&!ClassCombat.ResolveAction(p,s.EffectAt),"Once-only release/payment");
            var n=AbyssWorld.For(p).Single();var at=n.CreatedUtc;int hp=SkillHealth.Balance256(t);Require(n.Birth is{EffectiveEntropy:5,HardEntropy:4,Numbers.Radius:8}&&!FireGround.Ready(p,CombatAbility.Abyss,at.AddMilliseconds(1199))&&FireGround.Ready(p,CombatAbility.Abyss,at.AddMilliseconds(1200)),"Source cost/radius/30frame cooldown");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(AbyssBirths.Capture(p).Numbers.Radius==7,"Real +skill radius threshold control");
            Pump(p,at.AddMilliseconds(479));Require(SkillHealth.Balance256(t)==hp,"Early pulse");var r=Pump(p,at.AddMilliseconds(480));Require(r is{Pulses:1,Contacts:1,Pulled:0}&&SkillHealth.Balance256(t)<hp&&AbyssWorld.For(p).Single().Birth==n.Birth,"Actual pulse damage or immutable capture");
            Require(Pump(p,at.AddMilliseconds(480))==default,"Repeated pulse replay");foreach(int ms in new[]{960,1440,1920}){Require(Pump(p,at.AddMilliseconds(ms)).Contacts==1,"Twelve-frame pulse cadence");}
            hp=SkillHealth.Balance256(t);r=Pump(p,at.AddMilliseconds(2000));Require(r.Contacts==0&&AbyssWorld.For(p).Single()is{Terminal:true}&&SkillHealth.Balance256(t)==hp,"Parent expiry bypassed child stage");
            Pump(p,at.AddMilliseconds(2039));r=Pump(p,at.AddMilliseconds(2040));Require(r is{Terminals:1,Contacts:1,Pulled:0}&&AbyssWorld.For(p).Length==0&&SkillHealth.Balance256(t)<hp&&Pump(p,at.AddMilliseconds(2040))==default,"Terminal hit/lifetime/replay");
            PullAndTerrain(p,t,point);OverlapAndLifecycle(p,t,point);Immunity(p,t,point);
            if(w.Parent!=p){p.AddToBackpack(w);Require(p.EquipItem(w),"Restore dagger after owner death");}p.AddToBackpack(helm);Require(p.EquipItem(helm),"Restore soft-rank helm");ClassGear.Refresh(p);
            Save(p,t,point,w,helm);kept=true;Require(!AbyssWorld.Approved&&!WarlockCombat.Implemented(CombatAbility.Abyss),"Unapproved Abyss admitted learned input");
            Log.Information("Abyss world:numeric/isolated sksrc/real gear threshold,SC interrupt/late mana/one debit,ground launch,12frame pulses,pull/terrain,terminal,overlap,immunity,parking/death and atomic graph pass;learned gated");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){t.Delete();MiasmaWorld.Clear(p);p.Delete();}}
    }
    private static void PullAndTerrain(PlayerMobile p,BaseCreature t,Point3D point)
    {
        t.Frozen=t.CantWalk=false;t.MoveToWorld(new(point.X+3,point.Y,point.Z),p.Map);var at=TimedEffects.Now;Release(p,point,at);var r=Pump(p,at.AddMilliseconds(480));Require(r.Pulled==1&&t.X==point.X+2,"Actual forced step toward center");
        MiasmaWorld.Clear(p);t.MoveToWorld(new(point.X,point.Y,point.Z+8),p.Map);Release(p,point,at);r=Pump(p,at.AddMilliseconds(480));Require(r.Contacts==1&&r.Pulled==0&&t.X==point.X&&t.Y==point.Y,"Same horizontal center invented a pull direction across height");
        MiasmaWorld.Clear(p);t.Internalize();var elite=new FlameWaveWorldChecks.FireImmune{Name="AbyssElite",Frozen=false,CantWalk=false,NoKillAwards=true};elite.Assign(elite.Dungeon,60,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));elite.SetHits(30000);elite.Hits=elite.HitsMax;elite.MoveToWorld(new(point.X+2,point.Y,point.Z),p.Map);elite.AIObject?.AITimer.Stop();
        try{Require(NamedCombat.Elite(elite),"Explicit elite control");int hp=SkillHealth.Balance256(elite);Release(p,point,at);r=Pump(p,at.AddMilliseconds(480));Require(r.Contacts==1&&r.Pulled==0&&elite.X==point.X+2&&SkillHealth.Balance256(elite)<hp,"Elite pull immunity suppressed damage or failed movement exclusion");}
        finally{elite.Delete();MiasmaWorld.Clear(p);}
        t.Frozen=t.CantWalk=true;t.MoveToWorld(new(point.X+2,point.Y,point.Z),p.Map);Release(p,point,at);Require(Pump(p,at.AddMilliseconds(480)).Pulled==0,"Frozen target moved");MiasmaWorld.Clear(p);
        int wallId=Enumerable.Range(0x80,0x8000).First(i=>(TileData.ItemTable[i].Flags&(TileFlag.Wall|TileFlag.NoShoot))!=0&&TileData.ItemTable[i].Height>=16);var walls=Enumerable.Range(-2,5).Select(_=>new Static(wallId)).ToArray();
        try
        {
            var n=Release(p,point,at);for(int i=0;i<walls.Length;i++){walls[i].MoveToWorld(new(point.X+1,point.Y+i-2,point.Z),p.Map);}int hp=SkillHealth.Balance256(t);r=Pump(p,at.AddMilliseconds(480));Require(r.Contacts==0&&SkillHealth.Balance256(t)==hp,"Pulse crossed occluding wall");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);long next=MiasmaWorld.NextIdentity;Require(AbyssWorld.ReleasePrepared(p,walls[2].Location,at)==0&&MiasmaWorld.NextIdentity==next&&NecromancerEffects.ManaBalance256(p)==mana,"Blocked ground allocated/paid");
        }
        finally{foreach(var wall in walls){wall.Delete();}MiasmaWorld.Clear(p);}
    }
    private static void OverlapAndLifecycle(PlayerMobile p,BaseCreature t,Point3D point)
    {
        var at=TimedEffects.Now;Release(p,point,at);Release(p,point,at);Require(Pump(p,at.AddMilliseconds(480)).Contacts==2,"Separate paid fields shared hit window");MiasmaWorld.Clear(p);
        var n=Release(p,point,at);int hp=SkillHealth.Balance256(t);Pump(p,at.AddMilliseconds(480),false);Require(SkillHealth.Balance256(t)==hp&&AbyssWorld.For(p).Single().UntilUtc==n.UntilUtc,"Offline damage/renewed lifetime");
        var origin=p.Location;var map=p.Map;p.Internalize();Pump(p,at.AddMilliseconds(960));Require(SkillHealth.Balance256(t)==hp,"Cross facet damage");p.MoveToWorld(origin,map);
        var destination=new Point3D(p.X+30,p.Y,p.Z);var town=new GuardedRegion("AbyssTown"+p.Serial.Value,map,1000,new Rectangle2D(destination.X-2,destination.Y-2,5,5));town.Register();
        try{p.MoveToWorld(destination,map);Pump(p,at.AddMilliseconds(1440));Require(MinionSafeAreas.Contains(p)&&SkillHealth.Balance256(t)==hp,"Safe area retained attacks");}
        finally{p.MoveToWorld(origin,map);town.Unregister();}
        Require(Pump(p,at.AddMilliseconds(1920)).Contacts==1,"Remaining active lifetime failed to resume");Pump(p,at.AddMilliseconds(2000),false);Require(AbyssWorld.For(p).Length==0,"Offline terminal replay remained");
        n=Release(p,point,at);var r=Pump(p,at.AddMilliseconds(1000));Require(r.Pulses==1&&AbyssWorld.For(p).Single().NextUtc==at.AddMilliseconds(1440),"Late tick replayed historical pulses or drifted origin cadence");MiasmaWorld.Clear(p);
        Release(p,point,at);r=Pump(p,at.AddMilliseconds(2050));Require(r is{Terminals:1,Contacts:1}&&AbyssWorld.For(p).Length==0,"Late expiry lost valid one-frame child window");
        Release(p,point,at);hp=SkillHealth.Balance256(t);Pump(p,at.AddMilliseconds(2080));Require(AbyssWorld.For(p).Length==0&&SkillHealth.Balance256(t)==hp,"Expired terminal replayed");
        Release(p,point,at);p.Kill();Require(AbyssWorld.For(p).Length==0,"Actual owner death retained field");p.Resurrect();Ready(p);
    }
    private static void Immunity(PlayerMobile p,BaseCreature other,Point3D point)
    {
        other.Internalize();var t=new EldritchBlastWorldChecks.ImmuneRecipient{Name="AbyssImmune",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;t.MoveToWorld(new(point.X+2,point.Y,point.Z),p.Map);t.AIObject?.AITimer.Stop();
        try{Require(NamedElements.MagicResistance(t)==100,"Explicit magic immunity");var at=TimedEffects.Now;Release(p,point,at);int hp=SkillHealth.Balance256(t);Require(Pump(p,at.AddMilliseconds(480)).Contacts==1&&SkillHealth.Balance256(t)==hp,"Immune target took base magic");}
        finally{t.Delete();MiasmaWorld.Clear(p);other.MoveToWorld(new(point.X+2,point.Y,point.Z),p.Map);}
    }
    private static void Save(PlayerMobile p,BaseCreature t,Point3D point,Item w,Item helm)
    {
        var at=TimedEffects.Now.Add(FixtureSpace.Retention);var first=Release(p,point,at);Pump(p,at.AddMilliseconds(1920));Pump(p,at.AddMilliseconds(2000));Release(p,point,at.AddMilliseconds(1600));
        var state=MiasmaWorld.Snapshot();Require(AbyssWorld.For(p).Length==2&&AbyssWorld.For(p).Count(n=>n.Terminal)==1,"Saved simultaneous center/terminal graph");string before=JsonSerializer.Serialize(state);bool refused=false;
        try{MiasmaWorld.Restore(state with{Abysses=state.Abysses.Select((n,i)=>i==0?n with{Seed=0}:n).ToArray()});}catch(InvalidDataException){refused=true;}Require(refused&&JsonSerializer.Serialize(MiasmaWorld.Snapshot())==before,"Malformed Abyss partially committed shared graph");
        refused=false;try{MiasmaWorld.Restore(state with{Abysses=state.Abysses.Select(n=>n with{Id=state.Nodes[0].Id}).ToArray()});}catch(InvalidDataException){refused=true;}Require(refused&&JsonSerializer.Serialize(MiasmaWorld.Snapshot())==before,"Cross-pool Abyss ID collision committed");
        MiasmaWorld.Restore(JsonSerializer.Deserialize<MiasmaWorldSave>("{\"Nodes\":[],\"Leases\":[],\"NextId\":"+state.NextId+"}"));Require(AbyssWorld.Snapshot().Length==0,"Legacy root omitted Abyss but retained stale stages");MiasmaWorld.Restore(state);
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,helm.Serial.Value,w.Serial.Value,t.Serial.Value,SkillHealth.Balance256(t),p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),state)));
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"abyss-world-reload-");
        try
        {
            var state=MiasmaWorld.Snapshot();var t=(BaseCreature)World.FindMobile((Serial)saved.Target);t.AIObject?.AITimer.Stop();
            Require(JsonSerializer.Serialize(AbyssWorld.For(p))==JsonSerializer.Serialize(saved.State.Abysses.Where(n=>n.Owner==p.Serial.Value))&&state.NextId>=saved.State.NextId
                &&p.Female&&World.FindItem((Serial)saved.Helm)?.Parent==p&&World.FindItem((Serial)saved.Weapon)?.Parent==p&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(t)==saved.TargetLife,"Saved original stages/gear/Witch/FP256resources changed");
            FixtureSpace.Place(p,saved.Origin,Map.Trammel);var next=AbyssWorld.For(p).Min(n=>n.NextUtc);string before=JsonSerializer.Serialize(state);Pump(p,next.AddTicks(-1));Require(JsonSerializer.Serialize(MiasmaWorld.Snapshot())==before,"Reload replayed before deadline");
            var r=Pump(p,next);Require(r.Terminals==1&&r.Contacts==1&&SkillHealth.Balance256(t)<saved.TargetLife&&AbyssWorld.For(p)is[{Terminal:false}],"Saved one-frame terminal failed to resume once");
            MiasmaWorld.Restore(state,true);SkillHealth.SetBalance256(t,saved.TargetLife);r=Pump(p,next.AddMilliseconds(10));Require(r.Contacts==0&&AbyssWorld.For(p)is[{Terminal:false}]&&SkillHealth.Balance256(t)==saved.TargetLife,"Overdue loaded terminal replayed offline damage");
            MiasmaWorld.Restore(state);SkillHealth.SetBalance256(t,saved.TargetLife);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Require(ClassCombat.For(p).Abyss==null&&!WarlockCombat.Implemented(CombatAbility.Abyss),"Transient replay or unapproved learned admission");Log.Information("Abyss world:original simultaneous center/terminal,captured rank/radius/seed/clocks,gear/Witch/fractional wounds+mana retain through actual reload;terminal resumes once and overdue load skips");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
