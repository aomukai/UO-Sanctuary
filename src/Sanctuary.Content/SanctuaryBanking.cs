// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class SanctuaryBanking
{
    public static void Configure() => EventSink.Speech += IssueCheck;

    internal static bool CanPay(PlayerMobile owner, int price) =>
        owner.Backpack is { } pack && Banker.GetBalance(owner) >= price - Math.Clamp(pack.GetAmount(typeof(Gold)), 0, price);

    internal static bool TryPay(PlayerMobile owner, int price)
    {
        if (owner.Backpack is not { } pack)
            return false;

        int carried = Math.Clamp(pack.GetAmount(typeof(Gold)), 0, price);
        int fromBank = price - carried;
        if (fromBank > 0 && !Banker.Withdraw(owner, fromBank))
            return false;
        if (carried == 0 || pack.ConsumeTotal(typeof(Gold), carried))
            return true;
        if (fromBank > 0)
            Banker.Deposit(owner, fromBank);
        return false;
    }

    private static void IssueCheck(SpeechEventArgs speech)
    {
        if (!AccountGold.Enabled || speech.Blocked || speech.Handled || speech.Type != MessageType.Regular
            || speech.Mobile is not PlayerMobile { Alive: true, Deleted: false, Map: { } map, Account: { } account } player
            || map == Map.Internal)
            return;

        var text = speech.Speech.AsSpan().Trim();
        int space = text.IndexOfAny(' ', '\t');
        if (space < 0 || !text[..space].Equals("check", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(text[(space + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
            || amount <= 0)
            return;

        foreach (var banker in map.GetMobilesInRange<Banker>(player.Location, PortraitProtocol.NpcInteractionRange))
        {
            if (banker is not { Deleted: false, Alive: true } || !player.CanSee(banker) || !player.InLOS(banker))
                continue;

            speech.Handled = speech.Blocked = true;
            if (!ServerFeatureFlags.BankAccess || player.Criminal)
            {
                player.SendMessage("You cannot access your bank account right now.");
                return;
            }
            if (account.GetTotalGold() < amount)
            {
                player.SendMessage("You do not have that much gold in your bank account.");
                return;
            }

            var check = new BankCheck(amount);
            if (player.Backpack?.TryDropItem(player, check, false) != true)
            {
                check.Delete();
                player.SendMessage("Your backpack has no room for the bank check.");
                return;
            }
            if (!account.WithdrawGold(amount))
            {
                check.Delete();
                return;
            }

            player.SendMessage($"A bank check for {amount:N0} gold has been placed in your backpack.");
            return;
        }
    }
}
