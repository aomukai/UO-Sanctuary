// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class NpcItemSpellOriginsChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(NpcItemSpellOriginsChecks));
    public static void Configure()
    {
        Register("npc-item-spell-origins-check");
        Register("npc-item-spell-origins-reload-check");
    }
    private static void Require(bool ok, string why)
    { if (!ok) { throw new InvalidOperationException(why); } }
    private static void Register(string name) => ConsoleInputHandler.RegisterCommand(name,
        "Complete source item executor and outgoing/incoming NPC factory origins", commandLine => Core.LoopContext.Post(() =>
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "npc-item-spell-origins-cases.json")));
            int count = 0;
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var args = row[0].EnumerateArray().Select(x => x.GetInt32()).ToArray();
                int skill = args[0], rank = args[1], outgoing = args[2], mode = args[3], failure = args[4], ar = args[5];
                (int X, int Y)? target = mode is >= 1 and <= 4
                    ? (mode == 2 ? 0 : mode == 4 ? 380 : 400, mode == 3 ? 0 : mode == 4 ? 680 : 700) : null;
                var requests = new List<int[]>();
                object created = new(); int budget = 0, budgetCalls = 0;
                var frame = new NpcItemSpellOrigins((380, 680), target, (mode == 5 ? 0 : 420, mode == 6 ? 0 : 710), ar, false, request =>
                {
                    int ordinal = requests.Count;
                    requests.Add([request.Flags, 231, request.X, request.Y, request.TargetX, request.TargetY,
                        request.Velocity, request.Skill, request.Rank, request.Missile, request.AttackRating]);
                    return ordinal == failure ? null : created;
                }, (handle, value) =>
                {
                    Require(ReferenceEquals(handle, created) && requests.Count == 1, "Chain budget lost the actual root handle/order");
                    budgetCalls++; budget = value;
                });
                int result = frame.Run(skill, rank, outgoing);
                var expected = row[2].EnumerateArray().Select(r => r.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
                Require(requests.Count == expected.Length && requests.Zip(expected).All(x => x.First.SequenceEqual(x.Second)), "NPC item factory trace at " + count);
                Require(result == row[1][0].GetInt32() && frame.ServerDo == ((row[1][1].GetUInt32() & 64) != 0)
                    && budget == row[1][3].GetInt32() && budgetCalls == (budget > 0 ? 1 : 0), "NPC item result/server-do/budget at " + count);
                count++;
            }
            Require(count == 14112, "NPC item origin matrix truncated");
            int allocations = 0;
            var guard = new NpcItemSpellOrigins((1, 1), null, (2, 2), 0, true,
                _ => { allocations++; return new object(); }, (_, _) => throw new InvalidOperationException("Unexpected budget"));
            (int Skill, int Rank, int Flag)[] invalids = [(38,1,0), (36,0,0), (36,64,0), (36,1,2)];
            foreach (var invalid in invalids)
            {
                bool refused = false;
                try { _ = guard.Run(invalid.Skill, invalid.Rank, invalid.Flag); }
                catch (NotSupportedException) { refused = true; }
                Require(refused && allocations == 0 && guard.ServerDo, "Unsupported spell/rank/flag partly allocated or reset original frame");
            }
            Require(guard.Run(48, 1, 1) == 0 && allocations == 0 && guard.ServerDo, "Missing target emitted an outgoing ring or reset server-do");
            Log.Information("NPC item origins:14112 complete source contexts;shared executor,own NPC factory coordinates/AR,all64 ring directions,allocation failure/return and actual Chain handle budget;no player mana/RNG,callbacks or world allocations claimed");
            Log.Information("{Name}: passed", name);
        }
        catch (Exception e) { Log.Error(e, "{Name}: failed", name); }
    }));
}
