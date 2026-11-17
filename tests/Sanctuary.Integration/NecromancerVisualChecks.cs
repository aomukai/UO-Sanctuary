using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class NecromancerVisualChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(NecromancerVisualChecks));
    private static PlayerMobile Owner;
    private static Corpse Corpse;
    private static SkeletonGuardian Pet;
    private static GraveyardZombie Target;
    private static int Life;
    private static int ManaBeforeTeeth;

    public static void Configure()
    {
        Register("setup", Setup);
        Register("raised", Raised);
        Register("teeth", Teeth);
        Register("fight", Fight);
        Register("fought", Fought);
    }
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static object Position(IEntity entity) => entity == null ? null
        : new { x = entity.X, y = entity.Y, z = entity.Z, serial = entity.Serial.Value };
    private static bool DamagedBy(Mobile attacker)
    {
        foreach (var entry in Target.DamageEntries)
        { if (entry.Damager == attacker && entry.DamageGiven > 0) { return true; } }
        return false;
    }
    private static void Register(string suffix, Action action)
    {
        string command = "necromancer-visual-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Rendered real-client summon, Teeth and natural guardian combat", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"),
                    JsonSerializer.Serialize(new { player = Position(Owner), corpse = Position(Corpse),
                        pet = Position(Pet), target = Position(Target) }));
                Log.Information("{Command}: passed", command);
            }
            catch (Exception error) { Log.Error(error, "{Command}: failed", command); }
        }));
    }
    private static void Setup()
    {
        Owner = Accounts.GetAccount("classui")?[0] as PlayerMobile;
        Require(Owner?.NetState != null && ClassCreation.Profiles.TryGet(Owner.Serial, out var profile)
            && profile.Class == DiabloClass.Necromancer, "No actual GUI-created Necromancer");
        ClassCombat.Stop(Owner);
        ClassCreation.Profiles.AdvanceLevel(Owner, 99);
        ClassSkillChecks.Train(Owner, "necromancer.raiseskeleton", 20);
        ClassSkillChecks.Train(Owner, "necromancer.skeletonmastery", 20);
        // Rank1 emits two spread rays; rank2 adds the centre ray needed for
        // this deliberately isolated single-target damage observation.
        ClassSkillChecks.Train(Owner, "necromancer.teeth", 2);
        // Reuse the accepted elevated native fixture corridor. Walkable floor
        // at dungeon height does not establish an unobstructed missile lane.
        var terrain = SpiritWolfWorldChecks.Fresh(column: 6200);
        var origin = terrain.Location;
        var map = terrain.Map;
        terrain.Delete();
        Owner.MoveToWorld(origin, map);
        Owner.Mana = Owner.ManaMax;
        Owner.Target = null;
        Owner.CloseGump<TalentGump>();
        Require(SkillBindings.Instance.AssignQuick(Owner, 0, CombatAbility.RaiseSkeleton)
            && SkillBindings.Instance.AssignQuick(Owner, 1, CombatAbility.Teeth), "Could not establish test quick bindings");
        SkillBindingNetwork.Send(Owner);
        var dead = new GraveyardZombie { NoKillAwards = true, Frozen = true, CantWalk = true };
        dead.MoveToWorld(new Point3D(Owner.X + 2, Owner.Y, Owner.Z), Owner.Map);
        dead.AIObject?.AITimer.Stop();
        dead.Damage(100000, Owner);
        if (dead.Alive) { dead.Kill(); }
        Corpse = dead.Corpse as Corpse;
        Require(SkeletonPets.Usable(Owner, Corpse), "Visual corpse is unavailable");
    }
    private static void Raised()
    {
        Pet = NecromancerSkeletons.For(Owner, CombatAbility.RaiseSkeleton).SingleOrDefault();
        Require(Pet is { Alive: true, Deleted: false } && Corpse.SanctuaryConsumed,
            "Actual F1 client input did not raise the original learned Warrior");
        // Isolate player Teeth damage from the following natural pet combat.
        // Ordinary skill input can resume the owner's roster. Use the actual
        // native action lock instead of assuming manual parking persists.
        Pet.Frozen = Pet.CantWalk = true;
        Target = new GraveyardZombie { NoKillAwards = true, Frozen = true, CantWalk = true };
        Target.SetHits(20000);
        Target.SetResistance(ResistanceType.Physical, 0);
        Target.MoveToWorld(new Point3D(Owner.X + 6, Owner.Y, Owner.Z), Owner.Map);
        Target.AIObject?.AITimer.Stop();
        TimedEffects.PreventHealing(Target, TimedEffects.Now);
        Log.Information("Rendered target eligibility before aggression:hostile={Hostile},harmful={Harmful},town={Town},magic={Magic}",
            ClassCombat.IsHostile(Owner, Target), Owner.CanBeHarmful(Target, false), Server.Spells.SpellHelper.IsTown(Target.Location, Owner), NamedElements.MagicResistance(Target));
        Target.DoHarmful(Owner);
        Life = SkillHealth.Balance256(Target);
        ManaBeforeTeeth = Owner.Mana;
    }
    private static void Teeth()
    {
        Require(Pet.Frozen && SkillHealth.Balance256(Target) < Life
            && DamagedBy(Owner),
            $"Actual F2 Teeth input: frozen={Pet.Frozen},petMap={Pet.Map},life={SkillHealth.Balance256(Target)}/{Life},ownerDamage={DamagedBy(Owner)},mana={Owner.Mana}/{ManaBeforeTeeth},pending={ClassCombat.For(Owner).PendingAbility},point={ClassCombat.For(Owner).PendingPoint},hostile={ClassCombat.IsHostile(Owner, Target)},harmful={Owner.CanBeHarmful(Target, false)},flights={JsonSerializer.Serialize(NecromancerSpellMissiles.Snapshot().Flights.Where(f => f.Owner == Owner.Serial.Value).Select(f => new { f.Frame, f.X, f.Y, f.Fault }))}");
    }
    private static void Fight()
    {
        Life = SkillHealth.Balance256(Target);
        Pet.Frozen = Pet.CantWalk = false;
        PersistentMinions.Resume(Owner);
        Require(Pet.Map == Owner.Map, "Original pet did not return for combat");
        Target.DoHarmful(Owner);
    }
    private static void Fought()
    {
        Require(Pet.Alive && Pet.ControlMaster == Owner && SkillHealth.Balance256(Target) < Life
            && DamagedBy(Pet),
            "Natural original Warrior did not attack the visible enemy");
    }
}
