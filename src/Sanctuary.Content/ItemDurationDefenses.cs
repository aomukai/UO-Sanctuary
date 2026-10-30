// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Recipient item stats only. Do not inherit the controller's equipment or
// impose player resistance caps/difficulty penalties on an NPC stat pool.
internal static class ItemDurationDefenses
{
    private static ItemStatContext? Equipment(Mobile target)=>target is SkeletonGuardian actor
        &&IronGolemItems.State(actor) is{} binding
        &&World.FindItem((Serial)binding.Item) is{Deleted:false} original&&original.Parent==actor
        ?NpcEquipmentCombat.Context(actor):null;
    // Native OnRemoved callbacks also run during an explicit move. They see
    // no passive bonus;the explicit binding reader still rejects that move.
    internal static bool HasEquipment(Mobile target)=>Equipment(target)!=null;
    private static int Bonus(Mobile target,string code)=>target is PlayerMobile player?ClassGear.Bonus(player,code)
        :Equipment(target)?.Bonus(code)??0;
    internal static int PoisonLength(Mobile target)=>Bonus(target,"res-pois-len");
    internal static bool CannotFreeze(Mobile target)=>Bonus(target,"nofreeze")>0;
    internal static bool HalfFreeze(Mobile target)=>Bonus(target,"half-freeze")>0;
}
