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
public partial class ArchbishopLazarus : CampaignBoss
{
    [SerializableField(0)] private bool _challenged;
    [SerializableField(1)] private int _fallenGuards;
    [SerializableField(2)] private int _stop = -1;
    [SerializableField(3)] private DateTime _nextTravel;
    [SerializableField(4)] private DateTime _nextSummon;
    [SerializableField(5, setter: "private")] private List<MausoleumDemon> _retinue = [];
    private static readonly ScriptedDialogue.Node[] Speech =
    [
        new("Abandon your foolish quest! All that awaits you is the wrath of my master! " +
            "You have come too late, Avatar. I lacked only an offering, and you have walked willingly to my altar.",
            [], new("Archbishop Lazarus", (ushort)NpcPortrait.Lazarus))
    ];

    internal ArchbishopLazarus(CampaignArenas.Arena arena) : base(arena)
    {
        Blessed = true;
        CantWalk = true;
        Direction = Direction.Down;
    }

    protected override CreatureTraits EncounterTraits(DungeonProfile profile) =>
        new("Deceit", false, false, Leader, false, true, 1, 0, 0, 100, 0);

    internal static CampaignArenas.Arena Hall => CampaignArenas.All[1];
    public override bool IsInvulnerable => Blessed;
    protected override bool IsRefuge(Point3D point) => false;
    internal override int HomeLeashRadius => WorldDecorationGraphics.MausoleumSize;
    protected override bool CanCastEncounterBolt => false;
    internal override bool IsEnemyIgnoringLeash(Mobile mobile) => _fallenGuards == 3
        && mobile != null && mobile.Map == Map && Hall.Bounds.Contains(mobile.Location) && base.IsEnemyIgnoringLeash(mobile);

    // Sector AI sleeps when Lazarus teleports away from all clients. The ritual
    // clock must keep running at the remote circle while a player is in the hall.
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500), () =>
    {
        if (Hall.Boss is ArchbishopLazarus { Alive: true, Deleted: false } boss)
            boss.Advance(Core.Now);
    });

    public override void OnThink() { } // The ritual replaces the shared campaign ground strike.

    internal void PlaceBodyguards()
    {
        for (int role = 0; role < 2; role++)
        {
            if ((_fallenGuards & (1 << role)) != 0) continue;
            var post = role == 0 ? Hall.Offset(42, 42) : Hall.Offset(48, 44);
            var guard = _retinue.FirstOrDefault(d => d is { Deleted: false } && d.Role == role);
            if (guard == null)
                Spawn(role, post);
            else if (!_challenged)
            {
                // Restore waiting guards even when an older save has the correct home but a displaced guard.
                guard.CantWalk = guard.Blessed = true;
                guard.Home = post;
                if (guard.Location != post || guard.Map != Map) guard.MoveToWorld(post, Map);
            }
        }
    }

    private void Spawn(int role, Point3D at)
    {
        var demon = new MausoleumDemon(this, role) { Home = at, HomeMap = Map, RangeHome = Hall.Width };
        demon.CantWalk = demon.Frozen = demon.Blessed = role < 2 && !_challenged;
        demon.MoveToWorld(at, Map);
        _retinue.Add(demon);
        this.MarkDirty();
    }

    internal void Greet(PlayerMobile player)
    {
        if (_challenged) return;
        ScriptedDialogue.Show(player, this, Speech, onLeave: (reader, _) => Challenge(reader));
    }

    internal void Challenge(PlayerMobile player)
    {
        if (_challenged || Deleted || !Alive || !player.Alive || CampaignArenas.At(player.Map, player.Location) != Hall) return;
        if (player.Hidden && player.AccessLevel > AccessLevel.Player) return; // A GM inspecting the tableau is a spectator.
        Challenged = true;
        foreach (var guard in _retinue.Where(d => d is { Deleted: false }))
        {
            guard.CantWalk = guard.Frozen = guard.Blessed = false;
            guard.Combatant = player;
        }
    }

    internal void Lost(MausoleumDemon demon, bool killed)
    {
        if (!_retinue.Remove(demon)) return;
        this.MarkDirty();
        if (killed && demon.IsBodyguard)
            FallenGuards |= 1 << demon.Role;
        if (killed && _fallenGuards == 3 && _stop == -1)
            Advance(Core.Now);
    }

    internal bool Warded(ResistanceType element) => _stop >= 0 && !Deleted && Map == Map.Trammel
        && LazarusMausoleum.Circles[_stop].Element == element && InRange(LazarusMausoleum.Circle(Hall, _stop), 1)
        && _retinue.Any(d => d is { Deleted: false, Alive: true } && d.Map == Map && d.Role == _stop + 2);

    public override int GetResistance(ResistanceType type) => Warded(type) ? 100 : base.GetResistance(type);
    public override Poison PoisonImmune => Warded(ResistanceType.Poison) ? Poison.Lethal : null;

    internal void Advance(DateTime now)
    {
        if (_fallenGuards != 3)
        {
            PlaceBodyguards();
            return;
        }
        PlayerMobile player = null;
        foreach (var p in Map.GetMobilesInRange<PlayerMobile>(Hall.Center, Hall.Width))
            if (p.Alive && p.NetState?.Running == true && !p.Hidden && Hall.Bounds.Contains(p.Location)
                && (player == null || this.GetDistanceToSqrt(p) < this.GetDistanceToSqrt(player))) player = p;
        if (player == null) return;

        if (now >= _nextTravel)
        {
            int next = (_stop + 1) % LazarusMausoleum.Circles.Length;
            var circle = LazarusMausoleum.Circle(Hall, next);
            if (FreeNear(circle, 0, 1) is not { } landing) return;
            FixedEffect(0x3728, 10, 12);
            MoveToWorld(landing, Map);
            Home = landing;
            Stop = next;
            Blessed = false;
            NextSummon = now.AddSeconds(4);
            NextTravel = now.AddSeconds(18);
            Direction = GetDirectionTo(player);
            Effects.SendLocationEffect(circle, Map, 0x373A, 16, 9, LazarusMausoleum.Circles[next].Hue);
            Animate(12, 6, 1, true, false, 0);
            PlaySound(SpellEffectSounds.BoneSpiritRelease);
            new MausoleumSpirit(this, player).Launch();
        }
        else if (now >= _nextSummon && FreeNear(Location, 2, 4) is { } spot)
        {
            Spawn(_stop + 2, spot);
            NextSummon = DateTime.MaxValue;
            PublicOverheadMessage(MessageType.Regular, LazarusMausoleum.Circles[_stop].Hue, false,
                "The guardian binds its life to the seal.");
        }
    }

    private Point3D? FreeNear(Point3D center, int minimum, int maximum)
    {
        for (int radius = minimum; radius <= maximum; radius++)
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        {
            if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
            var at = new Point3D(center.X + x, center.Y + y, center.Z);
            if (Map.CanFit(at.X, at.Y, at.Z, 16, checkMobiles: true, requireSurface: true)) return at;
        }
        return null;
    }

    private void DismissRetinue()
    {
        foreach (var d in _retinue.ToArray()) d?.Delete();
        _retinue.Clear();
    }

    public override void OnDelete()
    {
        DismissRetinue();
        base.OnDelete();
    }

    public override void OnDeath(Container corpse)
    {
        DismissRetinue();
        base.OnDeath(corpse);
    }
}

