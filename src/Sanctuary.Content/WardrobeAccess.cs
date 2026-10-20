// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Multis;

namespace Sanctuary;

public static class WardrobeAccess
{
    public static Item NearbyWardrobe(PlayerMobile player)
    {
        foreach (var hall in TownRecognition.Halls)
        {
            var wardrobe = TownRecognition.PublicWardrobe(hall.Id);
            if (SourceValid(player, wardrobe)) { return wardrobe; }
        }
        return null;
    }
    public static bool SourceValid(PlayerMobile player, Item wardrobe)
    {
        if (wardrobe is not { Deleted: false, Parent: null } || wardrobe.Map != player.Map
            || !player.InRange(wardrobe, 2) || Math.Abs(player.Z - wardrobe.Z) >= 16
            || !player.CanSee(wardrobe) || !player.InLOS(wardrobe)) { return false; }
        return wardrobe switch
        {
            PublicRecognitionWardrobe publicWardrobe => ReferenceEquals(TownRecognition.PublicWardrobe(publicWardrobe.Site), wardrobe)
                && TownRecognition.AtOffice(player, publicWardrobe.Site),
            EarnedWardrobe personal => personal.Recipient == player.Serial.Value
                && BaseHouse.FindHouseAt(personal) is { } house && house.IsOwner(player),
            _ => false
        };
    }
    public static bool Ready(PlayerMobile player, Item wardrobe)
    {
        if (!ClassProfessions.Affected(player) || !ClassProfessions.Ready(player) || !SourceValid(player, wardrobe)
            || !TownRecognition.HasAccess(player) || player.Combatant != null
            || player.Spell != null || player.Target != null) { return false; }
        foreach (var follower in player.AllFollowers ?? [])
        {
            if (follower is BaseCreature { Deleted: false, Alive: true, Combatant: not null }) { return false; }
        }
        var session = ClassCombat.For(player);
        long now = Core.TickCount;
        if (session.Target != Serial.Zero || session.CastTarget != Serial.Zero || session.HeldAttack || session.PendingAbility != CombatAbility.None || session.HeldAbility != CombatAbility.None
            || now - session.NextAction < 0 || HitRecovery.Active(player, now)
            || now - player.NextSkillTime < 0 || now - player.NextSpellTime < 0
            || !player.CanBeginAction<Server.Engines.Craft.CraftSystem>()) { return false; }
        foreach (var aggression in player.Aggressors)
        {
            if (!aggression.Expired && Core.Now - aggression.LastCombatTime < TimeSpan.FromSeconds(10)) { return false; }
        }
        foreach (var aggression in player.Aggressed)
        {
            if (!aggression.Expired && Core.Now - aggression.LastCombatTime < TimeSpan.FromSeconds(10)) { return false; }
        }
        return true;
    }
}
