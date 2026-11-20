// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public partial class Wolfgang
{
    private const int BarkPauseMs = 7000;
    private const int RepeatBarkMs = 120_000;
    private const int FightQuietMs = 5000;

    private static readonly string[] _opening =
    [
        "Behind me, Avatar. I shall—come on, then, you bastards!",
        "Captain Geoffrey sends his regards!",
        "Pick on someone carrying a sword!",
        "Another step and I’ll take your bloody legs off!",
        "You want a fight? You’ve bloody well found one!",
        "I’ll nail your hide to the gate!",
        "Not one of you gets past me!",
        "Right. Just like the drills. Only uglier.",
        "Come on, then! I’m not afraid of you!",
        "Stay behind me! I can handle this!",
        "You there! Leave the Avatar alone!"
    ];

    private static readonly string[] _shouts =
    [
        "ME! You’re fighting ME!",
        "Eyes here, you ugly bastard!",
        "Come and break yourselves!",
        "Is that all you’ve got?",
        "Try that again! I dare you!",
        "You’ll have to go through me!"
    ];

    private static readonly string[] _composed =
    [
        "If thou hast a spare potion… mine are gone.",
        "A potion, please. Merely a precaution.",
        "I can hold. Some healing would help, though.",
        "Nothing serious. Have we any potions?",
        "My potions are gone. Canst thou spare one?",
        "No need to stop. Just… a potion, if thou hast one.",
        "I should have brought more potions. Hast thou one?",
        "It looks worse than it feels. A potion, please."
    ];

    private static readonly string[] _urgent =
    [
        "I’m still—ah! Help me stay standing!",
        "A potion. Please. Now.",
        "I can take another—no. No, I can’t. Help!",
        "That’s… rather a lot of blood. I need healing!",
        "Avatar? Stay close. I need help.",
        "I’m not afraid. It just bloody hurts! Help!",
        "Don’t tell the captain. Just help me.",
        "Cover me! I need to step back.",
        "I thought the armour would… please, help!"
    ];

    private static readonly string[] _healed =
    [
        "My thanks. I can hold again.",
        "There. Much better. I told thee it was nothing.",
        "I… needed that. Thank thee.",
        "Forgive the shouting.",
        "Right, you bastards. Where were we?",
        "Please forget whatever I just said."
    ];

    private static readonly string[] _afterFight =
    [
        "Was that… satisfactory?",
        "Captain Geoffrey would have wanted us to keep formation.",
        "My hands? They do that after a fight. Apparently.",
        "I thought that last one had me.",
        "We won. That’s what matters, isn’t it?",
        "I shall clean my sword. Give me something to do.",
        "About my language earlier… my apologies.",
        "Thou wilt not mention that to the captain, I hope?"
    ];

    private HashSet<Mobile> _foes;
    private Map _fightMap;
    private long _fightQuietUntil, _nextBark, _nextOpening, _nextComposed;
    private int _warningSpoken, _lowestWound = int.MaxValue;
    private bool _thanked;

    private void ObserveCombatBarks()
    {
        if (!Alive || IsDeadPet || ControlMaster is not PlayerMobile { Alive: true } owner
            || owner.Map != Map || Map == Map.Internal || !MercenaryCare.IsCompanion(owner, this))
        {
            EndBarkFight();
            return;
        }
        PrepareMap();

        long now = Core.TickCount;
        bool fighting = Remember(owner, Combatant);
        foreach (var aggression in Aggressors)
            if (!aggression.Expired && aggression.Attacker.Combatant == this)
                fighting |= Remember(owner, aggression.Attacker);

        bool starting = fighting && _fightQuietUntil == 0;
        if (fighting)
            _fightQuietUntil = now + FightQuietMs;
        var listener = Listener(owner);
        if (now >= _fightQuietUntil)
        {
            bool difficultVictory = _lowestWound != int.MaxValue && _foes?.Any(foe => foe.Hits <= 0) == true;
            EndBarkFight();
            if (difficultVictory && owner.InRange(this, 12))
                ClassCreation.Profiles.RecordWolfgangTrust(owner.Serial);
            if (difficultVictory && listener != null && now >= _nextBark && Random.Shared.Next(4) == 0)
                Bark(listener, Pick(_afterFight), now);
            return;
        }

        int warning = (long)Hits * 4 < HitsMax ? 2 : (long)Hits * 2 < HitsMax ? 1 : 0;
        if (warning > 0)
            _lowestWound = Math.Min(_lowestWound, Hits);
        if (listener == null)
            return;

        if (warning == 2 && _warningSpoken < 2 && MercenaryCare.Stock(this) == 0)
        {
            Urgent(listener, now);
        }
        else if (now >= _nextBark)
        {
            if (warning == 1 && _warningSpoken == 0 && now >= _nextComposed && MercenaryCare.Stock(this) == 0)
            {
                _warningSpoken = 1;
                _nextComposed = now + RepeatBarkMs;
                Bark(listener, Pick(_composed), now);
            }
            else if (!_thanked && Hits - _lowestWound >= Math.Max(1, HitsMax / 10))
            {
                _thanked = true;
                Bark(listener, Pick(_healed), now);
            }
            else if (starting && now >= _nextOpening && Random.Shared.Next(10) == 0)
            {
                _nextOpening = now + RepeatBarkMs;
                Bark(listener, Pick(_opening), now);
            }
        }
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        base.OnDamage(amount, from, willKill);
        long remaining = (long)Hits - amount;
        if (!Recruited || amount <= 0 || willKill || remaining <= 0 || !Alive || IsDeadPet
            || ControlMaster is not PlayerMobile { Alive: true } owner || owner.Map != Map
            || !MercenaryCare.IsCompanion(owner, this) || !BarkHostile(owner, from))
            return;

        PrepareMap();
        bool alreadyFighting = _fightQuietUntil != 0;
        long now = Core.TickCount;
        (_foes ??= []).Add(from);
        if (alreadyFighting || remaining * 2 < HitsMax)
            _fightQuietUntil = now + FightQuietMs;
        if (remaining * 2 < HitsMax)
            _lowestWound = Math.Min(_lowestWound, (int)remaining);
        if (remaining * 4 < HitsMax && _warningSpoken < 2 && MercenaryCare.Stock(this) == 0 && Listener(owner) is { } listener)
            Urgent(listener, now);
    }

    private void TryShoutBark(PlayerMobile owner)
    {
        long now = Core.TickCount;
        if (now >= _nextBark && Listener(owner) is { } listener)
            Bark(listener, Pick(_shouts), now);
    }

    private NetState Listener(PlayerMobile owner) =>
        owner.NetState is { Running: true } connection && owner.InRange(this, 12) && owner.CanSee(this) ? connection : null;

    private bool BarkHostile(PlayerMobile owner, Mobile foe) => foe is { Alive: true, Deleted: false }
        && foe.Map == Map && Math.Abs(Z - foe.Z) < 16 && ClassCombat.IsHostile(owner, foe);

    private bool Remember(PlayerMobile owner, Mobile foe)
    {
        if (!BarkHostile(owner, foe) || !InRange(foe, RangePerception))
            return false;
        (_foes ??= []).Add(foe);
        return true;
    }

    private void PrepareMap()
    {
        if (_fightMap == Map)
            return;
        EndBarkFight();
        _fightMap = Map;
    }

    private void EndBarkFight()
    {
        _foes?.Clear();
        _fightQuietUntil = 0;
        _warningSpoken = 0;
        _lowestWound = int.MaxValue;
        _thanked = false;
    }

    private void Urgent(NetState listener, long now)
    {
        _warningSpoken = 2;
        Bark(listener, Random.Shared.Next(40) == 0 ? "I don’t want to die here!" : Pick(_urgent), now);
    }

    private static string Pick(string[] lines) => lines[Random.Shared.Next(lines.Length)];

    private void Bark(NetState listener, string text, long now)
    {
        PrivateOverheadMessage(MessageType.Regular, 0x0021, false, text, listener);
        _nextBark = now + BarkPauseMs;
    }
}
