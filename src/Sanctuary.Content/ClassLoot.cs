using System;
using System.Buffers;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class ClassLoot
{
    private sealed class Session
    {
        public bool Open;
        public Serial Anchor;
        public long NextPoll = Core.TickCount;
        public long NextTake = Core.TickCount;
    }
    private static readonly Dictionary<Serial, Session> _sessions = new();
    public static void Configure()
    {
        ClassLootHooks.SuppressDrops = creature => creature is not BaseVendor && !creature.Controlled && !creature.Summoned;
        ClassLootHooks.CreatureDied = (creature, corpse) =>
        {
            if (ClassLootHooks.SuppressDrops(creature))
            {
                using var items = Server.Collections.PooledRefList<Item>.Create();
                foreach (var item in corpse.Items)
                {
                    items.Add(item);
                }
                foreach (var item in items)
                {
                    item.Delete();
                }
            }
            AwardGold(creature, corpse);
            if (creature is GraveyardUndead { Raised: false, NoKillAwards: false } undead
                && Killer(creature) is { Deleted: false, Alive: true } player && ClassCombat.Enabled(player)
                && corpse is Corpse body && body.Map == player.Map && player.InRange(body, LootProtocol.SightRange))
            {
                int xp = undead.BalanceLevel?.Experience ?? undead.NormalStats?.Experience ?? 200;
                ClassCreation.Profiles.AwardExperience(player,
                    MonsterExperience.Award(xp, undead.CombatProfile.Level, ClassStats.Level(player)));
            }
        };
        ClassLootHooks.Carved = (from, corpse) =>
        {
            if (from is PlayerMobile player && ClassCombat.Enabled(player) && corpse.OwnerWasBaseCreature)
            {
                corpse.SanctuaryHarvested = true;
                RemoveHarvestedIfEmpty(corpse);
            }
        };
        ClassLootHooks.ContentsChanged = corpse => Timer.DelayCall(() => RemoveHarvestedIfEmpty(corpse));
        EventSink.Disconnected += from => _sessions.Remove(from.Serial);
        EventSink.Logout += from => _sessions.Remove(from.Serial);
    }
    public static unsafe void Initialize() => IncomingExtendedCommandPackets.RegisterExtended(LootProtocol.SubCommand, true, &Receive);
    public static bool IsOpen(PlayerMobile from) => _sessions.TryGetValue(from.Serial, out var session) && session.Open;
    public static bool HasContents(Corpse corpse)
    {
        foreach (var item in corpse.Items)
        {
            if (!item.Deleted && item.Layer is not (Layer.Hair or Layer.FacialHair))
            {
                return true;
            }
        }
        return false;
    }
    public static bool CanOpen(PlayerMobile from, Corpse corpse, int range = LootProtocol.Range) =>
        ClassCombat.Enabled(from) && from.Alive && !from.Deleted && corpse is { Deleted: false, OwnerWasBaseCreature: true }
        && corpse.Parent == null && corpse.Map == from.Map && from.InRange(corpse, range)
        && Math.Abs(from.Z - corpse.Z) < 16 && from.CanSee(corpse) && from.InLOS(corpse)
        && corpse.CanLoot(from, null) && !corpse.IsCriminalAction(from);
    public static bool CanTake(PlayerMobile from, Corpse corpse, Item item) =>
        CanOpen(from, corpse) && item is { Deleted: false, Movable: true } && item.Parent == corpse
        && item.Layer is not (Layer.Hair or Layer.FacialHair)
        && corpse.IsChildVisibleTo(from, item) && corpse.CanLoot(from, item) && from.CanSee(item)
        && item.VerifyMove(from) && item.IsAccessibleTo(from);
    public static bool Take(PlayerMobile from, Item item)
    {
        if (item?.Parent is not Corpse corpse || !CanTake(from, corpse, item) || from.Backpack == null
            || from.Frozen || from.Paralyzed || from.Holding != null)
        {
            return false;
        }
        var reject = LRReason.Inspecific;
        if (!corpse.CheckLift(from, item, ref reject) || !item.CheckLift(from, item, ref reject))
        {
            return false;
        }
        if (from.TotalWeight + item.TotalWeight + item.PileWeight > from.MaxWeight)
        {
            from.SendMessage("You cannot carry that much weight.");
            return false;
        }
        if (!from.OnDragLift(item) || !item.OnDragLift(from) || !from.Alive || item.Deleted || item.Parent != corpse)
        {
            return false;
        }
        if (!from.Backpack.TryDropItem(from, item, true))
        {
            return false;
        }
        corpse.OnItemLifted(from, item);
        from.RevealingAction();
        from.PlaySound(0x57);
        RemoveHarvestedIfEmpty(corpse);
        return true;
    }
    public static void RemoveHarvestedIfEmpty(Corpse corpse)
    {
        if (!corpse.Deleted && (corpse.SanctuaryHarvested || corpse.SanctuaryConsumed) && !HasContents(corpse))
        {
            corpse.Delete();
        }
    }
    public static PlayerMobile Killer(BaseCreature creature) => creature.LastKiller as PlayerMobile
        ?? (creature.LastKiller as BaseCreature)?.GetMaster() as PlayerMobile;
    public static void AwardGold(BaseCreature creature, Container container)
    {
        if (container is not Corpse corpse || creature.Summoned || creature.Controlled || creature.NoKillAwards
            || creature is BaseVendor || Killer(creature) is not { } player || !CanOpen(player, corpse, LootProtocol.SightRange))
        {
            return;
        }
        var amount = 0;
        // A failed deposit leaves the original stack in the corpse.
        using var gold = Server.Collections.PooledRefList<Gold>.Create();
        foreach (var item in corpse.Items)
        {
            if (item is Gold stack && CanTakeGold(player, corpse, stack))
            {
                gold.Add(stack);
            }
        }
        foreach (var stack in gold)
        {
            var count = stack.Amount;
            if (player.TotalWeight + stack.PileWeight <= player.MaxWeight && player.Backpack?.TryDropItem(player, stack, true) == true)
            {
                corpse.OnItemLifted(player, stack);
                amount += count;
            }
        }
        if (amount > 0)
        {
            player.SendMessage($"Gold +{amount}");
        }
    }
    private static bool CanTakeGold(PlayerMobile from, Corpse corpse, Gold item) =>
        item.Movable && corpse.IsChildVisibleTo(from, item) && corpse.CanLoot(from, item);
    private static void Receive(NetState state, SpanReader reader)
    {
        if (state.Mobile is not PlayerMobile from || !ClassCombat.Enabled(from) || reader.Remaining != 6
            || reader.ReadByte() != LootProtocol.Version)
        {
            return;
        }
        var action = (LootRequest)reader.ReadByte();
        var serial = (Serial)reader.ReadUInt32();
        if (action is < LootRequest.Poll or > LootRequest.Close)
        {
            return;
        }
        if (!_sessions.TryGetValue(from.Serial, out var session))
        {
            _sessions[from.Serial] = session = new Session();
        }
        switch (action)
        {
            case LootRequest.Open:
                var corpse = World.FindItem(serial) as Corpse;
                if (corpse is { OwnerWasBaseCreature: false })
                {
                    session.Open = false;
                    if (corpse.Owner == from)
                    {
                        ClassDeath.Recover(from, corpse);
                        SendState(from, session);
                        return;
                    }
                    corpse.OnDoubleClick(from);
                    return;
                }
                if (!CanOpen(from, corpse))
                {
                    from.SendMessage("You cannot loot that corpse from here.");
                    break;
                }
                ClassCombat.Stop(from);
                session.Open = true;
                session.Anchor = serial;
                break;
            case LootRequest.Take:
                if (session.Open && Core.TickCount - session.NextTake >= 0)
                {
                    session.NextTake = Core.TickCount + 100;
                    if (!Take(from, World.FindItem(serial)))
                    {
                        from.SendMessage("That item could not be taken. Check your carrying capacity and distance.");
                    }
                }
                break;
            case LootRequest.Close:
                session.Open = false;
                break;
            case LootRequest.Poll:
                if (Core.TickCount - session.NextPoll < 0)
                {
                    return;
                }
                session.NextPoll = Core.TickCount + LootProtocol.PollMs;
                break;
        }
        SendState(from, session);
    }
    private static void SendState(PlayerMobile from, Session session)
    {
        if (!from.Alive)
        {
            session.Open = false;
        }
        Span<uint> corpses = stackalloc uint[LootProtocol.MaxCorpses];
        Span<byte> empty = stackalloc byte[LootProtocol.MaxCorpses];
        Span<uint> items = stackalloc uint[LootProtocol.MaxItems];
        var count = 0;
        var itemCount = 0;
        foreach (var corpse in from.Map.GetItemsInRange<Corpse>(from.Location, LootProtocol.SightRange))
        {
            if (count == corpses.Length || !CanOpen(from, corpse, LootProtocol.SightRange))
            {
                continue;
            }
            corpses[count] = corpse.Serial.Value;
            empty[count++] = (byte)(HasContents(corpse) ? 0 : 1);
            if (!session.Open || !CanOpen(from, corpse))
            {
                continue;
            }
            corpse.ProcessOpeners(from);
            corpse.SendContentTo(from.NetState);
            foreach (var item in corpse.Items)
            {
                if (itemCount < items.Length && CanTake(from, corpse, item))
                {
                    items[itemCount++] = item.Serial.Value;
                    from.NetState.SendOPLInfo(item);
                }
            }
        }
        var length = 14 + count * 5 + itemCount * 4;
        Span<byte> data = stackalloc byte[length];
        var writer = new SpanWriter(data);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write(LootProtocol.SubCommand);
        writer.Write(LootProtocol.Version);
        writer.Write((byte)(session.Open ? 1 : 0));
        writer.Write(session.Anchor.Value);
        writer.Write((byte)count);
        writer.Write((ushort)itemCount);
        for (var i = 0; i < count; i++)
        {
            writer.Write(corpses[i]);
            writer.Write(empty[i]);
        }
        for (var i = 0; i < itemCount; i++)
        {
            writer.Write(items[i]);
        }
        from.NetState.Send(data);
    }
}
