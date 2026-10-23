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
    private Dictionary<(Serial Owner, DiabloClass Class), (CombatAbility Left, CombatAbility Right)> _bindings = new();
    private Dictionary<(Serial Owner, DiabloClass Class), CombatAbility[]> _quick = new();
    private Dictionary<Serial, (CombatAbility Left, CombatAbility Right)> _legacy = new();
    public static SkillBindings Instance { get; private set; }
    public SkillBindings() : base("SanctuaryBindings", 101) { }
    public static void Configure()
    {
        Instance = new SkillBindings();
        CommandSystem.Register("Bindings", AccessLevel.Player, args => BindingGump.DisplayTo(args.Mobile));
        CommandSystem.Register("Pets", AccessLevel.Player, args => PetStatsGump.DisplayTo(args.Mobile));
        EventSink.WorldSave += () => Instance.PruneDeleted();
    }
    public (CombatAbility Left, CombatAbility Right) Get(PlayerMobile player) =>
        ClassCreation.Profiles.TryGet(player.Serial, out var profile) ? Get(player.Serial, profile.Class)
            : (CombatAbility.BasicAttack, CombatAbility.None);
    public (CombatAbility Left, CombatAbility Right) Get(Serial owner, DiabloClass characterClass) =>
        _bindings.TryGetValue((owner, characterClass), out var bindings) ? bindings
            : (CombatAbility.BasicAttack, characterClass == DiabloClass.Necromancer ? CombatAbility.Teeth : CombatAbility.None);
    public CombatAbility GetQuick(Serial owner, DiabloClass characterClass, int slot) => slot is >= 0 and < SkillBindingProtocol.Slots
        && _quick.TryGetValue((owner, characterClass), out var quick) ? quick[slot] : CombatAbility.None;
    public bool AssignQuick(PlayerMobile player, int slot, CombatAbility ability)
    {
        if (player == null || !player.Alive || !ClassCombat.Enabled(player) || !AlliedOverlap.Allowed(player)
            || slot is < 0 or >= SkillBindingProtocol.Slots || ability != CombatAbility.None && !ClassCombat.Available(player, ability)) { return false; }
        var key = (player.Serial, ProfileClass(player));
        _bindings.TryAdd(key, Get(player));
        if (!_quick.TryGetValue(key, out var quick)) { _quick.Add(key, quick = new CombatAbility[SkillBindingProtocol.Slots]); }
        quick[slot] = ability; return true;
    }
    public bool Assign(PlayerMobile player, bool left, CombatAbility ability)
    {
        if (player == null || !player.Alive || !ClassCombat.Enabled(player)
            || left && (PaladinCombat.IsAura(ability) || ability is CombatAbility.HolyShield or CombatAbility.Shout or CombatAbility.BattleOrders or CombatAbility.BattleCommand or CombatAbility.Howl or CombatAbility.BattleCry or CombatAbility.Taunt or CombatAbility.WarCry)
            || ability != CombatAbility.None && !ClassCombat.Available(player, ability))
        {
            return false;
        }
        var bindings = Get(player);
        _bindings[(player.Serial, ProfileClass(player))] = left ? (ability, bindings.Right) : (bindings.Left, ability);
        ClassCombat.Stop(player);
        var session = ClassCombat.For(player);
        session.Left = left ? ability : bindings.Left;
        session.Right = left ? bindings.Right : ability;
        if (!left) { PaladinAuras.SelectionChanged(player,bindings.Right,ability); }
        return true;
    }
    private static DiabloClass ProfileClass(PlayerMobile player)
    {
        ClassCreation.Profiles.TryGet(player.Serial, out var profile); return profile.Class;
    }
    public override void Serialize(IGenericWriter writer)
    {
        if (_legacy.Count != 0) { throw new InvalidOperationException("Legacy bindings must migrate before saving."); }
        writer.Write(1); writer.Write(_bindings.Count);
        var keys = new List<(Serial Owner, DiabloClass Class)>(_bindings.Keys);
        keys.Sort((a, b) => a.Owner == b.Owner ? a.Class.CompareTo(b.Class) : a.Owner.CompareTo(b.Owner));
        foreach (var key in keys)
        {
            var bindings = _bindings[key];
            writer.Write(key.Owner.Value); writer.Write((byte)key.Class);
            writer.Write((byte)bindings.Left); writer.Write((byte)bindings.Right);
            for (int slot = 0; slot < SkillBindingProtocol.Slots; slot++) { writer.Write((byte)GetQuick(key.Owner, key.Class, slot)); }
        }
    }
    public override void Deserialize(IGenericReader reader)
    {
        int version = reader.ReadInt();
        if (version is not (0 or 1)) { throw new InvalidDataException("Unsupported skill bindings version."); }
        int count = reader.ReadInt();
        if (count is < 0 or > 7000000) { throw new InvalidDataException("Invalid bindings count."); }
        var bindings = new Dictionary<(Serial Owner, DiabloClass Class), (CombatAbility Left, CombatAbility Right)>();
        var quick = new Dictionary<(Serial Owner, DiabloClass Class), CombatAbility[]>();
        var legacy = new Dictionary<Serial, (CombatAbility Left, CombatAbility Right)>();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); DiabloClass characterClass = default;
            if (!serial.IsMobile || version == 1 && !DiabloClasses.TryFromWire(reader.ReadByte(), out characterClass))
            { throw new InvalidDataException("Invalid binding owner or class."); }
            var left = (CombatAbility)reader.ReadByte(); var right = (CombatAbility)reader.ReadByte();
            if (!ClassAbilities.Valid(left) || !ClassAbilities.Valid(right)
                || (version == 0 ? !legacy.TryAdd(serial, (left, right)) : !bindings.TryAdd((serial, characterClass), (left, right))))
            { throw new InvalidDataException("Invalid or duplicate saved skill binding."); }
            if (version == 1)
            {
                var slots = new CombatAbility[SkillBindingProtocol.Slots];
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    slots[slot] = (CombatAbility)reader.ReadByte();
                    if (!ClassAbilities.Valid(slots[slot])) { throw new InvalidDataException("Invalid saved quick-cast binding."); }
                }
                quick.Add((serial, characterClass), slots);
            }
        }
        _bindings = bindings; _quick = quick; _legacy = legacy;
    }
    public void PruneDeleted()
    {
        var deleted = new List<(Serial Owner, DiabloClass Class)>();
        foreach (var key in _bindings.Keys)
        {
            if (World.FindMobile(key.Owner) is not PlayerMobile { Deleted: false }
                || !ClassCreation.Profiles.HasHistory(key.Owner, key.Class)) { deleted.Add(key); }
        }
        foreach (var key in deleted) { _bindings.Remove(key); _quick.Remove(key); }
    }
    public override void PostDeserialize()
    {
        // CharacterProfiles (priority100) has loaded before bindings (priority101).
        foreach (var (serial, bindings) in _legacy)
        {
            if (ClassCreation.Profiles.TryGet(serial, out var profile)) { _bindings.Add((serial, profile.Class), bindings); }
        }
        _legacy.Clear();
        PruneDeleted();
    }
}

