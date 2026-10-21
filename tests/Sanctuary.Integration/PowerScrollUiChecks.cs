using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PowerScrollUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(PowerScrollUiChecks));
    private static PlayerMobile _owner;
    private static PowerScroll _scroll, _duplicate, _skip;
    private sealed class Roll : Random
    { public override int Next(int max) => 0; public override int Next(int min, int max) => min; public override double NextDouble() => 0; }
    private static void Require(bool condition, string why) => GearDropChecks.Require(condition, why);
    public static void Configure()
    {
        Command("setup", () =>
        {
            _owner = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_owner); _owner.Target = null;
            _owner.Frozen = _owner.Paralyzed = false; _owner.Blessed = true; ClassCreation.Profiles.AdvanceLevel(_owner, 99);
            foreach (var item in _owner.Backpack.Items.ToArray()) { item.Delete(); }
            ClassProfessions.Profiles.Select(_owner, SkillName.Cooking);
            _owner.Skills.Alchemy.Cap = 100; _owner.Skills.Alchemy.Base = 100; _owner.Skills.Alchemy.SetLockNoRelay(SkillLock.Locked);
            _owner.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            var boss = new EncounterCreature(); boss.Assign(EncounterProfiles.Profile("crypt-sentinel-90"), 96, MonsterDifficulty.Hell,
                EncounterGrade.Unique, ChampionKind.Champion, [], EliteAura.None, null, new Random(9));
            boss.Frozen = true; boss.LastKiller = _owner; boss.MoveToWorld(new(_owner.X + 1, _owner.Y, _owner.Z), _owner.Map);
            var previous = ClassLootHooks.CreatureDied;
            try { ClassLootHooks.CreatureDied = (creature, container) => RewardLife.Process(creature, container, new Roll()); boss.Kill(); }
            finally { ClassLootHooks.CreatureDied = previous; }
            _scroll = ((Corpse)boss.Corpse).Items.OfType<PowerScroll>().Single();
            _owner.Backpack.DropItem(_scroll); _scroll.OnDoubleClick(_owner);
            Require(_owner.FindGump<PowerScrollGump>() != null && _scroll.Value == 105 && _scroll.Skill == SkillName.Alchemy, "Native dropped scroll did not open cap confirmation");
        });
        Command("cancelled", () =>
        {
            Require(!_scroll.Deleted && _owner.Skills.Alchemy.Cap == 100 && _owner.FindGump<PowerScrollGump>() == null, "Cancel consumed scroll or raised cap");
            _scroll.Use(_owner); Require(_owner.FindGump<PowerScrollGump>() != null && _owner.Skills.Alchemy.Cap == 100, "Native Use bypassed preview");
        });
        Command("raised", () =>
        {
            Require(_scroll.Deleted && _owner.Skills.Alchemy.Cap == 105 && _owner.Skills.Alchemy.Base == 100 && _owner.Skills.Alchemy.Lock == SkillLock.Locked
                && ClassProfessions.Profiles.Find(_owner).Craft == SkillName.Cooking, "Actual inactive cap use changed skill/lock/craft or failed");
            _duplicate = new PowerScroll(SkillName.Alchemy, 105); _skip = new PowerScroll(SkillName.Alchemy, 120);
            _owner.Backpack.DropItem(_duplicate); _owner.Backpack.DropItem(_skip); _duplicate.OnDoubleClick(_owner); _skip.OnDoubleClick(_owner);
            Require(_owner.FindGump<PowerScrollGump>() == null && !_duplicate.Deleted && !_skip.Deleted, "Duplicate or skipped step opened confirmation/consumed");
            SpecialScroll.SpecialScrollGump.DisplayTo(_owner, _skip);
        });
        Command("guarded", () =>
        {
            Require(!_skip.Deleted && _owner.Skills.Alchemy.Cap == 105, "Native special-scroll confirmation bypassed ordered cap rules");
            _scroll = new PowerScroll(SkillName.Alchemy, 110); _owner.Backpack.DropItem(_scroll); _scroll.OnDoubleClick(_owner);
            Require(_owner.FindGump<PowerScrollGump>() != null, "Next cap did not open confirmation");
        });
        Command("final", () => Require(_scroll.Deleted && !_duplicate.Deleted && !_skip.Deleted && _owner.Skills.Alchemy.Cap == 110
            && _owner.Skills.Alchemy.Base == 100 && _owner.Skills.Alchemy.Lock == SkillLock.Locked
            && ClassProfessions.Profiles.Find(_owner).Craft == SkillName.Cooking, "Final cap, scroll identities or inactive skill changed"));
    }
    private static void Command(string suffix, Action action)
    {
        string name = "power-scroll-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual boss scroll cap confirmation and native bypass checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { cap = _owner.Skills.Alchemy.Cap, trained = _owner.Skills.Alchemy.Base, scroll = _scroll.Serial.Value, consumed = _scroll.Deleted }));
                _log.Information("{Command}: passed", name);
            }
            catch (Exception e) { _log.Error(e, "{Command}: failed", name); }
        }));
    }
}
