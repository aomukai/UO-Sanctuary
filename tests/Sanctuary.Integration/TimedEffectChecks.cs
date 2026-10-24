using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class TimedEffectChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TimedEffectChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "timed-effects-check.json");
    private sealed record Saved(uint Player, DateTime PotionUntil, DateTime FoodUntil, DateTime MapUntil, uint PoisonTarget, DateTime Started);
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    public static void Configure()
    { Register("timed-effect-check", Check); Register("timed-effect-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Wall-clock shared effects and catch-up", _ => Core.LoopContext.Post(() =>
    { try { action(); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    private static byte[] Save()
    {
        TimedEffects.Profiles.Capture(); var w = new BufferWriter(new byte[1024 * 1024], true); TimedEffects.Profiles.Serialize(w);
        return w.Buffer[..(int)w.Position];
    }
    private static void Roundtrip() => TimedEffects.Profiles.Deserialize(new BufferReader(Save()));
    private static void Check()
    {
        var p = ImbuingChecks.Player(SkillName.Alchemy); DateTime now = TimedEffects.Now;
        bool Apply(TimedEffectCategory category, string id, int magnitude, DateTime at) => TimedEffects.ApplyBuff(p, category, id, magnitude, at: at);
        Require(Apply(TimedEffectCategory.Potion, "second-wind", 30, now), "Initial potion missing");
        Require(Apply(TimedEffectCategory.Potion, "second-wind", 30, now.AddSeconds(18))
            && TimedEffects.Buff(p, TimedEffectCategory.Potion, now.AddSeconds(18)).ExpiresUtc == now.AddSeconds(60), "12+30 did not become42");
        Require(Apply(TimedEffectCategory.Potion, "second-wind", 30, now.AddSeconds(18)) && Apply(TimedEffectCategory.Potion, "second-wind", 30, now.AddSeconds(18))
            && TimedEffects.Buff(p, TimedEffectCategory.Potion, now.AddSeconds(18)).ExpiresUtc == now.AddSeconds(108)
            && !Apply(TimedEffectCategory.Potion, "second-wind", 30, now.AddSeconds(18)), "Potion cap wrong");
        Require(!Apply(TimedEffectCategory.Potion, "second-wind", 60, now.AddSeconds(19)), "Same potion grew magnitude");
        Require(Apply(TimedEffectCategory.Potion, "protection", 10, now.AddSeconds(19))
            && TimedEffects.Buff(p, TimedEffectCategory.Potion, now.AddSeconds(19)).ExpiresUtc == now.AddSeconds(49), "Different potion inherited duration");
        Require(Apply(TimedEffectCategory.Food, "capacity", 74, now.AddSeconds(20)) && Apply(TimedEffectCategory.Map, "dungeon", 30, now.AddSeconds(20))
            && TimedEffects.Buff(p, TimedEffectCategory.Potion, now.AddSeconds(20))?.Id == "protection", "Food/map replaced potion");
        Require(Apply(TimedEffectCategory.Food, "light", 3, now.AddSeconds(21)) && TimedEffects.Buff(p, TimedEffectCategory.Food, now.AddSeconds(21)).ExpiresUtc == now.AddSeconds(3621), "Food did not replace/reset");
        Require(Apply(TimedEffectCategory.Map, "world", 5, now.AddSeconds(22)) && TimedEffects.Buff(p, TimedEffectCategory.Map, now.AddSeconds(22)).ExpiresUtc == now.AddSeconds(1822), "Map did not replace/reset");
        Roundtrip(); Require(TimedEffects.Buff(p, TimedEffectCategory.Potion, now.AddSeconds(49)) == null
            && TimedEffects.Buff(p, TimedEffectCategory.Food, now.AddSeconds(49))?.Id == "light", "Expiry/reload refreshed/cross-cleared effects"); TimedEffects.Clear(p);
        Periodic(p); Death(p); Career(p); SaveReload(p);
        _logger.Information("Timed effects: real-time expiry, 12+30=42/cap90, independent single potion/food/map slots, exact fractions, concurrent poison/healing, death, career, offline and v0 reload checks passed");
    }
    private static void Periodic(PlayerMobile p)
    {
        DateTime now = TimedEffects.Now; p.Hits = 100; p.Mana = 0;
        Require(TimedEffects.AddRestoration(p, true, 50, 5000, now) && TimedEffects.AddRestoration(p, false, 20, 5000, now), "Restoration missing");
        TimedEffects.Settle(p, now.AddMilliseconds(1050)); int hp = p.Hits, mana = p.Mana;
        var before = TimedEffects.Snapshot().Single(r => r.Owner == p.Serial.Value); Roundtrip();
        var after = TimedEffects.Snapshot().Single(r => r.Owner == p.Serial.Value);
        Require(JsonSerializer.Serialize(before) == JsonSerializer.Serialize(after) && p.Hits == hp && p.Mana == mana, "Serialization applied/refilled healing");
        TimedEffects.Settle(p, now.AddSeconds(5)); Require(p.Hits == 150 && p.Mana == 20, "Offline restoration lost/doubled fractional amount");
        TimedEffects.Settle(p, now.AddHours(1)); Require(p.Hits == 150 && p.Mana == 20, "Elapsed restoration replayed"); TimedEffects.Clear(p);
        var target = new TimedEffectDummy { Frozen = true, RawStr = 2000, Hits = 2000 }; target.MoveToWorld(p.Location, p.Map);
        long tick = Core.TickCount; Require(GearEffects.ApplyPoison(p, target, 1.5, tick), "Fractional poison missing");
        GearEffects.Tick(tick + 1000); Roundtrip(); GearEffects.Tick(tick + 3000);
        Require(target.Hits == 1999, "Poison fraction lost on save");
        Require(GearEffects.ApplyPoison(p, target, 1.5, tick + 3000), "Second poison missing"); Roundtrip(); GearEffects.Tick(tick + 6000);
        Require(target.Hits == 1997, "Completed poison remainder lost");
        GearEffects.Clear(target); TimedEffects.Clear(p);
        p.Hits = 100; now = TimedEffects.Now;
        Require(TimedEffects.AddRestoration(p, true, 50, 5000, now), "Concurrent healing missing");
        Require(TimedEffects.Poison(target, p, 20, 5000, now), "Concurrent poison missing");
        Roundtrip(); TimedEffects.Settle(p, now.AddHours(2)); Require(p.Hits == 130, $"Concurrent offline catch-up wrong: {p.Hits}");
        TimedEffects.Clear(p); target.Delete(); p.Hits = 100;
        TimedEffects.Chill(p, 50, 2000, now); Roundtrip();
        Require(TimedEffects.Chill(p, now.AddMilliseconds(1999)) == 50 && TimedEffects.Chill(p, now.AddSeconds(2)) == 0, "Chill duration refreshed across reload");
        TimedEffects.Clear(p);
    }
    private static void Death(PlayerMobile p)
    {
        var dead = ImbuingChecks.Player(SkillName.Alchemy); DateTime now = TimedEffects.Now;
        TimedEffects.ApplyBuff(dead, TimedEffectCategory.Food, "capacity", 25, at: now); dead.Hits = 1;
        var source = new Ogre(); TimedEffects.Poison(source, dead, 100, 1000, now); TimedEffects.Settle(dead, now.AddSeconds(1));
        Require(dead.Alive && SkillHealth.Balance256(dead) == 256 && TimedEffects.Buff(dead, TimedEffectCategory.Food) != null, "Player poison floor or retained food lost"); TimedEffects.Clear(dead); dead.Delete();
        var lethal = new TimedEffectDummy { Body = 0x32, RawStr = 2000, Hits = 1 }; lethal.MoveToWorld(p.Location, p.Map);
        TimedEffects.AddRestoration(lethal, true, 100, 10000, now); TimedEffects.Poison(source, lethal, 100, 1000, now); TimedEffects.Settle(lethal, now.AddSeconds(1));
        Require(!lethal.Alive && !TimedEffects.Snapshot().Any(r => r.Owner == lethal.Serial.Value), "Lethal NPC poison healed/retained effects"); lethal.Delete(); source.Delete();
        var nativeDeath = ImbuingChecks.Player(SkillName.Alchemy); TimedEffects.ApplyBuff(nativeDeath, TimedEffectCategory.Potion, "protection", 10);
        nativeDeath.Kill(); Require(!TimedEffects.Snapshot().Any(r => r.Owner == nativeDeath.Serial.Value), "Native death retained buffs");
    }
    private static void Career(PlayerMobile p)
    {
        DateTime now = TimedEffects.Now; TimedEffects.ApplyBuff(p, TimedEffectCategory.Food, "light", 1, at: now);
        var before = TimedEffects.Buff(p, TimedEffectCategory.Food);
        EventSink.InvokeDisconnected(p); EventSink.InvokeLogout(p); EventSink.InvokeConnected(p);
        Require(TimedEffects.Buff(p, TimedEffectCategory.Food) == before, "Reconnect cleared/refreshed buff");
        string site = RecognitionChecks.Place(p); Require(TownRecognition.Claim(p, site, RecognitionHonor.Hero), "Career ceremony missing");
        ClassCombat.Stop(p); var wardrobe = WardrobeAccess.NearbyWardrobe(p); ClassCreation.Profiles.TryGet(p.Serial, out var profile);
        Require(WardrobeSwitch.Commit(p, wardrobe, TownRecognition.BeginSession(p), profile, ClassProfessions.Profiles.Find(p), profile.Class, SkillName.Tinkering)
            && TimedEffects.Buff(p, TimedEffectCategory.Food) == before, "Craft switch changed shared effect"); TimedEffects.Clear(p);
    }
    private static void SaveReload(PlayerMobile p)
    {
        DateTime now = TimedEffects.Now;
        TimedEffects.ApplyBuff(p, TimedEffectCategory.Potion, "second-wind", 30, at: now);
        TimedEffects.ApplyBuff(p, TimedEffectCategory.Food, "light", 3, at: now);
        TimedEffects.ApplyBuff(p, TimedEffectCategory.Map, "world", 5, at: now);
        var target = new TimedEffectDummy { Frozen = true, RawStr = 2000, Hits = 2000 }; target.MoveToWorld(p.Location, p.Map);
        GearEffects.ApplyPoison(p, target, 6, Core.TickCount);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, now.AddSeconds(30), now.AddHours(1), now.AddMinutes(30), target.Serial.Value, now)));
    }
    private static void Reload()
    {
        var r = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)r.Player);
        Require(p != null && TimedEffects.Buff(p, TimedEffectCategory.Food)?.ExpiresUtc == r.FoodUntil && TimedEffects.Buff(p, TimedEffectCategory.Map)?.ExpiresUtc == r.MapUntil,
            "Actual saved food/map expiry changed");
        var potion = TimedEffects.Buff(p, TimedEffectCategory.Potion);
        Require(r.PotionUntil > TimedEffects.Now ? potion?.ExpiresUtc == r.PotionUntil : potion == null, "Actual saved potion timer refreshed");
        var target = World.FindMobile((Serial)r.PoisonTarget); TimedEffects.Settle(target, TimedEffects.Now);
        if (TimedEffects.Now >= r.Started.AddSeconds(4)) { Require(target.Hits == 1994, $"Offline poison disappeared/doubled after real restart: {target.Hits}/{target.HitsMax}; {JsonSerializer.Serialize(TimedEffects.Snapshot().FirstOrDefault(s => s.Owner == target.Serial.Value))}"); }
    }
}

// This saved fixture has no native regeneration, isolating periodic damage across restarts.
public sealed class TimedEffectDummy : Mobile
{
    [Constructible] public TimedEffectDummy() { }
    public TimedEffectDummy(Serial serial) : base(serial) { }
    public override bool CanRegenHits => false;
    public override int HitsMax => 2000;
    public override void Serialize(IGenericWriter writer) { base.Serialize(writer); writer.Write(0); }
    public override void Deserialize(IGenericReader reader)
    { base.Deserialize(reader); if (reader.ReadInt() != 0) { throw new InvalidDataException("Invalid timed fixture version"); } }
}