public sealed class BindingGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly CombatAbility[] _choices;
    private readonly int _page;
    private const int PageSize = 8;
    public override bool Singleton => true;
    private BindingGump(PlayerMobile owner, int page) : base(80, 70)
    {
        _owner = owner;
        var choices = new System.Collections.Generic.List<CombatAbility> { CombatAbility.None };
        for (int id = 1; id < ClassAbilities.Count; id++)
        { if (ClassCombat.Available(owner, (CombatAbility)id)) { choices.Add((CombatAbility)id); } }
        _choices = choices.ToArray(); _page = Math.Clamp(page, 0, (_choices.Length - 1) / PageSize);
    }
    public static void DisplayTo(Mobile mobile, int page = 0)
    {
        if (mobile is PlayerMobile { NetState: not null } owner && ClassCombat.Enabled(owner))
        { owner.SendGump(new BindingGump(owner, page)); }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var bindings = SkillBindings.Instance.Get(_owner);
        builder.AddBackground(0, 0, 510, 440, 5054);
        builder.AddLabel(20, 15, 0x481, "Mouse skills — placeholder labels");
        builder.AddLabel(20, 42, 0x47E, $"Left: {CombatProtocol.Label(bindings.Left)}    Right: {CombatProtocol.Label(bindings.Right)}");
        for (int row = 0; row < PageSize && _page * PageSize + row < _choices.Length; row++)
        {
            var ability = _choices[_page * PageSize + row]; int y = 80 + row * 35;
            builder.AddLabel(20, y, 0x47E, CombatProtocol.Label(ability));
            builder.AddButton(300, y, 4005, 4007, 100 + row); builder.AddLabel(335, y, 0x481, "Left");
            builder.AddButton(390, y, 4005, 4007, 200 + row); builder.AddLabel(425, y, 0x481, "Right");
        }
        if (_page > 0) { builder.AddButton(20, 370, 4014, 4016, 1); }
        if ((_page + 1) * PageSize < _choices.Length) { builder.AddButton(440, 370, 4005, 4007, 2); }
        builder.AddLabel(120, 370, 0x47E, $"Page {_page + 1}. S opens quick-cast assignment.");
        builder.AddLabel(20, 408, 0x47E, "Aim at a corpse for corpse skills; aim at ground for summons.");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != _owner) { return; }
        if (info.ButtonID is 1 or 2) { DisplayTo(_owner, _page + (info.ButtonID == 1 ? -1 : 1)); return; }
        int row = info.ButtonID is >= 100 and < 108 ? info.ButtonID - 100 : info.ButtonID is >= 200 and < 208 ? info.ButtonID - 200 : -1;
        int index = _page * PageSize + row;
        if (row < 0 || index >= _choices.Length) { return; }
        if (!SkillBindings.Instance.Assign(_owner, info.ButtonID < 200, _choices[index])) { _owner.SendMessage("That skill is unavailable."); }
        DisplayTo(_owner, _page);
    }
}
