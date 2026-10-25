// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class HostileFireboltChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HostileFireboltChecks));
    private sealed record Saved(uint Receiver,uint Caster,uint Source,Point3D Origin,WeaponFlight Flight,SlowMissilesState State,int Health,int Mana);
    private static PlayerMobile _receiver,_caster;
    private static GraveyardLich _source;
    private static Point3D _origin;
    private static Func<int,int> _oldDamage;
    private static bool _creating;
    private static int _phase,_before;
    private static long _deadline,_first;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"hostile-firebolt-fixtures.json");
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name=="HostileFireboltReceiver"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="HostileFireboltReceiver"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Freeze();Register("hostile-firebolt-check",Check);Register("hostile-firebolt-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Shared incoming firebolt flight, snapshots, defense and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Cleanup();Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Cleanup()
    {
        if(_oldDamage!=null){WeaponMissiles.DamageRoll=_oldDamage;_oldDamage=null;}
        if(_source!=null){_source.Frozen=false;_source.StopCombatActions();}
    }
    private static PlayerMobile Fresh(DiabloClass cls)
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="HostileFireboltReceiver",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,cls);ClassCreation.Profiles.AdvanceLevel(owner,99);owner.Mana=0;return owner;
    }
    private static void Place()
    {
        var map=Map.Trammel;bool loggedTown=false;
        for(int x=2200;x<2900;x+=4)for(int y=1000;y<1450;y+=4)
        {
            int z=map.GetAverageZ(x,y);var p=new Point3D(x,y,z);
            if(!Enumerable.Range(0,19).All(i=>map.GetAverageZ(x+i,y)==z && map.CanSpawnMobile(x+i,y,z))
                || !map.LineOfSight(new Point3D(x,y,z+12),new Point3D(x+18,y,z+12))){continue;}
            bool occupied=false;foreach(var other in map.GetMobilesInRange<Mobile>(p,22)){if(other!=_receiver){occupied=true;break;}}
            if(occupied){continue;}_receiver.MoveToWorld(p,map);
            if(Enumerable.Range(0,19).Any(i=>Server.Spells.SpellHelper.IsTown(new Point3D(x+i,y,z),_receiver)))
            {if(!loggedTown){Log.Information("Skipped guarded firebolt corridor: {Point}, sourceTown={Town}",p,Server.Spells.SpellHelper.IsTown(p,_receiver));loggedTown=true;}continue;}
            _origin=p;Log.Information("Hostile firebolt fixture corridor: {Point}",p);return;
        }
        throw new InvalidOperationException("No independent hostile-firebolt corridor");
    }
    private static void Reset(int distance=4)
    {
        WeaponMissiles.Cancel(_source);AmazonSlowMissiles.Clear(_source);_source.Frozen=false;_source.CantWalk=true;_source.StopCombatActions();_source.Combatant=null;_source.AIObject.AITimer.Stop();
        _source.MoveToWorld(_origin,Map.Trammel);_receiver.MoveToWorld(new(_origin.X+distance,_origin.Y,_origin.Z),Map.Trammel);
        SkillHealth.SetBalance256(_receiver,_receiver.HitsMax*256);_receiver.Mana=0;
    }
    private static void Slow()=>Require(AmazonSlowMissiles.Apply(_caster,_source,20,TimedEffects.Now),"Internal source slow state");
    private static long Launch()=>WeaponMissiles.LaunchHostileFirebolt(_source,_receiver.Location,1536,2560);
    private static void Step(long id,int frames)
    {for(int i=0;i<frames;i++){var state=WeaponMissiles.State(id);if(state==null){return;}WeaponMissiles.Tick(state.NextUtc);}}
    private static void Finish(long id){Step(id,250);Require(WeaponMissiles.State(id)==null,"Incoming missile outlived its bound");}
    private static void Train(PlayerMobile owner,string id)
    {
        if(ClassCreation.Profiles.GetRank(owner.Serial,id)>0){return;}
        Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var talent),"Talent "+id);
        foreach(var prerequisite in talent.Prerequisites){Train(owner,prerequisite);}
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);
    }
    private static void AvoidanceOrder()
    {
        var original=_receiver;var damage=WeaponMissiles.DamageRoll;var dodge=AmazonAvoidance.Roll;
        var amazon=Fresh(DiabloClass.Amazon);Train(amazon,"amazon.avoid");original.Internalize();_receiver=amazon;
        try
        {
            Reset();int rolls=0,avoids=0,before=SkillHealth.Balance256(amazon);
            WeaponMissiles.DamageRoll=span=>{Require(span==1024,"Firebolt damage span");rolls++;return 0;};
            AmazonAvoidance.Roll=max=>{Require(max==100 && rolls==1,"Avoidance happened before native damage draw");avoids++;return 0;};
            long id=Launch();Finish(id);
            Require(rolls==1 && avoids==1 && SkillHealth.Balance256(amazon)==before,"Incoming avoidance draw/order/damage");
        }
        finally{WeaponMissiles.DamageRoll=damage;AmazonAvoidance.Roll=dodge;_receiver=original;amazon.Delete();Reset();}
    }
    private static void Cases()
    {
        var old=WeaponMissiles.DamageRoll;var hit=WeaponMissiles.HitRoll;
        try
        {
            WeaponMissiles.DamageRoll=_=>0;WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Pure incoming spell requested weapon AR/shield check");
            Reset();int before=SkillHealth.Balance256(_receiver);long id=Launch();var f=WeaponMissiles.State(id);
            Require(f is{HostileSpell:true,Missile:"firebolt",Frames:50} && f.Slow==SlowMissilePayload.Normal && f.Vx==SkillGeometry.Velocity(20) && f.Vy==0
                && f.Damage.Elements.FireMin==1536 && SkillHealth.Balance256(_receiver)==before,"Pinned normal firebolt factory/no immediate damage: id="+id+",source="+_source.Location+",receiver="+_receiver.Location+",town="+Server.Spells.SpellHelper.IsTown(_source.Location,_source)+",flight="+JsonSerializer.Serialize(f)+",health="+SkillHealth.Balance256(_receiver)+",before="+before);
            Step(id,4);Require(SkillHealth.Balance256(_receiver)==before,"Firebolt hit without reaching receiver");Finish(id);
            Require(SkillHealth.Balance256(_receiver)==before-1536,"Pure spell FP256 impact");
            Reset();Slow();before=SkillHealth.Balance256(_receiver);id=Launch();f=WeaponMissiles.State(id);
            int velocity=SlowMissilePayload.VelocityFp8(20,0,1,0,0,true,33);
            Require(f.Slow==new SlowMissilePayload(33,67) && f.Vx==MissileVectors.VelocityFp8(4,0,velocity).X && f.Damage.Elements.FireMin==1029,"Captured slow vector/typed endpoints");
            AmazonSlowMissiles.Clear(_source);Step(id,7);Require(SkillHealth.Balance256(_receiver)==before && WeaponMissiles.State(id)!=null,"Later clear changed slow flight");Finish(id);
            Require(SkillHealth.Balance256(_receiver)==before-1029,"Captured slow FP256 damage changed after clear");
            Reset();before=SkillHealth.Balance256(_receiver);id=Launch();Slow();Finish(id);Require(SkillHealth.Balance256(_receiver)==before-1536,"Later cast changed existing ordinary missile");
            Reset(10);before=SkillHealth.Balance256(_receiver);id=Launch();Finish(id);Require(SkillHealth.Balance256(_receiver)==before-1536,"Normal range control");
            Reset(10);Slow();before=SkillHealth.Balance256(_receiver);id=Launch();Finish(id);Require(SkillHealth.Balance256(_receiver)==before,"Slow extended fixed lifetime to recover range");
            Reset();before=SkillHealth.Balance256(_receiver);id=Launch();Step(id,2);_receiver.MoveToWorld(new(_receiver.X,_receiver.Y+2,_receiver.Z),_receiver.Map);Finish(id);
            Require(SkillHealth.Balance256(_receiver)==before,"Firebolt became homing after release");
            Reset();var wall=new Static(0x80);try
            {
                before=SkillHealth.Balance256(_receiver);id=Launch();wall.MoveToWorld(new(_origin.X+2,_origin.Y,_origin.Z),Map.Trammel);Finish(id);
                Require(SkillHealth.Balance256(_receiver)==before,"Travelling firebolt crossed new solid terrain");
            }
            finally{wall.Delete();}
            Reset();var necro=Fresh(DiabloClass.Necromancer);necro.MoveToWorld(new(_origin.X,_origin.Y+3,_origin.Z),Map.Trammel);
            var values=new SkillValues(SkillMechanics.For(CombatAbility.BoneWall),_=>1,_=>0,99);
            var root=BoneBarrierSegment.Spawn(necro,CombatAbility.BoneWall,new(_origin.X+2,_origin.Y+2,_origin.Z),values,null);root.AddTile(new(_origin.X+2,_origin.Y,_origin.Z));
            try
            {
                int barrier=SkillHealth.Balance256(root);before=SkillHealth.Balance256(_receiver);id=Launch();Finish(id);
                Require(SkillHealth.Balance256(_receiver)==before && SkillHealth.Balance256(root)<barrier,"Shared collision missed off-anchor barrier connector");
            }
            finally{root.Delete();necro.Delete();}
            Reset();var legacy=new WeaponFlight(999,999,_receiver.Serial.Value,Map.Trammel.MapID,CombatAbility.MultipleShot,1,2,0,.5,0,0,40,0,0,0,0,2080,new(256,256,0,0,0,false,new()),TimedEffects.Now);
            var json=JsonNode.Parse(JsonSerializer.Serialize(legacy)).AsObject();json.Remove("HostileSpell");json.Remove("Missile");json.Remove("Slow");
            Require(JsonSerializer.Deserialize<WeaponFlight>(json.ToJsonString())==legacy,"Old version0 player-flight JSON lost default role/payload");
            AvoidanceOrder();
        }
        finally{WeaponMissiles.DamageRoll=old;WeaponMissiles.HitRoll=hit;Reset();}
    }
    private static void Later(Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>
    {try{action();}catch(Exception e){Cleanup();Log.Error(e,"hostile-firebolt-result: failed");}});
    private static WeaponFlight[] Flights()=>WeaponMissiles.Snapshot().Flights.Where(x=>x.HostileSpell && x.Owner==_source.Serial.Value).ToArray();
    private static void Natural()
    {_oldDamage=WeaponMissiles.DamageRoll;WeaponMissiles.DamageRoll=_=>0;_phase=0;Begin();}
    private static void Begin()
    {
        Reset(_phase==0?10:4);Slow();_before=SkillHealth.Balance256(_receiver);_deadline=Core.TickCount+6000;
        Require(_source.BeginFirebolt(_receiver),"Actual native firebolt windup");Require(Flights().Length==0,"Windup emitted before release");Later(ObserveLaunch);
    }
    private static void ObserveLaunch()
    {
        var flights=Flights();if(flights.Length==0 && Core.TickCount<_deadline){Later(ObserveLaunch);return;}
        Require(flights.Length==1 && flights[0].Slow==new SlowMissilePayload(33,67) && flights[0].Damage.Elements.FireMin==1029,"Actual release did not use shared saved flight");
        _first=flights[0].Id;
        if(_phase==0){Later(ObserveCadence);return;}
        AmazonSlowMissiles.Clear(_source);_source.Frozen=true;_source.StopCombatActions();Later(ObserveImpact);
    }
    private static void ObserveCadence()
    {
        if(Core.TickCount<_source.NextMeleeDecision && Core.TickCount<_deadline){Later(ObserveCadence);return;}
        Require(WeaponMissiles.State(_first)!=null && _source.BeginFirebolt(_receiver),"Outstanding slow missile delayed the next native cast cycle");Later(ObserveSecond);
    }
    private static void ObserveSecond()
    {
        var flights=Flights();if(flights.Length<2 && Core.TickCount<_deadline){Later(ObserveSecond);return;}
        Require(flights.Length==2 && WeaponMissiles.State(_first)!=null && SkillHealth.Balance256(_receiver)==_before,"Independent second windup/overlapping slow flights");
        _phase=1;Begin();
    }
    private static void ObserveImpact()
    {
        if(WeaponMissiles.State(_first)!=null && Core.TickCount<_deadline){Later(ObserveImpact);return;}
        Require(WeaponMissiles.State(_first)==null && SkillHealth.Balance256(_receiver)==_before-1029
            && NecromancerEffects.ManaBalance256(_receiver)==0,"Already-released missile did not survive source freeze/interrupt/clear");
        Cleanup();Persist();Log.Information("hostile-firebolt-result: passed");
    }
    private static void Persist()
    {
        Reset();Slow();long id=Launch();Step(id,3);var flight=WeaponMissiles.State(id);Require(flight!=null && flight.Frame==3,"Saved moving incoming phase");
        var state=AmazonSlowMissiles.State(_source) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Where(x=>x.Target!=_source.Serial.Value).Append(state).ToArray());
        var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Id==id?x with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}:x).ToArray()});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(_receiver.Serial.Value,_caster.Serial.Value,_source.Serial.Value,_origin,WeaponMissiles.State(id),state,SkillHealth.Balance256(_receiver),NecromancerEffects.ManaBalance256(_receiver))));
    }
    private static void Check()
    {_receiver=Fresh(DiabloClass.Barbarian);_caster=Fresh(DiabloClass.Amazon);Place();_source=new GraveyardLich{CantWalk=true,NoKillAwards=true};_source.AIObject.AITimer.Stop();Cases();Natural();}
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));_receiver=World.FindMobile((Serial)saved.Receiver) as PlayerMobile;_caster=World.FindMobile((Serial)saved.Caster) as PlayerMobile;_source=World.FindMobile((Serial)saved.Source) as GraveyardLich;_origin=saved.Origin;
        Require(_receiver!=null && _caster!=null && _source!=null && WeaponMissiles.State(saved.Flight.Id)==saved.Flight && AmazonSlowMissiles.State(_source)==saved.State,"Saved incoming role/payload/vector/phase/expiry/source");
        _source.AIObject.AITimer.Stop();_receiver.MoveToWorld(new(_origin.X+4,_origin.Y,_origin.Z),Map.Trammel);
        Require(SkillHealth.Balance256(_receiver)==saved.Health && NecromancerEffects.ManaBalance256(_receiver)==saved.Mana,"Offline incoming damage/resource replay");
        var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Id==saved.Flight.Id?x with{NextUtc=TimedEffects.Now}:x).ToArray()});
        var old=WeaponMissiles.DamageRoll;try{WeaponMissiles.DamageRoll=_=>0;AmazonSlowMissiles.Clear(_source);Finish(saved.Flight.Id);Require(SkillHealth.Balance256(_receiver)==saved.Health-1029,"Restored captured incoming impact");}finally{WeaponMissiles.DamageRoll=old;}
        Cases();Natural();
    }
}
