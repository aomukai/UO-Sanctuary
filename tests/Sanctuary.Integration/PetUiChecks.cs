using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PetUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(PetUiChecks));
    private static PlayerMobile _player;
    private static Corpse _near, _hovered;
    private static Point3D _origin;
    private static GraveyardSkeleton _foe;
    private static SkeletonGuardian _guardian;
    private static long _experience;
    private static CombatRandom _random;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = (PlayerMobile)Accounts.GetAccount("combatui")[0];
            SkeletonPets.Dismiss(_player);
            var dagger = _player.FindItemOnLayer(Layer.OneHanded) as Dagger ?? _player.Backpack.FindItemByType<Dagger>();
            Require(dagger != null && (dagger.Parent == _player || _player.EquipItem(dagger)), "No starting dagger for binding Teeth");
            _origin = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
            _player.MoveToWorld(_origin, Map.Felucca);
            ClassCreation.Profiles.AwardExperience(_player, 500);
            _player.Mana = _player.ManaMax;
            _near = MakeCorpse(1);
            _hovered = MakeCorpse(4);
            _hovered.DropItem(new Dagger());
            TalentGump.DisplayTo(_player);
            Require((uint)BaseGump.GetTypeId(typeof(TalentGump)) == ClassGumpProtocol.Talents,
                "Talent click areas do not match the server gump type");
        });
        Register("learned", () => Require(ClassCreation.Profiles.GetRank(_player.Serial, "necromancer.raiseskeleton") == 1
            && ClassCreation.Profiles.AvailablePoints(_player.Serial) == 0, "Actual Learn click did not spend the earned skill point"));
        Register("bound", () =>
        {
            var bindings = SkillBindings.Instance.Get(_player);
            Require(bindings.Left == CombatAbility.Teeth && bindings.Right == CombatAbility.RaiseSkeleton,
                "Actual binding buttons did not assign both mouse skills");
            _player.CloseGump<BindingGump>();
            _player.CloseGump<TalentGump>();
        });
        Register("hovered", () => Require(_hovered.SanctuaryConsumed && !_near.SanctuaryConsumed
            && SkeletonPets.Count(_player) == 1 && !_hovered.Deleted, "Right mouse did not prefer the hovered corpse and preserve its loot"));
        Register("fight-setup", () =>
        {
            SkeletonGuardian guardian = null;
            foreach (var follower in _player.AllFollowers)
            {
                guardian ??= follower as SkeletonGuardian;
            }
            Require(guardian != null, "No guardian for combat test");
            _guardian = guardian;
            guardian.MoveToWorld(new Point3D(_origin.X + 2, _origin.Y, _origin.Z), Map.Felucca);
            _foe = new GraveyardSkeleton { Frozen = true, CantWalk = true, Hits = 1 };
            _foe.MoveToWorld(new Point3D(_origin.X + 3, _origin.Y, _origin.Z), Map.Felucca);
            _foe.AIObject.AITimer.Stop();
            ClassCreation.Profiles.TryGet(_player.Serial, out var profile);
            _experience = profile.Experience;
            _random = new CombatRandom();
            _foe.DoHarmful(_player);
        });
        Register("fought", () =>
        {
            _random?.Dispose();
            _random = null;
            ClassCreation.Profiles.TryGet(_player.Serial, out var profile);
            Require(!_foe.Alive && profile.Experience == _experience + 34 && _player.Hits == _player.HitsMax,
                $"Guardian did not automatically fight an attacker using timed melee and award its owner XP: foe={_foe.Alive}/{_foe.Hits}/{_foe.Location}, pet={_guardian.Location}/{_guardian.ControlOrder}/{_guardian.Combatant?.Serial}, enemy={_guardian.IsEnemy(_foe)}, los={_guardian.InLOS(_foe)}, xp={profile.Experience}/{_experience}, killer={_foe.LastKiller?.GetType().Name}/{ClassLoot.Killer(_foe)?.Serial}, awards={!_foe.NoKillAwards}, corpseLOS={_player.InLOS(_foe.Corpse)}, life={_player.Hits}/{_player.HitsMax}");
            _foe.Corpse?.Delete();
        });
        Register("class", () => TalentGump.DisplayTo(_player));
        Register("pets", () =>
        {
            Require(_player.NetState.GetGumps().Has<PetStatsGump>(), "Pets button did not open summon statistics");
            _player.CloseGump<PetStatsGump>();
        });
        Register("reset", () =>
        {
            SkeletonPets.Dismiss(_player);
            _player.Mana = _player.ManaMax;
            ClassCombat.Stop(_player);
        });
        Register("nearest", () => Require(_near.Deleted && SkeletonPets.Count(_player) == 1,
            "Hotkey did not raise the nearest corpse when no corpse was hovered"));
        Register("left-setup", () =>
        {
            SkeletonPets.Dismiss(_player);
            _near = MakeCorpse(1);
            _player.Mana = _player.ManaMax;
            SkillBindings.Instance.Assign(_player, true, CombatAbility.RaiseSkeleton);
            SkillBindings.Instance.Assign(_player, false, CombatAbility.Teeth);
        });
        Register("left", () => Require(_near.Deleted && SkeletonPets.Count(_player) == 1
            && _player.Location == _origin, "Left mouse summoning moved the caster or failed"));
        Register("relog-prepare", () =>
        {
            foreach (var follower in _player.AllFollowers)
            {
                if (follower is SkeletonGuardian pet)
                {
                    _guardian = pet;
                }
            }
            Require(_guardian is { Deleted: false }, "No guardian for the real relog");
            _guardian.Hits = 13;
        });
        Register("offline", () => Require(_player.NetState == null && !_guardian.Deleted
            && _guardian.Map == Map.Internal && _guardian.ControlMaster == _player
            && _guardian.Combatant == null && _guardian.Hits == 13 && SkeletonPets.Count(_player) == 1,
            "Client disconnect did not log out the same injured guardian"));
        Register("online", () => Require(_player.NetState is { Running: true } && !_guardian.Deleted
            && _guardian.Map == _player.Map && _guardian.InRange(_player, 4)
            && _guardian.ControlMaster == _player && _guardian.ControlOrder == OrderType.Guard
            && _guardian.Hits == 13 && SkeletonPets.Count(_player) == 1,
            "Real client relog lost, healed or duplicated the guardian"));
        Register("final", () =>
        {
            Require(ClassCreation.Profiles.TryGet(_player.Serial, out var profile) && profile.Level == 2
                && ClassCreation.Profiles.AvailableStatPoints(_player.Serial) == 5,
                "Actual summoning UI changed earned progression");
            SkeletonPets.Dismiss(_player);
            _hovered.Delete();
            _near?.Delete();
        });
    }
    private static Corpse MakeCorpse(int dx)
    {
        var monster = new GraveyardSkeleton { Frozen = true, NoKillAwards = true };
        monster.MoveToWorld(new Point3D(_origin.X + dx, _origin.Y, _origin.Z), Map.Felucca);
        _player.DoHarmful(monster);
        monster.Damage(1000, _player);
        if (monster.Alive)
        {
            monster.Kill();
        }
        var corpse = monster.Corpse as Corpse;
        Require(corpse != null, "No summoning test corpse");
        // Remove generated loot so consuming an empty body has a visible disappearance.
        while (corpse.Items.Count > 0)
        {
            corpse.Items[0].Delete();
        }
        return corpse;
    }
    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static void Register(string suffix, Action action)
    {
        string command = "pet-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Actual client summon controls", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                {
                    player = new { x = _player.X, y = _player.Y, z = _player.Z },
                    near = new { x = _near.X, y = _near.Y, z = _near.Z },
                    hovered = new { x = _hovered.X, y = _hovered.Y, z = _hovered.Z }
                }));
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", command);
            }
        }));
    }
}
