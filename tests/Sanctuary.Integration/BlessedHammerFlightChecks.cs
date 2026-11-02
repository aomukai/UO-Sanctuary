// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
namespace Sanctuary.Integration;

public static class BlessedHammerFlightChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BlessedHammerFlightChecks));
    private sealed record Saved(uint Owner,int Map,Point3D Origin,int Mana,int Life,HammerFlight Flight,int Generation,uint DeletedCollider=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blessed-hammer-flight-checks.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Hammer curved source cells/RNG and natural original-flight reload",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("blessed-hammer-flight-check",Check);Register("blessed-hammer-flight-reload-check",()=>FixtureNetwork.Connected("blessed-hammer-flight-reload-result",Reload));
        foreach(string mode in new[]{"left","right","quick"})
        {string label="blessed-hammer-flight-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static bool ClearCurve(PlayerMobile p,Point3D point)
    {
        var points=BlessedHammers.Waypoints().Select(q=>new Point2D(point.X*2+q.X,point.Y*2+q.Y)).ToArray();
        var s=new ActorMotionState((uint)(point.X*131072+32768),(uint)(point.Y*131072+32768),points,points[^1],0,3456,7680,250,0,new(0,0),new(0,0),0,0x60020,0,[],0,14);
        s=SourceActorMotion.DirectionToNext(s);
        for(int i=0;i<119;i++)
        {
            double x=(s.X-32768)/131072.0,y=(s.Y-32768)/131072.0;
            var result=SourceActorMotion.Advance(s,(cx,cy)=>DruidFirestorm.CellBlocked(p.Map,point.Z,x,y,cx,cy)?4:0);
            if(!result.Moving){return false;}s=result.State;
        }
        return true;
    }
    private static PlayerMobile Owner()
    {
        var p=BlessedHammerDamageChecks.Owner();p.MoveToWorld(ZealChecks.Origin(p,point=>
            HolyBolts.Snapshot().Any(f=>f.Map==p.Map.MapID&&Math.Abs(f.X-point.X)<30&&Math.Abs(f.Y-point.Y)<30)
            ||BlessedHammers.Snapshot().Any(f=>f.Motion!=null&&f.Map==p.Map.MapID&&Math.Abs(f.OriginX-point.X)<30&&Math.Abs(f.OriginY-point.Y)<30)
            ||!ClearCurve(p,point)),p.Map);return p;
    }
    private static T Target<T>(PlayerMobile p,T t,int dx=0,int dy=1) where T:BaseCreature
    {
        t.NoKillAwards=t.Frozen=t.CantWalk=true;t.SetHits(30000);t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static HammerFlight Create(PlayerMobile p)
    {BlessedHammers.Cast(p,SkillMechanics.Values(p,CombatAbility.BlessedHammer));return BlessedHammers.Snapshot().Where(f=>f.Owner==p.Serial.Value).MaxBy(f=>f.Id);}
    private static void Through(long id,int frames,bool actions=true)
    {for(int i=0;i<frames&&BlessedHammers.State(id) is{} f;i++){BlessedHammerFlights.TickFlight(id,f.NextUtc,actions);}}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {foreach(var f in BlessedHammers.Snapshot().Where(f=>f.Owner==p.Serial.Value)){BlessedHammers.Remove(f.Id);}AmazonSlowMissiles.Clear(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void Check()
    {
        // Parent regression uses only its existing APIs before Details is JITed.
        var p=SacrificeContactChecks.Owner();p.MoveToWorld(ZealChecks.Origin(p),p.Map);var t=Target(p,new ConversionContactChecks.Recipient());int life=SkillHealth.Balance256(t);BlessedHammers.Cast(p,SkillMechanics.Values(p,CombatAbility.BlessedHammer));
        Timer.StartTimer(TimeSpan.FromMilliseconds(800),()=>Core.LoopContext.Post(()=>
        {
            try
            {
                Require(SkillHealth.Balance256(t)==life,"Disconnected Hammer replayed combat instead of harmless aging");
                BlessedHammers.Restore(BlessedHammers.Snapshot().Where(f=>f.Owner!=p.Serial.Value).ToArray());t.Delete();p.Delete();Details();Log.Information("blessed-hammer-flight-result: passed");
            }
            catch(Exception e){t.Delete();p.Delete();Log.Error(e,"blessed-hammer-flight-result: failed");}
        }));
    }
    private static void Details()
    {
        var seed=BlessedHammerFlights.SeedLow;var roll=BlessedHammers.Roll;
        try
        {
            BlessedHammerFlights.SeedLow=()=>0;BlessedHammers.Roll=_=>throw new InvalidOperationException("Source Hammer used compatibility RNG");
            BlessedHammerMotionChecks.Check();PathAndSeed();Controls();Atomic();Save();
            Log.Information("Hammer flight:12960complete source frames;6795FP first seeded hit/7098FP next;captured curved path/last collider/deep copy;10lifecycle/wall/slow controls;final119/120;atomic/legacy restore;original paid moving flight staged");
        }
        finally{BlessedHammerFlights.SeedLow=seed;BlessedHammers.Roll=roll;}
    }
    private static void PathAndSeed()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());var second=Target(p,new ConversionContactChecks.Recipient(),-1,0);
        try
        {
            second.MoveToWorld(new(p.X+15,p.Y+15,p.Z),p.Map);int life=SkillHealth.Balance256(t);var f=Create(p);Through(f.Id,1);var first=BlessedHammers.State(f.Id);
            Require(life-SkillHealth.Balance256(t)==6795&&t.Notifications==1&&first.Frame==1&&first.XFp16==0&&first.YFp16==55296
                &&first.Motion.Seed==1193071124610UL&&first.LastCollision==t.Serial.Value,"Hammer first source cells/2draws/species/captured damage");
            var before=first;first.Motion.Path.Points[0]=new(-1,-1);first.Motion.Path.SavedSteps[0]=new(-1,-1);
            Require(BlessedHammers.State(f.Id).Motion.Path.Points[0].X>=0&&BlessedHammers.State(f.Id).Motion.Path.SavedSteps[0].X>=0,"Hammer snapshot aliases live curve/cells");
            Through(f.Id,1);Require(BlessedHammers.State(f.Id).Motion.Seed==before.Motion.Seed&&t.Notifications==1,"Most recent Hammer collider consumed another draw");
            second.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);int sh=SkillHealth.Balance256(second);
            for(int i=0;i<20&&second.Notifications==0&&BlessedHammers.State(f.Id)!=null;i++){Through(f.Id,1);}
            var active=BlessedHammers.State(f.Id);Require(sh-SkillHealth.Balance256(second)==7098&&second.Notifications==1&&active.Motion.Seed==7129137990492200882UL
                &&active.LastCollision==second.Serial.Value,"Curved Hammer failed second seeded hit/piercing");
        }
        finally{Clean(p,t,second);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"miss","wall","offline","internal","facet","death","respec","class","slow","unsupported"})
        {
            var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());Item wall=null;
            try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                var f=Create(p);if(mode=="unsupported"){t.IsParagon=true;}int life=SkillHealth.Balance256(t);
                if(mode=="miss"){t.MoveToWorld(new(p.X+15,p.Y+15,p.Z),p.Map);}
                if(mode=="wall"){t.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);wall=new Static(0x80);wall.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);}
                if(mode=="internal"){p.Internalize();}
                if(mode=="facet"){p.MoveToWorld(p.Location,p.Map==Map.Trammel?Map.Felucca:Map.Trammel);}
                if(mode=="death"){p.Kill();Require(BlessedHammers.State(f.Id)==null,"Death retained source Hammer");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(BlessedHammers.State(f.Id)==null,"Respec retained source Hammer");}
                if(mode=="class"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out var packed),"Hammer wardrobe fixture: packed="+packed);Require(BlessedHammers.State(f.Id)==null,"Wardrobe retained source Hammer");}
                if(mode=="slow"){Require(f.Motion.Path.Velocity==3456&&f.Captured.Slow==new SlowMissilePayload(100,50)&&f.Min256==1966&&f.Max256==2621,"Hammer CanSlow0factory");AmazonSlowMissiles.Clear(p);}
                if(mode=="unsupported"){Through(f.Id,1);Require(BlessedHammers.State(f.Id) is{Motion.Fault:not null} bad&&bad.Motion.Seed==f.Motion.Seed,"Unsupported Hammer did not suspend before RNG");}
                Through(f.Id,125,mode!="offline");Require(BlessedHammers.State(f.Id)==null&&life-SkillHealth.Balance256(t)==(mode=="slow"?3846:0),"Hammer control "+mode+" loss="+(life-SkillHealth.Balance256(t)));
            }
            finally{wall?.Delete();Clean(p,t);}
        }
        foreach(int frame in new[]{118,119})
        {
            var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
            try{var f=Create(p);BlessedHammers.Update(f with{Frame=frame});int life=SkillHealth.Balance256(t);Through(f.Id,1);
                Require(life-SkillHealth.Balance256(t)==(frame==118?6795:0)&&(BlessedHammers.State(f.Id)==null)==(frame==119),"Hammer final collidable119/terminal120 boundary");}
            finally{Clean(p,t);}
        }
    }
    private static void Atomic()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
        try
        {
            var f=Create(p);BlessedHammers.Update(f with{LastCollision=t.Serial.Value});BlessedHammerFlights.RestoreReferences();Require(BlessedHammers.State(f.Id).LastCollision==t.Serial.Value,"Live Hammer collider cleared");
            t.Delete();BlessedHammerFlights.RestoreReferences();Require(BlessedHammers.State(f.Id).LastCollision==0,"Missing Hammer collider kept reusable serial");
            var all=BlessedHammers.Snapshot();f=all.Single(x=>x.Id==f.Id);bool refused=false;
            try{BlessedHammers.Restore(all.Append(f with{Id=f.Id+1000000,Motion=f.Motion with{Path=f.Motion.Path with{SavedSteps=[new(-1,-1)],StepsCount=1}}}).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&BlessedHammers.Snapshot().SequenceEqual(all),"Bad source tail partially replaced Hammer pool");refused=false;
            try{BlessedHammers.Restore(all.Append(all[0]).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&BlessedHammers.Snapshot().SequenceEqual(all),"Duplicate source Hammer partially replaced pool");
            var legacy=f with{Motion=null};BlessedHammers.Restore(all.Select(x=>x.Id==f.Id?legacy:x).ToArray());Require(JsonSerializer.Serialize(legacy).IndexOf("Motion",StringComparison.Ordinal)<0,"Legacy Hammer gained guessed motion");
        }
        finally{Clean(p,t);}
    }
    private static void Save()
    {
        var p=Owner();NecromancerEffects.SetMana256(p,1297);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-7*256-17);
        Require(PaladinCombat.Release(p,CombatAbility.BlessedHammer,Serial.Zero,p.Location),"Paid moving Hammer release");var f=BlessedHammers.Snapshot().Single(f=>f.Owner==p.Serial.Value);
        Through(f.Id,2);f=BlessedHammers.State(f.Id) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention),UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention).AddSeconds(5)};BlessedHammers.Update(f);
        Require(f.Frame==2&&f.Point==1&&f.YFp16==65536&&NecromancerEffects.ManaBalance256(p)==17,"Original paid moving Hammer save");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,p.Map.MapID,p.Location,17,SkillHealth.Balance256(p),f,0)));p.Internalize();
    }
    private static Point3D ReceiverPoint(HammerFlight f)
    {
        var s=f.Motion.Path;
        for(int i=0;i<20;i++)
        {
            s=SourceActorMotion.Advance(s,(_,_)=>0).State;
            if(i<3){continue;}
            foreach(var cell in s.SavedSteps){if((cell.X&1)==0||(cell.Y&1)==0){return new((cell.X+1)/2,(cell.Y+1)/2,f.Z);}}
        }
        throw new InvalidOperationException("No future source Hammer footprint for reload receiver");
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var f=BlessedHammers.State(s.Flight.Id);
        var expected=s.DeletedCollider==0?s.Flight:s.Flight with{LastCollision=0};
        Require(s.Generation<2&&f==expected&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,"Original saved Hammer curve/seed/resources/collider normalization changed");
        var account=FixtureNetwork.Authenticate(network,p,"hammer-source-reload-");p.MoveToWorld(s.Origin,Map.Maps[s.Map]);var t=Target(p,new ConversionContactChecks.Recipient());t.MoveToWorld(ReceiverPoint(f),p.Map);int life=SkillHealth.Balance256(t);
        BlessedHammers.Update(f with{NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(5)});long deadline=Core.TickCount+6000;HammerFlight last=f;
        void Observe()
        {
            try
            {
                if(BlessedHammers.State(f.Id) is{} seen){last=seen;}
                if(NativeMonsterReturn.Session(t).Packets==0){Require(Core.TickCount<deadline,$"Natural Hammer resume timeout:generation={s.Generation},exists={BlessedHammers.State(f.Id)!=null},frame={last.Frame},point={last.Point},xy={last.XFp16}/{last.YFp16},seed={last.Motion.Seed},collider={last.LastCollision},target={t.Serial.Value}/{t.Location},fault={last.Motion.Fault},connected={p.NetState?.Running}");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(life-SkillHealth.Balance256(t)==(s.Generation==0?6795:7098)&&t.Notifications==1&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,
                    $"Natural original Hammer resume:generation={s.Generation},loss={life-SkillHealth.Balance256(t)},notifications={t.Notifications},mana={NecromancerEffects.ManaBalance256(p)}/{s.Mana},life={SkillHealth.Balance256(p)}/{s.Life},seed={last.Motion.Seed}");
                var active=BlessedHammers.State(f.Id);Require(active.Frame>f.Frame&&active.Motion.Seed==(s.Generation==0?1193071124610UL:7129137990492200882UL)&&active.LastCollision==t.Serial.Value,"Natural Hammer payload did not advance");
                active=active with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention),UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention).AddSeconds(5)};BlessedHammers.Update(active);
                p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();t.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Flight=active,Generation=s.Generation+1,DeletedCollider=t.Serial.Value}));Log.Information("blessed-hammer-flight-reload-result: passed");
            }
            catch(Exception e){t.Delete();p.Internalize();Log.Error(e,"blessed-hammer-flight-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());var account=FixtureNetwork.Authenticate(network,p,"hammer-source-input-");
        var seed=BlessedHammerFlights.SeedLow;var roll=BlessedHammers.Roll;BlessedHammerFlights.SeedLow=()=>0;BlessedHammers.Roll=_=>throw new InvalidOperationException("Natural Hammer used global RNG");NecromancerEffects.SetMana256(p,1297);
        int life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+6000;
        void Cleanup(){BlessedHammerFlights.SeedLow=seed;BlessedHammers.Roll=roll;Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Source Hammer input timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.BlessedHammer&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==1297&&SkillHealth.Balance256(t)==life,"Hammer source input paid/hit before event");began=true;Log.Information("{Label}: windup",label);}
                if(NativeMonsterReturn.Session(t).Packets==0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                var f=BlessedHammers.Snapshot().Single(f=>f.Owner==p.Serial.Value);
                Require(began&&life-SkillHealth.Balance256(t)==6795&&t.Notifications==1&&f.Motion.Seed==1193071124610UL&&f.LastCollision==t.Serial.Value&&f.Frame>=1
                    &&NecromancerEffects.ManaBalance256(p)==17,"Actual source Hammer cells/RNG/once payment failed");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.BlessedHammer,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
