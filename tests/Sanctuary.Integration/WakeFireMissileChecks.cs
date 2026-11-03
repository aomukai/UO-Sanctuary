// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class WakeFireMissileChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WakeFireMissileChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,SentryBodyState Actor,WakeFireSave Pool,int Health,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"wake-fire-missile-fixtures.json");
    public static void Configure(){Register("wake-fire-missile-check",Check);Register("wake-fire-missile-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Real prepared Wake maker/waves, hit windows and original pool persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Atomic()
    {
        var before=AssassinWakeFireMissiles.Snapshot();string exact=JsonSerializer.Serialize(before);var row=before.Flights.Last();
        foreach(var bad in new[]{before with{Flights=before.Flights.Append(row).ToArray()},before with{Flights=before.Flights.Append(null).ToArray()},
            before with{Flights=before.Flights.Where(x=>x.Id!=row.Id).Append(row with{Frame=row.Frames}).ToArray()},
            before with{Leases=before.Leases.Append(new(long.MaxValue,1,DateTime.UtcNow)).ToArray()}})
        {
            bool refused=false;try{AssassinWakeFireMissiles.Restore(bad);}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(AssassinWakeFireMissiles.Snapshot())==exact,"Malformed Wake tail changed pool/leases/next-id");
        }
    }
    private static void ContactGroups(BladeMissileTarget target,WakeFireFlight template)
    {
        var original=AssassinWakeFireMissiles.Snapshot();DateTime at=template.NextUtc.AddHours(2);
        try
        {
            var first=template with{Id=original.NextId+1,Execution=template.Execution,Frame=0,Frames=20,
                X=target.X-.05,Y=target.Y,Vx=.1,Vy=0,Minimum256=25600,Maximum256=25600,
                LastCollision=0,Steps=new([new(target.X*2,target.Y*2)]),NextUtc=at};
            var second=first with{Id=first.Id+1};
            WakeFireSave Candidate(bool independent)=>new([first,independent?second with{Execution=second.Id}:second],[],second.Id);
            AssassinWakeFireMissiles.Restore(Candidate(false));int before=SkillHealth.Balance256(target);
            AssassinWakeFireMissiles.TickFrame(at,true);int shared=before-SkillHealth.Balance256(target);
            Require(shared>0&&AssassinWakeFireMissiles.Snapshot().Flights.Count(x=>x.LastCollision==target.Serial.Value)==1,"Same-execution overlapping waves did not share contact exclusion");
            AssassinWakeFireMissiles.Restore(Candidate(true));before=SkillHealth.Balance256(target);
            AssassinWakeFireMissiles.TickFrame(at,true);int independent=before-SkillHealth.Balance256(target);
            Require(independent==shared*2&&AssassinWakeFireMissiles.Snapshot().Flights.All(x=>x.LastCollision==target.Serial.Value),"Independent supplied executions suppressed one another's actual damage");
        }
        finally{AssassinWakeFireMissiles.Restore(original);}
    }
    private static void Check()
    {
        var p=BladeBodyChecks.Fresh(6900,24);p.Name="WakeMissileFixture";
        while(ClassCreation.Profiles.GetRank(p.Serial,"assassin.wakeoffire")<3){Require(ClassCreation.Profiles.TryTrain(p.Serial,"assassin.wakeoffire",out _),"Train Wake");}
        DateTime start=DateTime.UtcNow.AddDays(1);
        var body=AssassinSentryBody.CreateEmerging(p,new(p.X+2,p.Y,p.Z),start,SourceRandom.InitLow(91),CombatAbility.WakeOfFire);
        var target=new BladeMissileTarget{NoKillAwards=true,CantWalk=true,Frozen=true};target.MoveToWorld(new(body.X+2,body.Y+1,body.Z),body.Map);target.AIObject?.AITimer.Stop();
        target.SetHits(100000);target.Hits=target.HitsMax;target.SetResistance(ResistanceType.Fire,0);
        int health=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);
        long id=AssassinWakeFireMissiles.Create(body,new(body.X*2+20,body.Y*2));var root=AssassinWakeFireMissiles.State(id);
        Require(root is{Wave:false,Frame:0,Frames:31,PathChanged:false,PerpendicularX:0,PerpendicularY:20}&&root.Actor==body.Serial.Value&&root.Execution==id,"Maker launch/rank/original perpendicular");
        AssassinWakeFireMissiles.TickFrame(root.NextUtc,true);
        Require(AssassinWakeFireMissiles.Snapshot().Flights.Length==1,"Maker emitted before prior movement flag");
        bool pair=false;
        for(int i=0;i<30&&(!pair||SkillHealth.Balance256(target)==health);i++)
        {
            AssassinWakeFireMissiles.TickFrame(AssassinWakeFireMissiles.NextFrame,true);
            var children=AssassinWakeFireMissiles.Snapshot().Flights.Where(x=>x.Wave).ToArray();
            if(children.Length>=2)
            {
                pair=true;Require(children.All(x=>x.Execution==id&&x.Actor==body.Serial.Value&&x.Owner==p.Serial.Value&&x.Frames==20),"Child original identity/lifetime");
                Require(children.Any(x=>x.Vy>0)&&children.Any(x=>x.Vy<0)&&children.All(x=>x.Vx==0),"Opposite perpendicular wave velocities");
            }
        }
        Require(pair&&SkillHealth.Balance256(target)<health&&NecromancerEffects.ManaBalance256(p)==mana,"Real moving fire waves did not damage/charged additional owner mana");
        var captured=AssassinWakeFireMissiles.Snapshot();var contact=captured.Flights.First(x=>x.Wave&&x.LastCollision==target.Serial.Value);
        DateTime collision=contact.NextUtc.AddMilliseconds(-40);
        Require(AssassinWakeFireMissiles.Delayed(id,target.Serial.Value,collision.AddMilliseconds(120))
            &&!AssassinWakeFireMissiles.Delayed(id,target.Serial.Value,collision.AddMilliseconds(160)),"Four-frame exact lease boundary");
        long other=AssassinWakeFireMissiles.Create(body,new(body.X*2+20,body.Y*2));
        Require(!AssassinWakeFireMissiles.Delayed(other,target.Serial.Value,collision),"Independent supplied execution inherited another hit window");
        ContactGroups(target,contact);Atomic();
        // Freeze the whole actual pool at its original future inspection clock.
        // Offline expiry is tested separately below; this is identity continuity.
        SkillHealth.SetBalance256(body,12850);health=SkillHealth.Balance256(target);
        var held=AssassinWakeFireMissiles.Snapshot();
        var offline=held with{Flights=held.Flights.Select(x=>x with{NextUtc=start.AddHours(1)}).ToArray()};AssassinWakeFireMissiles.Restore(offline);
        for(int i=0;i<600&&AssassinWakeFireMissiles.Snapshot().Flights.Length>0;i++){AssassinWakeFireMissiles.TickFrame(AssassinWakeFireMissiles.NextFrame,false);}
        Require(AssassinWakeFireMissiles.Snapshot().Flights.Length==0&&AssassinWakeFireMissiles.Snapshot().Leases.Length==0&&SkillHealth.Balance256(target)==health,"Offline Wake emitted, damaged or stopped expiry");
        AssassinWakeFireMissiles.Restore(held);
        // Receiver is moved off the held inspection path before the real save.
        target.MoveToWorld(new(target.X,target.Y+8,target.Z),target.Map);p.Internalize();
        Write(new(p.Serial.Value,body.Serial.Value,target.Serial.Value,body.Prepared,held,health,mana,0));
        Require(!AssassinCombat.Implemented(CombatAbility.WakeOfFire),"Prepared backend enabled learned casting");
    }
    private static void Write(Saved saved)=>File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var body=(AssassinSentryBody)World.FindMobile((Serial)saved.Body);var target=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);
        Require(saved.Generation<2&&JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.Actor)
            &&JsonSerializer.Serialize(AssassinWakeFireMissiles.Snapshot())==JsonSerializer.Serialize(saved.Pool)
            &&SkillHealth.Balance256(body)==12850&&SkillHealth.Balance256(target)==saved.Health&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Wake original actor/flights/seed/history/leases/wounds/resources changed on reload");
        Atomic();var old=AssassinWakeFireMissiles.Snapshot();DateTime at=AssassinWakeFireMissiles.NextFrame;
        AssassinWakeFireMissiles.TickFrame(at,true);var next=AssassinWakeFireMissiles.Snapshot();
        Require(old.Flights.Where(x=>x.NextUtc==at).All(x=>AssassinWakeFireMissiles.State(x.Id) is{} n&&n.Frame==x.Frame+1)
            &&next.NextId>=old.NextId&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Original Wake flights did not resume once");
        Write(saved with{Pool=next,Health=SkillHealth.Balance256(target),Generation=saved.Generation+1});
    }
}
