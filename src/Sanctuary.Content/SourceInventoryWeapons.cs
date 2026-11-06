// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record SourceInventoryWeapon(uint Guid,int Kind,bool Weapon,bool MissilePotion,int Body,bool HasData,uint Flags);

// The four complete older inventory GUID routines. Grid/type/flag facts are
// explicit services; neither slot number nor insertion order alone is a hand.
internal sealed class SourceInventoryWeapons
{
    internal const uint Invalid=uint.MaxValue;
    internal readonly bool Present,Signature,Grid;
    internal readonly SourceInventoryWeapon[] Slots;
    internal uint Guid;
    internal SourceInventoryWeapons(bool present,bool signature,bool grid,uint guid,SourceInventoryWeapon[] slots)
    {if(slots is not{Length:13}){throw new ArgumentException("Source body grid needs thirteen slots");}Present=present;Signature=signature;Grid=grid;Guid=guid;Slots=slots;}
    internal SourceInventoryWeapon Primary()
    {
        if(Present&&Signature&&Guid!=Invalid&&Grid)
        {
            var item=Slots[5];if(item is{Weapon:true}&&item.Guid==Guid){return item;}
            item=Slots[4];if(item is{Weapon:true}&&item.Guid==Guid){return item;}
        }
        return null;
    }
    internal SourceInventoryWeapon Secondary()
    {
        if(Present&&Signature&&Guid!=Invalid&&Grid)
        {
            var left=Slots[5];if(left is not{Weapon:true}){left=null;}
            var right=Slots[4];if(right is not{Weapon:true}){right=null;}
            if(left!=null&&left.Guid==Guid){return right;}
            if(right!=null&&right.Guid==Guid){return left;}
        }
        return null;
    }
    private static bool Equippable(SourceInventoryWeapon item)=>SourceWeaponSelection.Equippable(item!=null,item?.Kind??0,item?.HasData??false,item?.Flags??0);
    internal void Insert(SourceInventoryWeapon item)
    {
        if(Present&&item is{Kind:4,Weapon:true,Body:4 or 5})
        {
            if(Equippable(item))
            {
                var weapon=Primary();
                if(weapon==null||!weapon.Weapon||weapon.MissilePotion)
                {if(Signature){Guid=item.Guid;}return;}
            }
            if(Signature&&Guid==item.Guid){Guid=Invalid;}
        }
    }
    internal void Remove(SourceInventoryWeapon item)
    {
        if(Present&&Signature&&item is{Kind:4,Weapon:true})
        {
            if(Guid==item.Guid){Guid=Invalid;}
            if(Grid)
            {
                SourceInventoryWeapon weapon;
                if(item.Body==4){weapon=Slots[5];}else if(item.Body==5){weapon=Slots[4];}else{return;}
                // Literal older behavior: the opposite item need not be a
                // weapon. The subsequent Primary getter performs that test.
                if(weapon is{Kind:4}&&Equippable(weapon)){Guid=weapon.Guid;}
            }
        }
    }
}
