// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class SkillBindingNetwork
{
    private sealed class Session
    {
        internal uint Sequence;
        internal byte Version;
        internal bool WantsSummonCounts, WantsExtendedSummonCounts;
        internal long NextPoll = Core.TickCount, NextEdit = Core.TickCount;
    }
    private static readonly Dictionary<Serial, Session> _sessions = new();
    public static void Configure()
    {
        EventSink.Disconnected += mobile => _sessions.Remove(mobile.Serial);
        EventSink.Logout += mobile => _sessions.Remove(mobile.Serial);
    }
    public static unsafe void Initialize() => IncomingExtendedCommandPackets.RegisterExtended(SkillBindingProtocol.SubCommand, true, &Receive);
    private static void Receive(NetState state, SpanReader reader)
    {
        if (state.Mobile is not PlayerMobile player || !ClassCombat.Enabled(player)
            || reader.Remaining is not (SkillBindingProtocol.RequestLength - 5 or SkillBindingProtocol.UseLength - 5)) { return; }
        byte version = reader.ReadByte();
        if (version is not (SkillBindingProtocol.LegacyVersion or SkillBindingProtocol.Version)) { return; }
        var action = (SkillBindingRequest)reader.ReadByte(); uint sequence = reader.ReadUInt32();
        var characterClass = (DiabloClass)reader.ReadByte(); int slot = reader.ReadByte(); var ability = (CombatAbility)reader.ReadByte();
        if (action > SkillBindingRequest.Use || reader.Remaining != (action == SkillBindingRequest.Use ? 9 : 0) || slot >= SkillBindingProtocol.Slots || !ClassAbilities.Valid(ability)) { return; }
        if (!_sessions.TryGetValue(player.Serial, out var session)) { _sessions.Add(player.Serial, session = new()); }
        if (unchecked((int)(sequence - session.Sequence)) <= 0) { return; }
        session.Sequence = sequence; session.Version = version; long now = Core.TickCount;
        if (action == SkillBindingRequest.Poll)
        {
            if (now - session.NextPoll < 0) { return; }
            session.NextPoll = now + SkillBindingProtocol.PollMs / 2;
            session.WantsSummonCounts = version == SkillBindingProtocol.Version && slot == SkillBindingProtocol.SummonCountsPollFlag;
            session.WantsExtendedSummonCounts = session.WantsSummonCounts && ability == SkillBindingProtocol.ExtendedSummonCountsCapability;
        }
        else
        {
            if (now - session.NextEdit < 0) { return; }
            session.NextEdit = now + 60;
            if (action == SkillBindingRequest.Use)
            {
                var target = (Serial)reader.ReadUInt32(); var point = new Point3D(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadSByte());
                Use(player, characterClass, slot, ability, target, point);
            }
            else { Apply(player, characterClass, action, slot, ability); }
        }
        Send(player);
    }
    internal static bool Apply(PlayerMobile player, DiabloClass expectedClass, SkillBindingRequest action, int slot, CombatAbility ability)
    {
        if (!ClassProfessions.Ready(player) || !ClassCreation.Profiles.TryGet(player.Serial, out var profile) || profile.Class != expectedClass) { return false; }
        return action switch
        {
            SkillBindingRequest.Left => SkillBindings.Instance.Assign(player, true, ability),
            SkillBindingRequest.Right => SkillBindings.Instance.Assign(player, false, ability),
            SkillBindingRequest.Quick => SkillBindings.Instance.AssignQuick(player, slot, ability),
            _ => false
        };
    }
    internal static bool Use(PlayerMobile player, DiabloClass expectedClass, int slot, CombatAbility quoted, Serial target, Point3D point)
    {
        if (!ClassProfessions.Ready(player) || player.Target != null
            || !ClassCreation.Profiles.TryGet(player.Serial, out var profile) || profile.Class != expectedClass
            || slot is < 0 or >= SkillBindingProtocol.Slots) { return false; }
        var ability = SkillBindings.Instance.GetQuick(player.Serial, profile.Class, slot);
        if (ability == CombatAbility.None || ability != quoted || !ClassCombat.Available(player, ability)) { return false; }
        if (!AlliedOverlap.Allowed(player))
            ClassCombat.WarnOverlap(player, ClassCombat.For(player), Core.TickCount);
        if (ability == CombatAbility.RaiseSkeleton)
            return ClassCombat.CastSummon(player, Core.TickCount);
        if (ability == CombatAbility.RaiseSkeletalMage)
            return NecromancerCombat.Begin(player, ability, Serial.Zero, player.Location, Core.TickCount);
        ClassCombat.Stop(player);
        if(ability==CombatAbility.Telekinesis){return SorceressCombat.Begin(player,ability,target,World.FindEntity(target)?.Location??point,Core.TickCount);}
        if(ClassAbilities.ItemTarget(ability)){return NecromancerCombat.Begin(player,ability,target,point,Core.TickCount);}
        if (ClassCombat.CorpseCast(ability)) { return ClassCombat.BeginCorpse(player, ability, target, point, Core.TickCount); }
        return ability switch
        {
            CombatAbility.Teleport => Teleportation.Begin(player, point, Core.TickCount),
            CombatAbility.Teeth => ClassCombat.Perform(player, ability, null, point, Core.TickCount),
            _ => ClassCombat.Perform(player, ability, World.FindMobile(target), point, Core.TickCount)
        };
    }
    public static void Send(PlayerMobile player)
    {
        if (player.NetState?.Running != true || !ClassCreation.Profiles.TryGet(player.Serial, out var profile)) { return; }
        Span<byte> mask = stackalloc byte[ClassAbilities.MaskBytes];
        mask.Clear();
        for (int id = 1; id < ClassAbilities.Count; id++)
        { if (ClassCombat.Available(player, (CombatAbility)id)) { mask[id / 8] |= (byte)(1 << (id % 8)); } }
        var pair = SkillBindings.Instance.Get(player);
        var session = _sessions.GetValueOrDefault(player.Serial);
        bool includeMana = session?.Version == SkillBindingProtocol.Version;
        bool includeExtendedSummons = includeMana && session.WantsExtendedSummonCounts
            && profile.Class is DiabloClass.Necromancer or DiabloClass.Warlock;
        bool includeSummons = includeMana && session.WantsSummonCounts
            && profile.Class is DiabloClass.Necromancer or DiabloClass.Druid;
        int length = includeExtendedSummons ? SkillBindingProtocol.ExtendedSummonStateLength
            : includeSummons ? SkillBindingProtocol.SummonStateLength
            : includeMana ? SkillBindingProtocol.StateLength : SkillBindingProtocol.LegacyStateLength;
        var writer = new SpanWriter(stackalloc byte[length]);
        writer.Write((byte)0xBF); writer.Write((ushort)length); writer.Write(SkillBindingProtocol.SubCommand);
        writer.Write(includeExtendedSummons ? SkillBindingProtocol.ExtendedSummonStateVersion
            : includeSummons ? SkillBindingProtocol.SummonStateVersion
            : includeMana ? SkillBindingProtocol.Version : SkillBindingProtocol.LegacyVersion);
        writer.Write(session?.Sequence ?? 0);
        writer.Write((byte)profile.Class); writer.Write((byte)pair.Left); writer.Write((byte)pair.Right); foreach (byte value in mask) { writer.Write(value); }
        int mana = includeMana ? NecromancerEffects.ManaBalance256(player) : 0;
        ushort lowMana = 0;
        if (includeMana)
            lowMana = (ushort)(ManaBit(player, mask, mana, pair.Left, 0) | ManaBit(player, mask, mana, pair.Right, 1));
        for (int slot = 0; slot < SkillBindingProtocol.Slots; slot++)
        {
            var ability = SkillBindings.Instance.GetQuick(player.Serial, profile.Class, slot);
            writer.Write((byte)ability);
            if (includeMana)
                lowMana |= ManaBit(player, mask, mana, ability, slot + 2);
        }
        if (includeMana)
            writer.Write(lowMana);
        if (includeSummons && profile.Class == DiabloClass.Necromancer)
        {
            var counts = CountSummons(player, includeExtendedSummons);
            writer.Write(counts.Warriors);
            writer.Write(counts.Mages);
            writer.Write(counts.Revives);
            if (includeExtendedSummons)
                writer.Write((byte)(counts.Golem ?? CombatAbility.None));
        }
        else if (includeSummons)
        {
            var counts = CountDruidSummons(player);
            writer.Write(counts.Ravens);
            writer.Write(counts.SpiritWolves);
            writer.Write(counts.DireWolves);
        }
        else if (includeExtendedSummons)
        {
            var counts = CountWarlockSummons(player);
            writer.Write(counts.LesserDaemons);
            writer.Write(counts.Succubi);
            writer.Write(counts.Defilers);
            writer.Write(counts.BoundDemons);
        }
        player.NetState.Send(writer.Span);
    }

    internal static NecromancerSummonCounts CountSummons(PlayerMobile player, bool includeGolem = false)
    {
        int warriors = 0, mages = 0, revives = 0;
        CombatAbility? golem = includeGolem ? CombatAbility.None : null;
        foreach (var follower in player.AllFollowers ?? [])
        {
            if (follower is not BaseCreature { Alive: true, Deleted: false, IsDeadBondedPet: false } pet
                || pet.ControlMaster != player)
                continue;
            if (ReviveNativeActivation.State(pet)?.Owner == player.Serial.Value)
                revives++;
            else if (pet is SkeletonGuardian skeleton)
            {
                switch (NecromancerSkeletons.Kind(skeleton))
                {
                    case CombatAbility.RaiseSkeleton: warriors++; break;
                    case CombatAbility.RaiseSkeletalMage: mages++; break;
                    default:
                        if (includeGolem && player.Map != null && player.Map != Map.Internal && skeleton.Map == player.Map)
                        {
                            var kind = NecromancerGolems.Kind(skeleton);
                            if (kind != CombatAbility.None)
                                golem = kind;
                        }
                        break;
                }
            }
        }

        return new((byte)Math.Min(warriors, byte.MaxValue), (byte)Math.Min(mages, byte.MaxValue),
            (byte)Math.Min(revives, byte.MaxValue), golem);
    }

    internal static DruidSummonCounts CountDruidSummons(PlayerMobile player)
    {
        if (player.Map == null || player.Map == Map.Internal)
        {
            return default;
        }

        int ravens = 0, spiritWolves = 0, direWolves = 0;
        foreach (var follower in player.AllFollowers ?? [])
        {
            if (follower is not BaseCreature { Alive: true, Deleted: false, IsDeadBondedPet: false } pet
                || pet.ControlMaster != player || pet.Map != player.Map)
                continue;

            switch (pet)
            {
                case DruidRavenBody raven: ravens += raven.FlockSize; break;
                case DruidSpiritWolfBody: spiritWolves++; break;
                case DruidDireWolfBody: direWolves++; break;
            }
        }

        return new((byte)Math.Min(ravens, byte.MaxValue), (byte)Math.Min(spiritWolves, byte.MaxValue),
            (byte)Math.Min(direWolves, byte.MaxValue));
    }

    internal static WarlockSummonCounts CountWarlockSummons(PlayerMobile player)
    {
        if (player.Map == null || player.Map == Map.Internal)
        {
            return default;
        }

        int lesserDaemons = 0, succubi = 0, defilers = 0, boundDemons = 0;
        foreach (var follower in player.AllFollowers ?? [])
        {
            if (follower is not BaseCreature { Alive: true, Deleted: false, IsDeadBondedPet: false } pet
                || pet.ControlMaster != player || pet.Map != player.Map)
                continue;

            switch (pet)
            {
                case WarlockGoatmanBody: lesserDaemons++; break;
                case WarlockTaintedBody: succubi++; break;
                case WarlockDefilerBody: defilers++; break;
                case var _ when BoundDemonHosts.State(pet) != null: boundDemons++; break;
            }
        }

        return new((byte)Math.Min(lesserDaemons, byte.MaxValue), (byte)Math.Min(succubi, byte.MaxValue),
            (byte)Math.Min(defilers, byte.MaxValue), (byte)Math.Min(boundDemons, byte.MaxValue));
    }

    private static ushort ManaBit(PlayerMobile player, ReadOnlySpan<byte> available, int mana, CombatAbility ability, int position)
    {
        int id = (int)ability;
        return (available[id / 8] & 1 << (id % 8)) != 0 && mana < SkillMechanics.CastManaCost256(player, ability)
            ? (ushort)(1 << position) : (ushort)0;
    }
}
