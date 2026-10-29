// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

public sealed record GenericPetMonsterSecondaryLink(int Id,short SecondaryId);
public sealed record GenericPetMonster2Metadata(int Id,string Name,int TextHcIdx,byte Flags4,byte Flags6,sbyte SizeX,sbyte SizeY);
internal sealed class GenericPetSecondaryTable
{
    private readonly GenericPetMonsterSecondaryLink[] _links;
    private readonly GenericPetMonster2Metadata[] _rows;
    internal GenericPetSecondaryTable(GenericPetMonsterSecondaryLink[] links,GenericPetMonster2Metadata[] rows)
    {
        ArgumentNullException.ThrowIfNull(links);ArgumentNullException.ThrowIfNull(rows);
        _links=(GenericPetMonsterSecondaryLink[])links.Clone();_rows=(GenericPetMonster2Metadata[])rows.Clone();
        for(int i=0;i<_links.Length;i++){if(_links[i] is not{} r||r.Id!=i){throw new InvalidDataException("Invalid secondary monster link");}}
        for(int i=0;i<_rows.Length;i++){if(_rows[i] is not{} r||r.Id!=i||string.IsNullOrWhiteSpace(r.Name)){throw new InvalidDataException("Invalid compiled secondary monster row");}}
    }
    internal GenericPetMonster2Metadata Row(int cls)
    {if(cls<0||cls>=_links.Length){return null;}int id=_links[cls].SecondaryId;return id>=0&&id<_rows.Length?_rows[id]:null;}
    internal GenericPetSecondaryView Bind(byte selector)=>new(this,selector);
}
internal sealed class GenericPetSecondaryView
{
    private readonly GenericPetSecondaryTable _table;
    private readonly byte _selector;
    internal GenericPetSecondaryView(GenericPetSecondaryTable table,byte selector)
    {ArgumentNullException.ThrowIfNull(table);if(selector is <1 or >3){throw new NotSupportedException("Secondary monster view requires selector1..3");}_table=table;_selector=selector;}
    internal GenericPetMonster2Metadata Row(byte selector,int cls)
    {if(selector!=_selector){throw new NotSupportedException("Secondary monster selector is not bound");}return _table.Row(cls);}
}
public static class GenericPetSecondaryTables
{
    internal static GenericPetSecondaryTable Table{get;private set;}
    public static void Configure()=>Table=new(BalanceCatalog.Read<GenericPetMonsterSecondaryLink>("generic-pet-search-monster-links"),BalanceCatalog.Read<GenericPetMonster2Metadata>("generic-pet-search-monster2"));
}
internal sealed record GenericPetSearchNestedUnit(int Id,bool HasObjectBase,int ObjectSizeX,int ObjectSizeY,int RelationHead,int PrimaryStatNode,int SecondaryStatNode);
internal sealed record GenericPetSearchRelationNode(int Guid,uint Flags4,int Next24);
internal sealed record GenericPetSearchStatNode(int State32,int Next104);
internal sealed record GenericPetSearchNestedServices(GenericPetSearchWorldServices World,Func<int,GenericPetSearchNestedUnit> Unit,
    Func<int,GenericPetSearchRelationNode> Relation,Func<int,GenericPetSearchStatNode> StatNode,
    Func<byte,int,bool> HasStatMetadata,Func<int,int,int> NodeStatValue,Func<byte,ulong> WideStateCount,
    Func<byte,uint[]> CorpseMask410,Func<byte,int,GenericPetMonster2Metadata> Secondary,Func<byte,int,byte?> MissileSize434,
    Func<int,int,int,int,int,int,int> Trace2a6c40);
