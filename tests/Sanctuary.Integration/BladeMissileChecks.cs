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

public sealed class BladeMissileTarget:GraveyardZombie
{
    public BladeMissileTarget(){}
    public BladeMissileTarget(Serial serial):base(serial){}
    public override bool CanRegenHits=>false;
    public override bool CanRegenMana=>false;
    public override void OnThink(){}
    public override void OnSectorActivate()=>AIObject?.AITimer.Stop();
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
    public override void Deserialize(IGenericReader reader)
    {base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown blade target fixture version");}}
}

public static class BladeMissileChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeMissileChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,BladeBodyState BodyState,BladeMissileState Missile,int Balance);
    private sealed class Rolls(params int[] values):Random
    {public int Calls;public override int Next(int maximum)=>Math.Clamp(Calls<values.Length?values[Calls++]:maximum-1,0,maximum-1);}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-missile-fixtures.json");
    private static void Require(bool ok,string reason){if(!ok){throw new InvalidOperationException(reason);}}
    public static void Configure(){Register("blade-missile-check",Check);Register("blade-missile-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared blade live weapon snapshot/following contact/combat and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Differential()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-sentinel-hit-source-probe.json")));int count=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int teleports=0,finds=0,ar=0,fills=0,flags=0,allocations=0,delay=-1,events=0,nullEvents=0;bool justHit=N(3)!=0;
            var actual=BladeSentinelContact.Advance(new(40,70,50),N(0)!=0?new(80,95):null,N(1)!=0,N(2)!=0,true,50,
                p=>{teleports++;return N(7);},()=>N(8),()=>{finds++;return !justHit && N(4)!=0?7u:null;},id=>{ar++;return N(5)!=0;},
                (id,frames)=>{allocations++;delay=1000+frames;if(N(6)!=0){justHit=true;}},id=>{if(id==null){nullEvents++;}else{events++;}},id=>{fills++;flags++;});
            var fields=new[]{actual.Alive?1:2,teleports,actual.X,actual.Y,actual.Frame,finds,ar,fills,flags,allocations,delay,events,justHit?1:0,nullEvents};
            Require(fields.SequenceEqual(Enumerable.Range(9,14).Select(N)),"Complete source following/collision/hit37 differential "+count);count++;
        }
        Require(count==1152,"Incomplete following source matrix");
        using var leech=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"nonplayer-leech-source-probe.json")));int drains=0;
        foreach(var row in leech.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var result=WeaponLeech.Nonplayer(N(0),N(1),N(2),N(3),N(4),N(5),N(6));
            Require(result.Life256==N(7) && result.Mana256==0 && result.VictimMana256==N(8),"Nonplayer drain source arithmetic");drains++;
        }
        Require(drains==14400,"Incomplete nonplayer source matrix");
        Log.Information("Blade missile:1152 complete follow/collision/hit37 and14400 nonplayer drain source cases match");
    }
    private static void Train(PlayerMobile p,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Assassin,id,out var definition),"Missing talent "+id);foreach(var pre in definition.Prerequisites){Train(p,pre);}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Cannot train "+id);}
    }
    private static PlayerMobile Fresh()
    {
        var p=BladeBodyChecks.Fresh();while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}ClassGear.Refresh(p);return p;
    }
    private static T Normal<T>(PlayerMobile p,T item) where T:BaseWeapon
    {
        if(CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));p.AddToBackpack(item);
        Require(p.EquipItem(item),"Equip "+typeof(T).Name);ClassGear.Refresh(p);return item;
    }
    private static void Collect()
    {
        var p=Fresh();var creation=BladeBodyChecks.Creation(p);var noCrit=new Rolls(99);
        var unarmed=BladeSentinelLaunch.Capture(p,creation,noCrit);
        Require(unarmed.Damage.Percent==0 && !unarmed.Damage.Critical && noCrit.Calls==1,"Unarmed weapon Strength/mastery or generic critical draw");
        Require(unarmed.Creation.Attack-creation.Attack==5*p.Dex-35+ClassStats.For(DiabloClass.Assassin).AttackBonus && unarmed.Creation.AttackPercent==0,"Flat owner attack inherited after percent by mistake");
        var claw=Normal(p,new Tekagi());Train(p,"assassin.clawmastery",20);var rolls=new Rolls(99,0);var captured=BladeSentinelLaunch.Capture(p,creation,rolls);
        Require(captured.Damage.Critical && rolls.Calls==2 && captured.Creation.AttackPercent==220,"Missile generic then actual claw critical/attack mastery");
        var raw=ClassStats.WeaponBaseDamage(p);var skill=SkillMechanics.Values(p,CombatAbility.BladeSentinel,3);
        Require(captured.Damage.Min256==raw.Min*192+skill.PhysicalDamage256(false) && captured.Damage.Max256==raw.Max*192+skill.PhysicalDamage256(true),"One-hand source fraction/assigned rank");
        var staff=Normal(p,new QuarterStaff());var two=BladeSentinelLaunch.Capture(p,creation,new Rolls(99));raw=ClassStats.WeaponBaseDamage(p);
        Require(two.Damage.Min256==raw.Min*96+skill.PhysicalDamage256(false) && two.Damage.UndeadPercent==50 && two.Creation.AttackPercent==0,"Two-handed source fraction/blunt undead bonus/mastery exclusion");
        p.AddToBackpack(staff);Require(p.EquipItem(claw),"Restore claw");var skull=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Wormskull").Id,99,new Random(173),true);Require(p.EquipItem(skull),"Actual poison helmet");ClassGear.Refresh(p);
        var poisoned=BladeSentinelLaunch.Capture(p,creation,new Rolls(99,99));Require(poisoned.Damage.Elements is{PoisonMin:76,PoisonMax:76,PoisonFrames:200,PoisonCount:0},"Actual item poison source scaling/count0");
        string snapshot=JsonSerializer.Serialize(poisoned);p.AddToBackpack(skull);p.AddToBackpack(claw);p.RawStr+=20;ClassGear.Refresh(p);
        Require(JsonSerializer.Serialize(poisoned)==snapshot && poisoned.Damage.Elements.PoisonFrames==200,"Captured owner/weapon poison changed after gear swap");
        bool repeat=false;try{BladeSentinelLaunch.Capture(p,captured.Creation,new Rolls(99));}catch(InvalidOperationException){repeat=true;}Require(repeat,"Repeated first-missile inheritance");p.Delete();
    }
    private static BaseCreature Target(PlayerMobile p,int dx=1)
    {
        var t=new BladeMissileTarget{NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;t.SetMana(1000);t.Mana=t.ManaMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static BladeSentinelBody Body(PlayerMobile p,DateTime at,bool command)
    {
        int x=p.X*2,y=p.Y*2;var pose=new ActorMotionState((uint)((x<<16)+32768),(uint)((y<<16)+32768),[],new(0,0),0,2304,2304,0,0,new(0,0),new(0,0),20,0x800,0,[],0);
        var body=BladeSentinelBody.CreatePrepared(p,BladeBodyChecks.Creation(p),pose,p.Z);body.StartPreparedController(new(x+8,y),2304,SourceRandom.InitLow(19),at,command);return body;
    }
    private static BladeMissileState Missile(BladeSentinelBody b)=>BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==b.Serial.Value);
    private static void Frames(BladeSentinelBody b,int count)
    {
        for(int i=0;i<count;i++){DateTime at=b.Prepared.NextFrameUtc;BladeSentinelMissiles.TickFrame(at);b.AdvanceController(at);}
    }
    private static void Natural(string marker)
    {
        DateTime at=TimedEffects.Now;
        var fixtures=Enumerable.Range(0,2).Select(_=>
        {
            var p=Fresh();var target=Target(p,-1);var body=Body(p,at,true);int x=p.X*2,y=p.Y*2;p.MoveToWorld(new(p.X-2,p.Y,p.Z),p.Map);
            return (p,target,body,x,y);
        }).ToArray();
        // Two actors share the live factory, clock grid and missile-first phase.
        Timer.StartTimer(TimeSpan.FromMilliseconds(1100),()=>
        {
            try
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-clock-source-probe.json")));
                var row=doc.RootElement.EnumerateArray().Single(r=>r.GetProperty("input")[0].GetInt32()==0 && r.GetProperty("input")[1].GetInt32()==8 && r.GetProperty("input")[2].GetInt32()==3 && r.GetProperty("input")[3].GetInt32()==1);
                foreach(var (p,target,body,x,y) in fixtures)
                {
                    var state=Missile(body);int frame=body.Prepared.Control.Frame;
                    Require(body.Prepared.Creation.MissileArmed && body.Prepared.Control.Creates==1 && frame>10 && state.Position.Frame==9 && state.Fault==null,"Natural live missile creation/frame/fault");
                    Require(SkillHealth.Balance256(target)<50000*256 && state.Owner==body.Serial.Value && state.OriginalOwner==p.Serial.Value,"Natural blade-owned damage/original credit identity");
                    var before=row.GetProperty("frames")[frame-2];
                    Require(state.Position.X==(before[1].GetInt64()>>16)+x-100 && state.Position.Y==(before[2].GetInt64()>>16)+y-100,"Missile did not follow the source cell from before current monster movement");
                }
                Require(fixtures.Select(x=>x.body.Prepared.Control.Frame).Distinct().Count()==1 && fixtures.Select(x=>Missile(x.body).NextUtc).Distinct().Count()==1,"Two blades drifted off their shared source frame");
                foreach(var f in fixtures){f.body.Delete();f.target.Delete();f.p.Delete();}
                Log.Information("Blade conductor:2 simultaneous actors retain missile-before-monster source cells and one shared frame clock");Log.Information("{Marker}: passed",marker);
            }
            catch(Exception e){foreach(var f in fixtures){f.body.Delete();f.target.Delete();f.p.Delete();}Log.Error(e,"{Marker}: failed",marker);}
        });
    }
    private static void Check()
    {
        Differential();Collect();var p=Fresh();var claw=Normal(p,new Tekagi());Train(p,"assassin.clawmastery",20);var target=Target(p);var body=Body(p,TimedEffects.Now.AddHours(2),false);
        var oldRandom=BladeSentinelMissiles.LaunchRandom;var oldSeed=BladeSentinelMissiles.SeedLow;
        try{BladeSentinelMissiles.LaunchRandom=new Rolls(99,99);BladeSentinelMissiles.SeedLow=()=>19;Frames(body,20);}
        finally{BladeSentinelMissiles.LaunchRandom=oldRandom;BladeSentinelMissiles.SeedLow=oldSeed;}
        var missile=Missile(body);Require(body.Prepared.Creation.MissileArmed && missile.Owner==body.Serial.Value && missile.Fault==null && missile.Position.Frame==9,"Actual first missile/owner transfer");
        int balance=SkillHealth.Balance256(target);Require(balance<50000*256 && !BladeSentinelMissiles.Ready(missile.Id,target.Serial.Value,missile.NextUtc),"Damage delivery/25-frame delay");
        string packet=JsonSerializer.Serialize(missile.Damage);p.AddToBackpack(claw);ClassGear.Refresh(p);Require(JsonSerializer.Serialize(Missile(body).Damage)==packet,"Live gear overwrote existing missile");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,target.Serial.Value,body.Prepared,Missile(body),balance)));
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Incomplete cast/cap enabled");Natural("blade-missile-result");
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(BladeSentinelBody)World.FindMobile((Serial)saved.Body);var target=(BaseCreature)World.FindMobile((Serial)saved.Target);
        Require(JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.BodyState) && JsonSerializer.Serialize(Missile(body))==JsonSerializer.Serialize(saved.Missile) && SkillHealth.Balance256(target)==saved.Balance,"Actual saved blade/missile/phase/packet/health mismatch");
        p.MoveToWorld(body.Location,body.Map);int frame=body.Prepared.Control.Frame;
        // Reach one new contact on each reload: frame27, then52. Gear remains removed.
        int nextContact=frame<27?27:52;Frames(body,nextContact-frame);
        Require(SkillHealth.Balance256(target)<saved.Balance && body.Prepared.Control.Creates==1 && body.Prepared.Creation.MissileArmed && Missile(body).Damage==saved.Missile.Damage,"Resumed blade damage/lease or repeated factory/gear collection");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{BodyState=body.Prepared,Missile=Missile(body),Balance=SkillHealth.Balance256(target)}));Natural("blade-missile-reload-result");
    }
}
