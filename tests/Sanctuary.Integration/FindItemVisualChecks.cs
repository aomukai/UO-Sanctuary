using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class FindItemVisualChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(FindItemVisualChecks));
    private static PlayerMobile Owner;
    private static Corpse Body;
    private static Point3D Origin;
    private static int Mana, Draws;
    private static Func<int, int> OriginalRoll;

    public static void Configure()
    {
        Register("setup", Setup);
        Register("right", () => Checked("right"));
        Register("quick", () => { Checked("quick"); BarbarianFindItem.Roll = OriginalRoll; });
    }
    private static void Require(bool condition, string why)
    { if (!condition) { throw new InvalidOperationException(why); } }
    private static void Register(string suffix, Action action)
    {
        string name = "find-item-visual-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Rendered Find Item near-cursor input", _ => Core.LoopContext.Post(() =>
        {
            try { action(); Log.Information("{Name}: passed", name); }
            catch (Exception error) { Log.Error(error, "{Name}: failed", name); }
        }));
    }
    private static object Position(Point3D point) => new { x = point.X, y = point.Y, z = point.Z };
    private static void State()
        => File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"),
            JsonSerializer.Serialize(new { player = Position(Owner.Location),
                cursor = Position(new Point3D(Body.X, Body.Y + 1, Body.Z)), corpse = Body.Serial.Value }));
    private static void Train(CombatAbility ability)
    {
        var row = SkillMechanics.For(ability);
        Require(TalentCatalog.TryGet(row.CharacterClass, row.TalentId, out var talent), "Missing talent");
        foreach (string id in talent.Prerequisites)
        { Train(SkillMechanics.All.Single(x => x.TalentId == id).Ability); }
        if (ClassCreation.Profiles.GetRank(Owner.Serial, row.TalentId) == 0)
        { Require(ClassCreation.Profiles.TryTrain(Owner.Serial, row.TalentId, out var why), "Training: " + why); }
    }
    private static void Setup()
    {
        Owner = NetState.Instances.Select(n => n.Mobile).OfType<PlayerMobile>().Single();
        Require(ClassCreation.Profiles.TryGet(Owner.Serial, out var profile)
            && profile.Class == DiabloClass.Barbarian, "Rendered character is not Barbarian");
        var platform = SpiritWolfWorldChecks.Fresh(column: 6200);
        Owner.MoveToWorld(platform.Location, platform.Map);
        platform.Delete();
        Owner.Name = "FindItemFixture";
        ClassCreation.Profiles.AdvanceLevel(Owner, 99);
        Train(CombatAbility.FindItem);
        Require(SkillBindings.Instance.Assign(Owner, false, CombatAbility.FindItem)
            && SkillBindings.Instance.AssignQuick(Owner, 0, CombatAbility.FindItem), "Visual bindings failed");
        OriginalRoll = BarbarianFindItem.Roll;
        BarbarianFindItem.Roll = _ => { Draws++; return 99; };
        Next(); SkillBindingNetwork.Send(Owner);
    }
    private static void Next()
    {
        ClassCombat.Stop(Owner);
        ClassCombat.For(Owner).NextAction = ClassCombat.For(Owner).RecoveryUntil = Core.TickCount;
        NecromancerEffects.SetMana256(Owner, Owner.ManaMax * 256);
        Mana = NecromancerEffects.ManaBalance256(Owner); Draws = 0; Origin = Owner.Location;
        Body = FindItemChecks.Body(Owner);
        Body.MoveToWorld(new Point3D(Owner.X + 2, Owner.Y, Owner.Z), Owner.Map);
        State();
    }
    private static void Checked(string mode)
    {
        Require(CorpseSkillRules.NoSelect(Body) && Owner.Location == Origin && Draws == 1
            && NecromancerEffects.ManaBalance256(Owner) == Mana - 1792,
            "Rendered " + mode + " ground cast failed selection/payment/no-walk/single release");
        Require(ClassLoot.HasContents(Body), "Existing corpse loot lost");
        if (mode == "right") { Next(); }
    }
}
