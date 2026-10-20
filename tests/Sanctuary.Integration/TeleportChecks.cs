using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Spells;
using Server.Spells.Fourth;
using Server.Targeting;

namespace Sanctuary.Integration;

public static class TeleportChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TeleportChecks));
    private sealed record Saved(uint Owner, uint Pack, uint Belt, uint Sorceress, Point3D Position);
    private static string Manifest => Path.Join(Core.BaseDirectory, "teleport-checks.json");
    public static void Configure() { Register("teleport-check", Check); Register("teleport-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Shared teleport landing, action timing and scroll conservation", _ => Core.LoopContext.Post(() =>
    { try { action(); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    private static void Require(bool v, string message) => RuneChecks.Require(v, message);
    internal static PlayerMobile Player(bool sorceress = false)
    {
        var p = new PlayerMobile { Player = true, Body = 0x190, Name = "Teleport check" }; p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p, sorceress ? DiabloClass.Sorceress : DiabloClass.Necromancer); ClassCreation.Profiles.AdvanceLevel(p, 99);
        p.RawStr = 1000; p.RawInt = 1000; p.Mana = p.ManaMax;
        p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5750, 1300), Map.Trammel);
        if (sorceress)
        {
            Require(!ClassCombat.Available(p, CombatAbility.Teleport), "Unlearned teleport available");
            Require(ClassCreation.Profiles.TryTrain(p.Serial, "sorceress.telekinesis", out _) && ClassCreation.Profiles.TryTrain(p.Serial, "sorceress.teleport", out _), "Talent prerequisites failed");
        }
        return p;
    }
    private static Point3D Clear(PlayerMobile p, int dx = 3) => CombatPractice.FindFloor(p.Map, p.X + dx, p.Y);
    private static TeleportScroll Scroll(PlayerMobile p)
    { var scroll = ItemOrigins.Create(ItemOrigin.Crafted, () => new TeleportScroll()); p.Backpack.DropItem(scroll); return scroll; }
    private static bool Start(PlayerMobile p, Point3D to, TeleportScroll scroll = null) => Teleportation.Begin(p, to, ClassCombat.For(p).NextAction, scroll);
    private static bool Finish(PlayerMobile p) => ClassCombat.ResolveAction(p, ClassCombat.For(p).EffectAt);
    private static void Check()
    {
        var p = Player(true); var to = Clear(p); int mana = p.Mana; var origin = p.Location;
        Require(SkillBindings.Instance.Assign(p, false, CombatAbility.Teleport) && Start(p, to), "Learned teleport/binding unavailable");
        var s = ClassCombat.For(p);
        Require(s.Timing == CombatTiming.For(p, CombatAbility.Teleport) && s.Timing.Frames == 13
            && !ClassCombat.ResolveAction(p, s.EffectAt - 1) && p.Location == origin && p.Mana == mana, "Teleport skipped cast timing");
        Require(!Start(p, to) && Finish(p) && p.Location == to && p.Mana == mana - 24 && !Finish(p), "Spell effect/cost replayed");
        Require(!Teleportation.Begin(p, origin, s.NextAction - 1), "Teleport bypassed recovery cycle");
        Require(CombatTiming.Calculate(DiabloClass.Sorceress, CombatAbility.Teleport, default, fcr: 105).Frames == 8, "FCR not used");
        p.Mana = 23; Require(!Start(p, origin), "Insufficient mana allowed"); p.Mana = p.ManaMax;
        Require(ClassCreation.Profiles.TryTrain(p.Serial, "sorceress.teleport", out _) && Teleportation.ManaCost(Teleportation.Rank(p)) == 23, "Rank mana progression failed");
        var scroll = Scroll(p); mana = p.Mana; Require(Start(p, origin, scroll) && Finish(p) && scroll.Deleted && p.Mana == mana, "Scroll cost not independent of mana");
        var sorceress = p; Landing(); Interruptions(); Belt();
        p = Player(); p.MoveToWorld(Clear(p, 12), p.Map); scroll = Scroll(p); var belt = Scroll(p);
        Require(PotionBelts.Load(p, 3, belt), "Persisted scroll could not load");
        Require(Start(p, Clear(p), scroll), "Pending save cast did not start");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, scroll.Serial.Value, belt.Serial.Value, sorceress.Serial.Value, p.Location)));
    }
    private static void Landing()
    {
        var p = Player(); var q = Clear(p); var scroll = Scroll(p);
        Require(!Teleportation.Landing(p, p.Location) && !Teleportation.Landing(p, new(-1, p.Y, p.Z))
            && !Teleportation.Landing(p, new(p.Map.Width, p.Y, p.Z)) && !Teleportation.Landing(p, new(p.X + 19, p.Y, p.Z))
            && !Teleportation.Landing(p, new(q.X, q.Y, q.Z + 1)), "Invalid/range/floating landing accepted");
        var block = new Static(0x80); block.MoveToWorld(q, p.Map);
        Require(!Start(p, q, scroll) && !scroll.Deleted, "Wall landing consumed scroll"); block.Delete();
        var between = new Static(0x80); between.MoveToWorld(new Point3D(p.X + 1, p.Y, p.Z), p.Map);
        Require(Teleportation.Landing(p, q) && Start(p, q, scroll) && Finish(p), "Wall crossing rejected"); between.Delete();
        var door = new MetalDoor(DoorFacing.WestCW); door.MoveToWorld(new Point3D(p.X - 1, p.Y, p.Z), p.Map);
        var behind = Clear(p, -3); Require(!Teleportation.Landing(p, door.Location) && Teleportation.Landing(p, behind), "Closed-door landing/crossing wrong");
        scroll = Scroll(p); Require(Start(p, behind, scroll) && Finish(p), "Closed-door crossing rejected"); door.Delete();
        var occupied = new PlayerMobile { Player = true }; occupied.MoveToWorld(Clear(p), p.Map);
        Require(!Teleportation.Landing(p, occupied.Location), "Occupied landing accepted"); occupied.Delete();
        var platform = new Static(0x71F); var below = Clear(p); platform.MoveToWorld(below, p.Map);
        var top = new Point3D(below.X, below.Y, below.Z + platform.ItemData.CalcHeight);
        Require(platform.ItemData.Surface && !platform.ItemData.Impassable && top.Z > below.Z && Teleportation.Landing(p, top), $"Stair landing/elevation rejected flags={platform.ItemData.Flags} height={platform.ItemData.CalcHeight} point={top} fit={p.Map.CanFit(top, 16)}");
        scroll = Scroll(p); Require(Start(p, top, scroll) && Finish(p) && p.Location == top, "Stair elevation lost");
        p.MoveToWorld(Clear(p, 5), p.Map); var roof = new Static(0x80); roof.MoveToWorld(new Point3D(top.X, top.Y, top.Z + 8), p.Map);
        Require(!Teleportation.Landing(p, top), "Insufficient headroom accepted"); roof.Delete(); platform.Delete();
        var hazardPoint = Clear(p); var caster = new Ogre { Frozen = true, CantWalk = true, Kills = 5 }; caster.MoveToWorld(Clear(p, -2), p.Map); caster.AIObject.AITimer.Stop();
        var field = new FireFieldItem(0x398C, hazardPoint, caster, p.Map, TimeSpan.FromMinutes(1), 0) { Visible = true };
        scroll = Scroll(p); p.Hits = p.HitsMax; int hits = p.Hits;
        bool land = Teleportation.Landing(p, hazardPoint), start = land && Start(p, hazardPoint, scroll), done = start && Finish(p);
        Require(field.ItemData.CalcHeight > 0 && land && start && done && scroll.Deleted && p.Hits < hits,
            $"Field blocked arrival or skipped native damage land={land} start={start} done={done} deleted={scroll.Deleted} hits={hits}->{p.Hits} field={field.Location} player={p.Location} harmful={caster.CanBeHarmful(p, false)} valid={SpellHelper.ValidIndirectTarget(caster,p)}"); field.Delete(); caster.Delete();
        var region = new BlockRegion(p.Map, q); region.Register(); Require(!Teleportation.Landing(p, q), "Restricted region bypassed"); region.Unregister();
        var house = new SmallOldHouse(p, 0x64); house.MoveToWorld(new Point3D(p.X + 8, p.Y, p.Z), p.Map);
        Require(!Teleportation.Landing(p, house.Location), "House restriction bypassed"); house.Delete(); p.Delete();
    }
    private sealed class BlockRegion(Map map, Point3D q) : BaseRegion("Teleport restricted", map, 250, new Rectangle3D(q.X, q.Y, -128, 1, 1, 256))
    { public override bool CheckTravel(Mobile p, Point3D point, TravelCheckType type, out TextDefinition message) { message = null; return false; } }
    private static void Interruptions()
    {
        foreach (var mode in new[] { "moved", "frozen", "paralyzed", "death", "lost", "reparent", "occupied", "recovery", "facet", "stop", "amount" })
        {
            var p = Player(); p.MoveToWorld(Clear(p, 12), p.Map); var scroll = Scroll(p); var to = Clear(p);
            Require(Start(p, to, scroll), "Could not begin interruption " + mode); var s = ClassCombat.For(p); var blocked = new PlayerMobile { Player = true };
            switch (mode)
            {
                case "moved": p.Location = Clear(p, -2); break;
                case "frozen": p.Frozen = true; break;
                case "paralyzed": p.Paralyzed = true; break;
                case "death": p.Kill(); break;
                case "lost": scroll.MoveToWorld(p.Location, p.Map); break;
                case "reparent": var bag = new Bag(); p.Backpack.DropItem(bag); bag.DropItem(scroll); break;
                case "occupied": blocked.MoveToWorld(to, p.Map); break;
                case "recovery": HitRecovery.Apply(p, p.HitsMax, s.EffectAt - 1); break;
                case "facet": p.MoveToWorld(p.Location, Map.Felucca); break;
                case "stop": ClassCombat.Stop(p); break;
                case "amount": scroll.Amount = 2; break;
            }
            Require(!Finish(p) && !scroll.Deleted && p.Location != to && !Finish(p), "Interrupted cast consumed/moved/replayed " + mode);
            blocked.Delete(); scroll.Delete(); p.Delete();
        }
        var owner = Player(); var item = Scroll(owner); var target = Clear(owner);
        item.OnDoubleClick(owner); Require(owner.Target != null && !owner.Target.CheckLOS, "Scroll did not provide obstacle-crossing cursor");
        owner.Target.Cancel(owner, TargetCancelType.Canceled); Require(!item.Deleted, "Cancel consumed scroll");
        item.OnDoubleClick(owner); owner.Target.Invoke(owner, new LandTarget(target, owner.Map)); Require(Finish(owner) && item.Deleted, "Native target did not cast");
    }
    private static void Belt()
    {
        var p = Player(); p.MoveToWorld(Clear(p, -12), p.Map); var scroll = Scroll(p); var second = Scroll(p); var q = Clear(p); int mana = p.Mana;
        Require(PotionBelts.Load(p, 0, scroll) && PotionBelts.Entry(p, 0) == scroll && !PotionBelts.Use(p, 0), "Outside-world cursor used scroll");
        Require(PotionBelts.Use(p, 0, q) && !PotionBelts.Use(p, 0, q) && Finish(p) && scroll.Deleted && PotionBelts.Entry(p, 0) == null && p.Mana == mana, "Belt scroll effect/cost wrong");
        Require(PotionBelts.Load(p, 0, second) && PotionBelts.Remove(p, 0) && second.Parent == p.Backpack && !second.Stackable, "Belt removal changed single-use identity");
        p.Delete();
    }
    private static void Reload()
    {
        var q = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = World.FindMobile((Serial)q.Owner) as PlayerMobile;
        Require(p.Location == q.Position && ClassCombat.For(p).PendingAbility == CombatAbility.None
            && SkillBindings.Instance.Get((PlayerMobile)World.FindMobile((Serial)q.Sorceress)).Right == CombatAbility.Teleport, "Reload resumed pending action or lost Teleport binding");
        Require(World.FindItem((Serial)q.Pack) is TeleportScroll { Amount: 1, Origin: ItemOrigin.Crafted } pack && pack.Parent == p.Backpack
            && World.FindItem((Serial)q.Belt) is TeleportScroll { Amount: 1, Origin: ItemOrigin.Crafted } belt && PotionBelts.Entry(p, 3) == belt, "Saved scroll origin/location/identity lost");
    }
}
