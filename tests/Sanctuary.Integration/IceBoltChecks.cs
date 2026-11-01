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

public static class IceBoltChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IceBoltChecks));
    private sealed record Saved(uint Owner,uint Target,int Map,Point3D Point,int Mana,int Life,SorceressSpellFlight Flight,DateTime ChillUntil,SourceColdState Cold,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ice-bolt-fixtures.json");
    public static void Configure()
    {
        Register("ice-bolt-check",Check);Register("ice-bolt-reload-check",()=>FixtureNetwork.Connected("ice-bolt-reload-result",Reload));
        foreach(bool quick in new[]{false,true})
        {bool q=quick;string label=q?"ice-bolt-quick":"ice-bolt-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Player Ice Bolt source flight,cold state,input and saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(){var p=FireBoltChecks.Owner(CombatAbility.IceBolt,7060);p.Name="IceBoltFixture";return p;}
    private static long Create(PlayerMobile p)=>SorceressSpellMissiles.Commit(SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.IceBolt),TimedEffects.Now));
    private static SorceressSpellFlight State(long id)=>FireBoltChecks.State(id);
    private static DateTime Finish(long id)
    {
        DateTime at=TimedEffects.Now;
        for(int i=0;i<50&&State(id) is{} s;i++){at=s.NextUtc;SorceressSpellMissiles.Tick(at,true);}
        Require(State(id)==null,"Ice Bolt did not expire/collide");return at;
    }
    private static TimedActor Cold(Mobile t)
    {Require(TimedEffects.TryActor(t,out var a)&&a.Cold!=null,"Missing Ice Bolt cold state");return a;}
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.IceBolt);string[] synergies=["Frost Nova","Ice Blast","Glacial Spike","Blizzard","Frozen Orb"];
        int Basis(int rank,bool max)
        {
            int result=max?10:6;
            for(int level=2;level<=rank;level++){result+=(level<=8?2:level<=16?4:level<=22?6:level<=28?8:10)+(max?1:0);}
            return result*128;
        }
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(row,k=>k=="Ice Bolt"?rank:200,k=>synergies.Contains(k)?hard:0,99);
            Require(v.Mana256==768&&v.DurationFrames==150+35*(rank-1)
                &&v.Damage256(false)==Basis(rank,false)*(100+75*hard)/100
                &&v.Damage256(true)==Basis(rank,true)*(100+75*hard)/100,"Independent Ice Bolt rank/soft-vs-hard supplier: "+rank+"/"+hard);
        }
    }
    private static void Check()
    {
        Goldens();var p=Owner();var t=FireBoltChecks.Target(p,14);var behind=FireBoltChecks.Target(p,17);
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t),other=SkillHealth.Balance256(behind);
            Require(SorceressCombat.Release(p,CombatAbility.IceBolt,Serial.Zero,new(p.X+10,p.Y,p.Z)),"Learned Ice Bolt release");
            var s=SorceressSpellMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value);
            Require(s is{Frame:0,Frames:50,VelocityFp8:2304,Vx:0.28125,Vy:0,Minimum256:768,Maximum256:1280,ColdFrames:150}
                &&NecromancerEffects.ManaBalance256(p)==mana-768&&SkillHealth.Balance256(t)==life
                &&(!TimedEffects.TryActor(t,out var cold)||cold.Cold==null),"Ice Bolt initial factory/payment/status");
            FireBoltChecks.Through(s.Id,40);Require(State(s.Id)!=null&&SkillHealth.Balance256(t)==life,"Ice Bolt used Fire Bolt velocity/instant damage");
            DateTime at=Finish(s.Id);int loss=life-SkillHealth.Balance256(t);var a=Cold(t);
            Require(loss>=768&&loss<1280&&SkillHealth.Balance256(behind)==other&&a.Cold.Source==p.Serial.Value&&a.ChillPercent==50
                &&Math.Abs((a.ChillUntil-at).TotalMilliseconds-6000)<2,"Ice Bolt damage/contact/cold/source or old12tile cap");
            TimedEffects.Settle(t,a.ChillUntil);Require(TimedEffects.Chill(t,a.ChillUntil)==0,"Natural six-second cold expiry");
            NecromancerEffects.SetMana256(p,767);Require(!SorceressCombat.Release(p,CombatAbility.IceBolt,Serial.Zero,new(p.X+10,p.Y,p.Z))
                &&NecromancerEffects.ManaBalance256(p)==767&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Lost mana Ice Bolt emitted/paid");
        }
        finally{FireBoltChecks.Clean(p,t,behind);}
        Capture();Resistances();Controls();Legacy();LoadedAging();Save();
        Log.Information("Ice Bolt:765independent rank/hard-synergy suppliers,50frame/12velocity flight beyond12tiles,captured gear/length,typed potion,resisted/difficulty cold and lifecycle/legacy controls pass");
    }
    private static void Capture()
    {
        var p=Owner();try
        {
            var helm=PoisonDaggerChecks.Named(p,"Nightwing's Veil");long id=Create(p);var captured=State(id);
            Require(captured.Rank==3&&captured.ColdFrames==220&&captured.Modifiers.ColdMastery>0,"Actual Nightwing cold/rank capture");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Require(State(id)==captured,"Ice Bolt reread removed gear");SorceressSpellMissiles.Cancel(p);
            id=Create(p);var fresh=State(id);Require(fresh.Rank==1&&fresh.ColdFrames==150&&fresh.Minimum256<captured.Minimum256&&fresh.Modifiers.ColdMastery==0,"New Ice Bolt retained old gear");
            var pool=SorceressSpellMissiles.Snapshot();bool rejected=false;
            try{SorceressSpellMissiles.Restore(pool with{Flights=[fresh with{ColdFrames=0}]});}catch(InvalidDataException){rejected=true;}
            Require(rejected&&State(id)==fresh,"Missing cold payload accepted/partially replaced pool");SorceressSpellMissiles.Cancel(p);
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.frostnova",out _),"Actual hard Frost Nova synergy");
            id=Create(p);Require(State(id) is{Rank:1,ColdFrames:150,Minimum256:883,Maximum256:1472},"Hard synergy altered length or missed FP endpoint");
        }
        finally{FireBoltChecks.Clean(p);}
    }
    private static void Resistances()
    {
        foreach(var (resist,potion,expected,frames) in new[]{(0,false,25600,150),(50,false,12800,75),(100,false,0,0),(100,true,7680,0),(0,true,35840,150)})
        {
            var p=Owner();var t=FireBoltChecks.Target(p,6);try
            {
                t.SetResistance(ResistanceType.Cold,resist);if(potion){Require(CombatPotions.Apply(p,3),"Actual Ice Bolt potion");}
                var packet=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.IceBolt),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};
                int before=SkillHealth.Balance256(t);long id=SorceressSpellMissiles.Commit(packet);DateTime at=Finish(id);
                Require(before-SkillHealth.Balance256(t)==expected,"Cold resistance/immunity or separate potion lanes");
                if(frames==0){Require(!TimedEffects.TryActor(t,out var a)||a.Cold==null,"Cold immune target chilled by potion");}
                else{Require(Math.Abs((Cold(t).ChillUntil-at).TotalMilliseconds-frames*40)<2,"Resisted cold length");}
            }
            finally{FireBoltChecks.Clean(p,t);}
        }
        foreach(var difficulty in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {
            var p=Owner();var t=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};try
            {
                MonsterVariants.Assign(t,difficulty);t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
                t.SetResistance(ResistanceType.Cold,0);TimedEffects.PreventHealing(t,TimedEffects.Now);long id=Create(p);DateTime at=Finish(id);var a=Cold(t);
                Require(a.ChillPercent==(difficulty==MonsterDifficulty.Nightmare?40:25)
                    &&Math.Abs((a.ChillUntil-at).TotalMilliseconds-(difficulty==MonsterDifficulty.Nightmare?3000:1480))<2,"Actual variant cold effect/duration/truncation");
            }
            finally{FireBoltChecks.Clean(p,t);}
        }
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"miss","wall","offline","facet","slow","death","respec","wardrobe"})
        {
            var p=Owner();var t=FireBoltChecks.Target(p,6);Item wall=null;try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                int before=SkillHealth.Balance256(t);long id=Create(p);var s=State(id);
                if(mode=="miss"){t.MoveToWorld(new(t.X,t.Y+3,t.Z),t.Map);}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="slow"){Require(s is{VelocityFp8:1152,Minimum256:384,Maximum256:640,ColdFrames:150},"Slow altered cold length or missed endpoints/velocity");AmazonSlowMissiles.Clear(p);}
                if(mode=="death"){p.Kill();Require(State(id)==null,"Death retained Ice Bolt");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(State(id)==null,"Respec retained Ice Bolt");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&State(id)==null,"Wardrobe retained Ice Bolt");}
                FireBoltChecks.Through(id,50,mode!="offline");Require(State(id)==null&&(SkillHealth.Balance256(t)<before)==(mode=="slow"),"Ice Bolt lifecycle control: "+mode);
            }
            finally{wall?.Delete();FireBoltChecks.Clean(p,t);}
        }
    }
    private static void Legacy()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"legacy-fire-bolt-fixture.json")));
        var raw=doc.RootElement.GetProperty("Flight");Require(!raw.TryGetProperty("ColdFrames",out _),"Legacy control is not the accepted old schema");
        var old=raw.Deserialize<SorceressSpellFlight>();Require(old.ColdFrames==0&&old.Ability==CombatAbility.FireBolt,"Legacy optional cold extension");
        var p=Owner();try
        {
            var pool=SorceressSpellMissiles.Snapshot();var copy=old with{Id=pool.NextId+1,Owner=p.Serial.Value,NextUtc=TimedEffects.Now.AddHours(1)};
            SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Append(copy).ToArray(),NextId=copy.Id});Require(State(copy.Id)==copy,"Legacy Fire Bolt restore after cold extension");
        }
        finally{FireBoltChecks.Clean(p);}
    }
    private static void Save()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,6);int mana=NecromancerEffects.ManaBalance256(p);Finish(Create(p));var cold=Cold(t);
        cold.ChillUntil=TimedEffects.Now.Add(FixtureSpace.Retention);long id=Create(p);var s=State(id) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==id?s:x).ToArray()});
        var saved=new Saved(p.Serial.Value,t.Serial.Value,p.Map.MapID,p.Location,mana,SkillHealth.Balance256(t),s,cold.ChillUntil,cold.Cold,0);
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void LoadedAging()
    {
        // Existing restart policy consumes overdue loaded frames without
        // replaying motion/damage. A complete elapsed lifetime retires it.
        foreach(int elapsed in new[]{0,80,2000})
        {
            var p=Owner();var t=FireBoltChecks.Target(p,14);try
            {
                int before=SkillHealth.Balance256(t);long id=Create(p);var original=State(id);SorceressSpellMissiles.Restore(SorceressSpellMissiles.Snapshot());
                SorceressSpellMissiles.Tick(original.NextUtc.AddMilliseconds(elapsed),true);var after=State(id);
                Require(SkillHealth.Balance256(t)==before&&(elapsed==2000?after==null:
                    after!=null&&after.Frame==1+elapsed/40&&after.X==original.X&&after.Y==original.Y),"Harmless loaded aging: "+elapsed);
                Finish(id);if(elapsed==0){Require(SkillHealth.Balance256(t)<before,"One loaded frame incorrectly prevented remaining source travel");}
            }
            finally{FireBoltChecks.Clean(p,t);}
        }
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var savedTarget=World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&State(s.Flight.Id)==s.Flight&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillBindings.Instance.Get(p).Right==CombatAbility.IceBolt
            &&SkillHealth.Balance256(savedTarget)==s.Life&&Cold(savedTarget).Cold==s.Cold&&Cold(savedTarget).ChillUntil==s.ChillUntil,"Saved Ice Bolt/cold identity,capture,deadline,resources or binding");
        var account=FixtureNetwork.Authenticate(network,p,"ice-bolt-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);
        // The held target stays off the resumed ray without resetting its wound/status.
        var heldPoint=savedTarget.Location;savedTarget.MoveToWorld(new(p.X+6,p.Y+2,p.Z),p.Map);
        var t=FireBoltChecks.Target(p,13);int life=SkillHealth.Balance256(t);
        void Start()
        {
        var pool=SorceressSpellMissiles.Snapshot();var copy=s.Flight with{Id=pool.NextId+1,NextUtc=TimedEffects.Now};
        SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Append(copy).ToArray(),NextId=copy.Id});var deadline=TimedEffects.Now.AddSeconds(5);
        var last=copy;
        void Observe()
        {
            if(State(copy.Id) is{} observed){last=observed;}
            if(SkillHealth.Balance256(t)==life&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
            try
            {
                Require(SkillHealth.Balance256(t)<life&&Cold(t).Cold.Source==p.Serial.Value&&State(copy.Id)==null&&State(s.Flight.Id)==s.Flight,
                    "Reload did not resume actual Ice Bolt travel/damage/cold: loss="+(life-SkillHealth.Balance256(t))+" state="+JsonSerializer.Serialize(State(copy.Id))
                    +" last="+JsonSerializer.Serialize(last)+" owner="+p.Location+" target="+t.Location+" held="+savedTarget.Location+" online="+network.Running+" originalEqual="+(State(s.Flight.Id)==s.Flight));
                Require(Cold(savedTarget).ChillUntil==s.ChillUntil&&SkillHealth.Balance256(savedTarget)==s.Life,"Reload refreshed old cold/wounds");
                t.Delete();savedTarget.MoveToWorld(heldPoint,p.Map);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("ice-bolt-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"ice-bolt-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
        }
        FixtureClock.NextLoop("ice-bolt-reload-result",Start);
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,14);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);string label=quick?"ice-bolt-quick":"ice-bolt-right";
        var account=FixtureNetwork.Authenticate(network,p,"ice-bolt-input-");bool began=false;long deadline=Core.TickCount+7000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Ice Bolt real input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.IceBolt&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==life,"Input paid/hit before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                var cold=Cold(t);Require(began&&NecromancerEffects.ManaBalance256(p)==mana-768&&cold.Cold.Source==p.Serial.Value&&cold.ChillUntil>TimedEffects.Now
                    &&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Input payment/collision/real cold state");
                FireBoltChecks.Clean(p,t);account.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){FireBoltChecks.Clean(p,t);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.IceBolt,(int)DiabloClass.Sorceress,p.X+10,p.Y,p.Z);Observe();});
    }
}
