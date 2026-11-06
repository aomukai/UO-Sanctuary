// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Sanctuary;

internal sealed record ValkyrieWeaponBinding(uint Primary,uint Secondary,string Identity);

// Saved original inventory identity and explicit attack-stream continuation.
// This admits weapon selection/allocation only, not AI/contact/item procs or
// an unknown current initializer/world seed. Original raw layers stay intact.
internal static class ValkyrieEquipmentWeapons
{
    private static bool Hash(string value)=>value is{Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static void Validate(ValkyrieWeaponBinding binding)
    {
        if(binding==null||!((Server.Serial)binding.Primary).IsItem||binding.Secondary!=0&&!((Server.Serial)binding.Secondary).IsItem
            ||binding.Primary==binding.Secondary||!Hash(binding.Identity))
        {throw new InvalidDataException("Invalid original Valkyrie weapon binding");}
    }
    private static ValkyrieWeaponBinding Original(AmazonValkyrieBody body)
    {
        var originals=ValkyrieEquipment.Originals(body);var slots=new SourceInventoryWeapon[13];
        var inventory=new SourceInventoryWeapons(true,true,true,SourceInventoryWeapons.Invalid,slots);
        foreach(var item in originals)
        {
            var s=item.Snapshot;var code=s.Metadata.Code;
            var row=new SourceInventoryWeapon(item.Serial.Value,4,NpcItemAffixes.Pool.IsType(code,"weap"),NpcItemAffixes.Pool.IsType(code,"mpot"),s.Equipment.Body,true,checked((uint)s.Metadata.Flags)|s.Equipment.Flags);
            // Materialize records only completed source placements. Replay
            // GUID updates after each placement, in original request order.
            slots[row.Body]=row;inventory.Insert(row);
        }
        if(inventory.Primary()is not{} primary){throw new NotSupportedException("Valkyrie has no admitted original primary weapon");}
        uint secondary=inventory.Secondary()?.Guid??0;
        string witness=JsonSerializer.Serialize(new{body.Prepared.Birth,body.Prepared.Equipment,Payloads=originals.Select(i=>i.SavedText).ToArray(),primary.Guid,Secondary=secondary});
        return new(primary.Guid,secondary,Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(witness))));
    }
    internal static ValkyrieWeaponBinding Binding(AmazonValkyrieBody body)
    {
        if(body?.Prepared is not{EquipmentStatProfile:ValkyrieEquipmentStats.Profile,EquipmentWeapons:{} saved})
        {throw new NotSupportedException("Valkyrie original weapon delivery has not been activated");}
        Validate(saved);if(Original(body)!=saved){throw new NotSupportedException("Valkyrie original hand/item/birth binding changed");}
        return saved;
    }
    internal static GeneratedNpcWeaponFrame Frame(AmazonValkyrieBody body)
    {
        var saved=Binding(body);var graph=body.EquipmentNumbers??throw new NotSupportedException("Missing original Valkyrie numeric graph");
        if(GeneratedNpcLiveResources.State(body)is{} resources&&(!resources.ActiveItems.Contains(saved.Primary)||saved.Secondary!=0&&!resources.ActiveItems.Contains(saved.Secondary)))
        {throw new NotSupportedException("Valkyrie original hand has been detached from the active stat graph");}
        return graph.Weapons(saved.Primary,saved.Secondary,false,false,0,0,0);
    }
    internal static void Activate(AmazonValkyrieBody body,ulong suppliedSeed)
    {
        if(body?.Prepared is not{EquipmentStatProfile:ValkyrieEquipmentStats.Profile,EquipmentWeapons:null} old||NpcEquipmentRandom.State(body)!=null)
        {throw new NotSupportedException("Valkyrie weapon delivery needs an unbound original numeric loadout and explicit starting seed");}
        var binding=Original(body);Validate(binding);_=body.EquipmentNumbers.Weapons(binding.Primary,binding.Secondary,false,false,0,0,0).Selected;
        try{body.RestorePrepared(old with{EquipmentWeapons=binding});NpcEquipmentRandom.Set(body,suppliedSeed);}
        catch{NpcEquipmentRandom.Forget(body);body.RestorePrepared(old);throw;}
    }
}
