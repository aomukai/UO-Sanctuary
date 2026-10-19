// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;

namespace Sanctuary;

public enum GearMaterial { Unknown, Metal, Wood, Leather, Cloth, Bone }
public sealed record NativeGearDetails(GearMaterial Material, CraftResource Resource, string Creator,
    int? Durability, int? MaximumDurability);

public static class GearDetails
{
    public static bool Socketable(Item item) => ClassGear.Slot(item) is
        GearSlot.Weapon or GearSlot.Shield or GearSlot.Head or GearSlot.Body;

    // Native serializers remain the sole owners of resource, maker and wear state.
    public static NativeGearDetails Read(Item item)
    {
        var material = item is BaseArmor { MaterialType: ArmorMaterialType.Bone } ? GearMaterial.Bone
            : EquipmentEconomy.Material(item) switch
            {
                SalvageMaterial.Metal => GearMaterial.Metal,
                SalvageMaterial.Wood => GearMaterial.Wood,
                SalvageMaterial.Leather => GearMaterial.Leather,
                SalvageMaterial.Cloth => GearMaterial.Cloth,
                _ => GearMaterial.Unknown
            };
        var resource = item switch
        {
            BaseWeapon weapon => weapon.Resource,
            BaseArmor armor => armor.Resource,
            BaseClothing clothing => clothing.Resource,
            BaseJewel jewel => jewel.Resource,
            _ => CraftResource.None
        };
        // Some native wood weapons default to Iron; hue is never a material identifier.
        if (material == GearMaterial.Wood && CraftResources.GetType(resource) != CraftResourceType.Wood)
        {
            resource = CraftResource.RegularWood;
        }
        if (material is GearMaterial.Cloth or GearMaterial.Bone)
        {
            resource = CraftResource.None;
        }
        return item switch
        {
            BaseWeapon weapon => new(material, resource, weapon.Crafter, weapon.HitPoints, weapon.MaxHitPoints),
            BaseArmor armor => new(material, resource, armor.Crafter, armor.HitPoints, armor.MaxHitPoints),
            BaseClothing clothing => new(material, resource, clothing.Crafter, clothing.HitPoints, clothing.MaxHitPoints),
            BaseJewel jewel => new(material, resource, null, jewel.HitPoints, jewel.MaxHitPoints),
            _ => new(material, resource, null, null, null)
        };
    }

    internal static void CopyNative(Item source, Item destination)
    {
        switch (source)
        {
            case BaseWeapon weapon when destination is BaseWeapon copy:
                copy.Resource = weapon.Resource;
                copy.Quality = weapon.Quality;
                copy.Crafter = weapon.Crafter;
                copy.Identified = weapon.Identified;
                copy.MaxHitPoints = weapon.MaxHitPoints;
                copy.HitPoints = weapon.HitPoints;
                break;
            case BaseArmor armor when destination is BaseArmor copy:
                copy.Resource = armor.Resource;
                copy.Quality = armor.Quality;
                copy.Crafter = armor.Crafter;
                copy.Identified = armor.Identified;
                copy.MaxHitPoints = armor.MaxHitPoints;
                copy.HitPoints = armor.HitPoints;
                break;
            case BaseClothing clothing when destination is BaseClothing copy:
                copy.Resource = clothing.Resource;
                copy.Quality = clothing.Quality;
                copy.Crafter = clothing.Crafter;
                copy.MaxHitPoints = clothing.MaxHitPoints;
                copy.HitPoints = clothing.HitPoints;
                break;
            case BaseJewel jewel when destination is BaseJewel copy:
                copy.Resource = jewel.Resource;
                copy.MaxHitPoints = jewel.MaxHitPoints;
                copy.HitPoints = jewel.HitPoints;
                break;
        }
    }
}
