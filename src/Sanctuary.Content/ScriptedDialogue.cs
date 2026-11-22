// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModernUO.Serialization;
using Server;
using Server.Accounting;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Systems.FeatureFlags;

namespace Sanctuary;

public static class ScriptedDialogue
{
    internal sealed record Reply(string Text, byte Next, Action<PlayerMobile, Mobile> Select = null);
    internal sealed record Voice(string Name, ushort Portrait);
    internal sealed record Node(string Line, Reply[] Replies, Voice Speaker = null);
    private sealed record Session(Mobile Speaker, IReadOnlyList<Node> Script, byte Node, uint Token, long Until,
        Action<PlayerMobile, Mobile> Leave);
    private static readonly Dictionary<Serial, Session> _sessions = new();
    private static readonly HashSet<Serial> _ghostBriefed = new();
    private static uint _nextToken;
    private static ArrivalGuard _guard;

    public static void Configure()
    {
        EventSink.Logout += player => _sessions.Remove(player.Serial);
        EventSink.Disconnected += player =>
        {
            _sessions.Remove(player.Serial);
            _ghostBriefed.Remove(player.Serial);
        };
        EventSink.ServerStarted += EnsureArrivalGuard;
    }

    public static unsafe void Initialize()
    {
        IncomingExtendedCommandPackets.RegisterExtended(PortraitProtocol.SubCommand, true, &Receive);
        ClassCombatHooks.PlayerDied += (player, _) => _ghostBriefed.Remove(player.Serial);
        ClassEconomyHooks.ShopListOpened = SendVendorShop;
    }

    private static void EnsureArrivalGuard()
    {
        _guard = World.Mobiles.Values.OfType<ArrivalGuard>().FirstOrDefault(guard => !guard.Deleted);
        if (_guard == null)
        {
            _guard = new ArrivalGuard();
            _guard.MoveToWorld(new Point3D(ClassCreation.Arrival.X, ClassCreation.Arrival.Y - 2, ClassCreation.Arrival.Z), Map.Trammel);
        }
    }

    internal static void TryArrival(PlayerMobile player)
    {
        if (!player.Alive && _guard != null)
        {
            TryGhostGuidance(player, _guard);
            return;
        }
        if (_guard != null && ClassCreation.Profiles.NeedsArrival(player.Serial) && CanTalk(player, _guard))
        {
            Show(player, _guard, _guard.Script, 0);
            ClassCreation.Profiles.Briefed(player.Serial);
        }
    }

    internal static void TryGhostGuidance(PlayerMobile player, ArrivalGuard guard, bool requested = false)
    {
        if (!player.Alive && StatNetwork.SupportsCurrentProtocol(player) && CanTalk(player, guard)
            && (_ghostBriefed.Add(player.Serial) || requested))
            Show(player, guard, ArrivalGuard.GhostGuidance);
    }

    internal static void SendAvatarFace(PlayerMobile player)
    {
        ushort template = ClassCreation.Profiles.AppearanceTemplate(player.Serial);
        int length = template == 0 ? 8 : 10;
        var writer = Packet(length, PortraitProtocol.AvatarFace, stackalloc byte[length]);
        writer.Write(ClassCreation.Profiles.Face(player.Serial));
        if (template != 0)
            writer.Write(template);
        player.NetState?.Send(writer.Span);
        var capability = Packet(7, PortraitProtocol.InteractionEnabled, stackalloc byte[7]);
        player.NetState?.Send(capability.Span);
        SendStoryState(player);
    }

    internal static void SendStoryState(PlayerMobile player)
    {
        var profiles = ClassCreation.Profiles;
        var writer = Packet(12, PortraitProtocol.StoryState, stackalloc byte[12]);
        writer.Write(profiles.HasMetIolo(player.Serial));
        writer.Write(MercenaryCare.ActiveStoryCompanion(player)?.Serial ?? Serial.Zero);
        player.NetState?.Send(writer.Span);
        var flags = profiles.Discoveries(player.Serial);
        var legacy = Packet(8, PortraitProtocol.BritainDiscoveries, stackalloc byte[8]);
        legacy.Write((byte)flags);
        player.NetState?.Send(legacy.Span);
        // Older clients read the two-byte snapshot. The four-byte snapshot carries newer journal entries.
        var discoveries = Packet(9, PortraitProtocol.WorldDiscoveries, stackalloc byte[9]);
        discoveries.Write((ushort)flags);
        player.NetState?.Send(discoveries.Span);
        var expanded = Packet(11, PortraitProtocol.WorldDiscoveries, stackalloc byte[11]);
        expanded.Write((uint)flags);
        player.NetState?.Send(expanded.Span);
    }

