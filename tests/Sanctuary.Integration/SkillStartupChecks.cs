// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class SkillStartupChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(SkillStartupChecks));
    private sealed class Services(JsonElement input) : SkillStartupServices
    {
        private int V(int i) => input[i].GetInt32();
        private bool _target = input[6].GetInt32() != 0;
        internal readonly List<int[]> Trace = [];
        internal bool UsedCleared, TargetCleared, ThrowStart;
        internal override bool HasGame => V(1) != 0;
        internal override bool HasSource => V(2) != 0;
        internal override int UnitType => V(3);
        internal override ExecutionSkill UsedSkill()
        { Trace.Add([0]); return V(4) != 0 ? new(66, 0) : null; }
        internal override SkillStartupRecord Record(int skill)
        {
            Trace.Add([1, skill]);
            return V(5) == 0 ? null : new(V(7) != 0, V(8) != 0, V(9) != 0, V(18) != 0, V(20), V(23), V(26) != 0, V(27) != 0);
        }
        internal override SkillStartupTarget Target()
        { Trace.Add([2]); return !_target ? null : new(V(6) == 3 ? 0 : 1, V(6) == 2 ? 12 : 1, V(6) != 4); }
        internal override bool EnemyAllowed(SkillStartupTarget target) { Trace.Add([3]); return V(10) != 0; }
        internal override bool PetAllowed(SkillStartupTarget target, int skill) { Trace.Add([4, skill]); return V(11) != 0; }
        internal override bool AllyAllowed(SkillStartupTarget target, int skill) { Trace.Add([5, skill]); return V(12) != 0; }
        internal override void ClearTarget() { Trace.Add([6]); _target = false; TargetCleared = true; }
        internal override int Rank(ExecutionSkill skill) { Trace.Add([7]); return V(13); }
        internal override (int Rank, int Charges)? ItemInfo(ExecutionSkill skill)
        { Trace.Add([8]); return V(14) == 0 ? null : (V(16), V(15)); }
        internal override SkillStartupRoom Room() { Trace.Add([9]); return V(17) == 0 ? null : new(V(17) == 1); }
        internal override void ClearUsedSkill() { Trace.Add([10]); UsedCleared = true; }
        internal override void TownNeutral() => Trace.Add([11]);
        internal override bool PlayerManaAllowed() { Trace.Add([12]); return V(19) != 0; }
        internal override (int X, int Y) TargetPoint(SkillStartupTarget target) => (V(21) == 1 ? 0 : 23, V(21) == 2 ? 0 : 29);
        internal override (int X, int Y) FirstPathPoint
        { get { Trace.Add([13]); return (V(21) == 1 ? 0 : 17, V(21) == 2 ? 0 : 19); } }
        internal override bool ClearLine(int x, int y, uint collisionMask)
        { Trace.Add([14, x, y, (int)collisionMask]); return V(22) != 0; }
        internal override int StartCount => 91;
        internal override bool HasStart(int function) => V(24) != 0;
        internal override int ExecuteStart(int function, int skill, int rank)
        {
            Trace.Add([15, function, skill, rank]);
            if (ThrowStart) { throw new NotSupportedException("Missing nested start body"); }
            return V(25);
        }
        internal override void ConsumeMana(int skill, int rank) => Trace.Add([16, skill, rank]);
        internal override void DeletePeriodicEvent() => Trace.Add([17]);
    }
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    public static void Configure() { Register("skill-startup-check", false); Register("skill-startup-reload-check", true); }
    private static void Register(string name, bool reload) => ConsoleInputHandler.RegisterCommand(name, "Complete older skill startup contracts", _ => Core.LoopContext.Post(() =>
    {
        try
        {
            string sourcePath = Path.Join(Core.BaseDirectory, "skill-startup-cases.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(sourcePath));
            int count = 0, missing = 0;
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var input = row[0]; var s = new Services(input);
                int result = input[0].GetInt32() != 0 ? SkillStartup.Selected(s)
                    : SkillStartup.Run(s, 66, input[29].GetInt32(), input[28].GetInt32() != 0);
                Require(result == row[1][0].GetInt32() && s.UsedCleared == (row[1][1].GetInt32() != 0)
                    && s.TargetCleared == (row[1][2].GetInt32() != 0), "Startup result/cleanup at " + count);
                var expected = row[2].EnumerateArray().Select(r => r.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray();
                Require(s.Trace.Count == expected.Length && s.Trace.Zip(expected).All(p => p.First.SequenceEqual(p.Second)), "Startup service order at " + count);
                if (missing == 0 && s.Trace.Any(t => t[0] == 15))
                {
                    var refused = new Services(input) { ThrowStart = true };
                    try
                    {
                        if (input[0].GetInt32() != 0) { SkillStartup.Selected(refused); }
                        else { SkillStartup.Run(refused, 66, input[29].GetInt32(), input[28].GetInt32() != 0); }
                        throw new InvalidOperationException("Missing start body became success");
                    }
                    catch (NotSupportedException) { missing++; }
                    Require(refused.Trace.All(t => t[0] is not (16 or 17)), "Missing start body paid or deleted events");
                }
                count++;
            }
            Require(count == 6796 && missing == 1, "Startup source controls incomplete");
            string identity = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)));
            string path = Path.Join(Core.BaseDirectory, "skill-startup-fixtures.json");
            if (reload) { Require(File.ReadAllText(path) == identity, "Startup source fixture changed across reloads"); }
            else { File.WriteAllText(path, identity); }
            Log.Information("Skill startup:6796complete older selected-wrapper/start-dispatch cases;target cleanup,charged rank,town,LOS,payment and periodic order;missing nested body refuses;live NPC adapters remain gated");
            Log.Information("{Name}: passed", name);
        }
        catch (Exception e) { Log.Error(e, "{Name}: failed", name); }
    }));
}
