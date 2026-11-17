using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BarbarianClosingEdgesChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(BarbarianClosingEdgesChecks));
    public static void Configure() => ConsoleInputHandler.RegisterCommand("barbarian-closing-edges-check",
        "Intent-adopted Leap landing and broken-hand cancellation", _ => Core.LoopContext.Post(() =>
        {
            try { Leap(); Throws(); Log.Information("barbarian-closing-edges-check: passed"); }
            catch (Exception error) { Log.Error(error, "barbarian-closing-edges-check: failed"); }
        }));
    private static void Require(bool condition, string reason)
    { if (!condition) { throw new InvalidOperationException(reason); } }
    private static void Tick(PlayerMobile owner)
    {
        var state = BarbarianLeap.State(owner);
        Require(state != null, "Missing flight");
        BarbarianLeap.Tick(state.NextUtc);
    }
    private static void Finish(PlayerMobile owner)
    {
        for (int n = 0; n < 600 && BarbarianLeap.State(owner) != null; n++) { Tick(owner); }
        Require(BarbarianLeap.State(owner) == null, "Flight failed to terminate");
    }
    private static void Leap()
    {
        foreach (string scenario in new[] { "zero", "occupied", "reservation", "moving-target" })
        {
            var owner = DoubleSwingChecks.Owner(ability: CombatAbility.LeapAttack);
            var target = DoubleSwingChecks.Target(owner, 4);
            Mobile blocker = null;
            try
            {
                DoubleSwingChecks.Ready(owner);
                int mana = NecromancerEffects.ManaBalance256(owner), life = SkillHealth.Balance256(target);
                var origin = owner.Location;
                if (scenario == "zero")
                {
                    Require(!BarbarianLeapAttack.Begin(owner, Serial.Zero, origin, Core.TickCount)
                        && BarbarianLeap.State(owner) == null && NecromancerEffects.ManaBalance256(owner) == mana,
                        "Zero-distance request allocated or charged");
                    continue;
                }
                Require(BarbarianLeapAttack.Begin(owner, target.Serial, target.Location, Core.TickCount), "Edge flight begin");
                var flight = BarbarianLeap.State(owner);
                if (scenario == "occupied")
                {
                    blocker = new GraveyardZombie { NoKillAwards = true, Frozen = true, CantWalk = true };
                    blocker.MoveToWorld(flight.Destination, owner.Map);
                    ((BaseCreature)blocker).AIObject?.AITimer.Stop();
                    Require(!LeapTerrain.Clear(owner, owner.Map, flight.Destination), "Destination was not blocked");
                }
                else if (scenario == "reservation") { World.FindMobile((Serial)flight.Reservation).Delete(); }
                else { target.MoveToWorld(new Point3D(owner.X + 15, owner.Y, owner.Z), owner.Map); }
                Finish(owner);
                Require(NecromancerEffects.ManaBalance256(owner) == mana - 2560
                    && SkillHealth.Balance256(target) == life && World.FindMobile((Serial)flight.Reservation) == null,
                    "Failed landing/moved target refunded, repeated combat or leaked reservation");
                Require(scenario == "occupied" ? owner.Location == origin : owner.Location == flight.Destination,
                    "Flight chased moving aim or failed safe return");
            }
            finally
            {
                blocker?.Delete(); target.Delete();
                if (BarbarianLeap.State(owner) != null) { Finish(owner); }
                owner.Delete();
            }
        }
    }
    private static void Throws()
    {
        foreach (bool secondary in new[] { false, true })
        foreach (bool afterFirst in new[] { false, true })
        {
            var owner = DoubleThrowChecks.Fresh();
            try
            {
                DoubleThrowChecks.Ready(owner);
                var pair = BarbarianDoubleThrow.Pair(owner).Value;
                var broken = secondary ? pair.Secondary : pair.Primary;
                int mana = NecromancerEffects.ManaBalance256(owner);
                if (afterFirst)
                {
                    Require(DoubleThrowChecks.Begin(owner), "Broken-hand scene begin");
                    Require(ClassCombat.ResolveAction(owner, ClassCombat.For(owner).EffectAt), "Primary release failed");
                    Require(ClassCombat.For(owner).DoubleThrow is { Released: 1 }, "Missing second-hand stage");
                }
                int firstQuantity = NamedAmmunition.Remaining(pair.Primary), secondQuantity = NamedAmmunition.Remaining(pair.Secondary);
                broken.MaxHitPoints = Math.Max(1, broken.MaxHitPoints); broken.HitPoints = 0;
                Require(ClassGear.Broken(broken), "Broken-hand fixture is active");
                Require(afterFirst
                    ? !ClassCombat.ResolveAction(owner, ClassCombat.For(owner).EffectAt) && ClassCombat.For(owner).DoubleThrow == null
                    : !DoubleThrowChecks.Begin(owner), "Broken pair emitted an attack");
                Require(NecromancerEffects.ManaBalance256(owner) == mana - (afterFirst ? 256 : 0)
                    && NamedAmmunition.Remaining(pair.Primary) == firstQuantity
                    && NamedAmmunition.Remaining(pair.Secondary) == secondQuantity,
                    "Broken hand consumed extra mana/ammunition or refunded the released primary");
            }
            finally { owner.Delete(); }
        }
    }
}
