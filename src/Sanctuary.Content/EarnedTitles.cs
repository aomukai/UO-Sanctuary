// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Commands;
using Server.Mobiles;

namespace Sanctuary;

public sealed record EarnedTitleState(byte Classes, IReadOnlyList<byte> Ranks, DiabloClass Prefix, byte Skill, byte Tier);

public static class EarnedTitles
{
    public static readonly SkillName[] Skills = [SkillName.Alchemy, SkillName.Blacksmith, SkillName.Fletching, SkillName.Carpentry,
        SkillName.Cartography, SkillName.Cooking, SkillName.Fishing, SkillName.Inscribe, SkillName.Lumberjacking,
        SkillName.Mining, SkillName.Tailoring, SkillName.Tinkering, SkillName.Imbuing];
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static long _sequence;
    internal static long Begin(PlayerMobile player) => _sessions[player.Serial] = ++_sequence;
    internal static bool Session(PlayerMobile player, long token) => _sessions.GetValueOrDefault(player.Serial) == token;
    internal static void End(Mobile player) => _sessions.Remove(player.Serial);
    public static EarnedTitleProfiles Profiles { get; private set; }
    public static void Configure()
    {
        Profiles = new(); CharacterTitleHooks.Display = Display;
        ClassCombatHooks.SkillChanged += (player, _, _) => Refresh(player);
        EventSink.Connected += player => { if (player is PlayerMobile p) { Refresh(p); } };
        EventSink.WorldSave += Profiles.Prune;
        EventSink.Disconnected += End;
        CommandSystem.Register("Titles", AccessLevel.Player, args => { if (args.Mobile is PlayerMobile p) { EarnedTitleGump.DisplayTo(p); } });
    }
    public static bool Applies(PlayerMobile player) => player is { Deleted: false }
        && ClassCreation.Profiles?.TryGet(player.Serial, out _) == true;
    public static string Prefix(DiabloClass characterClass) => characterClass switch
    {
        DiabloClass.Amazon => "Warmaster", DiabloClass.Sorceress => "Archmage", DiabloClass.Necromancer => "Deathspeaker",
        DiabloClass.Paladin => "High Templar", DiabloClass.Barbarian => "Warlord", DiabloClass.Druid => "Archdruid",
        DiabloClass.Assassin => "Shadowmaster", _ => ""
    };
    public static string Profession(SkillName skill) => skill switch
    {
        SkillName.Alchemy => "Alchemist", SkillName.Blacksmith => "Blacksmith", SkillName.Fletching => "Bowyer",
        SkillName.Carpentry => "Carpenter", SkillName.Cartography => "Cartographer", SkillName.Cooking => "Cook",
        SkillName.Fishing => "Fisher", SkillName.Inscribe => "Scribe", SkillName.Lumberjacking => "Lumberjack",
        SkillName.Mining => "Miner", SkillName.Tailoring => "Tailor", SkillName.Tinkering => "Tinker", SkillName.Imbuing => "Imbuer", _ => ""
    };
    public static string Rank(byte tier) => tier switch { 1 => "Grandmaster", 2 => "Elder", 3 => "Legendary", _ => "" };
    public static byte Tier(int trained) => trained >= 1200 ? (byte)3 : trained >= 1100 ? (byte)2 : trained >= 1000 ? (byte)1 : (byte)0;
    public static string Suffix(EarnedTitleState state) => state.Skill == byte.MaxValue ? "" : $"{Rank(state.Tier)} {Profession((SkillName)state.Skill)}";
    public static CharacterTitleDisplay? Display(PlayerMobile player)
    {
        if (!Applies(player)) { return null; }
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        var state = Profiles?.Find(player.Serial) ?? EarnedTitleProfiles.Empty;
        return new(Prefix(state.Prefix), Suffix(state), $"Level {profile.Level} {DiabloClasses.DisplayName(profile.Class, player.Female)}");
    }
    public static bool Refresh(PlayerMobile player, bool loading = false)
    {
        if (Profiles == null || World.Loading && !loading || !Applies(player)) { return false; }
        var state = Profiles.Find(player.Serial); byte classes = state.Classes; var ranks = state.Ranks.ToArray();
        foreach (var history in ClassCreation.Profiles.Histories(player.Serial))
        { if (history.Profile.Level == 99) { classes |= (byte)(1 << ((int)history.Profile.Class - 101)); } }
        for (int i = 0; i < Skills.Length; i++) { ranks[i] = Math.Max(ranks[i], Tier(player.Skills[Skills[i]].BaseFixedPoint)); }
        if (classes == state.Classes && ranks.SequenceEqual(state.Ranks)) { return false; }
        Profiles.Store(player.Serial, state with { Classes = classes, Ranks = Array.AsReadOnly(ranks) });
        if (!World.Loading) { Changed(player); }
        return true;
    }
    public static void Changed(PlayerMobile player)
    {
        if (!Applies(player)) { return; }
        player.InvalidateProperties(); TitleNetwork.Broadcast(player);
    }
    internal static bool Choose(PlayerMobile player, EarnedTitleState quoted, DiabloClass prefix, byte skill, byte tier)
    {
        if (!Applies(player) || !ClassProfessions.Ready(player)) { return false; }
        Refresh(player);
        var state = Profiles.Find(player.Serial);
        if (!ReferenceEquals(state, quoted) || !EarnedTitleProfiles.ValidChoice(state, prefix, skill, tier)) { return false; }
        Profiles.Store(player.Serial, state with { Prefix = prefix, Skill = skill, Tier = tier });
        if (!World.Loading) { Changed(player); }
        return true;
    }
}

