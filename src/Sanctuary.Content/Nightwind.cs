// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class Nightwind : Merydwen
{
    private long _nextScoutShot;

    [Constructible]
    public Nightwind()
    {
        Name = "Nightwind";
        Title = "the ranger";
        Race = Race.Elf;
        Body = 0x25E;
        Hue = 0x83EA;
        HairItemID = 0x2FC3;
        HairHue = 0x0455;
        FindItemOnLayer(Layer.Helm)?.Delete();
        AddItem(new Cloak(0x0591));
    }

    internal override ushort Portrait => (ushort)NpcPortrait.Nightwind;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => Recruited ? _travelling : _heartwood;

    private static readonly ScriptedDialogue.Node[] _heartwood =
    [
        new("Welcome to Heartwood. Thou hast walked a long way to reach us.", [])
    ];

    private static readonly ScriptedDialogue.Node[] _travelling =
    [
        new("I shall keep watch ahead. If I see a scout, I can put an arrow at their feet before they get far.",
            [new("Equipment", 0, (owner, companion) =>
            {
                if (companion.CanPaperdollBeOpenedBy(owner))
                    companion.DisplayPaperdollTo(owner);
            })])
    ];

    internal override bool CanTalkTo(PlayerMobile owner) => Recruited ? base.CanTalkTo(owner)
        : YewStory.HasHeartwoodAccess(owner);

    internal override void BeginConversation(PlayerMobile owner)
    {
        if (Recruited)
        {
            if (YewStory.Has(owner, YewProgress.Taming))
                base.BeginConversation(owner);
            else
                ScriptedDialogue.Show(owner, this,
                [
                    new(_travelling[0].Line,
                        [.. _travelling[0].Replies, new("Animal Taming", 0, (player, _) => Teach(player))])
                ]);
        }
        else if (YewStory.Has(owner, YewProgress.ReturnedRing))
            Offer(owner, "I keep Father's ring with me now. I had wondered about setting out beyond these woods myself.");
        else if (YewStory.Find(owner, YewKeepsakeKind.Ring) != null)
            ScriptedDialogue.Show(owner, this,
            [
                new("Sister Meriel sent thee? I have known her since I was small.", [new("Wedding Ring", 0, (player, _) => ReturnRing(player))])
            ]);
        else
            base.BeginConversation(owner);
    }

    private void ReturnRing(PlayerMobile owner)
    {
        if (YewStory.Find(owner, YewKeepsakeKind.Ring) is not { } ring || !YewStory.Add(owner, YewProgress.ReturnedRing))
            return;
        ring.Delete();
        Offer(owner, "Father's ring. I remember tracing that nick in the gold when I sat on his knee. Thank thee for bringing it home. Mother would have wanted to know.");
    }

    private void Offer(PlayerMobile owner, string line)
    {
        var replies = new List<ScriptedDialogue.Reply>();
        if (!YewStory.Has(owner, YewProgress.Taming))
            replies.Add(new("Animal Taming", 0, (player, _) => Teach(player)));
        if (Recruited)
            replies.AddRange(_travelling[0].Replies);
        else if (!MercenaryCare.HasContract<Nightwind>(owner))
            replies.Add(new("Travel Together", 0, (player, _) => Invite(player)));
        ScriptedDialogue.Show(owner, this, [new(line, [.. replies])]);
    }

    private void Teach(PlayerMobile owner)
    {
        if (!YewStory.Has(owner, YewProgress.ReturnedRing) || !YewStory.Add(owner, YewProgress.Taming))
            return;
        var skill = owner.Skills.AnimalTaming;
        skill.SetLockNoRelay(SkillLock.Up);
        skill.Base = Math.Max(skill.Base, 30);
        owner.SendMessage("Nightwind has taught you Animal Taming.");
        Offer(owner, "There is an art to asking an animal for its trust. We seldom teach it beyond Heartwood. Begin with the creatures that already know the sound of footsteps.");
    }

    private void Invite(PlayerMobile owner)
    {
        var previous = MercenaryCare.ActiveStoryCompanion(owner);
        string line = previous == null ? "I would travel with thee, if thou wilt have me."
            : $"I would travel with thee. {previous.Name} can wait for thee at Ayila's inn in Britain.";
        ScriptedDialogue.Show(owner, this,
        [
            new(line, [new("Accept", 0, (player, _) => Recruit(player)), new("Later", 0, (player, _) => BeginConversation(player))])
        ]);
    }

    private void Recruit(PlayerMobile owner)
    {
        if (!ScriptedDialogue.CanTalk(owner, this) || !YewStory.Has(owner, YewProgress.ReturnedRing)
            || MercenaryCare.HasContract<Nightwind>(owner) || !MountTravel.FindLanding(owner, out var spot))
            return;

        var companion = new Nightwind();
        companion.PrepareRangedCompanion();
        if (!companion.SetControlMaster(owner))
        {
            companion.Delete();
            return;
        }
        var previous = MercenaryCare.ActiveStoryCompanion(owner);
        if (previous != null && !MercenaryCare.ParkAtInn(owner, previous))
        {
            companion.Delete();
            return;
        }

        companion.MoveToWorld(spot, owner.Map);
        companion.ControlTarget = owner;
        companion.ControlOrder = OrderType.Guard;
        if (!MercenaryCare.Register(owner, companion, ClassStats.Level(owner)))
        {
            companion.Delete();
            return;
        }
        ScriptedDialogue.SendStoryState(owner);
        ScriptedDialogue.Show(owner, companion, _travelling);
    }

    public override void OnThink()
    {
        base.OnThink();
        if (Recruited && !IsDeadPet && Weapon is BaseRanged bow && Combatant == null && Core.TickCount >= _nextScoutShot
            && ArcherOwner(bow) is { } owner)
            CheckForScout(owner, bow);
    }

    private void CheckForScout(PlayerMobile owner, BaseRanged bow)
    {
        _nextScoutShot = Core.TickCount + 450;
        OrcFortScout scout = null;
        double distance = double.MaxValue;
        foreach (var target in Map.GetMobilesInRange<OrcFortScout>(Location, 13))
        {
            double candidate = Utility.GetDistanceToSqrt(Location, target.Location);
            if (candidate >= distance || target is not { Alive: true, Deleted: false, FightingNormally: false }
                || Math.Abs(Z - target.Z) > 12 || !InLOS(target) || !ClassCombat.IsHostile(owner, target))
                continue;
            scout = target;
            distance = candidate;
        }
        if (scout == null)
            return;

        _nextScoutShot = Core.TickCount + 10000;
        Direction = GetDirectionTo(scout);
        Say("Scout ahead. Hold a moment.");
        bow.PlaySwingAnimation(this);
        MovingParticles(scout, bow.EffectID, 18, 1, false, false, 0, 0, 0, 0, 0, EffectLayer.RightHand, 0);
        PlaySound(SpellEffectSounds.AmazonBowRelease);
        scout.Paralyze(TimeSpan.FromSeconds(4));
        scout.AIObject?.ClearMoveIntent();
    }
}
