// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class ItemEffectCastChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(ItemEffectCastChecks));
    private sealed record Unit(int Id);
    private sealed class Services(JsonElement input) : ItemEffectServices<Unit>
    {
        private int V(int i) => input[i].GetInt32();
        private readonly Unit[] _units = Enumerable.Range(0, 7).Select(i => new Unit(i)).ToArray();
        private Unit _target = input[2].GetInt32() != 0 ? new Unit(3) : null;
        private (int X, int Y) _point = (17, 19);
        internal uint Flags = (uint)(0x8000 | (input[3].GetInt32() != 0 ? 64 : 0));
        internal readonly List<int[]> Trace = [];
        internal bool ThrowOnExecute;
        internal override Unit Source => V(11) == 1 ? null : _units[1];
        internal override bool HasGame => V(11) != 2;
        internal override bool Uninterruptible => V(11) == 3;
        internal override Unit CurrentTarget => _target;
        internal override (int X, int Y) FirstPathPoint => _point;
        internal override bool ServerDo
        {
            get => (Flags & 64) != 0;
            set => Flags = value ? Flags | 64 : Flags & ~64u;
        }
        internal Unit Target => V(4) != 0 ? _units[2] : null;
        internal override ItemEffectSkill Skill(int id)
        {
            Trace.Add([0, id]);
            return V(11) == 4 ? null : new(V(11) == 5 ? 0 : 1, V(1), V(8) != 0);
        }
        internal override (int Type, int Id) Identity(Unit unit) => (1, unit.Id);
        internal override void SetTarget(Unit target) { Trace.Add([1, target?.Id ?? 0]); _target = target; }
        internal override void SetPosition(int x, int y) { Trace.Add([2, x, y]); _point = (x, y); _target = null; }
        internal override bool FindPosition(int skill, int level)
        {
            Trace.Add([3, skill, level]);
            if (V(5) != 0) { SetPosition(V(5) == 1 ? 0 : 23, V(5) == 2 ? 0 : 29); }
            return V(5) != 0;
        }
        internal override Unit FindCorpse(Unit target, int skill, int level)
        { Trace.Add([4, target?.Id ?? 0, skill, level]); return V(6) != 0 ? _units[4] : null; }
        internal override Unit LastAttacker() { Trace.Add([5]); return V(7) != 0 ? _units[5] : null; }
        private void Mutate(int x, int y) { SetPosition(x, y); SetTarget(_units[6]); ServerDo = !ServerDo; }
        internal override int Start(int skill, int level, int item)
        { Trace.Add([6, skill, level, item]); Mutate(31, 37); return V(9); }
        internal override int Execute(int skill, int level, int mode, int item, int flag)
        {
            Trace.Add([7, skill, level, mode, item, flag, _target?.Id ?? 0, _point.X, _point.Y, ServerDo ? 1 : 0]);
            Mutate(41, 43);
            if (ThrowOnExecute) { throw new ArithmeticException("Deliberate nested skill exception"); }
            return V(10);
        }
        internal override void NotifyTarget(int skill, int level, int type, int id, int flag)
            => Trace.Add([8, skill, level, type, id, flag, _target?.Id ?? 0, _point.X, _point.Y, ServerDo ? 1 : 0]);
        internal override void NotifyPosition(int skill, int level, int x, int y, int flag)
            => Trace.Add([9, skill, level, x, y, flag, _target?.Id ?? 0, _point.X, _point.Y, ServerDo ? 1 : 0]);
    }
    private static void Require(bool ok, string why) { if (!ok) { throw new InvalidOperationException(why); } }
    public static void Configure()
    {
        Register("item-effect-cast-check", false);
        Register("item-effect-cast-reload-check", true);
    }
    private static void Register(string name, bool reload) => ConsoleInputHandler.RegisterCommand(name, "Complete item-effect target/position cast contracts", _ => Core.LoopContext.Post(() =>
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "item-effect-cast-cases.json")));
            int count = 0;
            var exceptionContexts = new HashSet<(int Target, int Flag)>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var input = row[0];
                var s = new Services(input);
                int result = input[0].GetInt32() == 1
                    ? ItemEffectCasts.Target(s, 82, input[12].GetInt32(), s.Target, input[13].GetInt32())
                    : ItemEffectCasts.Position(s, 82, input[12].GetInt32(), 11, 13, input[13].GetInt32());
                var expected = row[1];
                Require(result == expected[0].GetInt32() && (s.CurrentTarget?.Id ?? 0) == expected[1].GetInt32()
                    && s.FirstPathPoint == (expected[2].GetInt32(), expected[3].GetInt32()) && s.Flags == expected[4].GetUInt32(), "Item effect result/restoration at " + count);
                var trace = row[2].EnumerateArray().Select(r => r.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray();
                Require(s.Trace.Count == trace.Length && s.Trace.Zip(trace).All(p => p.First.SequenceEqual(p.Second)), "Item effect ordered service/notification trace at " + count);
                // Managed exception cleanup is additional safety, not a claim
                // about C++ exceptions or skill delivery in the source probe.
                if (input[1].GetInt32() == 0 && input[4].GetInt32() == 1 && input[11].GetInt32() == 0 && input[8].GetInt32() == 0
                    && exceptionContexts.Add((input[2].GetInt32(), input[3].GetInt32())))
                {
                    var failure = new Services(input) { ThrowOnExecute = true };
                    try { ItemEffectCasts.Target(failure, 82, 5, failure.Target, 1); throw new InvalidOperationException("Missing deliberate exception"); }
                    catch (ArithmeticException) { }
                    Require((failure.CurrentTarget?.Id ?? 0) == (input[2].GetInt32() != 0 ? 3 : 0)
                        && failure.ServerDo == (input[3].GetInt32() != 0), "Nested managed failure lost original targeting/flag");
                    if (input[2].GetInt32() == 0) { Require(failure.FirstPathPoint == (17, 19), "Nested failure lost original path point"); }
                }
                count++;
            }
            Require(count == 18912 && exceptionContexts.Count == 4, "Missing item-effect source/exception controls");
            string path = Path.Join(Core.BaseDirectory, "item-effect-cast-fixtures.json");
            const string scope = "source-cast-contracts-and-actual-blood-life-tap:other-npc-spells-not-admitted";
            if (reload) { Require(File.ReadAllText(path) == scope, "Original cast contract scope"); }
            else { File.WriteAllText(path, scope); }
            Log.Information("Item effect casts:18912complete wrapper/handler return,ordered target/path/search/start/execute/notification and restored flag controls;four managed exception cleanups;actual Blood adapter is separate");
            Log.Information("{Name}: passed", name);
        }
        catch (Exception e) { Log.Error(e, "{Name}: failed", name); }
    }));
}
