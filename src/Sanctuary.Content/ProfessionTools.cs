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
public partial class ProfessionInstructor : BaseVendor
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
}
