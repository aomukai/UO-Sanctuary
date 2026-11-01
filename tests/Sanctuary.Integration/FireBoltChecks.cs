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

public static class FireBoltChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireBoltChecks));
    private sealed record Saved(uint Owner,int Map,Point3D Point,int Mana,SorceressSpellFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-bolt-fixtures.json");
    public static void Configure()
    {
        Register("fire-bolt-check",Check);Register("fire-bolt-reload-check",()=>FixtureNetwork.Connected("fire-bolt-reload-result",Reload));
        foreach(bool quick in new[]{false,true})
        {bool q=quick;string label=q?"fire-bolt-quick":"fire-bolt-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Player Fire Bolt capture,source flight,input and saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="FireBoltFixture",Player=true,Body=0x190};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fire Bolt Strength");}
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.firebolt",out _),"Learn Fire Bolt");
        FixtureSpace.Corridor(p,7100,32,3000);p.Hits=p.HitsMax;NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FireBolt)&&SkillBindings.Instance.Assign(p,false,CombatAbility.FireBolt)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FireBolt),"Fire Bolt bindings");return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx)
    {
        var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);
        foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static SorceressSpellFlight State(long id)=>SorceressSpellMissiles.Snapshot().Flights.FirstOrDefault(s=>s.Id==id);
    private static long Create(PlayerMobile p)
    {var flight=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.FireBolt),TimedEffects.Now);return SorceressSpellMissiles.Commit(flight);}
    private static void Through(long id,int frames,bool actions=true)
    {for(int i=0;i<frames&&State(id) is{} s;i++){SorceressSpellMissiles.Tick(s.NextUtc,actions);}}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {SorceressSpellMissiles.Cancel(p);AmazonSlowMissiles.Clear(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void Check()
    {
        var p=Owner();var first=Target(p,16);var second=Target(p,20);
        try
        {
            var values=SkillMechanics.Values(p,CombatAbility.FireBolt);Require(values.Mana256==640&&values.Damage256(false)==768&&values.Damage256(true)==1536,"Pinned Fire Bolt rank1");
            int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(first),behind=SkillHealth.Balance256(second);
            Require(SorceressCombat.Release(p,CombatAbility.FireBolt,Serial.Zero,new(p.X+10,p.Y,p.Z)),"Learned ground release");
            var s=SorceressSpellMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value);
            Require(s is{Frame:0,Frames:50,VelocityFp8:3840,Vx:0.46875,Vy:0,Minimum256:768,Maximum256:1536}
                &&NecromancerEffects.ManaBalance256(p)==mana-640&&SkillHealth.Balance256(first)==life,"Fire Bolt factory/cost or immediate hit");
            Through(s.Id,24);Require(SkillHealth.Balance256(first)==life&&State(s.Id)!=null,"Fire Bolt hit without source travel");
            Through(s.Id,50);int loss=life-SkillHealth.Balance256(first);
            Require(loss>=768&&loss<1536&&SkillHealth.Balance256(second)==behind&&State(s.Id)==null,"Fire Bolt retained12tile cap,pierced,or changed its FP packet");
            NecromancerEffects.SetMana256(p,639);Require(!SorceressCombat.Release(p,CombatAbility.FireBolt,Serial.Zero,new(p.X+10,p.Y,p.Z))
                &&NecromancerEffects.ManaBalance256(p)==639&&!SorceressSpellMissiles.Snapshot().Flights.Any(s=>s.Owner==p.Serial.Value),"Lost mana emitted/paid a bolt");
        }
        finally{Clean(p,first,second);}
        Capture();Resistances();Controls();Save();
        Log.Information("Fire Bolt:source50frame/20velocity single-hit flight beyond12tiles,FP damage/cost,captured actual gear/ranks,Slow Missiles,malformed restore,impact potion and lifecycle controls pass");
    }
    private static void Resistances()
    {
        foreach(var (resistance,potion,expected) in new[]{(50,false,12800),(100,false,0),(100,true,7680)})
        {
            var p=Owner();var t=Target(p,6);try
            {
                t.SetResistance(ResistanceType.Fire,resistance);
                if(potion){Require(CombatPotions.Apply(p,3),"Resistance control potion");}
                int before=SkillHealth.Balance256(t);var packet=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),
                    SkillMechanics.Values(p,CombatAbility.FireBolt),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};
                long id=SorceressSpellMissiles.Commit(packet);Through(id,50);
                Require(State(id)==null&&before-SkillHealth.Balance256(t)==expected,
                    "Fire resistance/immunity or separate T29 element lanes: "+resistance+"/"+potion);
            }
            finally{Clean(p,t);}
        }
    }
    private static void Capture()
    {
        var p=Owner();var t=Target(p,6);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");long id=Create(p);var captured=State(id);
            Require(captured.Modifiers.FireMastery>0&&captured.Rank>1,"Real Eschuta did not supply mastery/soft ranks");
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);Require(State(id)==captured,"Flight reread removed gear");
            SorceressSpellMissiles.Cancel(p);long freshId=Create(p);var fresh=State(freshId);
            Require(fresh.Rank==1&&fresh.Minimum256<captured.Minimum256&&fresh.Modifiers.FireMastery==0,"Fresh packet retained old gear");
            var pool=SorceressSpellMissiles.Snapshot();bool refused=false;
            try{SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Append(fresh with{Frames=0}).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&State(freshId)==fresh,"Malformed restore partially replaced pool");SorceressSpellMissiles.Cancel(p);
            foreach(string mode in new[]{"active","after-launch","removed"})
            {
                TimedEffects.Clear(p);if(mode!="after-launch"){Require(CombatPotions.Apply(p,3),"Actual potion");}
                var fixedPacket=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.FireBolt),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};
                id=SorceressSpellMissiles.Commit(fixedPacket);
                if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"Potion after launch");}else if(mode=="removed"){TimedEffects.Clear(p);}
                int before=SkillHealth.Balance256(t);Through(id,50);
                Require(before-SkillHealth.Balance256(t)==(mode=="removed"?25600:35840),"T29 contact eligibility/four components compounded or lost");
            }
        }
        finally{Clean(p,t);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"miss","wall","offline","facet","slow","death","respec","wardrobe"})
        {
            var p=Owner();var t=Target(p,8);Item wall=null;try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                int before=SkillHealth.Balance256(t);long id=Create(p);var s=State(id);
                if(mode=="miss"){t.MoveToWorld(new(t.X,t.Y+3,t.Z),t.Map);}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="slow"){Require(s.VelocityFp8==1920&&s.Minimum256==384&&s.Maximum256==768,"Slow factory capture");AmazonSlowMissiles.Clear(p);}
                if(mode=="death"){p.Kill();Require(State(id)==null,"Death retained flight");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(State(id)==null,"Respec retained flight");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&State(id)==null,"Wardrobe retained flight");}
                Through(id,50,mode!="offline");Require(State(id)==null&&(SkillHealth.Balance256(t)<before)==(mode=="slow"),"Control hit/expiry: "+mode);
            }
            finally{wall?.Delete();Clean(p,t);}
        }
    }
    private static void Save()
    {
        var p=Owner();int mana=NecromancerEffects.ManaBalance256(p);long id=Create(p);var s=State(id) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==id?s:x).ToArray()});
        var saved=new Saved(p.Serial.Value,p.Map.MapID,p.Location,mana,s,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(s.Generation<2&&State(s.Flight.Id)==s.Flight&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&SkillBindings.Instance.Get(p).Right==CombatAbility.FireBolt,"Saved Fire Bolt identity/capture/resources/binding");
        var account=FixtureNetwork.Authenticate(network,p,"fire-bolt-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var t=Target(p,16);int life=SkillHealth.Balance256(t);
        var pool=SorceressSpellMissiles.Snapshot();var copy=s.Flight with{Id=pool.NextId+1,NextUtc=TimedEffects.Now};
        SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Append(copy).ToArray(),NextId=copy.Id});var deadline=TimedEffects.Now.AddSeconds(4);
        void Observe()
        {
            if(SkillHealth.Balance256(t)==life&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
            try
            {
                Require(SkillHealth.Balance256(t)<life&&State(copy.Id)==null&&State(s.Flight.Id)==s.Flight,"Reload failed real source travel/damage or changed held original");
                t.Delete();p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("fire-bolt-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"fire-bolt-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Target(p,16);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);string label=quick?"fire-bolt-quick":"fire-bolt-right";
        var account=FixtureNetwork.Authenticate(network,p,"fire-bolt-input-");bool began=false;long deadline=Core.TickCount+6000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Fire Bolt real input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.FireBolt&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==life,"Input paid/hit before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-640&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Input did not pay once/stop on first collision");
                Clean(p,t);account.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean(p,t);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.FireBolt,(int)DiabloClass.Sorceress,p.X+10,p.Y,p.Z);Observe();});
    }
}
