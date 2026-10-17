// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class SkillBindings : GenericPersistence
{
    private readonly Dictionary<Serial, (CombatAbility Left, CombatAbility Right)> _bindings = new();
    public static SkillBindings Instance { get; private set; }
    public SkillBindings() : base("SanctuaryBindings", 101) { }
    public static void Configure()
    {
        Instance = new SkillBindings();
        CommandSystem.Register("Bindings", AccessLevel.Player, args => BindingGump.DisplayTo(args.Mobile));
        CommandSystem.Register("Pets", AccessLevel.Player, args => PetStatsGump.DisplayTo(args.Mobile));
    }
    public (CombatAbility Left, CombatAbility Right) Get(PlayerMobile player)
        => _bindings.TryGetValue(player.Serial, out var bindings) ? bindings
            : (CombatAbility.BasicAttack, ClassCreation.Profiles.TryGet(player.Serial, out var profile)
                && profile.Class == DiabloClass.Necromancer ? CombatAbility.Teeth : CombatAbility.None);
    public bool Assign(PlayerMobile player, bool left, CombatAbility ability)
    {
        if (player == null || !player.Alive || !ClassCombat.Enabled(player)
            || ability != CombatAbility.None && !ClassCombat.Available(player, ability))
        {
            return false;
        }
        var bindings = Get(player);
        _bindings[player.Serial] = left ? (ability, bindings.Right) : (bindings.Left, ability);
        ClassCombat.Stop(player);
        var session = ClassCombat.For(player);
        session.Left = left ? ability : bindings.Left;
        session.Right = left ? bindings.Right : ability;
        return true;
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0);
        writer.Write(_bindings.Count);
        foreach (var (serial, bindings) in _bindings)
        {
            writer.Write(serial.Value);
            writer.Write((byte)bindings.Left);
            writer.Write((byte)bindings.Right);
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0)
        {
            throw new InvalidDataException("Unsupported skill bindings version.");
        }
        int count = reader.ReadInt();
        if (count < 0)
        {
            throw new InvalidDataException("Invalid bindings count.");
        }
        _bindings.Clear();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt();
            var left = (CombatAbility)reader.ReadByte();
            var right = (CombatAbility)reader.ReadByte();
            if (left > CombatAbility.RaiseSkeleton || right > CombatAbility.RaiseSkeleton)
            {
                throw new InvalidDataException("Invalid saved skill binding.");
            }
            _bindings.Add(serial, (left, right));
        }
    }
    public override void PostDeserialize()
    {
        var deleted = new List<Serial>();
        foreach (var serial in _bindings.Keys)
        {
            if (World.FindMobile(serial) is not PlayerMobile { Deleted: false })
            {
                deleted.Add(serial);
            }
        }
        foreach (var serial in deleted)
        {
            _bindings.Remove(serial);
        }
    }
}

public sealed class BindingGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    public override bool Singleton => true;
    private BindingGump(PlayerMobile owner) : base(80, 70) => _owner = owner;
    public static void DisplayTo(Mobile mobile)
    {
        if (mobile is PlayerMobile { NetState: not null } player && ClassCombat.Enabled(player))
        {
            player.SendGump(new BindingGump(player));
        }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var bindings = SkillBindings.Instance.Get(_owner);
        builder.AddBackground(0, 0, 440, 300, 5054);
        builder.AddLabel(20, 15, 0x481, "Mouse skills");
        builder.AddLabel(20, 42, 0x47E, $"Left: {CombatProtocol.Label(bindings.Left)}    Right: {CombatProtocol.Label(bindings.Right)}");
        string[] names = ["None", "Basic attack", "Teeth", "Raise Skeleton"];
        for (int i = 0; i < names.Length; i++)
        {
            int y = 78 + 36 * i;
            builder.AddLabel(20, y, 0x47E, names[i]);
            if (i == 0 || ClassCombat.Available(_owner, (CombatAbility)i))
            {
                builder.AddButton(220, y, 4005, 4007, 100 + i);
                builder.AddLabel(252, y, 0x481, "Left");
                builder.AddButton(310, y, 4005, 4007, 200 + i);
                builder.AddLabel(342, y, 0x481, "Right");
            }
            else
            {
                builder.AddLabel(220, y, 0x3B2, "Not available");
            }
        }
        builder.AddHtml(20, 230, 400, 54,
            "<BASEFONT COLOR=#FFFFFF>Hotkey / macro: Say [Cast RaiseSkeleton<BR>Raise Skeleton uses a hovered or nearby corpse.</BASEFONT>",
            background: false, scrollbar: false);
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        int button = info.ButtonID;
        if (sender.Mobile != _owner || button is not (>= 100 and <= 103 or >= 200 and <= 203))
        {
            return;
        }
        if (!SkillBindings.Instance.Assign(_owner, button < 200, (CombatAbility)(button % 100)))
        {
            _owner.SendMessage("That skill is not available.");
        }
        DisplayTo(_owner);
    }
}
