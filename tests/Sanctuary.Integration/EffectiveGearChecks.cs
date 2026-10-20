using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class EffectiveGearChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(EffectiveGearChecks));
    private sealed record Saved(uint Serial, string State, NativeGearDetails Native);
    private sealed record Manifest(uint Player, uint Bow, Saved[] Items);
    private static string Pathname => Path.Join(Core.BaseDirectory, "effective-gear-checks.json");
    public static void Configure()
    {
        Register("effective-gear-check", Check);
        Register("effective-gear-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable effective gear checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    private static T Craft<T>(PlayerMobile player, CraftResource material) where T : Item
    {
        var item = typeof(T).CreateInstance<T>();
        var row = EquipmentCrafting.Recipes.First(r => r.Type == typeof(T).Name && r.Tier == GearTier.Normal);
        player.Skills[row.Skill].Base = 100;
        EquipmentCrafting.Finish(item, player, row, material, 0, CraftQuality.Average, new Random(12));
        player.Backpack.DropItem(item);
        return item;
    }
    private static void Check()
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "EffectiveGear" };
        player.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        ClassCreation.Profiles.AdvanceLevel(player, 99);
        player.RawStr = 200; player.RawDex = 200;
        player.MoveToWorld(new Point3D(5900, 1151, 0), Map.Trammel);
        var enemy = new Ogre { Frozen = true };
        enemy.SetHits(2000); enemy.Hits = 2000;
        enemy.SetResistance(ResistanceType.Physical, 0); enemy.SetResistance(ResistanceType.Poison, 0);
        enemy.SetResistance(ResistanceType.Cold, 0); enemy.SetResistance(ResistanceType.Fire, 0);
        enemy.MoveToWorld(new Point3D(player.X+1, player.Y, player.Z), player.Map);
        var bow = Craft<Bow>(player, CraftResource.Frostwood);
        var cap = Craft<LeatherCap>(player, CraftResource.SpinedLeather);
        var shoes = Craft<Boots>(player, CraftResource.HornedLeather);
        var cloth = Craft<SkullCap>(player, CraftResource.None);
        var shield = Craft<WoodenShield>(player, CraftResource.OakWood);
        var helmet = Craft<PlateHelm>(player, CraftResource.Verite);
        var chest = Craft<PlateChest>(player, CraftResource.Verite);
        var sword = Craft<Longsword>(player, CraftResource.Verite);
        try
        {
            var starter = new Dagger();
            ClassGear.Profiles.MarkStarter(starter);
            Require(player.EquipItem(starter) && ClassCombat.TeethRank(player) == 1, "Starter skill grant absent");
            starter.HitPoints = 0;
            Require(ClassCombat.TeethRank(player) == 0, "Broken starter dagger retained its skill grant");
            starter.Delete();
            var oldBroken = new LeatherCap { HitPoints = 0 };
            ClassGear.Profiles.Ensure(oldBroken);
            Require(ClassGear.State(oldBroken).Defense >= ClassGear.Base(oldBroken).MinDefense && ClassGear.Defense(oldBroken) == 0,
                "Legacy broken item could not receive valid inactive history");
            oldBroken.Delete();
            var halberd = new Halberd();
            Require(player.EquipItem(halberd), "Halberd equip rejected");
            halberd.HitPoints = 0;
            using (var properties = new ObjectPropertyList(halberd))
            {
                halberd.GetProperties(properties); properties.Terminate();
                Require(CombatTiming.Range(player, CombatAbility.BasicAttack) == 1
                    && properties.Buffer.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes("Class melee reach: 1 tiles")) >= 0,
                    "Broken halberd tooltip retained weapon reach");
            }
            halberd.Delete();
            Require(player.EquipItem(cloth) && !ClassGear.Active(player, cloth) && ClassGear.Defense(cloth) == 0
                && ClassGear.RequiredLevel(cloth) == 0 && !GearDetails.Socketable(cloth)
                && ClassGear.Profiles.PrepareImbue(cloth, GearRarity.Magic, new Random(1)) == null,
                "Cloth hat supplied combat stats, requirements, sockets or Imbuing");
            player.Backpack.DropItem(cloth);
            var oldCloth = ClassGear.State(cloth) with { NativeSockets = 2, Quality = CraftQuality.Superior };
            ClassGear.Profiles.Store(cloth, oldCloth);
            Require(ClassGear.State(cloth).SocketCount == 2 && !GearDetails.Socketable(cloth), "Historical cloth sockets deleted or activated");

            Require(player.EquipItem(cap) && player.EquipItem(shoes), "Leather head/feet equip rejected");
            ClassGear.Refresh(player);
            Require(!ClassGear.Broken(shoes) && ClassGear.Active(player, shoes), "Naturally non-wearing footwear treated as broken");
            int cold = ClassGear.Material(cap, "resistance-cold");
            Require(player.ColdResistance == cold && player.EnergyResistance == ClassGear.Material(cap, "resistance-lightning")
                && player.FireResistance == ClassGear.Material(shoes, "resistance-fire"), "Material resistance not projected");
            cap.HitPoints = 0;
            Require(player.ColdResistance == 0 && ClassGear.Defense(cap) == 0 && !cap.Deleted, "Break did not immediately suppress resistance/defense");
            cap.HitPoints = cap.MaxHitPoints;
            Require(player.ColdResistance == cold, "Repair did not immediately restore material resistance");
            for (int i = 0; i < 5; i++) { ClassGear.Refresh(player); }
            Require(player.ColdResistance == cold, "Repeated refresh doubled material stats");
            player.Backpack.DropItem(cap);

            int beforeFhr = HitRecovery.Fhr(player);
            for (int seed = 0; seed < 5000 && HitRecovery.Fhr(player) == beforeFhr; seed++)
            { ClassGear.Profiles.Convert(shoes, 99, GearRarity.Magic, new Random(seed)); }
            ClassGear.Refresh(player);
            Require(HitRecovery.Fhr(player) > beforeFhr && HitRecovery.Frames(DiabloClass.Necromancer, HitRecovery.Fhr(player)) < HitRecovery.Frames(DiabloClass.Necromancer),
                "Generated FHR affix did not affect recovery");
            int fhr = HitRecovery.Fhr(player);
            shoes.MaxHitPoints = 100; shoes.HitPoints = 0;
            Require(HitRecovery.Fhr(player) == beforeFhr, "Broken footwear retained its FHR affix");
            shoes.HitPoints = 100;
            Require(HitRecovery.Fhr(player) == fhr, "Repair rerolled or failed to restore FHR");

            Require(player.EquipItem(bow), "Cold bow equip rejected");
            var damage = GearEffects.Damage(player, 1);
            var basis = ClassGear.Base(bow);
            double expected = (basis.TwoHandMax > 0 ? basis.TwoHandMax : basis.MaxDamage) * ClassGear.Material(bow, "damage-cold") / 100.0
                * Math.Max(0, 100 + ClassStats.DamageBonus(player, bow)) / 100;
            Require(Math.Abs(damage.Cold - expected) < 1e-8 && damage.Cold > 0 && damage.Physical > 0,
                "Material damage converted physical damage, used modified base, or rounded too early");
            int hits = enemy.Hits;
            GearEffects.Strike(player, enemy, 1);
            Require(hits - enemy.Hits == (int)Math.Floor(damage.Physical + damage.Cold)
                && GearEffects.ChillPercent(enemy) > 0, "Cold hit damage/chill absent");
            Require(ClassGearHooks.MovementDelay(enemy, .4) > .4
                && ClassGearHooks.AttackDelay(enemy, TimeSpan.FromSeconds(1)) > TimeSpan.FromSeconds(1), "Chill did not slow movement and attacks");
            long now = Core.TickCount;
            Require(GearEffects.ChillPercent(enemy, now + 2100) == 0, "Chill failed to expire");
            Require(ClassCombat.Perform(player, CombatAbility.BasicAttack, enemy, enemy.Location, Core.TickCount), "Cannot begin pending bow attack");
            long deadline = ClassCombat.For(player).NextAction;
            hits = enemy.Hits;
            bow.HitPoints = 0;
            Require(!ClassCombat.ResolveAction(player, ClassCombat.For(player).EffectAt) && enemy.Hits == hits
                && ClassCombat.For(player).NextAction == deadline, "Break delivered a stale weapon hit or refunded its deadline");
            Require(CombatTiming.Equipped(player) == null && CombatTiming.Range(player, CombatAbility.BasicAttack) == 1
                && CombatTiming.Weapon(bow).Family == AttackFamily.Unarmed && ClassGear.Damage(bow) == (0, 0)
                && GearEffects.Damage(player, 1).Cold == 0 && !bow.Deleted && ClassCombat.For(player).NextAction == deadline,
                "Broken bow retained damage/reach/timing or refunded its deadline");
            for (int i = 0; i < 50; i++) { bow.WearOnHit(player, enemy); }
            Require(!bow.Deleted && bow.MaxHitPoints > 0, "Broken weapon wear deleted item or consumed maximum");
            Require(player.EquipItem(shield) == false, "Broken two-hander released its physical equipment slot");
            bow.HitPoints = bow.MaxHitPoints;
            Require(CombatTiming.Equipped(player) == bow && GearEffects.Damage(player, 1).Cold > 0, "Bow repair did not restore effective weapon");
            player.Backpack.DropItem(bow);
            foreach (var resource in new[] { CraftResource.ShadowIron, CraftResource.Agapite, CraftResource.Valorite, CraftResource.Bronze })
            {
                var sample = Craft<Longsword>(player, resource);
                try
                {
                    Require(player.EquipItem(sample), "Material sample equip rejected");
                    GearEffects.Clear(enemy); enemy.Hits = 2000;
                    foreach (ResistanceType type in Enum.GetValues<ResistanceType>()) { enemy.SetResistance(type, 50); }
                    var parts = GearEffects.Damage(player, 1);
                    int direct = (int)Math.Floor((parts.Total - parts.Poison) / 2);
                    GearEffects.Strike(player, enemy, 1);
                    Require(enemy.Hits == 2000 - direct, "Typed material damage used the wrong resistance or double-rounded");
                    long poisonAt = Core.TickCount;
                    GearEffects.Tick(poisonAt + 3100);
                    Require(enemy.Hits == 2000 - direct - (int)Math.Floor(parts.Poison / 2 + 1e-9),
                        "Weapon poison was immediate, missed its resistance or used damage per second");
                }
                finally { player.Backpack.DropItem(sample); sample.Delete(); }
            }
            GearEffects.Clear(enemy);
            foreach (ResistanceType type in Enum.GetValues<ResistanceType>()) { enemy.SetResistance(type, 0); }

            Require(player.EquipItem(helmet) && player.EquipItem(chest) && player.EquipItem(sword), "Verite equip rejected");
            Require(player.EquipItem(shield) && ClassGear.BlockChance(player) > 0, "Working shield did not block");
            shield.HitPoints = 0;
            Require(ClassGear.BlockChance(player) == 0, "Broken shield retained block");
            shield.HitPoints = shield.MaxHitPoints;
            Require(ClassGear.BlockChance(player) > 0, "Repair failed to restore shield block");
            player.Backpack.DropItem(shield);
            ClassGear.Refresh(player);
            Require(ClassGear.Material(player, "life-per-five") == 2 && ClassGear.Material(player, "mana-per-five") == 1,
                "Verite contributors failed to stack");
            TimeSpan baseline = TimeSpan.FromSeconds(10);
            Require(Math.Abs(GearEffects.RegenerationRate(player, baseline, "life-per-five").TotalSeconds - 2) < .00001,
                "Additive life regeneration changed base rate or failed stacking");
            Require(Math.Abs(Mobile.ManaRegenRateHandler(player).TotalSeconds - 1 / .7) < .00001,
                "Verite mana did not add to class base regeneration");
            int weight = EquipmentBalance.RunPenalty(player);
            chest.HitPoints = 0;
            Require(ClassGear.Material(player, "life-per-five") == 1 && EquipmentBalance.RunPenalty(player) == weight,
                "Broken Verite still regenerated or lost physical running penalty");
            var effect = ClassCombat.For(player);
            effect.RecoveryUntil = Core.TickCount;
            int max = player.HitsMax;
            player.Hits = Math.Max(1, max - 5);
            ClassGear.Refresh(player);
            Require(player.Hits == Math.Max(1, max - 5), "Refresh refilled life");

            GearEffects.Clear(enemy); enemy.Hits = 2000;
            now = Core.TickCount;
            Require(GearEffects.ApplyPoison(player, enemy, 6, now), "Poison could not start");
            Require(enemy.Hits == 2000, "Poison also applied immediate damage");
            GearEffects.Tick(now + 1000);
            Require(enemy.Hits == 1998, "Poison total was interpreted as damage per second");
            Require(!GearEffects.ApplyPoison(player, enemy, 3, now + 1000), "Weaker poison replaced stronger rate");
            Require(GearEffects.ApplyPoison(player, enemy, 6, now + 1000), "Equal poison did not refresh");
            GearEffects.Tick(now + 4000);
            Require(enemy.Hits == 1992, "Repeated poison stacked independent effects or lost elapsed damage");
            Require(GearEffects.ApplyPoison(player, enemy, 1.5, now + 5000), "Fractional poison rejected");
            GearEffects.Tick(now + 8000);
            Require(enemy.Hits == 1991, "Fractional poison was rounded on each periodic tick");
            enemy.SetResistance(ResistanceType.Poison, 100);
            Require(!GearEffects.ApplyPoison(player, enemy, 100, now + 9000), "Poison bypassed immunity");

            player.Backpack.DropItem(sword);
            Require(player.EquipItem(bow), "Bow could not be restored for reload"); bow.HitPoints = 0;
            var items = new Item[] { bow, cap, shoes, cloth, shield, helmet, chest, sword };
            File.WriteAllText(Pathname, JsonSerializer.Serialize(new Manifest(player.Serial.Value, bow.Serial.Value,
                items.Select(item => new Saved(item.Serial.Value, JsonSerializer.Serialize(ClassGear.State(item)), GearDetails.Read(item))).ToArray())));
        }
        finally { GearEffects.Clear(enemy); enemy.Delete(); }
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Pathname));
        var player = World.FindMobile((Serial)saved.Player) as PlayerMobile;
        Require(player != null, "Effective gear player lost on reload");
        foreach (var row in saved.Items)
        {
            var item = World.FindItem((Serial)row.Serial);
            Require(item != null && JsonSerializer.Serialize(ClassGear.State(item)) == row.State && GearDetails.Read(item) == row.Native,
                "Effective gear history/native wear changed on restart: " + row.Serial);
        }
        ClassGear.Refresh(player);
        Require(ClassGear.Broken(World.FindItem((Serial)saved.Bow)) && CombatTiming.Equipped(player) == null
            && ClassGear.Material(player, "life-per-five") == 1 && HitRecovery.Fhr(player) > 0,
            "Reload changed broken weapon, stacked regeneration or FHR");
    }
}
