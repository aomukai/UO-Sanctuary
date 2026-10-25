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

public static class SlowMissilesBoltChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SlowMissilesBoltChecks));
    private sealed record Saved(uint Owner,uint Caster,uint Source,Point3D Origin,SlowMissilesState State,int Mana);
    private static PlayerMobile _owner,_caster;
    private static EncounterCreature _source;
    private static Point3D _origin;
    private static int _phase,_before;
    private static long _started;
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"slow-missiles-bolt-fixtures.json");
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name=="SlowBoltOwner"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="SlowBoltOwner"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Freeze();Register("slow-missiles-bolts-check",Check);Register("slow-missiles-bolts-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Real encounter bolt capture/travel/damage, native curse pool and reload",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Cleanup();Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Cleanup(){if(_source!=null){EliteBolts.Cancel(_source);}}
    private static PlayerMobile Fresh(DiabloClass characterClass)
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="SlowBoltOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,characterClass);ClassCreation.Profiles.AdvanceLevel(owner,99);owner.Hits=owner.HitsMax;owner.Mana=0;return owner;
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        ClassCreation.Profiles.TryGet(owner.Serial,out var profile);
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank)
        {
            Require(TalentCatalog.TryGet(profile.Class,id,out var talent),"Talent "+id);
            foreach(var prerequisite in talent.Prerequisites){Train(owner,prerequisite);}
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);
        }
    }
    private static void Place()
    {
        var map=Map.Trammel;
        for(int x=2200;x<2900;x+=4)for(int y=1000;y<1450;y+=4)
        {
            int z=map.GetAverageZ(x,y);var point=new Point3D(x,y,z);
            if(!Enumerable.Range(0,19).All(i=>map.GetAverageZ(x+i,y)==z && map.CanSpawnMobile(x+i,y,z))
                || !map.LineOfSight(new Point3D(x,y,z+12),new Point3D(x+18,y,z+12))){continue;}
            bool occupied=false;foreach(var other in map.GetMobilesInRange<Mobile>(point,22)){occupied=true;break;}
            if(occupied){continue;}
            _owner.MoveToWorld(point,map);_origin=point;return;
        }
        throw new InvalidOperationException("No independent19-cell native bolt corridor");
    }
    private static void Reset(int distance=4)
    {
        EliteBolts.Cancel(_source);AmazonSlowMissiles.Clear(_source);NecromancerEffects.ClearOrdinaryCurse(_source);
        _source.MoveToWorld(new(_origin.X+distance,_origin.Y,_origin.Z),Map.Trammel);_source.CantWalk=true;_source.Frozen=false;_source.Combatant=null;_source.AIObject.AITimer.Stop();
        _owner.MoveToWorld(_origin,Map.Trammel);_owner.Hits=_owner.HitsMax;_owner.Mana=0;
        Require(EliteCombat.Enemy(_source,_owner,18),"Native source/receiver enemy guard");
    }
    private static void Slow(int rank)
    {
        Require(AmazonSlowMissiles.Apply(_caster,_source,rank,TimedEffects.Now),"Source slow state application");
    }
    private static void Ticks(int count){for(int i=0;i<count;i++){EliteBolts.Tick();}}
    private static int Lightning=>2*Math.Max(1,CreatureProfiles.Level(_source)/2)-1;
    private static int LightningLoss(int percent)=>(int)Math.Floor(NamedElements.Damage(_source,_owner,(long)Lightning*256*percent/100/256.0,"ltng"));
    private static void Projection()
    {
        foreach(string key in new[]{"arrow","firebolt","lightningbolt"}){Require(NecromancerSummons.Source(key,"CanSlow")==1,"Pinned mapped canSlow metadata");}
        var raw=new GearDamage(100,100,100,100,80,1600,4000,100);var scaled=EliteCombat.ScaleMissile(raw,new(33,67));
        Require(scaled.Physical==67 && scaled.Fire==67 && scaled.Cold==67 && scaled.Lightning==67 && scaled.Magic==67
            && scaled.Poison==343*40/256.0 && scaled.PoisonMs==1600 && scaled.ColdMs==4000,"Native typed-rate projection/unchanged durations");
        Require(EliteCombat.ScaleMissile(raw,SlowMissilePayload.Normal)==raw,"Normal native adapter changed original packet");
    }
    private static void Travel()
    {
        Reset();int before=_owner.Hits;EliteBolts.Lightning(_source);var normal=EliteBolts.Inspect(_source);
        Require(normal.Length==8 && normal.All(x=>x.Slow==SlowMissilePayload.Normal && x.DistancePerFrame==1),"Ordinary eight-ray launch baseline");
        Ticks(3);Require(_owner.Hits==before,"Ordinary projectile hit before travel");Ticks(1);Require(_owner.Hits==before-LightningLoss(100),"Ordinary native impact");
        Reset();Slow(1);before=_owner.Hits;EliteBolts.Lightning(_source);var initial=EliteBolts.Inspect(_source);
        double speed=SlowMissilePayload.VelocityFp8(0,0,0,20,256,true,68)/192.0;
        Require(initial.Length==8 && initial.All(x=>x.Slow==new SlowMissilePayload(68,94) && x.DistancePerFrame==speed),"Captured integer native slow speed/damage");
        AmazonSlowMissiles.Clear(_source);Ticks(4);Require(_owner.Hits==before && EliteBolts.Inspect(_source).Any(x=>x.Frame==4 && Math.Abs(x.Distance-4*speed)<1e-9 && x.Slow==new SlowMissilePayload(68,94)),"Clear after launch changed flight or early impact");
        Ticks(2);Require(_owner.Hits==before-LightningLoss(94),"Slowed real impact/captured damage");
        Reset();before=_owner.Hits;EliteBolts.Lightning(_source);Slow(20);Ticks(4);
        Require(_owner.Hits==before-LightningLoss(100),"Later cast changed an existing ordinary bolt");
        Reset(8);before=_owner.Hits;EliteBolts.Lightning(_source);Ticks(8);Require(_owner.Hits==before-LightningLoss(100),"Normal long-range control");
        Reset(8);Slow(20);before=_owner.Hits;EliteBolts.Lightning(_source);Ticks(19);
        Require(_owner.Hits==before && EliteBolts.Inspect(_source).Length==0,"Slowing extended lifetime/range instead of reducing travel distance");
        Reset();Slow(1);before=_owner.Hits;EliteBolts.Lightning(_source);Ticks(2);_owner.MoveToWorld(new(_origin.X,_origin.Y+2,_origin.Z),Map.Trammel);Ticks(17);
        Require(_owner.Hits==before && EliteBolts.Inspect(_source).Length==0,"Slowed projectile became homing");
        foreach(bool fire in new[]{false,true})
        {
            Reset();Slow(1);EliteBolts.Fan(_source,_owner,3,fire);var fan=EliteBolts.Inspect(_source);
            Require(fan.Length==3 && fan.All(x=>x.Slow==new SlowMissilePayload(68,94) && x.DistancePerFrame==speed),"Arrow/fire fan did not capture the same source state");
        }
        Reset();
    }
    private static void CursePool()
    {
        var curse=Fresh(DiabloClass.Necromancer);Train(curse,"necromancer.weaken");curse.MoveToWorld(new(_origin.X+1,_origin.Y+2,_origin.Z),Map.Trammel);
        try
        {
            Reset();Require(_owner.PhysicalResistance==0,"Known native physical resistance control");int before=_owner.Hits;
            EliteCombat.Hit(_source,_owner,100,ResistanceType.Physical,true,new Random(7));Require(_owner.Hits==before-100,"Native physical control");
            _owner.Hits=_owner.HitsMax;Require(NecromancerEffects.Apply(curse,_source,CombatAbility.Weaken,1),"Real Weaken application");_source.AIObject.AITimer.Stop();
            Require(NecromancerEffects.DamageStatPercent(_source)==-33,"Pinned Weaken1 damage stat");before=_owner.Hits;
            EliteCombat.Hit(_source,_owner,100,ResistanceType.Physical,true,new Random(7));Require(_owner.Hits==before-67,"Weaken missing from encounter weapon pool");
            _owner.Hits=_owner.HitsMax;before=_owner.Hits;
            EliteCombat.Hit(_source,_owner,100,ResistanceType.Physical,true,new Random(7),new(33,67));Require(_owner.Hits==before-44,"Slow damage was added to the Weaken pool instead of multiplied afterward");
            _owner.Hits=_owner.HitsMax;before=_owner.Hits;
            EliteCombat.Hit(_source,_owner,100,ResistanceType.Fire,true,new Random(7),new(33,67));Require(_owner.Hits==before-67,"Physical curse incorrectly changed native fire damage");
            NecromancerEffects.ClearOrdinaryCurse(_source);
        }
        finally{curse.Delete();Reset();}
    }
    private static void Natural()
    {Reset();_phase=0;Next();}
    private static void Next()
    {
        Reset();if(_phase!=0){Slow(20);}_before=_owner.Hits;_started=Core.TickCount;
        if(_phase<2){EliteBolts.Lightning(_source);}else{EliteBolts.Fan(_source,_owner,1,true);}
        Require(_owner.Hits==_before && EliteBolts.Inspect(_source).Length>0,"Natural launch applied immediate damage");
        if(_phase!=0){AmazonSlowMissiles.Clear(_source);}
        Later(TimeSpan.FromMilliseconds(90),ObserveImpact);
    }
    private static void Later(TimeSpan delay,Action action)=>Timer.StartTimer(delay,()=>
    {try{action();}catch(Exception e){Cleanup();Log.Error(e,"slow-missiles-bolts-result: failed");}});
    private static void ObserveImpact()
    {
        if(_owner.Hits==_before && Core.TickCount-_started<6000){Later(TimeSpan.FromMilliseconds(30),ObserveImpact);return;}
        Require(_owner.Hits<_before && NecromancerEffects.ManaBalance256(_owner)==0,"Natural native bolt arrival/resource");
        if(_phase<2){Require(_owner.Hits==_before-LightningLoss(_phase==0?100:67),"Natural captured lightning damage");}
        Log.Information("Slow native bolt phase={Phase} elapsedMs={Elapsed} lifeLoss={Loss}",_phase,Core.TickCount-_started,_before-_owner.Hits);
        if(++_phase<3){Next();return;}
        Cleanup();Reset();Slow(20);var state=AmazonSlowMissiles.State(_source) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Where(x=>x.Target!=_source.Serial.Value).Append(state).ToArray());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(_owner.Serial.Value,_caster.Serial.Value,_source.Serial.Value,_origin,state,NecromancerEffects.ManaBalance256(_owner))));
        Log.Information("slow-missiles-bolts-result: passed");
    }
    private static void Check()
    {
        _owner=Fresh(DiabloClass.Barbarian);_caster=Fresh(DiabloClass.Amazon);Train(_caster,"amazon.slowmissiles",20);Place();
        _source=new EncounterCreature{CantWalk=true,NoKillAwards=true,Name="SlowBoltSource"};_source.AIObject.AITimer.Stop();
        Projection();Travel();CursePool();Natural();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));_owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;_caster=World.FindMobile((Serial)saved.Caster) as PlayerMobile;_source=World.FindMobile((Serial)saved.Source) as EncounterCreature;_origin=saved.Origin;
        Require(_owner!=null && _caster!=null && _source!=null && AmazonSlowMissiles.State(_source)==saved.State && NecromancerEffects.ManaBalance256(_owner)==saved.Mana,"Saved source state/receiver resources: owner="+(_owner!=null)+",caster="+(_caster!=null)+",source="+(_source!=null)+",state="+(_source==null?null:AmazonSlowMissiles.State(_source))+",expected="+saved.State+",mana="+(_owner==null?-1:NecromancerEffects.ManaBalance256(_owner))+",expectedMana="+saved.Mana);
        Require(EliteBolts.Inspect(_source).Length==0,"Transient enemy bolts replayed across restart");
        _source.AIObject.AITimer.Stop();_owner.MoveToWorld(_origin,Map.Trammel);Projection();Travel();CursePool();Natural();
    }
}
