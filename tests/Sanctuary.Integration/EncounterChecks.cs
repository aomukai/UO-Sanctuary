using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Commands;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Targeting;

namespace Sanctuary.Integration;

public static class EncounterChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(EncounterChecks));
    private static readonly Point3D _point = new(5680, 1100, 0);
    private static string Filepath => Path.Join(Core.BaseDirectory, "encounter-checks.json");
    private sealed record Saved(uint Serial, string Profile, int BaseLevel, int Level, int Life, int Maximum, int Rating,
        int Defense, int Experience, MonsterDifficulty Difficulty, EncounterGrade Grade, ChampionKind Champion, string Modifiers, EliteAura Aura, uint Leader);
    private sealed record Manifest(Saved[] Creatures, uint Corpse, uint Mould, uint Player, DateTime CurseUntil);
    public static void Configure()
    {
        Register("encounter-check", Check); Register("encounter-reload-check", Reload);
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Dungeon encounters, full elite effects, rewards and persistence", _ => Core.LoopContext.Post(() =>
    {
        try { action(); _logger.Information("{Command}: passed", name); }
        catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
    }));
    private static void Require(bool value, string reason) => GearDropChecks.Require(value, reason);
    private static PlayerMobile Player(Point3D? at = null)
    {
        var player = GearDropChecks.Player(); ClassCreation.Profiles.AdvanceLevel(player, 99);
        player.MoveToWorld(at ?? new Point3D(_point.X + 2, _point.Y, 0), Map.Trammel);
        player.Hits = player.HitsMax; return player;
    }
    private static EncounterCreature Create(string id = "crypt-sentinel-90", EncounterGrade grade = EncounterGrade.Unique,
        EliteModifier[] mods = null, EliteAura aura = EliteAura.None, ChampionKind champion = ChampionKind.Champion,
        MonsterDifficulty difficulty = MonsterDifficulty.Hell, Point3D? at = null)
    {
        var profile = EncounterProfiles.Profile(id); var creature = new EncounterCreature();
        creature.Assign(profile, profile.MinimumLevel, difficulty, grade, champion, mods ?? [], aura, null, new Random(173));
        creature.Home = at ?? _point; creature.HomeMap = Map.Trammel;
        creature.MoveToWorld(creature.Home, Map.Trammel); creature.AIObject.AITimer.Stop(); creature.CantWalk = true;
        EliteAuras.Observe(creature); return creature;
    }
    private static Saved Snapshot(EncounterCreature c) => new(c.Serial.Value, c.ProfileId, c.BaseLevel, CreatureProfiles.Level(c),
        c.Hits, c.HitsMax, CreatureProfiles.AttackRating(c), CreatureProfiles.Defense(c), CreatureProfiles.Experience(c), c.Difficulty,
        c.Grade, c.Champion, string.Join(',', c.Modifiers), c.Aura, c.GroupLeader);
    private static void Check()
    {
        Profiles(); Immunities(); Modifiers(); Auras(); Projectiles(); Death(); Navigation(); RewardsAndSave();
    }
    private static void Profiles()
    {
        foreach (var profile in EncounterProfiles.All)
        {
            foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
            {
                foreach (var grade in Enum.GetValues<EncounterGrade>())
                {
                    var c = Create(profile.Id, grade, difficulty: difficulty);
                    var row = c.EncounterProfile;
                    Require(CreatureProfiles.Level(c) == EncounterProfiles.Level(profile.MinimumLevel, grade)
                        && c.HitsMax == row.Life && c.DamageMin == row.Attack1.MinDamage && c.ActiveMoveSpeed == row.Movement.WalkMs / 1000.0
                        && CreatureProfiles.Experience(c) > 0 && c.BasePoisonResistance == row.Resistances.Poison
                        && c.RangeFight == (profile.Archetype is MonsterArchetype.Caster or MonsterArchetype.Ranged ? 10 : 1), "Dungeon identity/combat projection differs");
                    var before = Snapshot(c); _ = CreatureProfiles.Snapshot(c); c.GetProperties(new ObjectPropertyList(c));
                    Require(before == Snapshot(c), "Examining an encounter rerolled its identity"); c.Delete();
                }
            }
            for (int level = profile.MinimumLevel; level < profile.MaximumLevel; level++)
            {
                var a = MonsterBalance.For(profile.Archetype, level); var b = MonsterBalance.For(profile.Archetype, level + 1);
                Require(a.Life.Max < b.Life.Min && a.AttackRating.Max <= b.AttackRating.Min && a.Defense.Max <= b.Defense.Min
                    && a.MaxDamage <= b.MaxDamage, "Dungeon level variance crossed its next level");
            }
        }
        foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
        {
            var pack = EncounterProfiles.Spawn("crypt-sentinel-60", difficulty, EncounterGrade.Unique, _point, Map.Trammel, new Random(39));
            Require(pack.Count == 6 && pack[0].Grade == EncounterGrade.Unique && pack.Skip(1).All(c => c.Grade == EncounterGrade.Minion
                && c.GroupLeader == pack[0].Serial.Value && !c.Controlled && !c.Summoned && EncounterProfiles.MouldGrade(c.Grade) == MouldGrade.Ordinary)
                && pack[0].Modifiers.Length == (int)difficulty + 1 && pack.Select(c => c.Location).Distinct().Count() == 6,
                "Unique pack membership, modifiers or ordinary minion mould odds differ");
            foreach (var c in pack) { c.Delete(); }
            pack = EncounterProfiles.Spawn("ossuary-archer-70", difficulty, EncounterGrade.Champion, _point, Map.Trammel, new Random(9));
            Require(pack.Count is >= 2 and <= 4 && pack.Take(pack.Count - 1).All(c => c.Champion == ChampionKind.Champion), "Champion cohort distribution differs");
            foreach (var c in pack) { c.Delete(); }
        }
        foreach (var named in EncounterProfiles.Named)
        {
            var pack = EncounterProfiles.Spawn(named.Id, MonsterDifficulty.Hell, EncounterGrade.SuperUnique, _point, Map.Trammel);
            Require(pack.Count == 1 && pack[0].Name == named.Name && CreatureProfiles.Level(pack[0]) == named.BaseLevel + 3
                && pack[0].Modifiers.SequenceEqual(named.Modifiers), "Named encounter did not keep its authored identity"); pack[0].Delete();
        }
        var blockers = new List<Item>();
        for (int x = -6; x <= 6; x++) { for (int y = -6; y <= 6; y++)
        { var wall = new Static(0x80); wall.MoveToWorld(new Point3D(_point.X + x, _point.Y + y, 0), Map.Trammel); blockers.Add(wall); } }
        try
        {
            bool rejected = false; try { EncounterProfiles.Spawn("crypt-sentinel-60", MonsterDifficulty.Hell, EncounterGrade.Unique, _point, Map.Trammel); }
            catch (InvalidOperationException) { rejected = true; }
            bool remaining = false; foreach (var c in Map.Trammel.GetMobilesInRange<EncounterCreature>(_point, 6)) { remaining = true; }
            Require(rejected && !remaining, "Failed pack spawn left a partial encounter");
        }
        finally { foreach (var wall in blockers) { wall.Delete(); } }
    }
    private static void Immunities()
    {
        var all = Enum.GetValues<EliteModifier>();
        foreach (var a in all) { foreach (var b in all) { foreach (var c in all)
        {
            int[] result = EncounterProfiles.Resistances(new(60, 0, 70, 70, 70, 100), null, [a, b, c]);
            Require(result.Count(r => r >= 100) <= 2 && result[5] == 100, "Elite resistance bonus created a third immunity or weakened an existing one");
            result = EncounterProfiles.Resistances(new(100, 0, 100, 80, 80, 0), null, [a, b, c]);
            Require(result.Count(r => r >= 100) == 2 && result[0] == 100 && result[2] == 100, "Two base immunities were increased or a third was created");
        } } }
        var ghost = Create(grade: EncounterGrade.Champion, champion: ChampionKind.Ghostly);
        Require(ghost.PhysicalResistance == 80, "Ghostly did not set physical resistance to 80"); ghost.Delete();
        foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
        {
            var ordinary = Create(grade: EncounterGrade.Ordinary, difficulty: difficulty);
            var champion = Create(grade: EncounterGrade.Champion, difficulty: difficulty);
            var berserker = Create(grade: EncounterGrade.Champion, champion: ChampionKind.Berserker, difficulty: difficulty);
            var possessed = Create(grade: EncounterGrade.Champion, champion: ChampionKind.Possessed, difficulty: difficulty);
            Require(berserker.HitsMax == berserker.RolledLife * new[] { 750, 625, 500 }[(int)difficulty] / 1000
                && possessed.HitsMax == possessed.RolledLife * new[] { 6, 5, 4 }[(int)difficulty]
                && champion.HitsMax > berserker.HitsMax && berserker.DamageMin > champion.DamageMin,
                "Champion variant life/offense differs from source grade formulas");
            EliteAuras.Amplify(possessed, TimeSpan.FromMinutes(1)); Require(!EliteAuras.Curses.Active(possessed.Serial, TimedEffects.Now), "Possessed accepted Amplify Damage");
            ordinary.Delete(); champion.Delete(); berserker.Delete(); possessed.Delete();
        }
    }
    private static void Modifiers()
    {
        var player = Player(); var pet = new Skeleton { Controlled = true, ControlMaster = player, CantWalk = true };
        pet.SetHits(20000); pet.SetMana(10000); pet.MoveToWorld(player.Location, player.Map); pet.AIObject.AITimer.Stop();
        try
        {
            foreach (var modifier in Enum.GetValues<EliteModifier>())
            {
                var c = Create("cinder-hierophant-90", mods: [modifier], aura: modifier == EliteModifier.AuraEnchanted ? EliteAura.Might : EliteAura.None);
                int life = pet.Hits; int mana = pet.Mana;
                EliteCombat.Hit(c, pet, 100, ResistanceType.Physical, random: new Random(1));
                Require(pet.Hits < life, $"Elite hit did no damage: {modifier}");
                if (modifier == EliteModifier.ManaBurn) { Require(pet.Mana < mana, "Mana Burn did not drain actual mana"); }
                if (modifier == EliteModifier.ColdEnchanted) { Require(GearEffects.ChillPercent(pet) > 0, "Cold enchant did not chill"); }
                if (modifier == EliteModifier.Cursed)
                {
                    for (int i = 0; i < 20 && !EliteAuras.Curses.Active(pet.Serial, TimedEffects.Now); i++)
                    { EliteCombat.Hit(c, pet, 10, ResistanceType.Physical, random: new Random(i)); }
                    Require(EliteAuras.Curses.Active(pet.Serial, TimedEffects.Now) && pet.PhysicalResistance < 0, "Cursed did not lower actual resistance");
                    EliteAuras.Curses.Clear(pet.Serial); EliteAuras.Refresh(pet);
                }
                if (modifier == EliteModifier.LightningEnchanted)
                {
                    int count = EliteBolts.ActiveCount; CreatureHitRecovery.Apply(c, player, 1, true);
                    Require(EliteBolts.ActiveCount == count + 8, "Lightning enchant did not release exactly eight real bolts");
                }
                c.Delete(); TimedEffects.Clear(pet); pet.Hits = pet.HitsMax;
            }
            var coldMinion = Create("ossuary-archer-90", EncounterGrade.Minion, [EliteModifier.ColdEnchanted], difficulty: MonsterDifficulty.Normal);
            EliteCombat.Hit(coldMinion, pet, 20, ResistanceType.Physical, ranged: true);
            Require(GearEffects.ChillPercent(pet) > 0, "Normal ranged cold minion did not inherit cold length");
            TimedEffects.Clear(pet); coldMinion.Delete();
            var teleporter = Create("cinder-hierophant-90", mods: [EliteModifier.Teleport]); teleporter.Combatant = pet;
            teleporter.Hits = teleporter.HitsMax / 5; int before = teleporter.Hits; var origin = teleporter.Location;
            Require(EliteCombat.Teleport(teleporter) && teleporter.Location != origin && teleporter.Location != pet.Location
                && teleporter.Hits > before && teleporter.Map.CanSpawnMobile(origin), "Teleport did not escape to clear ground and heal low life");
            teleporter.Delete();
        }
        finally { pet.Delete(); player.Delete(); }
    }
    private static void Auras()
    {
        var player = Player(); var ally = Create(grade: EncounterGrade.Minion, at: new Point3D(_point.X, _point.Y + 1, 0));
        try
        {
            foreach (var aura in Enum.GetValues<EliteAura>())
            {
                if (aura == EliteAura.None) { continue; }
                var first = Create(mods: [EliteModifier.AuraEnchanted], aura: aura);
                var second = Create(mods: [EliteModifier.AuraEnchanted], aura: aura, at: new Point3D(_point.X, _point.Y - 1, 0));
                int level = EliteAuras.Level(aura is EliteAura.Conviction or EliteAura.HolyFreeze ? player : ally, aura,
                    aura is EliteAura.Conviction or EliteAura.HolyFreeze);
                Require(level == EliteAuras.SkillLevel(first), $"Aura not exposed to intended target: {aura}");
                if (aura == EliteAura.BlessedAim) { Require(ClassStats.AttackRating(ally) > CreatureProfiles.AttackRating(ally), "Blessed Aim did not affect hit checks"); }
                if (aura == EliteAura.Fanaticism) { Require(EliteAuras.Speed(ally) > 0 && ally.NormalStats.Attack1.DurationMs < ally.Basis.AttackMs, "Fanaticism did not accelerate attacks"); }
                if (aura == EliteAura.Might) { Require(EliteAuras.DamageBonus(ally) > 0, "Might did not affect attacks"); }
                if (aura == EliteAura.Conviction)
                {
                    int fire = player.FireResistance; EliteAuras.Tick();
                    Require(player.FireResistance < fire && EliteAuras.Defense(player, 1000) < 1000, $"Conviction did not reduce real resistance/defense: fire={fire}->{player.FireResistance}, defense={EliteAuras.Defense(player,1000)}, enemy={EliteCombat.Enemy(first,player,18)}, auraLevel={EliteAuras.Level(player,aura,true)}, hp={player.Hits}, blocked={AlliedOverlap.Blocked(first)}");
                }
                if (aura == EliteAura.HolyFreeze) { Require(GearEffects.ChillPercent(player) > 0, "Holy Freeze did not slow its enemy"); }
                if (aura is EliteAura.HolyFire or EliteAura.HolyFreeze or EliteAura.HolyShock)
                {
                    int before = player.Hits;
                    EliteAuras.Tick(Core.TickCount + (int)aura * 10000);
                    int max = EliteAuras.ElementDamage(aura, EliteAuras.SkillLevel(first), new MaximumRandom());
                    Require(player.Hits < before && before - player.Hits <= max, $"Aura pulse missing or stacked from duplicate emitters: {aura}, loss={before-player.Hits}, max={max}");
                }
                first.Delete();
                Require(EliteAuras.Level(aura is EliteAura.Conviction or EliteAura.HolyFreeze ? player : ally, aura,
                    aura is EliteAura.Conviction or EliteAura.HolyFreeze) == level, "Deleting one emitter removed another's aura");
                second.Delete(); EliteAuras.Tick();
                Require(EliteAuras.Level(ally, aura, false) == 0 && EliteAuras.Level(player, aura, true) == 0, "Deleted aura persisted");
                player.Hits = player.HitsMax;
            }
        }
        finally { ally.Delete(); player.Delete(); }
    }
    private static void Projectiles()
    {
        var player = Player(new Point3D(_point.X + 4, _point.Y, 0));
        var caster = Create("cinder-hierophant-90", mods: [EliteModifier.Multishot]);
        try
        {
            int before = player.Hits;
            EliteBolts.Fan(caster, player, 3, true); Require(EliteBolts.ActiveCount == 3, "Multishot did not launch three missiles");
            for (int i = 0; i < 20; i++) { EliteBolts.Tick(); }
            Require(player.Hits < before && EliteBolts.ActiveCount == 0, "Real missile did not collide or expire");
            player.Hits = player.HitsMax; before = player.Hits;
            var wall = new Static(0x80); wall.MoveToWorld(new Point3D(_point.X + 2, _point.Y, 0), player.Map);
            try
            {
                EliteBolts.Fan(caster, player, 3, true); for (int i = 0; i < 20; i++) { EliteBolts.Tick(); }
                Require(player.Hits == before, "Multishot hit through a wall");
            }
            finally { wall.Delete(); }
            EliteBolts.Fan(caster, player, 1, true); player.MoveToWorld(new Point3D(player.X, player.Y + 3, 0), player.Map);
            for (int i = 0; i < 20; i++) { EliteBolts.Tick(); }
            Require(player.Hits == before, "Missile homed onto a dodging player");
            EliteBolts.Fan(caster, player, 3, true); caster.Delete(); Require(EliteBolts.ActiveCount == 0, "Deleting emitter retained projectile references");
        }
        finally { caster.Delete(); player.Delete(); }
    }
    private static void Death()
    {
        var player = Player();
        var fire = Create(mods: [EliteModifier.FireEnchanted]); var cold = Create(mods: [EliteModifier.ColdEnchanted]);
        try
        {
            int before = player.Hits; fire.DeathEffects(); Require(player.Hits < before, "Fire death explosion did not damage nearby enemy");
            before = player.Hits; fire.DeathEffects(); Require(player.Hits == before, "Fire death effect repeated");
            player.Hits = player.HitsMax; player.MoveToWorld(new Point3D(_point.X + 8, _point.Y, 0), player.Map);
            var pet = new Skeleton { Controlled = true, ControlMaster = player, CantWalk = true };
            pet.SetHits(20000); pet.MoveToWorld(new Point3D(_point.X + 2, _point.Y, 0), player.Map); pet.AIObject.AITimer.Stop();
            try
            {
                before = pet.Hits; cold.Kill();
                Require(pet.Hits < before && GearEffects.ChillPercent(pet) > 0, "Cold death nova did not damage/chill enemy");
                before = pet.Hits; cold.DeathEffects(); Require(pet.Hits == before, "Cold death effect repeated");
            }
            finally { pet.Delete(); cold.Corpse?.Delete(); }
            EliteAuras.Amplify(player, TimeSpan.FromMinutes(1)); player.Kill(); player.Resurrect(); EliteAuras.Tick();
            Require(!EliteAuras.Curses.Active(player.Serial, TimedEffects.Now), "Death did not clear the curse");
        }
        finally { fire.Delete(); cold.Delete(); player.Corpse?.Delete(); player.Delete(); }
    }
    private static void Navigation()
    {
        var player = Player(new Point3D(_point.X + 5, _point.Y, 0));
        var creature = Create(); creature.CantWalk = false; creature.Combatant = player;
        var walls = new List<Item>();
        for (int y = -2; y <= 2; y++) { var wall = new Static(0x80); wall.MoveToWorld(new Point3D(_point.X + 2, _point.Y + y, 0), player.Map); walls.Add(wall); }
        var pet = AlliedOverlapChecks.Pet(player, new Point3D(player.X, player.Y + 1, 0));
        try
        {
            Require(creature.IsEnemy(player) && creature.IsEnemy(pet), "Elite ignored player-controlled pets");
            for (int i = 0; i < 50 && !creature.InLOS(player); i++)
            { creature.AIObject.NextMove = Core.TickCount; creature.AIObject.DoActionCombat(); }
            Require(creature.InLOS(player) && creature.Location != creature.Home, "Elite pursuit did not route around a corner");
            creature.Combatant = pet;
            for (int i = 0; i < 20 && !creature.InRange(pet, 1); i++)
            { creature.AIObject.NextMove = Core.TickCount; creature.AIObject.DoActionCombat(); }
            Require(creature.InRange(pet, 1), "Elite pursuit failed to reach a pet");
            foreach (var wall in walls) { wall.Internalize(); }
            creature.MoveToWorld(_point, player.Map); creature.Combatant = player; creature.NextCombatTime = Core.TickCount + 60000;
            player.MoveToWorld(new Point3D(_point.X + 2, _point.Y, 0), player.Map);
            var door = new MetalDoor(DoorFacing.NorthCW); door.MoveToWorld(new Point3D(_point.X + 1, _point.Y, 0), player.Map);
            try
            {
                Require(!creature.InLOS(player) && creature.CanOpenDoors, "Closed door fixture or elite door policy differs");
                for (int i = 0; i < 8 && !door.Open; i++) { creature.AIObject.NextMove = Core.TickCount; creature.AIObject.DoActionCombat(); }
                Require(door.Open && creature.InLOS(player), "Elite AI did not open an unlocked door");
            }
            finally { door.Delete(); }

        }
        finally { foreach (var wall in walls) { wall.Delete(); } creature.Delete(); pet.Delete(); player.Delete(); }
    }
    private static void RewardsAndSave()
    {
        var savedPoint = CombatPractice.FindFloor(Map.Trammel, 5750, 1300);
        var player = Player(new Point3D(savedPoint.X + 10, savedPoint.Y, 0));
        var saved = new List<Saved>(); var pack = EncounterProfiles.Spawn("ossuary-archer-90", MonsterDifficulty.Hell, EncounterGrade.Unique, savedPoint, Map.Trammel, new Random(69));
        foreach (var c in pack) { c.AIObject.AITimer.Stop(); c.Frozen = true; c.Hits -= 37; saved.Add(Snapshot(c)); }
        foreach (var kind in Enum.GetValues<ChampionKind>())
        { var c = Create(grade: EncounterGrade.Champion, champion: kind, at: new Point3D(savedPoint.X + 15 + (int)kind, savedPoint.Y, 0)); c.Frozen = true; c.Hits -= 13; saved.Add(Snapshot(c)); }
        var boss = Create(mods: [EliteModifier.ColdEnchanted, EliteModifier.LightningEnchanted, EliteModifier.FireEnchanted],
            at: new Point3D(savedPoint.X + 12, savedPoint.Y, 0)); boss.Frozen = true; boss.Hits -= 21; saved.Add(Snapshot(boss));
        var victim = Create(at: new Point3D(savedPoint.X + 11, savedPoint.Y, 0));
        var corpse = GearDropChecks.Body(victim, player, victim.Location);
        RewardLife.Process(victim, corpse, new Random(42)); int count = corpse.Items.Count;
        Require(corpse.SanctuaryNoRewards && count <= 6, "Elite rewards escaped shared claim or six-item cap");
        RewardLife.Process(victim, corpse, new Random(42)); Require(corpse.Items.Count == count, "Elite reward callback paid twice");
        var drops = EncounterTreasure.Roll(victim, new Random(45)); Require(drops.Count == 5
            && drops.Count(d => d.Factors[3] == 1024) == 1, "Unique tree lost its gear-plus-four-potion structure or quality factors");
        var super = Create(grade: EncounterGrade.SuperUnique); Require(EncounterTreasure.Roll(super, new Random(45)).Count == 6, "Named treasure did not reserve two gear and four potion outcomes"); super.Delete();
        var champion = Create(grade: EncounterGrade.Champion); Require(EncounterTreasure.Roll(champion, new Random(45)).Count == 3, "Champion tree lost its one reward and two potion draws"); champion.Delete();
        var basis = GearDrops.Base("hax"); Require(GearDrops.Denominator(basis, 99, 0, DropQuality.Magic, 1024) == 0
            && GearDrops.Denominator(basis, 99, 100, DropQuality.Rare, 800) < GearDrops.Denominator(basis, 99, 0, DropQuality.Rare, 800), "Elite quality factor/MF ordering differs");
        foreach (var grade in Enum.GetValues<MouldGrade>())
        {
            int successes = 0;
            for (int i = 0; i < 10000; i++)
            { var mould = MouldRewards.Roll(99, MonsterDifficulty.Hell, new TicketRandom(i), grade); if (mould != null) { successes++; Require(mould.Grade == grade && mould.Tier == 8, "Mould grade/tier identity lost"); mould.Delete(); } }
            Require(successes == (grade switch { MouldGrade.Ordinary => 10, MouldGrade.Champion => 100, MouldGrade.Unique => 500, _ => 1000 }), "Mould chance did not use agreed exact tickets");
        }
        var keptMould = MouldRewards.Roll(99, MonsterDifficulty.Hell, new TicketRandom(0), MouldGrade.SuperUnique); corpse.DropItem(keptMould);
        EliteAuras.Amplify(player, TimeSpan.FromMinutes(30));
        File.WriteAllText(Filepath, JsonSerializer.Serialize(new Manifest(saved.ToArray(), corpse.Serial.Value, keptMould.Serial.Value, player.Serial.Value, EliteAuras.Curses.Expires(player.Serial))));
        victim.Delete();
        player.AccessLevel = AccessLevel.GameMaster;
        CommandSystem.Handle(player, "[SpawnEncounter bog-revenant-80 Hell Ordinary"); Require(player.Target != null, "Staff encounter command did not target");
        var target = CombatPractice.FindFloor(player.Map, savedPoint.X + 30, savedPoint.Y); player.MoveToWorld(new Point3D(target.X - 2, target.Y, target.Z), player.Map);
        player.Target.Invoke(player, new LandTarget(target, player.Map));
        EncounterCreature spawned = null;
        foreach (var c in player.Map.GetMobilesInRange<EncounterCreature>(target, 0)) { spawned = c; }
        Require(spawned != null && spawned.Difficulty == MonsterDifficulty.Hell, "Staff target did not create supported encounter"); spawned.Delete(); player.AccessLevel = AccessLevel.Player;
    }
    private sealed class MaximumRandom : Random
    { public override int Next(int min, int max) => max - 1; }
    private sealed class TicketRandom(int ticket) : Random
    { public override int Next(int maximum) => maximum == 10000 ? ticket : 0; }
    private static void Reload()
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Filepath));
        foreach (var row in manifest.Creatures)
        {
            var c = World.FindMobile((Serial)row.Serial) as EncounterCreature;
            Require(c != null && Snapshot(c) == row, $"Encounter identity/injury changed on reload: expected {JsonSerializer.Serialize(row)}, actual {(c == null ? "missing" : JsonSerializer.Serialize(Snapshot(c)))}");
        }
        var corpse = World.FindItem((Serial)manifest.Corpse) as Corpse; var mould = World.FindItem((Serial)manifest.Mould) as RuneMould;
        Require(corpse is { SanctuaryNoRewards: true } && mould?.Parent == corpse && mould.Grade == MouldGrade.SuperUnique, "Elite reward claim/mould changed on reload");
        var player = World.FindMobile((Serial)manifest.Player);
        Require(player != null && EliteAuras.Curses.Expires(player.Serial) == manifest.CurseUntil && manifest.CurseUntil > TimedEffects.Now
            && player.PhysicalResistance < 0, "Saved curse lost its absolute expiry or real resistance effect");
        Require(manifest.Creatures.Select(row => row.Serial).Distinct().Count() == manifest.Creatures.Length, "Reload duplicated a group member");
    }
}
