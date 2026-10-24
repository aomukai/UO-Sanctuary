// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SentryTargetChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SentryTargetChecks));
    public static void Configure(){Register("sentry-target-check");Register("sentry-target-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Source sentry target selection and charged-bolt path reuse",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-target-source-probe.json"))))
        {
            int cases=0;
            foreach(var row in doc.RootElement.EnumerateArray())
            {
                var input=row.GetProperty("input");var fields=row.GetProperty("candidates").EnumerateArray().ToArray();
                var candidates=fields.Select(f=>new SentryCandidate(f[0].GetUInt32(),f[1].GetInt32(),f[2].GetInt32(),f[3].GetInt32(),f[4].GetInt32()==0,
                    f[5].GetInt32()!=0?f[6].GetInt32():null)).ToArray();
                var result=AssassinSentryTargets.Select(new(100,100),candidates,c=>fields[c.Serial-1][7].GetInt32()!=0,
                    c=>fields[c.Serial-1][8].GetInt32()!=0,_=>input[4].GetInt32()!=0,input[5].GetInt32()!=0?new(new(99,0,0,0),7):null);
                var expected=row.GetProperty("selected");Require((result?.Candidate.Serial??0)==expected[0].GetUInt32()&&(result?.Distance??int.MaxValue)==expected[1].GetInt32(),"Complete source target choice differential "+cases);cases++;
            }
            Require(cases==4096,"Target-selection reference inventory");
        }
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"charged-bolt-path-source-probe.json"))))
        {
            int cases=0;
            foreach(var row in doc.RootElement.EnumerateArray())
            {
                int origin=row[0].GetInt32(),dx=row[1].GetInt32(),dy=row[2].GetInt32(),distance=row[4].GetInt32();uint seed=row[3].GetUInt32();
                int heading=ChargedBolts.Heading(unchecked((ushort)(origin+dx))-origin,unchecked((ushort)(origin+dy))-origin);
                Require(heading==row[5].GetInt32(),"Shared charged-bolt heading differential "+cases);
                var path=ChargedBolts.Path(seed,heading,distance);var expected=row[7];Require(path.Length==expected.GetArrayLength(),"Complete source path length "+cases);
                for(int i=0;i<path.Length;i++)
                {Require(unchecked((ushort)(origin+2*path[i].X))==expected[i][0].GetInt32()&&unchecked((ushort)(origin+2*path[i].Y))==expected[i][1].GetInt32(),"Shared complete charged-bolt path differential "+cases);}
                ulong state=SourceRandom.InitLow(unchecked((int)seed));for(int i=1;i<path.Length;i++){SourceRandom.Roll(ref state);}
                Require(state==row[6].GetUInt64(),"Shared final path RNG seed differential "+cases);cases++;
            }
            Require(cases==2160,"Charged-bolt path reference inventory");
        }
    }
    private static void UnsupportedFallback()
    {
        bool refused=false;
        try{AssassinSentryTargets.Select(new(100,100),[new(1,114,100,2,Threat:10),new(2,104,100,2)],_=>true,_=>false,_=>false);}
        catch(NotSupportedException){refused=true;}
        Require(refused,"Missing monster row silently invented callback7 threat metadata");
    }
    private static void Check()
    {
        Differential();UnsupportedFallback();var p=BladeBodyChecks.Fresh(6200,24);p.Name="SentryTargetFixture";
        AssassinSentryBody body=null;GraveyardSkeleton near=null,far=null;
        try
        {
            body=AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),DateTime.UtcNow.AddDays(1),SourceRandom.InitLow(1));
            near=new(){Name="SentryTargetNear",NoKillAwards=true};far=new(){Name="SentryTargetFar",NoKillAwards=true};
            near.MoveToWorld(new(body.X+2,body.Y,body.Z),body.Map);far.MoveToWorld(new(body.X+6,body.Y,body.Z),body.Map);near.AIObject?.AITimer.Stop();far.AIObject?.AITimer.Stop();
            var a=AssassinSentryWorldTargets.Select(body);Require(a?.Serial==near.Serial.Value,"Real mapped target choice did not select source-nearest threat");
            near.MoveToWorld(new(body.X+16,body.Y,body.Z),body.Map);a=AssassinSentryWorldTargets.Select(body);Require(a?.Serial==far.Serial.Value,"Target movement retained the previous sequence aim as AI override");
            var wall=new Static(0x80);wall.MoveToWorld(new(body.X+1,body.Y,body.Z),body.Map);
            try{Require(!body.InLOS(far),"Native barrier fixture does not obstruct LOS");Require(AssassinSentryWorldTargets.Select(body)==null,"Missile-barrier projection retained obstructed enemies");}
            finally{wall.Delete();}
            p.MoveToWorld(p.Location,Map.Felucca);a=AssassinSentryWorldTargets.Select(body);Require(a?.Serial==far.Serial.Value,"Owner-facet travel changed retained actor-local target scan");
            far.Delete();near.MoveToWorld(new(body.X+13,body.Y,body.Z),body.Map);a=AssassinSentryWorldTargets.Select(body);
            Require(a?.Serial==near.Serial.Value&&a?.Distance==25,$"Mapped full-size distance25 boundary: result={a} expected={near.Serial} near={near.Location} body={body.Location} visible={body.InLOS(near)} eligible={body.TrapTarget(p,near)}");
            body.AdvanceController(body.Prepared.NextFrameUtc.AddMilliseconds(24*40),true);
            Require(body.Prepared.Control is{Thinks:1,Attempts:0,Ai.RemainingShots:5},"Distance25 selected target caused a firing attempt");
            near.Delete();Require(AssassinSentryWorldTargets.Select(body)==null,"Dead/deleted target remained eligible");
            Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Target adapter enabled unfinished learned input");
            Log.Information("Sentry target:4096 complete selector and2160 complete shared charged-bolt heading/seed/path source comparisons pass; actual mapped enemy movement, visibility, remote owner and distance25 target/AI boundary pass");
        }
        finally{near?.Delete();far?.Delete();body?.Delete();p.Delete();}
    }
}
