// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
namespace Sanctuary;

internal sealed record NpcMeleeGeometryProfile(string Key,int BaseId,int Size,int RawRange);
internal sealed record MeleeGeometryUnit(Point2D Cell,int Size,int UnitType,int BaseId,int RawRange=0,bool TwoHandThrust=false,bool HasRoom=true);
internal static class SourceMeleeGeometry
{
    internal const int FlyingCollisionMask=0x804;
    private static readonly Lazy<IReadOnlyDictionary<string,NpcMeleeGeometryProfile>> Profiles=new(()=>
    {
        var rows=BalanceCatalog.Read<NpcMeleeGeometryProfile>("npc-melee-geometry");
        if(rows.Length!=3||rows.Any(s=>string.IsNullOrWhiteSpace(s.Key)||s.BaseId<0||s.Size is <0 or >255||s.RawRange is <0 or >255)
            ||rows.Select(s=>s.Key).Distinct(StringComparer.Ordinal).Count()!=rows.Length)
        {throw new InvalidDataException("Invalid NPC melee geometry profiles");}
        return rows.ToDictionary(s=>s.Key,s=>s,StringComparer.Ordinal);
    });
    internal static MeleeGeometryUnit Npc(string key,Point2D cell,bool hasRoom=true)
    {
        if(key==null||!Profiles.Value.TryGetValue(key,out var p)){throw new NotSupportedException("No verified NPC melee geometry profile");}
        // None of the owned demon profiles uses the255weapon-class sentinel.
        if(p.RawRange==255){throw new NotSupportedException("NPC melee range requires the actual composite weapon class");}
        return new(cell,p.Size,1,p.BaseId,p.RawRange,false,hasRoom);
    }
    private static void Size(int value)
    {if(value is <0 or >255){throw new NotSupportedException("Unsupported source melee unit size");}}
    internal static int Distance(Point2D a,Point2D b,int firstSize,int secondSize)
    {
        Size(firstSize);Size(secondSize);
        int x=Math.Abs(checked(a.X-b.X)),y=Math.Abs(checked(a.Y-b.Y));
        if(x>=8||y>=8||firstSize>=4||secondSize>=4)
        {
            int size=firstSize/2+secondSize/2;
            x=Math.Max(0,x-size);y=Math.Max(0,y-size);
            return checked(Math.Min(x,y)+2*Math.Max(x,y));
        }
        // Shared with the existing path/wolf near-distance consumer;do not
        // replace this table with the distinct weighted UnitDistance helper.
        int distance=SourceTowardPath.ShortDistance(x,y);
        if(distance<0){return 0;}
        if(firstSize==3||secondSize==3){distance=Math.Max(0,distance-1);}
        return distance+(firstSize<=1||secondSize<=1?1:0);
    }
    internal static int NpcReach(int rawRange,bool twoHandThrust)
    {
        if(rawRange is < -1 or >255){throw new NotSupportedException("Unsupported source NPC melee range");}
        return rawRange<0?0:rawRange==255?(twoHandThrust?2:0):rawRange;
    }
    internal static bool Collision(MeleeGeometryUnit first,MeleeGeometryUnit second,Func<Point2D,Point2D,int,bool> ray)
    {
        if(!first.HasRoom){return false;}
        Size(first.Size);Size(second.Size);
        int s1=Math.Min(first.Size,2),s2=Math.Min(second.Size,2);
        var a=first.Cell;var b=second.Cell;
        int x=Math.Abs(checked(b.X-a.X)),y=Math.Abs(checked(b.Y-a.Y));
        if(checked(x+y)<s1+s2){return false;}
        if(s1!=0||s2!=0)
        {
            if(x>=y)
            {int sign=b.X<=a.X?-1:1;a=new(checked(a.X+sign*s1),a.Y);b=new(checked(b.X-sign*s2),b.Y);}
            if(y>=x)
            {int sign=b.Y<=a.Y?-1:1;a=new(a.X,checked(a.Y+sign*s1));b=new(b.X,checked(b.Y-sign*s2));}
        }
        ArgumentNullException.ThrowIfNull(ray);
        return ray(a,b,FlyingCollisionMask);
    }
    internal static bool Within(MeleeGeometryUnit first,MeleeGeometryUnit second,int bonus,Func<Point2D,Point2D,int,bool> ray)
    {
        if(first==null||second==null){return false;}
        if(first.UnitType!=1){throw new NotSupportedException("Player weapon reach is not mapped by the NPC range supplier");}
        int Reach()=>NpcReach(first.RawRange,first.TwoHandThrust);
        int Measure()=>Distance(first.Cell,second.Cell,first.Size,second.Size);
        if(second.UnitType==1&&second.BaseId is 258 or 261)
        {if(checked(Reach()+8)>Measure()){return true;}}
        int distance=Measure();
        if(distance<=0){return true;}
        return checked(Reach()+bonus+1)>=distance&&!Collision(first,second,ray);
    }
}
