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
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool Unarmed=false,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] uint ReviveCorpse=0,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string ReviveBinding=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string ValkyrieBinding=null);
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
    internal static NpcEquipmentSeed State(BaseCreature actor)=>Seeds.GetValueOrDefault(actor.Serial.Value);
    internal static void Forget(BaseCreature actor)=>Forget(actor.Serial.Value);
    internal static void Forget(uint actor)=>Seeds.Remove(actor);
    // V0's Unarmed flag denotes the zero-item binding. A prepared Tainted
    // shares that actor stream without claiming an implemented melee packet.
    private static bool WithoutItem(SkeletonGuardian actor)=>actor is ISourceUnarmedNpc or WarlockTaintedBody;
    private static (uint Owner,uint Item,bool Unarmed,uint Corpse,string Identity,string Valkyrie) Binding(BaseCreature actor)
    {
        if(actor is AmazonValkyrieBody valkyrie)
        {
            var weapons=ValkyrieEquipmentWeapons.Binding(valkyrie);
            return(valkyrie.Prepared.Owner,weapons.Primary,false,0,null,weapons.Identity);
        }
        if(ReviveNativeActivation.State(actor)is{Setup.EquipmentLifeProfile:ReviveEquipmentCombat.Profile} original)
        {
            if(actor is{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false} owner}&&original.Owner==owner.Serial.Value)
            {return(owner.Serial.Value,0,false,original.Original.Corpse,ReviveEquipmentLife.Identity(original),null);}
            throw new NotSupportedException("Frozen Revive random binding lost its living original/owner");
        }
        if(actor is SkeletonGuardian body)
        {
            if(NpcEquipmentCombat.Context(body)is{} item){return(actor.ControlMaster.Serial.Value,item.Item.Serial.Value,false,0,null,null);}
            if(WithoutItem(body)&&actor.ControlMaster is PlayerMobile{Deleted:false} unarmedOwner){return(unarmedOwner.Serial.Value,0,true,0,null,null);}
        }
        throw new NotSupportedException("No original-item,unarmed or frozen Revive random binding");
    }
    internal static void Set(BaseCreature actor,ulong seed)
    {
        var binding=Binding(actor);
        Seeds[actor.Serial.Value]=new(actor.Serial.Value,binding.Owner,binding.Item,seed,binding.Unarmed,binding.Corpse,binding.Identity,binding.Valkyrie);
    }
    internal static uint Raw(BaseCreature actor)
    {
        var binding=Binding(actor);
        if(!Seeds.TryGetValue(actor.Serial.Value,out var state)||state.Owner!=binding.Owner||state.Item!=binding.Item||state.Unarmed!=binding.Unarmed
            ||state.ReviveCorpse!=binding.Corpse||state.ReviveBinding!=binding.Identity||state.ValkyrieBinding!=binding.Valkyrie)
        {
            if(actor is AmazonValkyrieBody){throw new NotSupportedException("Missing or changed explicit Valkyrie attack RNG binding;automatic reseeding is forbidden");}
            Set(actor,SourceRandom.InitLow(SeedLow()));state=Seeds[actor.Serial.Value];
        }
        ulong seed=state.Seed;uint value=SourceRandom.Roll(ref seed);Seeds[actor.Serial.Value]=state with{Seed=seed};return value;
    }
    internal static NpcEquipmentSeed Require(BaseCreature actor)
    {
        var binding=Binding(actor);var state=State(actor);
        if(state==null||state.Owner!=binding.Owner||state.Item!=binding.Item||state.Unarmed!=binding.Unarmed
            ||state.ReviveCorpse!=binding.Corpse||state.ReviveBinding!=binding.Identity||state.ValkyrieBinding!=binding.Valkyrie)
        {throw new NotSupportedException("Missing or changed explicit NPC attack RNG context");}
        return state;
    }
    internal static int Limited(BaseCreature actor,int bound)=>bound<=0?0:(int)(Raw(actor)%(uint)bound);
    private static bool Matches(BaseCreature actor,NpcEquipmentSeed saved)
    {
        if(actor.ControlMaster?.Serial.Value!=saved.Owner){return false;}
        if(saved.ValkyrieBinding!=null)
        {
            if(actor is not AmazonValkyrieBody valkyrie){return false;}
            try{var weapons=ValkyrieEquipmentWeapons.Binding(valkyrie);return weapons.Primary==saved.Item&&weapons.Identity==saved.ValkyrieBinding;}
            catch(NotSupportedException){return false;}
        }
        if(saved.ReviveCorpse!=0)
        {
            return actor.Alive&&actor.ControlMaster is PlayerMobile{Alive:true,Deleted:false}
                &&ReviveNativeActivation.State(actor)is{Setup.EquipmentLifeProfile:ReviveEquipmentCombat.Profile} original
                &&original.Owner==saved.Owner&&original.Original.Corpse==saved.ReviveCorpse&&ReviveEquipmentLife.Identity(original)==saved.ReviveBinding;
        }
        return actor is SkeletonGuardian body&&(saved.Unarmed?WithoutItem(body)&&saved.Item==0&&NpcEquipmentCombat.Context(body)==null
            :IronGolemItems.State(body)is{} binding&&binding.Owner==saved.Owner&&binding.Item==saved.Item
                &&World.FindItem((Serial)saved.Item)is{Deleted:false} item&&item.Parent==actor);
    }
    public static NpcEquipmentSeed[] Snapshot()=>Seeds.Values.Where(s=>World.FindMobile((Serial)s.Actor)is BaseCreature{Deleted:false} actor&&Matches(actor,s)).OrderBy(s=>s.Actor).ToArray();
    internal static void Restore(NpcEquipmentSeed[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing NPC equipment random state");}
        var rows=new Dictionary<uint,NpcEquipmentSeed>();
        foreach(var row in saved)
        {if(row==null||!((Serial)row.Actor).IsMobile||!((Serial)row.Owner).IsMobile||row.Actor==row.Owner
            ||(row.ValkyrieBinding!=null?row.Unarmed||row.ReviveCorpse!=0||row.ReviveBinding!=null||!((Serial)row.Item).IsItem||row.ValkyrieBinding is not{Length:64}||row.ValkyrieBinding.Any(c=>c is not(>= '0' and <= '9' or >= 'a' and <= 'f'))
                :row.ReviveCorpse!=0?row.Item!=0||row.Unarmed||!((Serial)row.ReviveCorpse).IsItem||row.ReviveBinding is not{Length:64}||row.ReviveBinding.Any(c=>c is not(>= '0' and <= '9' or >= 'a' and <= 'f'))
                :row.ReviveBinding!=null||(row.Unarmed?row.Item!=0:!((Serial)row.Item).IsItem))||!rows.TryAdd(row.Actor,row)){throw new InvalidDataException("Invalid NPC equipment random state");}}
        Seeds.Clear();foreach(var row in rows){Seeds.Add(row.Key,row.Value);}
    }
}
