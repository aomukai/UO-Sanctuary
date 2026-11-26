// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using ModernUO.Serialization;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class AuthoredWriting
{
    private static readonly Dictionary<Serial, NetState> _readers = new();

    public static void Configure()
    {
        EventSink.Disconnected += player => _readers.Remove(player.Serial);
        EventSink.Logout += player => _readers.Remove(player.Serial);
    }

    internal static void Enable(PlayerMobile player, NetState connection) => _readers[player.Serial] = connection;
    internal static bool Supported(PlayerMobile player) =>
        player.NetState is { Running: true } connection && _readers.GetValueOrDefault(player.Serial) == connection;

    internal static bool CanRead(PlayerMobile player, Item item) =>
        player is { Alive: true, NetState: { Running: true } } && !item.Deleted && player.CanSee(item)
        && (item.IsChildOf(player.Backpack) || item.Parent == null && item.Map == player.Map
            && player.InRange(item, 2) && Math.Abs(player.Z - item.Z) < 16 && player.InLOS(item));

    // Readable entries use id.title, id.author, and id.body in locale catalogs.
    // The optional audio ID is independent of the document's display keys.
    public static bool Show(PlayerMobile player, string id, AuthoredTextKind kind, string title, string author,
        string body, string narrationId = null)
    {
        if (!Supported(player) || !AuthoredTextProtocol.ValidId(id) || id.Length > AuthoredTextProtocol.MaxIdBytes - 7
            || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body)
            || Encoding.UTF8.GetByteCount(title) > PortraitProtocol.MaxTextBytes
            || Encoding.UTF8.GetByteCount(author ?? "") > PortraitProtocol.MaxTextBytes
            || kind is not (AuthoredTextKind.Letter or AuthoredTextKind.Book or AuthoredTextKind.Sign)
            || narrationId != null && !AuthoredTextProtocol.ValidId(narrationId))
            return false;
        return Send(player, AuthoredTextProtocol.Reading, kind, SpeechPresentation.None,
            id, narrationId ?? "", title, author, body);
    }

    public static bool Observe(PlayerMobile player, string id, string text,
        SpeechPresentation presentation = SpeechPresentation.AvatarOverhead)
    {
        if (!AuthoredTextProtocol.ValidId(id) || string.IsNullOrWhiteSpace(text)
            || presentation is not (SpeechPresentation.None or SpeechPresentation.AvatarOverhead)
            || Encoding.UTF8.GetByteCount(text) > PortraitProtocol.MaxTextBytes)
            return false;
        if (Supported(player))
            return Send(player, AuthoredTextProtocol.Observation, AuthoredTextKind.Observation, presentation, id, text);
        if (presentation == SpeechPresentation.AvatarOverhead)
            player.LocalOverheadMessage(MessageType.Regular, 0x0035, false, text);
        return false;
    }

    public static void Bark(Mobile speaker, string id, string speakerId, string text, ushort hue = 0x03B2,
        AuthoredTextKind kind = AuthoredTextKind.CombatBark)
    {
        if (speaker is not { Deleted: false, Map: { } map } || map == Map.Internal
            || !AuthoredTextProtocol.ValidId(id) || !AuthoredTextProtocol.ValidId(speakerId)
            || kind is not (AuthoredTextKind.CombatBark or AuthoredTextKind.Companion) || string.IsNullOrWhiteSpace(text))
            return;
        byte[][] fields = [Encoding.UTF8.GetBytes(id), Encoding.UTF8.GetBytes(speakerId), Encoding.UTF8.GetBytes(text)];
        if (fields[2].Length > PortraitProtocol.MaxTextBytes)
            return;
        Span<byte> packet = stackalloc byte[14 + fields.Sum(field => 2 + field.Length)];
        var writer = StartPacket(packet, AuthoredTextProtocol.Bark);
        writer.Write(speaker.Serial);
        writer.Write(hue);
        writer.Write((byte)kind);
        WriteFields(ref writer, fields);
        foreach (var connection in map.GetClientsInRange(speaker.Location))
        {
            if (connection.Mobile is not PlayerMobile player || !player.CanSee(speaker))
                continue;
            if (Supported(player))
                connection.Send(writer.Span);
            else
                speaker.PrivateOverheadMessage(MessageType.Regular, hue, false, text, connection);
        }
    }

    internal static string BookBody(BaseBook book)
    {
        return string.Join("\n\n", book.Pages.SelectMany(page =>
            string.Join("\n", page.Lines).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            .Select(paragraph => paragraph.Trim().Replace('\n', ' ')));
    }

    private static bool Send(PlayerMobile player, byte packetKind, AuthoredTextKind kind,
        SpeechPresentation presentation, params string[] fields)
    {
        byte[][] encoded = fields.Select(value => Encoding.UTF8.GetBytes(value ?? "")).ToArray();
        if (encoded.Any(field => field.Length > AuthoredTextProtocol.MaxBodyBytes))
            return false;
        int length = 9 + encoded.Sum(field => 2 + field.Length);
        Span<byte> packet = new byte[length];
        var writer = StartPacket(packet, packetKind);
        writer.Write((byte)kind);
        writer.Write((byte)presentation);
        WriteFields(ref writer, encoded);
        player.NetState.Send(writer.Span);
        return true;
    }

    internal static SpanWriter StartPacket(Span<byte> packet, byte kind)
    {
        var writer = new SpanWriter(packet);
        writer.Write((byte)0xBF);
        writer.Write((ushort)packet.Length);
        writer.Write(PortraitProtocol.SubCommand);
        writer.Write(PortraitProtocol.Version);
        writer.Write(kind);
        return writer;
    }

    internal static void WriteFields(ref SpanWriter writer, byte[][] fields)
    {
        foreach (var field in fields)
        {
            writer.Write((ushort)field.Length);
            writer.Write(field.AsSpan());
        }
    }
}

