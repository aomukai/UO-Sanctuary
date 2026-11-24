using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class StatUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(StatUiChecks));
    private static BaseWeapon _sheetWeapon;
    public static void Configure()
    {
        Register("setup", player =>
        {
            if (!ClassCreation.Profiles.AwardExperience(player, (int)ExperienceLevels.Threshold(2)))
            {
                throw new InvalidOperationException("Could not award test level");
            }
            var weapon = CombatTiming.Equipped(player) ?? player.Backpack.FindItemByType<Dagger>();
            if (weapon == null)
            {
                throw new InvalidOperationException("Character sheet fixture has no weapon");
            }
            _sheetWeapon = weapon;
            player.EquipItem(weapon);
        });
        Register("stowed", player =>
        {
            if (CombatTiming.Equipped(player) != null || _sheetWeapon.Parent != player.Backpack)
            {
                throw new InvalidOperationException("Dragging from the character sheet did not stow the weapon");
            }
            _sheetWeapon.Location = new Point3D(60, 67, 0);
        });
        Register("equipped", player =>
        {
            if (CombatTiming.Equipped(player) != _sheetWeapon)
            {
                throw new InvalidOperationException("Dragging onto the character sheet did not equip the same weapon");
            }
        });
        Register("talents", player =>
        {
            if (player.FindGump<TalentGump>() == null)
            {
                throw new InvalidOperationException("Talent notification button did not open the talents window");
            }
        });
        Register("matchup", player =>
        {
            var monster = new Skeleton { Name = "Crypt skeleton", NoKillAwards = true };
            try
            {
                monster.MoveToWorld(new Point3D(player.X + 2, player.Y, player.Z), player.Map);
                monster.AIObject?.AITimer.Stop();
                ClassStats.CheckHit(player, monster, 0, roll: () => 0.99);
                double Odds(Mobile attacker, Mobile defender) => ClassStats.HitChance(ClassStats.AttackRatingAgainst(attacker, defender),
                    NamedCombat.DefenseFor(attacker, defender, ClassStats.Defense(defender)),
                    ClassStats.Level(attacker), ClassStats.Level(defender)) * 100;
                var matchup = ClassStats.LastMatchup(player);
                if (matchup != ("Crypt skeleton", (int)Math.Round(Odds(player, monster)), (int)Math.Round(Odds(monster, player)))
                    || NamedCombat.ResistanceCap(player, ResistanceType.Fire) != 75)
                {
                    throw new InvalidOperationException($"Unexpected combat odds or resistance cap: {matchup}");
                }
                monster.Delete();
                if (ClassStats.LastMatchup(player) != matchup)
                {
                    throw new InvalidOperationException("The last matchup disappeared when the monster did");
                }
            }
            finally { if (!monster.Deleted) monster.Delete(); }
        });
        Register("batch", player =>
        {
            var profiles = ClassCreation.Profiles;
            if (!profiles.AdvanceLevel(player, 3))
                throw new InvalidOperationException("Could not set up two draft points");
            var raise = SkillMechanics.For(CombatAbility.RaiseSkeleton);
            var mastery = SkillMechanics.For(CombatAbility.SkeletonMastery);
            var draft = new Dictionary<string, int> { [raise.TalentId] = 1 };
            var nextRaise = new Dictionary<string, int> { [raise.TalentId] = 2 };
            var nextMastery = new Dictionary<string, int> { [raise.TalentId] = 1, [mastery.TalentId] = 1 };
            string raiseComparison = TalentDetails.Preview(player, raise, draft, nextRaise);
            string masteryComparison = TalentDetails.Preview(player, mastery, draft, nextMastery);
            string withoutNext = TalentDetails.Preview(player, mastery, nextMastery, null);
            if (!raiseComparison.Contains("Skeletons\t1\t2", StringComparison.Ordinal)
                || !masteryComparison.Contains("Skeletons\t1\t1", StringComparison.Ordinal)
                || !masteryComparison.Contains("Life each\t21\t29", StringComparison.Ordinal)
                || !masteryComparison.Contains("Damage\t1–2\t3–4", StringComparison.Ordinal)
                || withoutNext.Contains("Skeletons\t1\t", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected rank comparison. {raiseComparison}\n{masteryComparison}");
            }
            var mixed = new Dictionary<string, int> { ["necromancer.teeth"] = 1, ["necromancer.skeletonmastery"] = 1 };
            if (profiles.TryTrainBatch(player.Serial, mixed, out _) || profiles.GetRank(player.Serial, "necromancer.teeth") != 0
                || profiles.AvailablePoints(player.Serial) != 2)
            {
                throw new InvalidOperationException("An invalid draft partially allocated");
            }
        });
        Register("catalog", _ =>
        {
            foreach (var characterClass in Enum.GetValues<DiabloClass>())
            {
                var player = new PlayerMobile { Body = 0x190, Name = "TalentPreviewFixture", Player = true };
                try
                {
                    ClassCreation.Profiles.Assign(player, characterClass);
                    ClassCreation.Profiles.AdvanceLevel(player, 99);
                    foreach (var tree in TalentCatalog.ForClass(characterClass).Trees)
                    foreach (var talent in tree.Skills)
                    {
                        if (string.IsNullOrWhiteSpace(TalentCatalog.Description(talent.Id)))
                            throw new InvalidOperationException($"Missing description for {talent.Id}");
                        var skill = SkillMechanics.All.First(entry => entry.TalentId == talent.Id);
                        var draft = new Dictionary<string, int> { [talent.Id] = 1 };
                        var next = new Dictionary<string, int> { [talent.Id] = TalentCatalog.MaxRank };
                        string preview = TalentDetails.Preview(player, skill, draft, next);
                        if (skill.Ability == CombatAbility.SummonTainted
                            && (!preview.Contains("Fireball dmg\t5–7\t", StringComparison.Ordinal)
                                || !preview.Contains("Life\t", StringComparison.Ordinal)
                                || !preview.Contains("Defense\t", StringComparison.Ordinal)
                                || !preview.Contains("Fire resist aura\t", StringComparison.Ordinal)))
                        {
                            throw new InvalidOperationException($"Missing Succubus rank comparison. {preview}");
                        }
                    }
                }
                finally { player.Delete(); }
            }
        });
        Register("draft", player =>
        {
            var profiles = ClassCreation.Profiles;
            if (profiles.AvailablePoints(player.Serial) != 2 || profiles.GetRank(player.Serial, "necromancer.raiseskeleton") != 0
                || profiles.GetRank(player.Serial, "necromancer.skeletonmastery") != 0)
            {
                throw new InvalidOperationException("Preview changed saved talent ranks");
            }
        });
        Register("spent", player =>
        {
            var snapshot = ClassStats.Snapshot(player);
            if (snapshot[(int)StatField.TalentPoints] != 0
                || ClassCreation.Profiles.GetRank(player.Serial, "necromancer.raiseskeleton") != 1
                || ClassCreation.Profiles.GetRank(player.Serial, "necromancer.skeletonmastery") != 1)
            {
                throw new InvalidOperationException("Apply did not save the two drafted talents and their prerequisite together");
            }
        });
        Register("four", player =>
        {
            if (ClassCreation.Profiles.Stats(player.Serial) != new StatAllocation(1, 1, 1, 1)
                || ClassCreation.Profiles.AvailableStatPoints(player.Serial) != 1)
            {
                throw new InvalidOperationException("The four native status rows did not each spend exactly one point");
            }
        });
        Register("final", player =>
        {
            if (ClassCreation.Profiles.Stats(player.Serial) != new StatAllocation(1, 1, 1, 2)
                || ClassCreation.Profiles.AvailableStatPoints(player.Serial) != 0
                || player.Str != 16 || player.Dex != 26 || player.Int != 26 || ClassStats.Vitality(player) != 17
                || player.HitsMax != 50 || player.ManaMax != 29 || player.StamMax != 82)
            {
                throw new InvalidOperationException("Native status spending did not apply real class stats or overspent its budget");
            }
        });
    }
    private static void Register(string suffix, Action<PlayerMobile> check)
    {
        var name = "stat-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Status UI fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                check((PlayerMobile)Accounts.GetAccount("combatui")[0]);
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", name);
            }
        }));
    }
}
