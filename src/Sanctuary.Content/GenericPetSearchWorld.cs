// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace Sanctuary;

public sealed record GenericPetSearchMonster(int Id,string Name,byte Flags60,byte Threat137);
public sealed record GenericPetSearchLevel(int Id,string Name,byte Walk63);
internal sealed class GenericPetSearchTable
{
    private readonly GenericPetSearchMonster[] _monsters;
    private readonly GenericPetSearchLevel[] _levels;
    private readonly Dictionary<string,GenericPetSearchMonster> _names=new(StringComparer.OrdinalIgnoreCase);
    internal GenericPetSearchTable(GenericPetSearchMonster[] monsters,GenericPetSearchLevel[] levels)
    {
        ArgumentNullException.ThrowIfNull(monsters);ArgumentNullException.ThrowIfNull(levels);
        _monsters=(GenericPetSearchMonster[])monsters.Clone();_levels=(GenericPetSearchLevel[])levels.Clone();
        for(int i=0;i<_monsters.Length;i++){if(_monsters[i] is not{} r||r.Id!=i||string.IsNullOrWhiteSpace(r.Name)||!_names.TryAdd(r.Name,r)){throw new InvalidDataException("Invalid search monster row");}}
        for(int i=0;i<_levels.Length;i++){if(_levels[i] is not{} r||r.Id!=i||string.IsNullOrWhiteSpace(r.Name)){throw new InvalidDataException("Invalid search level row");}}
    }
    internal GenericPetSearchMonster Monster(int id)=>id>=0&&id<_monsters.Length?_monsters[id]:null;
    internal int MonsterCount=>_monsters.Length;
    internal GenericPetSearchMonster Monster(string name)=>name!=null?_names.GetValueOrDefault(name):null;
    internal GenericPetSearchLevel Level(int id)=>id>0&&id<_levels.Length?_levels[id]:null;
    internal GenericPetSearchTableView Bind(byte selector)=>new(this,selector);
}
internal sealed class GenericPetSearchTableView
{
    private readonly byte _selector;
    private readonly GenericPetSearchTable _table;
    internal GenericPetSearchTableView(GenericPetSearchTable table,byte selector)
    {
        ArgumentNullException.ThrowIfNull(table);
        if(selector is <1 or >3){throw new NotSupportedException("Search table view requires selector1..3");}
        _selector=selector;_table=table;
    }
    private void Require(byte selector){if(selector!=_selector){throw new NotSupportedException("Search table selector is not bound");}}
    internal GenericPetSearchMonster Monster(byte selector,int id){Require(selector);return _table.Monster(id);}
    internal GenericPetSearchLevel Level(byte selector,int id){Require(selector);return _table.Level(id);}
}
public static class GenericPetSearchTables
{
    private static GenericPetSearchTable _table;
    public static void Configure()
    {
        _table=new(BalanceCatalog.Read<GenericPetSearchMonster>("generic-pet-search-monsters"),
            BalanceCatalog.Read<GenericPetSearchLevel>("generic-pet-search-levels"));
    }
    // Explicit view binding does not establish real selector aliases or an area mapping.
    internal static GenericPetSearchTableView Bind(byte selector)=>_table.Bind(selector);
    internal static int? SourceId(string source)=>_table.Monster(source)?.Id;
    internal static string SourceName(int id)=>_table.Monster(id)?.Name;
    internal static int MonsterCount=>_table.MonsterCount;
}
internal sealed record GenericPetSearchStatList(int Flags28,IReadOnlyList<uint> Words);
internal sealed class GenericPetSearchSizeCache(int kind,int cls,int sizeX,int sizeY)
{internal int Kind=kind,ClassId=cls,SizeX=sizeX,SizeY=sizeY;}
internal sealed record GenericPetSearchWorldUnit(GenericPetDecisionUnit Position,int ClassId,int Mode,byte Selector445,
    int Room,uint Flags128,bool HasMonsterData,byte MonsterFlags26,GenericPetOwnerControl Owner,
    int MissileOwnerType,int MissileOwnerId,GenericPetSearchStatList Stats,GenericPetSearchSizeCache Cache);
