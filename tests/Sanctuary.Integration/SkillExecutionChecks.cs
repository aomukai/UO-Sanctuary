// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class SkillExecutionChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(SkillExecutionChecks));
    private sealed class Services(JsonElement input) : SkillExecutionServices
    {
        private int V(int i) => input[i].GetInt32();
        private readonly ExecutionSkill _used = new(input[2].GetInt32() == 3 ? 81 : 82, input[2].GetInt32() == 4 ? 1u : 0);
        private readonly ExecutionSkill _highest = new(82, 0);
        internal readonly List<int[]> Trace = [];
        internal bool Cleared;
        internal uint Flags = 0x8000;
        internal int? OverrideEffect;
        internal override bool HasGame => V(1) != 1;
        internal override bool HasSource => V(1) != 2;
        internal override int UnitType => V(0);
        internal override SkillExecutionRecord Record(int skill)
        {
            Trace.Add([0, skill]);
            return V(1) == 3 ? null : new(unchecked((ushort)V(8)), OverrideEffect ?? V(9), V(12), 0, V(5) != 0, V(6) != 0, V(14) != 0, V(16) != 0, V(18), 17);
        }
        internal override ExecutionSkill UsedSkill() { Trace.Add([1, V(2) != 0 ? 1 : 0]); return V(2) != 0 ? _used : null; }
        internal override bool HasRoom { get { Trace.Add([2]); return V(4) != 0; } }
        internal override bool RoomInTown { get { Trace.Add([15]); return V(4) == 1; } }
        internal override void ClearUsedSkill() { Trace.Add([3]); Cleared = true; }
        internal override void TownNeutral() => Trace.Add([4]);
        internal override ExecutionSkill HighestSkill(int skill) { Trace.Add([6, skill]); return V(3) != 0 ? _highest : null; }
        internal override int Rank(ExecutionSkill skill, bool bonuses)
        {
            bool used = ReferenceEquals(skill, _used);
            Trace.Add([5, used ? 1 : 2, bonuses ? 1 : 0]);
            return used ? V(2) == 2 ? 0 : 3 : V(3) == 2 ? 0 : 4;
        }
        internal override int DoCount => 191;
        internal override bool ManaAllowed() { Trace.Add([7]); return V(7) != 0; }
        internal override bool HasDo(int function) => V(10) != 0;
        internal override int Execute(int function, int skill, int level) { Trace.Add([8, function, skill, level]); return V(11); }
        internal override int MissileCount => 10;
        internal override void SetServerDo() => Flags |= 64;
        internal override (int X, int Y)? TargetPoint
        {
            get { Trace.Add([9]); return V(13) is >= 1 and <= 3 ? (V(13) == 2 ? 0 : 23, V(13) == 3 ? 0 : 29) : null; }
        }
        internal override (int X, int Y) FirstPathPoint => (V(13) == 4 ? 0 : 17, 19);
        internal override (int X, int Y) SourcePoint => (5, 7);
        internal override void Launch(bool lob, int missile, int skill, int level, int x, int y, int targetX, int targetY, int flags)
            => Trace.Add([10, lob ? 1 : 0, missile, skill, level, x, y, targetX, targetY, flags]);
        internal override bool HasStart(int function) => V(15) != 0;
        internal override void ConsumeMana(int skill, int level) => Trace.Add([11, skill, level]);
        internal override void ConsumeQuantity() => Trace.Add([12]);
        internal override int Delay(int formula, int skill, int level) { Trace.Add([13, formula, skill, level]); return V(17); }
        internal override uint SequenceFrame => (uint)V(19);
        internal override void SetDelay(int frames) => Trace.Add([14, frames]);
    }
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    public static void Configure() { Register("skill-execution-check", false); Register("skill-execution-reload-check", true); }
    private static void Register(string name, bool reload) => ConsoleInputHandler.RegisterCommand(name, "Complete common skill execution contract", _ => Core.LoopContext.Post(() =>
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "skill-execution-cases.json")));
            int count = 0; bool refused = false;
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var input = row[0]; var s = new Services(input);
                int result = SkillExecution.Run(s, 82, input[23].GetInt32(), input[20].GetInt32() != 0, input[21].GetInt32() != 0, input[22].GetInt32() != 0);
                Require(result == row[1][0].GetInt32() && s.Cleared == (row[1][1].GetInt32() != 0) && s.Flags == row[1][2].GetUInt32(), "Common execution result/state at " + count);
                var trace = row[2].EnumerateArray().Select(r => r.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray();
                Require(s.Trace.Count == trace.Length && s.Trace.Zip(trace).All(p => p.First.SequenceEqual(p.Second)), "Common execution ordered service trace at " + count);
                if (!refused && input[1].GetInt32() == 0 && input[8].GetInt32() == 30 && input[6].GetInt32() == 0 && input[2].GetInt32() == 0)
                {
                    var invalid = new Services(input) { OverrideEffect = 191 };
                    try { SkillExecution.Run(invalid, 82, 5, false, true, true); throw new InvalidOperationException("Unmapped override silently accepted"); }
                    catch (NotSupportedException) { }
                    Require(invalid.Trace.All(t => t[0] is not (8 or 10)), "Unsupported override dispatched a do-function or missile");
                    refused = true;
                }
                count++;
            }
            Require(count == 37680 && refused, "Missing full source execution/unsupported controls");
            string path = Path.Join(Core.BaseDirectory, "skill-execution-fixtures.json");
            const string scope = "complete-common-executor-contract:actual-blood-item-frame-only:other-services-not-admitted";
            if (reload) { Require(File.ReadAllText(path) == scope, "Original common execution scope"); }
            else { File.WriteAllText(path, scope); }
            Log.Information("Skill execution:37680complete source common-executor controls;do/missile/rank/town/mana/quantity/delay order;explicit invalid override refusal;actual Blood frame separate");
            Log.Information("{Name}: passed", name);
        }
        catch (Exception e) { Log.Error(e, "{Name}: failed", name); }
    }));
}
