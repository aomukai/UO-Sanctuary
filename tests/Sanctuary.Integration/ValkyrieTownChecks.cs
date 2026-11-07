// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
namespace Sanctuary.Integration;

public static class ValkyrieTownChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieTownChecks));
    private sealed record Held(uint Body,string Text,string[] Items,string Resources,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Point3D Origin,Point3D Town,int Map,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-town-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-town-check",false);Register("valkyrie-town-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Valkyrie safe-area registration/parking and live execution guards",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AmazonValkyrieBody b)=>new(b.Serial.Value,b.SavedText,ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray(),Json(GeneratedNpcLiveResources.State(b)),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    private static void Parked(AmazonValkyrieBody b)=>Require(b is{Alive:true,Deleted:false,Map:not null}&&b.Map==Map.Internal&&MinionSafeAreas.Parked(b)&&b.Ground.State.Controller is{Mode:1,ActionAt:0}&&b.Combatant==null,"Original Valkyrie was not parked safely");
    private static void Guards(AmazonValkyrieBody b,PlayerMobile p)
    {
        Parked(b);ValkyrieEquipmentContact.RequireRegistration(b);_=NpcOriginalItemProcs.RequireRegistrationHost(b);
        var before=Capture(b);var world=NpcWorldAllocation.Capture();int mana=NecromancerEffects.ManaBalance256(p);int visits=0;var observed=NpcOriginalItemProcs.Observed;
        NpcOriginalItemProcs.Observed=(_,_)=>visits++;
        try
        {
            foreach(Action action in new Action[]{()=>NpcOriginalItemProcs.RequireHost(b),()=>ValkyrieEquipmentContact.Registry(b),()=>ValkyrieGroundCombat.Attack(b),()=>NpcOriginalItemProcs.Dispatch(b,null,"domeleeattack",new(0,0,0,0,1),TimedEffects.Now,_=>throw new InvalidOperationException("Parked proc allocated"))})
            {
                bool refused=false;try{action();}catch(NotSupportedException){refused=true;}Require(refused,"Structural registration granted parked execution");
            }
            Require(!NpcItemSpellCasts.CastOriginalPosition(b,new(p.X+1,p.Y,p.Z),39,3,0,TimedEffects.Now,_=>throw new InvalidOperationException("Parked cast allocated")),"Parked original cast succeeded");
            Require(visits==0&&Json(Capture(b))==Json(before)&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana,"Parked refusal changed items/controller/resources/RNG/owner mana or callbacks");
        }
        finally{NpcOriginalItemProcs.Observed=observed;}
    }
    private static void Overlap(AmazonValkyrieBody b,PlayerMobile p)
    {
        var t=new ValkyrieContactChecks.Recipient{Frozen=false};t.MoveToWorld(b.Location,b.Map);t.AIObject.AITimer.Stop();
        Point2D[] expected=[new(0,-1),new(1,-1),new(1,0),new(1,1),new(0,1),new(-1,1),new(-1,0),new(-1,-1)];
        var facing=b.Direction;var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();int life=SkillHealth.Balance256(t);
        try
        {
            var rules=IronGolemContact.Require(b,p,t);
            for(int d=0;d<8;d++)foreach(bool frozen in new[]{false,true})
            {
                SourceKnockbacks.Clear(t);b.Direction=(Direction)d|Direction.Running;t.Frozen=frozen;
                var packet=new PreparedMonsterDamagePacket(0,0,0,0,9);
                PreparedMonsterReturn.Dispatch(ref packet,new NativeMonsterReturn(b,p,t,rules.Metadata,()=>throw new InvalidOperationException("Overlap return killed")),true);
                var state=SourceKnockbacks.Snapshot().Single(s=>s.Target==t.Serial.Value);var delta=SourceKnockbacks.Offset(expected[d].X,expected[d].Y,5);
                Require(state.Position==t.Location&&state.End==new Point3D(t.X+delta.X,t.Y+delta.Y,t.Z)&&state.Length16==delta.Length16&&state.PauseFrozen&&NativeMonsterReturn.Session(t).LastMode==13,"Approved overlap facing or selected frozen knockback changed");
                Require(SkillHealth.Balance256(t)==life&&NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world,"Facing fallback changed HP or consumed actor/world RNG");
            }
        }
        finally{SourceKnockbacks.Clear(t);t.Delete();b.Direction=facing;}
    }
    private static void Check(bool reload)
    {
        Require(NpcWorldAllocation.Capture()==NpcWorldAllocationChecks.CheckedContinuation,"Unexpected world draws before Valkyrie town checks");
        PlayerMobile p;Point3D origin,townPoint;int map,generation=0;AmazonValkyrieBody[] bodies;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2,"Town reload count");p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;origin=saved.Origin;townPoint=saved.Town;map=saved.Map;
            bodies=saved.Bodies.Select(h=>World.FindMobile((Serial)h.Body)as AmazonValkyrieBody).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i]),"Parked original Valkyrie changed on reload");Parked(bodies[i]);}
        }
        else
        {
            p=new PlayerMobile{Name="ValkyrieTownFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6220,32,3020);origin=p.Location;townPoint=new(p.X+16,p.Y,p.Z);map=p.Map.MapID;
            bodies=[];
        }
        var region=new GuardedRegion("ValkyrieTown"+p.Serial.Value,Map.Maps[map],1000,new Rectangle2D(townPoint.X-2,townPoint.Y-2,5,5));region.Register();
        try
        {
            p.MoveToWorld(townPoint,Map.Maps[map]);Require(MinionSafeAreas.Contains(p)&&SkillMechanics.For(CombatAbility.Valkyrie).Number("InTown")==1,"Pinned Valkyrie safe-area/town rule");
            if(!reload)
            {
                var at=TimedEffects.Now.AddHours(4);var first=AmazonValkyrieBody.CreateInitializedController(p,new(p.X+1,p.Y,p.Z),at);var second=AmazonValkyrieBody.CreateInitializedController(p,new(p.X,p.Y+1,p.Z),at);bodies=[first,second];
                foreach(var b in bodies){Parked(b);SkillHealth.SetBalance256(b,b.MaximumLife256-1025);}
            }
            foreach(var b in bodies)
            {
                Guards(b,p);int life=SkillHealth.Balance256(b);var rng=NpcEquipmentRandom.Require(b);DateTime at=TimedEffects.Now;
                Require(TimedEffects.AddRestoration(b,true,10,1000,at),"Original parked restoration fixture");TimedEffects.Chill(b,50,1000,at);
                TimedEffects.Settle(b,at.AddMilliseconds(1001));NpcEquipmentRegeneration.Apply(b,256);
                Require(SkillHealth.Balance256(b)==life&&TimedEffects.Chill(b,at.AddMilliseconds(1001))==0&&NpcEquipmentRandom.Require(b)==rng,"Town parking recovered life,reset an effect or consumed RNG");
                TimedEffects.Clear(b);
            }
            var items=bodies.Select(b=>Json(ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray())).ToArray();var seeds=bodies.Select(NpcEquipmentRandom.Require).ToArray();var lives=bodies.Select(SkillHealth.Balance256).ToArray();var worldBefore=NpcWorldAllocation.Capture();
            // Keep the original wound available for the next reload's parked
            // restoration check;the deliberate live frame may otherwise heal it.
            foreach(var b in bodies){TimedEffects.PreventHealing(b,TimedEffects.Now);}
            p.MoveToWorld(origin,Map.Maps[map]);PersistentMinions.Resume(p);
            for(int i=0;i<bodies.Length;i++)
            {
                var b=bodies[i];Require(b.Map==p.Map&&!MinionSafeAreas.Parked(b)&&NpcItemSpellFlights.Attached(b,p,p.Map.MapID),"Original parked roster failed return");_=NpcOriginalItemProcs.RequireHost(b);ValkyrieEquipmentDefense.Require(b);
                Require(SkillHealth.Balance256(b)==lives[i]&&NpcEquipmentRandom.Require(b)==seeds[i]&&Json(ValkyrieEquipment.Originals(b).Select(x=>x.SavedText).ToArray())==items[i]&&NpcWorldAllocation.Capture()==worldBefore,"Town return rerolled items/streams or restored wounds");
                // The existing conductor repairs the projected pose before its
                // next decision. No movement while parked is replayed.
                var s=b.Ground.State.Controller;IronGolemWorld.Advance(b,s.NextUtc,true);Require(b.Ground.State.Controller.Fault==null,"Returned source controller fault");
            }
            Overlap(bodies[0],p);NpcExpandedItemSpellChecks.Flights(bodies[0]);
            p.MoveToWorld(townPoint,Map.Maps[map]);
            for(int i=0;i<bodies.Length;i++){var b=bodies[i];Parked(b);Guards(b,p);Require(SkillHealth.Balance256(b)==lives[i],"Live observation lost the original test wound");TimedEffects.Clear(b);}
            NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());p.Internalize();
            File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,origin,townPoint,map,bodies.Select(Capture).ToArray(),generation)));
            Log.Information("Valkyrie town:two unfiltered initialized original rosters created and parked in a real safe region;structural registration with zero live callback/cast/allocation admission,normal effect expiry/no recovery,original wounds/items/RNG on return and two reloads;resumed39/49/66effects reuse actual execution;learned casting/sunder/contact gaps remain separate");
        }
        finally{region.Unregister();}
    }
}
