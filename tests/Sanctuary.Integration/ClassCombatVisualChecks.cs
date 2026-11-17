using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Collections;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ClassCombatVisualChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(ClassCombatVisualChecks));
    private static PlayerMobile Owner;
    private static GraveyardZombie Target;
    private static CombatAbility Ability;
    private static Point3D Origin, Aim;
    private static int Mana, Life, Cost;

    public static void Configure()
    {
        Register("setup", Setup); Register("cast", Check);
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("SANCTUARY_RENDER_CLASS") != null)
        { Mobile.ManaRegenRateHandler = _ => TimeSpan.FromHours(1); }
    }

    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static void Register(string suffix, Action action)
    {
        string command = "class-combat-visual-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Representative rendered learned skill consumer", _ => Core.LoopContext.Post(() =>
        {
            try { action(); Log.Information("{Command}: passed", command); }
            catch (Exception error) { Log.Error(error, "{Command}: failed", command); }
        }));
    }
    private static void Train(CombatAbility ability)
    {
        var row = SkillMechanics.For(ability);
        Require(TalentCatalog.TryGet(row.CharacterClass, row.TalentId, out var talent), "Missing talent");
        foreach (string prerequisite in talent.Prerequisites)
        { Train(SkillMechanics.All.Single(x => x.TalentId == prerequisite).Ability); }
        if (ClassCreation.Profiles.GetRank(Owner.Serial, row.TalentId) == 0)
        { Require(ClassCreation.Profiles.TryTrain(Owner.Serial, row.TalentId, out var why), "Training: " + why); }
    }
    private static object Point(Point3D point) => new { x = point.X, y = point.Y, z = point.Z };
    private static void Setup()
    {
        Owner = NetState.Instances.Select(n => n.Mobile).OfType<PlayerMobile>().Single();
        Require(ClassCreation.Profiles.TryGet(Owner.Serial, out var profile), "Missing created class");
        Ability = profile.Class switch
        {
            DiabloClass.Amazon => CombatAbility.InnerSight,
            DiabloClass.Sorceress => CombatAbility.FireBolt,
            DiabloClass.Paladin => CombatAbility.HolyBolt,
            DiabloClass.Assassin => CombatAbility.PsychicHammer,
            DiabloClass.Druid => CombatAbility.SummonSpiritWolf,
            DiabloClass.Warlock => CombatAbility.SummonGoatman,
            _ => throw new InvalidOperationException("Class already has a separate rendered checkpoint")
        };
        var platform = SpiritWolfWorldChecks.Fresh(column: 6200);
        Owner.MoveToWorld(platform.Location, platform.Map); platform.Delete();
        Owner.Name = "FindItemFixture"; // Reuse the existing one-hour test mana cadence.
        ClassCreation.Profiles.AdvanceLevel(Owner, 99); Train(Ability);
        Target = new GraveyardZombie { NoKillAwards = true, Frozen = true, CantWalk = true };
        Target.SetHits(20000);
        foreach (var resistance in Enum.GetValues<ResistanceType>()) { Target.SetResistance(resistance, 0); }
        Target.MoveToWorld(new Point3D(Owner.X + 4, Owner.Y, Owner.Z), Owner.Map);
        Target.AIObject?.AITimer.Stop(); TimedEffects.PreventHealing(Target, TimedEffects.Now);
        Target.DoHarmful(Owner);
        Owner.Target = null; ClassCombat.Stop(Owner);
        ClassCombat.For(Owner).NextAction = ClassCombat.For(Owner).RecoveryUntil = Core.TickCount;
        NecromancerEffects.SetMana256(Owner, Owner.ManaMax * 256);
        Require(SkillBindings.Instance.AssignQuick(Owner, 0, Ability), "Quick binding failed");
        SkillBindingNetwork.Send(Owner);
        Origin = Owner.Location;
        Aim = Ability is CombatAbility.SummonSpiritWolf or CombatAbility.SummonGoatman
            ? new Point3D(Owner.X + 2, Owner.Y + 2, Owner.Z) : Target.Location;
        Mana = NecromancerEffects.ManaBalance256(Owner); Life = SkillHealth.Balance256(Target);
        Cost = SkillMechanics.Values(Owner, Ability).Mana256;
        File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"),
            JsonSerializer.Serialize(new { player = Point(Origin), cursor = Point(Aim),
                wireClass = (int)profile.Class, ability = Ability.ToString(), manaCost256 = Cost }));
    }
    private static void Check()
    {
        Require(Owner.Location == Origin && NecromancerEffects.ManaBalance256(Owner) == Mana - Cost,
            $"Rendered {Ability} walked or paid incorrectly: mana={NecromancerEffects.ManaBalance256(Owner)}/{Mana - Cost}");
        if (Ability == CombatAbility.InnerSight)
        {
            Require(AmazonInnerSight.State(Target) is { Reduction: > 0 } state && state.Creator == Owner.Serial.Value,
                "Rendered Inner Sight did not reduce enemy defense");
        }
        else if (Ability == CombatAbility.SummonSpiritWolf)
        {
            var pet = DruidSpiritWolves.For(Owner).SingleOrDefault();
            Require(pet is { Alive: true, Deleted: false } && pet.ControlMaster == Owner && pet.Map == Owner.Map,
                "Rendered summon did not create the original learned Spirit Wolf");
        }
        else if (Ability == CombatAbility.SummonGoatman)
        {
            var pet = WarlockGoatmanWorld.For(Owner).SingleOrDefault();
            Require(pet is { Alive: true, Deleted: false } && pet.ControlMaster == Owner && pet.Map == Owner.Map
                && DemonNativeState.Controller(pet) != null, "Rendered summon did not create its original controlled demon");
        }
        else
        {
            bool attributed = false;
            foreach (var entry in Target.DamageEntries)
            { if (entry.Damager == Owner && entry.DamageGiven > 0) { attributed = true; } }
            Require(SkillHealth.Balance256(Target) < Life && attributed,
                "Rendered " + Ability + " did not deliver owner-attributed damage");
        }
    }
}
