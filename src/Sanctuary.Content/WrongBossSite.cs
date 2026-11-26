// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class WrongBossSite : Item
{
    private static readonly Point3D[] _kitchen = [new(5675, 529, 0), new(5691, 528, 0), new(5707, 524, 0)];
    private static readonly Point3D[] _court = [new(5703, 639, 0), new(5700, 639, 0), new(5706, 639, 0)];
    private static readonly WrongRole[] _cooks = [WrongRole.Wonk, WrongRole.Uruk, WrongRole.Glokurg];
    private static readonly WrongRole[] _courtRoles = [WrongRole.Goegork, WrongRole.Xoldred, WrongRole.Suldri];
    private static readonly HashSet<WrongBossSite> _sites = new();

    [SerializableField(0)] private bool _mageCourt;
    [SerializableField(1)] private WrongEncounter _first;
    [SerializableField(2)] private WrongEncounter _second;
    [SerializableField(3)] private WrongEncounter _third;
    [SerializableField(4)] private DateTime _respawnAt;
    [SerializableField(5)] private bool _engaged;
    [SerializableField(6, setter: "private")] private bool _mageReleased;

    private readonly Queue<(string Id, string Text)> _reactions = new();
    private readonly HashSet<WrongRole> _injuries = new();
    private WrongDeduction _deduction;
    private Mobile _target;
    private bool _linking, _petMourned;
    private long _lastThreat = Core.TickCount, _nextSpeech = Core.TickCount, _nextRemark = Core.TickCount;

    private WrongBossSite(bool mageCourt) : base(0x1B72)
    {
        _mageCourt = mageCourt;
        Movable = false;
        Visible = false;
        _sites.Add(this);
    }

    [AfterDeserialization]
    private void Restore() => _sites.Add(this);

    public static void Initialize()
    {
        EventSink.ServerStarted += () =>
        {
            Install(false, _kitchen[0]);
            Install(true, _court[0]);
            Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromSeconds(1), TickSites);
        };
    }

    private static void Install(bool court, Point3D point)
    {
        var map = Map.Trammel;
        foreach (var site in map.GetItemsAt<WrongBossSite>(point))
            if (site._mageCourt == court)
                return;
        new WrongBossSite(court).MoveToWorld(point, map);
    }

    private static void TickSites()
    {
        foreach (var site in _sites)
            if (site is { Deleted: false, Map: { } map } && map != Map.Internal)
                site.Tick(Core.TickCount);
    }

    public override void OnAfterDelete()
    {
        _sites.Remove(this);
        _first?.Delete();
        _second?.Delete();
        _third?.Delete();
        base.OnAfterDelete();
    }

    internal void Engage(WrongEncounter member, Mobile target)
    {
        if (_linking || !Member(member) || !Threat(target))
            return;

        bool firstContact = !_engaged || _target == null;
        Engaged = true;
        _target = target;
        _lastThreat = Core.TickCount;

        if (_mageCourt && firstContact && !MageReleased && Present(_first))
        {
            _deduction = null;
            _nextRemark = Core.TickCount + 10000;
            WrongBarks.Say(_first, "opening", "Darlings, we have a visitor. Remember what we practised.");
            _nextSpeech = Core.TickCount + 3000;
        }

        _linking = true;
        try
        {
            if (!_mageCourt || MageReleased)
                _first?.AttackWithPack(target);
            _second?.AttackWithPack(target);
            _third?.AttackWithPack(target);
        }
        finally
        {
            _linking = false;
        }
    }

    internal void Injured(WrongEncounter guard, int amount)
    {
        if (!_mageCourt || MageReleased || !_engaged || !Member(guard) || !Present(_first))
            return;

        int before = guard.Hits;
        int after = Math.Max(0, before - amount);
        if (amount < guard.HitsMax / 10 && (before * 100L <= guard.HitsMax * 65L || after * 100L > guard.HitsMax * 65L)
            || !_injuries.Add(guard.Role))
            return;

        _reactions.Enqueue(guard.Role == WrongRole.Xoldred
            ? ("xoldred-injury", "Easy, my brave boy. Give yourself room.")
            : ("suldri-injury", "Suldri! Away from there, sweetheart!"));
    }

    internal void Died(WrongEncounter member)
    {
        if (!Member(member))
            return;
        if (member == _first)
            First = null;
        else if (member == _second)
            Second = null;
        else
            Third = null;

        if (_mageCourt && member.IsGuard && !_petMourned)
        {
            _petMourned = true;
            _reactions.Clear();
            _reactions.Enqueue(("first-pet-death", "No… Oh, my darling. You did so well."));
        }
        if (!Present(_first) && !Present(_second) && !Present(_third))
            RespawnAt = Core.Now.AddSeconds(Utility.RandomMinMax(300, 420));
    }

    private bool Member(WrongEncounter member) => member == _first || member == _second || member == _third;

    private bool Threat(Mobile target) => target is { Alive: true, Deleted: false, Blessed: false }
        && target.Map == Map && Utility.InRange(Location, target.Location, _mageCourt ? 20 : 45)
        && (target is PlayerMobile || (target as BaseCreature)?.GetMaster() is PlayerMobile);

    private static bool Present(WrongEncounter member) => member is { Alive: true, Deleted: false };

    private void Tick(long now)
    {
        if (RespawnAt != default)
        {
            if (Core.Now >= RespawnAt)
                ResetPack();
            return;
        }
        if (!_engaged)
        {
            if (!Present(_first) || !Present(_second) || !Present(_third))
                ResetPack();
            return;
        }

        if (!Threat(_target))
        {
            _target = _second?.Combatant ?? _third?.Combatant ?? _first?.Combatant;
            if (!Threat(_target))
            {
                _target = null;
                if (now - _lastThreat >= 30000)
                    ResetPack();
                return;
            }
        }
        if (HasContact(_first) || HasContact(_second) || HasContact(_third))
            _lastThreat = now;
        else if (now - _lastThreat >= 30000)
        {
            ResetPack();
            return;
        }

        if (!_mageCourt || !Present(_first))
            return;

        if (now >= _nextSpeech && _reactions.TryDequeue(out var line))
        {
            Speak(line.Id, line.Text, now);
            return;
        }

        if (!MageReleased)
        {
            if (!Present(_second) && !Present(_third))
            {
                if (now >= _nextSpeech)
                {
                    Speak("joins-combat", "They trusted me to keep them safe. I brought you to them.", now);
                    MageReleased = true;
                    _first.SyncMage();
                    _first.AttackWithPack(_target);
                }
                return;
            }

            var observer = _target as PlayerMobile ?? (_target as BaseCreature)?.GetMaster() as PlayerMobile;
            if (_deduction == null && CanObserve(observer))
                _deduction = new WrongDeduction(observer);
            if (_deduction != null && CanObserve(_deduction.Subject))
            {
                ObserveFollowers();
                if (now >= _nextSpeech && _deduction.Next(now) is { } deduction)
                {
                    Speak(deduction.Id, deduction.Text, now);
                    return;
                }
            }
        }

        if (now >= _nextRemark)
        {
            _nextRemark = now + 10000;
            if (now >= _nextSpeech && Utility.Random(100) < 10)
            {
                if (MageReleased)
                    Speak("mage-combat", "You shall have my undivided attention.", now);
                else if (Present(_second))
                    Speak("xoldred-encouragement", "Beautifully executed, Xoldred. Though do mind your footing.", now);
                else
                    Speak("suldri-encouragement", "Steady, Suldri. Show our visitor how patiently we practised.", now);
            }
        }
    }

    private bool HasContact(WrongEncounter member) => Present(member)
        && (member.InRange(_target, 12) && member.InLOS(_target)
            || member.Combatant is { } target && Threat(target) && member.InRange(target, 12) && member.InLOS(target));

    private bool CanObserve(PlayerMobile player) => Threat(player) && player.InRange(_first, 14)
        && Math.Abs(player.Z - _first.Z) < 16 && _first.CanSee(player) && _first.InLOS(player);

    private void ObserveFollowers()
    {
        if (_deduction.SawSkeleton || !ClassCreation.Profiles.TryGet(_deduction.Subject.Serial, out var profile)
            || profile.Class != DiabloClass.Necromancer)
            return;

        foreach (var skeleton in Map.GetMobilesInRange<SkeletonGuardian>(_first.Location, 14))
        {
            if (skeleton.Alive && skeleton.GetMaster() == _deduction.Subject
                && Math.Abs(skeleton.Z - _first.Z) < 16 && _first.CanSee(skeleton) && _first.InLOS(skeleton))
            {
                _deduction.SawSkeleton = true;
                return;
            }
        }
    }

    private void Speak(string id, string text, long now)
    {
        WrongBarks.Say(_first, id, text);
        _nextSpeech = now + 2900;
    }

    internal static void ObserveAction(PlayerMobile player, CombatAbility ability)
    {
        foreach (var site in _sites)
            if (site._mageCourt && site._engaged && !site.MageReleased && Present(site._first) && site.CanObserve(player)
                && (site._deduction?.Subject == player || site._deduction == null
                    && (site._target == player || (site._target as BaseCreature)?.GetMaster() == player)))
            {
                site._deduction ??= new WrongDeduction(player);
                site._deduction.Observe(ability);
            }
    }

    private void ResetPack()
    {
        _first?.Delete();
        _second?.Delete();
        _third?.Delete();
        First = Second = Third = null;
        MageReleased = false;
        Engaged = false;
        RespawnAt = default;
        _lastThreat = Core.TickCount;
        _target = null;
        _deduction = null;
        _injuries.Clear();
        _reactions.Clear();
        _petMourned = false;
        var points = _mageCourt ? _court : _kitchen;
        var roles = _mageCourt ? _courtRoles : _cooks;
        First = Spawn(roles[0], points[0]);
        Second = Spawn(roles[1], points[1]);
        Third = Spawn(roles[2], points[2]);
    }

    private WrongEncounter Spawn(WrongRole role, Point3D point)
    {
        var member = new WrongEncounter(role, 0, this) { Home = point, HomeMap = Map, RangeHome = 0 };
        member.MoveToWorld(point, Map);
        member.OnAfterSpawn();
        return member;
    }
}
