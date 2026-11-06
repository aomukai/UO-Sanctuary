// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class InfernoStreamChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(InfernoStreamChecks));
    private sealed record Saved(uint Owner,uint Target,int Life256,SorceressSpellFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"inferno-stream-fixtures.json");
    private static int _scene;
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    public static void Configure()
    {
        Register("inferno-stream-check",Check);Register("inferno-stream-reload-check",Reload);
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Inferno in the shared Sorceress flight host",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Name="InfernoStreamFixture",Body=0x190,Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Corridor(p,6230,20,2600+32*_scene++);return p;
    }
    private static SkillValues Values(int rank=1,int hard=0)=>new(SkillMechanics.For(CombatAbility.Inferno),_=>rank,key=>key=="Warmth"?hard:0,99);
    private static ShockWaveTarget Target(PlayerMobile p)
    {var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Fire,0);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;}
    private static SorceressSpellFlight Emit(PlayerMobile p,DateTime at)
    {var shot=SorceressSpellMissiles.PrepareInferno(p,new(p.X*2+20,p.Y*2),Values(),at);Require(shot!=null,"Missing Inferno emission");SorceressSpellMissiles.Commit(shot);return shot;}
    private static void Arithmetic()
    {
        using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"inferno-stream-damage-source-probe.json")));int count=0;
        foreach(var c in damage.RootElement.EnumerateArray())
        {
            var p=SorceressSpellMissiles.CaptureInferno(Values(c[0].GetInt32(),c[1].GetInt32()),c[2].GetInt32());
            Require(p.Min==c[4].GetInt32()&&p.Max==c[5].GetInt32()&&p.Frames==(35+3*(c[0].GetInt32()-1))/2,"Inferno fire packet differs from complete source");count++;
        }
        Require(count==4590,"Incomplete Inferno damage matrix");
        var owner=Fresh();var slow=AmazonSlowMissiles.Snapshot();var allocate=SorceressSpellMissiles.AllocateInferno;var seed=SorceressSpellMissiles.SeedLow;int factories=0,calls=0,draws=0;
        try
        {
            SorceressSpellMissiles.SeedLow=()=>{draws++;return 1;};
            using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"inferno-stream-factory-source-probe.json")));
            foreach(var c in data.RootElement.EnumerateArray())
            {
                int rank=c[0].GetInt32(),dx=c[1].GetInt32(),dy=c[2].GetInt32();bool slowed=c[3].GetInt32()!=0,allocated=c[4].GetInt32()!=0;
                AmazonSlowMissiles.Restore(slow.Where(s=>s.Target!=owner.Serial.Value).Concat(slowed?[new SlowMissilesState(owner.Serial.Value,owner.Serial.Value,1,50,100,TimedEffects.Now.AddHours(2))]:[]).ToArray());
                SorceressSpellMissiles.AllocateInferno=()=>{calls++;return allocated;};int before=calls,beforeDraws=draws;
                var shot=SorceressSpellMissiles.PrepareInferno(owner,new(owner.X*2+dx,owner.Y*2+dy),Values(rank),TimedEffects.Now.AddHours(1));
                Require((shot!=null)==(c[5].GetInt32()!=0)&&calls-before==c[6].GetInt32(),"Inferno factory allocation/delta boundary");
                // Native creator seed allocation is an explicit adapter. The
                // original factory fixture supplies its nested creator.
                Require(draws-beforeDraws==(shot==null?0:1),"Failed emission consumed native seed");
                if(shot!=null)
                {
                    var src=c[8];if(dx==0&&dy==0){dx=dy=1;}var vector=MissileVectors.VelocityFp8(dx,dy,src[4].GetInt32());
                    Require(shot.VelocityFp8==src[4].GetInt32()&&shot.Vx==vector.X&&shot.Vy==vector.Y&&shot.Frames==src[5].GetInt32()
                        &&shot.LastCollision==owner.Serial.Value&&shot.Rank==rank,"Inferno velocity/lifetime/owner differs");
                }
                factories++;
            }
            Require(factories==14280,"Incomplete Inferno factory matrix");
        }
        finally{SorceressSpellMissiles.AllocateInferno=allocate;SorceressSpellMissiles.SeedLow=seed;AmazonSlowMissiles.Restore(slow);owner.Delete();}
        Log.Information("Inferno:4590fire source packets/14280complete-factory controls pass");
    }
    private static void Cases()
    {
        var held=SorceressSpellMissiles.Snapshot();var p=Fresh();var a=Target(p);var b=Target(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention);var slow=AmazonSlowMissiles.Snapshot();
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p);var shot=Emit(p,at);
            Require(shot.Minimum256==288&&shot.Maximum256==576&&shot.Frames==17&&shot.LastCollision==p.Serial.Value,"Rank1 Inferno packet/source history");
            var fixedHit=shot with{Minimum256=25600,Maximum256=25600};int life=SkillHealth.Balance256(a);
            SorceressSpellMissiles.Impact(p,a,fixedHit,out _);Require(SkillHealth.Balance256(a)==life-25600&&ColdStates.OtherRate(a)==0,"Pure fire health or unexpected cold state");
            a.SetResistance(ResistanceType.Fire,100);life=SkillHealth.Balance256(a);SorceressSpellMissiles.Impact(p,a,fixedHit,out _);
            Require(SkillHealth.Balance256(a)==life,"Inferno ignored fire immunity");a.SetResistance(ResistanceType.Fire,0);
            SorceressSpellMissiles.Tick(at.AddMilliseconds(40),true);var first=SorceressSpellMissiles.Snapshot().Flights.Single(s=>s.Id==shot.Id);
            Require(first.LastCollision==a.Serial.Value||first.LastCollision==b.Serial.Value,"Real stream missed stacked source contacts");
            SorceressSpellMissiles.Tick(at.AddMilliseconds(80),true);var second=SorceressSpellMissiles.Snapshot().Flights.Single(s=>s.Id==shot.Id);
            Require(second.LastCollision!=first.LastCollision&&second.LastCollision!=p.Serial.Value,"LastCollide did not alternate recipients");
            var state=SorceressSpellMissiles.Snapshot();string text=Json(state);bool refused=false;
            try{SorceressSpellMissiles.Restore(state with{Flights=state.Flights.Append(shot).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&Json(SorceressSpellMissiles.Snapshot())==text,"Duplicate Inferno restore was not atomic");
            SorceressSpellMissiles.Tick(at.AddSeconds(2),false);Require(SorceressSpellMissiles.Snapshot().Flights.All(s=>s.Owner!=p.Serial.Value),"Offline Inferno lifetime froze");
            var overdue=Emit(p,TimedEffects.Now.AddSeconds(-2));state=SorceressSpellMissiles.Snapshot();life=SkillHealth.Balance256(a);
            SorceressSpellMissiles.Restore(JsonSerializer.Deserialize<SorceressSpellSave>(Json(state)));SorceressSpellMissiles.Tick(TimedEffects.Now,true);
            Require(SorceressSpellMissiles.Snapshot().Flights.All(s=>s.Id!=overdue.Id)&&SkillHealth.Balance256(a)==life,"Restart replayed overdue fire damage");
            AmazonSlowMissiles.Restore(slow.Append(new SlowMissilesState(p.Serial.Value,p.Serial.Value,1,50,50,at.AddHours(2))).ToArray());refused=false;
            try{Emit(p,at);}catch(NotSupportedException){refused=true;}Require(refused,"Unknown modern slow damage ordering silently guessed");AmazonSlowMissiles.Restore(slow);
            Require(NecromancerEffects.ManaBalance256(p)==mana&&SorceressCombat.Implemented(CombatAbility.Inferno),"Stream charged mana or approved handler missing");
        }
        finally{SorceressSpellMissiles.Restore(held);AmazonSlowMissiles.Restore(slow);a.Delete();b.Delete();p.Delete();}
    }
    private static void Check()
    {
        Arithmetic();Cases();var p=Fresh();var t=Target(p);var shot=Emit(p,TimedEffects.Now.Add(FixtureSpace.Retention));p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,SkillHealth.Balance256(t),shot,0)));
    }
    private static void Reload()
    {
        Arithmetic();Cases();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(s.Generation<2&&World.FindMobile((Serial)s.Owner) is PlayerMobile p&&p.Map==Map.Internal
            &&World.FindMobile((Serial)s.Target) is ShockWaveTarget target&&SkillHealth.Balance256(target)==s.Life256
            &&Json(SorceressSpellMissiles.Snapshot().Flights.Single(f=>f.Id==s.Flight.Id))==Json(s.Flight),"Original Inferno owner/packet/history/seed/clock changed");
        File.WriteAllText(Manifest,Json(s with{Generation=s.Generation+1}));
    }
}
