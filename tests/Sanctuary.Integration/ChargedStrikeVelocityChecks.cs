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

public static class ChargedStrikeVelocityChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ChargedStrikeVelocityChecks));
    private sealed record Saved(uint Owner,uint Weapon,int Life256,int Mana256,Point3D Point,int Map,ChargedStrikeFlight[] Flights,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"charged-strike-velocity-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("charged-strike-velocity-check",Check);Register("charged-strike-velocity-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Charged Strike factory velocity,slows and old/new flight reload",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    // Supplied receiver state isolates the consumer from the still-gated
    // learned Slow Missiles targeting/casting and modern damage-order work.
    private static void Slow(PlayerMobile owner,int percent,int milliseconds=1000)
    {
        var other=AmazonSlowMissiles.Snapshot().Where(s=>s.Target!=owner.Serial.Value);
        AmazonSlowMissiles.Restore(other.Append(new(owner.Serial.Value,owner.Serial.Value,1,percent,100,TimedEffects.Now.AddMilliseconds(milliseconds))).ToArray());
    }
    private static ChargedStrikeFlight[] Flights(PlayerMobile owner)=>ChargedStrikeBolts.Snapshot().Where(f=>f.Owner==owner.Serial.Value).OrderBy(f=>f.Id).ToArray();
    private static void Replace(PlayerMobile owner,ChargedStrikeFlight[] rows)
        =>ChargedStrikeBolts.Restore(ChargedStrikeBolts.Snapshot().Where(f=>f.Owner!=owner.Serial.Value).Concat(rows).ToArray());
    private static void Source()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"charged-strike-velocity-source.json")));int count=0;
        foreach(var item in source.RootElement.EnumerateArray())
        {
            var row=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            int actual=SlowMissilePayload.VelocityFp8(12,0,row[0],33,0,row[1]!=0,row[2]!=0?row[3]:100);
            Require(actual==row[5]&&row[4]==1&&row[6]==77&&row[7]==77&&row[8]==77,"Complete factory/initializer speed or77frame cap differs");count++;
        }
        Require(count==72,"Full factory/initializer coverage missing");
    }
    private static ChargedStrikeFlight Move(ChargedStrikeFlight before,double expected)
    {
        ChargedStrikeBolts.Tick(before.NextUtc,before.Id);
        var after=ChargedStrikeBolts.Snapshot().Single(f=>f.Id==before.Id);
        double dx=after.X-before.X,dy=after.Y-before.Y;
        Require(after.Frame==before.Frame+1&&Math.Abs(Math.Sqrt(dx*dx+dy*dy)-expected)<1e-8,"Actual charged path ignored captured factory speed");
        return after;
    }
    private static void Check()
    {
        Source();var owner=ChargedStrikeChecks.Fresh();ChargedStrikeChecks.Train(owner,"amazon.chargedstrike");
        var weapon=ChargedStrikeChecks.Equip(owner,new Spear());var target=ChargedStrikeChecks.Enemy(owner);
        int mana=NecromancerEffects.ManaBalance256(owner),durability=weapon.HitPoints;
        var roll=AmazonMelee.HitRoll;
        try
        {
            AmazonMelee.HitRoll=()=>1; // Known miss: emitted spell bolts still use do11.
            Require(ClassCombat.Perform(owner,CombatAbility.ChargedStrike,target,target.Location,Core.TickCount),"Learned Charged Strike windup refused");
            ClassCombat.ReleaseCast(owner);var session=ClassCombat.For(owner);
            Slow(owner,50); // State acquired after start; factory reads at release.
            Require(ClassCombat.ResolveAction(owner,session.EffectAt),"Learned Charged Strike release failed");
        }
        finally{AmazonMelee.HitRoll=roll;}
        var half=Flights(owner);
        Require(half.Length==3&&half.All(f=>f.FactoryMotion==new ChargedStrikeFactoryMotion(1152,50)),"Learned do11 did not capture released Slow Missiles");
        Require(NecromancerEffects.ManaBalance256(owner)==mana-SkillMechanics.Values(owner,CombatAbility.ChargedStrike).Mana256&&weapon.HitPoints==durability,"Slow consumer changed once-only payment or miss wear");
        var origin=target.Location;target.Internalize();
        AmazonSlowMissiles.Clear(owner);Move(half[0],1152/8192.0);
        Require(Flights(owner).Single(f=>f.Id==half[0].Id).FactoryMotion==half[0].FactoryMotion,"Clearing slow rewrote an issued bolt");
        var values=SkillMechanics.Values(owner,CombatAbility.ChargedStrike);
        ChargedStrikeBolts.Cast(owner,origin,values);var normal=Flights(owner).Where(f=>f.Id>half.Max(x=>x.Id)).ToArray();
        Require(normal.Length==3&&normal.All(f=>f.FactoryMotion==new ChargedStrikeFactoryMotion(2304,100)),"Unslowed factory speed changed");
        Move(normal[0],2304/8192.0);
        Slow(owner,0);ChargedStrikeBolts.Cast(owner,origin,values);var zero=Flights(owner).Where(f=>f.Id>normal.Max(x=>x.Id)).ToArray();
        Require(zero.Length==3&&zero.All(f=>f.FactoryMotion==new ChargedStrikeFactoryMotion(0,0)),"Zero-speed factory control changed");Move(zero[0],0);
        ChargedStrikeBolts.Tick(zero[0].UntilUtc,zero[0].Id);Require(!Flights(owner).Any(f=>f.Id==zero[0].Id),"Zero-speed bolt gained an unlimited lifetime");
        var legacy=normal[1]with{FactoryMotion=null};Replace(owner,Flights(owner).Select(f=>f.Id==legacy.Id?legacy:f).ToArray());
        Require(!JsonSerializer.Serialize(legacy).Contains("FactoryMotion"),"Legacy JSON shape changed");
        Move(legacy,2304/8192.0); // Current zero-speed owner must not rewrite unknown history.
        var snapshot=ChargedStrikeBolts.Snapshot();
        foreach(var bad in new[]{new ChargedStrikeFactoryMotion(-1,50),new(1152,101),new(2304,50)})
        {
            bool refused=false;try{ChargedStrikeBolts.Restore(snapshot.Select(f=>f.Id==half[0].Id?f with{FactoryMotion=bad}:f).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&ChargedStrikeBolts.Snapshot().SequenceEqual(snapshot),"Invalid factory metadata partially replaced active flights");
        }
        var kept=Flights(owner).Where(f=>f.Id==half[0].Id||f.Id==legacy.Id).Select(f=>f with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}).ToArray();
        Replace(owner,kept);Slow(owner,50,100); // Ordinary state expires; only inspection flights are held.
        SkillHealth.SetBalance256(owner,SkillHealth.Maximum256(owner)/2+17);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,SkillHealth.Balance256(owner),NecromancerEffects.ManaBalance256(owner),owner.Location,owner.Map.MapID,kept)));
        owner.Internalize();target.Delete();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(SkillHealth.Balance256(owner)==saved.Life256&&NecromancerEffects.ManaBalance256(owner)==saved.Mana256
            &&CombatTiming.Equipped(owner)?.Serial.Value==saved.Weapon&&Flights(owner).SequenceEqual(saved.Flights),"Original wounds,mana,weapon or mixed old/new flights changed on reload");
        Require(AmazonSlowMissiles.Capture(owner,true)==SlowMissilePayload.Normal,"Expired caster state was revived to match the held flight");
        owner.MoveToWorld(saved.Point,Map.Maps[saved.Map]);
        var current=Flights(owner).Single(f=>f.FactoryMotion!=null);var legacy=Flights(owner).Single(f=>f.FactoryMotion==null);
        Move(current,1152/8192.0);Move(legacy,2304/8192.0);owner.Internalize();
        Require(SkillHealth.Balance256(owner)==saved.Life256&&NecromancerEffects.ManaBalance256(owner)==saved.Mana256,"Mixed flight continuation changed original resources");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flights=Flights(owner),Generation=saved.Generation+1}));
    }
}
