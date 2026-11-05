// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

// Monster labels are symbolic source contexts,not modern row/binary IDs.
internal enum EquipMonster {Other,ShadowWarrior,ShadowMaster,AncientBarbarian1,AncientBarbarian2,AncientBarbarian3}
internal sealed record EquipPlacementItem(string Code,int UnitType,bool HasData,int Quality,int File,bool Ethereal,bool Stackable,
    bool TwoHanded,bool OneOrTwoHanded,bool Weapon,bool Claw,int Body1,int Body2,string Ammo,string Quiver,string[] Types,int[] Damage,int Sockets);
internal sealed record EquipPlacementActor(int UnitType,int Class,EquipMonster Monster,bool HasInventory,bool Signature,bool Grid,
    EquipPlacementItem[] Slots,Func<EquipPlacementItem,bool> Requirements,Func<bool> FreePosition);

// Complete older body/equal-stack/one-or-two-handed and inventory outcome
// contracts. Actual inventory placement,requirements and stat delivery remain
// explicit services;NPC callers skip requirements,as their source caller does.
internal static class SourceEquipPlacement
{
    internal static bool BodyAllowed(EquipPlacementItem item,byte body)=>item is{UnitType:4}&&(item.Body1==body||item.Body2==body
        ||body is 11 or 12&&(item.Body1 is 4 or 5||item.Body2 is 4 or 5));
    internal static bool OneOrTwo(EquipPlacementActor actor,EquipPlacementItem item)=>actor.UnitType==0&&actor.Class==4&&item.OneOrTwoHanded;
    internal static bool TwoHanded(EquipPlacementItem item)=>item is{UnitType:4,TwoHanded:true};
    private static bool Weapon(EquipPlacementItem item)=>item is{UnitType:4,Weapon:true};
    private static bool Claw(EquipPlacementItem item)=>item is{UnitType:4,Claw:true};
    internal static bool EqualStacks(EquipPlacementItem first,EquipPlacementItem second)
    {
        if(first is not{UnitType:4,HasData:true}||second is not{UnitType:4,HasData:true}){return false;}
        if(first.Code!=second.Code||first.Quality!=second.Quality||first.File!=second.File||!first.Stackable){return false;}
        if(first.Ethereal!=second.Ethereal||first.Quality==0||first.Quality is>3 and<=9||second.Quality==0||second.Quality is>3 and<=9
            ||!first.Damage.SequenceEqual(second.Damage)){return false;}
        return first.Sockets==0&&second.Sockets==0;
    }
    internal static bool Compatible(EquipPlacementActor actor,EquipPlacementItem first,EquipPlacementItem second)
    {
        if(first==null||second==null||first.UnitType==4&&first.Ammo.Length!=0&&second.UnitType==4&&second.Types.Contains(first.Ammo)
            ||second.UnitType==4&&second.Ammo.Length!=0&&first.UnitType==4&&first.Types.Contains(second.Ammo)){return true;}
        if(first.UnitType==4&&first.Quiver.Length!=0||second.UnitType==4&&second.Quiver.Length!=0){return false;}
        if(!TwoHanded(first)||OneOrTwo(actor,first))
        {
            if(!TwoHanded(second)||OneOrTwo(actor,second))
            {
                if(Weapon(first)&&Weapon(second))
                {
                    if(actor==null){return false;}
                    if(actor.UnitType==1)
                    {
                        return actor.Monster switch
                        {EquipMonster.ShadowWarrior or EquipMonster.ShadowMaster=>Claw(first)&&Claw(second),
                         EquipMonster.AncientBarbarian1 or EquipMonster.AncientBarbarian2 or EquipMonster.AncientBarbarian3=>true,_=>false};
                    }
                    if(actor.UnitType!=0){return false;}
                    if(actor.Class==4){return true;}
                    if(actor.Class!=6||!Claw(first)||!Claw(second)){return false;}
                }
                else if(!Weapon(first)&&!Weapon(second)){return false;}
                return true;
            }
        }
        return false;
    }
    private static EquipPlacementItem Slot(EquipPlacementActor actor,int body)=>actor.Signature&&actor.Grid&&body is>=0 and<13?actor.Slots[body]:null;
    internal static int Hands(EquipPlacementActor actor,int otherBody,EquipPlacementItem item,EquipPlacementItem same,EquipPlacementItem other)
    {
        if(item==null){return same!=null?3:TwoHanded(other)?4:0;}
        if(same!=null)
        {
            if(EqualStacks(same,item)){return 6;}
            if(Compatible(actor,item,other)){return 5;}
            if(actor.Signature&&otherBody is>=0 and<13&&actor.Grid&&actor.Slots[otherBody]!=null&&actor.FreePosition()){return 7;}
            return 0;
        }
        return other!=null?2-(Compatible(actor,item,other)?1:0):1;
    }
    internal static int Classify(EquipPlacementActor actor,int body,EquipPlacementItem item,bool skipRequirements)
    {
        if(actor!=null&&actor.HasInventory&&(item==null||BodyAllowed(item,unchecked((byte)body))&&(skipRequirements||actor.Requirements(item))))
        {
            switch(body)
            {
                case 0:return 0;
                case 4:case 5:case 11:case 12:
                    int other=body switch{4=>5,5=>4,11=>12,_=>11};return Hands(actor,other,item,Slot(actor,body),Slot(actor,other));
                default:var held=Slot(actor,body);return item!=null?held!=null?5:1:held!=null?3:0;
            }
        }
        return 0;
    }
}
