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

public static class TaintedBodyChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(TaintedBodyChecks));
    private sealed record Saved(uint Owner, uint Body, uint Helm, bool Female, string State, int Life, int Maximum, int Mana, long Order);
    private static string Manifest => Path.Join(Core.BaseDirectory, "tainted-body-fixtures.json");
    public static void Configure() { Register("tainted-body-check", Check); Register("tainted-body-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Tainted saved vulnerable body and qualified NPC fire hit", _ => Core.LoopContext.Post(() =>
    { try { action(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static PlayerMobile Owner(bool female)
    {
        var p = new PlayerMobile { Name = "TaintedBodyFixture", Player = true, Female = female, Body = female ? 0x191 : 0x190 };
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Warlock); ClassCreation.Profiles.AdvanceLevel(p, 47);
        ClassSkillChecks.Train(p, "warlock.summongoatman"); ClassSkillChecks.Train(p, "warlock.summontainted");
        ClassSkillChecks.Train(p, "warlock.demonicmastery", 16); ClassSkillChecks.Train(p, "warlock.bloodoath", 5);
        FixtureSpace.Corridor(p, 6250, 24, female ? 3720 : 3680); PoisonDaggerChecks.Named(p, "Tarnhelm");
        NecromancerEffects.SetMana256(p, p.ManaMax * 256); return p;
    }
    private static void Numeric(WarlockTaintedBody body)
    {
        var birth = body.Prepared.Snapshot.Birth; var resistance = body.SourceResistances();
        Require(body.Body == 149 && body.BaseSoundID == 0x4B0 && body.Name == "Summoned Tainted", "Approved Succubus appearance");
        Require(CreatureProfiles.Definition(body).Reference == "warbighead" && CreatureProfiles.For(body).Traits is { Family: "Demon", Demon: true, Undead: false, Size: 3 }
            && ClassStats.Level(body) == birth.PetLevel && ClassStats.AttackRating(body) == birth.ProjectedAttack && ClassStats.Defense(body) == birth.ProjectedArmor,
            "Tainted borrowed native Succubus/skeleton source identity or stats");
        Require(body.MaximumLife256 == birth.MaximumLife256 && SkillHealth.Maximum256(body) == birth.MaximumLife256
            && body.PhysicalResistance == resistance.Physical && body.FireResistance == 50 && body.ColdResistance == 20
            && body.EnergyResistance == 20 && body.PoisonResistance == 20 && NamedElements.MagicResistance(body) == 0, "Actual Tainted life and resistances");
        Require(body.SourceRegeneration256 == (birth.BaseLife256 * 3 >> 12) && body.SkillWeaponDamage == (0, 0)
            && body.CombatProfile.MinDamage == 0 && body.CombatProfile.MaxDamage == 0 && !body.UsesNativeSummonTimer && !body.CanRegenHits
            && !body.CanRegenMana && !body.BleedImmune && body.PoisonImmune == null, "Native AI/offense/immunities/recovery leaked into Tainted");
        try { body.BeginMelee(body.ControlMaster); throw new InvalidOperationException("Unfinished GenericPet combat silently used native AI"); } catch (NotSupportedException) { }
    }
    private static Saved Scene(bool female)
    {
        var p = Owner(female); var facets = SummonDifficulties.Facets(); WarlockTaintedBody body = null;
        try
        {
            DateTime at = TimedEffects.Now.AddDays(2); int mana = NecromancerEffects.ManaBalance256(p); var capture = TaintedPrepared.Capture(p);
            body = WarlockTaintedBody.CreatePrepared(p, new(p.X + 1, p.Y, p.Z), at); Numeric(body);
            Require(body.ControlMaster == p && body.Permitted(p) && body.Summoned && body.ControlSlots == 0 && !body.Blessed
                && body.Prepared.Snapshot == capture && NecromancerEffects.ManaBalance256(p) == mana
                && !ClassCombat.Available(p, CombatAbility.SummonTainted), "Tainted creation/ownership/capture accidentally paid or enabled a learned cast");
            SkillHealth.SetBalance256(body, body.MaximumLife256 - 257); body.AdvanceRecovery(at.AddMilliseconds(39), true);
            Require(SkillHealth.Balance256(body) == body.MaximumLife256 - 257, "Tainted recovered early"); body.AdvanceRecovery(at.AddMilliseconds(40), true);
            Require(SkillHealth.Balance256(body) == body.MaximumLife256 - 257 + body.SourceRegeneration256, "Shared explicit source-frame recovery");
            int life = SkillHealth.Balance256(body); body.AdvanceRecovery(at.AddMilliseconds(80), false);
            Require(SkillHealth.Balance256(body) == life && body.Map == Map.Internal, "Offline Tainted healed or kept fighting");
            var helm = p.FindItemOnLayer(Layer.Helm); p.Backpack.DropItem(helm); ClassGear.Refresh(p); ClassSkillChecks.Train(p, "warlock.summontainted", 2);
            Require(body.Prepared.Snapshot == capture, "Live training/gear recaptured existing Tainted");
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID, MonsterDifficulty.Normal), new(Map.Felucca.MapID, MonsterDifficulty.Nightmare), new(Map.Ilshenar.MapID, MonsterDifficulty.Hell)]);
            foreach (var map in new[] { Map.Felucca, Map.Ilshenar, Map.Trammel })
            {
                int oldLife = SkillHealth.Balance256(body), oldMaximum = SkillHealth.Maximum256(body);
                p.MoveToWorld(new(6250, female ? 3720 : 3680, 0), map); SummonDifficulties.Sync(p); Numeric(body);
                Require(body.Prepared.Snapshot == TaintedPrepared.Rescale(capture, SummonDifficulties.For(map))
                    && SkillHealth.Balance256(body) == (long)oldLife * SkillHealth.Maximum256(body) / oldMaximum, "Tainted difficulty transition lost relative wounds/captured ranks");
            }
            Require(p.EquipItem(helm), "Original saved Tainted helm restore"); ClassGear.Refresh(p);
            var original = body.Prepared; string text = body.SavedText; life = SkillHealth.Balance256(body);
            foreach (bool fire in new[] { false, true })
            {
                var v = original.Snapshot.Birth.Suppliers;
                var unsupported = v with { PhysicalSunder = fire ? 0 : 1, FireSunder = fire ? 1 : 0 };
                try { body.RestorePrepared(original with { Snapshot = original.Snapshot with { Birth = original.Snapshot.Birth with { Suppliers = unsupported } } }); throw new InvalidOperationException("Unsupported Tainted sunder admitted"); }
                catch (InvalidDataException) { }
                Require(body.SavedText == text && SkillHealth.Balance256(body) == life, "Rejected Tainted restore changed state/life");
            }
            SkillHealth.SetBalance256(body, body.MaximumLife256 - 257); PersistentMinions.Suspend(p);
            body.RestorePrepared(body.Prepared with { NextRecoveryUtc = TimedEffects.Now.AddDays(2) });
            var saved = new Saved(p.Serial.Value, body.Serial.Value, helm.Serial.Value, female, body.SavedText, SkillHealth.Balance256(body), body.MaximumLife256,
                NecromancerEffects.ManaBalance256(p), BattleCommand.PetOrder(body)); body = null; p = null; return saved;
        }
        finally { SummonDifficulties.SetFacets(facets); body?.Delete(); p?.Delete(); }
    }
    private static void Hits()
    {
        var p = Owner(false); var body = WarlockTaintedBody.CreatePrepared(p, new(p.X + 1, p.Y, p.Z), TimedEffects.Now.AddDays(2));
        var target = FireBoltChecks.Target(p, 5); var friend = WarlockTaintedBody.CreatePrepared(p, new(p.X + 2, p.Y, p.Z), TimedEffects.Now.AddDays(2));
        try
        {
            int life = SkillHealth.Balance256(body); Require(body.CanBeDamaged(), "Tainted accidentally invulnerable"); SkillHealth.Damage(target, body, 257);
            Require(SkillHealth.Balance256(body) == life - 257 && SkillHealth.Heal(body, 1) == 1 && SkillHealth.Balance256(body) == life - 256, "Incoming fractional Tainted wound/heal");
            var packet = TaintedNpcSkills.FireBall(body.Prepared.Snapshot.Birth.Suppliers); int raw = packet.Minimum256, mana = NecromancerEffects.ManaBalance256(p);
            target.SetHits(10000); SkillHealth.SetBalance256(target, 10000 * 256);
            foreach (var (resistance, critical) in new[] { (0, false), (50, false), (100, false), (0, true) })
            {
                target.SetResistance(ResistanceType.Fire, resistance); int before = SkillHealth.Balance256(target);
                var result = TaintedFireBallHit.DeliverQualified(body, target, packet, raw, critical, TimedEffects.Now);
                int expected = raw * (critical ? 2 : 1) * (100 - resistance) / 100;
                Require(before - SkillHealth.Balance256(target) == expected && result.Packet.Fire256 == expected
                    && result.Packet.Physical256 == 0 && result.Packet.HitFlags == 2 && result.Packet.HitClass == (critical ? 16 : expected > 0 ? 45 : 13)
                    && MonsterUninterruptibles.State(target)?.LastAttacker == body.Serial.Value, $"Qualified Tainted typed fire/critical/actual emitter: resist={resistance},critical={critical},raw={raw},loss={before-SkillHealth.Balance256(target)},expected={expected},packet={JsonSerializer.Serialize(result.Packet)},attacker={MonsterUninterruptibles.State(target)?.LastAttacker},body={body.Serial.Value}");
            }
            target.SetResistance(ResistanceType.Fire, 0); Require(CombatPotions.Apply(p, 3), "Tainted owner potion fixture");
            int beforePotion = SkillHealth.Balance256(target); TaintedFireBallHit.DeliverQualified(body, target, packet, raw, false, TimedEffects.Now);
            Require(beforePotion - SkillHealth.Balance256(target) == raw && NecromancerEffects.ManaBalance256(p) == mana, "Tainted inherited owner potion lanes or spent owner mana");
            Require(PreparedMonsterCredit.Owner(target, body) == p && target.FindDamageEntryFor(body)?.Responsible?.Any(r => r.Damager == p && r.DamageGiven > 0) == true, "Tainted fire hit lost owner credit");
            foreach (var invalid in new[] { packet with { Radius = 5 }, packet with { Minimum256 = raw + 1 } })
            { int before = SkillHealth.Balance256(target); try { TaintedFireBallHit.DeliverQualified(body, target, invalid, raw, false, TimedEffects.Now); throw new InvalidOperationException("Invalid Tainted copy admitted"); } catch (NotSupportedException) { } Require(SkillHealth.Balance256(target) == before, "Rejected copy changed HP"); }
            int allyLife = SkillHealth.Balance256(friend);
            try { TaintedFireBallHit.DeliverQualified(body, friend, packet, raw, false, TimedEffects.Now); throw new InvalidOperationException("Friendly Tainted hit admitted"); } catch (NotSupportedException) { }
            Require(SkillHealth.Balance256(friend) == allyLife, "Friendly hit changed life");
            TimedEffects.Clear(p); SkillHealth.SetBalance256(target, 256); TaintedFireBallHit.DeliverQualified(body, target, packet, raw, false, TimedEffects.Now);
            Require(!target.Alive && target.LastKiller == body, "Tainted ordered lethal hit lost original NPC source");
        }
        finally { target.Delete(); friend.Delete(); body.Delete(); p.Delete(); }
    }
    private static void Check() { Hits(); File.WriteAllText(Manifest, JsonSerializer.Serialize(new[] { Scene(false), Scene(true) })); }
    private static void Reload()
    {
        foreach (var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p = World.FindMobile((Serial)saved.Owner) as PlayerMobile; var body = World.FindMobile((Serial)saved.Body) as WarlockTaintedBody; var helm = World.FindItem((Serial)saved.Helm);
            Require(p != null && body != null && body.ControlMaster == p && p.Female == saved.Female && helm?.Parent == p && body.SavedText == saved.State
                && SkillHealth.Balance256(body) == saved.Life && body.MaximumLife256 == saved.Maximum && NecromancerEffects.ManaBalance256(p) == saved.Mana
                && BattleCommand.PetOrder(body) == saved.Order && !ClassCombat.Available(p, CombatAbility.SummonTainted), "Original Tainted/owner/gear/wounds/resources/order/gate changed across reload");
            Numeric(body); int before = SkillHealth.Balance256(body); body.AdvanceRecovery(TimedEffects.Now, false);
            Require(SkillHealth.Balance256(body) == before, "Retained future recovery deadline healed on reload");
        }
    }
}
