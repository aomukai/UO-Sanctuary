// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class ClassDeath
{
    public static void Configure()
    {
        ClassCombatHooks.PlayerDied = OnDeath;
        ClassCombatHooks.PlayerCorpseCreated = ChargeGold;
    }

    private static void OnDeath(PlayerMobile player, Container corpse)
    {
        GamblingGump.Forget(player.Serial);
        if (!ClassCombat.Enabled(player) || player.Alive || player.Deleted
            || corpse is not Corpse { Deleted: false } body || body.Owner != player || player.Corpse != body)
        {
            return;
        }
        TimedEffects.Clear(player);
        NecromancerEffects.Profiles.Clear(player);
        SorceressEffects.Profiles.Clear(player);
        ChargedStrikeBolts.Cancel(player);
        AmazonJavelins.Cancel(player);
        BladeFuryMissiles.Cancel(player);
        BladeFuryModes.Cancel(player);
        AmazonImpale.Cancel(player);
        AmazonFend.Cancel(player);
        ImpaleSlow.Clear(player);
        PaladinAuras.Clear(player);
        HolyShields.Clear(player);
        AssassinBuffs.Clear(player);
        AssassinCharges.Clear(player);TigerStrikes.Cancel(player);
        CloakShadows.Clear(player);Venoms.Clear(player);
        BarbarianBerserk.Clear(player);
        BarbarianShout.Clear(player);
        BattleOrders.Clear(player);
        BattleCommand.Clear(player);
        SkillHealth.Clear(player);
        TownPortals.Close(player.Serial);
        PotionBelts.OnDeath(player, corpse);
        ClassCombat.Stop(player);
        ClassCombat.For(player).RecoveryUntil = Core.TickCount;
        PersistentMinions.Dismiss(player);
    }

    private static void ChargeGold(PlayerMobile player, Corpse corpse)
    {
        if (!ClassCombat.Enabled(player) || player.Deleted || corpse is not { Deleted: false }
            || corpse.Owner != player || corpse.OwnerWasBaseCreature || player.Corpse != corpse
            || corpse.SanctuaryDeathCharged)
        {
            return;
        }
        // Claim even a zero-gold death before touching stacks; the flag survives world saves.
        corpse.SanctuaryDeathCharged = true;
        using var gold = PooledRefList<Item>.Create();
        long carried = 0;
        if (player.Backpack is { Deleted: false } backpack)
        {
            foreach (var item in backpack.FindItemsByType(typeof(Gold), true))
            {
                gold.Add(item);
                carried += item.Amount;
            }
        }
        long loss = carried / 5;
        long remaining = loss;
        foreach (var item in gold)
        {
            if (remaining == 0)
            {
                break;
            }
            int amount = (int)Math.Min(remaining, item.Amount);
            if (amount == item.Amount)
            {
                item.Delete();
            }
            else
            {
                item.Amount -= amount;
            }
            remaining -= amount;
        }
        if (loss > 0)
        {
            player.SendMessage($"Death cost: {loss} gold. Your bank gold is safe.");
        }
    }
    private static bool CanLift(PlayerMobile player, Corpse corpse, Item item)
    {
        if (item.Deleted || !item.Movable || item.Layer is Layer.Hair or Layer.FacialHair
            || item.Parent != corpse && item.Parent != player.Backpack || !item.VerifyMove(player))
        {
            return false;
        }
        if (item.Parent == player.Backpack)
        {
            return true;
        }
        var reject = LRReason.Inspecific;
        return corpse.IsChildVisibleTo(player, item) && corpse.CanLoot(player, item)
            && item.IsAccessibleTo(player) && corpse.CheckLift(player, item, ref reject)
            && item.CheckLift(player, item, ref reject);
    }
    public static bool Recover(PlayerMobile player, Corpse corpse)
    {
        if (!CorpseEscrow.Available(corpse)) { return false; }
        if (!ClassCombat.Enabled(player) || !player.Alive || player.Deleted || player.Frozen || player.Paralyzed || !AlliedOverlap.Allowed(player)
            || player.Holding != null || player.Backpack == null || corpse is not { Deleted: false, Parent: null }
            || corpse.Owner != player || corpse.OwnerWasBaseCreature || corpse.Map != player.Map
            || !player.InRange(corpse, 2) || Math.Abs(player.Z - corpse.Z) >= 16
            || !player.CanSee(corpse) || !player.InLOS(corpse))
        {
            player.SendMessage("Return to your corpse after resurrection to recover your belongings.");
            return false;
        }
        ClassCombat.Stop(player);
        using var equipment = PooledRefList<Item>.Create();
        if (corpse.RestoreEquip != null)
        {
            foreach (var item in corpse.RestoreEquip)
            {
                if (item.Layer is not (Layer.Backpack or Layer.Bank or Layer.Hair or Layer.FacialHair)
                    && (item.Parent == corpse || item.Parent == player.Backpack))
                {
                    equipment.Add(item);
                }
            }
        }
        var resurrectionRobe = player.FindItemOnLayer(Layer.OuterTorso) as DeathRobe;
        bool restoredEquipment = false;
        // Repeated passes allow recovered stat bonuses to unlock other original equipment.
        for (int pass = 0; pass < equipment.Count; pass++)
        {
            bool changed = false;
            foreach (var item in equipment)
            {
                if (!CanLift(player, corpse, item))
                {
                    continue;
                }
                var current = player.FindItemOnLayer(item.Layer);
                if (current != null && current is not DeathRobe)
                {
                    continue;
                }
                bool fromCorpse = item.Parent == corpse;
                if (fromCorpse && player.TotalWeight + item.TotalWeight + item.PileWeight > player.MaxWeight)
                {
                    continue;
                }
                var robe = current as DeathRobe;
                if (robe != null)
                {
                    player.RemoveItem(robe);
                }
                if (player.EquipItem(item))
                {
                    robe?.Delete();
                    restoredEquipment = true;
                    changed = true;
                    if (fromCorpse)
                    {
                        corpse.OnItemLifted(player, item);
                    }
                }
                else if (robe != null)
                {
                    player.EquipItem(robe);
                }
            }
            if (!changed)
            {
                break;
            }
        }
        if (restoredEquipment && resurrectionRobe?.Parent == player)
        {
            resurrectionRobe.Delete();
        }
        using var contents = PooledRefList<Item>.Create();
        contents.AddRange(corpse.Items);
        foreach (var item in contents)
        {
            if (!CanLift(player, corpse, item)
                || player.TotalWeight + item.TotalWeight + item.PileWeight > player.MaxWeight)
            {
                continue;
            }
            if (player.Backpack.TryDropItem(player, item, false))
            {
                corpse.OnItemLifted(player, item);
            }
        }
        bool empty = !ClassLoot.HasContents(corpse);
        corpse.SelfLooted = empty;
        player.PlaySound(0x3E3);
        player.SendMessage(empty ? "You recover your belongings." : "Some belongings remain in your corpse. Check your carrying capacity.");
        return true;
    }
}
