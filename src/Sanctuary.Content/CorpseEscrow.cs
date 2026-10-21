// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record EscrowItem(Serial Item, Serial Parent, int Amount, long UnitValue);
public sealed record EscrowQuote(CorpseClaim Claim, int Fee, EscrowItem[] Items);

public static class CorpseEscrow
{
    private static readonly HashSet<Serial> _loaded = new();
    private static readonly Dictionary<Serial, TimerExecutionToken> _timers = new();
    public static CorpseEscrowProfiles Profiles { get; private set; }
    public static TimeSpan Lifetime => TimeSpan.FromHours(24);
    public static void Configure()
    {
        Profiles = new();
        CorpseRecoveryHooks.ManagedDecay = Managed;
        CorpseRecoveryHooks.Created = Created;
        CorpseRecoveryHooks.Loaded = corpse => { if (Managed(corpse)) { _loaded.Add(corpse.Serial); } };
        CorpseRecoveryHooks.Deleted = Deleted;
        CorpseRecoveryHooks.Available = Available;
        CorpseRecoveryHooks.Open = (from, banker) => { if (from is PlayerMobile p) { CorpseEscrowGump.DisplayTo(p, banker); } };
        CommandSystem.Register("Recover", AccessLevel.Player, e =>
        {
            if (e.Mobile is not PlayerMobile p || p.Map == null || p.Map == Map.Internal) { return; }
            foreach (var banker in p.Map.GetMobilesInRange<Banker>(p.Location, 3))
            { if (CanUse(p, banker)) { CorpseEscrowGump.DisplayTo(p, banker); return; } }
            p.SendMessage("Visit a nearby banker to recover escrowed belongings.");
        });
    }
    public static bool Managed(Corpse corpse) => corpse is { Owner: PlayerMobile, OwnerWasBaseCreature: false };
    private static void Created(Corpse corpse)
    {
        if (!Managed(corpse)) { return; }
        CorpseEscrowGump.Forget(corpse.Owner.Serial);
        Register(corpse, Core.Now);
    }
    private static void Register(Corpse corpse, DateTime death)
    {
        if (Profiles.Find(corpse.Serial) == null)
        { Profiles.Store(new(corpse.Serial, corpse.Owner.Serial, death, corpse.Location, (corpse.Map ?? Map.Internal).MapID, (Serial)0)); }
        Schedule(Profiles.Find(corpse.Serial));
    }
    [CallPriority(400)]
    public static void Initialize()
    {
        EscrowValuation.Initialize();
        foreach (var serial in _loaded)
        {
            if (World.FindItem(serial) is Corpse corpse && Managed(corpse))
            {
                corpse.BeginDecay(Lifetime);
                Register(corpse, corpse.TimeOfDeath);
            }
        }
        _loaded.Clear();
        foreach (var claim in Profiles.All.ToArray()) { Schedule(claim); }
    }
    private static void Schedule(CorpseClaim claim)
    {
        if (_timers.Remove(claim.Corpse, out var old)) { old.Cancel(); }
        if (claim.Storage.Value != 0) { return; }
        var remaining = claim.Death + Lifetime - Core.Now;
        Timer.StartTimer(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
            () => Expire(claim.Corpse, Core.Now), out var token);
        _timers[claim.Corpse] = token;
    }
    private static void Deleted(Corpse corpse)
    {
        if (_timers.Remove(corpse.Serial, out var timer)) { timer.Cancel(); }
        if (Profiles.Find(corpse.Serial) is { Storage.Value: 0 }) { Profiles.Remove(corpse.Serial); }
    }
    public static bool Available(Corpse corpse)
    {
        if (!Managed(corpse)) { return true; }
        var claim = Profiles.Find(corpse.Serial);
        if (claim == null) { return true; }
        if (Core.Now < claim.Death + Lifetime) { return true; }
        Expire(corpse.Serial, Core.Now); return false;
    }
    public static void Expire(Serial serial, DateTime now)
    {
        var claim = Profiles.Find(serial);
        if (claim == null || claim.Storage.Value != 0 || now < claim.Death + Lifetime) { return; }
        if (_timers.Remove(serial, out var timer)) { timer.Cancel(); }
        if (World.FindItem(serial) is not Corpse corpse || corpse.Deleted)
        { Profiles.Remove(serial); return; }
        if (corpse.Items.Count == 0) { corpse.Delete(); return; }
        var storage = new CorpseEscrowStorage();
        foreach (var item in corpse.Items.ToArray()) { storage.AddItem(item); }
        Profiles.Store(claim with { Storage = storage.Serial });
        corpse.Delete();
        if (World.FindMobile(claim.Owner) is PlayerMobile owner)
        { owner.SendMessage("Your unrecovered corpse belongings are now in escrow. Visit a banker; nothing is forfeited without your confirmation."); }
    }
    public static bool CanUse(PlayerMobile p, Banker banker) => p is { Deleted: false, Alive: true, Criminal: false }
        && banker is { Deleted: false, Alive: true } && p.Map != Map.Internal && p.Map == banker.Map
        && p.InRange(banker, 3) && p.CanSee(banker) && p.InLOS(banker) && ServerFeatureFlags.BankAccess;
    public static CorpseClaim[] Claims(PlayerMobile owner) => Profiles.All
        .Where(c => c.Owner == owner.Serial && c.Storage.Value != 0).OrderBy(c => c.Death).ToArray();
    public static EscrowQuote Quote(PlayerMobile owner, CorpseClaim claim)
    {
        if (claim == null || claim.Owner != owner.Serial || !ReferenceEquals(Profiles.Find(claim.Corpse), claim)
            || World.FindItem(claim.Storage) is not CorpseEscrowStorage { Deleted: false } storage) { return null; }
        var items = new List<EscrowItem>(); long total = 0;
        foreach (var item in storage.FindItems())
        {
            long value = EscrowValuation.Unit(item);
            total = checked(total + checked(value * item.Amount));
            items.Add(new(item.Serial, ((Item)item.Parent).Serial, item.Amount, value));
        }
        if (items.Count == 0 || total / 10 > int.MaxValue) { return null; }
        return new(claim, (int)Math.Max(100, total / 10), items.ToArray());
    }
    public static bool Current(PlayerMobile p, EscrowQuote quote)
    {
        if (quote == null) { return false; }
        var fresh = Quote(p, quote.Claim);
        return fresh != null && fresh.Fee == quote.Fee && fresh.Items.SequenceEqual(quote.Items);
    }
    public static bool Recover(PlayerMobile p, Banker banker, EscrowQuote quote)
    {
        if (!CanUse(p, banker) || !Current(p, quote))
        { p.SendMessage("This recovery quote has changed or the banker is out of reach. Request a fresh quote."); return false; }
        var storage = (CorpseEscrowStorage)World.FindItem(quote.Claim.Storage);
        var bank = p.BankBox;
        int count = 0, weight = 0;
        foreach (var item in storage.Items)
        {
            if (!bank.CheckHold(null, item, false, true, count, weight))
            { p.SendMessage("Your bank cannot hold the entire claim. Nothing was charged or forfeited; make room and retry."); return false; }
            count = checked(count + item.TotalItems + (item.IsVirtualItem ? 0 : 1));
            weight = checked(weight + item.TotalWeight + item.PileWeight);
        }
        if (!Banker.Withdraw(p, quote.Fee))
        { p.SendMessage("Your bank lacks the recovery fee. Your belongings remain safe in escrow."); return false; }
        foreach (var item in storage.Items.ToArray()) { bank.AddItem(item); }
        Profiles.Remove(quote.Claim.Corpse); storage.Delete();
        p.SendMessage($"Belongings recovered to your bank for {quote.Fee} gold."); return true;
    }
    public static bool Forfeit(PlayerMobile p, Banker banker, EscrowQuote quote)
    {
        if (!CanUse(p, banker) || !Current(p, quote)) { return false; }
        var storage = (CorpseEscrowStorage)World.FindItem(quote.Claim.Storage);
        Profiles.Remove(quote.Claim.Corpse); storage.Delete();
        p.SendMessage("You confirmed forfeiture of this claim. Those belongings have been deleted."); return true;
    }
}