internal static class GenericPetSearchNested
{
    internal static int Alignment(int subject,GenericPetSearchNestedServices s)
    {
        if(subject==0){return 0;}var u=s.World.Unit(subject);if(u.Stats==null){return 0;}
        if(u.Position.Kind is not(0 or 1)){return 2;}if(u.Stats.Flags28>=0){return 0;}
        var extra=s.Unit(subject);
        int Find(int node){while(node!=0){var r=s.StatNode(node);if(r.State32==105){return node;}node=r.Next104;}return 0;}
        int found=Find(extra.PrimaryStatNode);if(found==0){found=Find(extra.SecondaryStatNode);}
        return found!=0&&s.HasStatMetadata(u.Selector445,172)?s.NodeStatValue(found,0x00ac0000):0;
    }
    internal static int Allegiance(int subject,int target,GenericPetSearchNestedServices s)
    {
        if(subject==0||target==0){return 0;}if(subject==target){return 1;}
        int origin=Alignment(subject,s),other=Alignment(target,s);return origin==0&&other==0||origin==2&&other==2?1:0;
    }
    internal static int Relation(int subject,int target,GenericPetSearchNestedServices s)
    {
        if(subject==0||s.World.Unit(subject).Position.Kind!=0){throw new NotSupportedException("Current player relation requires a player source");}
        if(subject==target){return 0;}
        int id=target==0?-1:s.Unit(target).Id;
        if(!s.World.Unit(subject).HasMonsterData){throw new NotSupportedException("Current relation dereferences missing player data");}
        int node=s.Unit(subject).RelationHead;while(node!=0){var r=s.Relation(node);if(r.Guid==id){return node;}node=r.Next24;}return 0;
    }
    internal static int StateMask(int subject,uint[] mask,GenericPetSearchNestedServices s)
    {
        if(subject==0){return 0;}var u=s.World.Unit(subject);if(u.Stats is not{} stats||stats.Flags28>=0){return 0;}
        for(int i=0;;i=unchecked(i+1))
        {
            // The source reloads the wide count at every loop comparison.
            int count=unchecked((int)(unchecked(s.WideStateCount(u.Selector445)+31UL)>>5));if(i>=count){return 0;}
            if(mask==null||i>=mask.Length||i>=stats.Words.Count){throw new NotSupportedException("Missing source mask/state bitset word");}
            if((stats.Words[i]&mask[i])!=0){return 1;}
        }
    }
    private static int Dimension(int subject,bool vertical,GenericPetSearchNestedServices s)
    {
        if(subject==0){if(vertical){throw new NotSupportedException("Current second size getter requires a unit");}return 0;}
        var u=s.World.Unit(subject);
        switch(u.Position.Kind)
        {
            case 0:return 2;
            case 1:var row=s.Secondary(u.Selector445,u.ClassId);return row==null?0:vertical?row.SizeY:row.SizeX;
            case 2:
                var extra=s.Unit(subject);if(!u.HasMonsterData||!extra.HasObjectBase){throw new NotSupportedException("Current size dereferences missing object metadata");}
                return vertical?extra.ObjectSizeY:extra.ObjectSizeX;
            case 3:return s.MissileSize434(u.Selector445,u.ClassId)??0;
            case 4:return 1;
            default:return 0;
        }
    }
    internal static int SizeX(int subject,GenericPetSearchNestedServices s)=>Dimension(subject,false,s);
    internal static int SizeY(int subject,GenericPetSearchNestedServices s)=>Dimension(subject,true,s);
    private static int Abs32(int v)=>v<0?unchecked(-v):v;
    internal static int Ray(int x,int y,int size,int tx,int ty,int targetSize,int room,int mask,GenericPetSearchNestedServices s)
    {
        unchecked
        {
            int a=size<3?size:2,b=targetSize<3?targetSize:2,dx=Abs32(tx-x),dy=Abs32(ty-y);
            if(dx+dy<a+b){return 0;}
            if(a!=0||b!=0)
            {
                if(dy<=dx){if(tx>x){x+=a;tx-=b;}else{x-=a;tx+=b;}}
                if(dy>=dx){if(ty>y){y+=a;ty-=b;}else{y-=a;ty+=b;}}
            }
            return s.Trace2a6c40(room,x,y,tx,ty,mask);
        }
    }
    internal static int Point(int subject,int x,int y,int mask,GenericPetSearchNestedServices s)
    {
        if(subject==0){return 0;}int room=GenericPetSearchWorld.Room(subject,s.World);if(room==0){return 0;}
        var u=s.World.Unit(subject);var(ux,uy)=u.Position.Coordinates();GenericPetSearchWorld.Sizes(subject,u,s.World);
        return Ray(ux,uy,u.Cache.SizeX,x,y,2,room,mask,s);
    }
    internal static int SpecialIndex(int subject,int target,GenericPetSearchNestedServices s)
    {
        if(subject==0||target==0){throw new NotSupportedException("Current special index dereferences both units");}
        var u=s.World.Unit(subject);var(x,y)=u.Position.Coordinates();var(tx,ty)=s.World.Unit(target).Position.Coordinates();GenericPetSearchWorld.Sizes(subject,u,s.World);
        unchecked
        {
            int dx=Abs32(x-tx)-u.Cache.SizeX,dy=Abs32(y-ty)-u.Cache.SizeX;dx=Math.Max(0,dx);dy=Math.Max(0,dy);
            int total=dx>dy?2*dx+dy:dx+2*dy;return total/2;
        }
    }
    internal static GenericPetSearchWorldServices Bind(GenericPetSearchWorldServices original,GenericPetSearchNestedServices nested)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(nested);
        var context=nested;
        var result=original with
        {
            RelationFlags4=(source,target)=>{int node=Relation(source,target,context);return node==0?null:context.Relation(node).Flags4;},
            Allegiance24fd30=(source,target)=>Allegiance(source,target,context),
            Monster2=(selector,cls)=>context.Secondary(selector,cls) is{} row?new(row.Flags4,row.Flags6):null,
            CorpseStateMask296f60=(selector,unit)=>StateMask(unit,context.CorpseMask410(selector),context),
            SizeX277db0=unit=>SizeX(unit,context),SizeY277e60=unit=>SizeY(unit,context),
            Ray27c500=(x,y,size,tx,ty,targetSize,room,mask)=>Ray(x,y,size,tx,ty,targetSize,room,mask,context),
            SpecialIndex40c060=(source,target)=>SpecialIndex(source,target,context),Point27c5e0=(unit,x,y,mask)=>Point(unit,x,y,mask,context)
        };
        context=nested with{World=result};return result;
    }
}
