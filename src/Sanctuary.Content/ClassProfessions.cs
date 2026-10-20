// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Engines.Craft;
using Server.Items;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class ClassProfessions
{
    public static ProfessionProfiles Profiles { get; private set; }
    public static bool Enabled { get; private set; }
    public static int GoldPerTenth { get; private set; }
    internal sealed record Choice(long Token, SkillName Skill, Item Tool, BaseCreature Trainer, NetState Connection);
    internal sealed record TrainingQuote(Serial Trainer, SkillName Skill, int Base, int Cap, NetState Connection);
    private static readonly Dictionary<Serial, Choice> _choices = new();
    private static readonly Dictionary<Serial, TrainingQuote> _training = new();
    private static readonly HashSet<(Serial Student, Serial Trainer)> _trainingContacts = new();
    private static long _nextChoice;
    public static void Configure()
    {
        Enabled = ServerConfiguration.GetSetting("sanctuary.professions.enabled", false);
        GoldPerTenth = ServerConfiguration.GetSetting("sanctuary.professions.goldPerTenth", 1);
        if (GoldPerTenth is < 1 or > 100) { throw new ArgumentOutOfRangeException(nameof(GoldPerTenth)); }
        Profiles = new ProfessionProfiles();
        ProfessionPractice.Load();
        TradeSkillHooks.ActionAllowed = Request;
        TradeSkillHooks.ToolUsed = UseTool;
        TradeSkillHooks.RecipeAllowed = RecipeAllowed;
        TradeSkillHooks.IndependentSkills = Affected;
        TradeSkillHooks.TeachingAllowed = (from, skill) => !Affected(from) || TradeSkills.Includes((int)skill);
        TradeSkillHooks.Training = Train;
        TradeSkillHooks.TrainingPayment = Pay;
        EventSink.WorldSave += () => { Profiles.Prune(); Cleanup(); };
        EventSink.Disconnected += from =>
        {
            _choices.Remove(from.Serial); _training.Remove(from.Serial);
            _trainingContacts.RemoveWhere(pair => pair.Student == from.Serial);
        };
        EventSink.Connected += from =>
        {
            if (Affected(from))
            {
                from.SendMessage($"Craft: {Name(Profiles.Find(from).Craft)}. Use [Profession to view your skills and first-choice status.");
            }
        };
        CommandSystem.Register("Profession", AccessLevel.Player, e =>
        {
            if (Affected(e.Mobile)) { ProfessionGump.DisplayTo((PlayerMobile)e.Mobile); }
        });
    }
    public static bool Affected(Mobile from) => Enabled && from is PlayerMobile { AccessLevel: AccessLevel.Player }
        && ClassCombat.Enabled(from as PlayerMobile);
    public static bool Ready(Mobile from) => from is PlayerMobile { Deleted: false, Alive: true, Frozen: false, Paralyzed: false }
        && from.Backpack != null && from.Holding == null;
    public static string Name(SkillName skill) => skill switch
    {
        SkillName.Blacksmith => "Blacksmithing", SkillName.Fletching => "Bowcraft/Fletching",
        SkillName.Inscribe => "Inscription", _ => skill.ToString()
    };
    public static bool Active(Mobile from, SkillName skill) => !Affected(from) || TradeSkills.IsGathering((int)skill)
        || Profiles.Find(from) is { Confirmed: true } state && state.Craft == skill;
    internal static void InvalidateChoices(PlayerMobile player)
    {
        _choices.Remove(player.Serial); _training.Remove(player.Serial);
        player.Learning = (SkillName)(-1);
        player.CloseGump<ProfessionChoiceGump>(); player.CloseGump<ProfessionToolGump>();
    }
    internal static Choice Pending(Mobile from) => _choices.GetValueOrDefault(from.Serial);
    private static void Cleanup()
    {
        var removed = new List<Serial>();
        foreach (var (owner, choice) in _choices)
        {
            if (World.FindMobile(owner) is not { Deleted: false } from || !SourceValid(from, choice.Tool, choice.Trainer, choice.Skill))
            { removed.Add(owner); }
        }
        foreach (var owner in removed) { _choices.Remove(owner); }
        removed.Clear();
        foreach (var (owner, quote) in _training)
        {
            if (World.FindMobile(owner) is not { Deleted: false } from
                || World.FindMobile(quote.Trainer) is not BaseCreature trainer || !SourceValid(from, null, trainer, quote.Skill))
            { removed.Add(owner); }
        }
        foreach (var owner in removed) { _training.Remove(owner); }
        _trainingContacts.RemoveWhere(pair => World.FindMobile(pair.Student) is not { Deleted: false }
            || World.FindMobile(pair.Trainer) is not { Deleted: false });
    }
    private static bool RecipeAllowed(Mobile from, SkillName skill, Type output, BaseTool tool, bool prompt)
    {
        if (!Affected(from)) { return true; }
        if (!Ready(from)) { return false; }
        if (CraftFacilities.Processing(output)) { return true; }
        return (prompt ? Request(from, skill, tool, true) : Active(from, skill))
            && CraftFacilities.AllowsRecipe(from, skill, output);
    }
    private static bool SourceValid(Mobile from, Item tool, BaseCreature trainer, SkillName skill, Gold payment = null) =>
        (Ready(from) || payment != null && from is PlayerMobile { Deleted: false, Alive: true, Frozen: false, Paralyzed: false }
            && from.Backpack != null && ReferenceEquals(from.Holding, payment))
        && (tool != null ? tool is { Deleted: false } && (tool.IsChildOf(from.Backpack) || tool.Parent == from)
            && (tool is not BaseTool craftTool || craftTool.UsesRemaining > 0 && craftTool.CraftSystem.MainSkill == skill)
            : trainer is { Deleted: false, Alive: true, CanTeach: true } && from.Map == trainer.Map
                && from.InRange(trainer.Location, 3) && from.InLOS(trainer) && trainer.Skills[skill].Base >= 30);
    public static bool Request(Mobile from, SkillName skill, Item source = null, bool prompt = false)
    {
        if (!Affected(from)) { return true; }
        if (!Ready(from) || !TradeSkills.Includes((int)skill)) { return false; }
        if (Active(from, skill)) { return true; }
        var state = Profiles.Find(from);
        if (state.Confirmed)
        {
            from.SendMessage($"Your craft is {Name(state.Craft)}. Another craft requires earned town-hall wardrobe access.");
        }
        else if (prompt && TradeSkills.IsCraft((int)skill) && SourceValid(from, source, null, skill))
        {
            Ask((PlayerMobile)from, skill, source, null);
        }
        else
        {
            from.SendMessage("Confirm your first profession with its crafting tool or trainer before using that craft.");
        }
        return false;
    }
    private static void Ask(PlayerMobile owner, SkillName skill, Item tool, BaseCreature trainer)
    {
        var choice = new Choice(++_nextChoice, skill, tool, trainer, owner.NetState);
        _choices[owner.Serial] = choice;
        ProfessionChoiceGump.DisplayTo(owner, choice);
    }
    internal static bool Respond(PlayerMobile owner, long token, bool confirm)
    {
        if (!_choices.TryGetValue(owner.Serial, out var choice) || choice.Token != token) { return false; }
        _choices.Remove(owner.Serial);
        if (!confirm || !Affected(owner) || !ReferenceEquals(owner.NetState, choice.Connection)
            || !SourceValid(owner, choice.Tool, choice.Trainer, choice.Skill) || !Profiles.Confirm(owner, choice.Skill))
        {
            return false;
        }
        owner.SendMessage($"{Name(choice.Skill)} is your confirmed first profession.");
        if (choice.Tool is BaseTool tool) { ProfessionToolGump.DisplayTo(owner, tool); }
        else if (choice.Trainer != null) { choice.Trainer.Teach(choice.Skill, owner, 0, false); }
        else if (choice.Skill == SkillName.Imbuing) { EssenceGump.DisplayTo(owner); }
        return true;
    }
    private static bool UseTool(Mobile from, BaseTool tool)
    {
        if (!Affected(from)) { return false; }
        if (!SourceValid(from, tool, null, tool.CraftSystem.MainSkill)) { return true; }
        if (!Profiles.Find(from).Confirmed && !MaterialProcessing.Supports(tool) && !EquipmentSalvage.Supports(tool))
        {
            Request(from, tool.CraftSystem.MainSkill, tool, true);
        }
        else { ProfessionToolGump.DisplayTo((PlayerMobile)from, tool); }
        return true;
    }
    private static BaseCreature.TeachResult? Train(BaseCreature trainer, SkillName skill, Mobile student,
        int payment, ref int price, bool apply)
    {
        if (!Affected(student)) { return null; }
        if (!TradeSkills.Includes((int)skill) || !SourceValid(student, null, trainer, skill))
        {
            return BaseCreature.TeachResult.Failure;
        }
        _trainingContacts.Add((student.Serial, trainer.Serial));
        if (!Active(student, skill))
        {
            if (!Profiles.Find(student).Confirmed && TradeSkills.IsCraft((int)skill))
            {
                Ask((PlayerMobile)student, skill, null, trainer);
            }
            else { Request(student, skill); }
            return BaseCreature.TeachResult.Failure;
        }
        var trained = student.Skills[skill];
        int remaining = Math.Min(300, trained.CapFixedPoint) - trained.BaseFixedPoint;
        if (remaining <= 0) { return BaseCreature.TeachResult.KnowsWhatIKnow; }
        if (trained.Lock != SkillLock.Up) { return BaseCreature.TeachResult.SkillNotRaisable; }
        // Payment owns the mutation; a direct Teach/CheckTeachSkills call cannot grant free skill.
        if (apply) { return BaseCreature.TeachResult.Failure; }
        price = remaining * GoldPerTenth;
        _training[student.Serial] = new(trainer.Serial, skill, trained.BaseFixedPoint, trained.CapFixedPoint, student.NetState);
        return BaseCreature.TeachResult.Success;
    }
    private static bool? Pay(BaseCreature trainer, Mobile student, Gold gold)
    {
        if (!Affected(student) || !trainer.CanTeach || !_trainingContacts.Contains((student.Serial, trainer.Serial))) { return null; }
        if (student is not PlayerMobile player || gold is not { Deleted: false, Amount: > 0 }
            || !_training.TryGetValue(student.Serial, out var quote) || quote.Trainer != trainer.Serial
            || !ReferenceEquals(quote.Connection, student.NetState) || !SourceValid(student, null, trainer, quote.Skill, gold)
            || !Active(student, quote.Skill))
        {
            return false;
        }
        var skill = student.Skills[quote.Skill];
        if (skill.BaseFixedPoint != quote.Base || skill.CapFixedPoint != quote.Cap || skill.Lock != SkillLock.Up)
        {
            _training.Remove(student.Serial);
            return false;
        }
        int points = Math.Min(Math.Min(300, skill.CapFixedPoint) - skill.BaseFixedPoint, gold.Amount / GoldPerTenth);
        if (points <= 0) { return false; }
        gold.Consume(points * GoldPerTenth);
        if (!gold.Deleted) { student.AddToBackpack(gold); }
        skill.BaseFixedPoint += points;
        _training.Remove(student.Serial);
        player.Learning = (SkillName)(-1);
        student.SendMessage($"You learned {points / 10.0:F1} {Name(quote.Skill)} for {points * GoldPerTenth} gold.");
        return true;
    }
}
