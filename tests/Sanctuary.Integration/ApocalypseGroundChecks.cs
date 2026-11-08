// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ApocalypseGroundChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ApocalypseGroundChecks));
    private sealed record Saved(uint Owner,uint Target,ApocalypseGroundField Field,ulong Seed,int Life,Point3D Point,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"apocalypse-ground-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("apocalypse-ground-check",Check);Register("apocalypse-ground-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original Apocalypse ground marker and source do1/hit23/explosion composition",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static ApocalypseGroundField Field(uint id)=>ApocalypseGround.Snapshot().Fields.Single(s=>s.Marker==id);
    private static ApocalypseGroundField Create(PlayerMobile owner,BaseCreature target,DateTime at)
    {
        var stream=ApocalypseGround.Seed(owner);uint sample=SourceRandom.Roll(ref stream);
        var field=ApocalypseGround.CreatePrepared(owner,target.Location,1,at);
        Require(field!=null&&field.TargetMemory==sample&&ApocalypseGround.Seed(owner)==stream&&World.FindItem((Serial)field.Marker)is{Deleted:false},"Ground launch allocation/target-memory/caster draw");return field;
    }
    private static void Impact(PlayerMobile owner,BaseCreature target,ApocalypseGroundField field)
    {
        int before=SkillHealth.Balance256(target);ulong stream=ApocalypseGround.Seed(owner);
        // One extra hard Ring point,retained launchrank1:104–130 fire.
        int fire=104*256+(int)SourceRandom.Limited(ref stream,26*256);int expected=(int)SourcePercentage.Damage(fire,105);
        ApocalypseGround.Tick(field.BornUtc.AddMilliseconds(2000),true,owner.Serial.Value);
        Require(!ApocalypseGround.Snapshot().Fields.Any(s=>s.Marker==field.Marker)&&World.FindItem((Serial)field.Marker)is not{Deleted:false}
            &&ApocalypseGround.Seed(owner)==stream&&before-SkillHealth.Balance256(target)==expected,"Frame50 hit23/retained rank/live hard synergy/single caster roll/actual HP");
        before=SkillHealth.Balance256(target);ApocalypseGround.Tick(field.BornUtc.AddMilliseconds(2040),true,owner.Serial.Value);
        Require(SkillHealth.Balance256(target)==before&&ApocalypseGround.Seed(owner)==stream,"Terminal ground field replayed damage or RNG");
    }
    private static void Save(PlayerMobile owner,BaseCreature target,int generation)
    {
        var field=Create(owner,target,TimedEffects.Now.AddHours(1).AddMinutes(generation));int life=SkillHealth.Balance256(target);
        ApocalypseGround.Tick(field.BornUtc.AddMilliseconds(1960),true,owner.Serial.Value);field=Field(field.Marker);
        Require(field.Remaining==1&&SkillHealth.Balance256(target)==life,"No-collision ground field hit before frame50");
        var original=ApocalypseGround.Snapshot();bool rejected=false;
        try{ApocalypseGround.Restore(original with{Fields=original.Fields.Select(s=>s.Marker==field.Marker?s with{Remaining=0}:s).ToArray()});}catch(InvalidDataException){rejected=true;}
        Require(rejected&&JsonSerializer.Serialize(ApocalypseGround.Snapshot())==JsonSerializer.Serialize(original),"Invalid ground save replaced live state");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,field,ApocalypseGround.Seed(owner),life,owner.Location,owner.Map.MapID,generation)));
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6400,35,3100);var target=FireBoltChecks.Target(owner,8);ApocalypseGround.BindSeedPrepared(owner,SourceRandom.InitLow(42));
        ulong before=ApocalypseGround.Seed(owner);
        Require(ApocalypseGround.CreatePrepared(owner,new(0,owner.Y,owner.Z),1,TimedEffects.Now)==null&&ApocalypseGround.Seed(owner)==before,"Invalid coordinates consumed a launch draw");
        SourceRandom.Roll(ref before);Require(ApocalypseGround.CreatePrepared(owner,target.Location,1,TimedEffects.Now,false)==null&&ApocalypseGround.Seed(owner)==before,"Failed allocation did not consume source's preceding draw");
        var field=Create(owner,target,TimedEffects.Now.AddHours(1));int life=SkillHealth.Balance256(target);ApocalypseGround.Tick(field.BornUtc.AddMilliseconds(1960),true,owner.Serial.Value);
        Require(Field(field.Marker).Remaining==1&&SkillHealth.Balance256(target)==life,"Occupied target tile caused premature collision");
        ClassSkillChecks.Train(owner,"warlock.apocalypse",2);ClassSkillChecks.Train(owner,"warlock.ringoffire",2);Impact(owner,target,field);
        field=Create(owner,target,TimedEffects.Now.AddHours(1));before=ApocalypseGround.Seed(owner);life=SkillHealth.Balance256(target);
        ApocalypseGround.Tick(field.BornUtc.AddMilliseconds(2000),false,owner.Serial.Value);
        Require(!ApocalypseGround.Snapshot().Fields.Any(s=>s.Marker==field.Marker)&&ApocalypseGround.Seed(owner)==before&&SkillHealth.Balance256(target)==life,"Suppressed expiry replayed damage/RNG or kept marker");
        field=Create(owner,target,TimedEffects.Now.AddHours(1));before=ApocalypseGround.Seed(owner);var point=owner.Location;var map=owner.Map;
        owner.MoveToWorld(point,Map.Felucca);ApocalypseGround.Tick(field.BornUtc.AddMilliseconds(2000),true,owner.Serial.Value);owner.MoveToWorld(point,map);
        Require(ApocalypseGround.Seed(owner)==before&&SkillHealth.Balance256(target)==life,"Facet crossing moved/replayed ground explosion");
        Save(owner,target,0);
        Log.Information("Apocalypse ground:shared start14/do123/size1 placement;failed-allocation draw,original marker,target-memory and saved owner stream;stationary mode0 has no occupied-tile contact;frame49 no damage/frame50 hit23 once,retained launchrank/live hard synergy,offline/facet suppression and atomic restore. Prepared controller only;initial RNG provider/payment/input/lifecycle certification remain open.");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(BaseCreature)World.FindMobile((Serial)s.Target);owner.MoveToWorld(s.Point,Map.Maps[s.Map]);
        Require(s.Generation<2&&Field(s.Field.Marker)==s.Field&&ApocalypseGround.Seed(owner)==s.Seed&&SkillHealth.Balance256(target)==s.Life,"Original marker/rank/deadline/remaining/stream/wounds changed on reload");
        Impact(owner,target,s.Field);if(s.Generation<1){Save(owner,target,s.Generation+1);}else{File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=2}));}
    }
}
