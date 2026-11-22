// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class Merydwen : ConversationNpc, IClassCompanion
{
    internal static readonly Point3D InnLocation = new(2685, 2235, 2);
    internal const int HiringFee = 5000;
    private const DiabloClass CombatClass = DiabloClass.Amazon;
    [SerializableField(0, setter: "private")] private bool _recruited;

    DiabloClass IClassCompanion.CombatClass => CombatClass;
    public override bool IsInvulnerable => !Recruited;
    public override bool CanRegenMana => false;
    public override int ManaMax => 0;
    public override bool WandersWhileGuarding => false;
    internal override ushort Portrait => PortraitProtocol.MerydwenPortrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => Recruited ? _company : _inn;

    private static readonly ScriptedDialogue.Reply[] _innTopics =
    [
        new("Work", 1), new("Join me", 0, (player, speaker) => ((Merydwen)speaker).Offer(player))
    ];

    private static readonly ScriptedDialogue.Node[] _inn =
    [
        new("You look like someone with a job. Please tell me it pays better than sitting here.", _innTopics),
        new("I'm looking for work. I can put an arrow through a sail rope in a stiff wind. Mind you, I'd rather the next job kept my boots dry.", _innTopics),
        new("Five thousand gold up front and I'll come with you. We can argue over who buys the drinks later.",
            [new("Pay 5,000 gold", 0, (player, speaker) => ((Merydwen)speaker).Accept(player)), new("Later", 0)]),
        new("I don't like big crews anymore. Why? You don't know me well enough to ask that. Come back when you're on your own.", []),
        new("Five thousand, darling. Come see me when you've got the coin.", _innTopics),
        new("Let's try that again when we've got a little room to move.", _innTopics)
    ];

    private static readonly ScriptedDialogue.Reply[] _companyTopics =
    [
        new("Work", 1), new("Bow", 2), new("Equipment", 0, (player, speaker) =>
        {
            if (speaker.CanPaperdollBeOpenedBy(player))
                speaker.DisplayPaperdollTo(player);
        })
    ];

    private static readonly ScriptedDialogue.Node[] _company =
    [
        new("Right, captain. You've bought yourself a bow. Where are we taking it?", _companyTopics),
        new("You pay, I shoot. If we both keep our end of that bargain, I expect we'll get on splendidly.", _companyTopics),
        new("Give me room to draw and I'll find them. Give me a whole crowd and I'll find something to do with the rest of my arrows.", _companyTopics)
    ];

    public new static void Configure() => EventSink.ServerStarted += Install;

    private static void Install()
    {
        if (!World.Mobiles.Values.OfType<Merydwen>().Any(pirate => pirate.GetType() == typeof(Merydwen) && !pirate.Deleted && !pirate.Recruited))
            new Merydwen().MoveToWorld(InnLocation, Map.Trammel);
    }

    [Constructible]
    public Merydwen()
    {
        Name = "Merydwen";
        Title = "the freebooter";
        Female = true;
        Body = 0x191;
        Hue = 0x83EF;
        HairItemID = 0x203C;
        HairHue = 0x002B;
        CantWalk = true;
        Direction = Direction.South;
        AddItem(new Bandana(0x0021));
        AddItem(new FancyShirt(0x0491));
        AddItem(new LeatherChest { Hue = 0x07AE });
        AddItem(new LongPants(0x0455));
        AddItem(new ThighBoots(0x047A));
        AddItem(new Bow());
    }

    private void Offer(PlayerMobile player) => ScriptedDialogue.Show(player, this, _inn,
        MercenaryCare.HasActiveCompanion(player) ? (byte)3 : (byte)2);

    private void Accept(PlayerMobile player)
    {
        if (Recruited || !ScriptedDialogue.CanTalk(player, this))
            return;

        var companion = Hire(player, out byte response);
        if (companion == null)
        {
            ScriptedDialogue.Show(player, this, _inn, response);
            return;
        }

        player.NetState?.SendRemoveEntity(Serial);
        ScriptedDialogue.SendStoryState(player);
        ScriptedDialogue.Show(player, companion, _company);
    }

    internal static Merydwen Hire(PlayerMobile owner, out byte response)
    {
        response = 3;
        if (MercenaryCare.HasActiveCompanion(owner) || MercenaryCare.HasContract<Merydwen>(owner))
            return null;

        response = 5;
        if (owner is not { Alive: true, Deleted: false, Map: { } map, Backpack: not null } || map == Map.Internal
            || !PersistentMinions.FindPosition(map, owner.Location, point => owner.InLOS(point), out var nearby))
            return null;
        response = 4;
        if (!SanctuaryBanking.CanPay(owner, HiringFee))
            return null;

        var companion = new Merydwen();
        companion.PrepareRangedCompanion();
        bool hired = false;
        try
        {
            response = 5;
            if (!companion.SetControlMaster(owner))
                return null;

            companion.MoveToWorld(nearby, map);
            companion.ControlTarget = owner;
            companion.ControlOrder = OrderType.Guard;
            response = 4;
            hired = MercenaryCare.Register(owner, companion, ClassStats.Level(owner),
                () => SanctuaryBanking.TryPay(owner, HiringFee));
            return hired ? companion : null;
        }
        finally
        {
            if (!hired)
                companion.Delete();
        }
    }

    protected void PrepareRangedCompanion()
    {
        Recruited = true;
        CantWalk = false;
        AI = AIType.AI_Melee;
        ControlSlots = 0;
        AddItem(new Backpack { Movable = false });
    }

    private bool OwnedBy(Mobile from) => from is PlayerMobile owner && Recruited && MercenaryCare.IsCompanion(owner, this);
    internal override bool CanTalkTo(PlayerMobile player) =>
        Recruited ? OwnedBy(player) && !IsDeadPet : BritainStory.MobileVisible(player, this);

    private bool CanHandleGear(Mobile from) => OwnedBy(from) && from.Alive && Alive && !IsDeadPet
        && from.Map == Map && from.InRange(this, PortraitProtocol.NpcInteractionRange)
        && Math.Abs(from.Z - Z) < 16 && from.CanSee(this) && from.InLOS(this);

    public override bool CanPaperdollBeOpenedBy(Mobile from) => CanHandleGear(from);
    public override bool AllowEquipFrom(Mobile from) => CanHandleGear(from);
    public override bool IsSnoop(Mobile from) => !CanHandleGear(from) && base.IsSnoop(from);
    public override bool CheckItemUse(Mobile from, Item item) => CanHandleGear(from);
    public override bool CheckNonlocalLift(Mobile from, Item item) => item != Backpack && CanHandleGear(from);
    public override bool CheckNonlocalDrop(Mobile from, Item item, Item target) => CanHandleGear(from);
    public override bool CanBeRenamedBy(Mobile from) => false;
    public override bool OnDragDrop(Mobile from, Item dropped) =>
        CanHandleGear(from) && (dropped is BasePotion ? Backpack?.TryDropItem(from, dropped, true) == true : base.OnDragDrop(from, dropped));
    public override int GetResistance(ResistanceType type) => CompanionBuild.Resistance(this, type, base.GetResistance(type));

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        if (!Recruited || !IsDeadPet)
            base.GetContextMenuEntries(from, ref list);
    }

    public override bool EquipItem(Item item)
    {
        if (Recruited && !CompanionBuild.AllowedArcherGear(item, AttackFamily.Bow))
        {
            ControlMaster?.SendMessage($"{Name}: I can't use that.");
            return false;
        }
        return base.EquipItem(item);
    }

    public override void OnAfterResurrect()
    {
        base.OnAfterResurrect();
        ResetFight();
        if (ControlMaster is PlayerMobile owner && OwnedBy(owner))
        {
            ControlTarget = owner;
            ControlOrder = OrderType.Guard;
            if (this is not Nightwind)
                SayBark(owner, _afterResurrection, Core.TickCount, force: true);
        }
    }
}