    internal static void RecordDiscovery(PlayerMobile player, StoryDiscoveries flag)
    {
        if (!ClassCreation.Profiles.Discover(player.Serial, flag))
            return;
        player.SendMessage("A new entry has been added to thy journal.");
        SendStoryState(player);
    }

    internal static void Open(PlayerMobile player, Mobile speaker)
    {
        if (speaker is IScriptedSpeaker dialogue && StatNetwork.SupportsCurrentProtocol(player) && CanTalk(player, speaker))
        {
            dialogue.BeginConversation(player);
            if (speaker is ArrivalGuard && player.Alive)
                ClassCreation.Profiles.Briefed(player.Serial);
        }
    }

    internal static bool CanTalk(PlayerMobile player, Mobile speaker) =>
        player.NetState?.Running == true && (player.Alive || speaker is ArrivalGuard) && !speaker.Deleted && speaker.Alive
        && player.Map == speaker.Map && player.InRange(speaker, PortraitProtocol.NpcInteractionRange)
        && (speaker is IScriptedSpeaker dialogue && dialogue.CanTalkTo(player)
            || speaker is Banker banker && CorpseEscrow.CanUse(player, banker))
        && player.CanSee(speaker) && player.InLOS(speaker);

    private static void Receive(NetState state, SpanReader reader)
    {
        int length = reader.Remaining + 5;
        if (state.Mobile is not PlayerMobile player
            || length is not (PortraitProtocol.ReplyPacketLength or PortraitProtocol.InteractPacketLength
                or PortraitProtocol.ShopTabPacketLength)
            || reader.ReadByte() != PortraitProtocol.Version)
            return;

        if (length == PortraitProtocol.InteractPacketLength)
        {
            Interact(state, player, reader);
            return;
        }

        if (length == PortraitProtocol.ShopTabPacketLength)
        {
            if (reader.ReadByte() == PortraitProtocol.VendorShop)
            {
                var tab = (VendorShopTabs)reader.ReadByte();
                if (tab is VendorShopTabs.Buy or VendorShopTabs.Sell)
                    Interact(state, player, reader, tab);
            }
            return;
        }

        uint token = reader.ReadUInt32();
        byte choice = reader.ReadByte();
        if (!_sessions.TryGetValue(player.Serial, out var session) || session.Token != token)
            return;

        if (choice == PortraitProtocol.Leave)
        {
            _sessions.Remove(player.Serial);
            session.Leave?.Invoke(player, session.Speaker);
            return;
        }

        var replies = session.Script[session.Node].Replies;
        if (Core.TickCount >= session.Until || !CanTalk(player, session.Speaker))
        {
            Close(player, token);
        }
        else if (choice < replies.Length)
        {
            var reply = replies[choice];
            if (reply.Select is { } select)
            {
                Close(player, token);
                select(player, session.Speaker);
            }
            else
                Show(player, session.Speaker, session.Script, reply.Next, session.Leave);
        }
    }

    private static void Interact(NetState state, PlayerMobile player, SpanReader reader,
        VendorShopTabs tab = VendorShopTabs.None)
    {
        var targetReader = reader;
        var npc = World.FindMobile((Serial)reader.ReadUInt32());
        if (!StatNetwork.SupportsCurrentProtocol(player) || npc?.Deleted != false
            || player.Map != npc.Map || !player.CanSee(npc))
            return;

        if (tab != VendorShopTabs.None && (npc is not BaseVendor requestedVendor || (AvailableShopTabs(requestedVendor) & tab) == 0))
            return;

        if (npc is not (IScriptedSpeaker or NewHavenHealer or Banker
            or AnimalTrainer or BaseHealer or BaseVendor { IsActiveSeller: true } or BaseVendor { IsActiveBuyer: true }))
        {
            ContextMenuSystem.ContextMenuRequest(state, targetReader);
            return;
        }

        if ((!player.Alive && npc is not (ArrivalGuard or NewHavenHealer)) || !npc.Alive || Core.TickCount < player.NextActionTime)
            return;
        if (!player.InRange(npc, PortraitProtocol.NpcInteractionRange) || !player.InLOS(npc))
        {
            player.SendMessage("You need to be closer to interact.");
            return;
        }
        if (!player.Region.OnDoubleClick(player, npc))
            return;

        player.NextActionTime = Core.TickCount + Mobile.ActionDelay;
        switch (npc)
        {
            case IScriptedSpeaker speaker when speaker.CanTalkTo(player):
                Open(player, npc);
                break;
            case NewHavenHealer healer:
                MountTravel.ResurrectNearHealer(player, healer);
                healer.OnDoubleClick(player);
                break;
            case Banker banker:
                if (CorpseEscrow.CanUse(player, banker) && CorpseEscrow.Claims(player).Length > 0)
                    CorpseEscrowGump.DisplayTo(player, banker);
                else
                    OpenBank(player);
                break;
            case AnimalTrainer stablehand:
                MountTravel.StableInteraction(player, stablehand);
                break;
            case BaseHealer healer:
                if (!MountTravel.ResurrectNearHealer(player, healer))
                    healer.OnDoubleClick(player);
                break;
            case BaseVendor vendor:
                if (tab == VendorShopTabs.Sell || tab == VendorShopTabs.None && !vendor.IsActiveSeller)
                    vendor.VendorSell(player);
                else
                    vendor.VendorBuy(player);
                break;
        }
    }

