// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PoisonStateChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(PoisonStateChecks));
    private sealed record SavedTarget(uint Serial, int Balance256, TimedEffectState State, bool Player, bool Legacy);
    private sealed record Saved(uint Source, int Generation, SavedTarget[] Targets);
    private static string Manifest => Path.Join(Core.BaseDirectory, "poison-state.json");
    public static void Configure()
    {
        Register("poison-state-check", Check); Register("poison-state-reload-check", Reload);
        Register("poison-state-live-check", Live);
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Poison regeneration, ownership, source frames and saved fractions", _ => Core.LoopContext.Post(() =>
    { try { action(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static TimedEffectState State(Mobile target) => TimedEffects.Snapshot().Single(x => x.Owner == target.Serial.Value);
    private static TimedEffectState Seed(Mobile target, Mobile source, int duration, double rate, double fraction, DateTime at, SourcePoisonClock clock = null)
        => new(target.Serial.Value, at, at, 0, source.Serial.Value, duration, rate, fraction, new(0, 0, 0), new(0, 0, 0), [], PoisonClock: clock);
    private static void Replace(Mobile target, TimedEffectState state)
        => TimedEffects.Load(TimedEffects.Snapshot().Where(x => x.Owner != target.Serial.Value).Append(state).ToArray());
    private static void Roundtrip(int version = 3)
    {
        if (version == 3) { TimedEffects.Profiles.Capture(); var writer = new BufferWriter(new byte[1024 * 1024], true); TimedEffects.Profiles.Serialize(writer); TimedEffects.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position])); return; }
        var rows = TimedEffects.Snapshot(); var w = new BufferWriter(new byte[1024 * 1024], true); w.Write(version); w.Write(rows.Length);
        foreach (var row in rows) { var json = JsonSerializer.SerializeToNode(row); json.AsObject().Remove("PoisonClock"); w.Write(json.ToJsonString()); }
        TimedEffects.Profiles.Deserialize(new BufferReader(w.Buffer[..(int)w.Position]));
    }
    private static PoisonRegenProbe Target(PlayerMobile owner, int health = 2000)
    { var target = new PoisonRegenProbe { Frozen = true, RawStr = 2000, Hits = health }; target.MoveToWorld(new(owner.X + 3, owner.Y + 3, owner.Z), owner.Map); return target; }
    private static void Differential()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "poison-state-source-probe.json"))); int count = 0;
        foreach (var c in doc.RootElement.GetProperty("regen").EnumerateArray())
        {
            int kind = c[0].GetInt32(), life = c[1].GetInt32(), rate = c[2].GetInt32(), natural = c[3].GetInt32(), mask = c[4].GetInt32();
            int delta = natural - rate - (kind == 1 && mask != 0 ? natural : 0); if (delta >= 0) { continue; }
            Require(PoisonStates.BalanceAfter(life, -delta, kind == 0, c[5].GetInt32() != 0) == c[8].GetInt32(), "Complete source negative-regeneration health differential"); count++;
        }
        Require(count == 1624, "Negative source regen case count");
        PlayerMobile a = MagicArrowChecks.Fresh("PoisonOriginal"), b = MagicArrowChecks.Fresh("PoisonOther"), cOwner = MagicArrowChecks.Fresh("PoisonNew");
        var player = MagicArrowChecks.Fresh("PoisonPlayer"); var monster = Target(a); DateTime now = TimedEffects.Now.AddMinutes(10); int applications = 0;
        Mobile Owner(int id) => id == 10 ? a : id == 20 ? b : cOwner;
        try
        {
            foreach (var c in doc.RootElement.GetProperty("application").EnumerateArray())
            {
                int old = c[0].GetInt32(), rate = c[1].GetInt32(), length = c[2].GetInt32(), kind = c[5].GetInt32(); Mobile target = kind == 0 ? player : monster; TimedEffects.Clear(target);
                if (old > 0) { Replace(target, Seed(target, Owner(c[3].GetInt32()), 56000, old / 10240.0, 0, now, new(old, 17))); }
                bool accepted = TimedEffects.PoisonFrames(Owner(c[4].GetInt32()), target, rate, length, now), valid = rate > 0 && length > 0;
                Require(accepted == (valid && (old == 0 || rate >= old)), "Source equal/stronger acceptance");
                if (c[6].GetInt32() == 0) { Require(!TimedEffects.Snapshot().Any(x => x.Owner == target.Serial.Value), "Invalid poison created a state"); }
                else
                {
                    var state = State(target);
                    Require(state.PoisonSource == Owner(c[9].GetInt32()).Serial.Value && state.PoisonMs == (c[8].GetInt32() - 100) * 40 && state.PoisonClock.Rate256 == -c[7].GetInt32(), "Source ownership/rate/expiry differential");
                    Require(state.PoisonClock.ElapsedMs == (old == 0 || valid && kind == 1 ? 0 : 17), "Monster reschedule/player independent phase");
                }
                applications++;
            }
        }
        finally { TimedEffects.Clear(player); TimedEffects.Clear(monster); monster.Delete(); player.Delete(); a.Delete(); b.Delete(); cOwner.Delete(); }
        Require(applications == 384, "Source application case count"); Log.Information("Poison state:1624 negative-regeneration health and384 real application/ownership/phase source/native comparisons passed; source event services remain bounded");
    }
    private static void Cases()
    {
        PlayerMobile a = MagicArrowChecks.Fresh("PoisonOwnerA"), b = MagicArrowChecks.Fresh("PoisonOwnerB"), player = MagicArrowChecks.Fresh("PoisonFloor"); DateTime now = TimedEffects.Now;
        try
        {
            var target = Target(a, 1); Require(TimedEffects.PoisonFrames(a, target, 128, 3, now), "Source poison rejected");
            TimedEffects.Settle(target, now.AddMilliseconds(39)); Require(SkillHealth.Balance256(target) == 256, "Poison applied before40ms");
            TimedEffects.Settle(target, now.AddMilliseconds(40)); Require(target.Alive && SkillHealth.Balance256(target) == 128 && target.DamageCalls == 0, "Positive sub-one-life NPC balance or ordinary hit callback");
            TimedEffects.Settle(target, now.AddMilliseconds(80)); Require(!target.Alive && target.LastKiller == a && target.DamageCalls == 0, "NPC exact-zero poison death/credit"); target.Delete();
            player.Hits = 1; SkillHealth.Clear(player); player.Paralyzed = true;
            Require(TimedEffects.PoisonFrames(a, player, 512, 3, now), "Player source poison rejected"); TimedEffects.Settle(player, now.AddMilliseconds(120));
            Require(player.Alive && SkillHealth.Balance256(player) == 256 && player.Paralyzed, "Player poison floor or ordinary hit side effect"); player.Paralyzed = false; TimedEffects.Clear(player);
            target = Target(a, 10); target.Paralyzed = true; Require(TimedEffects.PoisonFrames(a, target, 256, 1, now), "No-hit poison setup"); TimedEffects.Settle(target, now.AddMilliseconds(40));
            Require(target.Hits == 9 && target.Paralyzed && target.DamageCalls == 0, "Poison dispatched ordinary Damage");
            target.Damage(1, a); Require(target.Hits == 8 && !target.Paralyzed && target.DamageCalls == 1, "Native ordinary-hit control inactive"); target.Delete();
            var ogre = new Ogre { Frozen = true, CantWalk = true, Hits = 1 }; ogre.MoveToWorld(new(a.X + 5, a.Y + 3, a.Z), a.Map); ogre.AIObject.AITimer.Stop();
            Require(TimedEffects.PoisonFrames(a, ogre, 16, 100, now) && TimedEffects.PoisonFrames(b, ogre, 512, 1, now.AddMilliseconds(20)), "Cross-owner stronger poison setup");
            TimedEffects.Settle(ogre, now.AddMilliseconds(60)); Require(!ogre.Alive && ogre.LastKiller == a && ClassLoot.Killer(ogre) == a && ogre.Corpse is Corpse { SanctuaryNoRewards: true }, "Real creature death/corpse reward hook lost original poison owner"); ogre.Corpse?.Delete(); ogre.Delete();
            foreach (bool frame in new[] { false, true })
            {
                TimedEffects.Clear(player); player.Hits = player.HitsMax; SkillHealth.Clear(player); int before = SkillHealth.Balance256(player);
                Require(CombatPotions.Apply(player, 5, now), "Approved Protection setup");
                Require(frame ? TimedEffects.PoisonFrames(a, player, 512, 800, now) : TimedEffects.Poison(a, player, 1600, 32000, now), "Protection poison setup");
                TimedEffects.Settle(player, now.AddSeconds(32));
                Require(before - SkillHealth.Balance256(player) == (frame ? 102 : 105) * 256 && TimedEffects.Buff(player, TimedEffectCategory.Potion, now.AddSeconds(32)) == null, "Approved Protection historical tick expiry or banked damage");
            }
            TimedEffects.Clear(player); player.Hits = player.HitsMax; SkillHealth.Clear(player); SkillHealth.SetBalance256(player, SkillHealth.Maximum256(player) - 128);
            Require(player.Hits == player.HitsMax && !SkillHealth.Full(player) && TimedEffects.AddRestoration(player, true, 1, 40, now), "Fractional life incorrectly rejected restorative lane");
            TimedEffects.Settle(player, now.AddMilliseconds(40)); Require(SkillHealth.Full(player), "Restorative lane did not heal fractional health"); TimedEffects.Clear(player);
            SkillHealth.SetBalance256(player, SkillHealth.Maximum256(player) - 128); var potion = AlchemyChecks.Potion(player, RestorationKind.InstantHealing, 1);
            Require(PotionRestoration.Drink(player, potion, new Random(17)) && SkillHealth.Full(player), "Real instant potion rejected displayed-full fractional life"); TimedEffects.Clear(player);
            player.Hits = 1; SkillHealth.Clear(player);
            for (int i = 0; i < 10; i++) { Require(TimedEffects.AddRestoration(player, true, 1000000, 1000, now), "Maximum queued restoration rejected"); }
            TimedEffects.Settle(player, now.AddSeconds(10)); Require(SkillHealth.Full(player), "Queued restoration FP conversion overflowed"); TimedEffects.Clear(player);
            var pending = Target(a, 10); SkillHealth.SetBalance256(pending, 2432); Replace(pending, Seed(pending, a, 0, 0, .75, now)); Roundtrip(2);
            TimedEffects.Settle(pending, now.AddMilliseconds(1)); Require(SkillHealth.Balance256(pending) == 2240, "Expired legacy pending poison lost");
            TimedEffects.Settle(pending, now.AddMilliseconds(2)); Require(SkillHealth.Balance256(pending) == 2240, "Expired legacy pending poison repaid"); pending.Delete();
            var negative = Target(a, 10); Replace(negative, Seed(negative, a, 1000, .025, -5e-10, now)); Roundtrip(2);
            Require(TimedEffects.PoisonFrames(a, negative, 256, 25, now), "Legacy negative fraction source transition"); TimedEffects.Settle(negative, now.AddMilliseconds(40));
            Require(SkillHealth.Balance256(negative) == 9 * 256 && State(negative).PoisonFraction == 0, "Legacy numerical zero lost the next raw poison unit"); negative.Delete();
            foreach (int version in new[] { 0, 1, 2 })
            {
                target = Target(a, 10); SkillHealth.SetBalance256(target, 2432); Replace(target, Seed(target, a, 1000, .001, .75, now)); Roundtrip(version);
                Require(SkillHealth.Balance256(target) == 2432 && State(target).PoisonFraction == .75, "Legacy load applied pending poison");
                TimedEffects.Settle(target, now.AddMilliseconds(1)); Require(SkillHealth.Balance256(target) == 2240, "Legacy pending HP fraction not transferred once"); Roundtrip();
                TimedEffects.Settle(target, now.AddMilliseconds(1000)); Require(SkillHealth.Balance256(target) == 1984 && target.DamageCalls == 0, "Legacy pending balance lost/replayed or ordinary hit"); target.Delete();
            }
        }
        finally { a.Delete(); b.Delete(); player.Delete(); }
    }
    private static void Check()
    {
        AlchemyChecks.RestorationCoverage(); Differential(); Cases(); var source = MagicArrowChecks.Fresh("SavedPoisonOwner"); DateTime now = TimedEffects.Now;
        var npc = Target(source); Require(TimedEffects.PoisonFrames(source, npc, 16, 1550, now), "Saved source-frame poison"); TimedEffects.Settle(npc, now.AddMilliseconds(39));
        var player = MagicArrowChecks.Fresh("SavedPoisonPlayer"); player.Hits = 1; SkillHealth.Clear(player); Require(TimedEffects.PoisonFrames(source, player, 512, 10000, now), "Saved player poison"); TimedEffects.Settle(player, now.AddMilliseconds(39));
        var legacy = Target(source, 10); SkillHealth.SetBalance256(legacy, 2432); Replace(legacy, Seed(legacy, source, 5000, .001, .75, now));
        Mobile[] targets = [npc, player, legacy]; foreach (var target in targets) { Replace(target, State(target) with { UpdatedUtc = TimedEffects.Now.AddMinutes(10) }); }
        var valid = State(npc);
        foreach (var invalid in new[] { valid with { PoisonClock = new(0, 0) }, valid with { PoisonClock = new(16, 40) }, valid with { PoisonClock = new(16, -1) }, valid with { PoisonRate = 99 }, valid with { PoisonMs = 0 } })
        { bool rejected = false; try { TimedEffectProfiles.Validate(invalid); } catch (InvalidDataException) { rejected = true; } Require(rejected, "Invalid source poison clock accepted"); }
        Roundtrip(); File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(source.Serial.Value, 0, targets.Select(t => new SavedTarget(t.Serial.Value, SkillHealth.Balance256(t), State(t), t == player, t == legacy)).ToArray())));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); Require(World.FindMobile((Serial)saved.Source) is PlayerMobile, "Saved poison owner missing"); DateTime now = TimedEffects.Now; var rows = new SavedTarget[saved.Targets.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var old = saved.Targets[i]; var target = World.FindMobile((Serial)old.Serial); var before = State(target);
            Require(target is { Alive: true, Deleted: false } && SkillHealth.Balance256(target) == old.Balance256 && JsonSerializer.Serialize(before) == JsonSerializer.Serialize(old.State), "World lost poison clock/owner/committed or pending fraction");
            Replace(target, before with { UpdatedUtc = now }); int elapsed = saved.Generation == 0 ? 1 : 40; TimedEffects.Settle(target, now.AddMilliseconds(elapsed));
            int expected = old.Player ? 256 : old.Legacy ? (saved.Generation == 0 ? 2240 : 2230) : old.Balance256 - 16;
            Require(SkillHealth.Balance256(target) == expected && State(target).PoisonSource == saved.Source && State(target).PoisonMs == old.State.PoisonMs - elapsed, "Resumed poison repaid/lost a frame, fraction or owner");
            Replace(target, State(target) with { UpdatedUtc = now.AddMinutes(10) }); rows[i] = old with { Balance256 = SkillHealth.Balance256(target), State = State(target) };
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved with { Generation = saved.Generation + 1, Targets = rows }));
    }
    private static void Live()
    {
        var source = MagicArrowChecks.Fresh("LivePoisonOwner"); var target = Target(source); int before = SkillHealth.Balance256(target); Require(TimedEffects.PoisonFrames(source, target, 16, 1000, TimedEffects.Now), "Natural poison setup");
        Timer.DelayCall(TimeSpan.FromMilliseconds(300), () =>
        {
            try { var state = State(target); int frames = (40000 - state.PoisonMs) / 40; Require(frames is >= 4 and <= 16 && before - SkillHealth.Balance256(target) == frames * 16 && target.DamageCalls == 0, "Natural40ms poison settlement or ordinary callback"); Log.Information("poison-state-live-result: passed ({Frames} source frames)", frames); }
            catch (Exception e) { Log.Error(e, "poison-state-live-result: failed"); }
            finally { target.Delete(); source.Delete(); }
        });
    }
}

public sealed class PoisonRegenProbe : Mobile
{
    public int DamageCalls;
    [Constructible] public PoisonRegenProbe() { Body = 0x32; }
    public PoisonRegenProbe(Serial serial) : base(serial) { }
    public override bool CanRegenHits => false;
    public override int HitsMax => 2000;
    public override void OnDamage(int amount, Mobile from, bool willKill) { DamageCalls++; base.OnDamage(amount, from, willKill); }
    public override void Serialize(IGenericWriter writer) { base.Serialize(writer); writer.Write(0); writer.Write(DamageCalls); }
    public override void Deserialize(IGenericReader reader) { base.Deserialize(reader); if (reader.ReadInt() != 0) { throw new InvalidDataException("Invalid poison fixture version"); } DamageCalls = reader.ReadInt(); }
}
