// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[Flags]
internal enum YewProgress : byte
{
    None = 0,
    HeardOfAbbey = 1,
    ClericReadLetter = 2,
    Heartwood = 4,
    ReturnedRing = 8,
    Taming = 16,
    Hellsteed = 32
}

public static class YewStory
{
    internal static readonly Point3D YewEntrance = new(534, 992, 0);
    internal static readonly Point3D HeartwoodLanding = new(6986, 340, 0);
    internal static readonly Point3D MayorPost = new(558, 987, 0);
    internal static readonly Point3D ClericPost = new(635, 821, 0);
    internal static readonly Point3D NightwindPost = new(6983, 336, 0);
    internal static readonly Point3D VampirePost = new(965, 703, 0);
    private static YewProfiles _profiles;

    public static void Configure()
    {
        _profiles = new();
        EventSink.ServerStarted += Install;
    }

    internal static bool Has(PlayerMobile owner, YewProgress flag) =>
        (_profiles.States.GetValueOrDefault(owner.Serial) & flag) == flag;

    internal static bool Add(PlayerMobile owner, YewProgress flag)
    {
        if (Has(owner, flag))
            return false;
        _profiles.States[owner.Serial] = _profiles.States.GetValueOrDefault(owner.Serial) | flag;
        return true;
    }

    private static void Install()
    {
        var map = Map.Trammel;
        using var obsolete = PooledRefList<Teleporter>.Create();
        bool found = false;
        foreach (var item in map.GetItemsAt<Teleporter>(YewEntrance))
        {
            if (item is HeartwoodPassage)
                found = true;
            else if (item.GetType() == typeof(Teleporter) && item.Z == YewEntrance.Z && item.PointDest == HeartwoodLanding)
                obsolete.Add(item);
        }
        foreach (var item in obsolete)
            item.Delete();
        using var duplicateSparkles = PooledRefList<Static>.Create();
        foreach (var item in map.GetItemsAt<Static>(YewEntrance))
            if (item.Z == YewEntrance.Z && item.ItemID == 0x375A && !item.Movable)
                duplicateSparkles.Add(item);
        foreach (var item in duplicateSparkles)
            item.Delete();
        if (!found)
            new HeartwoodPassage().MoveToWorld(YewEntrance, map);
        if (!World.Mobiles.Values.OfType<MayorOfYew>().Any())
            new MayorOfYew().MoveToWorld(MayorPost, map);
        if (!World.Mobiles.Values.OfType<EmpathAbbeyCleric>().Any())
            new EmpathAbbeyCleric().MoveToWorld(ClericPost, map);
        if (!World.Mobiles.Values.OfType<Nightwind>().Any(npc => !npc.Recruited))
            new Nightwind().MoveToWorld(NightwindPost, map);
        YewVampire.EnsurePresent();
    }

    internal static YewKeepsake Find(PlayerMobile owner, YewKeepsakeKind kind)
    {
        if (owner.Backpack is { } pack)
            foreach (var item in pack.FindItemsByType<YewKeepsake>())
                if (item.Kind == kind && item.Avatar == owner.Serial)
                    return item;
        return null;
    }

    private sealed class YewProfiles() : GenericPersistence("SanctuaryYewStory", 135)
    {
        internal readonly Dictionary<Serial, YewProgress> States = new();
        private KeyValuePair<Serial, YewProgress>[] _snapshot = [];
        public override void Serialize()
        {
            _snapshot = [.. States];
            base.Serialize();
        }
        public override void Serialize(IGenericWriter writer)
        {
            writer.Write(0);
            writer.Write(_snapshot.Length);
            foreach (var (owner, progress) in _snapshot)
            {
                writer.Write(owner.Value);
                writer.Write((byte)progress);
            }
        }
        public override void Deserialize(IGenericReader reader)
        {
            if (reader.ReadInt() != 0)
                throw new InvalidOperationException("Unknown Yew story save version.");
            int count = reader.ReadInt();
            for (int index = 0; index < count; index++)
                States.Add((Serial)reader.ReadUInt(), (YewProgress)reader.ReadByte());
        }
    }
}

[SerializationGenerator(0)]
public partial class HeartwoodPassage : Teleporter
{
    [Constructible]
    public HeartwoodPassage() : base(YewStory.HeartwoodLanding, Map.Trammel)
    {
        ItemID = 0x375A;
        Visible = true;
        Name = "passage to Heartwood";
    }

    public override bool CanTeleport(Mobile mobile)
    {
        if (!base.CanTeleport(mobile))
            return false;
        if (mobile is not PlayerMobile owner || owner.AccessLevel > AccessLevel.Player || YewStory.Has(owner, YewProgress.Heartwood))
            return true;
        owner.SendMessage("The way to Heartwood is closed. The mayor of Yew can grant permission to enter.");
        return false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from.Map == Map && from.InRange(Location, 2) && from.InLOS(this) && CanTeleport(from))
            StartTeleport(from);
    }
}

