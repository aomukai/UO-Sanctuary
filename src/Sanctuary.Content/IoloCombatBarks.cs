// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public partial class Iolo
{
    private const int BarkPauseMs = 7000;
    private const int RepeatBarkMs = 120_000;
    private const int FightQuietMs = 5000;

    private static readonly string[] _enteringCombat =
    [
        "Ah. An audience. Shall we give them something memorable?",
        "Thou hast the front, Avatar. I shall supply the objections.",
        "Let them come. No sense doing their walking for them.",
        "I was hoping we could settle this over supper.",
        "Steady. We have faced worse with poorer equipment.",
        "My knees warned me of this!",
        "Let us make this brief. I have plans involving a chair.",
        "Mind the flanks. Enthusiasm is no substitute for watching them.",
        "Well, this ought to loosen my shoulders.",
        "Still a steady hand. That is what matters.",
        "Stay close. I have no wish to explain this to Lord British.",
        "Some creatures will not appreciate a peaceful retirement."
    ];

    private static readonly string[] _composedWarning =
    [
        "A little aid, Avatar? My potion pouch is empty.",
        "They have found their aim. I could use a moment.",
        "I am holding, but I would welcome some healing.",
        "No potions left, Avatar!",
        "A brief rest would do. Preferably behind something solid.",
        "That shall ache tomorrow. Help me ensure there is a tomorrow."
    ];

    private static readonly string[] _urgentWarning =
    [
        "Avatar, I need healing!",
        "Keep them off me! I have nothing left to mend this.",
        "Cover me! I must fall back!",
        "My friend, this grows serious. Help me!",
        "Too much blood lost. We need some distance!",
        "I have a few more songs to teach. A hand, please!"
    ];

    private static readonly string[] _reachedInMelee =
    [
        "A little room for the crossbow!",
        "Avatar, one of thine has taken an interest in me!",
        "Too close. Let us remedy that.",
        "I preferred thee at a distance!",
        "Kindly occupy this fellow while I reload.",
        "There is a reason I brought a crossbow!"
    ];

    private static readonly string[] _afterDifficultFight =
    [
        "Victory deserves a chair.",
        "We should replenish our potions before testing our luck again.",
        "A good tale, that. Once my hands stop shaking.",
        "Thou didst well. Next time, let us leave less to chance.",
        "I have earned something warm and a very soft chair."
    ];

    private HashSet<Mobile> _barkFoes, _barkMeleeFoes;
    private Map _barkMap;
    private long _fightQuietUntil, _nextBark, _nextOpeningBark, _nextComposedBark;
    private bool _difficultFight;
    private int _warningSpoken;

    private void ObserveCombatBarks()
    {
        if (BarkOwner() is not { } owner)
        {
            EndBarkFight();
            return;
        }
        PrepareBarkMap();

        long now = Core.TickCount;
        bool fighting = ObserveFoe(owner, Combatant);
        foreach (var aggression in Aggressors)
            if (!aggression.Expired && aggression.Attacker.Combatant == this)
                fighting |= ObserveFoe(owner, aggression.Attacker);

        _barkMeleeFoes?.RemoveWhere(foe => !MeleeThreat(owner, foe));
        bool meleeBark = false;
        foreach (var foe in Map.GetMobilesInRange<BaseCreature>(Location, 1))
        {
            if (!MeleeThreat(owner, foe))
                continue;
            fighting = true;
            (_barkFoes ??= []).Add(foe);
            if ((_barkMeleeFoes ??= []).Add(foe))
                meleeBark |= Random.Shared.Next(100) < 20;
        }

        var listener = BarkListener(owner);
        if (!fighting && now >= _fightQuietUntil)
        {
            bool difficultVictory = _difficultFight && _barkFoes?.Any(foe => foe.Hits <= 0) == true;
            EndBarkFight();
            if (difficultVictory && listener != null && now >= _nextBark && Random.Shared.Next(4) == 0)
                Bark(listener, _afterDifficultFight, now);
            return;
        }

        bool starting = fighting && _fightQuietUntil == 0;
        if (fighting)
            _fightQuietUntil = now + FightQuietMs;

        int warning = (long)Hits * 4 < HitsMax ? 2 : (long)Hits * 2 < HitsMax ? 1 : 0;
        _difficultFight |= warning > 0;
        if (listener == null)
            return;

        if (warning == 2 && _warningSpoken < 2 && MercenaryCare.Stock(this) == 0)
        {
            WarnUrgently(listener, now);
        }
        else if (now >= _nextBark)
        {
            if (warning == 1 && _warningSpoken == 0 && now >= _nextComposedBark && MercenaryCare.Stock(this) == 0)
            {
                _warningSpoken = 1;
                _nextComposedBark = now + RepeatBarkMs;
                Bark(listener, _composedWarning, now);
            }
            else if (starting && now >= _nextOpeningBark && Random.Shared.Next(100) < 10)
            {
                _nextOpeningBark = now + RepeatBarkMs;
                Bark(listener, _enteringCombat, now);
            }
            else if (meleeBark)
            {
                Bark(listener, _reachedInMelee, now);
            }
        }
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        base.OnDamage(amount, from, willKill);
        long remaining = (long)Hits - amount;
        if (!Recruited || amount <= 0 || willKill || remaining <= 0 || remaining * 4 >= HitsMax
            || BarkOwner() is not { } owner || !BarkHostile(owner, from))
            return;

        // The damage callback runs before hit points are lowered, even while Iolo is in hit recovery.
        PrepareBarkMap();
        long now = Core.TickCount;
        (_barkFoes ??= []).Add(from);
        _fightQuietUntil = now + FightQuietMs;
        _difficultFight = true;
        if (_warningSpoken < 2 && MercenaryCare.Stock(this) == 0 && BarkListener(owner) is { } listener)
            WarnUrgently(listener, now);
    }

    private PlayerMobile BarkOwner() =>
        Alive && Hits > 0 && !IsDeadPet && Map != null && Map != Map.Internal
        && ControlMaster is PlayerMobile { Alive: true, Deleted: false } owner
        && owner.Map == Map && MercenaryCare.IsCompanion(owner, this) ? owner : null;

    private NetState BarkListener(PlayerMobile owner) =>
        owner.NetState is { Running: true } connection && owner.InRange(this, 12) && owner.CanSee(this)
            ? connection : null;

    private void PrepareBarkMap()
    {
        if (_barkMap == Map)
            return;
        EndBarkFight();
        _barkMap = Map;
    }

    private bool ObserveFoe(PlayerMobile owner, Mobile foe)
    {
        if (!BarkHostile(owner, foe) || !InRange(foe, RangePerception))
            return false;
        (_barkFoes ??= []).Add(foe);
        return true;
    }

    private bool MeleeThreat(PlayerMobile owner, Mobile foe) =>
        foe is BaseCreature { RangeFight: <= 1 } && foe.Combatant == this && InRange(foe, 1)
        && BarkHostile(owner, foe) && InLOS(foe);

    private bool BarkHostile(PlayerMobile owner, Mobile foe) =>
        foe is { Hits: > 0 } && Math.Abs(Z - foe.Z) < 16 && ClassCombat.IsHostile(owner, foe);

    private void EndBarkFight()
    {
        _barkFoes?.Clear();
        _barkMeleeFoes?.Clear();
        _fightQuietUntil = 0;
        _difficultFight = false;
        _warningSpoken = 0;
    }

    private void Bark(NetState listener, string[] lines, long now)
    {
        PrivateOverheadMessage(MessageType.Regular, VoiceHue, false, lines[Random.Shared.Next(lines.Length)], listener);
        _nextBark = now + BarkPauseMs;
    }

    private void WarnUrgently(NetState listener, long now)
    {
        _warningSpoken = 2;
        Bark(listener, _urgentWarning, now);
    }
}
