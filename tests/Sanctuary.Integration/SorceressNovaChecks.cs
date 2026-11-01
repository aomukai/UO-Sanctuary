// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SorceressNovaChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SorceressNovaChecks));
    private sealed record Saved(uint Owner,uint Target,Point3D Point,int Map,int Mana,int Life,SorceressSpellFlight[] Flights,int Generation);
    private static string Key(CombatAbility a)=>a==CombatAbility.Nova?"nova":"frost-nova";
    private static string Manifest(CombatAbility a)=>Path.Join(Core.BaseDirectory,Key(a)+"-fixtures.json");
    public static void Configure()
    {
        foreach(var ability in new[]{CombatAbility.Nova,CombatAbility.FrostNova})
        {
            var a=ability;string key=Key(a);Register(key+"-check",()=>Check(a));
            Register(key+"-reload-check",()=>FixtureNetwork.Connected(key+"-reload-result",n=>Reload(n,a)));
            foreach(bool q in new[]{false,true}){bool quick=q;string label=key+(q?"-quick":"-right");Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,a,quick)));}
        }
    }
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Saved Sorceress64ray Nova source,combat,input and reload",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(CombatAbility a)
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,a==CombatAbility.Nova?6750:6670);p.Name="SorceressNovaFixture";
        void Train(string id)
        {if(ClassCreation.Profiles.GetRank(p.Serial,id)>0){return;}TalentCatalog.TryGet(DiabloClass.Sorceress,id,out var r);foreach(string pre in r.Prerequisites){Train(pre);}Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Learn "+id);}
        Train(SkillMechanics.For(a).TalentId);Require(SkillBindings.Instance.Assign(p,false,a)&&SkillBindings.Instance.AssignQuick(p,7,a),"Nova bindings");return p;
    }
    private static SorceressSpellFlight[] Own(PlayerMobile p)=>SorceressSpellMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static void Through(PlayerMobile p,int n,bool actions=true)
    {for(int i=0;i<n&&Own(p).Length>0;i++){SorceressSpellMissiles.Tick(Own(p).Max(x=>x.NextUtc),actions);}}
    private static void Create(PlayerMobile p,CombatAbility a)
    {foreach(var s in SorceressSpellMissiles.PrepareRing(p,SkillMechanics.Values(p,a),TimedEffects.Now)){SorceressSpellMissiles.Commit(s);}}
    private static void Single(PlayerMobile p)
    {var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Where(x=>x.Owner!=p.Serial.Value||x.RadialIndex==0).ToArray()});}
    private static void NoCold(Mobile t)=>Require(!TimedEffects.TryActor(t,out var state)||state.Cold==null&&state.Control?.Freeze==null,"Lightning Nova created cold/freeze");
    private static void Source(CombatAbility a)
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sorceress-nova-emitter-source-cases.json")));int shots=0;
        foreach(var r in source.RootElement.EnumerateArray())
        {
            if(r[0].GetInt32()!=(a==CombatAbility.Nova?48:44)){continue;}
            int i=0;foreach(var s in r[9].EnumerateArray())
            {var d=FrozenOrbs.Direction(i++);Require(s[3].GetInt32()==400+d.X&&s[4].GetInt32()==700+d.Y&&s[5].GetInt32()==4608&&s[6].GetInt32()==(a==CombatAbility.Nova?13:14)&&s[8].GetInt32()==r[0].GetInt32()&&s[9].GetInt32()==r[1].GetInt32()&&s[10].GetInt32()==1&&s[11].GetInt32()==-1000&&s[12].GetInt32()==6144,"Complete Nova emitter/factory mismatch");shots++;}
            Require(i is 0 or 64,"Partial source ring");
        }
        Require(shots==384,"Missing source launch projections");
        using var acceleration=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"missile-acceleration-source-probe.json")));int cases=0;
        foreach(var r in acceleration.RootElement.EnumerateArray())
        {int V(int i)=>r[i].GetInt32();var v=MissileVectors.Accelerate(V(0),V(1),V(2),V(3),V(4),V(5),V(6));Require(v==(V(7),V(8),V(9),V(10),V(11))&&v==MartialLightning.VelocityStep(V(0),V(1),V(2),V(3),V(4),V(5),V(6)),"Shared source acceleration/vector regression");cases++;}
        Require(cases==1800,"Missing acceleration source contexts");
        int Basis(int rank,bool max)
        {
            bool cold=a==CombatAbility.FrostNova;int n=cold?(max?8:4):(max?20:1);
            for(int level=2;level<=rank;level++){n+=cold?(level<=8?6:level<=16?8:level<=22?10:level<=28?12:14)+(max?1:0):(level<=8?6:level<=16?7:level<=22?8:level<=28?9:10)+(max?2:0);}return n*(cold?128:256);
        }
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            bool cold=a==CombatAbility.FrostNova;var values=new SkillValues(SkillMechanics.For(a),k=>k==(cold?"Frost Nova":"Nova")?rank:200,k=>k=="Static Field"||k is "Blizzard" or "Frozen Orb"?hard:0,99);
            int percent=100+(cold?20:5)*hard;Require(values.Damage256(false)==Basis(rank,false)*percent/100&&values.Damage256(true)==Basis(rank,true)*percent/100&&values.Mana256==256*((cold?9:13)+rank-1)&&values.DurationFrames==(cold?200+25*(rank-1):0),"Independent Nova effective/hard supplier "+rank+"/"+hard);
        }
    }
    private static void Check(CombatAbility a)
    {
        Source(a);var p=Owner(a);try
        {
            int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,a).Mana256;
            Require(ClassCombat.Perform(p,a,null,new(p.X+3,p.Y,p.Z),Core.TickCount),"Nova windup");var session=ClassCombat.For(p);
            Require(session.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana&&Own(p).Length==0&&!ClassCombat.ResolveAction(p,session.EffectAt-1),"Nova self origin/early debit");
            Require(ClassCombat.ResolveAction(p,session.EffectAt)&&!ClassCombat.ResolveAction(p,session.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Own(p).Length==64,"Nova atomic release/payment");
            var ring=Own(p);Require(ring.Select(x=>x.Cast).Distinct().Count()==1&&ring.Select(x=>x.RadialIndex).SequenceEqual(Enumerable.Range(0,64)),"Nova cast/ray identities");
            Require(ring.All(x=>x.VelocityFp8==4608&&x.Frames==(a==CombatAbility.Nova?13:14)&&x.LastCollision==p.Serial.Value&&x.ColdFrames==(a==CombatAbility.FrostNova?200:0)),"Nova launch shape");
            Through(p,4);Require(Own(p).Length==64&&Own(p).All(x=>x.VelocityFp8==4608&&x.AccelerationCounter==4),"Nova acceleration before fifth update");
            Through(p,1);Require(Own(p).All(x=>x.VelocityFp8==3608&&x.AccelerationCounter==0),"Nova fifth update did not decelerate");Through(p,5);Require(Own(p).All(x=>x.VelocityFp8==2608),"Nova second acceleration step");Through(p,20);Require(Own(p).Length==0,"Nova lifetime exceeded source");
            AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());
            Create(p,a);Require(Own(p).All(x=>x.VelocityFp8==4608&&x.Slow.VelocityPercent==100&&x.Slow.DamagePercent==50),"Nova CanSlow0/independent damage slow");
        }finally{FireBoltChecks.Clean(p);}
        Contacts(a);Gear(a);Controls(a);Save(a);
        Log.Information("{Key}:765independent rank/hard contexts,384complete factory projections,1800shared acceleration cases,64saved decelerating rays,non-killing collision/last-hit/cast delays,typed potion/cold,lifecycle and saves pass",Key(a));
    }
    private static void Contacts(CombatAbility a)
    {
        var p=Owner(a);var near=FireBoltChecks.Target(p,2);var far=FireBoltChecks.Target(p,4);try
        {
            Create(p,a);Single(p);int first=SkillHealth.Balance256(near),second=SkillHealth.Balance256(far);Through(p,20);
            Require(SkillHealth.Balance256(near)<first&&SkillHealth.Balance256(far)<second,"Nova ray stopped at first recipient");
            if(a==CombatAbility.FrostNova){Require(TimedEffects.TryActor(near,out var state)&&state.Cold!=null&&state.Control?.Freeze==null,"Frost Nova omitted chill or applied freeze");}else{NoCold(near);NoCold(far);}
        }finally{FireBoltChecks.Clean(p,near,far);}
        foreach(bool immune in new[]{false,true})
        {
            p=Owner(a);var t=FireBoltChecks.Target(p,2);try
            {
                t.SetResistance(a==CombatAbility.Nova?ResistanceType.Energy:ResistanceType.Cold,immune?100:50);Require(CombatPotions.Apply(p,3),"Nova actual potion");
                var shots=SorceressSpellMissiles.PrepareRing(p,SkillMechanics.Values(p,a),TimedEffects.Now);
                foreach(var s in shots){SorceressSpellMissiles.Commit(s with{Minimum256=25600,Maximum256=25600});}
                Single(p);int life=SkillHealth.Balance256(t);Through(p,20);
                Require(life-SkillHealth.Balance256(t)==(immune?7680:21760),"Nova typed potion/resistance or repeat LastCollide hit");
                if(a==CombatAbility.Nova||immune){NoCold(t);}
            }finally{FireBoltChecks.Clean(p,t);}
        }
        p=Owner(a);var overlap=FireBoltChecks.Target(p,2);try
        {
            var shots=SorceressSpellMissiles.PrepareRing(p,SkillMechanics.Values(p,a),TimedEffects.Now);
            foreach(var shot in shots){SorceressSpellMissiles.Commit(shot with{Minimum256=25600,Maximum256=25600});}
            int last=-4,hits=0;
            for(int frame=1;frame<=14&&Own(p).Length>0;frame++)
            {
                int life=SkillHealth.Balance256(overlap);Through(p,1);int loss=life-SkillHealth.Balance256(overlap);
                if(loss>0){Require(loss==25600&&frame-last>=4,"Same-cast Nova rays ignored their shared160ms delay");last=frame;hits++;}
            }
            Require(hits>0,"Full Nova ring missed its overlap target");
        }finally{FireBoltChecks.Clean(p,overlap);}
        p=Owner(a);var target=FireBoltChecks.Target(p,3);try
        {
            Create(p,a);var flight=Own(p)[0];int hp=SkillHealth.Balance256(target);SorceressSpellMissiles.Tick(flight.NextUtc,true);
            Require(!SorceressSpellMissiles.Ready(flight.Cast,target.Serial.Value,flight.NextUtc)||SkillHealth.Balance256(target)==hp,"Nova lease bookkeeping");
            // Lease boundaries are independent of the selected native hit geometry.
            var pool=SorceressSpellMissiles.Snapshot();var lease=new WeaponFlightLease(flight.Cast,target.Serial.Value,flight.NextUtc.AddMilliseconds(160));
            SorceressSpellMissiles.Restore(pool with{Leases=pool.Leases.Where(x=>x.Cast!=flight.Cast||x.Target!=target.Serial.Value).Append(lease).ToArray()});
            Require(!SorceressSpellMissiles.Ready(flight.Cast,target.Serial.Value,flight.NextUtc.AddMilliseconds(159))&&SorceressSpellMissiles.Ready(flight.Cast,target.Serial.Value,flight.NextUtc.AddMilliseconds(160))&&SorceressSpellMissiles.Ready(pool.NextId+1,target.Serial.Value,flight.NextUtc),"Nova160ms boundary/separate cast");
        }finally{FireBoltChecks.Clean(p,target);}
    }
    private static void Gear(CombatAbility a)
    {
        var p=Owner(a);try
        {
            var item=PoisonDaggerChecks.Named(p,a==CombatAbility.Nova?"Eschuta's temper":"Nightwing's Veil");Create(p,a);var captured=Own(p);
            Require(captured.All(x=>x.Rank>1&&(a==CombatAbility.Nova?x.Modifiers.LightningMastery:x.Modifiers.ColdMastery)>0),"Actual Nova rank/enhancement gear");
            p.Backpack.DropItem(item);ClassGear.Refresh(p);Require(Own(p).SequenceEqual(captured),"Nova changed captured packet after gear removal");
        }finally{FireBoltChecks.Clean(p);}
    }
    private static void Controls(CombatAbility a)
    {
        foreach(string mode in new[]{"offline","facet","death","respec","wardrobe","wall"})
        {
            var p=Owner(a);var t=FireBoltChecks.Target(p,4);Item wall=null;try
            {
                Create(p,a);int hp=SkillHealth.Balance256(t);
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="death"){p.Kill();}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Nova wardrobe");}
                if(mode=="wall"){Single(p);wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);}
                Through(p,20,mode!="offline");Require(Own(p).Length==0&&SkillHealth.Balance256(t)==hp,"Nova lifecycle "+mode);
            }finally{wall?.Delete();FireBoltChecks.Clean(p,t);}
        }
        var owner=Owner(a);try
        {
            int before=NecromancerEffects.ManaBalance256(owner);
            Require(ClassCombat.Perform(owner,a,null,owner.Location,Core.TickCount),"Cancelled Nova windup");long effect=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);
            Require(!ClassCombat.ResolveAction(owner,effect)&&NecromancerEffects.ManaBalance256(owner)==before&&Own(owner).Length==0,"Cancelled Nova paid/emitted");
            NecromancerEffects.SetMana256(owner,0);Require(!SorceressCombat.Release(owner,a,Serial.Zero,owner.Location)&&Own(owner).Length==0,"Unpaid Nova emitted rays");
            Create(owner,a);var pool=SorceressSpellMissiles.Snapshot();var first=Own(owner)[0];
            foreach(var bad in new[]{first with{RadialIndex=64},first with{Cast=0},first with{AccelerationCounter=5},first with{ColdFrames=a==CombatAbility.Nova?1:0}})
            {
                bool refused=false;try{SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==first.Id?bad:x).ToArray()});}catch(InvalidDataException){refused=true;}
                Require(refused&&SorceressSpellMissiles.Snapshot().Flights.SequenceEqual(pool.Flights),"Malformed Nova save partially replaced pool");
            }
            owner.MoveToWorld(new(1497,1628,10),Map.Trammel);SorceressSpellMissiles.Cancel(owner);NecromancerEffects.SetMana256(owner,20000);int mana=NecromancerEffects.ManaBalance256(owner);
            Require(!SorceressCombat.Release(owner,a,Serial.Zero,owner.Location)&&mana==NecromancerEffects.ManaBalance256(owner)&&Own(owner).Length==0,"Town Nova paid/emitted");
        }finally{FireBoltChecks.Clean(owner);}
    }
    private static void Save(CombatAbility a)
    {
        var p=Owner(a);var target=FireBoltChecks.Target(p,2);Create(p,a);Through(p,20);int life=SkillHealth.Balance256(target);var point=target.Location;target.MoveToWorld(new(p.X+12,p.Y+12,p.Z),p.Map);Create(p,a);Through(p,6);target.MoveToWorld(point,p.Map);
        var flights=Own(p).Select(x=>x with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}).ToArray();var pool=SorceressSpellMissiles.Snapshot();
        SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(flights).ToArray()});
        var saved=new Saved(p.Serial.Value,target.Serial.Value,p.Location,p.Map.MapID,NecromancerEffects.ManaBalance256(p),life,flights,0);p.Internalize();File.WriteAllText(Manifest(a),JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network,CombatAbility a)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest(a)));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var held=World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&Own(p).SequenceEqual(s.Flights)&&SkillHealth.Balance256(held)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillBindings.Instance.Get(p).Right==a&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Sorceress,7)==a,"Saved Nova rays/wounds/mana/binding");
        var account=FixtureNetwork.Authenticate(network,p,Key(a)+"-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var point=held.Location;held.MoveToWorld(new(p.X+12,p.Y+12,p.Z),p.Map);
        var t=FireBoltChecks.Target(p,4);int life=SkillHealth.Balance256(t);
        void Start()
        {
            var pool=SorceressSpellMissiles.Snapshot();long first=pool.NextId+1;var copies=s.Flights.Select(x=>x with{Id=first+x.RadialIndex,Cast=first,NextUtc=TimedEffects.Now}).ToArray();
            SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Concat(copies).ToArray(),NextId=first+63});var deadline=TimedEffects.Now.AddSeconds(4);
            void Observe()
            {
                if(Own(p).Length>64&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
                try
                {
                    Require(SkillHealth.Balance256(t)<life&&Own(p).SequenceEqual(s.Flights)&&SkillHealth.Balance256(held)==s.Life,"Resumed moving Nova ring or held wound preservation");
                    t.Delete();held.MoveToWorld(point,p.Map);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                    File.WriteAllText(Manifest(a),JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("{Key}-reload-result: passed",Key(a));
                }catch(Exception e){Log.Error(e,"{Key}-reload-result: failed",Key(a));}
            }
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
        }
        FixtureClock.NextLoop(Key(a)+"-reload-result",Start);
    }
    private static void Input(NetState network,CombatAbility a,bool quick)
    {
        var p=Owner(a);var t=FireBoltChecks.Target(p,3);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t),cost=SkillMechanics.Values(p,a).Mana256;
        var account=FixtureNetwork.Authenticate(network,p,Key(a)+"-input-");string label=Key(a)+(quick?"-quick":"-right");bool began=false;long deadline=Core.TickCount+6000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Real Nova input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==a&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==life&&Own(p).Length==0,"Nova input paid before release");began=true;Log.Information("{Label}: windup",label);}
                if(Own(p).Length>0||SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Actual Nova input payment");
                FireBoltChecks.Clean(p,t);account.Delete();Log.Information("{Label}-result: passed",label);
            }catch(Exception e){FireBoltChecks.Clean(p,t);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)a,(int)DiabloClass.Sorceress,p.X+3,p.Y,p.Z);Observe();});
    }
}
