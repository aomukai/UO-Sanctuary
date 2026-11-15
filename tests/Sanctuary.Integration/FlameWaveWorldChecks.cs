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

public static class FlameWaveWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(FlameWaveWorldChecks));
    private sealed record Saved(int Generation,uint Owner,uint Helm,uint Weapon,uint Target,int TargetLife,Point3D Origin,int Mana,int Life,FlameWaveSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"flame-wave-world-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class FireImmune:EncounterCreature
    {
        [Constructible]public FireImmune(){}
        public FireImmune(Serial serial):base(serial){}
        protected override int[] AdjustResistances(int[] resistance)=>[0,0,100,0,0,0];
        public override bool CanRegenHits=>false;
        public override void OnThink(){}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name?.StartsWith("FlameWave",StringComparison.Ordinal)==true)&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name?.StartsWith("FlameWave",StringComparison.Ordinal)==true)&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"flame-wave-world-reload-check":"flame-wave-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared Flame Wave action,complete staged graph/fire and saved continuation",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static void Ready(PlayerMobile p)
    {
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        NecromancerEffects.SetMana256(p,p.ManaMax*256);
        var save=FireGround.Snapshot();FireGround.Restore(save with{Cooldowns=save.Cooldowns.Where(c=>c.Owner!=p.Serial.Value||c.Ability!=CombatAbility.FlameWave).ToArray()});
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int x,int y=0)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="FlameWaveTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(r,0);}return t;
    }
    private static FlameWaveAdvance Pump(PlayerMobile p,DateTime at,bool actions=true)
    {var r=FlameWaveWorld.TickPrepared(at,actions,p.Serial.Value);Require(r.Fault==null,"Wave fault: "+r.Fault);return r;}
    private static long Release(PlayerMobile p,DateTime at)
    {Ready(p);long id=FlameWaveWorld.ReleasePrepared(p,new(p.X+10,p.Y,p.Z),at);Require(id>0,"Prepared wave release");return id;}
    private static void Scene(NetState network)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Name="FlameWaveFixture";p.Female=true;p.Body=0x191;p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.flamewave");FixtureSpace.Corridor(p,6500,60,3400);
        var weapon=new Dagger();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Wave dagger");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(i=>i.SourceKey=="Tarnhelm").Id,99,new Random(719),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Wave original soft-rank item");ClassGear.Refresh(p);
        var account=FixtureNetwork.Authenticate(network,p,"flame-world-");var target=Target(p,4);bool kept=false;
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var action=FlameWaveCasting.BeginPrepared(p,Serial.Zero,new(p.X+10,p.Y,p.Z),Core.TickCount);var session=ClassCombat.For(p);
            Require(action!=null&&mana==NecromancerEffects.ManaBalance256(p)&&session.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"SC/FCR entry paid or wrong clock");
            Require(!ClassCombat.ResolveAction(p,session.EffectAt-1)&&FlameWaveWorld.For(p).Length==0,"Early release");ClassCombat.Stop(p);
            Require(action.Cancelled&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled windup paid");
            Ready(p);action=FlameWaveCasting.BeginPrepared(p,Serial.Zero,new(p.X+10,p.Y,p.Z),Core.TickCount);NecromancerEffects.SetMana256(p,1);
            Require(!ClassCombat.ResolveAction(p,session.EffectAt)&&action.Released&&FlameWaveWorld.For(p).Length==0,"Late insufficient mana allocated");
            Ready(p);action=FlameWaveCasting.BeginPrepared(p,Serial.Zero,new(p.X+10,p.Y,p.Z),Core.TickCount);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            Require(!ClassCombat.ResolveAction(p,session.EffectAt)&&action.Cancelled,"Movement did not interrupt windup");p.MoveToWorld(origin,p.Map);
            Ready(p);action=FlameWaveCasting.BeginPrepared(p,Serial.Zero,new(p.X+10,p.Y,p.Z),Core.TickCount);p.AddToBackpack(weapon);
            Require(!ClassCombat.ResolveAction(p,session.EffectAt)&&action.Cancelled&&p.EquipItem(weapon),"Weapon change did not interrupt windup");
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);action=FlameWaveCasting.BeginPrepared(p,Serial.Zero,new(p.X+10,p.Y,p.Z),Core.TickCount);
            Require(ClassCombat.ResolveAction(p,session.EffectAt)&&action.Wave>0&&action.ManaPaid256==1920&&NecromancerEffects.ManaBalance256(p)==mana-1920&&!ClassCombat.ResolveAction(p,session.EffectAt),"One fractional release/payment");
            var wave=FlameWaveWorld.For(p).Single();var at=wave.CreatedUtc;int hp=SkillHealth.Balance256(target),wear=weapon.HitPoints;
            Require(!FireGround.Ready(p,CombatAbility.FlameWave,at.AddMilliseconds(599))&&FireGround.Ready(p,CombatAbility.FlameWave,at.AddMilliseconds(600)),"15-frame local cooldown");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Pump(p,at.AddMilliseconds(640));Require(FlameWaveWorld.For(p).Single()is{Frame:16,Lanes.Length:0,Fires.Length:0}&&SkillHealth.Balance256(target)==hp,"Unveiling dealt damage or launched early");
            var r=Pump(p,at.AddMilliseconds(680));var launched=FlameWaveWorld.For(p).Single();
            Require(launched.Lanes.Length==9&&launched.Fires.Any(f=>f.Maker==695)&&launched.Fires.Any(f=>f.Maker==716)&&r.Burns==0,"17-frame lane launch/paired makers/first burn ordering");
            r=Pump(p,at.AddMilliseconds(1000));Require(r.HeadContacts>0&&r.Burns>0&&SkillHealth.Balance256(target)<hp&&FlameWaveWorld.For(p).Single().Birth==wave.Birth&&weapon.HitPoints==wear,"Head and ground damage,immutable birth or wear");
            Require(FlameWaveWorld.Snapshot().Leases.Any(l=>l.Cast==wave.Id&&l.Target==target.Serial.Value),"Shared 15-frame head lease missing");
            Overlap(p,target);Lifecycle(p,target);Walls(p,target);Immunity(p,target);
            if(weapon.Parent!=p){p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Restore original dagger after death");}
            p.AddToBackpack(helm);Require(p.EquipItem(helm),"Restore original soft-rank helm");ClassGear.Refresh(p);FlameWaveWorld.Clear(p);at=TimedEffects.Now.Add(FixtureSpace.Retention);Release(p,at);Pump(p,at.AddMilliseconds(1000));
            var state=FlameWaveWorld.Snapshot();Require(FlameWaveWorld.For(p).Single().Lanes.Length==9&&FlameWaveWorld.For(p).Single().Fires.Length>0,"Saved moving/front/ground graph");
            string before=JsonSerializer.Serialize(state);bool refused=false;
            try{FlameWaveWorld.Restore(state with{Casts=state.Casts.Select(n=>n.Owner==p.Serial.Value?n with{Seed=0}:n).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(FlameWaveWorld.Snapshot())==before,"Malformed graph partially committed");
            refused=false;try{FlameWaveWorld.Restore(state with{NextId=0});}catch(InvalidDataException){refused=true;}Require(refused&&JsonSerializer.Serialize(FlameWaveWorld.Snapshot())==before,"ID rollback partly committed");
            FlameWaveWorld.Restore(JsonSerializer.Deserialize<FlameWaveSave>(before));
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,helm.Serial.Value,weapon.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),state)));kept=true;
            Require(WarlockCombat.Implemented(CombatAbility.FlameWave)&&FlameWaveWorld.Approved,"Approved Flame Wave admission missing");
            Log.Information("Flame Wave world:SC/FCR interruption/late mana/one fractional payment,cooldown,708→690→695/716→240,head/fire damage,overlap,terrain,immunity,parking and atomic persistence pass;complete world rule approved");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){target.Delete();FlameWaveWorld.Clear(p);p.Delete();}}
    }
    private static void Overlap(PlayerMobile p,BaseCreature target)
    {
        // Compare identical RNG/clock against the complete overlapping graph
        // and a single surviving fire. No mirrored implementation arithmetic.
        var state=FlameWaveWorld.Snapshot();var n=FlameWaveWorld.For(p).Single();var fire=n.Fires.First(f=>CombatHitboxes.SourceTouches(2,2,(int)Math.Round(f.X*2)-target.X*2,(int)Math.Round(f.Y*2)-target.Y*2));
        var one=n with{Lanes=[],Fires=[fire]};var all=n with{Lanes=[]};int hp=SkillHealth.Balance256(target);
        FlameWaveWorld.Restore(state with{Casts=[one]});var r=Pump(p,n.NextUtc);int loss=hp-SkillHealth.Balance256(target);Require(r.Burns==1&&loss>0,"Single fire positive control");
        SkillHealth.SetBalance256(target,hp);FlameWaveWorld.Restore(state with{Casts=[all]});r=Pump(p,n.NextUtc);Require(r.Burns==1&&hp-SkillHealth.Balance256(target)==loss,"Overlapping tail/static/adjacent lanes multiplied damage");
        FlameWaveWorld.Clear(p);var at=TimedEffects.Now;Release(p,at);Release(p,at.AddMilliseconds(600));r=Pump(p,at.AddMilliseconds(1600));
        Require(FlameWaveWorld.For(p).Length==2&&r.Burns>1,"Independent paid casts lost burns");FlameWaveWorld.Clear(p);
    }
    private static void Lifecycle(PlayerMobile p,BaseCreature target)
    {
        var at=TimedEffects.Now;Release(p,at);Pump(p,at.AddMilliseconds(1000));var n=FlameWaveWorld.For(p).Single();int hp=SkillHealth.Balance256(target);var origin=p.Location;var map=p.Map;
        Pump(p,at.AddMilliseconds(1040),false);Require(SkillHealth.Balance256(target)==hp&&FlameWaveWorld.For(p).Single().Fires.All(f=>n.Fires.Any(old=>old==f)),"Offline damage/new field/renewed lifetime");
        p.Internalize();Pump(p,at.AddMilliseconds(1080));Require(SkillHealth.Balance256(target)==hp,"Facet departure kept fighting");p.MoveToWorld(origin,map);
        var destination=new Point3D(p.X+30,p.Y,p.Z);var town=new GuardedRegion("FlameTown"+p.Serial.Value,map,1000,new Rectangle2D(destination.X-2,destination.Y-2,5,5));town.Register();
        try{p.MoveToWorld(destination,map);Pump(p,at.AddMilliseconds(1120));Require(MinionSafeAreas.Contains(p)&&SkillHealth.Balance256(target)==hp,"Town kept remote wave fighting");}
        finally{p.MoveToWorld(origin,map);town.Unregister();}
        Pump(p,at.AddMilliseconds(1160));Require(SkillHealth.Balance256(target)<hp,"Remaining field failed to resume");
        Pump(p,at.AddSeconds(10),false);Require(FlameWaveWorld.For(p).Length==0,"Offline lifetime reset");
        Release(p,at);p.Kill();Require(FlameWaveWorld.For(p).Length==0,"Owner death retained wave");p.Resurrect();Ready(p);
    }
    private static void Walls(PlayerMobile p,BaseCreature target)
    {
        int id=Enumerable.Range(0x80,0x8000).First(i=>(TileData.ItemTable[i].Flags&(TileFlag.Wall|TileFlag.NoShoot))!=0&&TileData.ItemTable[i].Height>=16);
        var walls=Enumerable.Range(-4,9).Select(_=>new Static(id)).ToArray();var at=TimedEffects.Now;
        try
        {
            Release(p,at);for(int i=0;i<walls.Length;i++){walls[i].MoveToWorld(new(p.X+6,p.Y+i-4,p.Z),p.Map);}Pump(p,at.AddMilliseconds(1800));
            Require(FlameWaveWorld.For(p).Single().Lanes.Length==0&&FlameWaveWorld.For(p).Single().Fires.All(f=>f.X<p.X+6),"Wave crossed wall/left fields past obstruction");
        }
        finally{foreach(var wall in walls){wall.Delete();}FlameWaveWorld.Clear(p);}
    }
    private static void Immunity(PlayerMobile p,BaseCreature other)
    {
        other.Internalize();var t=new FireImmune{Name="FlameWaveImmune",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        try{Require(CreatureProfiles.For(t).Resistances.Fire==100,"Explicit fire immunity");var at=TimedEffects.Now;Release(p,at);int hp=SkillHealth.Balance256(t);var r=Pump(p,at.AddMilliseconds(1000));Require(r.HeadContacts>0&&r.Burns>0&&SkillHealth.Balance256(t)==hp,"Fire immune target took base fire");}
        finally{t.Delete();FlameWaveWorld.Clear(p);other.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"flame-world-reload-");
        try
        {
            var state=FlameWaveWorld.Snapshot();var target=(BaseCreature)World.FindMobile((Serial)saved.Target);target.AIObject?.AITimer.Stop();
            Require(state.NextId>=saved.State.NextId&&JsonSerializer.Serialize(FlameWaveWorld.For(p))==JsonSerializer.Serialize(saved.State.Casts.Where(n=>n.Owner==p.Serial.Value))
                &&p.Female&&World.FindItem((Serial)saved.Helm)?.Parent==p&&World.FindItem((Serial)saved.Weapon)?.Parent==p&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(target)==saved.TargetLife,"Saved graph/gear/FP256wounds/mana changed");
            FixtureSpace.Place(p,saved.Origin,Map.Trammel);var n=FlameWaveWorld.For(p).Single();string before=JsonSerializer.Serialize(state);Pump(p,n.NextUtc.AddTicks(-1));Require(JsonSerializer.Serialize(FlameWaveWorld.Snapshot())==before,"Reload advanced before deadline");
            FlameWaveWorld.Restore(state,true);var r=Pump(p,n.NextUtc.AddMilliseconds(80));Require(r.Updates==0&&r.Burns==0&&SkillHealth.Balance256(target)==saved.TargetLife&&FlameWaveWorld.For(p).Single().NextUtc==n.NextUtc.AddMilliseconds(120),"Overdue loaded graph replayed offline frames");
            FlameWaveWorld.Restore(state);r=Pump(p,n.NextUtc);Require(r.Burns==1&&SkillHealth.Balance256(target)<saved.TargetLife&&FlameWaveWorld.For(p).Single().Frame==n.Frame+1,"Original saved fire failed to resume");
            FlameWaveWorld.Restore(state);SkillHealth.SetBalance256(target,saved.TargetLife);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Require(ClassCombat.For(p).FlameWave==null&&WarlockCombat.Implemented(CombatAbility.FlameWave),"Reload lost approved input or retained transient");
            Log.Information("Flame Wave world:saved moving lanes,paired maker/240fields,leases,RNG,clocks,original gear/Witch/wounds and mana retain through actual reload;next deadline resumes damage once");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
