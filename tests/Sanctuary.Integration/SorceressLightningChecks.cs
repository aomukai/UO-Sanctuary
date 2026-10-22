using System;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SorceressLightningChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(SorceressLightningChecks));
    public static void Configure() => ConsoleInputHandler.RegisterCommand("sorceress-lightning-check",
        "Lightning sequence, travelling pierce and target exclusions", _ => Core.LoopContext.Post(() =>
        { try { Check(); _log.Information("sorceress-lightning-check: passed"); } catch (Exception e) { _log.Error(e, "sorceress-lightning-check: failed"); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static GraveyardSkeleton Enemy(PlayerMobile owner, int dx, int dy = 0)
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true };
        enemy.MoveToWorld(new Point3D(owner.X + dx, owner.Y + dy, owner.Z), owner.Map); enemy.AIObject.AITimer.Stop();
        enemy.SetHits(5000); enemy.Hits = enemy.HitsMax; enemy.SetResistance(ResistanceType.Energy, 0); return enemy;
    }
    private static void Check()
    {
        int[] breaks = [0, 7, 15, 23, 35, 52, 78, 117, 194];
        var weapon = new WeaponTiming(AttackFamily.Swing, -20);
        for (int i = 0; i < breaks.Length; i++)
        {
            foreach (var ability in new[] { CombatAbility.Lightning, CombatAbility.ChainLightning })
            {
                Require(CombatTiming.Calculate(DiabloClass.Sorceress, ability, weapon, ias: 200, fcr: breaks[i]).Frames == 19 - i,
                    "Lightning sequence breakpoint " + breaks[i]);
                if (i > 0)
                { Require(CombatTiming.Calculate(DiabloClass.Sorceress, ability, weapon, fcr: breaks[i] - 1).Frames == 20 - i, "Lightning breakpoint lower boundary"); }
            }
        }
        Require(CombatTiming.Calculate(DiabloClass.Sorceress, CombatAbility.Lightning, weapon).EffectFrame == 7
            && CombatTiming.Calculate(DiabloClass.Sorceress, CombatAbility.Lightning, weapon, fcr: 194).EffectFrame == 5
            && CombatTiming.Calculate(DiabloClass.Sorceress, CombatAbility.FireBolt, weapon).Frames == 13,
            "Sequence event or ordinary cast changed");
        var row = SkillMechanics.For(CombatAbility.Lightning);
        var rank20 = new SkillValues(row, k => k == row.SourceKey ? 20 : 0, _ => 0, 99);
        Require(rank20.Mana256 == 4480 && rank20.Damage256(false) == 256 && rank20.Damage256(true) == 69632,
            "Lightning source rank20 damage or fractional mana");
        var owner = ClassSkillChecks.Player(DiabloClass.Sorceress);
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner, 5640, 1290), owner.Map);
        var first = Enemy(owner, 1); var second = Enemy(owner, 4); var side = Enemy(owner, 3, 3); var outside = Enemy(owner, 14);
        var ally = ClassSkillChecks.Player(); ally.MoveToWorld(new Point3D(owner.X + 2, owner.Y, owner.Z), owner.Map);
        int a = first.Hits, b = second.Hits, c = side.Hits, d = outside.Hits, friendly = ally.Hits, mana = owner.Mana;
        Require(SorceressCombat.Begin(owner, CombatAbility.Lightning, first.Serial, first.Location, Core.TickCount), "Lightning begin failed");
        var session = ClassCombat.For(owner);
        Require(session.Timing == new ActionTiming(19, 7) && owner.Mana == mana && first.Hits == a, "Lightning spent before its sequence action point");
        // Offline fixture actors have no network-session combat pump; resolve the real boundary explicitly.
        Require(!ClassCombat.ResolveAction(owner, session.EffectAt - 1) && owner.Mana == mana && first.Hits == a,
            "Lightning released before sequence point 7");
        Require(ClassCombat.ResolveAction(owner, session.EffectAt) && owner.Mana == mana - 8 && first.Hits == a,
            "Lightning failed its sequence release or applied instant damage");
        Timer.StartTimer(TimeSpan.FromMilliseconds(1500), () =>
        {
            try
            {
                Require(first.Hits < a && second.Hits < b, "Lightning failed travelling pierce");
                Require(side.Hits == c && outside.Hits == d && ally.Hits == friendly, "Lightning became an area hit or damaged an ally/out-of-range target");
                Require(session.PendingAbility == CombatAbility.None && owner.Mana < mana, "Lightning action failed to release or spend mana");
                _log.Information("sorceress-lightning-result: passed");
            }
            catch (Exception e) { _log.Error(e, "sorceress-lightning-result: failed"); }
            finally { first.Delete(); second.Delete(); side.Delete(); outside.Delete(); ally.Delete(); owner.Delete(); }
        });
    }
}
