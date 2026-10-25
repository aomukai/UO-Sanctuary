using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SorceressSkillChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(SorceressSkillChecks));
    private sealed record Saved(uint Owner, ColdArmorState Armor, EnergyShieldState Shield);
    private static string Manifest => Path.Join(Core.BaseDirectory, "sorceress-skills-checks.json");
    public static void Configure()
    { Register("sorceress-skills-check", Check); Register("sorceress-skills-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Sorceress armor, shield and control checks", _ => Core.LoopContext.Post(() =>
    { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static void Cast(PlayerMobile owner, CombatAbility ability)
    {
        owner.Mana = owner.ManaMax;
        Require(SorceressCombat.Release(owner, ability, Serial.Zero, owner.Location), "Cannot cast " + ability);
    }
    private static void ResetMana(PlayerMobile owner, int mana)
    { NecromancerEffects.Profiles.Clear(owner); owner.Mana = mana; }
    private static void Check()
    {
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress); var origin = ClassSkillChecks.Corridor(owner);
        owner.MoveToWorld(origin, owner.Map);
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(new Point3D(origin.X + 1, origin.Y, origin.Z), owner.Map); enemy.AIObject.AITimer.Stop();
        enemy.SetHits(5000); enemy.Hits = enemy.HitsMax; enemy.SetResistance(ResistanceType.Cold, 0); enemy.SetResistance(ResistanceType.Energy, 0);
        try
        {
            int baseline = ClassStats.Defense(owner);
            Cast(owner, CombatAbility.FrozenArmor);
            var frozen = SorceressEffects.Armor(owner);
            Require(frozen is { Defense: 30, FreezeFrames: 33 } && ClassStats.Defense(owner) == baseline * 130 / 100
                && Math.Abs((frozen.UntilUtc - TimedEffects.Now).TotalSeconds - 164) < 1, "Frozen Armor source values or defense");
            ClassStats.CheckHit(enemy, owner);
            Require(NamedControl.FrozenRemaining(enemy) == 0, "Frozen Armor triggered on a melee attempt without damage");
            AOS.Damage(owner, enemy, 10, false, 0, 100, 0, 0, 0, weaponDamage: true);
            Require(NamedControl.FrozenRemaining(enemy) == 0, "Frozen Armor triggered without physical melee damage");
            AOS.Damage(owner, enemy, 10, false, 100, 0, 0, 0, 0, weaponDamage: true);
            Require(NamedControl.FrozenRemaining(enemy) > 1500, "Native physical melee did not trigger Frozen Armor");
            TimedEffects.Clear(enemy);
            Cast(owner, CombatAbility.ShiverArmor);
            Require(SorceressEffects.Armor(owner).Ability == CombatAbility.ShiverArmor && ClassStats.Defense(owner) == baseline * 145 / 100,
                "Cold armor replacement stacked defenses");
            bool sawMiss = false;
            for (int i = 0; i < 32 && !sawMiss; i++)
            {
                int before = enemy.Hits; bool? hit = ClassStats.CheckHit(enemy, owner);
                Require(enemy.Hits < before, "Shiver Armor did not retaliate on an attempted melee attack");
                sawMiss = hit == false;
            }
            Require(sawMiss && NamedControl.FrozenRemaining(enemy) == 0 && TimedEffects.Chill(enemy, TimedEffects.Now) > 0,
                "Shiver Armor failed its miss/chill behavior");
            TimedEffects.Clear(enemy); SorceressEffects.Profiles.Clear(owner);
            EnergyShield(owner, enemy);
            Telekinesis(owner, enemy, origin);
            enemy.Frozen = true; enemy.CantWalk = true; enemy.MoveToWorld(new Point3D(origin.X + 3, origin.Y, origin.Z), owner.Map);
            Cast(owner, CombatAbility.ChillingArmor);
            int unchanged = enemy.Hits;
            SorceressEffects.MeleeAttempt(enemy, owner);
            SorceressEffects.MissileHit(enemy, owner, "chillingarmorbolt");
            Require(enemy.Hits == unchanged && SorceressEffects.Armor(owner).Defense == 60, "Chilling Armor melee/return-fire filter or defense");
            AOS.Damage(owner, enemy, 5, false, 100, 0, 0, 0, 0, archer: true, weaponDamage: true);
            Require(enemy.Hits == unchanged, "Chilling Armor applied instant counter damage");
            Timer.StartTimer(TimeSpan.FromMilliseconds(1200), () =>
            {
                try
                {
                    Require(enemy.Hits < unchanged && TimedEffects.Chill(enemy, TimedEffects.Now) > 0, "Native arrow failed travelling cold-armor retaliation");
                    var armor = SorceressEffects.Armor(owner);
                    SorceressEffects.Profiles.Store(owner, new(armor with { UntilUtc = TimedEffects.Now.AddMilliseconds(-1) }));
                    Require(SorceressEffects.Armor(owner) == null && SorceressEffects.Defense(owner) == 0, "Expired cold armor retained defense");
                    Cast(owner, CombatAbility.ChillingArmor);
                    Lifecycle();
                    Cast(owner, CombatAbility.EnergyShield);
                    var state = SorceressEffects.Profiles.Find(owner);
                    Require(state is { Armor: not null, Shield: not null }, "Cold armor and Energy Shield did not coexist");
                    Require(Math.Abs((state.Armor.UntilUtc-TimedEffects.Now).TotalSeconds-164)<1
                        && Math.Abs((state.Shield.UntilUtc-TimedEffects.Now).TotalSeconds-144)<1,"Natural cold armor/Energy Shield cast lifetimes changed");
                    // Only saved inspection deadlines are held beyond the full suite; runtime casts retain source lifetimes.
                    var inspection=TimedEffects.Now.Add(FixtureSpace.Retention);
                    state=state with{Armor=state.Armor with{UntilUtc=inspection},Shield=state.Shield with{UntilUtc=inspection}};
                    SorceressEffects.Profiles.Store(owner,state);
                    File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(owner.Serial.Value, state.Armor, state.Shield)));
                    enemy.Delete();
                    _log.Information("sorceress-skills-result: passed");
                }
                catch (Exception e) { owner.Delete(); enemy.Delete(); _log.Error(e, "sorceress-skills-result: failed"); }
            });
        }
        catch { owner.Delete(); enemy.Delete(); throw; }
    }
    private static void EnergyShield(PlayerMobile owner, Mobile enemy)
    {
        var capped = new SkillValues(SkillMechanics.For(CombatAbility.EnergyShield), k => k == "Energy Shield" ? 99 : 0, _ => 0, 99);
        Require(capped.Eval("calc1") == 95, "Energy Shield exceeded source absorption cap");
        Cast(owner, CombatAbility.EnergyShield); ResetMana(owner, 100);
        var first = SorceressEffects.Absorb(owner, new(100, 0, 0, 0, 0));
        Require(first.Physical == 80 && NecromancerEffects.ManaBalance256(owner) == 15680,
            "Energy Shield lost source Telekinesis rank1 fractional mana cost");
        for (int i = 1; i < 20; i++) { Require(ClassCreation.Profiles.TryTrain(owner.Serial, "sorceress.telekinesis", out _), "Telekinesis synergy train"); }
        ResetMana(owner, 100);
        var parts = SorceressEffects.Absorb(owner, new(100, 100, 100, 100, 100, Magic: 100));
        Require(parts.Physical == 80 && parts.Fire == 80 && parts.Lightning == 80 && parts.Cold == 80 && parts.Magic == 80
            && parts.Poison == 100 && owner.Mana == 25, "Energy Shield eligible channels, poison exclusion or hard-point efficiency");
        ResetMana(owner, 9);
        var depleted = SorceressEffects.Absorb(owner, new(100, 100, 0, 0, 0));
        Require(depleted.Physical == 88 && depleted.Fire == 100 && owner.Mana == 0 && SorceressEffects.Shield(owner) == null,
            "Shared mana exhaustion ignored source channel order or failed to remove shield");
        Cast(owner, CombatAbility.EnergyShield); ResetMana(owner, 100); owner.Hits = owner.HitsMax;
        var resistance = ClassGearHooks.Resistance; int life = owner.Hits;
        try
        {
            ClassGearHooks.Resistance = (player, type) => player == owner && type == ResistanceType.Fire ? 50 : resistance?.Invoke(player, type);
            AOS.Damage(owner, enemy, 100, false, 0, 100, 0, 0, 0, spellDamage: true);
            Require(owner.Hits == life - 40 && owner.Mana == 85, "Native resistance ran before Energy Shield mana absorption");
        }
        finally { ClassGearHooks.Resistance = resistance; }
        if (owner.Backpack == null) { owner.AddItem(new Backpack()); }
        owner.RawStr = owner.RawDex = 1000;
        var definition = NamedGear.Catalog.Items.First(d => NamedGear.Available(d.Id) && d.Properties.Any(p => p.Code == "dmg-to-mana"));
        var gear = NamedGear.Create(definition.Id, 99, new Random(1)); owner.Backpack.DropItem(gear);
        Require(owner.EquipItem(gear), "Mana-conversion gear fixture"); ClassGear.Refresh(owner);
        owner.Hits = owner.HitsMax; ResetMana(owner, 100); life = owner.Hits;
        AOS.Damage(owner, enemy, 80, false, 100, 0, 0, 0, 0, weaponDamage: true);
        Require(owner.Mana == 88 + (life - owner.Hits) * ClassGear.Bonus(owner, "dmg-to-mana") / 100,
            "Energy Shield generated damage-to-mana recovery from absorbed damage");
        gear.Delete(); ClassGear.Refresh(owner); owner.Hits = owner.HitsMax;
        SorceressEffects.Profiles.Store(owner, new(Shield: new(TimedEffects.Now.AddMilliseconds(-1), 20)));
        ResetMana(owner, 100);
        Require(SorceressEffects.Absorb(owner, new(100, 0, 0, 0, 0)).Physical == 100 && owner.Mana == 100,
            "Expired Energy Shield absorbed damage");
        SorceressEffects.Profiles.Clear(owner);
    }
    private static void Lifecycle()
    {
        var player = ClassSkillChecks.Player(DiabloClass.Sorceress);
        if (player.Backpack == null) { player.AddItem(new Backpack()); }
        try
        {
            Cast(player, CombatAbility.FrozenArmor); Cast(player, CombatAbility.EnergyShield); Cast(player, CombatAbility.ThunderStorm); Cast(player, CombatAbility.Enchant); Cast(player, CombatAbility.Blaze);
            Cast(player, CombatAbility.Meteor);
            Cast(player, CombatAbility.Blizzard);
            Cast(player, CombatAbility.FrozenOrb); Cast(player, CombatAbility.Hydra); Cast(player, CombatAbility.ChainLightning);
            InfernoFlames.Launch(player, new(player.X + 3, player.Y, player.Z), SkillMechanics.Values(player, CombatAbility.Inferno));
            Require(FireGround.Snapshot().Meteors.Any(x => x.Owner == player.Serial.Value), "Death fixture did not create pending Meteor");
            player.Kill(); player.Resurrect();
            Require(player.Alive && SorceressEffects.Profiles.Find(player) == null && !FireGround.Snapshot().Meteors.Any(x => x.Owner == player.Serial.Value)
                && !Blizzards.Snapshot().Storms.Any(x => x.Owner == player.Serial.Value) && !FrozenOrbs.Snapshot().Any(x => x.Owner == player.Serial.Value) && !Hydras.Snapshot().Heads.Any(x => x.Owner == player.Serial.Value) && !InfernoFlames.Snapshot().Any(x => x.Owner == player.Serial.Value) && !ChainLightning.Snapshot().Any(x => x.Owner == player.Serial.Value),
                "Death/resurrection retained skill state or pending Meteor");
            Cast(player, CombatAbility.FrozenArmor); Cast(player, CombatAbility.EnergyShield); Cast(player, CombatAbility.ThunderStorm); Cast(player, CombatAbility.Enchant); Cast(player, CombatAbility.Blaze);
            // Seed respec state independently of the previous cast's still-active local delay.
            FireGround.CastMeteor(player, player.Location, SkillMechanics.Values(player, CombatAbility.Meteor));
            Blizzards.Cast(player, player.Location, SkillMechanics.Values(player, CombatAbility.Blizzard));
            FrozenOrbs.Cast(player, player.Location, SkillMechanics.Values(player, CombatAbility.FrozenOrb));
            Hydras.Cast(player, player.Location, SkillMechanics.Values(player, CombatAbility.Hydra));
            ChainLightning.Cast(player, new(player.X+3,player.Y,player.Z), SkillMechanics.Values(player, CombatAbility.ChainLightning));
            InfernoFlames.Launch(player, new(player.X + 3, player.Y, player.Z), SkillMechanics.Values(player, CombatAbility.Inferno));
            ClassCreation.Profiles.TryGet(player.Serial, out var profile);
            ClassCreation.Profiles.ResetAllocatedPoints(player, profile);
            Require(SorceressEffects.Profiles.Find(player) == null && !FireGround.Snapshot().Meteors.Any(x => x.Owner == player.Serial.Value)
                && !Blizzards.Snapshot().Storms.Any(x => x.Owner == player.Serial.Value) && !FrozenOrbs.Snapshot().Any(x => x.Owner == player.Serial.Value) && !Hydras.Snapshot().Heads.Any(x => x.Owner == player.Serial.Value) && !InfernoFlames.Snapshot().Any(x => x.Owner == player.Serial.Value) && !ChainLightning.Snapshot().Any(x => x.Owner == player.Serial.Value),
                "Respec retained old skill state or pending Meteor");
        }
        finally { player.Corpse?.Delete(); player.Delete(); }
    }
    private static void Telekinesis(PlayerMobile owner, GraveyardSkeleton enemy, Point3D origin)
    {
        int before = owner.Mana;
        Require(!SorceressCombat.Release(owner, CombatAbility.Telekinesis, Serial.Zero, owner.Location) && owner.Mana == before,
            "Untargeted Telekinesis spent mana");
        enemy.Frozen = false; enemy.CantWalk = false; bool displaced = false;
        for (int i = 0; i < 64 && !displaced; i++)
        {
            enemy.MoveToWorld(new Point3D(origin.X + 1, origin.Y, origin.Z), owner.Map); enemy.AIObject.AITimer.Stop();
            owner.Mana = owner.ManaMax; var point = enemy.Location; int life = enemy.Hits;
            Require(SorceressCombat.Release(owner, CombatAbility.Telekinesis, enemy.Serial, enemy.Location) && enemy.Hits < life,
                "Telekinesis did not damage its hostile target");
            displaced = enemy.Location != point;
        }
        Require(displaced, "Source Telekinesis knockback never displaced an ordinary monster");
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner = World.FindMobile((Serial)saved.Owner);
        Require(owner is { Deleted: false, Alive: true } && SorceressEffects.Armor(owner) == saved.Armor
            && SorceressEffects.Shield(owner) == saved.Shield, "Saved cold armor or Energy Shield snapshot/lifetime lost");
    }
}
