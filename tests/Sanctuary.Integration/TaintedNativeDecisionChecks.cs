// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class TaintedNativeDecisionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(TaintedNativeDecisionChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,string Birth,DeathMarkSavedActor Source,NpcEquipmentSeed Random,int Life);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"tainted-native-decision-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"tainted-native-decision-reload-check":"tainted-native-decision-check";
            ConsoleInputHandler.RegisterCommand(name,"Actual native Tainted source root/search/scoring to registered A2 and projectile",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>Scene(n,!reload,name+"-result"));}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Scene(NetState network,bool retain,string result)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Name="Tainted source-root fixture";p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,47);
        ClassSkillChecks.Train(p,"warlock.summontainted",6);ClassSkillChecks.Train(p,"warlock.demonicmastery",16);ClassSkillChecks.Train(p,"warlock.bloodboil",1);
        FixtureSpace.Corridor(p,6560,28,3660);var account=FixtureNetwork.Authenticate(network,p,"tainted-root-");WarlockTaintedBody b=null;GoatmanNpcContactChecks.Recipient t=null;bool kept=false;
        try
        {
            DateTime at=TimedEffects.Now;int actor=0;
            b=WarlockTaintedBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at,new(1,TaintedGroundMovementChecks.PathFor));actor=checked((int)b.Serial.Value);NpcEquipmentRandom.Set(b,SourceRandom.InitLow(743));
            t=new(){NoKillAwards=true,Frozen=true,CantWalk=true};t.SetHits(50000);t.Hits=t.HitsMax;t.MoveToWorld(new(b.X+6,b.Y,b.Z),b.Map);t.AIObject?.AITimer.Stop();
            var birth=b.Prepared.Snapshot;var point=b.Location;int mana=NecromancerEffects.ManaBalance256(p);var events=new GenericPetTimedEvents();int idles=0;
            var motion=new GoatmanNativeMotion(b,p,events,()=>0,()=>at,()=>new(0,DeathMarkWorld.State(b).Mode,1,GenericPetMetadata.Table.StateCount,true,-1,0,4),_=>idles++,DemonGroundMovement.Tainted.Flags241);
            int Choose(bool cast=true)
            {
                var world=new GoatmanNativeSearch(b,p);var random=new GenericPetActionRandom(NpcEquipmentRandom.Require(b).Seed);motion.Random=random;
                var actions=new GenericPetActionServices((_,_,_)=>throw new NotSupportedException("Tainted aura is selected outside offensive gather"),world.Position,
                    (x,y)=>GenericPetSearch.Squared(world.Position(actor),x,y),(id,mask)=>world.Search.Callback.Collision27c710(actor,id,mask)!=0,
                    _=>throw new NotSupportedException("Tainted has no melee descriptor"),motion.Stop,(_,_,_,_,_)=>throw new InvalidOperationException(),()=>{},_=>DemonGroundMovement.Tainted.Flags241,
                    (_,_)=>throw new InvalidOperationException(),(_,_,_,_,_,_)=>throw new NotSupportedException("Unbound Tainted skill"),
                    _=>throw new NotSupportedException("Tainted has no base mode descriptor"),_=>throw new NotSupportedException("Tainted has no base attack"),(_,_)=>throw new InvalidOperationException(),
                    id=>world.Search.Callback.Valid372ec0(actor,id,1)!=0,_=>0);
                actions=motion.BindActions(TaintedFireBallCasting.BindRegistered(b,actions,(target,rank)=>target is BaseCreature&&rank>0&&DeathMarkWorld.State(b).Mode==1&&GenericPetNativeControlStates.Read(b)==0,()=>at));
                var leaves=new GenericPetCombatServices(()=>throw new InvalidOperationException(),_=>0,_=>0,(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
                    ()=>DeathMarkWorld.Path(b).Size5c,()=>world.Room(actor),(room,x,y,property,mask)=>mask==64&&property==DemonGroundMovement.Size(b)?AlliedOverlap.Blocked(b):throw new NotSupportedException("Wrong standing collision query"),
                    ()=>GenericPetMovement.Prepare(motion.Unit,0,0,0),(x,y,radius,hint,flags)=>actions.Move(x,y,checked((byte)radius),hint,flags),actions.MoveFinished,
                    _=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException());
                System.Collections.Generic.List<GenericPetCombatCandidate> Gather()=>GoatmanNativeDecision.Gather(b,GenericPetProfiles.Raw(CombatAbility.SummonTainted),new(0),
                    new((_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),_=>throw new NotSupportedException("No progressive Tainted skill"),(_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),(_,_)=>0),(_,_)=>0);
                var commander=ClassSkillChecks.Player(DiabloClass.Barbarian);
                try
                {
                    BattleCommand.Apply(commander,b,1,at);
                    Require(TaintedNpcRanks.Level(b,422)==7&&Gather()[0].Opaque24==6,"Bonus-disabled source gather borrowed effective Battle Command rank");
                }
                finally{BattleCommand.Clear(b);commander.Delete();}
                var rows=Gather();
                Require(rows.Count==2&&rows[1] is{SkillId:420,Byte36:6,Opaque28:8}&&rows[0] is{SkillId:422,Byte36:11,Opaque28:5}&&rows[0].Opaque24==TaintedNpcRanks.Level(b,422),"Tainted gather lost source Fire Ball/A2,aura/S1 slots or injected Blood Boil/base melee");
                var binding=world.Bind(random,leaves,motion.Follow(world),motion.FollowExecution(world),actions,(_,_)=>0,_=>idles++);
                var root=events.Bind(binding.Context,binding.Services,actor);GenericPetDecisions.Think(root.Context,root.Services);
                if(cast)
                {Require(TaintedFireBallCasting.State(b)is{LiveRank:true,Released:false,Rank:6} state&&state.Target==t.Serial.Value&&b.Location==point&&motion.Paths==0,
                    "Source root did not use ranged grid scoring and own A2 against the actual native enemy");}
                else{Require(TaintedFireBallCasting.State(b)==null&&motion.Paths>0&&b.Location==point,"Out-of-range Tainted did not choose source approach before A2");}
                return rows.Count;
            }
            int hp=SkillHealth.Balance256(t);var seed=NpcEquipmentRandom.State(b);Choose();var action=TaintedFireBallCasting.State(b);var chosen=NpcEquipmentRandom.State(b);
            Require(chosen!=seed&&NecromancerEffects.ManaBalance256(p)==mana,"Source decision did not commit its actor RNG or spent owner mana");
            TaintedFireBallCasting.Tick(action.ActionUtc.AddTicks(-1),true);Require(!TaintedFireBallMissiles.Snapshot().Flights.Any(f=>f.Actor==b.Serial.Value),"Source-root A2 emitted early");
            TaintedFireBallCasting.Tick(action.ActionUtc,true);var flight=TaintedFireBallMissiles.Snapshot().Flights.Single(f=>f.Actor==b.Serial.Value);
            Require(flight.Rank==6&&TaintedFireBallCasting.State(b)is{Released:true,Fault:null},"Own live A2 rank did not launch original projectile");
            TaintedFireBallCasting.Tick(action.EndUtc,true);
            for(int i=0;i<100&&TaintedFireBallMissiles.Snapshot().Flights.Any(f=>f.Id==flight.Id);i++)
            {var current=TaintedFireBallMissiles.Snapshot().Flights.Single(f=>f.Id==flight.Id);TaintedFireBallMissiles.Tick(current.NextUtc,true,flight.Id);}
            Require(SkillHealth.Balance256(t)<hp&&MonsterUninterruptibles.State(t)?.LastAttacker==b.Serial.Value&&b.Prepared.Snapshot==birth&&NecromancerEffects.ManaBalance256(p)==mana,
                "Source-selected A2 failed actual NPC-owned damage or recaptured/payed owner stats");
            // A second root choice must reuse original control/search,not a
            // fixture-selected skill or a detached seed captured before release.
            at=action.EndUtc.AddMilliseconds(40);Choose();Require(NpcEquipmentRandom.State(b)!=chosen,"Repeated source root rewound its committed actor seed");TaintedFireBallCasting.Cancel(b);
            t.MoveToWorld(new(b.X+13,b.Y,b.Z),b.Map);double range=b.GetDistanceToSqrt(t);Choose(false);
            for(int i=0;i<40&&(motion.Motion.Flags&0x20)!=0;i++){motion.Advance();}
            Require(b.Location!=point&&b.GetDistanceToSqrt(t)<range&&TaintedFireBallCasting.State(b)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Source ranged approach did not move original Tainted or cast before admission");motion.Stop();
            Require(WarlockCombat.Implemented(CombatAbility.SummonTainted),"Learned Tainted route missing after shared host integration");
            if(retain)
            {
                SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,b.SavedText,DeathMarkWorld.State(b),NpcEquipmentRandom.State(b),SkillHealth.Balance256(b))));kept=true;
            }
            Log.Information("Tainted source root:3actual native room/grid/own-fire-resistance decisions;NPC422/A2 projectile damaged native enemy with NPC credit;out-of-range source approach moved original body before casting;no owner payment,passive cast/BloodBoil/melee injection");
            Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally
        {
            TaintedFireBallCasting.Cancel(b);TaintedFireBallMissiles.Cancel(b);t?.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){DeathMarkWorld.Remove(b);b?.Delete();p.Delete();}
        }
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)s.Body)as WarlockTaintedBody;
        Require(b!=null&&b.Map==Map.Internal&&b.ControlMaster?.Serial.Value==s.Owner&&b.SavedText==s.Birth&&DeathMarkWorld.State(b)==s.Source
            &&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life&&TaintedFireBallCasting.State(b)==null,"Original source-root Tainted/control/birth/wounds/RNG changed on reload");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
