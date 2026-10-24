// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BladeControllerChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeControllerChecks));
    private sealed record Saved(uint Owner,uint Body,int OriginX,int OriginY,BladeBodyState State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-controller-fixtures.json");
    public static void Configure(){Register("blade-controller-check",Check);Register("blade-controller-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared blade automatic world controller/expiry; no learned skill",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static (PlayerMobile Owner,BladeSentinelBody Body,int X,int Y) Fresh(DateTime at)
    {
        var p=BladeBodyChecks.Fresh();int x=p.X*2,y=p.Y*2;
        var pose=new ActorMotionState((uint)((x<<16)+32768),(uint)((y<<16)+32768),[],new(0,0),0,2304,2304,0,0,new(0,0),new(0,0),20,0x800,0,[],0);
        var body=BladeSentinelBody.CreatePrepared(p,BladeBodyChecks.Creation(p),pose,p.Z);p.MoveToWorld(new(p.X-2,p.Y,p.Z),p.Map);
        body.StartPreparedController(new(x+8,y),2304,SourceRandom.InitLow(19),at);return (p,body,x,y);
    }
    private static void Oracle(BladeSentinelBody body,int x,int y)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-clock-source-probe.json")));
        var row=doc.RootElement.EnumerateArray().Single(r=>r.GetProperty("input")[0].GetInt32()==0 && r.GetProperty("input")[1].GetInt32()==8 && r.GetProperty("input")[2].GetInt32()==3 && r.GetProperty("input")[3].GetInt32()==1);
        var state=body.Prepared;var c=state.Control;var expected=row.GetProperty("frames")[c.Frame-1];
        Require(state.Motion.X==expected[1].GetUInt32()+(long)(x-100)*65536 && state.Motion.Y==expected[2].GetUInt32()+(long)(y-100)*65536
            && c.Mode==expected[0].GetInt32() && c.Ai.Expiry==expected[9].GetInt32() && c.Ai.Forward==(expected[10].GetInt32()!=0)
            && c.Creates==1 && c.Thinks==expected[15].GetInt32() && c.Moves==expected[16].GetInt32() && state.ControllerFault==null,
            "Actual world blade controller diverged from composed source clock");
    }
    private static void Natural(string marker)
    {
        var old=BladeSentinelActors.CreateFollowingMissile;
        var created=Fresh(TimedEffects.Now);
        try
        {
            // Explicit source allocation-failure service: movement/lifetime is
            // tested independently of the still-unbound real missile factory.
            BladeSentinelActors.CreateFollowingMissile=b=>b.Prepared.Creation;
            created.Body.AdvanceController(created.Body.Prepared.NextFrameUtc);
        }
        finally{BladeSentinelActors.CreateFollowingMissile=old;}
        var body=created.Body;var p=created.Owner;
        Timer.StartTimer(TimeSpan.FromMilliseconds(6300),()=>
        {
            try{Require(body.Deleted && body.Prepared.Control.Mode==0 && body.Prepared.Control.Frame>body.Prepared.Control.Ai.Expiry && body.Prepared.Control.Creates==1,"Natural controller movement/AI-observed expiry/once-only factory attempt");p.Delete();Log.Information("{Marker}: passed",marker);}
            catch(Exception e){body.Delete();p.Delete();Log.Error(e,"{Marker}: failed",marker);}
        });
    }
    private static void Check()
    {
        var old=BladeSentinelActors.CreateFollowingMissile;var created=Fresh(TimedEffects.Now.AddHours(1));
        try
        {
            BladeSentinelActors.CreateFollowingMissile=b=>b.Prepared.Creation;
            created.Body.AdvanceController(created.Body.Prepared.NextFrameUtc.AddMilliseconds(19*40));Oracle(created.Body,created.X,created.Y);
        }
        finally{BladeSentinelActors.CreateFollowingMissile=old;}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(created.Owner.Serial.Value,created.Body.Serial.Value,created.X,created.Y,created.Body.Prepared)));
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Prepared controller enabled unfinished learned skill");Natural("blade-controller-result");
        Log.Information("Blade world controller: actual source pose/reversal/first-attempt state matches frame20; source allocation failure is explicit");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var body=World.FindMobile((Serial)saved.Body) as BladeSentinelBody;
        Require(body!=null && JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.State) && body.SummonMaster?.Serial.Value==saved.Owner,"Saved actual blade controller/phase/RNG/next-time mismatch");
        // Disconnected player fixtures load on the internal map. This test
        // resumes a live world owner; offline-owner policy remains separate.
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(new(saved.OriginX/2-2,saved.OriginY/2,body.Z),body.Map);
        int frame=body.Prepared.Control.Frame;body.AdvanceController(body.Prepared.NextFrameUtc.AddMilliseconds(9*40));Require(body.Prepared.Control.Frame==frame+10,"Saved controller did not advance exact frames");Oracle(body,saved.OriginX,saved.OriginY);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=body.Prepared}));Natural("blade-controller-reload-result");
        Log.Information("Blade world controller reload: source phase/pose/RNG/expiry and once-only attempt resumed without factory rebinding");
    }
}
