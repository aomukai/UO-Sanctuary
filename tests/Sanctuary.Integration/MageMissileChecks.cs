// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
namespace Sanctuary.Integration;

public static class MageMissileChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageMissileChecks));
    private sealed record Saved(uint Owner,uint Mage,uint Target,Point3D Point,int Map,MageMissileState State,MageCriticalSeed Seed,int Life,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-missile-fixtures.json");
    public static void Configure()
    {
        Register("mage-missile-check",Check);Register("mage-missile-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("mage-missile-live","Natural mage windup and delayed collision",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("mage-missile-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Skeletal Mage source packet and live projectile cases",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id)
    {if(ClassCreation.Profiles.GetRank(p.Serial,id)>0){return;}TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){Train(p,prior);}Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    private static (PlayerMobile Owner,NecromancerMinion Mage,GraveyardSkeleton Target) Scene(int element,int dx=5)
    {
        var p=PoisonDaggerChecks.Owner();Train(p,SkillMechanics.For(CombatAbility.RaiseSkeletalMage).TalentId);var roll=NecromancerSummons.ElementRoll;
        var mage=new NecromancerMinion();try{NecromancerSummons.ElementRoll=()=>element;mage.Prepare(p,CombatAbility.RaiseSkeletalMage);}finally{NecromancerSummons.ElementRoll=roll;}
        Require(mage.SetControlMaster(p),"Mage ownership");mage.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);mage.AIObject.AITimer.Stop();
        PersistentMinions.Profiles.Track(mage);var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);
        foreach(var resistance in Enum.GetValues<ResistanceType>()){target.SetResistance(resistance,0);}TimedEffects.PreventHealing(target,TimedEffects.Now);return(p,mage,target);
    }
    private static MageMissileState State(long id)=>NecromancerMageMissiles.Snapshot().Bolts.SingleOrDefault(x=>x.Id==id);
    private static void Clear(NecromancerMinion mage)
    {var s=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(s with{Bolts=s.Bolts.Where(x=>x.Actor!=mage.Serial.Value).ToArray(),Seeds=s.Seeds.Where(x=>x.Actor!=mage.Serial.Value).ToArray()});}
    private static void Clean(PlayerMobile p,NecromancerMinion mage,Mobile target)
    {Clear(mage);mage.Delete();target.Corpse?.Delete();target.Delete();p.Corpse?.Delete();p.Delete();}
    private static void Through(long id,int frames,bool fight=true)
    {for(int i=0;i<frames&&State(id) is{} state;i++){NecromancerMageMissiles.Tick(state.NextUtc,fight);}}
    private static void Source()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"mage-missile-source-cases.json")));int count=0;
        foreach(var row in json.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();var packet=NecromancerMageMissiles.Packet(V(0),V(1));int calls=0;
            int roll=WeaponMissileDamage.RollValue(packet.Minimum256,packet.Maximum256,0,n=>{calls++;return n>0?n-1:0;});
            Require(packet.Minimum256==V(2)&&packet.Maximum256==V(3)&&packet.ElementFrames==V(4)&&roll==V(5)&&calls==V(6),"Complete mage source packet/impact roll "+count);count++;
        }
        Require(count==1524,"Incomplete mage source contexts");
    }
    private static void Travel()
    {
        for(int element=0;element<4;element++)
        {
            var(p,m,t)=Scene(element);try
            {
                int before=SkillHealth.Balance256(t);long id=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);var first=State(id);
                Require(first is{Frame:0,VelocityFp8:2688}&&first.Packet.Missile=="necromage"+(element+1)&&SkillHealth.Balance256(t)==before,"Launch dealt immediate damage or wrong missile");
                Through(id,5);Require(State(id) is{Frame:5,Fault:null}&&SkillHealth.Balance256(t)==before,"Distant target hit before travel");
                Through(id,30);Require(State(id)==null&&SkillHealth.Balance256(t)<before,"Traced elemental collision missing");
                if(element==0){Require(TimedEffects.TryActor(t,out var poison)&&poison.PoisonClock is{Rate256:>0},"Poison missile lost FP state");}
                if(element==1){Require(TimedEffects.TryActor(t,out var cold)&&cold.Cold?.Source==m.Serial.Value,"Cold missile lost chill source");}
            }
            finally{Clean(p,m,t);}
        }
        var(miss,body,enemy)=Scene(2);try
        {
            int before=SkillHealth.Balance256(enemy);long id=NecromancerMageMissiles.Launch(body,enemy,TimedEffects.Now);
            enemy.MoveToWorld(new(enemy.X,enemy.Y+4,enemy.Z),enemy.Map);Through(id,35);
            Require(State(id)==null&&SkillHealth.Balance256(enemy)==before,"Missile homed onto moving target");
            enemy.MoveToWorld(new(miss.X+5,miss.Y,miss.Z),miss.Map);var blocker=PoisonDaggerChecks.Target(miss);blocker.MoveToWorld(new(miss.X+2,miss.Y,miss.Z),miss.Map);blocker.SetResistance(ResistanceType.Fire,0);
            try
            {int other=SkillHealth.Balance256(blocker);id=NecromancerMageMissiles.Launch(body,enemy,TimedEffects.Now);Through(id,35);
                Require(SkillHealth.Balance256(blocker)<other&&SkillHealth.Balance256(enemy)==before,"First intervening enemy was not the sole collider");}
            finally{blocker.Delete();}
        }
        finally{Clean(miss,body,enemy);}
    }
    private static void Critical()
    {
        foreach(bool critical in new[]{false,true})
        {
            var(p,m,t)=Scene(2);var block=SourceBlocking.BlockRoll;
            try
            {
                long id=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);var bolt=State(id);ulong chosen=0,after=0;
                for(int low=0;low<10000;low++){ulong candidate=SourceRandom.InitLow(low),next=candidate;bool hit=SourceRandom.Roll(ref next)%100<5;if(hit==critical){chosen=candidate;after=next;break;}}
                var pool=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(pool with{Seeds=pool.Seeds.Select(x=>x.Actor==m.Serial.Value?x with{Seed=chosen}:x).ToArray()});
                SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Pure elemental mage projectile rolled a physical shield");
                ulong random=bolt.Seed;int raw=WeaponMissileDamage.RollValue(bolt.Packet.Minimum256,bolt.Packet.Maximum256,0,n=>(int)SourceRandom.Limited(ref random,n));
                int before=SkillHealth.Balance256(t);NecromancerMageMissiles.Impact(m,t,bolt);
                Require(before-SkillHealth.Balance256(t)==raw*(critical?2:1)&&NecromancerMageMissiles.Snapshot().Seeds.Single(x=>x.Actor==m.Serial.Value).Seed==after,
                    "Actual intrinsic critical amount/actor stream or physical shield path");
            }
            finally{SourceBlocking.BlockRoll=block;Clean(p,m,t);}
        }
    }
    private static void Controls()
    {
        foreach(string control in new[]{"immune","offline","facet","deleted","wall","slow"})
        {
            var(p,m,t)=Scene(2);Item wall=null;try
            {
                int before=SkillHealth.Balance256(t);
                if(control=="immune"){t.SetResistance(ResistanceType.Fire,100);}
                if(control=="slow")
                {AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(t.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());
                    // Apply the same captured state to the actual emitting NPC.
                    var rows=AmazonSlowMissiles.Snapshot();AmazonSlowMissiles.Restore(rows.Append(rows[^1] with{Target=m.Serial.Value}).ToArray());}
                long id=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);var first=State(id);
                if(control=="slow"){Require(first.VelocityFp8==1344&&first.Packet.Minimum256==256&&first.Packet.Maximum256==768,"Slow Missiles capture did not scale factory speed/endpoints");}
                if(control=="facet"){p.MoveToWorld(p.Location,p.Map==Map.Trammel?Map.Felucca:Map.Trammel);}
                if(control=="deleted"){m.Delete();}
                if(control=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);}
                Through(id,35,control!="offline");Require(State(id)==null,"Control projectile did not expire: "+control);
                if(control!="slow"){Require(SkillHealth.Balance256(t)==before,"Unexpected control damage: "+control);}
            }
            finally{wall?.Delete();AmazonSlowMissiles.Clear(m);AmazonSlowMissiles.Clear(t);Clean(p,m,t);}
        }
    }
    private static void Save()
    {
        var(p,m,t)=Scene(3,8);long first=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);NecromancerMageMissiles.Impact(m,t,State(first));
        var initial=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(initial with{Bolts=initial.Bolts.Where(x=>x.Id!=first).ToArray()});
        long id=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);Through(id,4);var current=NecromancerMageMissiles.Snapshot();
        var held=State(id) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        NecromancerMageMissiles.Restore(current with{Bolts=current.Bolts.Select(x=>x.Id==id?held:x).ToArray()});
        var seed=current.Seeds.Single(x=>x.Actor==m.Serial.Value);var point=p.Location;int map=p.Map.MapID;PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,m.Serial.Value,t.Serial.Value,point,map,held,seed,SkillHealth.Balance256(t),0)));
    }
    private static void Clocks()
    {
        var(p,m,t)=Scene(2);
        try
        {
            Require(m.MageAttackClock==(10,20)&&m.CombatProfile.AttackMs==800,"Mage SKA1HTH skill event8/speed208/end16 clock");
            TimedEffects.Slow(m,50,TimedEffects.Now);Require(m.MageAttackClock==(20,40),"Mage50percent slow clock");
            TimedEffects.Clear(m);Require(m.MageAttackClock==(10,20),"Cleared mage slow clock");
            PoisonDaggerChecks.Named(p,"Bloodfist");Require(m.MageAttackClock==(10,20),"Owner equipment IAS inherited by mage");
            Require(DruidWolfGeometry.Clock("A1",SourceAttackRate.Speed(208,0,275),"necromage-animation")== (6,12),"Mage175percent final rate cap");
            var flags=new int[144];flags[8]=2;
            var plan=SourceAnimationEvents.Initial(new(0,208,4096,flags,[]),0);
            Require(plan.Events.SequenceEqual(new[]{new SourceAnimationEvent(SourceAnimationEvents.ModeChange,10,2),new SourceAnimationEvent(SourceAnimationEvents.EndAnimation,20,0)}),"Mage projectile must retain skill action kind2");
            var golem=new NecromancerMinion();try{golem.Prepare(p,CombatAbility.ClayGolem);Require(golem.CombatProfile.AttackMs==720,"Mage clock changed another golem");}finally{golem.Delete();}
        }
        finally{Clean(p,m,t);}
    }
    private static void Check()
    {
        Source();Clocks();var seed=NecromancerMageMissiles.SeedLow;try{NecromancerMageMissiles.SeedLow=()=>1;Travel();Critical();Controls();Save();}finally{NecromancerMageMissiles.SeedLow=seed;}
        Log.Information("Mage missiles:1524source getter/length/roll cases;4elements delayed collision,nonhoming,interception,6immunity/lifecycle/terrain/slow controls and saved midflight/critical stream pass");
    }
    private static void Reload()
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var m=(NecromancerMinion)World.FindMobile((Serial)saved.Mage);var t=World.FindMobile((Serial)saved.Target);
        Require(State(saved.State.Id)==saved.State&&NecromancerMageMissiles.Snapshot().Seeds.Single(x=>x.Actor==saved.Mage)==saved.Seed&&SkillHealth.Balance256(t)==saved.Life,"Saved midflight packet/motion/seed changed");
        Require(m.MageAttackClock==(10,20)&&m.CombatProfile.AttackMs==800,"Saved mage lost source A1 skill clock");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);p.ClaimAutoStabledPets();m.MoveToWorld(p.Location,p.Map);m.AIObject.AITimer.Stop();
        var pool=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(pool with{Bolts=pool.Bolts.Select(x=>x.Id==saved.State.Id?x with{NextUtc=TimedEffects.Now.AddMilliseconds(40)}:x).ToArray()});
        Through(saved.State.Id,1);var held=State(saved.State.Id);Require(held is{Fault:null}&&held.Frame==saved.State.Frame+1&&SkillHealth.Balance256(t)==saved.Life,"Loaded projectile failed to resume once");
        held=held with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};pool=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(pool with{Bolts=pool.Bolts.Select(x=>x.Id==held.Id?held:x).ToArray()});
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=held,Generation=saved.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var(p,m,t)=Scene(2);network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("mage-flight-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;network.Mobile=p;p.NetState=network;network.ResetMovementState();
        int life=SkillHealth.Balance256(t);long deadline=0,started=0;bool early=false,launched=false;long flight=0;MageMissileState last=null;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,$"Natural mage projectile timeout:ownerAlive{p.Alive},ownerMap{p.Map},ownerNet{p.NetState!=null},permitted{m.Permitted(p)},active{PersistentMinions.Active(p,m)},town{SpellHelper.IsTown(p.Location,p)},last{JsonSerializer.Serialize(last)},launched{launched},alive{m.Alive},blocked{AlliedOverlap.Blocked(m)},frozen{m.Frozen},enemy{m.IsEnemy(t)},map{m.Map},position{m.Location},target{t.Location},network{network.Running},flights{NecromancerMageMissiles.Snapshot().Bolts.Count(x=>x.Actor==m.Serial.Value)}");var bolt=NecromancerMageMissiles.Snapshot().Bolts.FirstOrDefault(x=>x.Actor==m.Serial.Value);if(bolt!=null){last=bolt;}
                if(!launched&&bolt!=null){Require(early&&Core.TickCount-started is >=400 and <800&&SkillHealth.Balance256(t)==life&&bolt.Fault==null,"Natural mage launch must follow400ms skill event before800ms cycle and precede damage");launched=true;flight=bolt.Id;m.SuspendActions();}
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(launched&&State(flight)==null,"Natural mage hit without traced collision/removal");Clean(p,m,t);account.Delete();Log.Information("mage-missile-live-result: passed");
            }
            catch(Exception e){Clean(p,m,t);account.Delete();Log.Error(e,"mage-missile-live-result: failed");}
        }
        FixtureClock.NextLoop("mage-missile-live-result",()=>
        {
            started=Core.TickCount;deadline=started+5000;
            Require(m.BeginMelee(t)==TimeSpan.FromMilliseconds(800),"Natural mage cycle must be800ms");
            Timer.StartTimer(TimeSpan.FromMilliseconds(360),()=>
            {
                try{Require(!NecromancerMageMissiles.Snapshot().Bolts.Any(x=>x.Actor==m.Serial.Value)&&SkillHealth.Balance256(t)==life,"Mage projectile or HP before skill event10");early=true;}
                catch(Exception e){Clean(p,m,t);account.Delete();Log.Error(e,"mage-missile-live-result: failed");}
            });
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>{Log.Information("mage-missile-live: ready");Inspect();});
        });
    }
}
