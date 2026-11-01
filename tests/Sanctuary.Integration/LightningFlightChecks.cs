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

public static class LightningFlightChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LightningFlightChecks));
    private sealed record Saved(uint Owner,int Map,Point3D Point,int Mana,SorceressSpellFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lightning-flight-fixtures.json");
    public static void Configure()
    {
        Register("lightning-flight-check",Check);Register("lightning-flight-reload-check",()=>FixtureNetwork.Connected("lightning-flight-reload-result",Reload));
        foreach(bool quick in new[]{false,true})
        {bool q=quick;string label=q?"lightning-flight-quick":"lightning-flight-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Player Lightning capture,source flight,input and saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.ChargedBolt,6380);
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightning",out _),"Lightning prerequisite/train");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Lightning)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Lightning)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Lightning),"Lightning bindings");return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx)=>FireBoltChecks.Target(p,dx);
    private static SorceressSpellFlight State(long id)=>FireBoltChecks.State(id);
    private static long Create(PlayerMobile p)
    {var flight=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Lightning),TimedEffects.Now);return SorceressSpellMissiles.Commit(flight);}
    private static void Through(long id,int frames,bool actions=true)=>FireBoltChecks.Through(id,frames,actions);
    private static void Clean(PlayerMobile p,params Mobile[] targets)=>FireBoltChecks.Clean(p,targets);
    private static void Check()
    {
        var p=Owner();var first=Target(p,5);var second=Target(p,10);
        try
        {
            var values=SkillMechanics.Values(p,CombatAbility.Lightning);Require(values.Mana256==2048&&values.Damage256(false)==256&&values.Damage256(true)==11059,"Pinned Lightning rank1");
            int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(first),behind=SkillHealth.Balance256(second);
            Require(SorceressCombat.Release(p,CombatAbility.Lightning,Serial.Zero,new(p.X+10,p.Y,p.Z)),"Learned ground release");
            var s=SorceressSpellMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value);
            Require(s is{Frame:0,Frames:25,VelocityFp8:5760,Vx:0.703125,Vy:0,Minimum256:256,Maximum256:11059}
                &&NecromancerEffects.ManaBalance256(p)==mana-2048&&SkillHealth.Balance256(first)==life,"Lightning factory/cost or immediate hit");
            Through(s.Id,2);Require(SkillHealth.Balance256(first)==life&&State(s.Id)!=null,"Lightning hit without source travel");
            Through(s.Id,25);int loss=life-SkillHealth.Balance256(first);
            Require(loss>=256&&loss<11059&&SkillHealth.Balance256(second)<behind&&State(s.Id)==null,"Lightning failed passing contact or changed its FP packet");
            NecromancerEffects.SetMana256(p,2047);Require(!SorceressCombat.Release(p,CombatAbility.Lightning,Serial.Zero,new(p.X+10,p.Y,p.Z))
                &&NecromancerEffects.ManaBalance256(p)==2047&&!SorceressSpellMissiles.Snapshot().Flights.Any(s=>s.Owner==p.Serial.Value),"Lost mana emitted/paid a bolt");
        }
        finally{Clean(p,first,second);}
        Capture();Resistances();Contacts();Controls();Save();
        Log.Information("Lightning:source25frame/30velocity Size2 passing flight/LastCollision,FP damage/cost,captured actual gear/ranks,Slow Missiles,malformed restore,impact potion and lifecycle controls pass");
    }
    private static void Contacts()
    {
        // Same target remains excluded on every overlapping cell/update. A new
        // collision replaces the last target rather than creating a lifetime set.
        var p=Owner();var first=Target(p,5);var next=Target(p,10);try
        {
            var flight=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Lightning),TimedEffects.Now)
                with{Minimum256=25600,Maximum256=25600};
            long id=SorceressSpellMissiles.Commit(flight);int a=SkillHealth.Balance256(first),b=SkillHealth.Balance256(next);
            Through(id,6);Require(State(id)?.LastCollision==first.Serial.Value&&a-SkillHealth.Balance256(first)==25600,"Size2 contact/retained last collision");
            Through(id,5);Require(a-SkillHealth.Balance256(first)==25600,"Overlapping update hit the remembered target again");
            Through(id,15);Require(State(id)==null&&b-SkillHealth.Balance256(next)==25600,"Second target passing hit/expiry");
            // Size2/footprint2 reaches a target one tile beside the centreline.
            first.MoveToWorld(new(p.X+5,p.Y+1,p.Z),p.Map);next.MoveToWorld(new(p.X+10,p.Y+3,p.Z),p.Map);
            id=SorceressSpellMissiles.Commit(SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Lightning),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600});
            a=SkillHealth.Balance256(first);b=SkillHealth.Balance256(next);Through(id,25);
            Require(a-SkillHealth.Balance256(first)==25600&&SkillHealth.Balance256(next)==b,"Size2 source footprint versus outside side target");
        }
        finally{Clean(p,first,next);}
    }
    private static void Resistances()
    {
        foreach(var (resistance,potion,expected) in new[]{(50,false,12800),(100,false,0),(100,true,7680)})
        {
            var p=Owner();var t=Target(p,6);try
            {
                t.SetResistance(ResistanceType.Energy,resistance);
                if(potion){Require(CombatPotions.Apply(p,3),"Resistance control potion");}
                int before=SkillHealth.Balance256(t);var packet=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),
                    SkillMechanics.Values(p,CombatAbility.Lightning),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};
                long id=SorceressSpellMissiles.Commit(packet);Through(id,25);
                Require(State(id)==null&&before-SkillHealth.Balance256(t)==expected,
                    "Lightning resistance/immunity or separate T29 element lanes: "+resistance+"/"+potion);
            }
            finally{Clean(p,t);}
        }
    }
    private static void Capture()
    {
        var p=Owner();var t=Target(p,6);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");long id=Create(p);var captured=State(id);
            Require(captured.Modifiers.LightningMastery>0&&captured.Rank>1,"Real Eschuta did not supply mastery/soft ranks");
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);Require(State(id)==captured,"Flight reread removed gear");
            SorceressSpellMissiles.Cancel(p);long freshId=Create(p);var fresh=State(freshId);
            Require(fresh.Rank==1&&fresh.Minimum256<captured.Minimum256&&fresh.Modifiers.LightningMastery==0,"Fresh packet retained old gear");
            var pool=SorceressSpellMissiles.Snapshot();bool refused=false;
            try{SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Append(fresh with{Frames=0}).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&State(freshId)==fresh,"Malformed restore partially replaced pool");SorceressSpellMissiles.Cancel(p);
            foreach(string mode in new[]{"active","after-launch","removed"})
            {
                TimedEffects.Clear(p);if(mode!="after-launch"){Require(CombatPotions.Apply(p,3),"Actual potion");}
                var fixedPacket=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Lightning),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};
                id=SorceressSpellMissiles.Commit(fixedPacket);
                if(mode=="removed")
                {
                    for(int rank=0;rank<20;rank++){Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightningmastery",out _),"Train mastery during Lightning flight");}
                    Require(State(id)==fixedPacket&&SorceressCombat.Passive(p,CombatAbility.LightningMastery)==278,"In-flight Lightning changed capture");
                }
                if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"Potion after launch");}else if(mode=="removed"){TimedEffects.Clear(p);}
                int before=SkillHealth.Balance256(t);Through(id,25);
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
                if(mode=="slow"){Require(s.VelocityFp8==2880&&s.Minimum256==128&&s.Maximum256==5529,"Slow factory capture");AmazonSlowMissiles.Clear(p);}
                if(mode=="death"){p.Kill();Require(State(id)==null,"Death retained flight");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(State(id)==null,"Respec retained flight");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&State(id)==null,"Wardrobe retained flight");}
                Through(id,25,mode!="offline");Require(State(id)==null&&(SkillHealth.Balance256(t)<before)==(mode=="slow"),"Control hit/expiry: "+mode);
            }
            finally{wall?.Delete();Clean(p,t);}
        }
        var owner=Owner();try
        {
            int before=NecromancerEffects.ManaBalance256(owner);
            Require(ClassCombat.Perform(owner,CombatAbility.Lightning,null,new(owner.X+10,owner.Y,owner.Z),Core.TickCount),"Lightning cancel windup");
            long effect=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);
            Require(!ClassCombat.ResolveAction(owner,effect)&&NecromancerEffects.ManaBalance256(owner)==before&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==owner.Serial.Value),"Cancelled Lightning paid/emitted");
            owner.MoveToWorld(new(1497,1628,10),Map.Trammel);
            Require(!SorceressCombat.Release(owner,CombatAbility.Lightning,Serial.Zero,owner.Location)&&NecromancerEffects.ManaBalance256(owner)==before,"Town Lightning paid/emitted");
        }
        finally{Clean(owner);}
    }
    private static void Save()
    {
        var p=Owner();int mana=NecromancerEffects.ManaBalance256(p);var t=Target(p,5);long id=Create(p);Through(id,7);Require(State(id)?.LastCollision==t.Serial.Value,"Save lost first collision");t.Internalize();var s=State(id) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==id?s:x).ToArray()});
        var saved=new Saved(p.Serial.Value,p.Map.MapID,p.Location,mana,s,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(s.Generation<2&&State(s.Flight.Id)==s.Flight&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&SkillBindings.Instance.Get(p).Right==CombatAbility.Lightning,"Saved Lightning identity/capture/resources/binding");
        var account=FixtureNetwork.Authenticate(network,p,"lightning-flight-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var t=Target(p,10);int life=SkillHealth.Balance256(t);
        var pool=SorceressSpellMissiles.Snapshot();var copy=s.Flight with{Id=pool.NextId+1,NextUtc=TimedEffects.Now};
        SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Append(copy).ToArray(),NextId=copy.Id});var deadline=TimedEffects.Now.AddSeconds(4);
        void Observe()
        {
            if((SkillHealth.Balance256(t)==life||State(copy.Id)!=null)&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
            try
            {
                Require(SkillHealth.Balance256(t)<life&&State(copy.Id)==null&&State(s.Flight.Id)==s.Flight,"Reload travel/damage or held original: loss="+(life-SkillHealth.Balance256(t))+" copy="+State(copy.Id)+" original="+(State(s.Flight.Id)==s.Flight)+" target="+t.Serial.Value+" previous="+s.Flight.LastCollision);
                t.Delete();p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("lightning-flight-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"lightning-flight-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Target(p,10);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);string label=quick?"lightning-flight-quick":"lightning-flight-right";
        var account=FixtureNetwork.Authenticate(network,p,"lightning-flight-input-");bool began=false;long deadline=Core.TickCount+6000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Lightning real input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.Lightning&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==life,"Input paid/hit before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==life||SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-2048&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Input did not pay once/expire after passing collision");
                Clean(p,t);account.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean(p,t);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.Lightning,(int)DiabloClass.Sorceress,p.X+10,p.Y,p.Z);Observe();});
    }
}
