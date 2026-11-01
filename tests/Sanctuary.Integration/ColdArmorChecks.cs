// SPDX-License-Identifier: GPL-3.0-or-later
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

public static class ColdArmorChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(ColdArmorChecks));
    private static readonly CombatAbility[] Abilities = [CombatAbility.FrozenArmor, CombatAbility.ShiverArmor, CombatAbility.ChillingArmor];
    private sealed record Entry(uint Owner, uint Orb, int Mana, int Life, int Map, Point3D Point, ColdArmorState Armor);
    private sealed record Saved(Entry[] Entries, int Generation);
    private static string Manifest => Path.Join(Core.BaseDirectory, "cold-armor-fixtures.json");
    public static void Configure()
    {
        var prior = ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateMana = p => p.Name?.StartsWith("ColdArmorFixture", StringComparison.Ordinal) != true && prior?.Invoke(p) != false;
        Register("cold-armor-check", Check); Register("cold-armor-reload-check", Reload);
        foreach (var ability in Abilities) foreach (bool quick in new[] { false, true })
        {
            var a = ability; string label = Label(a, quick);
            Register(label + "-check", () => FixtureNetwork.Connected(label + "-result", n => Input(n, a, label)));
        }
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()
    {
        var prior = ClassGearHooks.CanRegenerateHits;
        ClassGearHooks.CanRegenerateHits = p => p.Name?.StartsWith("ColdArmorFixture", StringComparison.Ordinal) != true && prior?.Invoke(p) != false;
    }
    private static string Label(CombatAbility a, bool quick) => "cold-armor-" + (a == CombatAbility.FrozenArmor ? "frozen" : a == CombatAbility.ShiverArmor ? "shiver" : "chilling") + (quick ? "-quick" : "-right");
    private static void Register(string name, Action run) => ConsoleInputHandler.RegisterCommand(name,
        "Cold armor source casts and Frozen Armor incoming FP reaction", _ => Core.LoopContext.Post(() =>
        { try { run(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool ok, string why) { if (!ok) { throw new InvalidOperationException(why); } }
    private static void Train(PlayerMobile p, CombatAbility ability, int rank)
    {
        string id = SkillMechanics.For(ability).TalentId;
        while (ClassCreation.Profiles.GetRank(p.Serial, id) < rank) { Require(ClassCreation.Profiles.TryTrain(p.Serial, id, out _), "Train " + id); }
    }
    internal static PlayerMobile Owner(CombatAbility ability = CombatAbility.FrozenArmor)
    {
        var p = FireBoltChecks.Owner(CombatAbility.FireBolt, 5800); p.Name = "ColdArmorFixture";
        foreach (var a in new[] { CombatAbility.IceBolt, CombatAbility.IceBlast, CombatAbility.FrozenArmor, CombatAbility.ShiverArmor, CombatAbility.GlacialSpike, CombatAbility.ChillingArmor }) { Train(p, a, 1); }
        Require(SkillBindings.Instance.Assign(p, false, ability) && SkillBindings.Instance.AssignQuick(p, 7, ability)
            && !SkillBindings.Instance.Assign(p, true, ability), "Cold armor right/quick only");
        return p;
    }
    private static SkillValues Values(CombatAbility a, int rank, int hard = 0) => new(SkillMechanics.For(a),
        k => k == SkillMechanics.For(a).SourceKey ? rank : 255, _ => hard, 99);
    private static void Check()
    {
        Goldens(); SourceGroup(); Admission(); Reaction(); Save();
        Log.Information("Cold armor:2295 rank/hard goldens,45 complete-source group replacements,Frozen Armor live retained-rank freeze,pre-HP fractional/lethal native reactions and three original saved armor/gear/resource sets pass");
    }
    private static void Goldens()
    {
        var at = TimedEffects.Now;
        for (int rank = 1; rank <= 255; rank++) foreach (int hard in new[] { 0, 1, 20 }) foreach (var a in Abilities)
        {
            int basis = a == CombatAbility.FrozenArmor ? 30 : a == CombatAbility.ShiverArmor ? 45 : 60;
            int gain = a == CombatAbility.FrozenArmor ? 5 : a == CombatAbility.ShiverArmor ? 6 : 9;
            int mana = a == CombatAbility.FrozenArmor ? 7 : a == CombatAbility.ShiverArmor ? 11 : 17;
            var v = Values(a, rank, hard); var prepared = ColdArmors.Prepare(v, at);
            Require(prepared.Rank == rank && prepared.Defense == basis + gain * (rank - 1)
                && prepared.UntilUtc == at.AddMilliseconds((3600 + 300 * (rank - 1) + 500 * hard) * 40L)
                && v.Mana256 == mana * 256, "Independent armor defense/duration/hard synergy/mana");
            if (a == CombatAbility.FrozenArmor)
            { Require(prepared.FreezeFrames == (30 + 3 * (rank - 1)) * (100 + 10 * hard) / 100, "Independent freeze formula"); }
        }
    }
    private static void SourceGroup()
    {
        var p = Owner(); int count = 0;
        try
        {
            using var source = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "cold-armor-source-cases.json")));
            foreach (var row in source.RootElement.GetProperty("cast").EnumerateArray())
            {
                int V(int i) => row[i].GetInt32();
                if (V(0) != 0 || V(1) != 1 || V(4) != 1) { continue; }
                foreach (var a in Abilities)
                {
                    SorceressEffects.Profiles.Clear(p);
                    if (V(2) != 0) { SorceressEffects.CastArmor(p, a, Values(a, V(2))); }
                    SorceressEffects.CastArmor(p, a, Values(a, V(3)));
                    var actual = SorceressEffects.Armor(p);
                    Require(V(5) == 1 && V(9) == 1 && actual.Rank == V(15) && actual.Ability == a,
                        "Grouped same-state recast must replace weaker/equal/stronger ranks"); count++;
                }
            }
            Require(count == 45, "Source group comparison count");
            SorceressEffects.CastShield(p, new(SkillMechanics.For(CombatAbility.EnergyShield), _ => 1, _ => 0, 99));
            foreach (var a in Abilities)
            {
                SorceressEffects.CastArmor(p, a, Values(a, 1));
                Require(SorceressEffects.Armor(p).Ability == a && SorceressEffects.Shield(p) != null, "Armor replacement lost independent Energy Shield");
            }
        }
        finally { FireBoltChecks.Clean(p); }
    }
    private static void Admission()
    {
        foreach (var a in Abilities)
        {
            var p = Owner(a); int mana = NecromancerEffects.ManaBalance256(p), cost = SkillMechanics.Values(p, a).Mana256;
            try
            {
                Require(ClassCombat.ContinueCast(p, a, null, p.Location, Core.TickCount), "Armor windup");
                long release = ClassCombat.For(p).EffectAt;
                Require(SorceressEffects.Armor(p) == null && NecromancerEffects.ManaBalance256(p) == mana, "Armor paid at start");
                ClassCombat.Stop(p); Require(!ClassCombat.ResolveAction(p, release) && NecromancerEffects.ManaBalance256(p) == mana, "Cancelled armor paid");
                NecromancerEffects.SetMana256(p, cost - 1);
                Require(!SorceressCombat.Release(p, a, Serial.Zero, p.Location) && SorceressEffects.Armor(p) == null
                    && NecromancerEffects.ManaBalance256(p) == cost - 1, "Armor accepted insufficient fractional mana");
                NecromancerEffects.SetMana256(p, mana);
                Require(SorceressCombat.Release(p, a, Serial.Zero, p.Location) && NecromancerEffects.ManaBalance256(p) == mana - cost, "Once-only armor payment");
                var first = SorceressEffects.Armor(p); var point = p.Location; var map = p.Map; p.Internalize(); mana = NecromancerEffects.ManaBalance256(p);
                Require(!SorceressCombat.Release(p, a, Serial.Zero, p.Location) && SorceressEffects.Armor(p) == first
                    && NecromancerEffects.ManaBalance256(p) == mana, "Internal armor caster paid/replaced");
                p.MoveToWorld(point, map); p.Kill(); Require(!p.Alive, "Dead caster fixture"); mana = NecromancerEffects.ManaBalance256(p);
                Require(!SorceressCombat.Release(p, a, Serial.Zero, p.Location) && NecromancerEffects.ManaBalance256(p) == mana, "Dead armor caster paid");
            }
            finally { ClassCombat.Stop(p); FireBoltChecks.Clean(p); }
        }
    }
    private static void Reaction()
    {
        var p = Owner(); var enemy = FireBoltChecks.Target(p, 2);
        try
        {
            var orb = PoisonDaggerChecks.Named(p, "Eschuta's temper");
            Require(SorceressCombat.Release(p, CombatAbility.FrozenArmor, Serial.Zero, p.Location), "Geared Frozen Armor");
            var captured = SorceressEffects.Armor(p); Require(captured.Rank > 1, "Captured soft rank");
            p.Backpack.DropItem(orb); ClassGear.Refresh(p); Train(p, CombatAbility.ShiverArmor, 20);
            int frames = (30 + 3 * (captured.Rank - 1)) * 205 / 100;
            Require(SkillMechanics.Rank(p, CombatAbility.FrozenArmor) == 1 && ColdArmors.FreezeFrames(p, captured) == frames
                && SorceressEffects.Armor(p) == captured, "Reaction must retain rank but read current hard synergies");
            foreach (int physical in new[] { 0, 1, 255, 256 }) foreach (bool ranged in new[] { false, true })
            {
                TimedEffects.Clear(enemy); long tick = Core.TickCount;
                bool applied = ColdArmors.PhysicalMelee(enemy, p, physical, ranged);
                Require(applied == (physical > 0 && !ranged) && (NamedControl.FrozenRemaining(enemy) > 0) == applied, "Physical FP/ranged event qualification");
                if (applied) { Require(Math.Abs((NamedControl.State(enemy).FreezeUntil - TimedEffects.TimeAt(tick)).TotalMilliseconds - frames * 40L) < 100, "Actual retained-rank freeze duration"); }
            }
            TimedEffects.Clear(enemy); enemy.SetResistance(ResistanceType.Cold, 100);
            Require(!ColdArmors.PhysicalMelee(enemy, p, 1, false) && NamedControl.FrozenRemaining(enemy) == 0, "Cold immune attacker frozen");
            enemy.SetResistance(ResistanceType.Cold, 0); TimedEffects.Clear(enemy);
            int hp = SkillHealth.Balance256(p); SkillCombat.HitParts(enemy, p, new(.5, 0, 0, 0, 0), false, weapon: true);
            Require(hp - SkillHealth.Balance256(p) == 128 && NamedControl.FrozenRemaining(enemy) > 0, "Fractional actual weapon hit did not react before whole-HP projection");
            TimedEffects.Clear(enemy); AOS.Damage(p, enemy, 1, false, 0, 100, 0, 0, 0, weaponDamage: true);
            Require(NamedControl.FrozenRemaining(enemy) == 0, "Pure elemental native weapon triggered Frozen Armor");
            AOS.Damage(p, enemy, 1, false, 100, 0, 0, 0, 0, archer: true, weaponDamage: true);
            Require(NamedControl.FrozenRemaining(enemy) == 0, "Native ranged hit triggered Frozen Armor");
            p.Hits = 1; AOS.Damage(p, enemy, 50, false, 100, 0, 0, 0, 0, weaponDamage: true);
            Require(!p.Alive && NamedControl.FrozenRemaining(enemy) > 0, "Lethal native melee lost its pre-health armor event");
            var legacy = JsonSerializer.Deserialize<ColdArmorState>(JsonSerializer.Serialize(new { captured.Ability, captured.UntilUtc,
                captured.Defense, captured.FreezeFrames, captured.Min256, captured.Max256, captured.ColdFrames }));
            Require(legacy.Rank == 0 && ColdArmors.FreezeFrames(p, legacy) == captured.FreezeFrames, "Historical record guessed a rank/current formula");
        }
        finally { TimedEffects.Clear(enemy); FireBoltChecks.Clean(p, enemy); }
    }
    private static void Save()
    {
        var entries = new Entry[3]; int i = 0;
        foreach (var a in Abilities)
        {
            var p = Owner(a); foreach (var other in Abilities) { Train(p, other, 20); }
            var orb = PoisonDaggerChecks.Named(p, "Eschuta's temper");
            Require(SorceressCombat.Release(p, a, Serial.Zero, p.Location), "Saved actual armor cast"); SkillHealth.Damage(p, p, 9 * 256 + 13);
            entries[i++] = new(p.Serial.Value, orb.Serial.Value, NecromancerEffects.ManaBalance256(p), SkillHealth.Balance256(p),
                p.Map.MapID, p.Location, SorceressEffects.Armor(p)); p.Internalize();
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(entries, 0)));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); Require(saved.Generation < 2 && saved.Entries.Length == 3, "Saved armor generation");
        foreach (var s in saved.Entries)
        {
            var p = World.FindMobile((Serial)s.Owner) as PlayerMobile;
            Require(p != null && World.FindItem((Serial)s.Orb)?.Parent == p && SorceressEffects.Armor(p) == s.Armor
                && s.Armor.Rank > 20 && NecromancerEffects.ManaBalance256(p) == s.Mana && SkillHealth.Balance256(p) == s.Life
                && SkillBindings.Instance.Get(p).Right == s.Armor.Ability, "Original armor/actor/gear/deadline/resources/binding changed on reload");
            if (s.Armor.Ability != CombatAbility.FrozenArmor) { continue; }
            p.MoveToWorld(s.Point, Map.Maps[s.Map]); var target = FireBoltChecks.Target(p, 2);
            try { Require(ColdArmors.PhysicalMelee(target, p, 1, false) && NamedControl.FrozenRemaining(target) > 0, "Saved retained-rank freeze reaction did not resume"); }
            finally { TimedEffects.Clear(target); target.Delete(); p.Internalize(); }
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved with { Generation = saved.Generation + 1 }));
    }
    private static void Input(NetState network, CombatAbility a, string label)
    {
        var p = Owner(a); var account = FixtureNetwork.Authenticate(network, p, "cold-armor-input-");
        int mana = NecromancerEffects.ManaBalance256(p), cost = SkillMechanics.Values(p, a).Mana256; bool began = false; long deadline = Core.TickCount + 5000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount < deadline, "Armor input timeout");
                if (ClassCombat.For(p).PendingAbility == a && !began)
                { Require(SorceressEffects.Armor(p) == null && NecromancerEffects.ManaBalance256(p) == mana, "Input paid before release"); began = true; Log.Information("{Label}: windup", label); }
                if (SorceressEffects.Armor(p) == null) { Timer.StartTimer(TimeSpan.FromMilliseconds(20), Observe); return; }
                Require(began && NecromancerEffects.ManaBalance256(p) == mana - cost && SorceressEffects.Armor(p) is { Rank: 1 } armor && armor.Ability == a, "Actual right/quick armor identity/cost");
                FireBoltChecks.Clean(p); account.Delete(); Log.Information("{Label}-result: passed", label);
            }
            catch (Exception e) { FireBoltChecks.Clean(p); account.Delete(); Log.Error(e, "{Label}-result: failed", label); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30), () =>
        { Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}", label, (int)a, (int)DiabloClass.Sorceress, p.X, p.Y, p.Z); Observe(); });
    }
}