internal sealed record GenericPetSearchCorpseMetadata(byte Flags4,byte Flags6);
internal sealed record GenericPetSearchWorldServices(Func<int,GenericPetSearchWorldUnit> Unit,
    Func<byte,int,GenericPetSearchMonster> Monster,Func<byte,int,GenericPetSearchLevel> Level,Func<byte,int> StateCount,
    Func<int,int,int,int> Resolve,Func<int,int,uint?> RelationFlags4,Func<int,int,int> Allegiance24fd30,
    Func<byte,int,GenericPetSearchCorpseMetadata> Monster2,Func<byte,int,int> CorpseStateMask296f60,
    Func<int,int> SizeX277db0,Func<int,int> SizeY277e60,
    Func<int,int,int,int,int,int,int,int,int> Ray27c500,Func<int,int,int> SpecialIndex40c060,
    Func<int,int,int,int,int> Point27c5e0);
internal static class GenericPetSearchWorld
{
    internal static int Room(int subject,GenericPetSearchWorldServices s)
    {
        if(subject==0){throw new NotSupportedException("Current room getter requires a unit");}
        var u=s.Unit(subject);if(u.Position.Kind is 2 or 4 or 5&&!u.Position.HasPath){throw new NotSupportedException("Current static room dereferences a missing path");}
        return u.Position.HasPath?u.Room:0;
    }
    internal static int HasState(int subject,int state,GenericPetSearchWorldServices s)
    {
        if(subject==0){return 0;}var u=s.Unit(subject);
        if(u.Position.Kind is not(0 or 1 or 3)||state<0){return 0;}
        int count=s.StateCount(u.Selector445);if(state>=count||u.Stats is not{} stats||stats.Flags28>=0||state>=511){return 0;}
        if(state/32>=stats.Words.Count){throw new NotSupportedException("Missing source state bitset word");}
        return unchecked((int)(stats.Words[state/32]&(1u<<(state&31))));
    }
    internal static int Priority(int subject,GenericPetSearchWorldServices s)
    {
        if(subject==0){return 0;}var u=s.Unit(subject);int kind=u.Position.Kind;
        if(kind==0){return 5;}
        if(kind==1&&s.Monster(u.Selector445,u.ClassId) is{} first&&(first.Flags60&64)!=0){return 4;}
        // The current body deliberately performs this second table lookup.
        if(kind==1&&s.Monster(u.Selector445,u.ClassId) is{} second&&(second.Flags60&128)!=0){return 4;}
        if(kind!=1||!u.HasMonsterData){return 0;}
        if((u.MonsterFlags26&2)!=0){return 3;}if((u.MonsterFlags26&8)!=0){return 2;}return (u.MonsterFlags26&4)!=0?1:0;
    }
    internal static int Corpse(int subject,GenericPetSearchWorldServices s)
    {
        if(subject==0){return 0;}var u=s.Unit(subject);
        if(u.Position.Kind!=1||u.Mode!=12){return 0;}
        var row=s.Monster2(u.Selector445,u.ClassId);
        if(row==null||(row.Flags6&8)!=0||(row.Flags4&128)==0){return 0;}
        if(u.Selector445 is <1 or >3){throw new NotSupportedException("Current corpse mask requires selector1..3");}
        return s.CorpseStateMask296f60(u.Selector445,subject)==0?1:0;
    }
    private static int Owner(int subject,GenericPetSearchWorldServices s)
    {
        if(subject==0){return 0;}var u=s.Unit(subject);
        return u.Position.Kind==1&&u.HasMonsterData&&u.Owner is{Game:not 0} a?s.Resolve(a.Game,a.Type,a.Id):0;
    }
    internal static int Valid(int game,int subject,int target,byte playerHostility,GenericPetSearchWorldServices s)
    {
        int origin=subject;
        if(subject!=0)
        {
            var u=s.Unit(subject);int owner=0;
            if(u.Position.Kind==1){owner=Owner(subject,s);}
            else if(u.Position.Kind==3&&(u.Flags128&1024)!=0){owner=s.Resolve(game,u.MissileOwnerType,u.MissileOwnerId);}
            if(owner!=0){origin=owner;}
        }
        int candidate=target;
        // Source ancestry follows monster owners until null/self/nonmonster.
        // Arbitrary cyclic invalid ownership graphs are not certified.
        while(candidate!=0)
        {int next=Owner(candidate,s);if(next==0||next==candidate){break;}candidate=next;}
        if(origin==candidate){return 0;}
        if(playerHostility!=0&&origin!=0&&s.Unit(origin).Position.Kind==0&&candidate!=0&&s.Unit(candidate).Position.Kind==0)
        {return (int)(s.RelationFlags4(origin,candidate).GetValueOrDefault()&8);}
        return s.Allegiance24fd30(origin,candidate)==0?1:0;
    }
    internal static void Sizes(int subject,GenericPetSearchWorldUnit u,GenericPetSearchWorldServices s)
    {
        var cache=u.Cache??throw new NotSupportedException("Missing source size cache");
        if(cache.Kind==u.Position.Kind&&cache.ClassId==u.ClassId){return;}
        cache.ClassId=u.ClassId;cache.Kind=u.Position.Kind;cache.SizeX=s.SizeX277db0(subject);cache.SizeY=s.SizeY277e60(subject);
    }
    internal static int Collision(int subject,int target,int mask,GenericPetSearchWorldServices s)
    {
        if(subject==0||target==0){throw new NotSupportedException("Current collision requires both units");}
        int room=Room(subject,s);if(room==0){return 0;}
        var source=s.Unit(subject);var(x,y)=source.Position.Coordinates();Sizes(subject,source,s);int size=source.Cache.SizeX;
        var dest=s.Unit(target);var(tx,ty)=dest.Position.Coordinates();Sizes(target,dest,s);
        return s.Ray27c500(x,y,size,tx,ty,dest.Cache.SizeX,room,mask);
    }
    internal static int Special(int subject,int target,GenericPetSearchWorldServices s)
    {
        if(subject==0||target==0){return 0;}int index=s.SpecialIndex40c060(subject,target);
        if(index<0){throw new NotSupportedException("Negative current special index reads outside the defined stack table");}
        int offset=index>=25?3:index<3?2:index<11?3:4;
        var(tx,ty)=s.Unit(target).Position.Coordinates();var(x,y)=s.Unit(subject).Position.Coordinates();
        int dx=unchecked(x-tx),dy=unchecked(y-ty);int ox=dx<0?-offset:dx>0?offset:0,oy=dy<0?-offset:dy>0?offset:0;
        if(s.Point27c5e0(subject,tx,ty,0x805)==0){return 1;}
        if(s.Point27c5e0(subject,unchecked(tx-oy),unchecked(ty+ox),0x805)==0){return 1;}
        return s.Point27c5e0(subject,unchecked(tx+oy),unchecked(ty-ox),0x805)==0?1:0;
    }
    internal static GenericPetSearchServices Bind(GenericPetSearchServices original,int game,GenericPetSearchWorldServices s,Func<int,GenericPetSearchRoom> room)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(s);ArgumentNullException.ThrowIfNull(room);
        return original with
        {
            Valid372ec0=(pet,target,flag)=>Valid(game,pet,target,flag,s),
            Room=unit=>{int id=Room(unit,s);return id==0?null:room(id);},
            Collision27c710=(pet,target,mask)=>Collision(pet,target,mask,s),LevelByte63=(selector,id)=>s.Level(selector,id)?.Walk63,
            Predicate40c450=(pet,target)=>Special(pet,target,s),Predicate27ddb0=unit=>Corpse(unit,s),Value3000=unit=>Priority(unit,s),
            MonsterByte137=(selector,id)=>s.Monster(selector,id)?.Threat137,HasState=(unit,state)=>HasState(unit,state,s)!=0
        };
    }
}
