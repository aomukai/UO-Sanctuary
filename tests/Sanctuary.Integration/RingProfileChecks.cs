// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class RingProfileChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RingProfileChecks));
    private sealed record Saved(uint Owner,RingOfFireMotionState Motion,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ring-profile-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("ring-profile-check",Check);Register("ring-profile-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Ring primary binding/free flight/supplied-frame scaling",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static SkillValues Values(int rank)=>new(SkillMechanics.For(CombatAbility.RingOfFire),k=>k=="Ring of Fire"?rank:0,_=>0,99);
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Source(PlayerMobile owner)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"ring-profile-cases.json")));int n=0;
        owner.MoveToWorld(new(90,110,0),Map.Trammel);
        foreach(var row in doc.RootElement.GetProperty("launch").EnumerateArray())
        {
            var c=Ints(row.GetProperty("input"));var attempts=new List<int[]>();
            var emission=RingOfFireMotion.Prepare(owner,Values(c[10]),c[14],r=>
            {attempts.Add([r.Flags,1,r.Missile,r.X,r.Y,r.TargetX,r.TargetY,r.Velocity,r.Skill,r.Rank,r.Context12c]);return c[16]==0;});
            var expected=row.GetProperty("output");var requests=expected.GetProperty("requests").EnumerateArray().Select(Ints).ToArray();
            Require(emission.Result==expected.GetProperty("result").GetInt32()&&attempts.Count==requests.Length&&attempts.Where((r,i)=>!r.SequenceEqual(requests[i])).Any()==false,"Current do154 primary count/velocity/formula request binding");
            Require(emission.Motions.Length==(c[16]==0?requests.Length:0)&&emission.Motions.All(s=>s.VelocityFp8==4800&&s.Frame==0),"Declared allocation result or shared initial velocity");n++;
        }
        Require(n==8,"Missing primary launch cases");n=0;
        foreach(var row in doc.RootElement.GetProperty("scale").EnumerateArray())
        {
            var packet=Ints(row.GetProperty("output").GetProperty("before"));RingOfFireMotion.ScaleAtSuppliedFrame(Values(1),row.GetProperty("frame").GetInt32(),packet);
            Require(packet.SequenceEqual(Ints(row.GetProperty("output").GetProperty("after"))),"Current packet float scaling at supplied frame");n++;
        }
        Require(n==36,"Missing supplied-frame scale cases");
        foreach(int frame in new[]{-1,12}){bool rejected=false;try{RingOfFireMotion.ScaleAtSuppliedFrame(Values(1),frame,new int[24]);}catch(ArgumentOutOfRangeException){rejected=true;}Require(rejected,"Unqualified frame accepted");}
    }
    private static void Motion(RingOfFireMotionState first)
    {
        var s=first;double origin=s.X;
        for(int frame=1;frame<=11;frame++)
        {
            s=RingOfFireMotion.Step(s);int velocity=frame<5?4800:frame<10?3800:2800;
            int sum=Math.Min(frame,4)*4800+Math.Min(Math.Max(frame-4,0),5)*3800+Math.Max(frame-9,0)*2800;
            Require(s.Frame==frame&&s.VelocityFp8==velocity&&s.AccelerationCounter==frame%5&&s.X==origin+sum/8192.0&&s.Y==first.Y,"Shared axis acceleration/counter/position at "+frame);
        }
        Require(RingOfFireMotion.Step(s)==s,"Expired prepared motion advanced");
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Warlock);Source(owner);
        var ring=RingOfFireMotion.Prepare(owner,Values(9),173);Require(ring.Motions.Length==16,"Below10 threshold");Motion(ring.Motions[0]);
        ClassSkillChecks.Train(owner,"warlock.ringoffire",9);owner.RawStr=owner.RawDex=1000;
        var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(s=>s.SourceKey=="Magefist").Id,99,new Random(173),true);owner.AddToBackpack(gloves);Require(owner.EquipItem(gloves),"Ring +skills fixture");ClassGear.Refresh(owner);
        Require(SkillMechanics.Rank(owner,CombatAbility.RingOfFire)==10&&RingOfFireMotion.Prepare(owner,SkillMechanics.Values(owner,CombatAbility.RingOfFire),173).Motions.Length==32,"Effective +skills count threshold");gloves.Delete();ClassGear.Refresh(owner);
        Require(Values(1).Mana256==2048&&Values(2).Mana256==2176&&!WarlockCombat.Implemented(CombatAbility.RingOfFire),"Half-mana profile or unfinished admission");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ring.Motions[0],0)));
        Log.Information("Ring primary plan:8 current do154 binding cases,36 current supplied-frame scale cases;real +skills16→32threshold;shared velocity4800 and eleven-step axis projection,typed path/JSON continuation. Live fnum,collision/damage dispatcher/world factory and learned route remain open;no fake damage path.");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);Source(owner);Require(saved.Generation<2,"Extra Ring profile reload");Motion(saved.Motion);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