internal sealed class MausoleumSpirit(ArchbishopLazarus caster, PlayerMobile target)
{
    private double _x = caster.X, _y = caster.Y;
    private double _heading = Math.Atan2(target.Y - caster.Y, target.X - caster.X);
    private int _steps;
    private TimerExecutionToken _flight;

    internal void Launch() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), Tick, out _flight);

    private void Tick()
    {
        var map = caster.Map;
        if (caster.Deleted || !caster.Alive || target.Deleted || !target.Alive || target.Map != map
            || target.NetState?.Running != true || CampaignArenas.At(map, target.Location) != ArchbishopLazarus.Hall
            || ++_steps > 120)
        {
            _flight.Cancel();
            return;
        }

        Point3D Position() => new((int)Math.Round(_x), (int)Math.Round(_y), caster.Z);
        var previous = Position();
        if (_steps > 3)
        {
            double desired = Math.Atan2(target.Y - _y, target.X - _x);
            _heading += Math.Clamp(Math.IEEERemainder(desired - _heading, 2 * Math.PI), -0.25, 0.25);
        }
        double vx = Math.Cos(_heading), vy = Math.Sin(_heading);
        _x += vx;
        _y += vy;
        var next = Position();
        if (!EncounterProfiles.ClearSight(map, previous, next))
        {
            Burst(previous);
            return;
        }
        int frames = SpellEffectGraphics.NecromancerFrames;
        int graphic = SpellEffectGraphics.BoneSpiritFlight + frames * SpellEffectGraphics.ProjectileFacing(vx, vy) + _steps % frames;
        Effects.SendLocationEffect(next, map, graphic, 3);
        if (target.InRange(next, 0) && caster.CanBeHarmful(target, false))
        {
            double damage = NamedElements.Damage(caster, target, Utility.RandomMinMax(caster.Basis.MinDamage, caster.Basis.MaxDamage), "mag");
            caster.DoHarmful(target);
            SkillHealth.TypedDamage(caster, target, NamedElements.To256(damage), DirectDamageParts.From(new GearDamage(0, 0, 0, 0, 0, Magic: damage)));
            Burst(next);
        }
    }

    private void Burst(Point3D point)
    {
        Effects.SendLocationEffect(point, caster.Map, SpellEffectGraphics.BoneSpiritBurst, SpellEffectGraphics.NecromancerFrames);
        Effects.PlaySound(point, caster.Map, SpellEffectSounds.BoneSpiritImpact);
        _flight.Cancel();
    }
}
