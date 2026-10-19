// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Engines.Craft;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class RepairHammer : BaseTool
{
    [Constructible]
    public RepairHammer() : base(25, 0x13E3) { }
    public override string DefaultName => "repair hammer";
    public override CraftSystem CraftSystem => DefBlacksmithy.CraftSystem;
}

[SerializationGenerator(0)]
public partial class TailoringRepairKit : BaseTool
{
    [Constructible]
    public TailoringRepairKit() : base(25, 0xF9F) { }
    public override string DefaultName => "tailoring repair kit";
    public override CraftSystem CraftSystem => DefTailoring.CraftSystem;
}
