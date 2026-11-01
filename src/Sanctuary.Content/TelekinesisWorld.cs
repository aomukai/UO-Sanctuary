// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Spells;

namespace Sanctuary;

// Native objects are explicit adapters. Do not invoke arbitrary OnDoubleClick
// handlers (potions would drink, scrolls cast, and native gates check proximity).
internal static class TelekinesisWorld
{
    internal static bool Pickup(Item item)=>item is Gold or BasePotion or Arrow or Bolt or Key or SpellScroll
        or IdentifyScroll or TownPortalScroll or TreasureSkeletonKey;

    internal static bool Valid(PlayerMobile owner,Serial selected)
    {
        if(owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal||owner.Holding!=null){return false;}
        var target=World.FindEntity(selected);
        if(target==null||target.Deleted||target.Map!=owner.Map){return false;}
        long dx=target.X-owner.X,dy=target.Y-owner.Y;
        // Pinned start12 uses a25-subtile inclusive squared radius. Sanctuary
        // projects two source subtiles to one native tile; no Chebyshev square.
        if(4*(dx*dx+dy*dy)>625||!owner.CanSee(target)||!owner.InLOS(target)){return false;}
        if(target is Mobile mobile){return ClassCombat.IsHostile(owner,mobile)
            &&!SpellHelper.IsTown(owner.Location,owner)&&!SpellHelper.IsTown(mobile.Location,owner)&&TelekinesisCombat.Supported(owner,mobile,out _);}
        // Nested inventory, corpses, gates and waypoints have no implicit
        // ground-item/object mapping. The latter need their travel adapter.
        return target is Item{Parent:null} item&&item is not(Corpse or BankBox or TownPortalEnd)
            &&(item is BaseDoor or BaseContainer||item.Movable);
    }

    internal static bool Operate(PlayerMobile owner,Item item)
    {
        if(!Valid(owner,item.Serial)||!item.IsAccessibleTo(owner)||!item.CheckItemUse(owner,item)
            ||!owner.Region.OnDoubleClick(owner,item)){return false;}
        if(item is BaseDoor door){item.OnItemUsed(owner,item);door.OnTelekinesis(owner);return true;}
        if(item is BaseContainer container)
        {
            item.OnItemUsed(owner,item);
            // Open retains lock/trap/house/display permissions. Native
            // OnTelekinesis only springs traps and would not open the chest.
            container.Open(owner);return true;
        }
        if(Pickup(item)){return TakeGround(owner,item);}
        // Older do21 leaves non-whitelisted items on the ground. Preserve
        // identity/ownership rather than emulating its engine refresh flags.
        item.InvalidateProperties();owner.PlaySound(0x1F5);return true; // Identified native placeholder.
    }

    private static bool TakeGround(PlayerMobile owner,Item item)
    {
        if(item is not{Deleted:false,Movable:true,Parent:null,IsLockedDown:false,IsSecure:false}
            ||owner.Backpack==null||owner.Frozen||owner.Paralyzed||owner.Holding!=null
            ||!item.VerifyMove(owner)||!item.IsAccessibleTo(owner)){return false;}
        var reject=LRReason.Inspecific;
        if(!item.CheckLift(owner,item,ref reject)
            ||owner.TotalWeight+item.TotalWeight+item.PileWeight>owner.MaxWeight
            ||!owner.OnDragLift(item)||!item.OnDragLift(owner)||!Valid(owner,item.Serial)
            ||item.Parent!=null||!item.VerifyMove(owner)||!item.IsAccessibleTo(owner)){return false;}
        // Same lift/drop callbacks as ClassLoot/native Lift, without replacing
        // the ordinary nearby-loot range or ever using/equipping the pickup.
        item.SetLastMoved();
        if(item.Spawner!=null){item.Spawner.Remove(item);item.Spawner=null;}
        item.OnItemLifted(owner,item);
        if(!Valid(owner,item.Serial)||item.Parent!=null||!item.VerifyMove(owner)||!item.IsAccessibleTo(owner)
            ||!owner.Backpack.TryDropItem(owner,item,true)){return false;}
        owner.RevealingAction();owner.PlaySound(0x57);return true;
    }
}
