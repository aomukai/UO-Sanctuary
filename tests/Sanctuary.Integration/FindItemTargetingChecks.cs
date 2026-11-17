using System;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class FindItemTargetingChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(FindItemTargetingChecks));
    public static void Configure()
    {
        Register("find-item-targeting-check", Check);
        foreach (string mode in new[] { "right", "quick" })
        {
            string label = "find-item-ground-" + mode;
            Register(label + "-check", () => FixtureNetwork.Connected(label + "-result", network => Input(network, label)));
        }
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Approved Find Item near-click selection and real ground input", _ => Core.LoopContext.Post(() =>
        {
            try { action(); Log.Information("{Name}: passed", name); }
            catch (Exception error) { Log.Error(error, "{Name}: failed", name); }
        }));
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static void Ready(PlayerMobile owner)
    {
        ClassCombat.Stop(owner);
        ClassCombat.For(owner).NextAction = ClassCombat.For(owner).RecoveryUntil = Core.TickCount;
        NecromancerEffects.SetMana256(owner, owner.ManaMax * 256);
    }
    private static Point3D Point(PlayerMobile owner, int x, int y = 0) => new(owner.X + x, owner.Y + y, owner.Z);
    private static void Check()
    {
        var owner = FindItemChecks.Fresh();
        var first = FindItemChecks.Body(owner);
        var second = FindItemChecks.Body(owner);
        var roll = BarbarianFindItem.Roll;
        try
        {
            first.MoveToWorld(Point(owner, 2), owner.Map);
            second.MoveToWorld(Point(owner, 5), owner.Map);
            int draws = 0; BarbarianFindItem.Roll = _ => { draws++; return 99; };
            Ready(owner); int mana = NecromancerEffects.ManaBalance256(owner);
            Require(BarbarianFindItem.Acquire(owner, Point(owner, 5, 1)) == second,
                "Acquisition used caster distance instead of cursor distance");
            Require(BarbarianFindItem.Acquire(owner, Point(owner, 7)) == second
                && BarbarianFindItem.Acquire(owner, Point(owner, 8)) == null, "Two-tile cursor radius changed");
            first.MoveToWorld(Point(owner, 3, -1), owner.Map);
            second.MoveToWorld(Point(owner, 3, 1), owner.Map);
            var tie = first.Serial.Value < second.Serial.Value ? first : second;
            Require(BarbarianFindItem.Acquire(owner, Point(owner, 3)) == tie, "Equal-distance tie is unstable");
            first.QuestItem = true;
            var original = CorpseSkillRules.Profiles.Find(second);
            CorpseSkillRules.Profiles.Store(original with { Treasure = null });
            Require(!BarbarianFindItem.ValidTarget(owner, first.Serial)
                && !BarbarianFindItem.Begin(owner, Serial.Zero, Point(owner, 3), Core.TickCount)
                && NecromancerEffects.ManaBalance256(owner) == mana && draws == 0,
                "Quest/missing-context acquisition spent mana or rolled chance");
            first.QuestItem = false;
            CorpseSkillRules.Profiles.Store(original);
            first.MoveToWorld(Point(owner, 9), owner.Map);
            second.MoveToWorld(Point(owner, 8), owner.Map);
            Require(!BarbarianFindItem.ValidTarget(owner, first.Serial)
                && BarbarianFindItem.ValidTarget(owner, second.Serial), "Eight-tile corpse reach changed");
            first.MoveToWorld(Point(owner, 2), owner.Map);
            second.MoveToWorld(Point(owner, 5), owner.Map);
            var origin = owner.Location;
            Require(BarbarianFindItem.Begin(owner, Serial.Zero, Point(owner, 5, 1), Core.TickCount), "Ground begin failed");
            var action = ClassCombat.For(owner);
            Require(action.FindItemTarget == second.Serial && owner.Location == origin
                && NecromancerEffects.ManaBalance256(owner) == mana - 1792, "Ground cast walked, selected wrongly or paid incorrectly");
            ClassCombat.ReleaseCast(owner);
            ClassCombat.ResolveAction(owner, action.EffectAt);
            Require(CorpseSkillRules.NoSelect(second) && !CorpseSkillRules.NoSelect(first)
                && draws == 1 && NecromancerEffects.ManaBalance256(owner) == mana - 1792,
                "Failed chance lost reservation, rerolled or repaid");
        }
        finally
        {
            BarbarianFindItem.Roll = roll;
            first.Delete(); second.Delete(); owner.Delete();
        }
    }
    private static void Input(NetState network, string label)
    {
        var owner = FindItemChecks.Fresh();
        FixtureNetwork.Authenticate(network, owner, label + "-");
        var corpse = FindItemChecks.Body(owner);
        corpse.MoveToWorld(Point(owner, 2), owner.Map);
        Require(SkillBindings.Instance.Assign(owner, false, CombatAbility.FindItem)
            && SkillBindings.Instance.AssignQuick(owner, 7, CombatAbility.FindItem), "Ground input bindings failed");
        Ready(owner); int mana = NecromancerEffects.ManaBalance256(owner);
        var cursor = Point(owner, 2, 1);
        var originalRoll = BarbarianFindItem.Roll;
        int draws = 0; bool windup = false, finished = false;
        BarbarianFindItem.Roll = _ => { draws++; return 99; };
        Timer.StartTimer(TimeSpan.FromMilliseconds(30), () =>
            Log.Information("{Label}: ready ability={Ability} class=105 x={X} y={Y} z={Z}", label,
                (int)CombatAbility.FindItem, cursor.X, cursor.Y, cursor.Z));
        long deadline = Core.TickCount + 6000;
        void Close()
        {
            if (finished) { return; }
            finished = true; BarbarianFindItem.Roll = originalRoll;
            corpse.Delete(); owner.Delete();
        }
        void Poll()
        {
            if (finished) { return; }
            try
            {
                Require(network.Running && Core.TickCount < deadline, "Ground input timed out");
                var action = ClassCombat.For(owner);
                if (!windup && action.PendingAbility == CombatAbility.FindItem)
                { windup = true; Log.Information("{Label}: windup", label); }
                if (CorpseSkillRules.NoSelect(corpse))
                {
                    Require(windup && draws == 1 && NecromancerEffects.ManaBalance256(owner) == mana - 1792,
                        "Actual target-zero input did not release once with one payment");
                    Close(); Log.Information("{Label}-result: passed", label); return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20), Poll);
            }
            catch (Exception error) { Close(); Log.Error(error, "{Label}-result: failed", label); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20), Poll);
    }
}
