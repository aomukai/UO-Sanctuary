// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class WakeFirePreparedChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WakeFirePreparedChecks));
    private sealed record Saved(WakeFirePlan Plan,WakeFireControlState[] States);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"wake-fire-prepared-fixtures.json");
    public static void Configure(){Register("wake-fire-prepared-check",Check);Register("wake-fire-prepared-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Wake fire plans/direct controller; learned handler remains disabled",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank,int fire=0,int inferno=0)
    {
        var row=SkillMechanics.For(CombatAbility.WakeOfFire);
        int Hard(string key)=>key=="Fire Trauma"?fire:key=="Inferno Sentry"?inferno:0;
        return new(row,key=>key==row.SourceKey?rank:Hard(key),Hard,99);
    }
    private static void Differential()
    {
        using var clocks=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"wake-of-fire-clock-source-probe.json")));
        int n=0;
        foreach(var r in clocks.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();var clock=AssassinWakeFire.Clock(V(0));
            Require(clock.Action==V(5)&&clock.End==V(6)&&AssassinWakeFire.NeutralThink(V(6),V(1),V(2)!=0)==V(7),"Wake direct source clock/handoff");n++;
        }
        Require(n==96,"Wake clock fixture inventory");
        using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"wake-of-fire-damage-source-probe.json")));
        string[] fields=damage.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();
        int min=Array.IndexOf(fields,"nFireMinDamage"),max=Array.IndexOf(fields,"nFireMaxDamage");n=0;
        foreach(var r in damage.RootElement.GetProperty("cases").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();if(V(5)!=0){continue;}
            var endpoints=AssassinWakeFire.Fire(Values(V(0),V(1),V(2)),V(3));var expected=r[8];
            Require(endpoints==(expected[min].GetInt32(),expected[max].GetInt32()),"Wake full source damage getter");n++;
        }
        Require(n==3711,"Wake supported maker/wave damage fixture inventory");
        using var factory=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"wake-of-fire-factory-source-probe.json")));
        Point2D[] aims=[new(0,0),new(10,0),new(0,-10),new(-12,17),new(99,-99),new(100,0),new(0,-100)];n=0;
        foreach(var r in factory.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();
            // World guards remain separate: compare plans only when the source
            // factory has its owner, row, room, allocation and origin services.
            if(Enumerable.Range(4,5).Any(i=>V(i)==0)){continue;}
            var origin=V(0)!=0?new Point2D(47,79):new Point2D(40,70);
            var plan=AssassinWakeFire.Plan(Values(V(1)),V(0)!=0,origin,aims[V(2)],0,V(3)==2?0:V(3)==1?50:100);
            Require((plan!=null)==(V(9)!=0),"Wake source relative-vector range guard");
            if(plan!=null)
            {
                Require(plan.Origin.X==V(11)&&plan.Origin.Y==V(12)&&plan.Aim.X==V(13)&&plan.Aim.Y==V(14)
                    &&plan.Frames==V(15)&&plan.VelocityFp8==V(16)&&plan.DamageRate==V(18),"Wake source factory position/lifetime/velocity/framerate");
            }
            n++;
        }
        Require(n==210,"Wake source factory supported-plan fixture inventory");
        var zero=new Point2D(0,0);Require(AssassinWakeFire.Perpendicular(zero)==zero,"Rotate original zero vector before factory fallback");
        Require(AssassinWakeFire.Perpendicular(new(12,-17))==new Point2D(17,12),"Wake stored perpendicular");
        Require(!AssassinCombat.Implemented(CombatAbility.WakeOfFire),"Prepared Wake must not expose an unfinished learned action");
    }
    private static WakeFireControlState[] Controller()
    {
        var c=AssassinWakeFire.Start(true);int starts=0,dispatches=0;var states=new WakeFireControlState[4];
        for(int frame=1;frame<=111;frame++)
        {
            c=AssassinWakeFire.Advance(c,true,true,false,true,true,()=>5,()=>new SentryTarget(17,10),()=>true,()=>starts++,serial=>
            {Require(serial==17,"Wake retained target identity");dispatches++;});
            AssassinWakeFire.Validate(c);
            if(frame==15){Require(c.Mode==1&&c.NextThink==25&&starts==0,"Wake emergence preserves AI25");}
            if(frame==25){Require(c.Mode==9&&c.ActionAt==26&&c.EndAt==27&&c.Ai.RemainingShots==4&&starts==1&&dispatches==0,"Wake direct shot entry");states[0]=c;}
            if(frame==26){Require(c.Mode==9&&c.ActionAt==0&&dispatches==1,"Wake shot at first sparse event");states[1]=c;}
            if(frame==27){Require(c.Mode==1&&c.NextThink==42,"Wake end+15 AI handoff");states[2]=c;}
        }
        Require(c.Mode==0&&starts==5&&dispatches==5&&c.Ai.RemainingShots==0,"Wake five attempts release before exhausted exit");states[3]=c;
        var offline=AssassinWakeFire.Inactive(states[0],50);Require(offline.Mode==1&&offline.Target==0&&offline.NextThink==76&&offline.Ai.RemainingShots==4,"Offline mode cancels pending action without shot refund/replay");
        foreach(int distance in new[]{15,16,100})
        {
            var idle=AssassinWakeFire.Start() with{NextThink=1};
            idle=AssassinWakeFire.Advance(idle,true,true,false,true,true,()=>5,()=>new SentryTarget(17,distance),
                ()=>throw new Exception("Out-of-range attack RNG"),()=>throw new Exception("Out-of-range start"),_=>throw new Exception("Out-of-range dispatch"));
            Require(idle.Mode==1&&idle.NextThink==16&&idle.Ai.RemainingShots==5,"Wake active range is strict15, not lightning25");
        }
        return states;
    }
    private static void Check()
    {
        Differential();var saved=new Saved(AssassinWakeFire.Plan(Values(21,20,20),true,new(47,79),new(12,-17),25),Controller());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Prepared Wake direct clocks,3711 current fire endpoints,210 supported factory plans,five-shot controller and offline/range checks pass; real actor/waves/input pending");
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(saved.Plan==AssassinWakeFire.Plan(Values(21,20,20),true,new(47,79),new(12,-17),25)&&saved.States.SequenceEqual(Controller()),"Prepared Wake JSON snapshots across fixture world restart");
        foreach(var state in saved.States){AssassinWakeFire.Validate(state);}
    }
}
