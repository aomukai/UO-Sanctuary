// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class ImbuingLadle : BaseTool
{
    [Constructible]
    public ImbuingLadle() : base(25, 0x9F8) { }
    public override string DefaultName => "imbuing ladle";
    public override CraftSystem CraftSystem => ImbuingPracticeSystem.Instance;
}

public sealed class ImbuingPracticeSystem : CraftSystem
{
    public static CraftSystem Instance { get; } = new ImbuingPracticeSystem();
    private ImbuingPracticeSystem() : base(1, 1, 1.0) { }
    public override SkillName MainSkill => SkillName.Imbuing;
    public override double GetChanceAtMin(CraftItem item) => 0;
    public override void InitCraftList() { }
    public override void PlayCraftEffect(Mobile from) { }
    public override int PlayEndingEffect(Mobile from, bool failed, bool lostMaterial, bool toolBroken,
        int quality, bool makersMark, CraftItem item) => 0;
    public override int CanCraft(Mobile from, BaseTool tool, System.Type itemType) =>
        tool is { Deleted: false, UsesRemaining: > 0 } && (tool.IsChildOf(from.Backpack) || tool.Parent == from) ? 0 : 1044038;
}

[SerializationGenerator(0)]
public partial class ProfessionInstructor : BaseVendor, IScriptedSpeaker
{
    private readonly List<SBInfo> _stock = new();
    [Constructible]
    public ProfessionInstructor() : base("the professions instructor")
    {
        foreach (byte skill in TradeSkills.Crafting) { SetSkill((SkillName)skill, 100); }
        foreach (byte skill in TradeSkills.Gathering) { SetSkill((SkillName)skill, 100); }
    }
    protected override List<SBInfo> SBInfos => _stock;
    public override void InitSBInfo() { }

    internal bool UsesDialogueTraining =>
        TownMarkets.Profiles?.Find(Serial)?.Site != null && TownMarkets.Role(this) is { } role && TownMarkets.Skill(role) != null;

    ushort IScriptedSpeaker.Portrait => TownMarkets.PortraitOf(this);
    bool IScriptedSpeaker.CanTalkTo(PlayerMobile player) => UsesDialogueTraining && ClassProfessions.Affected(player);

    void IScriptedSpeaker.BeginConversation(PlayerMobile player)
    {
        var skill = TownMarkets.Skill(TownMarkets.Role(this).Value).Value;
        ScriptedDialogue.Show(player, this,
        [
            new($"I teach {ClassProfessions.Name(skill)}. If thou wouldst learn, I can give thee a few lessons.",
                [new("Train", 0, (student, speaker) => ClassProfessions.OpenTraining(student, (BaseCreature)speaker, skill)),
                    new("Buy", 0, (customer, speaker) => ((BaseVendor)speaker).VendorBuy(customer)),
                    new("Sell", 0, (customer, speaker) => ((BaseVendor)speaker).VendorSell(customer))])
        ]);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && UsesDialogueTraining && ClassProfessions.Affected(player))
            ScriptedDialogue.Open(player, this);
        else
            base.OnDoubleClick(from);
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (dropped is Gold && UsesDialogueTraining && ClassProfessions.Affected(from))
        {
            from.SendMessage("Speak with the trainer to arrange a lesson.");
            return false;
        }
        return base.OnDragDrop(from, dropped);
    }
}
