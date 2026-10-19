using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CreatureExperienceChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CreatureExperienceChecks));
    private static readonly List<Mobile> _mobiles = new();
    private static readonly List<Item> _items = new();
    private static PlayerMobile _owner;
    private static GraveyardLich _lich;
    private static Corpse _source;
    private static Point3D _point;
    private static long _before;
    private static string Manifest => Path.Join(Core.BaseDirectory, "creature-xp-check.json");
    private sealed record Saved(uint Player, long Experience, uint Corpse, uint Raised);
    public static void Configure()
    {
        Register("creature-xp-check", Setup);
        Register("creature-xp-reload-check", Reload);
        Register("creature-xp-claims-check", Claims);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Profile XP, pet credit and revival persistence", _ => Core.LoopContext.Post(() => Run(command, action)));
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    private static void Run(string command, Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            _logger.Error(error, "{Command}: failed", command);
            Cleanup();
        }
    }
    private static void Later(int ms, Action action) => Timer.StartTimer(TimeSpan.FromMilliseconds(ms), () => Run("creature-xp-check", action));
    private static T At<T>(T mobile) where T : Mobile
    {
        _mobiles.Add(mobile);
        mobile.MoveToWorld(_point, Map.Trammel);
        if (mobile is BaseCreature creature)
        {
            creature.AIObject?.AITimer.Stop();
            creature.Frozen = true;
        }
        return mobile;
    }
    private static PlayerMobile Player(string name)
    {
        var player = At(new PlayerMobile { Name = name, Player = true, Body = 0x190 });
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        return player;
    }
    private static long Xp(PlayerMobile player)
    {
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        return profile.Experience;
    }
    private static Corpse Kill(BaseCreature creature, Mobile killer)
    {
        creature.Damage(100000, killer);
        if (!creature.Deleted && creature.Alive) { creature.Kill(); }
        var corpse = creature.Corpse as Corpse;
        Require(corpse is { Deleted: false } || creature.Summoned, $"Real {creature.GetType().Name} death did not leave a corpse (deleted={creature.Deleted}, alive={creature.Alive})");
        if (corpse != null)
        {
            _items.Add(corpse);
            if (creature is GraveyardUndead { Raised: true })
            {
                Require(corpse.SanctuaryNoRewards && corpse.GetAmount(typeof(Gold)) == 0, "Revived life paid gold");
            }
        }
        return corpse;
    }
    private static void Setup()
    {
        _point = CombatPractice.FindFloor(Map.Trammel, 5530, 1150);
        _owner = Player("ProfileXP");
        var account = new Account("profile-xp-check", Guid.NewGuid().ToString("N"));
        account[0] = _owner;
        var skeleton = At(new Skeleton());
        Require(CreatureProfiles.Experience(skeleton) == 34, "Ordinary skeleton has no common-profile XP");
        var corpse = Kill(skeleton, _owner);
        Require(Xp(_owner) == 34 && corpse.SanctuaryNoRewards, "Ordinary UO skeleton bypassed common XP or first-death claim");
        Require(!CreatureExperience.Award(skeleton, corpse) && Xp(_owner) == 34, "Repeated callback paid twice");
        var pet = At(new Skeleton { ControlMaster = _owner, Controlled = true });
        Kill(At(new Zombie()), pet);
        Require(Xp(_owner) == 67, "Native pet kill did not pay 33 zombie XP to its owner");
        var summon = At(new Skeleton { SummonMaster = _owner, Summoned = true });
        Kill(At(new Zombie()), summon);
        Require(Xp(_owner) == 100, "Summoned killer did not pay its owner");
        Kill(At(new GraveyardSkeleton()), _owner);
        Require(Xp(_owner) == 134, "Custom skeleton baseline changed");
        Kill(At(new GraveyardLich()), _owner);
        Require(Xp(_owner) == 334, "Existing provisional lich XP changed");
        var ownPet = At(new Skeleton { ControlMaster = _owner, Controlled = true });
        Kill(ownPet, _owner);
        Kill(At(new Skeleton { Summoned = true, SummonMaster = _owner }), _owner);
        Kill(At(new Skeleton { NoKillAwards = true }), _owner);
        Kill(At(new Cow()), _owner);
        Require(Xp(_owner) == 334, "Pets, summons, no-awards or domestic animals paid XP");
        CheckRoster();
        var vendor = At(new Provisioner());
        // Native vendors refuse death; exercise the reward boundary with a synthetic corpse.
        var vendorBody = new Corpse(vendor, new List<Item>());
        vendorBody.MoveToWorld(_point, Map.Trammel);
        _items.Add(vendorBody);
        Require(!CreatureExperience.Award(vendor, vendorBody) && Xp(_owner) == 334, "Vendor paid XP");
        var enemy = At(new Skeleton());
        _owner.MoveToWorld(new Point3D(_point.X + LootProtocol.SightRange + 1, _point.Y, _point.Z), Map.Trammel);
        corpse = Kill(enemy, pet);
        _owner.MoveToWorld(_point, Map.Trammel);
        Require(Xp(_owner) == 334 && corpse.SanctuaryNoRewards && !CreatureExperience.Award(enemy, corpse),
            "Remote pet kill or later retry paid the owner");
        var npc = At(new Skeleton());
        Kill(At(new Zombie()), npc);
        Require(Xp(_owner) == 334, "Unowned NPC kill found an unrelated nearby player");
        var legacy = At(new PlayerMobile { Name = "LegacyXP", Player = true, Body = 0x190 });
        Kill(At(new Skeleton()), legacy);
        Require(Xp(_owner) == 334 && !ClassCreation.Profiles.TryGet(legacy.Serial, out _), "Non-class killer gained a class profile");
        CheckOverrides();
        // Crossing a real threshold must still grant the existing stat/talent budgets.
        ClassCreation.Profiles.AwardExperience(_owner, 150);
        Require(Xp(_owner) == 484 && ClassStats.Level(_owner) == 1, "XP setup unexpectedly levelled");
        var revivable = At(new GraveyardSkeleton());
        revivable.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, _point.X + 3, _point.Y + 2), Map.Trammel);
        revivable.AIObject.AITimer.Stop();
        _source = Kill(revivable, _owner);
        Require(Xp(_owner) == 518 && ClassStats.Level(_owner) == 2
            && ClassCreation.Profiles.AvailableStatPoints(_owner.Serial) == 5
            && ClassCreation.Profiles.AvailablePoints(_owner.Serial) == 1, "D2 threshold or level rewards changed");
        _before = Xp(_owner);
        _lich = At(new GraveyardLich { CantWalk = true });
        _lich.Frozen = false;
        Later(3200, () =>
        {
            Require(_lich.BeginRaise(_source), "Existing lich revival did not start");
            Later(950, Raised);
        });
    }
    private static void CheckOverrides()
    {
        // Change one in-memory fixture row, synchronously, restoring before any timer tick.
        // This exercises the same loader-resolved record used for per-type JSON overrides.
        var variants = (Dictionary<(string, MonsterDifficulty), CreatureVariant>)typeof(CreatureProfiles)
            .GetField("_variants", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var key = (typeof(Skeleton).FullName, MonsterDifficulty.Normal);
        var original = variants[key];
        var player = Player("HighXP");
        try
        {
            ClassCreation.Profiles.AwardExperience(player, (int)ExperienceLevels.Threshold(90));
            long before = Xp(player);
            variants[key] = original with { Rewards = original.Rewards with { Experience = 10000, Level = 90 } };
            Kill(At(new Skeleton()), player);
            Require(Xp(player) == before + 595, "Profile XP/actual level or D2 level-90 reduction was ignored");
            variants[key] = original with { Rewards = original.Rewards with { Experience = 10000, Level = 80 } };
            Kill(At(new Skeleton()), player);
            Require(Xp(player) == before + 595 + 29, "Level-gap reduction was not applied before high-level reduction");
            ClassCreation.Profiles.AdvanceLevel(player, 99);
            long capped = Xp(player);
            Kill(At(new Skeleton()), player);
            Require(Xp(player) == capped, "Level-99 character gained more XP");
            variants[key] = original with { Rewards = original.Rewards with { Experience = 0 } };
            Kill(At(new Skeleton()), _owner);
            Require(Xp(_owner) == 334, "Explicit zero reward fell back to another XP source");
            variants[key] = original with { Rewards = original.Rewards with { Enabled = false, Experience = 10000 } };
            Kill(At(new Skeleton()), _owner);
            Require(Xp(_owner) == 334, "Disabled reward policy awarded XP");
        }
        finally { variants[key] = original; }
    }
    private static void CheckRoster()
    {
        var player = Player("RosterXP");
        ClassCreation.Profiles.AdvanceLevel(player, 6);
        long before = Xp(player);
        var orc = At(new Orc());
        Require(ClassStats.Level(orc) == 6 && CreatureProfiles.Experience(orc) == 72,
            "Orc reward level disagrees with combat or the reviewed Goatman analogue");
        Kill(orc, player);
        Require(Xp(player) == before + 72, "Ordinary orc did not pay its common-profile reward");
        ClassCreation.Profiles.AdvanceLevel(player, 24);
        before = Xp(player);
        var drake = At(new Drake());
        Require(ClassStats.Level(drake) == 24 && CreatureProfiles.Experience(drake) == 566
            && CreatureProfiles.Snapshot(drake).Reference == "megademon1",
            "Drake inherited the full-size dragon level or reward");
        Kill(drake, player);
        Require(Xp(player) == before + 566, "Balrog-tier drake did not pay its level-matched reward");
        ClassCreation.Profiles.AdvanceLevel(player, 30);
        before = Xp(player);
        var dragon = new Dragon();
        int life = dragon.HitsMax, fire = dragon.FireResistance, damage = dragon.DamageMin;
        At(dragon);
        var row = CreatureProfiles.Variant(dragon);
        Require(!row.Enabled && !row.ApplyStats && row.Rewards.Enabled
            && ClassStats.Level(dragon) == 30 && CreatureProfiles.Experience(dragon) == 1001
            && dragon.HitsMax == life && dragon.FireResistance == fire && dragon.DamageMin == damage,
            "Reviewed XP failed to opt in separately from the disabled dragon combat conversion");
        Require(CreatureProfiles.Snapshot(dragon, MonsterDifficulty.Nightmare).Level == 57
            && CreatureProfiles.Experience(dragon, MonsterDifficulty.Nightmare) == 13057
            && CreatureProfiles.Snapshot(dragon, MonsterDifficulty.Hell).Level == 84
            && CreatureProfiles.Experience(dragon, MonsterDifficulty.Hell) == 64786
            && !CreatureProfiles.Variant(dragon, MonsterDifficulty.Hell).Enabled,
            "Prepared difficulty rewards mixed percentages, levels or active world difficulty");
        var pet = At(new Skeleton { Controlled = true, ControlMaster = player });
        Kill(dragon, pet);
        Require(Xp(player) == before + 1001, "Dragon did not pay the Normal players-1 reward to the pet's owner");
        var remoteLevel = At(new Dragon());
        var novice = Player("NoviceXP");
        Kill(remoteLevel, novice);
        Require(Xp(novice) == 20, "Native dragon still used fallback level 1 for low-level XP credit");
    }
    private static GraveyardUndead FindRaised()
    {
        foreach (var mob in Map.Trammel.GetMobilesInRange<GraveyardUndead>(_point, 5))
        {
            if (mob is { Raised: true, Deleted: false }) { return mob; }
        }
        return null;
    }
    private static void Raised()
    {
        var raised = FindRaised();
        Require(raised is { NoKillAwards: true } && Xp(_owner) == _before, $"Revival failed: found={raised != null}, noAwards={raised?.NoKillAwards}, xp={Xp(_owner)}/{_before}, sourceDeleted={_source.Deleted}, animated={_source.Animated}");
        raised.AIObject.AITimer.Stop();
        raised.Frozen = true;
        _mobiles.Add(raised);
        var next = Kill(raised, _owner);
        Require(Xp(_owner) == _before && next.SanctuaryNoRewards, "Revived profile mob paid a second time");
        // Retained contents preserve a spent corpse for the world-save check.
        next.DropItem(new Dagger { Name = "XP spent corpse fixture" });
        _lich.Frozen = false;
        Later(8200, () =>
        {
            Require(_lich.BeginRaise(next), "Second revival did not start");
            Later(950, () =>
            {
                var again = FindRaised();
                Require(again is { Raised: true, NoKillAwards: true } && Xp(_owner) == _before,
                    "Repeated revival reset rewards");
                again.AIObject.AITimer.Stop();
                again.Frozen = true;
                File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_owner.Serial.Value, Xp(_owner), next.Serial.Value, again.Serial.Value)));
                _mobiles.Remove(_owner);
                _items.Remove(next);
                Cleanup();
                _logger.Information("creature-xp-check: passed");
            });
        });
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var player = World.FindMobile((Serial)saved.Player) as PlayerMobile;
        var corpse = World.FindItem((Serial)saved.Corpse) as Corpse;
        Require(player != null && Xp(player) == saved.Experience && ClassStats.Level(player) == 2
            && corpse is { SanctuaryNoRewards: true }, "XP or spent corpse flag did not persist");
        // On the first reload, a saved live revived mob must still pay nothing.
        if (World.FindMobile((Serial)saved.Raised) is GraveyardUndead raised)
        {
            Require(raised.Raised && raised.NoKillAwards, "Saved revival lost no-awards state");
            player.MoveToWorld(_point = CombatPractice.FindFloor(Map.Trammel, 5530, 1150), Map.Trammel);
            raised.MoveToWorld(_point, Map.Trammel);
            raised.AIObject.AITimer.Stop();
            Kill(raised, player);
            Require(Xp(player) == saved.Experience, "Saved revived mob paid XP after restart");
        }
        _logger.Information("creature-xp-reload-check: passed");
    }
    private static void Claims()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(World.FindItem((Serial)saved.Corpse) is Corpse { SanctuaryNoRewards: true },
            "Spent corpse flag disappeared during later save/reload cycles");
        _logger.Information("creature-xp-claims-check: passed");
    }
    private static void Cleanup()
    {
        foreach (var mob in _mobiles) { mob.Delete(); }
        foreach (var item in _items) { item.Delete(); }
        _mobiles.Clear();
        _items.Clear();
        ClassCreation.Profiles.PruneDeleted();
    }
}
