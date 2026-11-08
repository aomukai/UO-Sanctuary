// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class RingWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RingWorldChecks));
    private sealed record Saved(uint Owner,uint Target,int Life256,Point3D Point,int Map,RingOfFireWorldSave State,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ring-world-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("ring-world-check",Check);Register("ring-world-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Candidate Ring world mechanics;learned route gated",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int y)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);ClassSkillChecks.Train(p,"warlock.ringoffire",1);FixtureSpace.Corridor(p,6400,40,y);
        NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static RingOfFireFlight[] Flights(long cast)=>RingOfFireWorld.Snapshot().Flights.Where(s=>s.Cast==cast).ToArray();
    private static BaseCreature Target(PlayerMobile owner,int dx,int dy=0)
    {var t=FireBoltChecks.Target(owner,dx);t.MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z),owner.Map);t.Frozen=false;t.CantWalk=true;return t;}
    private static long Cast(PlayerMobile owner,DateTime at,bool one=false)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.RingOfFire);int mana=NecromancerEffects.ManaBalance256(owner);
        long cast=RingOfFireWorld.CreatePaidCandidate(owner,values,at,one?r=>r.TargetX==30&&r.TargetY==0:null);
        Require(cast>0&&Flights(cast).Length==(one?1:16)&&NecromancerEffects.ManaBalance256(owner)==mana-values.Mana256,"Candidate launch/count/once-only payment");return cast;
    }
    private static void Finish(long cast)
    {for(int i=0;i<12&&Flights(cast).Length>0;i++){RingOfFireWorld.Tick(Flights(cast).Min(s=>s.NextUtc),true,cast);}Require(Flights(cast).Length==0,"Candidate Ring did not expire");}
    private static void FirstArea()
    {
        var p=Owner(2800);var a=Target(p,3);var b=Target(p,3,1);a.SetResistance(ResistanceType.Fire,25);b.SetResistance(ResistanceType.Fire,25);
        int first=SkillHealth.Balance256(a),second=SkillHealth.Balance256(b);long cast=Cast(p,TimedEffects.Now.AddMinutes(5));RingOfFireFlight hit=null;
        for(int n=0;n<10&&hit==null;n++)
        {RingOfFireWorld.Tick(Flights(cast).Min(s=>s.NextUtc),true,cast);hit=Flights(cast).FirstOrDefault(s=>s.Packet!=null&&s.LastCollision==a.Serial.Value||s.Packet!=null&&s.LastCollision==b.Serial.Value);}
        Require(hit!=null&&SkillHealth.Balance256(a)<first&&first-SkillHealth.Balance256(a)==second-SkillHealth.Balance256(b),"First explosion did not copy one packet to nearby ordinary recipients");
        int expected=NamedElements.Resolve256(p,a,RingOfFireDamage.AtAge(hit.Packet,hit.Motion.Frame),"fire",enhance:false,pierceOverride:0).Damage256;
        Require(first-SkillHealth.Balance256(a)==expected,"World hit did not apply approved elapsed age before mitigation");
        Require(SourceKnockbacks.Snapshot().Any(s=>s.Target==hit.LastCollision),"First contact knockback was absent");
        int after=SkillHealth.Balance256(a);Finish(cast);Require(SkillHealth.Balance256(a)==after,"Same cast damaged a recipient again or repeated terminal damage");
        SourceKnockbacks.Clear(a);SourceKnockbacks.Clear(b);
        long independent=Cast(p,TimedEffects.Now.AddMinutes(5));Finish(independent);Require(SkillHealth.Balance256(a)<after,"Separate cast inherited prior cast delay");
        SourceKnockbacks.Clear(a);SourceKnockbacks.Clear(b);RingOfFireWorld.Clear(p);
    }
    private static void LaterContact()
    {
        var p=Owner(3000);var a=Target(p,2);var b=Target(p,4);int life=SkillHealth.Balance256(b);long cast=Cast(p,TimedEffects.Now.AddMinutes(5),true);bool first=false,second=false;
        for(int n=0;n<10&&Flights(cast).Length>0;n++)
        {
            RingOfFireWorld.Tick(Flights(cast).Min(s=>s.NextUtc),true,cast);var s=Flights(cast).SingleOrDefault();
            if(s?.LastCollision==a.Serial.Value){first=s.Packet!=null;}
            if(s?.LastCollision==b.Serial.Value){second=true;Require(s.Packet!=null&&SourceKnockbacks.Snapshot().Any(k=>k.Target==b.Serial.Value),"Later contact did not retain first packet and knock back");}
        }
        Finish(cast);Require(first&&second&&SkillHealth.Balance256(b)==life,"A pierced later contact dealt extra fire damage");SourceKnockbacks.Clear(a);SourceKnockbacks.Clear(b);
        var immune=Target(p,3,5);immune.MoveToWorld(new(p.X+3,p.Y+8,p.Z),p.Map);a.MoveToWorld(new(p.X-10,p.Y,p.Z),p.Map);b.MoveToWorld(new(p.X-12,p.Y,p.Z),p.Map);
        immune.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);immune.SetResistance(ResistanceType.Fire,100);int before=SkillHealth.Balance256(immune);
        long next=Cast(p,TimedEffects.Now.AddMinutes(5),true);bool pushed=false;
        for(int n=0;n<10&&Flights(next).Length>0;n++){RingOfFireWorld.Tick(Flights(next).Min(s=>s.NextUtc),true,next);pushed|=SourceKnockbacks.Snapshot().Any(s=>s.Target==immune.Serial.Value);}
        Finish(next);Require(pushed&&SkillHealth.Balance256(immune)==before,"Fire immunity incorrectly prevented knockback or took ordinary fire damage");SourceKnockbacks.Clear(immune);RingOfFireWorld.Clear(p);
    }
    private static void Guards()
    {
        var p=Owner(3200);var values=SkillMechanics.Values(p,CombatAbility.RingOfFire);var at=TimedEffects.Now.AddMinutes(5);
        int mana=NecromancerEffects.ManaBalance256(p);Require(RingOfFireWorld.CreatePaidCandidate(p,values,at,_=>false)==0&&NecromancerEffects.ManaBalance256(p)==mana,"All-failed candidate allocations charged mana");
        NecromancerEffects.SetMana256(p,values.Mana256-1);Require(RingOfFireWorld.CreatePaidCandidate(p,values,at)==0,"Underfunded Ring emitted");NecromancerEffects.SetMana256(p,mana);
        long cast=Cast(p,at);p.MoveToWorld(p.Location,Map.Felucca);RingOfFireWorld.Tick(Flights(cast).Min(s=>s.NextUtc),true,cast);Require(Flights(cast).Length==0,"Facet travel retained candidate missiles");
        p.MoveToWorld(new(6400,3200,0),Map.Trammel);cast=Cast(p,at);var snapshot=RingOfFireWorld.Snapshot();var left=Flights(cast).First(s=>s.Motion.Request.TargetX==-30&&s.Motion.Request.TargetY==0);
        // A legitimate outward final in-bounds position from a tile1 launch.
        var edge=left with{Motion=left.Motion with{Frame=1,X=1-4800/8192.0,AccelerationCounter=1,Request=left.Motion.Request with{X=2},Steps=new([new(1,p.Y*2)])}};
        RingOfFireWorld.Restore(snapshot with{Flights=[edge],Leases=[]});RingOfFireWorld.Tick(edge.NextUtc,false,cast);Require(Flights(cast).Length==0,"Offline boundary crossing retained an invalid saved position");
        cast=Cast(p,at);p.Kill();RingOfFireWorld.Tick(Flights(cast).Min(s=>s.NextUtc),true,cast);Require(Flights(cast).Length==0,"Owner death retained candidate missiles");
        Require(!WarlockCombat.Implemented(CombatAbility.RingOfFire),"Unapproved Ring candidate enabled");
    }
    private static void Check()
    {
        FirstArea();LaterContact();Guards();var p=Owner(3400);var target=Target(p,5);SkillHealth.SetBalance256(target,SkillHealth.Balance256(target)-13*256-17);int life=SkillHealth.Balance256(target);
        long cast=Cast(p,TimedEffects.Now.AddMinutes(10));RingOfFireWorld.Tick(Flights(cast).Min(s=>s.NextUtc).AddMilliseconds(9*40),false,cast);
        var saved=RingOfFireWorld.Snapshot();Require(saved.Flights.Length==16&&saved.Flights.All(s=>s.Motion.Frame==10&&s.Packet==null)&&SkillHealth.Balance256(target)==life,"Passive lifetime spent damage or failed to prepare original final-frame missiles");
        string original=JsonSerializer.Serialize(saved);bool rejected=false;try{RingOfFireWorld.Restore(saved with{Flights=[saved.Flights[0] with{Motion=saved.Flights[0].Motion with{Frame=11}}]});}catch(InvalidDataException){rejected=true;}
        Require(rejected&&JsonSerializer.Serialize(RingOfFireWorld.Snapshot())==original,"Malformed Ring restore was not atomic");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,life,p.Location,p.Map.MapID,saved,0)));
        Log.Information("Candidate Ring world:current16fragment launch/once-only mana,first shared explosion/age/mitigation,per-cast delay and independent recasts,piercing later knockback without damage,immune knockback,allocation/mana/facet/death guards. Original16final-frame flights and wounded target prepared for two reloads. Proposed rules only;learned route gated.");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var t=(BaseCreature)World.FindMobile((Serial)saved.Target);p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);
        Require(saved.Generation<2&&JsonSerializer.Serialize(RingOfFireWorld.Snapshot())==JsonSerializer.Serialize(saved.State)&&SkillHealth.Balance256(t)==saved.Life256,"Original Ring state/seed/wounds changed on reload");
        RingOfFireWorld.Tick(saved.State.Flights.Min(s=>s.NextUtc),true,saved.State.Flights[0].Cast);
        Require(RingOfFireWorld.Snapshot().Flights.Length==0&&SkillHealth.Balance256(t)==saved.Life256,"Loaded terminal missiles replayed damage or failed expiry");
        RingOfFireWorld.Restore(saved.State);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
