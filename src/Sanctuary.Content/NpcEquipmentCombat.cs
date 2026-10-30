// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;
namespace Sanctuary;

// Live original-item consumers for the existing D2 summon body. Native UO
// creature attribute bases and frozen Revive equipment need separate adapters.
// This is not full item/resource/proc support or a learned Iron Golem handler.
internal static class NpcEquipmentCombat
{
    private static ItemStatContext? Context(BaseCreature actor)=>actor is SkeletonGuardian&&IronGolemItems.State(actor) is{} binding
        &&actor.ControlMaster?.Serial.Value==binding.Owner
        ?IronGolemItems.Equipment(actor,CreatureProfiles.Level(actor)):null;
    internal static int Bonus(BaseCreature actor,string code)=>Context(actor)?.Bonus(code)??0;
    internal static int Defense(BaseCreature actor)=>Context(actor) is{} item
        ?checked(item.Defense+item.Bonus("dex")/4):0;
    internal static int AttackFlat(BaseCreature actor)=>Context(actor) is{} item
        ?checked(item.Bonus("att")+5*item.Bonus("dex")):0;
    internal static int AttackPercent(BaseCreature actor)=>Bonus(actor,"att%");
}
