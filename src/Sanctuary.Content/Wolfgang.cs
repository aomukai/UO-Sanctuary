// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class Wolfgang : ConversationNpc, IClassCompanion
{
    [SerializableField(0)]
    private bool _recruited;
    private TimerExecutionToken _swing;
    private DateTime _nextShout;
    private DateTime _rushUntil, _nextRush;

    private const DiabloClass CombatClass = DiabloClass.Barbarian;
    private const double CombatPace = 0.18, RushPace = 0.085;
    DiabloClass IClassCompanion.CombatClass => CombatClass;
    public override bool IsInvulnerable => !Recruited;
    public override bool CanRegenMana => false;
    public override int ManaMax => 0;
    public override bool WandersWhileGuarding => false;
    internal override byte Portrait => PortraitProtocol.WolfgangPortrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => Recruited ? _company : _recruit;
    internal static readonly ScriptedDialogue.Voice Voice = new("Wolfgang", PortraitProtocol.WolfgangPortrait);

    private static readonly ScriptedDialogue.Node[] _recruit =
    [
        new("Good day, Avatar. The captain says I ought to look people in the eye when I speak. I am working on it.", [])
    ];

    private static readonly ScriptedDialogue.Reply[] _topics =
    [
        new("Training", 1),
        new("Justice", 2),
        new("Equipment", 0, (player, speaker) =>
        {
            if (speaker.CanPaperdollBeOpenedBy(player))
                speaker.DisplayPaperdollTo(player);
        })
    ];

    private static readonly ScriptedDialogue.Node[] _company =
    [
        new("I am ready when thou art, Avatar. At least, I think I am.", _topics),
        new("Captain Geoffrey put me on shield drill all winter. If something comes at us, let me take the first blow. That part I know.", _topics),
        new("If a man is about to hurt someone, I'll kick his knee out and answer for it after. The captain says I ought to learn to tell when someone is about to hurt a soul and when he is merely loud. I think I understand. In the yard, anyway.", _topics)
    ];

    private static readonly ScriptedDialogue.Node[] _trusted = TrustedDialogue();

    private static ScriptedDialogue.Node[] TrustedDialogue()
    {
        ScriptedDialogue.Reply[] topics = [.._topics, new("The Streets", 3)];
        return
        [
            _company[0] with { Replies = topics },
            _company[1] with { Replies = topics },
            _company[2] with { Replies = topics },
            new("I did not learn to fight in the yard. I learned in the alleys. I stole bread and fought whoever meant to take it back. One night I picked a fight I could not finish. Geoffrey pulled me out, still breathing. I wanted to be the sort of guard who would do that for someone like me. That is why I signed on.", topics)
        ];
    }

    internal override void BeginConversation(PlayerMobile player) =>
        ScriptedDialogue.Show(player, this,
            Recruited && ClassCreation.Profiles.WolfgangOpenedUp(player.Serial) ? _trusted : Script);

    [Constructible]
    public Wolfgang()
    {
        Name = "Wolfgang";
        Title = "the recruit";
        Body = 0x190;
        Hue = 0x83EF;
        HairItemID = 0x203B;
        HairHue = 0x045A;
        CantWalk = true;
        Direction = Direction.North;
        AddItem(new Broadsword());
        AddItem(new WoodenShield());
        AddItem(new LeatherChest { Hue = 0x0590 });
        AddItem(new FancyShirt(0x0591));
        AddItem(new LongPants(0x0455));
        AddItem(new Boots(0x0590));
    }

    internal static Wolfgang Join(PlayerMobile player)
    {
        if (!BritainStory.Retired(player) || MercenaryCare.HasActiveCompanion(player))
            return null;

        var wolfgang = new Wolfgang { Recruited = true, CantWalk = false, AI = AIType.AI_Melee, ControlSlots = 0 };
        if (!wolfgang.SetControlMaster(player) || !MercenaryCare.Register(player, wolfgang, ClassStats.Level(player)))
        {
            wolfgang.Delete();
            return null;
        }

        wolfgang.MoveToWorld(PersistentMinions.FindPosition(player, out var nearby) ? nearby : player.Location, player.Map);
        wolfgang.ControlTarget = player;
        wolfgang.ControlOrder = OrderType.Guard;
        return wolfgang;
    }

    private bool OwnedBy(Mobile from) => from is PlayerMobile player && Recruited && MercenaryCare.IsCompanion(player, this);
    internal override bool CanTalkTo(PlayerMobile player) =>
        Recruited ? OwnedBy(player) : BritainStory.MobileVisible(player, this);
    private bool CanHandleGear(Mobile from) => OwnedBy(from) && from.Alive && Alive && !IsDeadPet
        && from.Map == Map && Math.Abs(from.Z - Z) < 16 && from.InRange(this, PortraitProtocol.NpcInteractionRange)
        && from.CanSee(this) && from.InLOS(this);

    internal void PrepareForDuty()
    {
        if (Backpack == null)
            AddItem(new Backpack { Movable = false });
        if (ActiveSpeed != 0.12)
            SetSpeed(0.12, PassiveSpeed, isPassive: !Warmode);
        ActiveMoveSpeed = CombatPace;
    }

    public override bool CanPaperdollBeOpenedBy(Mobile from) => CanHandleGear(from);
    public override bool AllowEquipFrom(Mobile from) => CanHandleGear(from);
    public override bool IsSnoop(Mobile from) => !CanHandleGear(from) && base.IsSnoop(from);
    public override bool CheckItemUse(Mobile from, Item item) => CanHandleGear(from);
    public override bool CheckNonlocalLift(Mobile from, Item item) => item != Backpack && CanHandleGear(from);
    public override bool CheckNonlocalDrop(Mobile from, Item item, Item target) => CanHandleGear(from);
    public override bool CanBeRenamedBy(Mobile from) => false;
    public override bool OnDragDrop(Mobile from, Item dropped) =>
        CanHandleGear(from) && (dropped is BasePotion
            ? Backpack?.TryDropItem(from, dropped, true) == true : base.OnDragDrop(from, dropped));
    public override int GetResistance(ResistanceType type) => CompanionBuild.Resistance(this, type, base.GetResistance(type));

    public override bool EquipItem(Item item)
    {
        if (Recruited && item is BaseWeapon and not BaseSword and not Fists)
        {
            ControlMaster?.SendMessage("Wolfgang: I can't wield that.");
            return false;
        }
        return base.EquipItem(item);
    }

    public override void OnAfterResurrect()
    {
        base.OnAfterResurrect();
        EndBarkFight();
        if (ControlMaster is PlayerMobile owner && OwnedBy(owner))
        {
            ControlTarget = owner;
            ControlOrder = OrderType.Guard;
        }
    }

    internal static int SkillRank(int level) => Math.Min(20, 1 + (level - 1) / 4);

    private static SkillValues Values(CombatAbility ability, int level)
    {
        var row = SkillMechanics.For(ability);
        int Rank(string key) => key == row.SourceKey ? SkillRank(level) : 0;
        return new SkillValues(row, Rank, Rank, level);
    }

    internal int IronSkinDefense => MercenaryCare.CompanionLevel(this) is { } level
        ? (int)Values(CombatAbility.IronSkin, level).Eval("passivecalc1") : 0;

    private bool CanFight(Mobile target, out PlayerMobile owner)
    {
        owner = ControlMaster as PlayerMobile;
        return Alive && !Deleted && !IsDeadPet && !Frozen && !Paralyzed && owner is { Alive: true }
            && owner.Map == Map && MercenaryCare.IsCompanion(owner, this) && Combatant == target
            && ClassCombatHooks.ActionAllowed?.Invoke(this) != false && CreatureHitRecovery.Remaining(this) == 0
            && target.Map == Map && ClassCombat.IsHostile(owner, target) && Math.Abs(Z - target.Z) < 16 && InLOS(target);
    }

    public override void OnCombatantChange()
    {
        base.OnCombatantChange();
        if (!Recruited)
            return;

        var now = TimedEffects.Now;
        if (Combatant is Mobile target && now >= _nextRush && !InRange(target, 1)
            && InRange(target, 7) && CanFight(target, out _))
        {
            _rushUntil = now.AddSeconds(1);
            _nextRush = now.AddSeconds(4);
            ActiveMoveSpeed = RushPace;
        }
        else if (Combatant == null)
        {
            ActiveMoveSpeed = CombatPace;
        }
    }

    public override void OnThink()
    {
        base.OnThink();
        if (!Recruited)
            return;
        ObserveCombatBarks();

        var now = TimedEffects.Now;
        if (ActiveMoveSpeed == RushPace && (now >= _rushUntil || Combatant == null || InRange(Combatant, 1)))
            ActiveMoveSpeed = CombatPace;
        if (now < _nextShout || Combatant is not Mobile target || !CanFight(target, out var owner))
            return;

        int level = MercenaryCare.CompanionLevel(this) ?? 1;
        var shout = Values(CombatAbility.Shout, level);
        _nextShout = now.AddMilliseconds(shout.Eval("auralencalc") * ActionTiming.FrameMs);
        TryShoutBark(owner);
        BarbarianCryVisuals.Cast(this, CombatAbility.Shout, now);
        BarbarianShout.Apply(this, this, shout, now, keepStronger: true);
        foreach (var ally in Map.GetMobilesInRange<Mobile>(Location, 5))
            if (ally != this && InParty(owner, ally) && Math.Abs(Z - ally.Z) < 16 && InLOS(ally))
                BarbarianShout.Apply(this, ally, shout, now, keepStronger: true);
        foreach (var foe in Map.GetMobilesInRange<BaseCreature>(Location, 3))
            if (foe.Combatant is Mobile ally && ally != this && InParty(owner, ally)
                && foe is not EncounterCreature { Grade: EncounterGrade.Unique or EncounterGrade.SuperUnique }
                && BarkHostile(owner, foe) && InLOS(foe))
                foe.Combatant = this;
    }

    private static bool InParty(PlayerMobile owner, Mobile ally)
    {
        var player = ally is BaseCreature creature ? creature.GetMaster() : ally;
        return player == owner || owner.Party is { } party && player?.Party == party;
    }

    internal TimeSpan BeginMelee(Mobile target)
    {
        if (_swing.Running || !CanFight(target, out _) || Weapon is not BaseWeapon weapon
            || !InRange(target, 1) || ClassGear.Broken(weapon))
            return TimeSpan.FromMilliseconds(100);

        var timing = CompanionBuild.Timing(this, CombatClass, CombatAbility.BasicAttack, weapon);
        Direction = GetDirectionTo(target);
        weapon.PlaySwingAnimation(this);
        Timer.StartTimer(TimeSpan.FromMilliseconds(timing.EffectMs), () => Strike(target, weapon), out _swing);
        return TimeSpan.FromMilliseconds(timing.DurationMs);
    }

    private void Strike(Mobile target, BaseWeapon weapon)
    {
        if (!CanFight(target, out _) || Weapon != weapon || !InRange(target, 1) || ClassGear.Broken(weapon))
            return;
        DoHarmful(target);

        int level = MercenaryCare.CompanionLevel(this) ?? 1;
        var mastery = weapon is BaseSword ? Values(CombatAbility.SwordMastery, level) : null;
        int attack = CompanionBuild.Bonus(this, "att%") + (int)(mastery?.Eval("passivecalc1") ?? 0);
        if (ClassStats.CheckHit(this, target, attack) != true)
            return;

        var (minimum, maximum) = ClassGear.Damage(weapon, level);
        var basis = ClassGear.Base(weapon);
        int strength = Str + CompanionBuild.Bonus(this, "str"), dexterity = Dex + CompanionBuild.Bonus(this, "dex");
        int damage = (basis == null ? strength : ClassStats.WeaponStatPercent(strength, dexterity, basis.StrengthBonus, basis.DexterityBonus))
            + CompanionBuild.Bonus(this, "dmg%", except: weapon) + (int)(mastery?.Eval("passivecalc2") ?? 0);
        double physical = Utility.RandomMinMax(minimum, maximum) * (100 + Math.Max(-90, damage)) / 100.0;
        if (mastery != null && Utility.Random(100) < mastery.Eval("passivecalc3"))
            physical *= 2;

        var parts = DefensiveArmors.Absorb(target, new GearDamage(physical, 0, 0, 0, 0), this);
        parts = parts with { Physical = GemCombat.Component(target, parts.Physical, target.PhysicalResistance, false) };
        var settled = DirectDamageParts.From(parts);
        SkillHealth.TypedDamage(this, target, checked((int)settled.Total), settled);
    }
}
