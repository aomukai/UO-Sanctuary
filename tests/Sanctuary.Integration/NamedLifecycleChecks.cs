using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

internal static class NamedLifecycleChecks
{
    private sealed record Stored(uint Serial, uint Parent, string State, bool Insured);
    private sealed record Worn(uint Owner, int Strength, int ManaMax, int Undead, int Skills);
    private sealed record Snapshot(Stored[] Items, Worn Active);
    private static string Manifest => Path.Join(Core.BaseDirectory, "named-lifecycle-checks.json");
    private static void Require(bool ok, string why)
    { if (!ok) { throw new InvalidOperationException(why); } }
    private static string State(Item item) => JsonSerializer.Serialize(ClassGear.State(item));
    private static Item Create(string key, PlayerMobile owner)
    {
        var d = NamedGear.Catalog.Items.Single(d => d.SourceKey == key);
        var item = NamedGear.Create(d.Id, 99, new Random(197)); owner.Backpack.DropItem(item); return item;
    }
    private static bool Swap(PlayerMobile owner, DiabloClass characterClass)
    {
        string site = RecognitionChecks.Place(owner);
        ClassCreation.Profiles.TryGet(owner.Serial, out var profile);
        return WardrobeSwitch.Commit(owner, TownRecognition.PublicWardrobe(site), TownRecognition.BeginSession(owner),
            profile, ClassProfessions.Profiles.Find(owner), characterClass, SkillName.Alchemy);
    }
    internal static void Check()
    {
        var saved = new List<Item>();
        Appearance(saved); Trade(saved); Sockets(saved);
        var p = InsuranceChecks.Player();
        ClassProfessions.Profiles.Confirm(p, SkillName.Alchemy);
        Require(TownRecognition.Claim(p, RecognitionChecks.Place(p), RecognitionHonor.Hero), "Named wardrobe right missing");
        for (int i = 0; i < 50; i++)
        { Require(ClassCreation.Profiles.TrySpendStat(p, CharacterStat.Strength), "Named wardrobe strength allocation failed"); }
        Require(ClassCreation.Profiles.TryTrain(p.Serial, "necromancer.teeth", out _), "Named wardrobe learned skill missing");
        var equipment = new[] { "Civerb's Ward", "Civerb's Icon", "Civerb's Cudgel", "The Stone of Jordan" }
            .Select(n => Create(n, p)).ToArray();
        foreach (var item in equipment) { Require(p.EquipItem(item), "Named wardrobe equipment rejected: " + item.Name); }
        ClassGear.Refresh(p);
        var original = equipment.Select(State).ToArray();
        int strength = p.Str, mana = p.ManaMax, skills = ClassGear.Bonus(p, "allskills");
        Require(NamedGear.SetBonus(p, "dmg-undead%") == 200 && skills == 1, "Named wardrobe initial bonuses missing");
        p.Hits = p.HitsMax; p.Mana = p.ManaMax; p.Stam = p.StamMax;
        Require(Swap(p, DiabloClass.Sorceress), "Named wardrobe could not park equipment");
        var storage = WardrobeEquipment.Instance.Find(p.Serial, DiabloClass.Necromancer);
        Require(equipment.All(i => i.Parent == storage) && NamedGear.SetBonus(p, "dmg-undead%") == 0
            && ClassGear.Bonus(p, "allskills") == 0, "Parked named gear kept active set/skill bonuses");
        p.Hits = p.HitsMax;
        Require(Swap(p, DiabloClass.Necromancer), "Named wardrobe could not restore equipment");
        Require(equipment.All(i => i.Parent == p) && strength == p.Str && mana == p.ManaMax
            && skills == ClassGear.Bonus(p, "allskills") && original.SequenceEqual(equipment.Select(State)),
            $"Named wardrobe restoration changed rolls, requirements or bonuses: parents={string.Join(',', equipment.Select(i => i.Parent == p))}, strength={strength}/{p.Str}, mana={mana}/{p.ManaMax}, skills={skills}/{ClassGear.Bonus(p, "allskills")}, rolls={original.SequenceEqual(equipment.Select(State))}");
        var active = new Worn(p.Serial.Value, p.Str, p.ManaMax, NamedGear.SetBonus(p, "dmg-undead%"), NamedSkills.Rank(p, "necromancer.teeth"));
        saved.AddRange(equipment);
        // Leave a second class loadout parked across real saved-world boots.
        var parked = InsuranceChecks.Player(); ClassProfessions.Profiles.Confirm(parked, SkillName.Alchemy);
        Require(TownRecognition.Claim(parked, RecognitionChecks.Place(parked), RecognitionHonor.Hero), "Parked named right missing");
        var inactive = Create("The Stone of Jordan", parked); Require(parked.EquipItem(inactive), "Parked named ring equip failed");
        parked.Hits = parked.HitsMax;
        Require(Swap(parked, DiabloClass.Sorceress) && inactive.Parent is WardrobeSet, "Named parked save fixture failed");
        saved.Add(inactive);
        foreach (var key in new[] { "Civerb's Icon", "The Stone of Jordan" })
        {
            var owner = InsuranceChecks.Player(); var insured = Create(key, owner); var loose = Create(key, owner);
            // The first price quote legitimately caches a resale offer; all other saved fields must survive it.
            var unpriced = ClassGear.State(loose); EscrowValuation.Unit(loose);
            Require(JsonSerializer.Serialize(unpriced with { IdentifiedSaleOffer = ClassGear.State(loose).IdentifiedSaleOffer }) == State(loose),
                "Named valuation changed identity or property rolls");
            string insuredState = State(insured), looseState = State(loose);
            int premium = EquipmentInsurance.Cost(insured);
            Require(Banker.Deposit(owner, premium) && EquipmentInsurance.Apply(owner, [EquipmentInsurance.Quote(insured)], [true]),
                "Named death insurance payment failed");
            Require(owner.EquipItem(insured), "Named death insured equip failed");
            var corpse = CorpseEscrowChecks.Death(owner);
            Require(insured.Parent == owner.Backpack && loose.Parent == corpse && !insured.PaidInsurance
                && State(insured) == insuredState && State(loose) == looseState, "Named death protection lost/rerolled gear or duplicated premium");
            Require(ClassDeath.Recover(owner, corpse) && loose.IsChildOf(owner.Backpack), "Named free physical recovery failed");
            corpse.Delete(); saved.Add(insured);
            // Move a real nested named item through timed escrow and paid bank recovery.
            var bag = new Bag(); owner.Backpack.DropItem(bag); bag.AddItem(loose);
            var claim = CorpseEscrowChecks.Expired(CorpseEscrowChecks.Death(owner));
            var banker = CorpseEscrowChecks.BankerFor(owner); var quote = CorpseEscrow.Quote(owner, claim);
            int bankBefore = Banker.GetBalance(owner);
            bool recovered = quote != null && CorpseEscrow.Recover(owner, banker, quote);
            Require(quote != null && !recovered && loose.Parent == bag
                && !loose.Deleted && State(loose) == looseState,
                $"Unfunded named escrow changed belongings: quote={quote?.Fee}, bank={bankBefore}, recovered={recovered}, parent={loose.Parent?.GetType().Name}, deleted={loose.Deleted}, state={State(loose) == looseState}");
            Require(Banker.Deposit(owner, quote.Fee) && CorpseEscrow.Recover(owner, banker, quote)
                && bag.Parent == owner.BankBox && loose.Parent == bag && State(loose) == looseState,
                "Paid named escrow lost nested identity or changed rolls");
            saved.Add(loose);
            var waiting = Create(key, owner); var pending = CorpseEscrowChecks.Expired(CorpseEscrowChecks.Death(owner));
            Require(waiting.Parent?.Serial == pending.Storage, "Named persistent escrow fixture failed"); saved.Add(waiting);
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Snapshot(saved.Select(i =>
            new Stored(i.Serial.Value, i.Parent?.Serial.Value ?? 0, State(i), i.Insured)).ToArray(), active)));
    }
    private static void Appearance(List<Item> saved)
    {
        var p = InsuranceChecks.Player(); var ordinary = new Circlet(); p.Backpack.DropItem(ordinary);
        Require(!p.EquipItem(ordinary), "Named appearance exception relaxed ordinary elf-only equipment"); ordinary.Delete();
        var named = NamedGear.Create("unique/336", 99, new Random(31), true, nameof(Circlet)); p.Backpack.DropItem(named);
        Require(p.EquipItem(named), "Human could not wear named Circlet"); ClassGear.Refresh(p); saved.Add(named);
    }
    private static void Trade(List<Item> saved)
    {
        foreach (var key in new[] { "Civerb's Icon", "The Stone of Jordan" })
        {
            var from = InsuranceChecks.Player(); var to = InsuranceChecks.Player(); var item = Create(key, from);
            int premium = EquipmentInsurance.Cost(item);
            Require(Banker.Deposit(from, premium) && EquipmentInsurance.Apply(from, [EquipmentInsurance.Quote(item)], [true]),
                "Named trade insurance setup failed");
            string state = State(item);
            var cancelled = new SecureTrade(from, to); cancelled.From.Container.DropItem(item); cancelled.Cancel();
            Require(item.Parent == from.Backpack && item.Insured && EquipmentInsurance.Profiles.Find(item.Serial) is { Paid: true }
                && State(item) == state, "Cancelled secure trade lost named gear, rolls or prepaid insurance");
            var accepted = new SecureTrade(from, to); accepted.From.Container.DropItem(item);
            accepted.From.Accepted = accepted.To.Accepted = true; accepted.Update();
            Require(!accepted.Valid && item.Parent == to.Backpack && !item.Insured && !item.PaidInsurance
                && EquipmentInsurance.Profiles.Find(item.Serial) == null && State(item) == state,
                "Completed secure trade changed named rolls or transferred insurance credit"); saved.Add(item);
        }
    }
    private static void Sockets(List<Item> saved)
    {
        var p = ImbuingChecks.Player(SkillName.Tinkering); var tool = SocketChecks.Put(p, new TinkerTools(100));
        foreach (var key in new[] { "Civerb's Cudgel", "The Diggler" })
        {
            var item = Create(key, p); var before = ClassGear.State(item); var native = GearDetails.Read(item);
            SocketChecks.Ready(p); var q = SocketChecks.Quote(p, tool, item);
            Require(q != null && TinkeringSockets.Commit(p, q, out var result, new SocketChecks.Rolls(0))
                && result.Outcome == SocketOutcome.Added && GearDetails.Read(item) == native
                && State(item) == JsonSerializer.Serialize(before with { TinkerSockets = before.TinkerSockets + 1 }),
                "Tinkering a named item changed its identity, native details or rolls");
            GemActivationChecks.Install(p, GemActivationChecks.Gem(p, "Ruby"), item);
            Require(ClassGear.State(item).NamedItemId == before.NamedItemId && ClassGear.State(item).Gems.Length == 1,
                "Named gem installation lost identity");
            var weapon = (BaseWeapon)item; weapon.MaxHitPoints = 1; weapon.HitPoints = 1;
            string state = State(item); int ceiling = Fortification.Ceiling(item);
            var powder = ItemOrigins.Create(ItemOrigin.Drop, () => new FortifyingPowder()); p.Backpack.DropItem(powder);
            var fortify = Fortification.Quote(p, powder, item);
            Require(fortify != null && Fortification.Commit(fortify) && weapon.MaxHitPoints > 1 && weapon.MaxHitPoints <= ceiling
                && State(item) == state, "Named fortification changed rolls or failed to restore durability"); saved.Add(item);
        }
    }
    internal static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(Manifest));
        foreach (var row in saved.Items)
        {
            var item = World.FindItem((Serial)row.Serial);
            Require(item != null && !item.Deleted && item.Parent?.Serial.Value == row.Parent
                && State(item) == row.State && item.Insured == row.Insured,
                "Named wardrobe/death/escrow identity changed across reload: " + row.Serial);
        }
        var p = World.FindMobile((Serial)saved.Active.Owner) as PlayerMobile;
        Require(p != null && p.Str == saved.Active.Strength && p.ManaMax == saved.Active.ManaMax
            && NamedGear.SetBonus(p, "dmg-undead%") == saved.Active.Undead
            && NamedSkills.Rank(p, "necromancer.teeth") == saved.Active.Skills,
            "Worn named set/skill/pool effects changed across reload");
    }
}
