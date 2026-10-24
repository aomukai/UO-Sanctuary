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

public static class BladeBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeBodyChecks));
    private sealed record Saved(uint Owner,uint Body,BladeBodyState State,uint OrdinaryTimer);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-body-fixtures.json");
    public static void Configure(){Register("blade-body-check",Check);Register("blade-body-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared blade world body/footprint/persistence; no learned skill or AI",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static ActorMotionState Pose(PlayerMobile p)
    {
        var target=new Point2D(p.X*2+8,p.Y*2);var state=new ActorMotionState((uint)((p.X*2<<16)+32768),(uint)((p.Y*2<<16)+32768),[target],target,0,2304,2304,0,0,new(0,0),new(0,0),20,0x820,0,[],0);
        return SourceActorMotion.DirectionToNext(state);
    }
    internal static PlayerMobile Fresh()
    {
        var p=ClassSkillChecks.Player(DiabloClass.Assassin);p.Name="BladeBodyFixture";p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);
        int floor=Enumerable.Range(0x500,0x100).First(id=>TileData.ItemTable[id].Surface && !TileData.ItemTable[id].Impassable && TileData.ItemTable[id].CalcHeight==0);
        for(int y=2600;y<3800;y+=32)
        {
            bool clear=true;
            for(int x=5996;x<=6010;x++)for(int dy=-3;dy<=3;dy++)
            {if(!Map.Trammel.CanFit(x,y+dy,0,16,true,true,false)){clear=false;}}
            if(!clear){continue;}
            for(int x=5996;x<=6010;x++)for(int dy=-3;dy<=3;dy++){new Static(floor).MoveToWorld(new(x,y+dy,0),Map.Trammel);}
            FixtureSpace.Place(p,new(6000,y,0),Map.Trammel);
            Log.Information("Blade body controlled native floor fixture: item={Floor} origin={Point}",floor,p.Location);return p;
        }
        p.Delete();throw new InvalidOperationException("No blade body flat-floor corridor");
    }
    internal static AssassinTrapCreationStats Creation(PlayerMobile p)
    {
        var row=SkillMechanics.For(CombatAbility.BladeSentinel);var values=new SkillValues(row,k=>k==row.SourceKey?3:0,_=>0,99);
        // Explicit spawn/stat service inputs, not a complete live summon factory.
        return AssassinTrapCreation.Prepare(values,99,0,true,new(25600,100,0),_=>0);
    }
    private static void Space(PlayerMobile p,BladeSentinelBody body)
    {
        var at=body.Location;p.MoveToWorld(new(at.X-1,at.Y,at.Z),body.Map);p.Stam=5;p.Direction=Direction.East;
        Require(!body.BlocksFit && body.Summoned && !body.UsesNativeSummonTimer && !body.CanBeDamaged() && !p.CanBeHarmful(body,false),"Blade source no-footprint/nonattackable/timer ownership");
        Require(body.Map.CanFit(at,16,true,true,true) && Teleportation.Space(body.Map,at,p),"Blade occupied Map.CanFit/teleport placement");
        Require(p.Move(Direction.East) && p.Location==at && p.Stam==5 && AlliedOverlap.Allowed(p),"Walking through blade shoved or blocked allied actions");
        p.MoveToWorld(new(at.X-2,at.Y,at.Z),body.Map);
        Require(BladeWorldSpace.Mask(body.Map,at.Z,at.X*2,at.Y*2,body)==0,"Zero-footprint body blocked its own source cell");
        var mob=new GraveyardSkeleton{Name="BladeBodyFixtureObstacle",NoKillAwards=true};mob.MoveToWorld(at,body.Map);mob.AIObject.AITimer.Stop();
        Require(mob.BlocksFit && mob.UsesNativeSummonTimer && !body.Map.CanFit(at,16,true,true,true) && !Teleportation.Space(body.Map,at,p)
            && BladeWorldSpace.Mask(body.Map,at.Z,at.X*2,at.Y*2,body)==0x1000,"Ordinary mobile footprint/default lifetime changed");mob.Delete();
        var wall=new Static(0x80);wall.MoveToWorld(new(at.X+1,at.Y,at.Z),body.Map);
        Require(BladeWorldSpace.Mask(body.Map,at.Z,(at.X+1)*2,at.Y*2,body)!=0,"Native world wall projection missing");wall.Delete();
        Require(BladeWorldSpace.Mask(body.Map,at.Z,p.X*2,p.Y*2,body)==0x1000,"Caster center footprint silently ignored");
    }
    private static DateTime NativeEnd(BaseCreature creature,DateTime? value=null)
    {
        var field=typeof(BaseCreature).GetField("_summonEnd",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"Native summon timer fixture field");if(value.HasValue){field.SetValue(creature,value.Value);}return (DateTime)field.GetValue(creature);
    }
    private static void Check()
    {
        var p=Fresh();var stats=Creation(p);var pose=Pose(p);var body=BladeSentinelBody.CreatePrepared(p,stats,pose,p.Z);
        Require(body.Prepared.Creation==stats && body.SummonMaster==p && body.Location==p.Location && body.Hits==100
            && ClassStats.Level(body)==99 && ClassStats.Defense(body)==stats.Defense && ClassStats.AttackRating(body)==stats.Attack,"Prepared actual actor/source stats/center bias");
        Space(p,body);var result=body.AdvancePrepared();Require(result.Moving && body.Prepared.Frames==1 && body.Prepared.Motion.X==pose.X+36864,"Actual actor first fixed-point frame");
        var fractional=body.Prepared.Motion;var cell=new Point2D((int)(fractional.X>>16),(int)(fractional.Y>>16));
        var shortPath=body.BeginPreparedWalk(new(cell.X+2,cell.Y));Require(shortPath.Motion.Points.Length==0 && (shortPath.Motion.Flags&0x20)==0,"Source short-path rejection replaced with native chase");
        var farPath=body.BeginPreparedWalk(new(cell.X+20,cell.Y));Require(farPath.Motion.Points.Length==0,"Source distance budget silently clipped");
        var target=new Point2D(cell.X+12,cell.Y);var initialized=body.BeginPreparedWalk(target);
        Require(initialized.Motion.Points.SequenceEqual(new[]{target}) && initialized.Target==target && initialized.PreviousTarget==target
            && initialized.Motion.FinalTarget==target && initialized.Motion.X==fractional.X && initialized.Motion.Y==fractional.Y
            && body.Prepared.Frames==1,"Actual body source path initialization/fractional position");
        var toward=body.BeginPreparedWalk(target,1,13);
        Require(toward.Motion.PathType==13 && toward.Motion.Distance==1 && (toward.Motion.Flags&0x900)==0x100
            && toward.Motion.Points.SequenceEqual(new[]{target}) && toward.Motion.X==fractional.X && toward.Motion.Y==fractional.Y,
            "Actual toward-finish body path inherited wall-follow distance rejection or snapped pose");
        var before=body.Prepared;bool closed=false;try{BladeWorldSpace.Mask(body.Map,body.Z,0,0,body);}catch(NotSupportedException){closed=true;}Require(closed && body.Prepared==before,"Unmapped native boundary accepted/mutated actor");
        var ordinary=new GraveyardSkeleton{Name="BladeBodyFixtureDefaultSummonTimer",NoKillAwards=true,Summoned=true};
        NativeEnd(ordinary,TimedEffects.Now.AddMilliseconds(50));ordinary.MoveToWorld(new(p.X-3,p.Y+2,p.Z),p.Map);ordinary.AIObject.AITimer.Stop();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,ordinary.Serial.Value)));
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Prepared actor enabled unfinished learned handler");
        Log.Information("Blade body: actual no-footprint movement/placement, source stats, point-path initialization and prepared fractional pose pass; no AI/cast/damage enabled");
    }
    private static bool Same(BladeBodyState a,BladeBodyState b)=>JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b);
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Body) as BladeSentinelBody;
        Require(body!=null && !body.Deleted && Same(body.Prepared,saved.State) && body.SummonMaster==p && !body.BlocksFit && body.Summoned
            && !body.UsesNativeSummonTimer && NativeEnd(body)==DateTime.MinValue,"Saved actual body/source/pose or managed native timer ownership");
        Require(World.FindMobile((Serial)saved.OrdinaryTimer)==null,"Ordinary summon did not retain native post-load unsummon timer");
        Require(ClassStats.Level(body)==saved.State.Creation.Level && ClassStats.Defense(body)==saved.State.Creation.Defense && ClassStats.AttackRating(body)==saved.State.Creation.Attack && body.Hits==100
            && body.Prepared.Motion.PathType==13 && (body.Prepared.Motion.Flags&0x900)==0x100,"Saved source stats/getters/path13");
        p.MoveToWorld(new(body.X-2,body.Y,body.Z),body.Map);var result=body.AdvancePrepared();Require(result.Moving && body.Prepared.Frames==saved.State.Frames+1 && body.Prepared.Motion.X==saved.State.Motion.X+36864,"Saved fractional frame failed to resume");
        // Save the newly advanced real entity for the next process reload.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=body.Prepared}));
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Reload enabled incomplete handler");
        Log.Information("Blade body reload: actual owner/creation/fractional pose preserved; native default summon expires, source-controlled body remains");
    }
}