public sealed class EarnedTitleProfiles : GenericPersistence
{
    public static readonly EarnedTitleState Empty = new(0, Array.AsReadOnly(new byte[13]), 0, byte.MaxValue, 0);
    private Dictionary<Serial, EarnedTitleState> _players = new();
    public EarnedTitleProfiles() : base("SanctuaryEarnedTitles", 132) { }
    public EarnedTitleState Find(Serial owner) => _players.GetValueOrDefault(owner, Empty);
    internal void Store(Serial owner, EarnedTitleState state) => _players[owner] = state;
    public static bool ValidChoice(EarnedTitleState state, DiabloClass prefix, byte skill, byte tier)
    {
        if (prefix != 0 && (!DiabloClasses.TryFromWire((int)prefix, out _) || (state.Classes & 1 << ((int)prefix - 101)) == 0)) { return false; }
        if (skill == byte.MaxValue) { return tier == 0; }
        int index = Array.IndexOf(EarnedTitles.Skills, (SkillName)skill);
        return index >= 0 && tier is >= 1 and <= 3 && tier <= state.Ranks[index];
    }
    public void Prune()
    {
        foreach (var owner in _players.Keys.ToArray())
        { if (World.FindMobile(owner) is not PlayerMobile { Deleted: false }) { _players.Remove(owner); } }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_players.Count);
        foreach (var (owner, state) in _players.OrderBy(p => p.Key.Value))
        {
            writer.Write(owner.Value); writer.Write(state.Classes);
            foreach (byte rank in state.Ranks) { writer.Write(rank); }
            writer.Write((byte)state.Prefix); writer.Write(state.Skill); writer.Write(state.Tier);
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unsupported earned titles version."); }
        int count = reader.ReadInt(); if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid title owner count."); }
        var players = new Dictionary<Serial, EarnedTitleState>();
        for (int i = 0; i < count; i++)
        {
            var owner = (Serial)reader.ReadUInt(); byte classes = reader.ReadByte(); var ranks = new byte[13];
            for (int j = 0; j < ranks.Length; j++) { ranks[j] = reader.ReadByte(); }
            var state = new EarnedTitleState(classes, Array.AsReadOnly(ranks), (DiabloClass)reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            // The existing byte now holds all eight class achievement bits, including Warlock at bit7.
            if (!owner.IsMobile || ranks.Any(r => r > 3) || !ValidChoice(state, state.Prefix, state.Skill, state.Tier)
                || !players.TryAdd(owner, state)) { throw new InvalidDataException("Invalid earned title identity or selection."); }
        }
        _players = players;
    }
    public override void PostDeserialize()
    {
        Prune();
        foreach (var owner in _players.Keys.ToArray())
        { if (!EarnedTitles.Applies(World.FindMobile(owner) as PlayerMobile)) { throw new InvalidDataException("Title owner has no class history."); } }
        // Existing class characters are indexed by the already loaded character ledger.
        foreach (var player in ClassCreation.Profiles.Owners()) { EarnedTitles.Refresh(player, true); }
    }
}
