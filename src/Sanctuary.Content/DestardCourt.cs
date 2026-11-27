// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class DestardBounty
{
    internal const int GoldPerHorn = 100;
    internal sealed record Favor(long Horns, int FirstAt, int SecondAt)
    {
        internal bool First => Horns >= FirstAt;
        internal bool Second => Horns >= SecondAt;
    }

    private static BountyProfiles _profiles;
    public static void Configure() => _profiles = new BountyProfiles();
    internal static Favor For(PlayerMobile player) => _profiles.PlayerFavor.GetValueOrDefault(player.Serial);

    internal static Favor Record(PlayerMobile player, int count)
    {
        var before = For(player) ?? new(0, Utility.RandomMinMax(10, 19), Utility.RandomMinMax(20, 29));
        var after = before with { Horns = before.Horns > long.MaxValue - count ? long.MaxValue : before.Horns + count };
        _profiles.PlayerFavor[player.Serial] = after;
        return after;
    }

    private sealed class BountyProfiles() : GenericPersistence("SanctuaryDestardBounty", 146)
    {
        internal readonly Dictionary<Serial, Favor> PlayerFavor = new();
        private KeyValuePair<Serial, Favor>[] _snapshot = [];

        public override void Serialize()
        {
            _snapshot = [.. PlayerFavor];
            base.Serialize();
        }

        public override void Serialize(IGenericWriter writer)
        {
            writer.Write(1);
            writer.Write(_snapshot.Length);
            foreach (var (serial, favor) in _snapshot)
            {
                writer.Write(serial.Value);
                writer.Write(favor.Horns);
                writer.Write(favor.FirstAt);
                writer.Write(favor.SecondAt);
            }
        }

        public override void Deserialize(IGenericReader reader)
        {
            int version = reader.ReadInt();
            if (version is < 0 or > 1)
                throw new InvalidDataException("Unsupported Destard bounty save.");

            int count = reader.ReadInt();
            if (count is < 0 or > 1000000)
                throw new InvalidDataException("Invalid Destard bounty count.");

            for (int i = 0; i < count; i++)
            {
                var serial = (Serial)reader.ReadUInt();
                long horns = version == 0 ? reader.ReadInt() : reader.ReadLong();
                var favor = new Favor(horns, reader.ReadInt(), reader.ReadInt());
                if (!serial.IsMobile || horns < 1 || version == 0 && horns > 29 || favor.FirstAt is < 10 or > 19
                    || favor.SecondAt is < 20 or > 29 || !PlayerFavor.TryAdd(serial, favor))
                    throw new InvalidDataException("Invalid Destard bounty entry.");
            }
        }

        public override void PostDeserialize()
        {
            foreach (var serial in new List<Serial>(PlayerFavor.Keys))
                if (World.FindMobile(serial) is not PlayerMobile { Deleted: false })
                    PlayerFavor.Remove(serial);
        }
    }
}

[SerializationGenerator(0)]
public partial class DestardQueen : ConversationNpc
{
    private static readonly ScriptedDialogue.Reply _payment =
        new("Turn In Daemon Horns", 0, (player, speaker) => ((DestardQueen)speaker).Pay(player));
    private static readonly ScriptedDialogue.Node[] _victory =
    [
        new("Thou hast freed our home. My brood can return to its halls, and for that thou hast my thanks. Daemons still linger here. I will pay one hundred gold for each horn thou bringest me.", [_payment])
    ];

    [Constructible]
    public DestardQueen()
    {
        Name = "Vaelith";
        Title = "the dragon queen";
        Female = true;
        Body = 12;
        BaseSoundID = 362;
        CantWalk = true;
    }

    internal override ushort Portrait => (ushort)NpcPortrait.DestardQueen;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script =>
    [
        new("My brood hears a voice from below, and its servants crawl over our stones. Bring me their horns. I will give thee one hundred gold for each.", [_payment])
    ];

    internal override void BeginConversation(PlayerMobile player)
    {
        ClassCreation.Profiles.Discover(player.Serial, StoryDiscoveries.DestardQueenAudience);
        if (BritanniaCampaign.Has(player, CampaignProgress.Diablo))
        {
            ScriptedDialogue.Show(player, this, _victory);
            return;
        }

        foreach (var consort in Map.GetMobilesInRange<DestardConsort>(Location, 8))
        {
            if (!consort.Alive || consort.Deleted || Math.Abs(consort.Z - Z) >= 16
                || !ScriptedDialogue.CanTalk(player, consort))
                continue;

            ScriptedDialogue.Show(player, this,
            [
                new("A small meal, but we have endured worse.", [new("Continue", 1)], new(consort.Name, consort.Portrait)),
                new("Be silent. This one may yet prove useful.", [new("Continue", 2)]),
                Script[0]
            ]);
            return;
        }
        base.BeginConversation(player);
    }

    private void Pay(PlayerMobile player)
    {
        if (!ScriptedDialogue.CanTalk(player, this) || player.Backpack is not { } pack)
            return;
        int count = pack.GetAmount(typeof(DaemonHorn));
        if (count <= 0 || !pack.ConsumeTotal(typeof(DaemonHorn), count))
        {
            ScriptedDialogue.Show(player, this, [new("Thou hast brought no horns. I pay for those of the lesser daemons, daemons, and archdaemons.", [_payment])]);
            return;
        }

        long reward = (long)count * DestardBounty.GoldPerHorn;
        for (long remaining = reward; remaining > 0; remaining -= 60000)
            pack.DropItem(new Gold((int)Math.Min(remaining, 60000)));

        var favor = DestardBounty.Record(player, count);
        string horns = favor.Horns == 1 ? "horn" : "horns";
        string thanks = BritanniaCampaign.Has(player, CampaignProgress.Diablo)
            ? "The voice is gone, yet its servants remain. I still honour our bargain."
            : "Thou hast my thanks.";
        ScriptedDialogue.Show(player, this,
            [new($"Here is thy {reward:N0} gold. Thou hast brought me {favor.Horns:N0} {horns} in all. {thanks}", [_payment])]);
    }
}

[SerializationGenerator(0)]
public partial class DestardConsort : ConversationNpc
{
    internal const string DragonName = "Rhazek";
    private static readonly ScriptedDialogue.Node[] _victory =
    [
        new("I took thee for a morsel. I was wrong. There, I have said it. My people have their home again, and I owe thee for that.", [])
    ];

    [Constructible]
    public DestardConsort()
    {
        Name = DragonName;
        Title = "the queen’s consort";
        Body = 12;
        BaseSoundID = 362;
        Hue = 0x455;
        CantWalk = true;
    }

    internal override ushort Portrait => (ushort)NpcPortrait.DestardConsort;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script =>
    [
        new("The wyverns and wyrms betrayed us at the first whisper of power. The drakes and dragons heard the same voice and were enslaved. I shall remember who chose to kneel.", [])
    ];

    internal override void BeginConversation(PlayerMobile player) => ScriptedDialogue.Show(player, this,
        BritanniaCampaign.Has(player, CampaignProgress.Diablo) ? _victory : Script);
}
