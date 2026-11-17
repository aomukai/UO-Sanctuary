using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Regions;

namespace Sanctuary.Integration;

public static class DruidSafeAreaChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(DruidSafeAreaChecks));
    private static readonly CombatAbility[] Skills = [CombatAbility.Raven, CombatAbility.SummonSpiritWolf,
        CombatAbility.SummonDireWolf, CombatAbility.SummonGrizzly, CombatAbility.OakSage,
        CombatAbility.HeartOfWolverine, CombatAbility.SpiritOfBarbs, CombatAbility.PoisonCreeper,
        CombatAbility.CarrionVine, CombatAbility.SolarCreeper];
    private sealed record Actor(uint Owner, uint Pet, CombatAbility Ability, Point3D Origin, Point3D Town,
        int Map, int Life, int Maximum, int Mana, int Rank, int Slots, long Order, int? RavenBudget);
    private sealed record Saved(Actor[] Actors, int Generation = 0);
    private static string Manifest => Path.Join(Core.BaseDirectory, "druid-safe-area-fixtures.json");

    public static void Configure()
    {
        Register("druid-safe-area-check", Check);
        Register("druid-safe-area-reload-check", Reload);
    }

    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Original Druid summon identities across actual safe-region parking and saved return", _ => Core.LoopContext.Post(() =>
        {
            try { action(); Log.Information("{Command}: passed", command); }
            catch (Exception error) { Log.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }

    private static GuardedRegion Town(PlayerMobile owner, Point3D point, Map map)
    {
        var region = new GuardedRegion("DruidParking" + owner.Serial.Value, map, 1000,
            new Rectangle2D(point.X - 2, point.Y - 2, 5, 5));
        region.Register();
        return region;
    }

    private static SkeletonGuardian Create(PlayerMobile owner, CombatAbility ability)
    {
        // Exercise the real prepared actor implementations. Learned casting and
        // hard-point birth snapshots have separate current input/birth proofs.
        var point = new Point3D(owner.X + 1, owner.Y, owner.Z);
        var at = TimedEffects.Now.AddDays(1);
        return ability switch
        {
            CombatAbility.Raven => DruidRavenBody.CreatePrepared(owner, point, at, 71),
            CombatAbility.SummonSpiritWolf => DruidSpiritWolfWorld.Create(owner, point, at, 71),
            CombatAbility.SummonDireWolf => DruidDireWolfWorld.Create(owner, point, at, 71),
            CombatAbility.SummonGrizzly => DruidGrizzlyWorld.Create(owner, point, at, 71),
            CombatAbility.OakSage or CombatAbility.HeartOfWolverine or CombatAbility.SpiritOfBarbs
                => DruidOakWorld.Create(owner, point, at, 71, ability),
            CombatAbility.PoisonCreeper => DruidCreeperWorld.Create(owner, point, at, 71),
            CombatAbility.CarrionVine or CombatAbility.SolarCreeper => DruidRecyclerWorld.Create(owner, ability,
                point, at, 71, ability == CombatAbility.SolarCreeper
                    ? RecyclerRecoveryModel.SanctuaryApprovedSolar : RecyclerRecoveryModel.LegacySource),
            _ => throw new InvalidOperationException("Unknown fixture species")
        };
    }

    private static void Verify(PlayerMobile owner, SkeletonGuardian pet, Actor saved, bool parked)
    {
        Require(pet is { Alive: true, Deleted: false } && pet.ControlMaster == owner
            && pet.Serial.Value == saved.Pet && pet.Rank == saved.Rank
            && SkillHealth.Balance256(pet) == saved.Life && SkillHealth.Maximum256(pet) == saved.Maximum
            && pet.Mana == saved.Mana && BattleCommand.PetOrder(pet) == saved.Order,
            "Original identity, wounds, resources, rank or order changed for " + saved.Ability);
        Require((pet as DruidRavenBody)?.Prepared.Remaining == saved.RavenBudget,
            "Parking reset Raven's remaining attack budget");
        Require(MinionSafeAreas.Parked(pet) == parked && pet.ControlSlots == (parked ? 0 : saved.Slots)
            && pet.Map == (parked ? Map.Internal : owner.Map), "Wrong parking/return state for " + saved.Ability);
        if (parked)
        {
            Require(pet.Combatant == null && pet.ControlTarget == null && MinionSafeAreas.NoRecovery(pet),
                "Parked Druid actor retained combat or recovery");
        }
    }

    private static void Cycle(PlayerMobile owner, SkeletonGuardian pet, Actor saved)
    {
        // Offline player bodies load on Internal; their saved world facet is
        // authoritative for the temporary region and subsequent relocation.
        var region = Town(owner, saved.Town, Map.Maps[saved.Map]);
        try
        {
            owner.MoveToWorld(saved.Town, Map.Maps[saved.Map]);
            Verify(owner, pet, saved, true);
            PersistentMinions.Resume(owner);
            Verify(owner, pet, saved, true);
            int followers = owner.Followers;
            DateTime at = TimedEffects.Now;
            Require(TimedEffects.AddRestoration(pet, true, 10, 1000, at), "Missing parked restoration control");
            TimedEffects.Settle(pet, at.AddMilliseconds(1001));
            Verify(owner, pet, saved, true);
            owner.MoveToWorld(saved.Origin, owner.Map);
            PersistentMinions.Resume(owner); // Offline fixture: reuse the same return admission called on login/movement.
            Verify(owner, pet, saved, false);
            Require(owner.Followers == followers + saved.Slots, "Return restored follower slots twice");
            owner.MoveToWorld(saved.Town, owner.Map);
            Verify(owner, pet, saved, true);
        }
        finally { region.Unregister(); }
    }

    private static void Check()
    {
        var actors = new System.Collections.Generic.List<Actor>();
        for (int i = 0; i < Skills.Length; i++)
        {
            var owner = SpiritWolfWorldChecks.Fresh(sceneOffset: i * 96, column: 6000);
            ClassSkillChecks.Train(owner, SkillMechanics.For(Skills[i]).TalentId, 1);
            var pet = Create(owner, Skills[i]);
            SkillHealth.SetBalance256(pet, SkillHealth.Maximum256(pet) / 2 + 1);
            var saved = new Actor(owner.Serial.Value, pet.Serial.Value, Skills[i], owner.Location,
                new Point3D(owner.X + 16, owner.Y, owner.Z), owner.Map.MapID,
                SkillHealth.Balance256(pet), SkillHealth.Maximum256(pet), pet.Mana, pet.Rank,
                pet.ControlSlots, BattleCommand.PetOrder(pet), (pet as DruidRavenBody)?.Prepared.Remaining);
            Cycle(owner, pet, saved);
            actors.Add(saved);
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(actors.ToArray())));
    }

    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(saved.Generation < 2 && saved.Actors.Length == Skills.Length, "Wrong saved Druid roster");
        foreach (var row in saved.Actors)
        {
            var owner = World.FindMobile((Serial)row.Owner) as PlayerMobile;
            var pet = World.FindMobile((Serial)row.Pet) as SkeletonGuardian;
            Verify(owner, pet, row, true);
            Cycle(owner, pet, row);
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved with { Generation = saved.Generation + 1 }));
    }
}
