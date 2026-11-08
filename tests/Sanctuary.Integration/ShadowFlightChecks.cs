// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;
using Server.Items;
using Server.Spells.Fourth;
namespace Sanctuary.Integration;

public static class ShadowFlightChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowFlightChecks));
    private sealed class BlockRegion(Map map,Point3D p):BaseRegion("Shadow Flight travel fixture",map,250,new Rectangle3D(p.X-4,p.Y-4,-128,8,8,256))
    {public override bool CheckTravel(Mobile traveller,Point3D point,TravelCheckType type,out TextDefinition message){message=null;return false;}}
    private sealed class ArrivalProbe:FireFieldItem
    {
        internal uint Traveller;
        internal ArrivalProbe(Point3D point,Mobile caster,Map map):base(0x398C,point,caster,map,TimeSpan.FromMinutes(1),0){}
        public ArrivalProbe(Serial serial):base(serial){}
        public override bool OnMoveOver(Mobile m){Traveller=m.Serial.Value;if(m is AssassinShadowBody b){b.InterruptForRecovery();}return true;}
    }
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-flight-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Clear(AssassinShadowBody b)=>ShadowDragonClawChecks.Clear(b);
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(s=>s.Owner==b.Serial.Value).ToArray();
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b));
    private static void Gain(AssassinShadowBody b,int skill,DateTime at)
    {var v=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(skill),skill);for(int i=0;i<3;i++){AssassinCharges.Gain(b,v,at);}}
    private static ulong Seed(bool hit)
    {for(int i=1;i<10000;i++){ulong s=SourceRandom.InitLow(i),r=s;uint n=SourceRandom.Limited(ref r,100);if(hit?n<5:n>=95){return s;}}throw new InvalidOperationException("No hit seed");}
    private static ShadowTailChecks.Witness Target(AssassinShadowBody b,int dx=8)
    {var t=new ShadowTailChecks.Witness{Actor=b.Serial.Value};t.SetHits(65000);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}t.MoveToWorld(new(b.X+dx,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    public static void Configure(){Register("shadow-flight-check",false);Register("shadow-flight-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Flight two-stage transfer,kick,ground controller and saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void ResetPose(AssassinShadowBody b,PlayerMobile p,Point3D point)
    {Clear(b);b.MoveToWorld(point,p.Map);if(ShadowGroundMotion.Active(b)){var s=b.WorldGround.State.Controller;b.WorldGround.Store(IronGolemWorld.Park(s)with{Map=p.Map.MapID,Motion=DruidRavenMotion.Center(point.X*2,point.Y*2)});}}
    private static void Action(AssassinShadowBody b,PlayerMobile p,DateTime at,bool all)
    {
        Clear(b);var origin=b.Location;var ownerPoint=p.Location;var t=Target(b);
        try
        {
            if(all){foreach(int id in new[]{254,265,280,259,274,269}){Gain(b,id,at);}}else{Gain(b,254,at);}
            if(!all){NecromancerEffects.SetMana256(b,0);}
            int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),own=NecromancerEffects.ManaBalance256(b);var rng=NpcEquipmentRandom.Require(b);string charges=Json(Charges(b));
            Require(ShadowActions.BeginDragonFlight(b,t,at),"Own Flight startup refused");var a=ShadowActions.State(b);
            Require(a.Plan.Speed==256&&a.Plan.Events.Select(e=>e.Frame).SequenceEqual(new[]{10,15,24}),"Own NPC Flight clock differs from source controls");
            ShadowActions.Tick(at.AddMilliseconds(9*40),true,b.Serial.Value);Require(b.Location==origin&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b)==rng,"Flight moved/rolled/hit before first marker");
            ShadowActions.Tick(at.AddMilliseconds(10*40),true,b.Serial.Value);var landed=b.Location;
            Require(a.Cast.FlightLanded&&!a.Cast.FlightKicked&&landed!=origin&&landed!=t.Location&&ShadowDragonFlight.Continues(b,t)
                &&SkillHealth.Balance256(t)==hp&&Json(Charges(b))==charges&&NpcEquipmentRandom.Require(b)==rng&&p.Location==ownerPoint,"First marker did not exclusively transfer own pet");
            if(ShadowGroundMotion.Active(b)){Require(IronGolemWorld.Cell(b.WorldGround.State.Controller)==new Point2D(b.X*2,b.Y*2),"Direct transfer left stored ground pose stale");}
            Require(ShadowSkillCasting.ReleaseFlight(a.Cast,9*256,10,at)==0,"Flight teleport callback replayed");
            ShadowActions.Tick(at.AddMilliseconds(14*40),true,b.Serial.Value);Require(SkillHealth.Balance256(t)==hp&&Json(Charges(b))==charges,"Flight kicked before second marker");
            ShadowActions.Tick(at.AddMilliseconds(15*40),true,b.Serial.Value);
            Require(a.Cast.FlightKicked&&SkillHealth.Balance256(t)<hp&&Charges(b).All(c=>c.Count==2)&&t.Flag&&t.Selected==0,"Second marker lost own kick,flag,weapsel4 or per-contact consume-one");
            if(all){Require(MartialCold.Snapshot().Cubes.Any(c=>c.Npc?.Actor==b.Serial.Value)&&FireGround.Snapshot().Patches.Any(c=>c.PhoenixNpc?.Actor==b.Serial.Value)&&MartialLightning.Snapshot().Flights.Any(c=>c.Npc?.Actor==b.Serial.Value),"Flight did not share all-six releases");}
            hp=SkillHealth.Balance256(t);rng=NpcEquipmentRandom.Require(b);Require(ShadowSkillCasting.ReleaseFlight(a.Cast,14*256,15,at)==0,"Flight kick callback replayed");
            ShadowActions.Tick(at.AddMilliseconds(24*40),true,b.Serial.Value);Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b)==rng
                &&NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.ManaBalance256(b)>=own,"Flight recovery/replay or NPC/player mana changed");
        }
        finally{SourceKnockbacks.Clear(t);t.Delete();ResetPose(b,p,origin);}
    }
    private static void Failures(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        var origin=b.Location;var t=Target(b,19);
        try
        {
            Require(ShadowDragonFlight.Start(b,t),"Inclusive source38 range refused");t.MoveToWorld(new(origin.X+20,origin.Y,origin.Z),b.Map);
            Require(!ShadowActions.BeginDragonFlight(b,t,at)&&b.OwnedSkills.Used==null,"Outside start range leaked used skill");
        }
        finally{Clear(b);t.Delete();}
        for(int reason=0;reason<9;reason++)
        {
            t=Target(b);try
            {
                Gain(b,254,at);Require(ShadowActions.BeginDragonFlight(b,t,at),"Interrupted Flight entry refused");var a=ShadowActions.State(b);int hp=SkillHealth.Balance256(t);string charges=Json(Charges(b));var rng=NpcEquipmentRandom.Require(b);
                if(reason>=4){ShadowActions.Tick(at.AddMilliseconds(400),true,b.Serial.Value);Require(a.Cast.FlightLanded,"Post-landing failure fixture did not land");}
                if(reason is 0 or 4){b.InterruptForRecovery();}if(reason is 1 or 5){b.MoveToWorld(new(b.X,b.Y+3,b.Z),b.Map);}if(reason==2){b.Frozen=true;}if(reason==3){PersistentMinions.Park(b);}if(reason==6){t.MoveToWorld(new(t.X+8,t.Y,t.Z),t.Map);}if(reason==7){t.Delete();}
                ShadowActions.Tick(at.AddMilliseconds(24*40),reason!=8,b.Serial.Value);
                Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&NpcEquipmentRandom.Require(b)==rng&&Json(Charges(b))==charges&&(t.Deleted||SkillHealth.Balance256(t)==hp),"Interrupted/escaped Flight rolled,hit,consumed or retained action");b.Frozen=false;
            }
            finally{t.Delete();ResetPose(b,p,origin);}at=at.AddSeconds(2);
        }
        // Refuse a real release-time landing through a newly installed travel
        // veto. No RNG/charge/mana/kick is committed.
        t=Target(b);var block=new BlockRegion(b.Map,b.Location);try
        {
            Gain(b,254,at);Require(ShadowActions.BeginDragonFlight(b,t,at),"Failed landing entry refused");var rng=NpcEquipmentRandom.Require(b);string charges=Json(Charges(b));block.Register();
            ShadowActions.Tick(at.AddMilliseconds(400),true,b.Serial.Value);Require(ShadowActions.State(b)==null&&b.Location==origin&&NpcEquipmentRandom.Require(b)==rng&&Json(Charges(b))==charges,"Refused landing transferred/rolled/consumed");
        }
        finally{block.Unregister();t.Delete();ResetPose(b,p,origin);}
    }
    private static void Contacts(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        var origin=b.Location;Clear(b);var t=Target(b);
        try
        {
            Require(ShadowActions.BeginDragonFlight(b,t,at),"Miss Flight entry refused");ShadowActions.Tick(at.AddMilliseconds(400),true,b.Serial.Value);int hp=SkillHealth.Balance256(t);ulong seed=Seed(false),expected=seed;SourceRandom.Limited(ref expected,100);NpcEquipmentRandom.Set(b,seed);ShadowActions.Tick(at.AddMilliseconds(600),true,b.Serial.Value);
            Require(SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b).Seed==expected&&ShadowActions.State(b)?.Cast.FlightKicked==true,"Missed Flight hit or used damage RNG");
        }
        finally{t.Delete();ResetPose(b,p,origin);}
        var choice=SourceBlocking.ChoiceRoll;var roll=SourceBlocking.BlockRoll;var shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
        try
        {
            shield.SetHits(65000);shield.MoveToWorld(new(b.X+8,b.Y,b.Z),b.Map);shield.AIObject.AITimer.Stop();Gain(b,254,at);SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;Require(ShadowActions.BeginDragonFlight(b,shield,at),"Shielded Flight entry refused");ShadowActions.Tick(at.AddMilliseconds(400),true,b.Serial.Value);int hp=SkillHealth.Balance256(shield);var rng=NpcEquipmentRandom.Require(b);string charges=Json(Charges(b));ShadowActions.Tick(at.AddMilliseconds(600),true,b.Serial.Value);
            Require(SkillHealth.Balance256(shield)==hp&&NpcEquipmentRandom.Require(b)==rng&&Json(Charges(b))==charges,"Charged Flight bypassed shield,rolled damage or consumed charges");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=roll;shield.Delete();ResetPose(b,p,origin);}
    }
    private static void WorldAi(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        Clear(b);if(!ShadowGroundMotion.Active(b)){ShadowGroundMotion.Activate(b,p,at);}
        if(b.WorldGround.Loaded){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);}
        var origin=b.Location;var t=Target(b);
        try
        {
            Gain(b,254,at);int hp=SkillHealth.Balance256(t);var s=b.WorldGround.State.Controller;b.WorldGround.Store(IronGolemWorld.Park(s)with{NextUtc=at.AddMilliseconds(40),NextThink=0,Mode=1});
            int duration=((INecromancerGroundBrain)b).GroundSkill(p,14,275,t,at);Require(duration==24,"World Flight duration differs");
            for(int i=0;i<24;i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Flight ground transfer fault: "+b.WorldGround.State.Controller.Fault);}
            Require(SkillHealth.Balance256(t)<hp&&ShadowActions.State(b)==null&&IronGolemWorld.Cell(b.WorldGround.State.Controller)==new Point2D(b.X*2,b.Y*2),"World transfer/kick/recovery failed: hp="+hp+"->"+SkillHealth.Balance256(t)+",pending="+(ShadowActions.State(b)?.Frame.ToString()??"none")+",controller="+IronGolemWorld.Cell(b.WorldGround.State.Controller)+",native="+b.Location);
            ResetPose(b,p,origin);t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);Gain(b,254,at.AddMinutes(1));
            if(b.Prepared.Capture.Ability==CombatAbility.ShadowWarrior)
            {
                Require(SkillBindings.Instance.Assign(p,true,CombatAbility.DragonFlight)&&SkillBindings.Instance.Assign(p,false,CombatAbility.DragonFlight),"Natural Flight bindings refused");
                hp=SkillHealth.Balance256(t);s=b.WorldGround.State.Controller;b.WorldGround.Store(s with{NextThink=s.Frame+1});bool selected=false;
                for(int i=0;i<1000&&!(selected&&SkillHealth.Balance256(t)<hp);i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Natural Flight AI fault");selected|=ShadowActions.State(b)?.Cast.Skill==275;}
                Require(selected&&SkillHealth.Balance256(t)<hp,"Natural Warrior did not complete Flight");
            }
        }
        finally{t.Delete();ResetPose(b,p,origin);}
    }
    private static void Arrival(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        Clear(b);var origin=b.Location;var t=Target(b);ArrivalProbe field=null;
        try
        {
            Gain(b,254,at);Require(ShadowDragonFlight.Destination(b,t,out var landing),"Arrival probe has no landing");field=new(landing,p,b.Map);int hp=SkillHealth.Balance256(t);var rng=NpcEquipmentRandom.Require(b);string charges=Json(Charges(b));
            var s=b.WorldGround.State.Controller;b.WorldGround.Store(IronGolemWorld.Park(s)with{NextUtc=at.AddMilliseconds(40),NextThink=0});Require(((INecromancerGroundBrain)b).GroundSkill(p,14,275,t,at)==24,"Arrival world action refused");
            for(int i=0;i<10;i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);}
            Require(field.Traveller==b.Serial.Value&&b.Location==landing&&ShadowActions.State(b)==null&&IronGolemWorld.Cell(b.WorldGround.State.Controller)==new Point2D(landing.X*2,landing.Y*2)
                &&b.WorldGround.State.Controller.Fault==null&&NpcEquipmentRandom.Require(b)==rng&&Json(Charges(b))==charges,"Arrival dispatch/interruption lost traveller or ground pose,or consumed charges/RNG");
            ShadowActions.Tick(at.AddSeconds(2),true,b.Serial.Value);Require(SkillHealth.Balance256(t)==hp,"Arrival-interrupted Flight still kicked");
        }
        finally{field?.Delete();t.Delete();ResetPose(b,p,origin);}
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bodies=s.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(s.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed original Flight actor/items/resources/wounds/RNG/charges or replayed intent");}p.MoveToWorld(new(6390,3540,0),Map.Trammel);
        }
        else
        {
            p=BladeBodyChecks.Fresh(6390,30,3540);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);ClassSkillChecks.Train(p,"assassin.dragonflight",1);var world=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];
            try{int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);foreach(int id in new[]{275,254,265,280,259,274,269}){b.ChangeSkills(book=>book.Assign(id,3));}NecromancerEffects.SetMana256(p,0);var at=TimedEffects.Now.AddMinutes(20+generation);
            Action(b,p,at,false);Action(b,p,at.AddMinutes(1),true);Failures(b,p,at.AddMinutes(2));Contacts(b,p,at.AddMinutes(3));WorldAi(b,p,at.AddMinutes(4));Arrival(b,p,at.AddMinutes(5));
            Clear(b);var t=Target(b);Gain(b,254,at.AddMinutes(6));Require(ShadowActions.BeginDragonFlight(b,t,at.AddMinutes(6)),"Pending saved Flight refused");if(b.Prepared.Capture.Ability==CombatAbility.ShadowMaster){ShadowActions.Tick(at.AddMinutes(6).AddMilliseconds(400),true,b.Serial.Value);}t.Delete();SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Flight enabled unfinished learned Shadows");
        Log.Information("Own Flight:approved first-marker actual transfer/second-marker own kick,original NPC10/15/24clock,shared six-family contacts/free NPC mana,stored and in-flight ground pose synchronization,world/natural Warrior AI,cancel/no replay and exact original saves through two reloads;modern boundaries and other learned gates remain.");
    }
}
