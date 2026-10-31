// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class BoneBarrierClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BoneBarrierClockChecks));
    private sealed record Saved(uint Body,uint Owner,string Type,DateTime Until,int Life,BarrierThinkState Clock);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bone-barrier-clock-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("bone-barrier-clock-check",Check);Register("bone-barrier-clock-reload-check",Reload);
        Register("bone-barrier-clock-save",()=>{StageSave();BoneWallMakerChecks.StageSave();BoneBarrierBirthChecks.StageSave();});
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Barrier source first-think/deadline/idle clock",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bone-barrier-retirement-source-cases.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();var s=BoneBarrierThinkClock.Capture(new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc).AddMilliseconds(V(1)*40L));
            Require(s.Deadline+V(1)==V(3)&&s.Next+V(1)==V(4),"Source initial AI event/deadline "+count);
            s=BoneBarrierThinkClock.Observe(s,s.Next+V(2));while(!s.DeathRequested){s=BoneBarrierThinkClock.Observe(s,s.Next);}
            Require(s.Last+V(1)==V(5)&&s.Thinks==V(6)&&s.Thinks==V(8)&&s.Thinks==V(9),"Composed source event phase "+count);count++;
        }
        Require(count==128,"Incomplete source schedule contexts");
        var boundary=BoneBarrierThinkClock.Capture(TimedEffects.Now);boundary=BoneBarrierThinkClock.Observe(boundary,600);
        Require(!boundary.DeathRequested&&boundary.Next==615&&BoneBarrierThinkClock.Observe(boundary,614)==boundary
            &&BoneBarrierThinkClock.Observe(boundary,615).DeathRequested,"Strict deadline equality or next15frame idle");
    }
    private static PlayerMobile Owner()
    {var p=ClassSkillChecks.Player();p.Name="BoneBarrierClockFixture";p.AddItem(new Backpack());FixtureSpace.Corridor(p,7000,24,3500);return p;}
    private static BoneBarrierBody Spawn(PlayerMobile p,CombatAbility ability,Point3D point,BoneBarrierSegment root=null)
        =>(BoneBarrierBody)BoneBarrierSegment.Spawn(p,ability,point,root==null?SkillMechanics.Values(p,ability):null,root);
    private static void Field(BoneBarrierSegment b,string name,object value)
        =>typeof(BoneBarrierSegment).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(b,value);
    private static BoneBarrierSegment Legacy(PlayerMobile p,bool prepared,Point3D point)
    {
        BoneBarrierSegment b=prepared?new BoneBarrierBody():new BoneBarrierSegment();b.SummonMaster=p;b.SetHits(19);b.Hits=18;
        Field(b,"_ability",CombatAbility.BoneWall);Field(b,"_life",19);Field(b,"_expiresUtc",TimedEffects.Now.AddSeconds(24));
        typeof(BaseCreature).GetField("_summonEnd",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(b,b.ExpiresUtc);
        b.MoveToWorld(point,p.Map);b.AddTile(point);
        // Exercise the historical saved type's native load/start path. This
        // fixture is not evidence of an actual old-build byte upgrade.
        typeof(BoneBarrierSegment).GetMethod("AfterLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b,null);
        var expiry=typeof(BoneBarrierSegment).GetMethod("LifetimeExpired",BindingFlags.Instance|BindingFlags.NonPublic);
        Require((bool)expiry.Invoke(b,new object[]{b.ExpiresUtc})&&!(bool)expiry.Invoke(b,new object[]{b.ExpiresUtc.AddTicks(-1)}),"Legacy nominal expiry gate changed");
        return b;
    }
    private static void Check()
    {
        Source();var p=Owner();var root=(BoneWallThinkingBody)Spawn(p,CombatAbility.BoneWall,new(p.X+6,p.Y,p.Z));
        var originals=new List<BoneBarrierSegment>{root};
        try
        {
            var s=((IBarrierThinkClock)root).Clock;Require(!root.UsesNativeSummonTimer&&s.Next==25&&s.Thinks==0&&root.ExpiresUtc==s.BornUtc.AddSeconds(24),"New root clock capture/timer ownership");
            originals.Add(Legacy(p,false,new(p.X+12,p.Y,p.Z)));originals.Add(Legacy(p,true,new(p.X+14,p.Y,p.Z)));
            Timer.StartTimer(TimeSpan.FromMilliseconds(120),()=>Core.LoopContext.Post(()=>
            {
                try
                {
                    var child=Spawn(p,CombatAbility.BoneWall,new(root.X+1,root.Y,root.Z),root);originals.Add(child);
                    var own=((IBarrierThinkClock)child).Clock;
                    Require(!child.UsesNativeSummonTimer&&own.BornUtc>s.BornUtc&&child.ExpiresUtc==own.BornUtc.AddSeconds(24)&&child.ExpiresUtc>root.ExpiresUtc
                        &&child.Prepared==root.Prepared&&root.MadeChildren.Contains(child),"Later child inherited root birth deadline or lost recipe/link");
                    Log.Information("bone-barrier-clock-result: passed");
                }
                catch(Exception e){Log.Error(e,"bone-barrier-clock-result: failed");}
                finally{foreach(var b in originals){b.Delete();}p.Delete();}
            }));
        }
        catch{foreach(var b in originals){b.Delete();}p.Delete();throw;}
    }
    private static void StageSave()
    {
        var p=Owner();var root=Spawn(p,CombatAbility.BoneWall,new(p.X+6,p.Y,p.Z));
        var child=Spawn(p,CombatAbility.BoneWall,new(p.X+7,p.Y,p.Z),root);
        var prison=Spawn(p,CombatAbility.BonePrison,new(p.X+10,p.Y,p.Z));
        var bodies=new[]{root,child,prison,Legacy(p,false,new(p.X+14,p.Y,p.Z)),Legacy(p,true,new(p.X+16,p.Y,p.Z))};
        foreach(var b in bodies)
        {
            if(b is IBarrierThinkClock clock)
            {
                // Inspect through frame595 without moving BornUtc or its
                // nominal deadline. This keeps the saved state quiet during
                // boot; separate real-input checks use natural observations.
                var s=clock.Clock;for(int f=25;f<=595;f+=15){s=BoneBarrierThinkClock.Observe(s,f);}clock.StoreClock(s);
                Require(s.Last==595&&s.Next==610&&s.Thinks==39,"Inspected partial clock fixture");
            }
            SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(bodies.Select(b=>new Saved(b.Serial.Value,p.Serial.Value,b.GetType().Name,b.ExpiresUtc,SkillHealth.Balance256(b),(b as IBarrierThinkClock)?.Clock)).ToArray()));
    }
    private static void Reload()
    {
        var records=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));Require(records.Length==5,"Missing live/legacy clock cohort");
        var resume=new List<(IBarrierThinkClock Clock,BarrierThinkState State)>();
        foreach(var r in records)
        {
            var b=World.FindMobile((Serial)r.Body) as BoneBarrierSegment;
            Require(b is{Deleted:false,Alive:true}&&b.GetType().Name==r.Type&&b.SummonMaster?.Serial.Value==r.Owner&&b.ExpiresUtc==r.Until
                &&SkillHealth.Balance256(b)==r.Life&&b.Tiles.All(t=>!t.Deleted&&t.Segment==b),
                $"Saved clock mismatch id={r.Body} type={b?.GetType().Name}/{r.Type} alive={b?.Alive} owner={b?.SummonMaster?.Serial.Value}/{r.Owner} until={b?.ExpiresUtc:o}/{r.Until:o} hp={(b==null?-1:SkillHealth.Balance256(b))}/{r.Life} tiles={b?.Tiles.Count}");
            if(b is IBarrierThinkClock clock)
            {
                Require(!b.UsesNativeSummonTimer&&clock.Clock==r.Clock&&r.Clock.Last==595&&r.Clock.Next==610,"Partial AI clock reset/native timer ownership on load");
                resume.Add((clock,r.Clock));
            }
            else{Require(b.UsesNativeSummonTimer&&r.Clock==null,"Legacy timer ownership changed");}
        }
        foreach(var (clock,state) in resume){Require(BoneBarrierThinkClock.Expired(clock,state.BornUtc.AddMilliseconds(610*40)),"Saved clock did not resume its next event");}
        Timer.StartTimer(TimeSpan.FromMilliseconds(160),()=>Core.LoopContext.Post(()=>
        {
            try
            {
                foreach(var r in records)
                {
                    var b=World.FindMobile((Serial)r.Body) as BoneBarrierSegment;
                    Require(r.Clock!=null?b==null||b.Deleted:b is{Alive:true,Deleted:false}&&SkillHealth.Balance256(b)==r.Life,"Resumed AI death/removal or legacy retention failed");
                }
                Log.Information("bone-barrier-clock-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"bone-barrier-clock-reload-result: failed");}
        }));
    }
}
