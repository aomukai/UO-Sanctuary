// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class MeleeGeometryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MeleeGeometryChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("melee-geometry-check","Shared source melee distance and contact geometry",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("melee-geometry-check: passed");}catch(Exception e){Log.Error(e,"melee-geometry-check: failed");}}));
    private static MeleeGeometryUnit Unit(int x,int y,int size,bool room=true,int type=1,int id=0,int raw=0,bool thrust=false)=>new(new(x,y),size,type,id,raw,thrust,room);
    private static void Rays(List<int[]> actual,JsonElement expected,string why)
    {var want=expected.EnumerateArray().Select(N).ToArray();Require(actual.Count==want.Length&&actual.Zip(want).All(p=>p.First.SequenceEqual(p.Second)),why);}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"melee-geometry-source-probe.json")));int count=0,shared=0,ranges=0;
        foreach(var row in doc.RootElement.GetProperty("geometry").EnumerateArray())
        {
            var i=N(row[0]);var first=Unit(40,40,i[2],i[4]!=0);var second=Unit(40+i[0],40+i[1],i[3]);
            int distance=SourceMeleeGeometry.Distance(first.Cell,second.Cell,first.Size,second.Size);
            Require(distance==row[1].GetInt32(),"Source melee distance differs at "+count);
            if(first.Size<=3&&second.Size<=3){Require(DruidWolfGeometry.MeleeDistance(first.Cell,second.Cell,first.Size,second.Size)==distance,"Existing wolf distance delegation changed");shared++;}
            var queries=new List<int[]>();bool hit=SourceMeleeGeometry.Collision(first,second,(a,b,mask)=>{queries.Add([a.X,a.Y,b.X,b.Y,mask]);return i[5]!=0;});
            Require(hit==(row[2].GetInt32()!=0),"Source collision/overlap differs at "+count);Rays(queries,row[3],"Source collision endpoints/order/mask differ at "+count);count++;
        }
        foreach(var row in doc.RootElement.GetProperty("range").EnumerateArray())
        {
            var i=N(row[0]);var first=(i[0]&1)!=0?Unit(40,40,i[3],i[8]!=0,raw:i[5],thrust:i[6]!=0):null;
            var second=(i[0]&2)!=0?Unit(40+i[10],40+i[11],i[4],type:i[1],id:i[2]):null;
            var queries=new List<int[]>();bool answer=SourceMeleeGeometry.Within(first,second,i[7],(a,b,mask)=>{queries.Add([a.X,a.Y,b.X,b.Y,mask]);return i[9]!=0;});
            Require(answer==(row[1].GetInt32()!=0),"Source melee range differs at "+ranges);Rays(queries,row[2],"Source special/zero-distance/collision short-circuit differs at "+ranges);ranges++;
        }
        int profiles=0;
        foreach(var row in doc.RootElement.GetProperty("profiles").EnumerateArray())
        {
            string key=row.GetProperty("Key").GetString();var s=SourceMeleeGeometry.Npc(key,new(123,456),false);
            Require(s.Cell==new Point2D(123,456)&&!s.HasRoom&&s.UnitType==1&&s.BaseId==row.GetProperty("BaseId").GetInt32()&&s.Size==row.GetProperty("Size").GetInt32()&&s.RawRange==row.GetProperty("RawRange").GetInt32(),"Owned NPC profile differs");profiles++;
        }
        Require((count,shared,ranges,profiles)==(32400,14400,91464,3),"Complete source geometry grid missing");
        Unsupported();Pipeline();
        Log.Information("Melee geometry:32400distance/endpoints,91464range and14400retained wolf distances;three current NPC profiles,native contact HP/free/retained-copy bridge pass;actual world ray/metadata remain explicit");
    }
    private static void Unsupported()
    {
        bool Refused(Action action){try{action();return false;}catch(NotSupportedException){return true;}}
        Require(Refused(()=>SourceMeleeGeometry.Npc("unknown",new(0,0))),"Unknown NPC profile silently guessed");
        Require(Refused(()=>SourceMeleeGeometry.Distance(new(0,0),new(1,1),-1,2)),"Invalid source size accepted");
        Require(Refused(()=>DruidWolfGeometry.MeleeDistance(new(0,0),new(1,1),4,2)),"Existing wolf size contract broadened implicitly");
        Require(Refused(()=>SourceMeleeGeometry.Within(Unit(0,0,2,type:0),Unit(8,0,2),3,(_,_,_)=>false)),"Player weapon supplier silently guessed");
    }
    private sealed class Contact:GeometricMonsterContactServices
    {
        internal BaseCreature Actor,Target;internal PreparedMonsterDamagePacket Packet;internal bool Blocked;internal int RayCalls;
        internal override PreparedMonsterDamagePacket CapturedPacket()=>Packet;
        internal override MeleeGeometryUnit AttackerGeometry()=>SourceMeleeGeometry.Npc("wargoatman",PetOwnerRoutes.Cell(Actor),Actor.Map!=null&&Actor.Map!=Map.Internal);
        internal override MeleeGeometryUnit DefenderGeometry()=>new(PetOwnerRoutes.Cell(Target),2,1,0);
        internal override bool CollisionRay(Point2D a,Point2D b,int mask){Require(mask==0x804,"Native ray received wrong source mask");RayCalls++;return Blocked;}
        internal override bool AttackerInDeathMode()=>false;
        internal override bool Hostile()=>true;
        internal override bool DefenderHasRoom()=>true;
        internal override bool DefenderInTown()=>false;
        internal override bool AttackerAllowedInTown()=>false;
        internal override bool DefenderKillable()=>true;
        internal override bool DefenderDead()=>false;
    }
    private sealed class Damage:MonsterOrderedReleaseChecks.Services
    {
        internal Contact Store;internal int Frees;
        internal override void FreePacket(){base.FreePacket();Store.Packet=null;Frees++;}
    }
    private static void Pipeline()
    {
        var actor=MonsterOrderedReleaseChecks.Body();var target=MonsterOrderedReleaseChecks.Body();actor.MoveToWorld(new(6100,1590,0),Map.Trammel);target.MoveToWorld(new(6101,1590,0),Map.Trammel);
        SkillHealth.SetBalance256(target,100001);int life=100001;
        foreach(int offset in new[]{1,2,4})
        {
            target.Location=new(6100+offset,1590,0);var packet=new PreparedMonsterDamagePacket(17,17,0,0,1);
            var contact=new Contact{Actor=actor,Target=target,Packet=packet,Blocked=true};bool allowed=offset==1;
            var damage=new Damage{Store=contact,Body=target,At=TimedEffects.Now,ExpectedLife=allowed?life-17:life,HealthWrite=allowed};
            var r=PreparedMonsterContact.Release(actor,target,contact,new(false,()=>false,()=>5,()=>false),_=>0,damage,TimedEffects.Now);
            if(allowed){life-=17;}
            Require(SkillHealth.Balance256(target)==life&&r.Exit==(allowed?MonsterContactExit.Finished:MonsterContactExit.OutOfRangeFreed),"Geometry did not gate actual contact HP");
            Require(contact.RayCalls==(offset==2?1:0)&&damage.Frees==1&&contact.Packet==null&&packet is{Physical256:17,Total256:17,HitFlags:0},"Contact ray/packet ownership or captured copy differs");
            var again=PreparedMonsterContact.Release(actor,target,contact,null,null,damage,default);
            Require(again.Exit==MonsterContactExit.MissingPacket&&damage.Frees==1&&SkillHealth.Balance256(target)==life,"Freed captured contact replayed");
        }
        actor.Delete();target.Delete();
    }
}
