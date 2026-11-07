// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcChargedItemChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcChargedItemChecks));
    private sealed record Saved(uint Owner,uint Actor,Point3D Point,string Body,string[] Items,NpcEquipmentSeed Random,int Life,string Resources,
        uint Target,Point3D TargetPoint,int TargetLife,SorceressSpellFlight[] Flights,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-charged-item-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-charged-item-check",false);Register("npc-charged-item-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"NPC do17 emission/post-path RNG and actual saved charged flights",commandLine=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static SorceressSpellFlight[] Own(AmazonValkyrieBody b)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    private static ulong PostPath(int aim,int ordinal)
    {ulong seed=SourceRandom.InitLow(unchecked(aim+ordinal));for(int i=0;i<38;i++){SourceRandom.Roll(ref seed);}return seed;}
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-charged-item-cases.json")));int cases=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var a=row[0];int rank=a[0].GetInt32(),flag=a[1].GetInt32(),mode=a[2].GetInt32(),fail=a[3].GetInt32(),ar=a[4].GetInt32(),queries=0;var requests=new List<int[]>();
            var frame=new NpcItemSpellOrigins((380,680),null,(420,710),ar,false,r=>
            {int i=requests.Count;requests.Add([r.Flags,231,r.X,r.Y,r.TargetX,r.TargetY,r.Velocity,r.Skill,r.Rank,r.Missile,r.AttackRating,r.ChargedOrdinal]);return fail==-2||i==fail?null:new object();},(_,_)=>throw new InvalidOperationException("Charged budget"),i=>
            {Require(i==queries++,"do17 target query skipped/repeated ordinal");return mode==0||mode==4&&i%2==0?null:(mode==1?0:400+i,mode==2?0:700-i);});
            int result=frame.Run(38,rank,flag);var expected=row[2].EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
            Require(result==1&&frame.ServerDo&&queries==row[1][3].GetInt32()&&requests.Count==expected.Length&&requests.Zip(expected).All(p=>p.First.SequenceEqual(p.Second)),"Complete source NPC do17 trace at "+cases);cases++;
        }
        Require(cases==3780,"NPC do17 source coverage");
        using var paths=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"charged-bolt-path-source-probe.json")));cases=0;
        foreach(var r in paths.RootElement.EnumerateArray())
        {
            int origin=r[0].GetInt32(),dx=r[1].GetInt32(),dy=r[2].GetInt32();int heading=ChargedBolts.Heading(unchecked((ushort)(origin+dx))-origin,unchecked((ushort)(origin+dy))-origin);
            var path=ChargedBolts.Path(r[3].GetUInt32(),heading,r[4].GetInt32(),out ulong seed);
            Require(seed==r[6].GetUInt64()&&path.Length==r[7].GetArrayLength(),"Complete source path/final stream at "+cases);
            for(int i=0;i<path.Length;i++){Require(unchecked((ushort)(origin+2*path[i].X))==r[7][i][0].GetInt32()&&unchecked((ushort)(origin+2*path[i].Y))==r[7][i][1].GetInt32(),"Charged path changed at "+cases);}
            cases++;
        }
        Require(cases==2160,"Shared path source coverage");
    }
    private static void Place(AmazonValkyrieBody b,PlayerMobile p)
    {b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();var row=PersistentMinions.Profiles.Track(b);PersistentMinions.Profiles.Store(b,row with{Parked=false});Require(NpcItemSpellFlights.Attached(b,p,p.Map.MapID),"Charged item source inactive");}
    private static GraveyardZombie Target(AmazonValkyrieBody b)
    {var t=new GraveyardZombie{CantWalk=true,Frozen=true,NoKillAwards=true};t.SetHits(65000);t.MoveToWorld(new(b.X+12,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static NpcItemSpellEmission Emit(AmazonValkyrieBody b,GraveyardZombie t,int rank,int flag,DateTime at,bool partial=false,bool failed=false)
    {
        int calls=0;var rng=NpcEquipmentRandom.State(b);var result=SorceressSpellMissiles.EmitOriginalItem(b,NpcItemTarget.Capture(t),t.Location,38,rank,flag,at,r=>
        {Require(r.Flags==33&&r.AttackRating==0&&r.ChargedOrdinal==calls,"Charged source used helper AR or lost ordinal");int i=calls++;return new(!failed&&(!partial||i%2==0),SourceRandom.InitLow(999+i));});
        int count=Math.Min(24,rank+2);var own=Own(b).Where(f=>result.Flights.Contains(f.Id)).ToArray();
        Require(result.Result==1&&result.Attempts==calls&&calls==count&&own.Length==(failed?0:partial?(count+1)/2:count)&&NpcEquipmentRandom.State(b)==rng,"Charged emission count/return/actor RNG differs");
        foreach(var f in own){Require(f.Charged!=null&&f.Seed==PostPath(t.X*2,f.Charged.Ordinal)&&f.X==b.X&&f.Y==b.Y&&f.Frames==77,"Charged missile did not retain post-initializer path stream");}
        return result;
    }
    private static void CrossConsumers()
    {
        var p=FireBoltChecks.Owner(CombatAbility.ChargedBolt,6960);var old=SorceressSpellMissiles.SeedLow;int calls=0;
        try
        {
            SorceressSpellMissiles.SeedLow=()=>{calls++;return 12345;};
            foreach(bool self in new[]{false,true})
            {
                var aim=self?p.Location:new Point3D(p.X+12,p.Y,p.Z);var values=SkillMechanics.Values(p,CombatAbility.ChargedBolt,3);var shots=SorceressSpellMissiles.PrepareCharged(p,aim,values,TimedEffects.Now.AddMinutes(10));
                Require(shots.Length==5&&shots.All(f=>f.Seed==PostPath(aim.X*2+(self?1:0),f.Charged.Ordinal)),"Player charged creation lost source final path seed/self aim correction");
            }
            Require(calls==10,"Player charged allocation draw count changed");
        }
        finally{SorceressSpellMissiles.SeedLow=old;SorceressSpellMissiles.Cancel(p);p.Delete();}
        var necro=MagePetBuffChecks.Owner(5,6840,2880);var mage=MagePetBuffChecks.Pet(necro,3);var target=PoisonDaggerChecks.Target(necro);target.MoveToWorld(new(mage.X+12,mage.Y,mage.Z),mage.Map);target.AIObject?.AITimer.Stop();var previous=NecromancerMageMissiles.SeedLow;int draws=0;
        try
        {
            NecromancerMageMissiles.SetActorSeed(mage,SourceRandom.InitLow(333));var actor=NecromancerMageMissiles.ActorSeed(mage);NecromancerMageMissiles.SeedLow=()=>{draws++;return 999;};
            MageBasicSkills.Launch(mage,target,TimedEffects.Now.AddMinutes(10));var shots=NecromancerMageMissiles.Snapshot().Bolts.Where(f=>f.Actor==mage.Serial.Value).OrderBy(f=>f.Id).ToArray();
            var rays=FanGeometry.Create((target.X-mage.X)*2,(target.Y-mage.Y)*2,3);int count=MageBasicSkills.Count(mage),ordinal=0;
            foreach(var ray in rays)for(int i=0;i<count;i++,ordinal++){Require(shots[ordinal].Seed==PostPath(mage.X*2+ray.X,ordinal),"Mage charged triple lost concatenated ordinal/final path seed");}
            Require(shots.Length==count*3&&draws==shots.Length&&NecromancerMageMissiles.ActorSeed(mage)==actor,"Mage charged draw count/actor stream changed");
        }
        finally
        {
            NecromancerMageMissiles.SeedLow=previous;var saved=NecromancerMageMissiles.Snapshot();
            NecromancerMageMissiles.Restore(saved with{Bolts=saved.Bolts.Where(f=>f.Actor!=mage.Serial.Value).ToArray()});
            target.Delete();mage.Delete();necro.Delete();
        }
    }
    private static void Controls(AmazonValkyrieBody b,PlayerMobile p)
    {
        Place(b,p);var t=Target(b);
        try
        {
            foreach(int rank in Enumerable.Range(1,63))foreach(int flag in new[]{0,1})foreach(int mode in new[]{0,1,2})
            {Emit(b,t,rank,flag,TimedEffects.Now.AddMinutes(10),mode==1,mode==2);SorceressSpellMissiles.Cancel(b);}
            var result=Emit(b,t,3,0,TimedEffects.Now.AddMinutes(10));var save=SorceressSpellMissiles.Snapshot();var id=result.Flights[0];var old=save with{Flights=save.Flights.Select(f=>f.Id==id?f with{Seed=123456789UL}:f).ToArray()};
            SorceressSpellMissiles.Restore(old);Require(Own(b).Single(f=>f.Id==id).Seed==123456789UL,"Existing saved charged seed was recalculated");SorceressSpellMissiles.Cancel(b);
        }
        finally{t.Delete();SorceressSpellMissiles.Cancel(b);}
        CrossConsumers();
    }
    private static void Step(SorceressSpellFlight f)=>SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);
    private static void Check(bool reload)
    {
        Source();PlayerMobile p;AmazonValkyrieBody b;Point3D point;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra charged item reload");p=World.FindMobile((Serial)s.Owner)as PlayerMobile;b=World.FindMobile((Serial)s.Actor)as AmazonValkyrieBody;point=s.Point;
            Require(p is{Alive:true,Deleted:false}&&b is{Alive:true,Deleted:false}&&b.SavedText==s.Body&&Json(Items(b))==Json(s.Items)&&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life&&Json(GeneratedNpcLiveResources.State(b))==s.Resources&&Json(Own(b))==Json(s.Flights),"Charged actual path/seed/items/resources/wounds changed on reload");
            p.MoveToWorld(point,Map.Trammel);Place(b,p);var target=(GraveyardZombie)World.FindMobile((Serial)s.Target);Require(SkillHealth.Balance256(target)==s.TargetLife,"Held target life changed");
            var before=Own(b);foreach(var f in before){Step(f);}Require(Own(b).Zip(before).All(pair=>pair.First.X==pair.Second.X&&pair.First.Y==pair.Second.Y&&pair.First.Frame==pair.Second.Frame+1&&pair.First.Seed==pair.Second.Seed),"Loaded charged interval replayed movement or RNG");
            target.MoveToWorld(s.TargetPoint,b.Map);var rng=NpcEquipmentRandom.State(b);ulong critical=rng.Seed;int expected=s.TargetLife,hits=0;
            for(int frame=0;frame<80&&Own(b).Length>0;frame++)foreach(var f in Own(b))
            {
                int life=SkillHealth.Balance256(target);Step(f);if(SkillHealth.Balance256(target)<life)
                {int raw=SorceressSpellMissiles.Roll(f,out _);if(SourceRandom.Roll(ref critical)%100<5){raw*=2;}expected-=raw;hits++;Require(SkillHealth.Balance256(target)==expected,"Resumed charged HP differs from post-path seed");}
            }
            Require(hits>0&&Own(b).Length==0&&NpcEquipmentRandom.State(b)==rng with{Seed=critical},"Saved charged streams did not resume natural HP/actor RNG");target.Delete();
        }
        else
        {
            p=new PlayerMobile{Name="NpcChargedItemFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6800,24,3440);point=p.Location;
            using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var row=source.RootElement.EnumerateArray().First(x=>x.GetProperty("Critical").GetInt32()==1&&x.GetProperty("Rank").GetInt32()==20);
            b=ValkyrieDamageChecks.Body(p,row);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);Place(b,p);
        }
        Controls(b,p);Place(b,p);var held=Target(b);var made=Emit(b,held,3,0,TimedEffects.Now.AddMinutes(10),true);held.Internalize();foreach(var f in Own(b)){Step(f);}var flights=Own(b);Require(flights.Length==3&&flights.All(f=>f.Frame==1)&&flights.Select(f=>f.Charged.Ordinal).SequenceEqual(new[]{0,2,4}),"Held partial charged population differs");
        var path=flights[0].Charged;var targetPoint=new Point3D(path.OriginX+path.Path[3].X,path.OriginY+path.Path[3].Y,b.Z);var random=NpcEquipmentRandom.State(b);int hp=SkillHealth.Balance256(b);string resources=Json(GeneratedNpcLiveResources.State(b));PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,point,b.SavedText,Items(b),random,hp,resources,held.Serial.Value,targetPoint,SkillHealth.Balance256(held),flights,generation)));
        Log.Information("NPC charged item:3780source do17/2160source path contexts;378world rank/flag/failure casts,post-path stream and retained allocation draws,player/Mage triples,self-aim and legacy seed preservation;partial3flights/original items/resources/wounds resume exact natural HP/RNG through two reloads;Chain/full callbacks/game RNG remain separate");
    }
}
