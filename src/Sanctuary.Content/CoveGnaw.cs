// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class CoveGnaw : ConversationNpc
{
    internal const int ChampionLevel = 22;
    internal static readonly Point3D Pentagram = new(2582, 1118, 0);
    private static readonly HashSet<Serial> _departing = new();
    private static readonly ScriptedDialogue.Node[] _pact =
    [
        new("Master's dead. Left Gnaw in the dirt with his chalk and his bones. I can smell the Abyss through the circle, but I can't find the way back.",
            [new("The Abyss", 1), new("A Bargain", 2)]),
        new("Home. I don't care if it burns. Anywhere but sitting here till the rain washes the circle clean.", [new("A Bargain", 2)]),
        new("Help me through and you can have what's left of my gold and my things. Just get me out of this forest.",
            [new("Speak the Dispel", 0, (player, speaker) => ((CoveGnaw)speaker).Dispel(player))])
    ];

    public new static void Configure()
    {
        EventSink.Disconnected += player => _departing.Remove(player.Serial);
        ClassMapHooks.LocationChanged += mobile =>
        {
            if (mobile is PlayerMobile player && _departing.Contains(player.Serial)
                && (player.Map != Map.Trammel || !player.InRange(Pentagram, 20)))
                _departing.Remove(player.Serial);
        };
    }

    [Constructible]
    public CoveGnaw()
    {
        Name = "Gnaw";
        Body = 39;
        BaseSoundID = 422;
        CantWalk = true;
        Blessed = true;
        Direction = Direction.South;
    }

    internal override ushort Portrait => PortraitProtocol.GnawPortrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _pact;
    internal override bool CanTalkTo(PlayerMobile player) => FriendlyTo(player);
    public override bool IsInvulnerable => true;

    internal static bool FriendlyTo(PlayerMobile player) =>
        ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == DiabloClass.Warlock
        && (ClassCreation.Profiles.Discoveries(player.Serial) & StoryDiscoveries.GnawFreed) == 0;

    internal static bool HostileTo(Mobile target)
    {
        var player = target as PlayerMobile ?? (target as BaseCreature)?.GetMaster() as PlayerMobile;
        return player == null || !FriendlyTo(player) && !_departing.Contains(player.Serial);
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Level "}{ChampionLevel}");
        list.Add("Champion");
    }

    private void Dispel(PlayerMobile player)
    {
        if (!ScriptedDialogue.CanTalk(player, this) || player.Backpack is not { } pack)
            return;

        var random = Random.Shared;
        var bases = GearDrops.Bases.Where(basis => basis.Tier == GearTier.Normal && basis.Variants.Length > 0
            && basis.QualityLevel <= ChampionLevel && basis.ExcludedReason == null).ToArray();
        var gear = GearDrops.GenerateMagic(bases[random.Next(bases.Length)], ChampionLevel, random);
        var gold = new Gold(50);
        if (!pack.TryDropItem(player, gear, false) || !pack.TryDropItem(player, gold, false))
        {
            gear.Delete();
            gold.Delete();
            ScriptedDialogue.Show(player, this,
                [new("Make room for my things first. I'm holding up my end of the bargain.", _pact[2].Replies)]);
            return;
        }

        _departing.Add(player.Serial);
        ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.GnawFreed);
        player.PrivateOverheadMessage(MessageType.Regular, 0x0481, false, "An Ort!", player.NetState);
        FixedParticles(0x3728, 10, 13, 5042, EffectLayer.Waist);
        PlaySound(0x201);
        player.NetState?.SendRemoveEntity(Serial);
        player.SendMessage("Gnaw's gold and a piece of magic equipment have been placed in thy pack.");
    }
}
