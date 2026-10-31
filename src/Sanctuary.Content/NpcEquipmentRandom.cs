// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record NpcEquipmentSeed(uint Actor,uint Owner,uint Item,ulong Seed,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool Unarmed=false);
public sealed class NpcEquipmentRandomProfiles:GenericPersistence
{
    public NpcEquipmentRandomProfiles():base("SanctuaryNpcEquipmentRandom",190){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(NpcEquipmentRandom.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown NPC equipment random version");}NpcEquipmentRandom.Restore(JsonSerializer.Deserialize<NpcEquipmentSeed[]>(reader.ReadString()));}
}

// One persisted source-algorithm stream per actor/original binding. This does
// not claim the historical D2 world seed or complete native AI seed ownership.
internal static class NpcEquipmentRandom
{
    private static readonly Dictionary<uint,NpcEquipmentSeed> Seeds=new();
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static NpcEquipmentRandomProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    internal static NpcEquipmentSeed State(SkeletonGuardian actor)=>Seeds.GetValueOrDefault(actor.Serial.Value);
    internal static void Forget(BaseCreature actor)=>Seeds.Remove(actor.Serial.Value);
    private static (uint Owner,uint Item,bool Unarmed) Binding(SkeletonGuardian actor)
    {
        if(NpcEquipmentCombat.Context(actor) is{} item)
        {return(actor.ControlMaster.Serial.Value,item.Item.Serial.Value,false);}
        if(actor is RaisedSkeleton&&actor.ControlMaster is PlayerMobile{Deleted:false} owner)
        {return(owner.Serial.Value,0,true);}
        throw new NotSupportedException("No original NPC or learned unarmed skeleton random binding");
    }
    internal static void Set(SkeletonGuardian actor,ulong seed)
    {
        var binding=Binding(actor);
        Seeds[actor.Serial.Value]=new(actor.Serial.Value,binding.Owner,binding.Item,seed,binding.Unarmed);
    }
    internal static uint Raw(SkeletonGuardian actor)
    {
        var binding=Binding(actor);
        if(!Seeds.TryGetValue(actor.Serial.Value,out var state)||state.Owner!=binding.Owner||state.Item!=binding.Item||state.Unarmed!=binding.Unarmed)
        {Set(actor,SourceRandom.InitLow(SeedLow()));state=Seeds[actor.Serial.Value];}
        ulong seed=state.Seed;uint value=SourceRandom.Roll(ref seed);Seeds[actor.Serial.Value]=state with{Seed=seed};return value;
    }
    internal static int Limited(SkeletonGuardian actor,int bound)=>bound<=0?0:(int)(Raw(actor)%(uint)bound);
    public static NpcEquipmentSeed[] Snapshot()=>Seeds.Values.Where(s=>World.FindMobile((Serial)s.Actor) is SkeletonGuardian{Deleted:false} actor
        &&actor.ControlMaster?.Serial.Value==s.Owner&&(s.Unarmed?actor is RaisedSkeleton&&s.Item==0&&NpcEquipmentCombat.Context(actor)==null
            :IronGolemItems.State(actor) is{} binding&&binding.Owner==s.Owner&&binding.Item==s.Item
                &&World.FindItem((Serial)s.Item) is{Deleted:false} item&&item.Parent==actor)).OrderBy(s=>s.Actor).ToArray();
    internal static void Restore(NpcEquipmentSeed[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing NPC equipment random state");}
        var rows=new Dictionary<uint,NpcEquipmentSeed>();
        foreach(var row in saved)
        {if(row==null||!((Serial)row.Actor).IsMobile||!((Serial)row.Owner).IsMobile||row.Actor==row.Owner
            ||(row.Unarmed?row.Item!=0:!((Serial)row.Item).IsItem)||!rows.TryAdd(row.Actor,row)){throw new InvalidDataException("Invalid NPC equipment random state");}}
        Seeds.Clear();foreach(var row in rows){Seeds.Add(row.Key,row.Value);}
    }
}
