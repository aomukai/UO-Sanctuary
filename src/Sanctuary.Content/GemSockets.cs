// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed record InstalledGem(string Family, GemQuality Quality, GemEffects Effects)
{
    public string Name => $"{Quality} {SocketGems.Family(Family).Name}";
}
public sealed record GemInsertion(PlayerMobile Owner, SocketGem Gem, Item Host, GearState State,
    InstalledGem Content, NetState Connection, long Session)
{
    internal object GemParent, HostParent;
    internal EquipmentSalvage.Snapshot GemSnapshot, HostSnapshot;
    internal string EffectIdentity;
    public bool Claimed { get; internal set; }
}

public static class GemSockets
{
    private static GemEffects Clone(GemEffects e) => new((GemEffect[])e.Weapon.Clone(), (GemEffect[])e.Shield.Clone(), (GemEffect[])e.Armor.Clone());
    internal static InstalledGem[] Copy(InstalledGem[] gems)
    {
        var result = new InstalledGem[gems.Length];
        for (int i = 0; i < gems.Length; i++) { result[i] = gems[i] with { Effects = Clone(gems[i].Effects) }; }
        return result;
    }
    internal static bool Accessible(PlayerMobile owner, SocketGem gem) => RuneSockets.Accessible(owner, gem, true) && gem.Origin != ItemOrigin.Vendor;
    public static GemEffect[] Effects(Item host, InstalledGem gem) => ClassGear.Slot(host) switch
    {
        GearSlot.Weapon => gem.Effects.Weapon, GearSlot.Shield => gem.Effects.Shield,
        GearSlot.Head or GearSlot.Body => gem.Effects.Armor, _ => []
    };
    internal static int Bonus(Item host, GearState state, string code)
    {
        int value = 0;
        foreach (var gem in state.Gems)
        {
            foreach (var effect in Effects(host, gem)) { if (effect.Code == code) { value += effect.Minimum; } }
        }
        return value;
    }
    public static GemInsertion Quote(PlayerMobile owner, SocketGem gem, Item host)
    {
        if (!RuneSockets.Ready(owner) || !Accessible(owner, gem) || !RuneSockets.Accessible(owner, host)
            || !ClassGear.Converted(host) || !GearDetails.Socketable(host) || host.Origin == ItemOrigin.Vendor) { return null; }
        var state = ClassGear.State(host);
        if (state is not { Identified: true, StarterTeeth: false } || RuneSockets.Filled(state) >= state.SocketCount) { return null; }
        return new(owner, gem, host, state, new(gem.Family, gem.Quality, Clone(gem.Effects)), owner.NetState, RuneSockets.Begin(owner))
        { GemParent = gem.Parent, HostParent = host.Parent, GemSnapshot = new(gem), HostSnapshot = new(host), EffectIdentity = gem.EffectIdentity };
    }
    public static bool Commit(GemInsertion q)
    {
        if (q == null || q.Claimed) { return false; } q.Claimed = true;
        bool current = RuneSockets.Current(q.Owner, q.Session); RuneSockets.End(q.Owner, q.Session);
        if (!current || !RuneSockets.Ready(q.Owner) || !ReferenceEquals(q.Connection, q.Owner.NetState)
            || !Accessible(q.Owner, q.Gem) || !RuneSockets.Accessible(q.Owner, q.Host)
            || !ReferenceEquals(q.Gem.Parent, q.GemParent) || !ReferenceEquals(q.Host.Parent, q.HostParent)
            || !ReferenceEquals(ClassGear.State(q.Host), q.State) || !q.GemSnapshot.Valid || !q.HostSnapshot.Valid
            || q.Gem.EffectIdentity != q.EffectIdentity || q.Gem.Family != q.Content.Family || q.Gem.Quality != q.Content.Quality
            || RuneSockets.Filled(q.State) >= q.State.SocketCount || !GearDetails.Socketable(q.Host) || q.Host.Origin == ItemOrigin.Vendor) { return false; }
        var gems = new InstalledGem[q.State.Gems.Length + 1]; q.State.Gems.CopyTo(gems, 0); gems[^1] = q.Content;
        ClassGear.Profiles.Store(q.Host, q.State with { Gems = gems }); q.Gem.Consume(1);
        q.Owner.SendMessage("One gem is permanently installed. It cannot be removed or replaced."); return true;
    }
    internal static void Properties(Item host, GearState state, IPropertyList list)
    {
        foreach (var gem in state.Gems)
        {
            list.Add($"{"Installed "}{gem.Name}");
            foreach (var effect in Effects(host, gem)) { Property(list, effect); }
        }
    }
    internal static void Property(IPropertyList list, GemEffect effect)
    {
        if (effect.Code == "cold-damage")
        { list.Add($"{effect.Label}{": "}{effect.Minimum}{"–"}{effect.Maximum}{"; chill "}{effect.DurationMs / 1000.0:F1}{" seconds"}"); }
        else if (effect.DurationMs > 0)
        { list.Add($"{effect.Label}{": "}{effect.Minimum}{"–"}{effect.Maximum}{" over "}{effect.DurationMs / 1000.0:F1}{" seconds"}"); }
        else if (effect.Minimum != effect.Maximum) { list.Add($"{effect.Label}{": "}{effect.Minimum}{"–"}{effect.Maximum}"); }
        else { list.Add($"{effect.Label}{": +"}{effect.Minimum}"); }
    }
    internal static void Validate(InstalledGem gem)
    {
        if (gem == null || !Enum.IsDefined(gem.Quality)) { throw new InvalidDataException("Invalid installed gem identity."); }
        SocketGems.Family(gem.Family); SocketGems.Validate(gem.Effects);
    }
    internal static void Write(IGenericWriter writer, InstalledGem[] gems)
    {
        writer.Write(gems.Length);
        foreach (var gem in gems)
        { writer.Write(gem.Family); writer.Write((byte)gem.Quality); writer.Write(JsonSerializer.Serialize(gem.Effects)); }
    }
    internal static InstalledGem[] Read(IGenericReader reader)
    {
        int count = reader.ReadInt(); if (count is < 0 or > 5) { throw new InvalidDataException("Invalid installed gem count."); }
        var gems = new InstalledGem[count];
        for (int i = 0; i < count; i++)
        {
            string family = reader.ReadString(); var quality = (GemQuality)reader.ReadByte(); string json = reader.ReadString();
            if (json is not { Length: > 0 and <= 16000 }) { throw new InvalidDataException("Invalid installed gem payload."); }
            gems[i] = new(family, quality, JsonSerializer.Deserialize<GemEffects>(json)); Validate(gems[i]);
        }
        return gems;
    }
}
