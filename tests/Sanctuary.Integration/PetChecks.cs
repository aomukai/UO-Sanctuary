using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PetChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(PetChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "pet-check.json");
    public static void Configure()
    {
        Register("pet-check", Check);
        Register("pet-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Summoning and earned progression checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", command);
            }
        }));
    private static void Require(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
    private static CharacterProfile Profile(PlayerMobile p)
    {
        ClassCreation.Profiles.TryGet(p.Serial, out var profile);
        return profile;
    }
    private static Corpse Kill(PlayerMobile owner, int dx)
    {
        var enemy = new GraveyardSkeleton { Frozen = true };
        enemy.MoveToWorld(CombatPractice.FindFloor(owner.Map, owner.X + dx, owner.Y), owner.Map);
        owner.DoHarmful(enemy);
        enemy.Damage(1000, owner);
        if (enemy.Alive)
        {
            enemy.Kill();
        }
        return enemy.Corpse as Corpse;
    }
    private static void ExactPlayerKill()
    {
        var player = new PlayerMobile { Player = true, Body = 0x190, Name = "ExactKill" };
        player.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 1440, 1695), Map.Trammel);
        var dagger = new Dagger();
        ClassGear.Profiles.MarkStarter(dagger);
        player.EquipItem(dagger);
        var enemy = new GraveyardSkeleton { Frozen = true, Hits = 6 };
        enemy.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, player.X + 2, player.Y), Map.Trammel);
        try
        {
            Require(ClassCombat.Perform(player, CombatAbility.Teeth, enemy, enemy.Location, Core.TickCount),
                "Could not start the exact-life player attack");
            Require(ClassCombat.ResolveAction(player, ClassCombat.For(player).EffectAt) && !enemy.Alive
                && enemy.LastKiller == player && Profile(player).Experience == 34,
                "Exact-life Teeth kill lost death or reward credit");
        }
        finally
        {
            enemy.Corpse?.Delete();
            enemy.Delete();
            player.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
    private static void Check()
    {
        ExactPlayerKill();
        var p = new PlayerMobile { Player = true, Body = 0x190, Name = "PetCheck" };
        p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p, DiabloClass.Necromancer);
        p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 1440, 1695), Map.Trammel);
        CheckSkeletonStats(p);
        Require(!ClassCombat.Available(p, CombatAbility.RaiseSkeleton), "Unlearned summoning was available");
        for (int i = 0; i < 15; i++)
        {
            var killed = Kill(p, 2);
            killed?.Delete();
        }
        Require(Profile(p).Experience == 510 && Profile(p).Level == 2
            && ClassCreation.Profiles.AvailablePoints(p.Serial) == 1
            && ClassCreation.Profiles.AvailableStatPoints(p.Serial) == 5, "Real kills did not award level 2 and earned points");
        Require(ClassCreation.Profiles.TryTrain(p.Serial, "necromancer.raiseskeleton", out _), "Earned skill point could not train Raise Skeleton");
        Require(!ClassCreation.Profiles.TryTrain(p.Serial, "necromancer.teeth", out _), "Skill point was spent twice");
        Require(ClassCreation.Profiles.TrySpendStat(p, CharacterStat.Vitality)
            && ClassCreation.Profiles.AvailableStatPoints(p.Serial) == 4, "Earned stat point could not be spent");
        Require(SkillBindings.Instance.Assign(p, true, CombatAbility.RaiseSkeleton)
            && SkillBindings.Instance.Assign(p, false, CombatAbility.BasicAttack), "Both mouse bindings could not be assigned");
        var near = Kill(p, 1);
        var far = Kill(p, 3);
        far.DropItem(new Dagger());
        Require(SkeletonPets.FindCorpse(p, far.Serial) == far && SkeletonPets.FindCorpse(p, Serial.Zero) == near,
            "Hovered and nearest corpse selection priorities were wrong");
        int mana = p.Mana;
        long now = Core.TickCount;
        Require(ClassCombat.CastSummon(p, far.Serial, now), "Learned summoning could not start");
        var session = ClassCombat.For(p);
        Require(!ClassCombat.ResolveAction(p, now) && SkeletonPets.Count(p) == 0 && p.Mana == mana,
            "Summon bypassed the casting windup");
        Require(ClassCombat.ResolveAction(p, session.EffectAt) && SkeletonPets.Count(p) == 1
            && p.Mana == mana - 6 && far.SanctuaryConsumed && !far.Deleted && ClassLoot.HasContents(far),
            "Summon did not charge mana, consume the selected corpse, or preserve unclaimed loot");
        SkeletonGuardian pet = null;
        foreach (var follower in p.AllFollowers)
        {
            pet ??= follower as SkeletonGuardian;
        }
        Require(pet is { Controlled: true, Summoned: true, Commandable: false, ControlOrder: OrderType.Guard }
            && pet.ControlMaster == p && pet.CombatProfile.Level == 2 && pet.HitsMax == 21
            && ClassStats.AttackRating(pet) == 32 && ClassStats.Defense(pet) == 32
            && pet.CombatProfile.MinDamage == 1 && pet.CombatProfile.MaxDamage == 2,
            "Skeleton did not receive level/rank stats and the guard-me order");
        Require(!ClassCombat.IsHostile(p, pet) && !pet.CanBeHarmful(p, false) && !p.CanBeHarmful(pet, false),
            "Owner and skeleton were hostile");
        int ownerLife = p.Hits, petLife = pet.Hits;
        Require(AOS.Damage(pet, p, 100, 100, 0, 0, 0, 0) == 0
            && AOS.Damage(p, pet, 100, 0, 100, 0, 0, 0) == 0, "Physical or spell friendly damage was accepted");
        p.Damage(100, pet);
        pet.Damage(100, p);
        Require(p.Hits == ownerLife && pet.Hits == petLife, "Direct damage bypassed friendly-fire protection");
        var healer = new GraveyardHealer();
        healer.MoveToWorld(p.Location, p.Map);
        Require(!pet.IsEnemy(healer) && !pet.CanBeHarmful(healer, false), "Guardian could attack a friendly healer");
        healer.Delete();
        var original = pet;
        Require(SkeletonPets.Raise(p, near) && original.Deleted && SkeletonPets.Count(p) == 1
            && p.Mana == mana - 12, "Summoning at the rank limit did not replace the oldest guardian");
        pet = null;
        foreach (var follower in p.AllFollowers)
        {
            pet ??= follower as SkeletonGuardian;
        }
        long before = Profile(p).Experience;
        var foe = new GraveyardSkeleton { Frozen = true };
        foe.MoveToWorld(CombatPractice.FindFloor(p.Map, p.X + 2, p.Y), p.Map);
        foe.DoHarmful(p);
        pet.AIObject.DoOrderGuard();
        Require(pet.ControlOrder == OrderType.Guard && pet.Combatant == foe, "Guard me did not engage an attacker of the owner");
        pet.DoHarmful(foe);
        foe.Damage(1000, pet);
        if (foe.Alive)
        {
            foe.Kill();
        }
        Require(Profile(p).Experience == before + 34, "Pet kill did not award XP to its owner");
        foe.Corpse?.Delete();
        pet.AIObject.DoOrderGuard();
        Require(pet.ControlOrder == OrderType.Guard && pet.Combatant == null, "Guardian did not stand down after target death");
        near.Delete();
        far.Delete();
        pet.Hits = 13;
        // Exercise the real auto-stable/login hooks, including repeated logout notifications.
        p.AutoStablePets();
        p.AutoStablePets();
        Require(!pet.Deleted && pet.Map == Map.Internal && pet.ControlMaster == p && pet.Controlled
            && !pet.IsStabled && !(p.AutoStabled?.Contains(pet) ?? false) && SkeletonPets.Count(p) == 1
            && pet.Combatant == null && pet.Hits == 13, "Logout did not safely retain the same injured guardian");
        p.ClaimAutoStabledPets();
        p.ClaimAutoStabledPets();
        Require(pet.Map == p.Map && pet.InRange(p, 4) && p.InLOS(pet) && pet.ControlOrder == OrderType.Guard
            && pet.Hits == 13 && SkeletonPets.Count(p) == 1, "Login did not resume the guardian without healing or duplication");
        var mastered = new SkeletonGuardian();
        mastered.Initialize(p, 20, 20);
        mastered.SetControlMaster(p);
        // Emulate a guardian saved with the old, understated derived values.
        mastered.SetHits(359);
        mastered.SetDamage(77, 79);
        mastered.Hits = 300;
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new[] { p.Serial.Value, pet.Serial.Value, mastered.Serial.Value }));
    }
    private static void CheckSkeletonStats(PlayerMobile owner)
    {
        Require(SkeletonStats.For(2, 1, 0) is { Life: 21, AttackRating: 32, Defense: 32, MinDamage: 1, MaxDamage: 2 },
            "Rank-one Normal stats lost base monster-level attack/defense");
        Require(SkeletonStats.For(2, 3, 1) is { Life: 29, AttackRating: 77, Defense: 77 }
            && SkeletonStats.For(2, 4, 1).Life == 43,
            "Mastery life was not multiplied at the rank-four life breakpoint");
        Require(SkeletonStats.For(99, 20, 20) is { Life: 1719, AttackRating: 1673, Defense: 1199, MinDamage: 124, MaxDamage: 127 }
            && SkeletonStats.For(20, 19, 0) is { MinDamage: 31, MaxDamage: 33 },
            "High-rank skeleton stats used tooltip totals instead of the summon formulas");
        var pet = new SkeletonGuardian();
        pet.Initialize(owner, 20, 20);
        pet.MoveToWorld(owner.Location, owner.Map);
        try
        {
            pet.Hits = 10;
            pet.RecoverLife(1000);
            pet.RecoverLife(3047);
            Require(pet.Hits == 10, "Fractional recovery rounded up early");
            pet.RecoverLife(3048);
            pet.RecoverLife(5096);
            Require(pet.Hits == 12, "Skeleton did not recover one life per 2048 ms");
            pet.RecoverLife(21480);
            Require(pet.Hits == 20, "Recovery depended on the number of AI ticks or scaled with Mastery life");
            pet.Hits = pet.HitsMax - 1;
            pet.RecoverLife(100000);
            Require(pet.Hits == pet.HitsMax, "Recovery exceeded or failed to reach maximum life");
            pet.Hits--;
            pet.RecoverLife(102047);
            Require(pet.Hits == pet.HitsMax - 1, "Reaching full life retained excess fractional recovery");
            pet.RecoverLife(102048);
            pet.RecoverLife(200000);
            pet.Hits--;
            pet.RecoverLife(200001);
            Require(pet.Hits == pet.HitsMax - 1, "Full life banked recovery for later damage");
            pet.SuspendActions();
            pet.Internalize();
            pet.RecoverLife(300000);
            Require(pet.Hits == pet.HitsMax - 1, "An internalized skeleton recovered life");
            pet.SuspendActions();
            pet.MoveToWorld(owner.Location, owner.Map);
            pet.RecoverLife(400000);
            Require(pet.Hits == pet.HitsMax - 1, "Suspension time became login healing");
        }
        finally
        {
            pet.Delete();
        }
    }
    private static void Reload()
    {
        var ids = JsonSerializer.Deserialize<uint[]>(File.ReadAllText(Manifest));
        var p = World.FindMobile((Serial)ids[0]) as PlayerMobile;
        var pet = World.FindMobile((Serial)ids[1]) as SkeletonGuardian;
        var mastered = World.FindMobile((Serial)ids[2]) as SkeletonGuardian;
        Require(mastered is { Hits: 300, HitsMax: 1719, Rank: 20, Mastery: 20 }
            && mastered.CombatProfile.MinDamage == 124 && mastered.CombatProfile.MaxDamage == 127
            && mastered.DamageMin == 124 && mastered.DamageMax == 127,
            "Saved skeleton stats were not corrected while preserving current life on restart");
        mastered.Delete();
        Require(p != null && pet is { Deleted: false, Controlled: true, Summoned: true, Hits: 13 }
            && pet.ControlMaster == p && pet.Map == Map.Internal && SkeletonPets.Count(p) == 1
            && pet.Level == 2 && pet.Rank == 1 && pet.Mastery == 0 && pet.CombatProfile.Life == 21,
            "Saved guardian identity, ownership, health or stats were lost on restart");
        var bindings = SkillBindings.Instance.Get(p);
        Require(bindings.Left == CombatAbility.RaiseSkeleton && bindings.Right == CombatAbility.BasicAttack,
            "Mouse bindings did not survive save/reload");
        Require(Profile(p).Level == 2 && Profile(p).Experience == 612
            && ClassCreation.Profiles.GetRank(p.Serial, "necromancer.raiseskeleton") == 1
            && ClassCreation.Profiles.Stats(p.Serial).Vitality == 1, "Earned progression was not preserved");
        p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 1440, 1695), Map.Trammel);
        p.ClaimAutoStabledPets();
        Require(pet.Map == p.Map && pet.InRange(p, 4) && pet.ControlOrder == OrderType.Guard && pet.Hits == 13,
            "Saved guardian did not return with the login hook");
        p.AutoStablePets();
        p.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, 1382, 1495), Map.Felucca);
        p.ClaimAutoStabledPets();
        Require(pet.Map == p.Map && pet.InRange(p, 4) && pet.Hits == 13 && SkeletonPets.Count(p) == 1,
            "Guardian did not return to its owner's current map");
        p.Mana = p.ManaMax;
        var enemy = new GraveyardSkeleton { NoKillAwards = true };
        var summonPoint = p.Location;
        for (int dx = -2; dx <= 2; dx++)
        {
            var candidate = CombatPractice.FindFloor(p.Map, p.X + dx, p.Y + 1);
            if (p.Map.CanSpawnMobile(candidate.X, candidate.Y, candidate.Z) && p.InLOS(candidate))
            {
                summonPoint = candidate;
                break;
            }
        }
        Require(summonPoint != p.Location, "No unoccupied tile for the summon reload fixture");
        enemy.MoveToWorld(summonPoint, p.Map);
        p.DoHarmful(enemy);
        enemy.Damage(1000, p);
        if (enemy.Alive)
        {
            enemy.Kill();
        }
        var corpse = (Corpse)enemy.Corpse;
        Require(SkeletonPets.Raise(p, corpse), $"Saved learned skill could not summon again: rank={SkeletonPets.Rank(p)}, mana={p.Mana}, count={SkeletonPets.Count(p)}, usable={SkeletonPets.Usable(p, corpse)}, spawn={p.Map.CanSpawnMobile(corpse.X, corpse.Y, corpse.Z)}, see={p.CanSee(corpse)}, los={p.InLOS(corpse)}, criminal={corpse.IsCriminalAction(p)}, location={p.Location}/{corpse.Location}, alive={p.Alive}");
        p.Kill();
        Require(SkeletonPets.Count(p) == 0 && Profile(p).Experience == 612, "Owner death retained a summon or removed XP");
        corpse.Delete();
        p.Corpse?.Delete();
        p.Delete();
        ClassCreation.Profiles.PruneDeleted();
    }
}