[SerializationGenerator(0)]
public partial class MayorOfYew : ConversationNpc
{
    [Constructible]
    public MayorOfYew()
    {
        Name = "Aldwyn";
        Title = "the mayor of Yew";
        Body = 0x190;
        Hue = 0x83EA;
        HairItemID = 0x2048;
        FacialHairItemID = 0x203E;
        HairHue = FacialHairHue = 0x0481;
        CantWalk = true;
        AddItem(new FancyShirt(0x044E));
        AddItem(new Cloak(0x059D));
        AddItem(new LongPants(0x0455));
        AddItem(new Boots(0x0455));
    }

    internal override ushort Portrait => (ushort)NpcPortrait.YewMayor;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script =>
    [
        new("Welcome to Yew. If thou hast business with Heartwood, the elves have asked that visitors come by arrangement.", [])
    ];

    internal override void BeginConversation(PlayerMobile owner)
    {
        if (YewStory.Has(owner, YewProgress.Heartwood))
            ScriptedDialogue.Show(owner, this, [new("Thou hast leave to enter Heartwood. Take the passage in the old tree west of here.", [])]);
        else if (YewStory.Find(owner, YewKeepsakeKind.Recommendation) != null)
            ScriptedDialogue.Show(owner, this,
            [
                new("Art thou returned from the abbey?", [new("Recommendation", 0, (player, _) => Permit(player))])
            ]);
        else if (YewStory.Find(owner, YewKeepsakeKind.Letter) != null)
            ScriptedDialogue.Show(owner, this,
            [
                new("Welcome to Yew. What brings thee to my door?", [new("Rainsong's Letter", 0, (player, _) =>
                {
                    if (YewStory.Find(player, YewKeepsakeKind.Letter) == null)
                        return;
                    YewStory.Add(player, YewProgress.HeardOfAbbey);
                    ScriptedDialogue.Show(player, this,
                    [
                        new("Vael? The man's name means nothing to me. Rainsong, now, is a name I think I would remember. Try Empath Abbey. Their clerics know more of the old families than I do.", [])
                    ]);
                })])
            ]);
        else
            base.BeginConversation(owner);
    }

    private void Permit(PlayerMobile owner)
    {
        if (YewStory.Find(owner, YewKeepsakeKind.Recommendation) is not { } recommendation
            || !YewStory.Add(owner, YewProgress.Heartwood))
            return;
        recommendation.Delete();
        owner.SendMessage("You have been granted passage to Heartwood.");
        ScriptedDialogue.Show(owner, this,
        [
            new("If Sister Meriel vouches for thee, that is enough for me. Thou hast my permission. Seek the bright passage at the old tree west of here.", [])
        ]);
    }
}

[SerializationGenerator(0)]
public partial class EmpathAbbeyCleric : ConversationNpc
{
    [Constructible]
    public EmpathAbbeyCleric()
    {
        Name = "Sister Meriel";
        Title = "the cleric";
        Female = true;
        Body = 0x191;
        Hue = 0x83EA;
        HairItemID = 0x2046;
        HairHue = 0x0463;
        CantWalk = true;
        AddItem(new Robe(0x0482));
        AddItem(new Sandals(0x03E5));
    }

    internal override ushort Portrait => (ushort)NpcPortrait.AbbeyCleric;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script =>
    [
        new("Welcome to Empath Abbey, traveller. Thou art welcome to rest by the ankh.", [])
    ];

    internal override void BeginConversation(PlayerMobile owner)
    {
        if (YewStory.Has(owner, YewProgress.HeardOfAbbey) && !YewStory.Has(owner, YewProgress.Heartwood)
            && (YewStory.Find(owner, YewKeepsakeKind.Letter) != null || YewStory.Has(owner, YewProgress.ClericReadLetter)))
            ScriptedDialogue.Show(owner, this, [new("The mayor said thou hadst asked after one of our sisters?", [new("Rainsong", 0, (player, _) => Explain(player))])]);
        else
            base.BeginConversation(owner);
    }

    private void Explain(PlayerMobile owner)
    {
        if (YewStory.Find(owner, YewKeepsakeKind.Recommendation) == null)
        {
            var recommendation = new YewKeepsake(owner, YewKeepsakeKind.Recommendation);
            if (owner.Backpack?.TryDropItem(owner, recommendation, false) != true)
            {
                recommendation.Delete();
                owner.SendMessage("Make room in your backpack for the recommendation.");
                return;
            }
        }

        YewStory.Find(owner, YewKeepsakeKind.Letter)?.Delete();
        YewStory.Add(owner, YewProgress.ClericReadLetter);
        ScriptedDialogue.Show(owner, this,
        [
            new("Rainsong came here when Vael vanished. He was a mage. He left on an expedition thirty years ago, and for years she kept a candle in the window. She died a few winters back, still waiting for word.", [new("Her Family", 1)]),
            new("Their daughter Nightwind lives in Heartwood. Please, take my letter to the mayor. He can give thee leave to enter. Thank thee for bringing word, even after all this time.", [])
        ]);
    }
}
