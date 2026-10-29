// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

public sealed record GenericPetMissileSize(int Id,string Name,byte Size434);
internal sealed class GenericPetProducerView
{
    private readonly byte Selector;
    private readonly GenericPetMissileSize[] Missiles;
    private readonly int StatCount,MissileCount;
    internal GenericPetProducerView(byte selector,GenericPetMissileSize[] missiles,int statCount,int missileCount)
    {
        if(selector is <1 or >3){throw new NotSupportedException("Producer table view requires selector1..3");}
        ArgumentNullException.ThrowIfNull(missiles);
        if(statCount is <0 or >368||missileCount<0||missileCount>missiles.Length){throw new NotSupportedException("Unsupported source table extent");}
        Selector=selector;Missiles=missiles;StatCount=statCount;MissileCount=missileCount;
    }
    private void Require(byte selector){if(selector!=Selector){throw new NotSupportedException("Producer selector is not bound");}}
    internal EquipmentStatDefinition Stat(byte selector,int id){Require(selector);return id>=0&&id<StatCount?EquipmentStatValues.Definition(id):null;}
    internal GenericPetMissileSize Missile(byte selector,int id){Require(selector);return id>=0&&id<MissileCount?Missiles[id]:null;}
}
public static class GenericPetProducerTables
{
    private static GenericPetMissileSize[] Missiles;
    public static void Configure()
    {
        var rows=BalanceCatalog.Read<GenericPetMissileSize>("generic-pet-missile-sizes");
        if(rows.Length!=742){throw new InvalidDataException("Incomplete current missile size catalog");}
        for(int i=0;i<rows.Length;i++){if(rows[i] is not{} row||row.Id!=i||string.IsNullOrWhiteSpace(row.Name)){throw new InvalidDataException("Invalid missile size row");}}
        Missiles=(GenericPetMissileSize[])rows.Clone();
    }
    internal static GenericPetProducerView Bind(byte selector,int statCount=368,int missileCount=742)=>new(selector,Missiles,statCount,missileCount);
}
internal sealed record GenericPetProducerServices(Func<byte,int,EquipmentStatDefinition> Stat,Func<int,EquipmentStatNode> Node,
    Func<byte,int,GenericPetMissileSize> Missile,Func<int,SourceCollisionRoom> Room);
internal static class GenericPetSearchProducers
{
    internal static GenericPetSearchNestedServices Bind(GenericPetSearchNestedServices original,GenericPetProducerServices s)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(s);
        return original with
        {
            StatMetadata=s.Stat,
            NodeStatValue=(node,key,metadata)=>EquipmentStatValues.SourceRead(s.Node(node),unchecked((uint)key),metadata),
            MissileSize434=(view,id)=>s.Missile(view,id)?.Size434,
            Trace2a6c40=(room,x,y,tx,ty,mask)=>SourceRoomTrace.Run(room,new(x,y),new(tx,ty),mask,s.Room)
        };
    }
}
