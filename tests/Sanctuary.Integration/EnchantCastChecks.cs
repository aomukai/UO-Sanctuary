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

public static class EnchantCastChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(EnchantCastChecks));
    private sealed record Saved(uint Owner, uint Ally, uint Orb, int Mana, int Life, EnchantState Self, EnchantState Other, int Generation);
    private static string Manifest => Path.Join(Core.BaseDirectory, "enchant-cast-fixtures.json");
    public static void Configure()
    {
        var prior = ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateMana = p => p.Name?.StartsWith("EnchantCast", StringComparison.Ordinal) != true && prior?.Invoke(p) != false;
        Register("enchant-cast-check", Check);
        Register("enchant-cast-reload-check", Reload);
        foreach (string target in new[] { "self", "ally", "hostile" })
        foreach (string mode in new[] { "right", "quick" })
        {
            string kind = target, label = "enchant-" + target + "-" + mode;
            Register(label + "-check", () => FixtureNetwork.Connected(label + "-result", n => Input(n, kind, label)));
        }
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()
    {
        var prior = ClassGearHooks.CanRegenerateHits;
        ClassGearHooks.CanRegenerateHits = p => p.Name?.StartsWith("EnchantCast", StringComparison.Ordinal) != true && prior?.Invoke(p) != false;
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Enchant ordinary player input, source replacement and independent saved buffs", _ => Core.LoopContext.Post(() =>
        { try { action(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool ok, string why) { if (!ok) { throw new InvalidOperationException(why); } }
    internal static void Train(PlayerMobile p, CombatAbility ability, int rank)
    {
        string id = SkillMechanics.For(ability).TalentId;
        while (ClassCreation.Profiles.GetRank(p.Serial, id) < rank)
        { Require(ClassCreation.Profiles.TryTrain(p.Serial, id, out _), "Enchant fixture train " + id); }
    }
    internal static PlayerMobile Owner()
    {
        var p = FireBoltChecks.Owner(CombatAbility.FireBolt, 5830);
        p.Name = "EnchantCastFixture";
        Train(p, CombatAbility.Warmth, 1); Train(p, CombatAbility.Fireball, 1); Train(p, CombatAbility.Enchant, 1);
        Require(SkillBindings.Instance.Assign(p, false, CombatAbility.Enchant)
            && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Enchant)
            && !SkillBindings.Instance.Assign(p, true, CombatAbility.Enchant), "Enchant right/quick only");
        return p;
    }
    internal static PlayerMobile Ally(PlayerMobile p)
    {
        var ally = ClassSkillChecks.Player(); ally.Name = "EnchantCastAlly";
        ally.MoveToWorld(new(p.X + 2, p.Y, p.Z), p.Map); return ally;
    }
    internal static SkillValues Values(int rank, int hard = 0) => new(SkillMechanics.For(CombatAbility.Enchant),
        k => k == "Enchant" ? rank : 255, k => k == "Warmth" ? hard : 0, 99);
    private static void Clean(PlayerMobile p, params Mobile[] targets) => FireBoltChecks.Clean(p, targets);
    private static void Check()
    {
        Goldens(); SourceReplacement(); Admission(); Snapshots(); Save();
        Log.Information("Enchant casting:765 independent rank/hard goldens,60 compiled-source state comparisons,release-only mana,cancel/invalid/insufficient guards,real gear/live recipient mastery and unextended saved deadlines pass");
    }
    private static void Goldens()
    {
        for (int rank = 1; rank <= 255; rank++) foreach (int hard in new[] { 0, 1, 20 })
        {
            int Tier(int basis, int start)
            {
                int n = rank - 1;
                return basis + Math.Min(n, 7) * start + Math.Min(Math.Max(0, n - 7), 8) * (start + 4)
                    + Math.Min(Math.Max(0, n - 15), 6) * (start + 8) + Math.Min(Math.Max(0, n - 21), 6) * (start + 12)
                    + Math.Max(0, n - 27) * (start + 16);
            }
            var v = Values(rank, hard);
            Require(v.Mana256 == (24 + rank) * 256 && v.Eval("auralencalc") == 3600 + 600 * (rank - 1)
                && v.Damage256(false) == (long)Tier(16, 3) * 128 * (100 + 9 * hard) / 100
                && v.Damage256(true) == (long)Tier(20, 5) * 128 * (100 + 9 * hard) / 100,
                "Independent Enchant tier/Warmth curve " + rank + "/" + hard);
        }
    }
    private static void SourceReplacement()
    {
        var p = Owner(); var ally = Ally(p); int compared = 0;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "enchant-source-cases.json")));
            foreach (var r in doc.RootElement.EnumerateArray())
            {
                int V(int i) => r[i].GetInt32();
                // Native host adapter has no D2 skill-list identity or NPC/isatt
                // metadata. Compare only the shared ordinary Enchant domain.
                if (V(0) != 0 || V(1) > 3 || V(4) != 1 || V(5) != 3600 || V(6) != 0) { continue; }
                SorceressEffects.Profiles.Clear(ally);
                var before = new EnchantState(TimedEffects.Now.AddMinutes(10), V(3), 7, 8, 9, 101);
                if (V(3) > 0) { SorceressEffects.Profiles.Store(ally, new(Enchantment: before)); }
                var v = Values(V(2)); SorceressEffects.CastEnchant(p, ally, v);
                var result = SorceressEffects.Enchantment(ally);
                if (V(7) == 0) { Require(result == before, "Source weaker-rank refusal"); }
                else
                {
                    Require(result.Rank == V(14) && result.Caster == (V(16) == 101 ? 101U : p.Serial.Value)
                        && result.Min256 == v.Damage256(false) && result.Max256 == v.Damage256(true)
                        && result.AttackPercent == 20 + 9 * (V(2) - 1)
                        && Math.Abs((result.UntilUtc - TimedEffects.Now).TotalMilliseconds - v.Eval("auralencalc") * 40) < 100,
                        "Source equal/stronger state reuse and independent duration");
                }
                compared++;
            }
            Require(compared == 60, "Expected source comparison coverage");
        }
        finally { Clean(p, ally); }
    }
    private static void Admission()
    {
        var p = Owner(); var ally = Ally(p); int mana = NecromancerEffects.ManaBalance256(p);
        try
        {
            Require(ClassCombat.ContinueCast(p, CombatAbility.Enchant, ally, ally.Location, Core.TickCount), "Enchant begin");
            var pending = ClassCombat.For(p);
            Require(NecromancerEffects.ManaBalance256(p) == mana && SorceressEffects.Enchantment(ally) == null, "Paid during windup");
            ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p, pending.EffectAt) && NecromancerEffects.ManaBalance256(p) == mana
                && SorceressEffects.Enchantment(ally) == null, "Cancelled Enchant released/paid");
            NecromancerEffects.SetMana256(p, 25 * 256 - 1);
            Require(!SorceressCombat.Release(p, CombatAbility.Enchant, ally.Serial, ally.Location)
                && NecromancerEffects.ManaBalance256(p) == 25 * 256 - 1 && SorceressEffects.Enchantment(ally) == null,
                "Insufficient fractional mana paid or created a buff");
            NecromancerEffects.SetMana256(p, mana);
            ally.MoveToWorld(new(p.X + 20, p.Y, p.Z), p.Map);
            Require(!SorceressCombat.Release(p, CombatAbility.Enchant, ally.Serial, ally.Location)
                && NecromancerEffects.ManaBalance256(p) == mana, "Out-of-range paid");
            ally.MoveToWorld(new(p.X + 2, p.Y, p.Z), p.Map);
            // Cancellation retains the cast's recovery. Advance the explicit
            // action-clock input to its deadline; do not erase that recovery.
            long next = Math.Max(Core.TickCount, ClassCombat.For(p).NextAction);
            Require(ClassCombat.ContinueCast(p, CombatAbility.Enchant, ally, ally.Location, next)
                && ClassCombat.For(p).PendingAbility == CombatAbility.Enchant, "Second begin after recovery");
            long release = ClassCombat.For(p).EffectAt; Train(p, CombatAbility.Enchant, 2);
            Require(ClassCombat.ResolveAction(p, release) && NecromancerEffects.ManaBalance256(p) == mana - 26 * 256
                && SorceressEffects.Enchantment(ally) is { Rank: 2 }, "Release did not use current rank/once-only payment");
            ClassCombat.Stop(p);
            p.Internalize(); mana = NecromancerEffects.ManaBalance256(p);
            Require(!SorceressCombat.Release(p, CombatAbility.Enchant, Serial.Zero, p.Location)
                && NecromancerEffects.ManaBalance256(p) == mana, "Internal caster paid");
            p.MoveToWorld(ally.Location, ally.Map); p.Kill(); mana = NecromancerEffects.ManaBalance256(p);
            Require(!SorceressCombat.Release(p, CombatAbility.Enchant, Serial.Zero, p.Location)
                && NecromancerEffects.ManaBalance256(p) == mana, "Dead caster paid or created a buff");
        }
        finally { ClassCombat.Stop(p); Clean(p, ally); }
    }
    private static void Snapshots()
    {
        var p = Owner(); var ally = Ally(p);
        try
        {
            Train(p, CombatAbility.FireMastery, 1);
            var orb = PoisonDaggerChecks.Named(p, "Eschuta's temper");
            var v = SkillMechanics.Values(p, CombatAbility.Enchant);
            int percent = NamedElements.MasteryPercent(p, "fire");
            Require(v.Rank > 1 && percent > 30, "Real gear did not raise learned Enchant/mastery");
            SorceressEffects.CastEnchant(p, ally, v);
            var original = SorceressEffects.Enchantment(ally);
            Require(original.Min256 == (long)v.Damage256(false) * (100 + percent) / 100
                && original.Max256 == (long)v.Damage256(true) * (100 + percent) / 100, "Caster positive mastery/item capture");
            p.Backpack.DropItem(orb); ClassGear.Refresh(p); Train(p, CombatAbility.Warmth, 20);
            Require(SorceressEffects.Enchantment(ally) == original, "Ally snapshot followed caster gear/hard-point changes");
            int hard = ClassCreation.Profiles.GetRank(p.Serial, "sorceress.warmth");
            Require(hard == 20 && SkillMechanics.Values(p, CombatAbility.Enchant).Damage256(false) == Values(1, 20).Damage256(false), "Warmth item ranks became hard synergies");
            p.Kill(); Require(SorceressEffects.Enchantment(ally) == original, "Caster death erased independent recipient buff");
        }
        finally { Clean(p, ally); }
    }
    private static void Save()
    {
        var p = Owner(); var ally = Ally(p); Train(p, CombatAbility.Enchant, 20); Train(p, CombatAbility.FireMastery, 1);
        var orb = PoisonDaggerChecks.Named(p, "Eschuta's temper");
        Require(SorceressCombat.Release(p, CombatAbility.Enchant, ally.Serial, ally.Location)
            && SorceressCombat.Release(p, CombatAbility.Enchant, Serial.Zero, p.Location), "Saved paid Enchants");
        SkillHealth.Damage(p, p, 19 * 256 + 23);
        var saved = new Saved(p.Serial.Value, ally.Serial.Value, orb.Serial.Value, NecromancerEffects.ManaBalance256(p),
            SkillHealth.Balance256(p), SorceressEffects.Enchantment(p), SorceressEffects.Enchantment(ally), 0);
        Require(saved.Self.Rank > 20 && saved.Other.Rank == saved.Self.Rank, "Saved real gear rank");
        // Keep actual source deadlines and independent caster/recipient serials.
        // No held inspection deadline, wounds/resource reset or replacement actor.
        p.Internalize(); File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        var s = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p = World.FindMobile((Serial)s.Owner) as PlayerMobile;
        var ally = World.FindMobile((Serial)s.Ally) as PlayerMobile;
        Require(s.Generation < 2 && p != null && ally != null && World.FindItem((Serial)s.Orb)?.Parent == p
            && NecromancerEffects.ManaBalance256(p) == s.Mana && SkillHealth.Balance256(p) == s.Life
            && SorceressEffects.Enchantment(p) == s.Self && SorceressEffects.Enchantment(ally) == s.Other,
            "Original Enchant actors/gear/fractional resources/unextended deadlines did not survive reload");
        Require(SkillBindings.Instance.Get(p).Right == CombatAbility.Enchant
            && GearEffects.Damage(ally, 0).Fire == s.Other.Min256 / 256.0
            && GearEffects.Damage(ally, 1).Fire == s.Other.Max256 / 256.0, "Saved binding or weapon endpoints changed");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(s with { Generation = s.Generation + 1 }));
    }
    private static void Input(NetState network, string kind, string label)
    {
        var p = Owner(); Mobile target = kind == "self" ? null : kind == "ally" ? Ally(p) : FireBoltChecks.Target(p, 2);
        var recipient = kind == "ally" ? target : p;
        var account = FixtureNetwork.Authenticate(network, p, "enchant-cast-input-");
        int mana = NecromancerEffects.ManaBalance256(p); bool began = false; long deadline = Core.TickCount + 5000;
        void Finish() { Clean(p, target == null ? Array.Empty<Mobile>() : new[] { target }); account.Delete(); }
        void Observe()
        {
            try
            {
                Require(Core.TickCount < deadline, "Enchant input timeout");
                if (ClassCombat.For(p).PendingAbility == CombatAbility.Enchant && !began)
                {
                    Require(NecromancerEffects.ManaBalance256(p) == mana && SorceressEffects.Enchantment(recipient) == null, "Input paid before release");
                    began = true; Log.Information("{Label}: windup", label);
                }
                if (SorceressEffects.Enchantment(recipient) == null) { Timer.StartTimer(TimeSpan.FromMilliseconds(20), Observe); return; }
                Require(began && NecromancerEffects.ManaBalance256(p) == mana - 25 * 256
                    && SorceressEffects.Enchantment(recipient) is { Rank: 1 }
                    && (kind != "hostile" || SorceressEffects.Enchantment(target) == null), "Actual Enchant input once-only mana/recipient");
                Finish(); Log.Information("{Label}-result: passed", label);
            }
            catch (Exception e) { Finish(); Log.Error(e, "{Label}-result: failed", label); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30), () =>
        {
            var point = target?.Location ?? p.Location;
            Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",
                label, (int)CombatAbility.Enchant, (int)DiabloClass.Sorceress, target?.Serial.Value ?? 0, point.X, point.Y, point.Z);
            Observe();
        });
    }
}