    internal static void OpenBank(PlayerMobile player)
    {
        if (player.Criminal)
            player.SendLocalizedMessage(500378);
        else if (player.BankBox is { } bank)
        {
            bank.Open();
            if (bank.Opened && AccountGold.Enabled && player.Account is { } account)
                player.SendMessage($"Shared bank balance: {account.GetTotalGold():N0} gold.");
        }
    }

    private static void Close(PlayerMobile player, uint token)
    {
        _sessions.Remove(player.Serial);
        var writer = Packet(11, PortraitProtocol.Closed, stackalloc byte[11]);
        writer.Write(token);
        player.NetState?.Send(writer.Span);
    }

    internal static void CloseForSpeaker(PlayerMobile player, Serial speaker)
    {
        if (_sessions.TryGetValue(player.Serial, out var session) && session.Speaker.Serial == speaker)
            Close(player, session.Token);
    }

    internal static void Show(PlayerMobile player, Mobile speaker, IReadOnlyList<Node> script, byte node = 0,
        Action<PlayerMobile, Mobile> onLeave = null)
    {
        var page = script[node];
        var talkative = speaker as IScriptedSpeaker;
        ushort portrait = page.Speaker?.Portrait ?? talkative?.Portrait
            ?? throw new InvalidOperationException($"A dialogue portrait is required for {speaker.Name}.");
        bool extendedPortrait = portrait > byte.MaxValue;
        uint token = ++_nextToken;
        byte[][] texts = [Encoding.UTF8.GetBytes(page.Speaker?.Name ?? talkative?.DialogueName ?? speaker.Name), Encoding.UTF8.GetBytes(page.Line),
            .. page.Replies.Select(reply => Encoding.UTF8.GetBytes(reply.Text))];
        int length = (extendedPortrait ? 14 : 13) + texts.Sum(text => 2 + text.Length);
        var writer = Packet(length, extendedPortrait ? PortraitProtocol.ExtendedConversation : PortraitProtocol.Conversation,
            stackalloc byte[length]);
        writer.Write(token);
        if (extendedPortrait)
            writer.Write(portrait);
        else
            writer.Write((byte)portrait);
        writer.Write((byte)page.Replies.Length);
        foreach (byte[] text in texts)
        {
            writer.Write((ushort)text.Length);
            writer.Write(text.AsSpan());
        }
        _sessions[player.Serial] = new Session(speaker, script, node, token, Core.TickCount + 120_000, onLeave);
        player.NetState.Send(writer.Span);
    }

    internal static void SendIdentifyShop(PlayerMobile player, Serial scholar)
    {
        var writer = Packet(11, PortraitProtocol.IdentifyShop, stackalloc byte[11]);
        writer.Write(scholar);
        player.NetState.Send(writer.Span);
    }

    private static VendorShopTabs AvailableShopTabs(BaseVendor vendor)
    {
        if (vendor is IScriptedSpeaker or Banker)
            return VendorShopTabs.None;

        var tabs = VendorShopTabs.None;
        if (vendor.IsActiveSeller && ContentFeatureFlags.VendorPurchase)
            tabs |= VendorShopTabs.Buy;
        if (vendor.IsActiveBuyer && ContentFeatureFlags.VendorSell)
            tabs |= VendorShopTabs.Sell;
        return tabs;
    }

    private static bool SendVendorShop(BaseVendor vendor, Mobile customer, bool buying)
    {
        if (customer is not PlayerMobile { NetState: { } connection } player || !StatNetwork.SupportsCurrentProtocol(player))
            return false;

        var tab = buying ? VendorShopTabs.Buy : VendorShopTabs.Sell;
        var tabs = AvailableShopTabs(vendor);
        if ((tabs & tab) == 0)
            return false;

        string name = string.IsNullOrWhiteSpace(vendor.Name) ? "Shop" : vendor.Name;
        if (!string.IsNullOrWhiteSpace(vendor.Title))
            name += " " + vendor.Title;
        byte[] title = Encoding.UTF8.GetBytes(name);
        if (title.Length > PortraitProtocol.MaxTextBytes)
            title = "Shop"u8.ToArray();

        int length = 15 + title.Length;
        var writer = Packet(length, PortraitProtocol.VendorShop, stackalloc byte[length]);
        writer.Write(vendor.Serial);
        writer.Write((byte)tab);
        writer.Write((byte)tabs);
        writer.Write((ushort)title.Length);
        writer.Write(title.AsSpan());
        connection.Send(writer.Span);
        return true;
    }

