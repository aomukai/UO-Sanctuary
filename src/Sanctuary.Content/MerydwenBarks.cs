// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public partial class Merydwen
{
    private static readonly string[] _opening =
    [
        "Well. There goes the quiet afternoon.",
        "Hold still. I’m earning my fee.",
        "You picked an expensive fight.",
        "Let’s see how brave you are with holes in you.",
        "I’ve had worse trouble over a bar tab.",
        "You take the ugly one. Oh, that’s unfortunate.",
        "Try to leave my arrows in one piece.",
        "Finally. Something I can settle with a bow."
    ];
    private static readonly string[] _guided = ["Run. It likes a chase.", "That one’s got your name on it."];
    private static readonly string[] _sight = ["Found your weak spot.", "There. Make it hurt."];
    private static readonly string[] _strafe = ["Enough for the whole crew!", "One each. Mind your manners."];
    private static readonly string[] _closing =
    [
        "You’re supposed to stay over THERE.", "A little room to draw, darling!", "Get this bastard off me!",
        "Careful. I bite closer up.", "Don’t leave me boxed in!", "You’re still behind me, yes?"
    ];
    private static readonly string[] _wounded =
    [
        "Spare a potion? Mine seem to have deserted.", "I’m bleeding on perfectly good boots.",
        "Healing would be a sound investment.", "A bottle, please. The useful kind.",
        "I’d rather not earn the whole fee in scars."
    ];
    private static readonly string[] _urgent =
    [
        "I can’t—cover me!", "Don’t you bloody leave me here.", "Tell me you’ve got a potion.",
        "I’m still here. Help me!", "I thought I could take that.", "Please. I need a hand."
    ];
    private static readonly string[] _healed =
    [
        "Good. I was rather attached to my blood.", "That goes on your side of the ledger.",
        "You came back. …Good.", "Thanks. Don’t make a ceremony of it.", "Right. Who was trying to kill me?"
    ];
    private static readonly string[] _afterFight =
    [
        "Everyone still breathing? On our side, I mean.", "Let me count my arrows.", "Five thousand. A bargain.",
        "That’ll make a decent tavern story.", "You held your ground. I noticed.", "Give me a moment. Just catching my breath."
    ];
    private static readonly string[] _afterResurrection =
    [
        "That is staying out of the tavern story.", "Did you keep my bow?", "You paid to bring me back?",
        "I remember falling. Then… nothing.", "Let’s not do that again."
    ];

    private const int VoiceHue = 0x0030;
    private const int BarkIntervalMs = 7000, RepeatIntervalMs = 120_000, FightQuietMs = 5000;
    private long _fightUntil, _nextBark, _nextOpening, _nextWarning;
    private bool _urgentSpoken, _enemyClose, _thanked;
    private int _lowestHits = int.MaxValue;
    private Map _barkMap;

    private bool BarkEnemy(PlayerMobile owner, Mobile foe) => foe is { Deleted: false, Alive: true }
        && foe.Map == Map && Math.Abs(Z - foe.Z) < 16 && InRange(foe, RangePerception) && ClassCombat.IsHostile(owner, foe);

    private void ObserveBarks()
    {
        if (ControlMaster is not PlayerMobile { Alive: true } owner || !OwnedBy(owner) || !Alive || IsDeadPet
            || owner.Map != Map || Map == Map.Internal)
        {
            ResetFight();
            return;
        }
        if (_barkMap != Map)
        {
            ResetFight();
            _barkMap = Map;
        }

        long now = Core.TickCount;
        bool fighting = BarkEnemy(owner, Combatant);
        bool closing = fighting && InRange(Combatant, 2) && Combatant.Combatant == this;
        foreach (var aggression in Aggressors)
        {
            var foe = aggression.Attacker;
            if (!aggression.Expired && foe.Combatant == this && BarkEnemy(owner, foe))
            {
                fighting = true;
                closing |= InRange(foe, 2);
            }
        }

        bool starting = _fightUntil == 0;
        if (fighting)
            _fightUntil = now + FightQuietMs;
        if (_fightUntil == 0)
            return;

        if (now >= _fightUntil)
        {
            if (!_thanked && Hits - _lowestHits >= Math.Max(1, HitsMax / 10))
                SayBark(owner, _healed, now);
            else if (Random.Shared.Next(4) == 0)
                SayBark(owner, _afterFight, now);
            ResetFight();
            return;
        }

        if (Hits * 2L < HitsMax)
            _lowestHits = Math.Min(_lowestHits, Hits);
        if (Hits * 4L < HitsMax && !_urgentSpoken && MercenaryCare.Stock(this) == 0)
            _urgentSpoken = SayBark(owner, _urgent, now, force: true);
        else if (now >= _nextBark)
        {
            if (Hits * 2L < HitsMax && now >= _nextWarning && !_urgentSpoken && MercenaryCare.Stock(this) == 0)
            {
                if (SayBark(owner, _wounded, now))
                    _nextWarning = now + RepeatIntervalMs;
            }
            else if (!_thanked && Hits - _lowestHits >= Math.Max(1, HitsMax / 10))
                _thanked = SayBark(owner, _healed, now);
            else if (starting && now >= _nextOpening && Random.Shared.Next(10) == 0)
            {
                if (SayBark(owner, _opening, now))
                    _nextOpening = now + RepeatIntervalMs;
            }
            else if (closing && !_enemyClose && Random.Shared.Next(5) == 0)
                SayBark(owner, _closing, now);
        }
        _enemyClose = closing;
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        base.OnDamage(amount, from, willKill);
        int remaining = Hits - amount;
        if (!Recruited || IsDeadPet || willKill || remaining <= 0 || amount <= 0 || remaining * 2L >= HitsMax
            || ControlMaster is not PlayerMobile { Alive: true } owner || !OwnedBy(owner) || !BarkEnemy(owner, from))
            return;

        if (_barkMap != Map)
        {
            ResetFight();
            _barkMap = Map;
        }
        long now = Core.TickCount;
        _fightUntil = now + FightQuietMs;
        _lowestHits = Math.Min(_lowestHits, remaining);
        if (remaining * 4L < HitsMax && !_urgentSpoken && MercenaryCare.Stock(this) == 0)
            _urgentSpoken = SayBark(owner, _urgent, now, force: true);
    }

    private bool SayBark(PlayerMobile owner, string[] lines, long now, bool force = false)
    {
        if (!force && now < _nextBark || owner.NetState is not { Running: true } connection
            || !owner.InRange(this, 12) || !owner.CanSee(this))
            return false;
        PrivateOverheadMessage(MessageType.Regular, VoiceHue, false, lines[Random.Shared.Next(lines.Length)], connection);
        _nextBark = now + BarkIntervalMs;
        return true;
    }

    private void ResetFight()
    {
        _fightUntil = 0;
        _lowestHits = int.MaxValue;
        _urgentSpoken = _enemyClose = _thanked = false;
    }
}
