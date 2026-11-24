// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class DestardAid
{
    internal static bool HasPatron(CampaignBoss boss, CampaignArenas.Arena arena, bool final)
    {
        foreach (var player in boss.Map.GetMobilesInRange<PlayerMobile>(boss.Location, 18))
        {
            if (!player.Alive || player.AccessLevel != AccessLevel.Player || CampaignArenas.At(player.Map, player.Location) != arena
                || DestardBounty.For(player) is not { } favor)
                continue;

            if (final ? favor.Second : favor.First)
                return true;
        }

        return false;
    }

    internal static DestardChampion Arrive(CampaignBoss boss, CampaignArenas.Arena arena, bool final)
    {
        // Stay next to Diablo so his melee attacks can reach the consort.
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            if (x == 0 && y == 0)
                continue;
            var point = new Point3D(boss.X + x, boss.Y + y, boss.Z);
            if (!arena.Bounds.Contains(point) || point.X < arena.Origin.X + 9 || !EncounterProfiles.Free(boss.Map, point)
                || !EncounterProfiles.ClearSight(boss.Map, boss.Location, point))
                continue;

            var dragon = new DestardChampion(boss, final);
            dragon.MoveToWorld(point, boss.Map);
            dragon.Direction = dragon.GetDirectionTo(boss);
            dragon.PublicOverheadMessage(MessageType.Regular, 0x59, false, "Stand aside, little morsel. This grievance is mine.");
            return dragon;
        }

        return null;
    }
}

[SerializationGenerator(0)]
public partial class DestardChampion : BaseCreature
{
    [SerializableField(0)] private CampaignBoss _boss;
    [SerializableField(1)] private DateTime _until;
    [SerializableField(2)] private bool _atMinimum;
    private long _nextSwing;

    public override bool CanRegenHits => false;
    public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness) => false;

    internal DestardChampion(CampaignBoss boss, bool final) : base(AIType.AI_Melee, FightMode.None, 10, 1)
    {
        _boss = boss;
        _until = final ? DateTime.MaxValue : Core.Now.AddMinutes(1);
        Name = DestardConsort.DragonName;
        Title = "the queen’s consort";
        Body = 12;
        BaseSoundID = 362;
        Hue = 0x455;
        NoKillAwards = true;
        CantWalk = true;
        SetStr(1000);
        SetHits(1000);
    }

    internal bool Protecting(CampaignBoss boss) => _boss == boss && boss is { Alive: true, Deleted: false }
        && !Deleted && Alive && Core.Now < _until && Map == boss.Map
        && CampaignArenas.At(boss.Map, boss.Location) is { Victory: CampaignProgress.Diablo } arena
        && CampaignArenas.At(Map, Location) == arena;

    public override void OnThink()
    {
        if (_boss is not { Alive: true, Deleted: false } || !Protecting(_boss))
        {
            Delete();
            return;
        }
        Direction = GetDirectionTo(_boss);
        if (InRange(_boss, 1) && Core.TickCount >= _nextSwing)
        {
            _nextSwing = Core.TickCount + 1200;
            Animate(4, 5, 1, true, false, 0);
        }
        base.OnThink();
    }

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (from != _boss || amount <= 0 || Hits <= 1)
            return;

        base.Damage(Math.Min(amount, Hits - 1), from, informMount, ignoreEvilOmen);
        if (Hits == 1)
        {
            AtMinimum = true;
            PublicOverheadMessage(MessageType.Regular, 0x59, false, "Any time now, little mortal. I am doing my part.");
        }
    }

    public override void OnHitsChange(int oldValue)
    {
        if (_atMinimum && Hits != 1 && !World.Loading)
            Hits = 1;
        base.OnHitsChange(oldValue);
    }

    public override void OnHeal(ref int amount, Mobile from) => amount = 0;
    public override bool OnBeforeDeath() => false;
}
