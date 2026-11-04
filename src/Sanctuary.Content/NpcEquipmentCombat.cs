// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;
namespace Sanctuary;

// Live original-item consumers for the existing D2 summon body. Native UO
// creature attribute bases and frozen Revive equipment need separate adapters.
// This is not full item/resource/proc support or a learned Iron Golem handler.
internal static class NpcEquipmentCombat
{
    internal static ItemStatContext? Context(BaseCreature actor)=>actor is SkeletonGuardian&&IronGolemItems.State(actor) is{} binding
        &&actor.ControlMaster?.Serial.Value==binding.Owner
        ?IronGolemItems.Equipment(actor,CreatureProfiles.Level(actor)):null;
    internal static int Bonus(BaseCreature actor,string code)=>Context(actor)?.Bonus(code)??0;
    internal static int EventBonus(BaseCreature actor,string code)=>ReviveEquipmentEvents.Bonus(actor,code)??Bonus(actor,code);
    internal static int DefensiveBonus(BaseCreature actor,string code)=>ReviveEquipmentDefenses.Bonus(actor,code)??Bonus(actor,code);
    internal static int DefensePercent(BaseCreature actor)=>ReviveEquipmentGraph.DefensePercent(actor);
    internal static int Defense(BaseCreature actor)=>ReviveEquipmentCombat.Defense(actor)??(Context(actor) is{} item
        ?checked(item.Defense+item.Bonus("dex")/4):0);
    internal static int AttackFlat(BaseCreature actor)=>ReviveEquipmentCombat.AttackFlat(actor)??(Context(actor) is{} item
        ?checked(item.Bonus("att")+5*item.Bonus("dex")):0);
    internal static int AttackPercent(BaseCreature actor)=>ReviveEquipmentCombat.AttackPercent(actor)??Bonus(actor,"att%");
}
