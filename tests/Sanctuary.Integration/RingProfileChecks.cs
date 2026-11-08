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
    private sealed record Saved(uint Owner,RingOfFireMotionState Motion,int Generation,uint Target=0,int Life256=0,RingOfFirePacket Packet=null);
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
    private static void Damage(PlayerMobile owner,BaseCreature target,RingOfFirePacket packet)
    {
        RingOfFireDamage.Validate(packet);
        Require(packet.Minimum256==6*256&&packet.Maximum256==10*256,"Pinned rank-one Ring endpoints");
        Require(RingOfFireDamage.AtAge(packet,0)==packet.Fire256,"Ring creation age must not grow damage");
        foreach(int age in new[]{1,11})
        {
            int scaled=(int)(packet.Fire256*(float)((100+5*age)/100.0));
            Require(RingOfFireDamage.AtAge(packet,age)==scaled,"Approved elapsed age growth");
            target.SetResistance(ResistanceType.Fire,25);int before=SkillHealth.Balance256(target);
            int expected=NamedElements.Resolve256(owner,target,scaled,"fire",enhance:false,pierceOverride:0).Damage256;
            RingOfFireDamage.DeliverPrepared(owner,target,packet,age,TimedEffects.Now);
            Require(SkillHealth.Balance256(target)==before-expected&&target.FireResistance==25,"Age growth must precede native mitigation exactly once");
        }
        target.SetResistance(ResistanceType.Fire,100);int life=SkillHealth.Balance256(target);
        RingOfFireDamage.DeliverPrepared(owner,target,packet,11,TimedEffects.Now);
        Require(SkillHealth.Balance256(target)==life,"Ring fire immunity without added potion elements");
        target.SetResistance(ResistanceType.Fire,0);
        bool rejected=false;try{RingOfFireDamage.AtAge(packet with{Fire256=packet.Fire256+1},1);}catch(InvalidDataException){rejected=true;}
        Require(rejected,"Modified saved Ring roll accepted");
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Warlock);Source(owner);
        var ring=RingOfFireMotion.Prepare(owner,Values(9),173);Require(ring.Motions.Length==16,"Below10 threshold");Motion(ring.Motions[0]);
        ClassSkillChecks.Train(owner,"warlock.ringoffire",9);owner.RawStr=owner.RawDex=1000;
        var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(s=>s.SourceKey=="Magefist").Id,99,new Random(173),true);owner.AddToBackpack(gloves);Require(owner.EquipItem(gloves),"Ring +skills fixture");ClassGear.Refresh(owner);
        Require(SkillMechanics.Rank(owner,CombatAbility.RingOfFire)==10&&RingOfFireMotion.Prepare(owner,SkillMechanics.Values(owner,CombatAbility.RingOfFire),173).Motions.Length==32,"Effective +skills count threshold");gloves.Delete();ClassGear.Refresh(owner);
        Require(Values(1).Mana256==2048&&Values(2).Mana256==2176&&!WarlockCombat.Implemented(CombatAbility.RingOfFire),"Half-mana profile or unfinished admission");
        ulong seed=SourceRandom.InitLow(173);var packet=RingOfFireDamage.CapturePrepared(owner,Values(1),new(0,0,0,0,0),ref seed);
        FixtureSpace.Corridor(owner,6400,30,2800);var target=FireBoltChecks.Target(owner,6);Damage(owner,target,packet);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ring.Motions[0],0,target.Serial.Value,SkillHealth.Balance256(target),packet)));
        Log.Information("Ring primary plan:8 current do154 binding cases,36 current supplied-frame scale cases;real +skills16→32threshold;shared velocity4800 and eleven-step axis projection. Approved elapsed age0/1/11 feeds one prepared ordinary-NPC HP hit before mitigation;immunity and saved packet provenance checked. Current collision/damage dispatcher/world factory and learned route remain open.");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);Source(owner);Require(saved.Generation<2,"Extra Ring profile reload");Motion(saved.Motion);
        var target=(BaseCreature)World.FindMobile((Serial)saved.Target);owner.MoveToWorld(new(target.X-6,target.Y,target.Z),target.Map);
        Require(SkillHealth.Balance256(target)==saved.Life256,"Prepared Ring recipient wounds changed over reload");Damage(owner,target,saved.Packet);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,Life256=SkillHealth.Balance256(target)}));
    }
}
