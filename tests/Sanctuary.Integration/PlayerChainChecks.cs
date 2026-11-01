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

public static class PlayerChainChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerChainChecks));
    private sealed record Saved(uint Owner,int Map,Point3D Point,int Mana,ChainFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-chain-fixtures.json");
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("player-chain-offline","Actual disconnect suppresses prepared player Chain while lifetime runs",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("player-chain-offline-result",Offline)));
        Register("player-chain-check",Check);Register("player-chain-reload-check",()=>FixtureNetwork.Connected("player-chain-reload-result",Reload));
        foreach(bool quick in new[]{false,true}){bool q=quick;string label=q?"player-chain-quick":"player-chain-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared player Chain Lightning root/child/input/save",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.ChargedBolt,6180);
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightning",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.chainlightning",out _),"Chain prerequisite/train");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ChainLightning)&&SkillBindings.Instance.Assign(p,false,CombatAbility.ChainLightning)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ChainLightning),"Chain bindings");return p;
    }
    private static ChainFlight State(long id)=>ChainLightning.Snapshot().SingleOrDefault(x=>x.Cast==id);
    private static ChainFlight Prepare(PlayerMobile p)=>ChainLightning.PreparePlayer(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.ChainLightning),TimedEffects.Now);
    private static void Step(long id,bool actions=true){if(State(id) is{} s){ChainLightning.Tick(s.NextUtc,actions,id);}}
    private static void Through(long id,int frames,bool actions=true){for(int i=0;i<frames&&State(id)!=null;i++){Step(id,actions);}}
    private static void Clean(PlayerMobile p,params Mobile[] targets){ChainLightning.Cancel(p);FireBoltChecks.Clean(p,targets);}
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static void Replace(ChainFlight f)=>ChainLightning.Restore(ChainLightning.Snapshot().Where(x=>x.Cast!=f.Cast).Append(f).ToArray());
    private static void Check()
    {
        CaptureAndChildren();PotionAndContacts();Delays();Lifecycle();Save();
        Log.Information("Prepared player Chain:captured root/live retained-rank children,real gear/Slow Missiles,typed contact potion/immune channels,Size2/source steps,cyclic identity/delay/finite count,atomic snapshots/lifecycle and saved partial flight pass");
    }
    private static void CaptureAndChildren()
    {
        var p=Owner();var first=FireBoltChecks.Target(p,5);var second=FireBoltChecks.Target(p,9);var nearer=FireBoltChecks.Target(p,5);nearer.MoveToWorld(new(nearer.X,nearer.Y+1,nearer.Z),p.Map);
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(first);
            Require(SorceressCombat.Release(p,CombatAbility.ChainLightning,Serial.Zero,new(p.X+10,p.Y,p.Z)),"Chain actual release");var f=ChainLightning.Snapshot().Single(x=>x.Owner==p.Serial.Value);long id=f.Cast;
            Require(f is{Rank:1,Remaining:5,Min256:256,Max256:11059,Vx:.703125}&&f.PlayerPacket is{VelocityFp8:5760}&&NecromancerEffects.ManaBalance256(p)==mana-2304&&SkillHealth.Balance256(first)==hp,"Chain root/cost/capture");
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.chainlightning",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightning",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightningmastery",out _),"Chain live child ranks");
            AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());
            Require(Json(State(id))==Json(f),"Root reread ranks/slow before impact");
            for(int i=0;i<25&&SkillHealth.Balance256(first)==hp;i++){Step(id);}
            var child=State(id);Require(SkillHealth.Balance256(first)<hp&&child is{Remaining:4,Rank:1,Min256:192,Max256:8601}&&child.PlayerPacket.Slow==new SlowMissilePayload(50,50)
                &&child.AimTarget==second.Serial.Value&&child.Delays.Any(x=>x.Target==first.Serial.Value),"Child retained rank/live mastery/synergy/slow/cyclic target: "+child);
            int b=SkillHealth.Balance256(second),c=SkillHealth.Balance256(nearer);AmazonSlowMissiles.Clear(p);Through(id,200);
            Require(State(id)==null&&SkillHealth.Balance256(second)<b&&SkillHealth.Balance256(nearer)<c,"Finite prepared identity hops");
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var packet=Prepare(p);id=ChainLightning.CommitPlayer(packet);p.Backpack.DropItem(orb);ClassGear.Refresh(p);
            Require(Json(State(id))==Json(packet)&&packet.PlayerPacket.Modifiers.LightningMastery>50,"Actual Chain gear capture changed");
        }finally{Clean(p,first,second,nearer);}
    }
    private static void PotionAndContacts()
    {
        foreach(var (res,potion,expected) in new[]{(0,"none",25600),(50,"none",12800),(100,"none",0),(100,"after",7680),(0,"after",35840),(0,"removed",25600)})
        {
            var p=Owner();var t=FireBoltChecks.Target(p,5);t.MoveToWorld(new(t.X,t.Y+1,t.Z),p.Map);try
            {
                t.SetResistance(ResistanceType.Energy,res);if(potion=="removed"){Require(CombatPotions.Apply(p,3),"Chain potion before launch");}
                var packet=Prepare(p) with{Remaining=1,Min256=25600,Max256=25600};long id=ChainLightning.CommitPlayer(packet);
                if(potion=="after"){Require(CombatPotions.Apply(p,3),"Chain potion after launch");}if(potion=="removed"){TimedEffects.Clear(p);}
                int hp=SkillHealth.Balance256(t);Through(id,25);Require(State(id)==null&&hp-SkillHealth.Balance256(t)==expected,"Size2 typed Chain contact: "+res+"/"+potion+" loss="+(hp-SkillHealth.Balance256(t)));
            }finally{Clean(p,t);}
        }
    }
    private static void Delays()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,5);try
        {
            var f=Prepare(p) with{Remaining=1,Min256=2560,Max256=2560};long a=ChainLightning.CommitPlayer(f),b=ChainLightning.CommitPlayer(Prepare(p) with{Remaining=1,Min256=2560,Max256=2560});int hp=SkillHealth.Balance256(t);
            Through(a,25);Through(b,25);Require(hp-SkillHealth.Balance256(t)==5120,"Independent Chain casts share hit delay");
            var at=TimedEffects.Now;f=Prepare(p) with{Remaining=1,Delays=[new(t.Serial.Value,at.AddMilliseconds(160))]};Require(ChainLightning.Delayed(f,t.Serial.Value,at.AddMilliseconds(159))&&!ChainLightning.Delayed(f,t.Serial.Value,at.AddMilliseconds(160)),"Strict four-frame delay");
            a=ChainLightning.CommitPlayer(f);var snapshot=ChainLightning.Snapshot();snapshot.Single(x=>x.Cast==a).Delays[0]=new(t.Serial.Value,at.AddHours(1));
            Require(State(a).Delays[0].UntilUtc==at.AddMilliseconds(160),"Chain delay snapshot alias");
            snapshot=ChainLightning.Snapshot();snapshot.Single(x=>x.Cast==a).PlayerPacket.Steps.Cells[0]=new(1,1);Require(State(a).PlayerPacket.Steps.Cells[0]!=new SourceMissileCell(1,1),"Chain step snapshot alias");
            var original=State(a);foreach(var bad in new[]{original with{Map=-1},original with{Rank=256},original with{PlayerPacket=original.PlayerPacket with{VelocityFp8=6000}},original with{PlayerPacket=original.PlayerPacket with{Steps=new([])}}})
            {
                string before=Json(ChainLightning.Snapshot());bool refused=false;try{Replace(bad);}catch(InvalidDataException){refused=true;}Require(refused&&Json(ChainLightning.Snapshot())==before,"Invalid prepared restore partially published");
            }
        }finally{Clean(p,t);}
    }
    private static void Lifecycle()
    {
        foreach(string mode in new[]{"offline","wall","facet","death","respec","wardrobe","loaded"})
        {
            var p=Owner();var t=FireBoltChecks.Target(p,5);Item wall=null;try
            {
                long id=ChainLightning.CommitPlayer(Prepare(p));var f=State(id);int hp=SkillHealth.Balance256(t);
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}if(mode=="death"){p.Kill();}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Chain wardrobe");}
                if(mode=="loaded"){Replace(f);ChainLightning.Tick(f.NextUtc.AddMilliseconds(800),true,id);}
                Through(id,30,mode!="offline");Require(State(id)==null&&SkillHealth.Balance256(t)==hp,"Prepared Chain lifecycle: "+mode);
            }finally{wall?.Delete();Clean(p,t);}
        }
        var owner=Owner();try
        {
            int mana=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,CombatAbility.ChainLightning,null,new(owner.X+10,owner.Y,owner.Z),Core.TickCount),"Chain cancelled windup");long effect=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);
            Require(!ClassCombat.ResolveAction(owner,effect)&&NecromancerEffects.ManaBalance256(owner)==mana&&!ChainLightning.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Cancelled Chain paid/emitted");
            NecromancerEffects.SetMana256(owner,2303);Require(!SorceressCombat.Release(owner,CombatAbility.ChainLightning,Serial.Zero,new(owner.X+10,owner.Y,owner.Z))&&NecromancerEffects.ManaBalance256(owner)==2303,"Insufficient Chain mana emitted/paid");
            owner.MoveToWorld(new(1497,1628,10),Map.Trammel);Require(!SorceressCombat.Release(owner,CombatAbility.ChainLightning,Serial.Zero,owner.Location),"Town prepared Chain cast");
        }finally{Clean(owner);}
    }
    private static void Save()
    {
        var p=Owner();long id=ChainLightning.CommitPlayer(Prepare(p));Step(id);Step(id);var at=TimedEffects.Now.Add(FixtureSpace.Retention);var f=State(id) with{NextUtc=at,UntilUtc=at.AddSeconds(1)};Replace(f);
        var saved=new Saved(p.Serial.Value,p.Map.MapID,p.Location,NecromancerEffects.ManaBalance256(p),f,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);Require(s.Generation<2&&Json(State(s.Flight.Cast))==Json(s.Flight)&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved prepared Chain packet/steps/resources");
        var account=FixtureNetwork.Authenticate(network,p,"player-chain-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var t=FireBoltChecks.Target(p,5);int hp=SkillHealth.Balance256(t);long id=ChainLightning.Snapshot().Max(x=>x.Cast)+1;
        var copy=s.Flight with{Cast=id,Remaining=1,NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(1)};Replace(copy);var deadline=TimedEffects.Now.AddSeconds(3);
        void Observe()
        {
            if(SkillHealth.Balance256(t)==hp&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
            try
            {
                Require(SkillHealth.Balance256(t)<hp&&State(id)==null&&Json(State(s.Flight.Cast))==Json(s.Flight),"Prepared Chain real resumed source travel/contact");t.Delete();p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("player-chain-reload-result: passed");
            }catch(Exception e){Log.Error(e,"player-chain-reload-result: failed");}
        }
        FixtureClock.NextLoop("player-chain-reload-result",Observe);
    }
    private static void Offline(NetState network)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,8);var account=FixtureNetwork.Authenticate(network,p,"player-chain-offline-");int hp=SkillHealth.Balance256(t);long id=ChainLightning.CommitPlayer(Prepare(p));
        Log.Information("player-chain-offline: ready");
        Timer.StartTimer(TimeSpan.FromMilliseconds(100),()=>
        {
            try
            {
                Require(State(id)!=null&&SkillHealth.Balance256(t)==hp,"No pre-contact chain at actual disconnect");
#pragma warning disable CS0618
                network.Dispose();
#pragma warning restore CS0618
                Require(p.NetState==null,"Actual Chain disconnect retained player session");
                Timer.StartTimer(TimeSpan.FromMilliseconds(1300),()=>
                {
                    try{Require(State(id)==null&&SkillHealth.Balance256(t)==hp,"Disconnected player chain damaged target or froze lifetime");Log.Information("player-chain-offline-result: passed");}
                    catch(Exception e){Log.Error(e,"player-chain-offline-result: failed");}finally{Clean(p,t);account.Delete();}
                });
            }catch(Exception e){Clean(p,t);account.Delete();Log.Error(e,"player-chain-offline-result: failed");}
        });
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,5);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);string label=quick?"player-chain-quick":"player-chain-right";
        var account=FixtureNetwork.Authenticate(network,p,"player-chain-input-");bool began=false;long deadline=Core.TickCount+6000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Player Chain input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.ChainLightning&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==hp,"Player Chain input paid before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-2304&&!ChainLightning.Snapshot().Any(x=>x.Owner==p.Serial.Value),"Actual Chain payment/contact");Clean(p,t);account.Delete();Log.Information("{Label}-result: passed",label);
            }catch(Exception e){Clean(p,t);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.ChainLightning,(int)DiabloClass.Sorceress,p.X+10,p.Y,p.Z);Observe();});
    }
}
