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
            || reader.Remaining is not (SkillBindingProtocol.RequestLength - 5 or SkillBindingProtocol.UseLength - 5) || reader.ReadByte() != SkillBindingProtocol.Version) { return; }
        var action = (SkillBindingRequest)reader.ReadByte(); uint sequence = reader.ReadUInt32();
        var characterClass = (DiabloClass)reader.ReadByte(); int slot = reader.ReadByte(); var ability = (CombatAbility)reader.ReadByte();
        if (action > SkillBindingRequest.Use || reader.Remaining != (action == SkillBindingRequest.Use ? 9 : 0) || slot >= SkillBindingProtocol.Slots || !ClassAbilities.Valid(ability)) { return; }
        if (!_sessions.TryGetValue(player.Serial, out var session)) { _sessions.Add(player.Serial, session = new()); }
        if (unchecked((int)(sequence - session.Sequence)) <= 0) { return; }
        session.Sequence = sequence; long now = Core.TickCount;
        if (action == SkillBindingRequest.Poll)
        {
            if (now - session.NextPoll < 0) { return; }
            session.NextPoll = now + SkillBindingProtocol.PollMs / 2;
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
        // Quick cast targets the hovered corpse exactly; ordinary summon macros may still find the nearest one.
        if (ability == CombatAbility.RaiseSkeleton && (World.FindItem(target) is not Server.Items.Corpse corpse || !SkeletonPets.CanRaise(player, corpse))) { return false; }
        ClassCombat.Stop(player);
        if(ClassAbilities.ItemTarget(ability)){return NecromancerCombat.Begin(player,ability,target,point,Core.TickCount);}
        if (ClassCombat.CorpseCast(ability)) { return ClassCombat.BeginCorpse(player, ability, target, point, Core.TickCount); }
        return ability switch
        {
            CombatAbility.RaiseSkeleton => ClassCombat.CastSummon(player, target, Core.TickCount),
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
        var writer = new SpanWriter(stackalloc byte[SkillBindingProtocol.StateLength]);
        writer.Write((byte)0xBF); writer.Write((ushort)SkillBindingProtocol.StateLength); writer.Write(SkillBindingProtocol.SubCommand);
        writer.Write(SkillBindingProtocol.Version); writer.Write(_sessions.GetValueOrDefault(player.Serial)?.Sequence ?? 0);
        writer.Write((byte)profile.Class); writer.Write((byte)pair.Left); writer.Write((byte)pair.Right); foreach (byte value in mask) { writer.Write(value); }
        for (int slot = 0; slot < SkillBindingProtocol.Slots; slot++) { writer.Write((byte)SkillBindings.Instance.GetQuick(player.Serial, profile.Class, slot)); }
        player.NetState.Send(writer.Span);
    }
}