    private static SpanWriter Packet(int length, byte kind, Span<byte> buffer)
    {
        var writer = new SpanWriter(buffer);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write(PortraitProtocol.SubCommand);
        writer.Write(PortraitProtocol.Version);
        writer.Write(kind);
        return writer;
    }
}

internal interface IScriptedSpeaker
{
    ushort Portrait { get; }
    string DialogueName => null;
    bool CanTalkTo(PlayerMobile player) => true;
    void BeginConversation(PlayerMobile player);
}

public abstract class ConversationNpc : BaseCreature, IScriptedSpeaker
{
    protected ConversationNpc() : base(AIType.AI_Vendor, FightMode.None, 10, 1) { }
    protected ConversationNpc(Serial serial) : base(serial) { }
    public override bool IsInvulnerable => true;
    public override bool CanTeach => false;
    public override DeathMoveResult GetParentMoveResultFor(Item item) =>
        MercenaryCare.CompanionLevel(this).HasValue ? DeathMoveResult.RemainEquipped : base.GetParentMoveResultFor(item);
    internal abstract ushort Portrait { get; }
    internal abstract IReadOnlyList<ScriptedDialogue.Node> Script { get; }
    internal virtual bool CanTalkTo(PlayerMobile player) => true;
    internal virtual void BeginConversation(PlayerMobile player) => ScriptedDialogue.Show(player, this, Script);
    ushort IScriptedSpeaker.Portrait => Portrait;
    bool IScriptedSpeaker.CanTalkTo(PlayerMobile player) => CanTalkTo(player);
    void IScriptedSpeaker.BeginConversation(PlayerMobile player) => BeginConversation(player);

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player)
            ScriptedDialogue.Open(player, this);
    }
}

[SerializationGenerator(0)]
public partial class ArrivalGuard : ConversationNpc
{
    internal static readonly ScriptedDialogue.Node[] GhostGuidance =
    [
        new("Thy spirit still walks, traveller. Go northwest, past the bank, to the healer’s house. Look for its sign at the steps. The healer there can restore thee to life.", [])
    ];
    private static readonly ScriptedDialogue.Reply[] _topics =
        [new("Iolo", 1), new("New Haven", 2), new("Bountiful Harvest Inn", 3)];

    private static readonly ScriptedDialogue.Node[] _script =
    [
        new("Thou must be the one that old man Iolo was talking about. Welcome to New Haven, traveler. He awaits thee at the Bountiful Harvest Inn.",
            _topics),
        new("Only that he'd sent for a friend from far away. He'd scarce sat down before he asked whether thou hadst come. Best ask him the rest thyself.",
            _topics),
        new("Ask the smith or tinker in the market to teach thee a trade. The lumberjack sells hatchets, and the miner sells picks. The ruins of Old Haven lie east, the farm beyond them to the south. Look for the mine on the road toward the ruins.",
            _topics),
        new("Follow the road north from here. Look for the sign of the Bountiful Harvest. Iolo said he'd wait for thee within.",
            _topics)
    ];

    [Constructible]
    public ArrivalGuard()
    {
        Name = "Elin";
        Title = "the New Haven guard";
        Body = 0x191;
        Female = true;
        Hue = 0x83EF;
        HairItemID = 0x203B;
        HairHue = 0x0455;
        CantWalk = true;
        AddItem(new BronzeShield());
        AddItem(new Bascinet());
        AddItem(new RingmailChest());
        AddItem(new Boots());
    }

    internal override ushort Portrait => (ushort)NpcPortrait.Elin;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _script;

    internal override void BeginConversation(PlayerMobile player)
    {
        if (player.Alive)
            base.BeginConversation(player);
        else
            ScriptedDialogue.TryGhostGuidance(player, this, requested: true);
    }

    public override void OnMovement(Mobile mobile, Point3D oldLocation)
    {
        base.OnMovement(mobile, oldLocation);
        if (mobile is PlayerMobile { Alive: false } ghost && InRange(ghost, PortraitProtocol.NpcInteractionRange))
            ScriptedDialogue.TryGhostGuidance(ghost, this);
    }
}
