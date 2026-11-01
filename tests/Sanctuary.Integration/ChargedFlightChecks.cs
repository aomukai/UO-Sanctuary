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

public static class ChargedFlightChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ChargedFlightChecks));
    private sealed record Saved(uint Owner,int Map,Point3D Point,int Mana,SorceressSpellFlight[] Flights,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"charged-flight-fixtures.json");
    public static void Configure()
    {
        Register("charged-flight-check",Check);Register("charged-flight-reload-check",()=>FixtureNetwork.Connected("charged-flight-reload-result",Reload));
        foreach(bool quick in new[]{false,true}){bool q=quick;string label=q?"charged-flight-quick":"charged-flight-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Player saved charged paths/capture/input/reload",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()=>FireBoltChecks.Owner(CombatAbility.ChargedBolt,6280);
    private static SorceressSpellFlight[] Own(PlayerMobile p)=>SorceressSpellMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static SorceressSpellFlight[] Prepare(PlayerMobile p)=>SorceressSpellMissiles.PrepareCharged(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.ChargedBolt),TimedEffects.Now);
    private static void Through(PlayerMobile p,int frames,bool actions=true)
    {for(int i=0;i<frames&&Own(p).FirstOrDefault() is{} s;i++){SorceressSpellMissiles.Tick(s.NextUtc,actions);}}
    private static void Emit(SorceressSpellFlight[] shots){foreach(var s in shots){SorceressSpellMissiles.Commit(s);}}
    private static void Check()
    {
        Goldens();Emission();Contacts();Capture();Controls();Save();
        Log.Information("Player Charged Bolt:765rank/hard goldens,32rank/direction emissions,77frame capped independent saved paths,once-only mana,captured gear/mastery/Slow Missiles,contact potion/resistance,atomic path restore and lifecycle pass");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.ChargedBolt);
        int Basis(int rank,int start){int n=start;for(int level=2;level<=rank;level++){n+=level<=16?1:level<=22?2:level<=28?3:4;}return n*128;}
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(row,k=>k==row.SourceKey?rank:200,k=>k=="Lightning"?hard:0,99);
            Require(v.Mana256==768+128*(rank-1)&&v.Eval("calc1")==Math.Min(24,rank+2)
                &&v.Damage256(false)==Basis(rank,4)*(100+6*hard)/100&&v.Damage256(true)==Basis(rank,8)*(100+6*hard)/100,"Independent charged rank/hard-only curve");
        }
    }
    private static void Emission()
    {
        var p=Owner();try
        {
            foreach(int rank in new[]{1,20,22,255})foreach(var d in new[]{new Point2D(10,0),new(10,10),new(0,10),new(-10,10),new(-10,0),new(-10,-10),new(0,-10),new(10,-10)})
            {
                var row=SkillMechanics.For(CombatAbility.ChargedBolt);var v=new SkillValues(row,k=>k==row.SourceKey?rank:0,_=>0,99);var aim=new Point3D(p.X+d.X,p.Y+d.Y,p.Z);
                var shots=SorceressSpellMissiles.PrepareCharged(p,aim,v,TimedEffects.Now);
                Require(shots.Length==Math.Min(24,rank+2)&&Own(p).Length==0,"Charged count/cap or preparation publication");
                for(int i=0;i<shots.Length;i++)
                {
                    var s=shots[i];Require(s.Rank==rank&&s.Frames==77&&s.VelocityFp8==2304&&s.Charged is{Next:1} c&&c.Ordinal==i
                        &&c.Path.SequenceEqual(ChargedBolts.Path(ChargedBolts.Seed(aim,i),ChargedBolts.Heading(d.X,d.Y),77)),"Charged initializer/path/ordinal");
                }
            }
            int mana=NecromancerEffects.ManaBalance256(p);Require(SorceressCombat.Release(p,CombatAbility.ChargedBolt,Serial.Zero,new(p.X+10,p.Y,p.Z))&&Own(p).Length==3&&NecromancerEffects.ManaBalance256(p)==mana-768,"Charged release pays once");
            Require(!Own(p)[0].Charged.Path.SequenceEqual(Own(p)[1].Charged.Path),"Independent charged paths collapsed");SorceressSpellMissiles.Cancel(p);
            NecromancerEffects.SetMana256(p,767);Require(!SorceressCombat.Release(p,CombatAbility.ChargedBolt,Serial.Zero,new(p.X+10,p.Y,p.Z))&&Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==767,"Unpaid charged volley emitted");
        }finally{FireBoltChecks.Clean(p);}
    }
    private static void Contacts()
    {
        foreach(var (resistance,potion,expected) in new[]{(0,false,76800),(50,false,38400),(100,false,0),(100,true,23040)})
        {
            var p=Owner();var t=FireBoltChecks.Target(p,0);var behind=FireBoltChecks.Target(p,3);try
            {
                t.SetResistance(ResistanceType.Energy,resistance);if(potion){Require(CombatPotions.Apply(p,3),"Charged actual potion");}
                var shots=Prepare(p).Select(x=>x with{Minimum256=25600,Maximum256=25600}).ToArray();Emit(shots);
                int hp=SkillHealth.Balance256(t),peer=SkillHealth.Balance256(behind);Through(p,77);
                Require(hp-SkillHealth.Balance256(t)==expected&&SkillHealth.Balance256(behind)==peer&&Own(p).Length==0,"Independent bolt collision/immunity/potion: "+resistance+"/"+potion);
            }finally{FireBoltChecks.Clean(p,t,behind);}
        }
    }
    private static void Capture()
    {
        var p=Owner();try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Emit(Prepare(p));var captured=Own(p);
            Require(captured.Length>3&&captured.All(x=>x.Modifiers.LightningMastery>0&&x.Rank>1),"Charged real gear/soft rank");
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);Require(Own(p).SequenceEqual(captured),"Charged gear removal changed flight");SorceressSpellMissiles.Cancel(p);
            var snapshot=Prepare(p);Emit(snapshot);snapshot[0].Charged.Path[1]=new(999,999);Require(Own(p)[0].Charged.Path[1]!=new Point2D(999,999),"Prepared charged path aliases pool");
            var pool=SorceressSpellMissiles.Snapshot();pool.Flights.First(x=>x.Owner==p.Serial.Value).Charged.Path[1]=new(888,888);Require(Own(p)[0].Charged.Path[1]!=new Point2D(888,888),"Snapshot charged path aliases pool");
            pool=SorceressSpellMissiles.Snapshot();var first=Own(p)[0];foreach(var bad in new[]{first with{Charged=null},first with{Charged=first.Charged with{Next=0}},first with{Charged=first.Charged with{Ordinal=24}},first with{Charged=first.Charged with{Path=[Point2D.Zero]}},first with{Frames=98}})
            {
                bool refused=false;try{SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==first.Id?bad:x).ToArray()});}catch(InvalidDataException){refused=true;}
                Require(refused&&Own(p).SequenceEqual(pool.Flights.Where(x=>x.Owner==p.Serial.Value)),"Malformed charged restore partially published");
            }
            SorceressSpellMissiles.Cancel(p);
            foreach(string mode in new[]{"active","after-launch","removed"})
            {
                var t=FireBoltChecks.Target(p,0);try
                {
                    TimedEffects.Clear(p);if(mode!="after-launch"){Require(CombatPotions.Apply(p,3),"Charged potion before launch");}
                    var packet=Prepare(p)[0] with{Minimum256=25600,Maximum256=25600};SorceressSpellMissiles.Commit(packet);
                    if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"Charged potion after launch");}
                    if(mode=="removed")
                    {
                        for(int i=0;i<20;i++){Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightningmastery",out _),"Charged hard mastery after launch");}
                        TimedEffects.Clear(p);Require(Own(p).Single()==packet,"Captured charged packet changed after mastery");
                    }
                    int hp=SkillHealth.Balance256(t);Through(p,77);Require(hp-SkillHealth.Balance256(t)==(mode=="removed"?25600:35840),"Charged captured mastery/live potion: "+mode);
                }finally{t.Delete();}
            }
        }finally{FireBoltChecks.Clean(p);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"empty","wall","offline","facet","slow","death","respec","wardrobe"})
        {
            var p=Owner();Item wall=null;try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                Emit(Prepare(p));var shots=Own(p);
                if(mode=="slow"){Require(shots.All(s=>s.VelocityFp8==1152&&s.Minimum256==256&&s.Maximum256==512),"Captured charged slow velocity/damage");AmazonSlowMissiles.Clear(p);}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(p.Location,p.Map);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="death"){p.Kill();}if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Charged wardrobe");}
                Through(p,77,mode!="offline");Require(Own(p).Length==0,"Charged lifecycle: "+mode);
            }finally{wall?.Delete();FireBoltChecks.Clean(p);}
        }
        var owner=Owner();try
        {
            int mana=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,CombatAbility.ChargedBolt,null,new(owner.X+10,owner.Y,owner.Z),Core.TickCount),"Charged cancel windup");long effect=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);
            Require(!ClassCombat.ResolveAction(owner,effect)&&NecromancerEffects.ManaBalance256(owner)==mana&&Own(owner).Length==0,"Cancelled charged volley paid/emitted");
            owner.MoveToWorld(new(1497,1628,10),Map.Trammel);Require(!SorceressCombat.Release(owner,CombatAbility.ChargedBolt,Serial.Zero,owner.Location)&&NecromancerEffects.ManaBalance256(owner)==mana,"Town charged volley paid/emitted");
        }finally{FireBoltChecks.Clean(owner);}
    }
    private static void Save()
    {
        var p=Owner();Emit(Prepare(p));Through(p,7);var held=Own(p).Select(x=>x with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}).ToArray();
        Require(held.Length==3&&held.All(x=>x.Frame==7&&x.Charged.Next>1),"Saved charged partial path");
        var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>held.FirstOrDefault(h=>h.Id==x.Id)??x).ToArray()});
        var saved=new Saved(p.Serial.Value,p.Map.MapID,p.Location,NecromancerEffects.ManaBalance256(p),held,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(s.Generation<2&&Own(p).SequenceEqual(s.Flights)&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillBindings.Instance.Get(p).Right==CombatAbility.ChargedBolt,"Saved charged capture/path/mana/binding");
        var account=FixtureNetwork.Authenticate(network,p,"charged-flight-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var c=s.Flights[0].Charged;var next=c.Path[c.Next];var t=FireBoltChecks.Target(p,0);t.MoveToWorld(new(c.OriginX+next.X,c.OriginY+next.Y,p.Z),p.Map);int hp=SkillHealth.Balance256(t);
        var pool=SorceressSpellMissiles.Snapshot();var copies=s.Flights.Select((x,i)=>x with{Id=pool.NextId+i+1,NextUtc=TimedEffects.Now}).ToArray();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Concat(copies).ToArray(),NextId=copies.Last().Id});var deadline=TimedEffects.Now.AddSeconds(5);
        void Observe()
        {
            if((SkillHealth.Balance256(t)==hp||Own(p).Length>s.Flights.Length)&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
            try
            {
                Require(SkillHealth.Balance256(t)<hp&&Own(p).SequenceEqual(s.Flights),"Resumed charged movement/contact or held originals");t.Delete();p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("charged-flight-reload-result: passed");
            }catch(Exception e){Log.Error(e,"charged-flight-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,0);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);string label=quick?"charged-flight-quick":"charged-flight-right";
        var account=FixtureNetwork.Authenticate(network,p,"charged-flight-input-");bool began=false;long deadline=Core.TickCount+6000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Charged real input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.ChargedBolt&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==hp,"Charged input paid/hit before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==hp||Own(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-768&&hp-SkillHealth.Balance256(t)>=1536&&hp-SkillHealth.Balance256(t)<3072,"Charged actual once-payment/three independent contacts");FireBoltChecks.Clean(p,t);account.Delete();Log.Information("{Label}-result: passed",label);
            }catch(Exception e){FireBoltChecks.Clean(p,t);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.ChargedBolt,(int)DiabloClass.Sorceress,p.X+10,p.Y,p.Z);Observe();});
    }
}