[SerializationGenerator(0)]
public partial class AuthoredReadable : Item
{
    [SerializableField(0, setter: "private")] private string _textId;
    [SerializableField(1, setter: "private")] private AuthoredTextKind _textKind;
    [SerializableField(2, setter: "private")] private string _author;
    [SerializableField(3, setter: "private")] private string _body;
    [SerializableField(4, setter: "private")] private string _narrationId;

    [Constructible]
    public AuthoredReadable(string id, string title, string text)
        : this(0x0BD2, "sign", id, title, "", text, "") { }

    [Constructible]
    public AuthoredReadable(int graphic, string kind, string id, string title, string author, string text, string narrationId)
        : base(graphic)
    {
        if (!AuthoredTextProtocol.ValidId(id) || id.Length > AuthoredTextProtocol.MaxIdBytes - 7
            || !Enum.TryParse<AuthoredTextKind>(kind, true, out var type)
            || type is not (AuthoredTextKind.Letter or AuthoredTextKind.Book or AuthoredTextKind.Sign)
            || !string.IsNullOrEmpty(narrationId) && !AuthoredTextProtocol.ValidId(narrationId)
            || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text)
            || Encoding.UTF8.GetByteCount(title) > PortraitProtocol.MaxTextBytes
            || Encoding.UTF8.GetByteCount(author ?? "") > PortraitProtocol.MaxTextBytes
            || Encoding.UTF8.GetByteCount(text) > AuthoredTextProtocol.MaxBodyBytes)
            throw new ArgumentException("Supply a stable text ID, a valid readable type, and its English title and text.");

        TextId = id;
        TextKind = type;
        Author = author;
        Body = text.Replace("\\n", "\n", StringComparison.Ordinal);
        NarrationId = string.IsNullOrEmpty(narrationId) ? null : narrationId;
        Name = title;
        Movable = type != AuthoredTextKind.Sign;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && AuthoredWriting.CanRead(player, this)
            && !AuthoredWriting.Show(player, _textId, _textKind, Name, _author, _body, _narrationId))
            player.SendGump(new ReadingFallback(Name, _author, _body));
    }

    private sealed class ReadingFallback(string title, string author, string body) : DynamicGump(65, 65)
    {
        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 440, 350, 2600);
            builder.AddHtml(30, 20, 380, 44, WebUtility.HtmlEncode(title)
                + (string.IsNullOrEmpty(author) ? "" : "<br>" + WebUtility.HtmlEncode(author)));
            builder.AddHtml(30, 68, 380, 240, WebUtility.HtmlEncode(body).Replace("\n", "<br>"),
                background: true, scrollbar: true);
        }
    }
}
