using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class EnchantChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(EnchantChecks));
    private sealed record Saved(uint Ally, EnchantState State, int BaseRating, int X, int Y, int Z);
    private static string Manifest => Path.Join(Core.BaseDirectory, "enchant-checks.json");
    public static void Configure() { Register("enchant-check", Check); Register("enchant-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Enchant targeting, snapshots, weapon damage and persistence", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static GraveyardSkeleton Enemy(PlayerMobile owner, int offset = 3)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(new Point3D(owner.X + offset, owner.Y, owner.Z), owner.Map); enemy.AIObject.AITimer.Stop();
        enemy.SetHits(5000); enemy.Hits = enemy.HitsMax; enemy.SetResistance(ResistanceType.Physical, 0); enemy.SetResistance(ResistanceType.Fire, 0);
        return enemy;
    }
    private static void Cast(PlayerMobile caster, Mobile target = null)
    {
        caster.Mana = caster.ManaMax;
        Require(SorceressCombat.Release(caster, CombatAbility.Enchant, target?.Serial ?? Serial.Zero, target?.Location ?? caster.Location), "Enchant cast failed");
    }
    private static void NativeWeapon(Mobile attacker, Mobile enemy, bool ranged)
    {
        var state = SorceressEffects.Enchantment(attacker); int before = enemy.Hits*256-SkillHealth.Remainder256(enemy);
        AOS.Damage(enemy, attacker, 100, false, 100, 0, 0, 0, 0, archer: ranged, weaponDamage: true);
        double min = NamedElements.Enhance(attacker, state.Min256 / 256.0, "fire");
        double max = NamedElements.Enhance(attacker, state.Max256 / 256.0, "fire");
        int loss=before-(enemy.Hits*256-SkillHealth.Remainder256(enemy));
        Require(loss >= 100*256+(int)Math.Floor(min*256) && loss <= 100*256+(int)Math.Floor(max*256),
            $"Native weapon lost Enchant or wielder Fire Mastery: FP loss={loss}, range={100*256+Math.Floor(min*256)}..{100*256+Math.Floor(max*256)}");
    }
    private static void Check()
    {
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress);
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5720, 1350), owner.Map);
        var ally = ClassSkillChecks.Player(); ally.MoveToWorld(new Point3D(owner.X + 1, owner.Y, owner.Z), owner.Map);
        var pet = Enemy(owner, 2); pet.ControlMaster = ally; pet.Controlled = true;
        var enemy = Enemy(owner, 4);
        int rating = ClassStats.AttackRating(ally), petRating = ClassStats.AttackRating(pet), mana = owner.Mana;
        Require(ClassAbilities.AllyTarget(CombatAbility.Enchant) && ClassCombat.Friendly(owner, pet), "Ally/pet targeting metadata");
        Require(SorceressCombat.Release(owner, CombatAbility.Enchant, ally.Serial, ally.Location) && owner.Mana == mana - 25,
            "Ally Enchant source mana");
        var first = SorceressEffects.Enchantment(ally);
        Require(first is { Rank: 1, Min256: 2901, Max256: 3627, AttackPercent: 20 }
            && Math.Abs((first.UntilUtc - TimedEffects.Now).TotalSeconds - 144) < 0.2
            && ClassStats.AttackRating(ally) == rating * 120 / 100, "Enchant source values/mastery/attack rating");
        Require(GearEffects.Damage(ally, 0).Fire == first.Min256 / 256.0 && GearEffects.Damage(ally, 1).Fire == first.Max256 / 256.0,
            "Class weapon damage omitted Enchant");
        Require(SkillBindings.Instance.Assign(owner, false, CombatAbility.Enchant), "Enchant right binding");
        Require(ClassCombat.ContinueCast(owner, CombatAbility.Enchant, ally, ally.Location, Core.TickCount), "Held cast rejected an ally");
        var pending = ClassCombat.For(owner);
        Require(ClassCombat.ResolveAction(owner, pending.EffectAt), "Held ally cast did not release"); ClassCombat.Stop(owner);
        Cast(owner, pet);
        Require(ClassStats.AttackRating(pet) == petRating * 120 / 100, "Pet attack rating omitted Enchant");
        NativeWeapon(ally, enemy, false); NativeWeapon(pet, enemy, false); NativeWeapon(pet, enemy, true);
        int life = enemy.Hits, beforePet256=SkillHealth.Balance256(enemy);
        SkillCombat.HitParts(pet, enemy, new(100, 0, 0, 0, 0), true, weapon: true);
        var petEnchant=SorceressEffects.Enchantment(pet);int petLoss256=beforePet256-SkillHealth.Balance256(enemy);
        int petMin256=100*256+NamedElements.To256(NamedElements.Enhance(pet,petEnchant.Min256/256.0,"fire"));
        int petMax256=100*256+NamedElements.To256(NamedElements.Enhance(pet,petEnchant.Max256/256.0,"fire"));
        // Earlier native hits retain their fractional damage. Whole-Hits loss
        // can cross the next integer; compare the exact packet budget instead.
        Require(petLoss256>=petMin256 && petLoss256<=petMax256,$"Summon typed Enchant FP loss={petLoss256}, expected={petMin256}..{petMax256}, whole={life-enemy.Hits}");
        Cast(owner, enemy);
        Require(SorceressEffects.Enchantment(owner) != null && SorceressEffects.Enchantment(enemy) == null, "Hostile Enchant did not fall back to self");
        NativeWeapon(owner, enemy, false); NativeWeapon(owner, enemy, true);
        life = enemy.Hits; AOS.Damage(enemy, owner, 10, false, 0, 100, 0, 0, 0, spellDamage: true);
        Require(life - enemy.Hits == 13, "Enchant leaked weapon damage into an ordinary spell");
        if (owner.Backpack == null) { owner.AddItem(new Backpack()); }
        owner.RawStr = owner.RawDex = 100;
        var bow = new Bow(); owner.Backpack.DropItem(bow); Require(owner.EquipItem(bow), "Enchant bow fixture");
        var raw = GearEffects.Damage(owner, 0, enemy);
        int before256=enemy.Hits*256-SkillHealth.Remainder256(enemy);
        GearEffects.Strike(owner, enemy, 0, new Random(1));
        Require(before256-(enemy.Hits*256-SkillHealth.Remainder256(enemy))
            ==NamedElements.To256(raw.Physical)+NamedElements.To256(NamedElements.Enhance(owner,raw.Fire,"fire")),
            "Class ranged strike lost second Fire Mastery application or its fixed-point remainder");
        bow.Delete();
        Require(ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.enchant", out _), "Enchant rank2 fixture");
        Cast(owner, ally); var stronger = SorceressEffects.Enchantment(ally);
        Require(stronger.Rank == 2 && stronger.AttackPercent == 29, "Stronger Enchant did not replace");
        var weaker = ClassSkillChecks.Player(DiabloClass.Sorceress);
        weaker.MoveToWorld(new Point3D(owner.X + 2, owner.Y, owner.Z), owner.Map);
        Cast(weaker, ally); Require(SorceressEffects.Enchantment(ally) == stronger, "Weaker Enchant overwrote/extended a stronger state"); weaker.Delete();
        Require(ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.firemastery", out _), "Fire Mastery fixture");
        Require(SorceressEffects.Enchantment(ally) == stronger, "Cast snapshot changed after caster training");
        Cast(owner, ally); var refreshed = SorceressEffects.Enchantment(ally);
        Require(refreshed.Min256 > stronger.Min256 && refreshed.UntilUtc >= stronger.UntilUtc, "Equal-rank Enchant did not refresh its snapshot");
        SorceressEffects.Profiles.Store(ally, new(Enchantment: refreshed with { UntilUtc = TimedEffects.Now.AddMilliseconds(-1) }));
        Require(SorceressEffects.Enchantment(ally) == null && ClassStats.AttackRating(ally) == rating && GearEffects.Damage(ally, 0).Fire == 0,
            "Expired Enchant retained damage or attack rating");
        Cast(owner, ally);
        // Rank2 naturally lasts168s. The growing combined suite can exceed that
        // before its second reload. Hold only this inspection fixture's deadline;
        // source duration and expiry behavior were checked above, independently.
        var saved = SorceressEffects.Enchantment(ally) with { UntilUtc = TimedEffects.Now.AddMinutes(10) };
        SorceressEffects.Profiles.Store(ally,(SorceressEffects.Profiles.Find(ally) ?? new()) with { Enchantment = saved });
        pet.IsBonded = true; pet.Kill(); pet.ResurrectPet();
        Require(!pet.IsDeadBondedPet && SorceressEffects.Profiles.Find(pet) == null, "Creature death/resurrection restored stale skill effects");
        ClassCreation.Profiles.TryGet(owner.Serial, out var profile); ClassCreation.Profiles.ResetAllocatedPoints(owner, profile);
        Require(SorceressEffects.Profiles.Find(owner) == null && SorceressEffects.Enchantment(ally) == saved, "Caster respec changed an ally's independent snapshot");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(ally.Serial.Value, saved, rating, ally.X, ally.Y, ally.Z)));
        enemy.Delete(); pet.Corpse?.Delete(); pet.Delete(); owner.Delete();
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var ally = World.FindMobile((Serial)saved.Ally) as PlayerMobile;
        Require(ally != null && SorceressEffects.Enchantment(ally) == saved.State, "Enchant snapshot or absolute duration lost across reload");
        ally.MoveToWorld(new Point3D(saved.X, saved.Y, saved.Z), Map.Trammel);
        Require(ClassStats.AttackRating(ally) == saved.BaseRating * (100 + saved.State.AttackPercent) / 100, "Saved Enchant attack rating lost");
        var enemy = Enemy(ally);
        try { NativeWeapon(ally, enemy, true); } finally { enemy.Delete(); }
    }
}
