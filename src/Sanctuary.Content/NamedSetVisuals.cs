// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class NamedSetVisuals
{
    private static readonly HashSet<Serial> _owners = new();
    private static readonly HashSet<Serial> _faded = new();
    private static readonly List<Serial> _keys = new();
    public static void Configure()
    {
        EventSink.Connected += owner => { if (owner is PlayerMobile player) { Track(player); } };

    }
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), Pulse);
    public static bool Faded(PlayerMobile owner)
    {
        if (owner is not { Alive: true, Deleted: false } || !ClassCombat.Enabled(owner)) { return false; }
        return ClassGear.Bonus(owner, "fade") > 0 || AssassinBuffs.Faded(owner);
    }
    public static byte Opacity(PlayerMobile owner) => Faded(owner) ? NamedVisualProtocol.Faded : NamedVisualProtocol.Opaque;
    public static bool Active(PlayerMobile owner)
    {
        if (owner is not { Alive: true, Deleted: false } || !ClassCombat.Enabled(owner)) { return false; }
        foreach (var value in NamedGear.ActiveProperties(owner))
        {
            if (value.Property.Code == "state" && value.Property.Parameter?.ToString() == "fullsetgeneric") { return true; }
        }
        return false;
    }
    public static void Track(PlayerMobile owner)
    {
        bool faded = Faded(owner);
        if (faded ? _faded.Add(owner.Serial) : _faded.Remove(owner.Serial)) { SendOpacity(owner, faded); }
        if (Active(owner) || faded)
        {
            if (_owners.Add(owner.Serial) && !faded) { SendOpacity(owner, false); }
        }
        else { _owners.Remove(owner.Serial); }
    }
    private static void Pulse()
    {
        _keys.Clear(); foreach (var owner in _owners) { _keys.Add(owner); }
        foreach (var serial in _keys)
        {
            if (World.FindMobile(serial) is not PlayerMobile player)
            { _owners.Remove(serial); _faded.Remove(serial); continue; }
            bool faded = Faded(player), aura = Active(player);
            if (faded || _faded.Contains(serial)) { SendOpacity(player, faded); }
            if (faded) { _faded.Add(serial); } else { _faded.Remove(serial); }
            if (!aura && !faded) { _owners.Remove(serial); continue; }
            if (!aura || player.Hidden || player.Map == null || player.Map == Map.Internal) { continue; }
            // Reuse native gold particles; the aura never owns body, hue, light or hidden-state flags.
            player.FixedParticles(0x376A, 9, 32, 5030, 0x8A5, 0, EffectLayer.Waist);
        }
    }
    public static void WriteOpacity(Span<byte> packet, Serial owner, byte opacity)
    {
        var writer = new System.Buffers.SpanWriter(packet);
        writer.Write((byte)0xBF); writer.Write((ushort)NamedVisualProtocol.Length);
        writer.Write(NamedVisualProtocol.SubCommand); writer.Write(NamedVisualProtocol.Version);
        writer.Write(owner.Value); writer.Write(opacity);
    }
    private static void SendOpacity(PlayerMobile owner, bool faded)
    {
        if (owner.Map == null || owner.Map == Map.Internal) { return; }
        Span<byte> packet = stackalloc byte[NamedVisualProtocol.Length];
        WriteOpacity(packet, owner.Serial, faded ? NamedVisualProtocol.Faded : NamedVisualProtocol.Opaque);
        foreach (var state in owner.Map.GetClientsInRange(owner.Location))
        {
            if (state.Mobile?.CanSee(owner) == true) { state.Send(packet); }
        }
    }

}
