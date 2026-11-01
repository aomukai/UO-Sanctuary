// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class OpenWoundsStateChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(OpenWoundsStateChecks));
    private sealed record Saved(uint Source,uint Other,uint Target,int Life,TimedEffectState State,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"open-wounds-state-fixtures.json");
    public static void Configure()
    {
        Register("open-wounds-state-check",Check);Register("open-wounds-state-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("open-wounds-state-live","Connected real item Open Wounds refresh/lifetime",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("open-wounds-state-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Source wound arithmetic and retained-first-application state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(int row=3120)
    {var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6100,40,row);return p;}
    private static TimedActor State(Mobile target)
    {Require(TimedEffects.TryActor(target,out var a),"Missing bleed actor");return a;}
    private static TimedEffectState Snapshot(Mobile target)=>TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value);
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"open-wounds-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            if(row[6].GetInt32()!=0||row[7].GetInt32()!=1){continue;}
            int type=row[3].GetInt32(),flags=row[5].GetInt32();double value=NamedCombat.OpenWoundsDamage(row[2].GetInt32(),type==0,row[4].GetInt32()==6,type==1&&(flags&12)!=0);
            Require(value==-row[11].GetInt32()*200.0/256,"Native wound arithmetic differs from complete callback at "+count);count++;
        }
        Require(count>5000&&NamedCombat.OpenWoundsDamage(127,false,false,false)>NamedCombat.OpenWoundsDamage(99,false,false,false),"Source cases absent or level99 cap retained");
        Log.Information("Open Wounds source: {Count} native new-state arithmetic comparisons from complete callback/factory,including level127 and integer target reductions",count);
    }
    private static void Direct()
    {
        Source();var p=Owner();var other=Owner();var target=PoisonDaggerChecks.Target(p);
        try
        {
            DateTime at=TimedEffects.Now;int hp=target.Hits;Require(TimedEffects.Bleed(p,target,32,at)&&!TimedEffects.CanRegenerateHits(target),"Initial wound/regeneration suppression");
            TimedEffects.Settle(target,at.AddMilliseconds(200));var a=State(target);double fraction=a.BleedFraction;
            Require(Math.Abs(fraction-0.8)<1e-9&&TimedEffects.Bleed(other,target,80,at.AddMilliseconds(200))&&a.BleedSource==p.Serial&&a.BleedRate==32/8000.0&&a.BleedMs==8000&&a.BleedFraction==fraction,"Active refresh changed first source/rate/fraction");
            TimedEffects.Settle(target,at.AddMilliseconds(8199));Require(State(target).BleedMs==1&&!TimedEffects.CanRegenerateHits(target),"Refresh failed to extend full eight-second deadline");
            TimedEffects.Settle(target,at.AddMilliseconds(8200));Require(target.Hits==hp-32&&TimedEffects.CanRegenerateHits(target),"Wound expiry damage or regeneration release");
            Require(TimedEffects.Bleed(other,target,80,at.AddMilliseconds(8200))&&State(target).BleedSource==other.Serial&&State(target).BleedRate==80/8000.0&&State(target).BleedFraction==0,"Expired wound retained old creator/rate/fraction");
            TimedEffects.Clear(target);Require(!TimedEffects.Bleed(other,target,0,TimedEffects.Now)&&!TimedEffects.Bleed(other,target,double.NaN,TimedEffects.Now),"Invalid bleed input accepted");
            PoisonDaggerChecks.Named(p,"The Gnasher");int chance=ClassGear.Bonus(p,"openwounds");Require(chance>0,"Actual item lacks Open Wounds");
            int seed=Enumerable.Range(0,1000).First(x=>new Random(x).Next(100)<Math.Min(100,chance));NamedCombat.AfterStrike(p,target,false,new Random(seed));
            a=State(target);Require(a.BleedSource==p.Serial&&a.BleedRate==NamedCombat.OpenWoundsDamage(ClassStats.Level(p),target,false)/8000,"Actual named item did not use shared wound state");
        }
        finally{target.Delete();other.Delete();p.Delete();}
        Log.Information("Open Wounds state:first creator/rate/fraction retained on8second refresh,new application after expiry,regen suppression/release,invalid inputs and actual named-item AfterStrike pass;NPC event integration remains pending");
    }
    private static void Check()
    {
        Direct();var p=Owner();var other=Owner();var target=PoisonDaggerChecks.Target(p);DateTime at=TimedEffects.Now;
        TimedEffects.Bleed(p,target,32,at);TimedEffects.Settle(target,at.AddMilliseconds(200));TimedEffects.Bleed(other,target,80,at.AddMilliseconds(200));
        // Hold only this saved observation clock; Live tests normal deadlines.
        State(target).Updated=TimedEffects.Now.AddHours(1);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,other.Serial.Value,target.Serial.Value,target.Hits,Snapshot(target),0)));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Source);var other=(PlayerMobile)World.FindMobile((Serial)s.Other);var target=(GraveyardSkeleton)World.FindMobile((Serial)s.Target);var a=State(target);
        Require(JsonSerializer.Serialize(Snapshot(target))==JsonSerializer.Serialize(s.State)&&target.Hits==s.Life&&a.BleedSource==p.Serial&&a.BleedRate==32/8000.0&&a.BleedMs==8000,"Saved wound clock/source/rate/fraction changed");
        DateTime at=a.Updated.AddMilliseconds(100);TimedEffects.Settle(target,at);double fraction=State(target).BleedFraction;TimedEffects.Bleed(other,target,80,at);
        Require(State(target).BleedFraction==fraction&&State(target).BleedSource==p.Serial&&State(target).BleedRate==32/8000.0&&State(target).BleedMs==8000,"Reload refresh replaced original application");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Life=target.Hits,State=Snapshot(target),Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(3600);FixtureNetwork.Authenticate(network,p,"open-wounds");var other=Owner(3650);var target=PoisonDaggerChecks.Target(p);target.SetResistance(ResistanceType.Physical,100);
        PoisonDaggerChecks.Named(p,"The Gnasher");int chance=ClassGear.Bonus(p,"openwounds"),seed=Enumerable.Range(0,1000).First(x=>new Random(x).Next(100)<Math.Min(100,chance));
        int hp=target.Hits;double total=NamedCombat.OpenWoundsDamage(ClassStats.Level(p),target,false);NamedCombat.AfterStrike(p,target,false,new Random(seed));double rate=State(target).BleedRate;long start=Core.TickCount,deadline=start+14000;bool refreshed=false;
        void End(){target.Delete();other.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&network.Running&&target.Alive,"Natural wound timeout/death/disconnect");
                if(!refreshed&&Core.TickCount-start>=1000)
                {Require(TimedEffects.Bleed(other,target,total*2,TimedEffects.Now)&&State(target).BleedSource==p.Serial&&State(target).BleedRate==rate,"Natural refresh replaced rate/source");refreshed=true;}
                if(TimedEffects.TryActor(target,out var a)&&a.BleedMs>0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(refreshed&&Core.TickCount-start>=8900&&hp-target.Hits>total&&hp-target.Hits<total*1.25&&TimedEffects.CanRegenerateHits(target),"Natural refreshed expiry/rate/physical-immunity/regen contract");End();Log.Information("open-wounds-state-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"open-wounds-state-live-result: failed");}
        }
        Log.Information("open-wounds-state-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
