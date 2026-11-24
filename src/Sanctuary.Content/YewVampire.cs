// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class YewVampire : EncounterCreature, IScriptedSpeaker
{
    internal static readonly DungeonProfile Species = new("yew-vampire", "yew-cult", "Vael",
        MonsterArchetype.Melee, "Yew", 36, 36, 0x25D, 0, new(0, 0, 0, 0, 0, 0));
    [SerializableField(0)] private bool _hostile;
    private static TimerExecutionToken _respawn;

    [Constructible]
    public YewVampire()
    {
        Assign(Species, 36, MonsterDifficulty.Normal, EncounterGrade.Ordinary, ChampionKind.Champion,
            [], default, Species.Name, Random.Shared);
        Race = Race.Elf;
        Body = 0x25D;
        Hue = 0x83EA;
        HairItemID = 0x2FC0;
        HairHue = 0x0455;
        AddItem(new Robe(0x0455) { Movable = false });
        AddItem(new Boots(0x0455) { Movable = false });
        RestoreDisposition();
    }

    [AfterDeserialization]
    private void RestoreDisposition()
    {
        CantWalk = !_hostile;
        FightMode = _hostile ? FightMode.Closest : FightMode.None;
    }

    public override bool IsInvulnerable => !_hostile;
    public override bool AlwaysMurderer => _hostile;
    public override bool AlwaysAttackable => _hostile && !PaladinConversion.Active(this);
    internal override bool IsEnemyIgnoringLeash(Mobile target) => _hostile && base.IsEnemyIgnoringLeash(target);

    ushort IScriptedSpeaker.Portrait => (ushort)NpcPortrait.YewVampire;
    bool IScriptedSpeaker.CanTalkTo(PlayerMobile owner) => !_hostile && Math.Abs(Z - owner.Z) <= 5;
    void IScriptedSpeaker.BeginConversation(PlayerMobile owner)
    {
        var profile = ClassCreation.Profiles;
        if (!profile.TryGet(owner.Serial, out var character))
            return;
        string reply = character.Class switch
        {
            DiabloClass.Warlock => "A sacrifice summons a daemon. Thine is already here.",
            DiabloClass.Necromancer => "I know the scent of the dead. It clings to thee.",
            DiabloClass.Paladin => "I can feel what thou art. There is nothing holy in thee.",
            DiabloClass.Druid => "Even the roots beneath us draw away from thee.",
            DiabloClass.Sorceress => "There is a spell about thee, but no living pulse beneath it.",
            DiabloClass.Amazon => "Then why would they leave their sacrifice unchained?",
            DiabloClass.Assassin => "I have watched thee move. Nobody draws a breath so rarely.",
            _ => "A prisoner would watch the door. Thou hast been watching my throat."
        };
        // Dialogue starts the fight only after the Avatar calls out the deception.
        ScriptedDialogue.Show(owner, this,
        [
            new("Keep thy voice down. These cultists brought me here to sacrifice me to their daemon. Help me out of here before they come back.",
                [new("Challenge His Story", 1)]),
            new(reply, [new("Confront Vael", 0, (player, _) => StartBattle(player))])
        ]);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!_hostile && from is PlayerMobile owner)
            ScriptedDialogue.Open(owner, this);
        else
            base.OnDoubleClick(from);
    }

    private void StartBattle(PlayerMobile owner)
    {
        if (_hostile || !MountTravel.Nearby(owner, this))
            return;
        _hostile = true;
        this.MarkDirty();
        RestoreDisposition();
        Delta(MobileDelta.Noto);
        Say("Thou shouldst have walked away.");
        InvalidateProperties();
        Combatant = owner;
        FocusMob = owner;
        AIObject?.AITimer.Prod();
    }

    public override void OnDeath(Container corpse)
    {
        base.OnDeath(corpse);
        if (!_respawn.Running)
            Timer.StartTimer(TimeSpan.FromMinutes(5), EnsurePresent, out _respawn);
    }

    internal static void LeaveKeepsakes(PlayerMobile owner, Corpse corpse)
    {
        if (!YewStory.Has(owner, YewProgress.ClericReadLetter) && YewStory.Find(owner, YewKeepsakeKind.Letter) == null)
            corpse.DropItem(new YewKeepsake(owner, YewKeepsakeKind.Letter));
        if (!YewStory.Has(owner, YewProgress.ReturnedRing) && YewStory.Find(owner, YewKeepsakeKind.Ring) == null)
            corpse.DropItem(new YewKeepsake(owner, YewKeepsakeKind.Ring));
    }

    internal static void EnsurePresent()
    {
        if (!World.Mobiles.Values.OfType<YewVampire>().Any(npc => npc.Alive && !npc.Deleted))
            new YewVampire().MoveToWorld(YewStory.VampirePost, Map.Trammel);
    }
}

public enum YewKeepsakeKind : byte { Letter, Ring, Recommendation }

[SerializationGenerator(0)]
public partial class YewKeepsake : Item
{
    [SerializableField(0, setter: "private")] private Serial _avatar;
    [SerializableField(1, setter: "private")] private YewKeepsakeKind _kind;

    public YewKeepsake(PlayerMobile owner, YewKeepsakeKind kind) : base(kind == YewKeepsakeKind.Ring ? 0x108A : 0x14ED)
    {
        Avatar = owner.Serial;
        Kind = kind;
        Name = kind switch
        {
            YewKeepsakeKind.Letter => "Rainsong's letter",
            YewKeepsakeKind.Ring => "Vael's wedding ring",
            _ => "Sister Meriel's recommendation"
        };
        Weight = 0.1;
        LootType = LootType.Blessed;
    }

    public override bool IsAccessibleTo(Mobile from) => from.Serial == Avatar && base.IsAccessibleTo(from);
    public override bool OnDragLift(Mobile from) => from.Serial == Avatar && base.OnDragLift(from);

    public override void OnDoubleClick(Mobile from)
    {
        if (from.Serial != Avatar || !IsChildOf(from.Backpack) || Kind == YewKeepsakeKind.Ring)
            return;
        string message = Kind == YewKeepsakeKind.Letter
            ? "My dearest Vael,\n\nTake care on thy journey to Yew. I know what this expedition means to thee. Still, when thy work is done, remember that a light burns in the window for thee, and there are two of us waiting to hear thy step.\n\nWith all my love,\nRainsong"
            : "To the Mayor of Yew,\n\nThe bearer brings word of Vael, long since lost, and seeks his daughter Nightwind. I ask that thou grant the bearer passage to Heartwood so that Nightwind may have word of her father.\n\nSister Meriel, Empath Abbey";
        if (from is PlayerMobile owner)
            owner.SendGump(new CoveMessenger.LetterGump(owner, title: Name, text: message));
    }
}
