// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class WarlockGroundLaunchChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WarlockGroundLaunchChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("warlock-ground-launch-check","Shared Abyss/Apocalypse launch and explicit hit dispatch",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("warlock-ground-launch-check: passed");}catch(Exception e){Log.Error(e,"warlock-ground-launch-check: failed");}}));
    private sealed class Services:GroundLaunchServices
    {
        internal int Family,Guard,X,Y,NativeDistance=99,Checks,Cx,Cy,Size,Mask,Notifications,Distances,RoomLookups,TownLookups;
        internal bool Selected,Allocate=true,Room=true,Destination=true,InTown,Flag;
        internal uint Memory;internal ulong Seed=0x123456789ABCDEF0;
        internal readonly List<int[]> Requests=new();internal readonly List<ulong> AllocationSeeds=new();
        internal override bool HasSkill(int id)=>Guard!=1;
        internal override int SkillMissile()=>Guard==2?1000:Guard==7?0:709;
        internal override int MissileCount=>1000;
        internal override int ProgressiveMissile(int skill)=>Guard==2?0:Guard==7?-1:722;
        internal override bool HasRoom()=>Room&&Guard!=3;
        internal override Point2D Coordinates()=>new(X+(Selected?0:1),Y+(Selected?0:1));
        internal override bool DestinationRoom(Point2D p){RoomLookups++;return Destination;}
        internal override bool Town(){TownLookups++;return InTown;}
        internal override int MissileSize(int missile)=>Family==0?3:1;
        internal override bool Blocked(Point2D p,int size,int mask){Checks++;Cx=p.X;Cy=p.Y;Size=size;Mask=mask;int bits=Guard==4?1:Guard==5?4:Guard==6?0x40:0;return (bits&mask)!=0;}
        internal override int Distance(Point2D p){Distances++;return NativeDistance;}
        internal override void SetDoFlag()=>Flag=true;
        internal override uint Random()=>SourceRandom.Roll(ref Seed);
        internal override bool Create(GroundMissileRequest r){Requests.Add([r.Flags,r.Missile,r.Point.X,r.Point.Y,0,0,r.Skill,r.Rank,0,1,0,0]);AllocationSeeds.Add(Seed);return Allocate;}
        internal override void SetTargetMemory(uint seed)=>Memory=seed;
        internal override void Notify(GroundMissileRequest request,uint seed){Require(Memory==seed,"Notification preceded memory assignment");Notifications++;}
    }
    private sealed class Hit:GroundHitServices
    {
        internal int[] Input;internal int Dispatches,Targets,Function,Id,Level,Owners;
        internal override bool Present()=>Input[0]!=0;
        internal override bool HasRow()=>Input[1]!=0;
        internal override int Skill()=>Input[3];
        internal override int Rank()=>Input[4];
        internal override bool HasOwner(){Owners++;return Input[2]!=0;}
        internal override bool HasVictim()=>Input[5]!=0;
        internal override int HitParameter()=>163;
        internal override void SetTarget()=>Targets++;
        internal override void Dispatch(int fn,int skill,int rank){Require(Targets==(Input[5]!=0?1:0),"Dispatch preceded target assignment");Dispatches++;Function=fn;Id=skill;Level=rank;}
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"warlock-ground-launch-source-probe.json")));int start=0,launch=0,hit=0;
        foreach(var row in doc.RootElement.GetProperty("start").EnumerateArray())
        {
            var i=N(row);var s=new Services{Room=i[0]!=0,Selected=i[1]!=0,X=i[2],Y=i[3],Destination=i[4]!=0,InTown=i[5]!=0};
            Require(GroundMissileLaunch.Start14(s)==(i[6]!=0)&&s.RoomLookups==i[7]&&s.TownLookups==i[8],"Source start14 ordering/result differs");start++;
        }
        foreach(var row in doc.RootElement.GetProperty("launch").EnumerateArray())
        {
            var i=N(row[0]);var o=row[1];var s=new Services{Family=i[0],Guard=i[2],Selected=i[3]!=0,X=i[4],Y=i[5],NativeDistance=i[6],Allocate=i[7]!=0};
            bool result=i[0]==0?GroundMissileLaunch.Do28(402,i[1],s):GroundMissileLaunch.Do123(401,i[1],s);
            var state=new[]{result?1:0,s.Flag?0x100:0,s.Checks,s.Cx,s.Cy,s.Size,s.Mask};
            Require(state.SequenceEqual(o.EnumerateArray().Take(7).Select(x=>x.GetInt32()))&&s.Seed==o[7].GetUInt64()&&s.Memory==o[8].GetUInt32()&&s.Notifications==o[9].GetInt32()&&s.Distances==o[10].GetInt32(),"Source ground flags/placement/RNG/allocation ordering differs at "+launch);
            var requests=row[2].EnumerateArray().Select(N).ToArray();Require(s.Requests.Count==requests.Length&&s.Requests.Zip(requests).All(x=>x.First.SequenceEqual(x.Second)),"Source factory request differs at "+launch);
            Require(s.AllocationSeeds.SequenceEqual(row[3].EnumerateArray().Select(x=>x.GetUInt64())),"Source RNG-before-allocation differs");launch++;
        }
        foreach(var row in doc.RootElement.GetProperty("hit23").EnumerateArray())
        {
            var i=N(row);var h=new Hit{Input=i};int result=GroundMissileLaunch.Hit23(h);
            Require(new[]{result,h.Dispatches,h.Targets,h.Function,h.Id,h.Level}.SequenceEqual(i.Skip(6)),"Source hit23 explicit dispatch differs");
            Require(h.Owners==((i[0]!=0&&i[1]!=0&&i[3]>0&&i[4]>0)?1:0),"Invalid hit23 context reached owner lookup");hit++;
        }
        foreach(var source in doc.RootElement.GetProperty("profiles").EnumerateArray())
        {
            var r=WarlockGroundRoutes.For((CombatAbility)source.GetProperty("Ability").GetInt32());
            Require(JsonSerializer.Serialize(r)==JsonSerializer.Serialize(JsonSerializer.Deserialize<WarlockGroundRoute>(source.GetRawText())),"Owned ground profile differs");
            Require(!WarlockCombat.Implemented(r.Ability),"Missing do39/do163 damage route was enabled");
        }
        Require((start,launch,hit)==(64,2304,144),"Complete source ground grid missing");
        Log.Information("Warlock ground launch:64start14/2304do28-do123/144hit23contexts;exact placement/flags/seed-at-allocation and target-before-dispatch;new do39/do163remain gated");
    }
}
