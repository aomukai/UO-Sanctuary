// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class ItemAffixChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(ItemAffixChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "item-affix-fixtures.json");
    private sealed class Services(int[] input) : SourceItemAffixServices
    {
        internal readonly List<int> Trace = [];
        private readonly int[] Prefixes = new int[3], Suffixes = new int[3];
        private int Flags = 16, NamePrefix, NameSuffix, PrefixCount, SuffixCount, Draws;
        private void T(params int[] values) => Trace.AddRange(values);
        internal override bool CanBeRare { get { T(1); return input[1] != 0; } }
        internal override uint Format { get { T(2); return (uint)input[2]; } }
        internal override int LegacyRare(ReadOnlySpan<int> prefixes, ReadOnlySpan<int> suffixes) { T(3); return 7; }
        internal override int RareName(bool prefix) { T(4, prefix ? 1 : 0); return (input[3] & (prefix ? 1 : 2)) != 0 ? prefix ? 11 : 22 : 0; }
        internal override void AssignRareName(bool prefix, int id) { if (prefix) { NamePrefix = id; } else { NameSuffix = id; } T(5, prefix ? 1 : 0, id); }
        internal override bool Jewel { get { T(6); return input[4] != 0; } }
        internal override uint Random() { int n = (input[7] + Draws++) & 15; T(7, n); return (uint)n; }
        internal override int Limited(int bound) { int n = (int)(Random() % bound); T(8, bound, n); return n; }
        internal override int Pick(bool spawn, bool force, bool properties, bool prefix, int preferred)
        {
            T(9, spawn ? 1 : 0, force ? 1 : 0, properties ? 1 : 0, prefix ? 1 : 0, preferred);
            int old = prefix ? PrefixCount++ : SuffixCount++;
            return old < input[prefix ? 5 : 6] ? (prefix ? 101 : 201) + old : 0;
        }
        internal override void Assign(bool prefix, int slot, int id) { (prefix ? Prefixes : Suffixes)[slot] = id; T(10, prefix ? 1 : 0, slot, id); }
        internal override int Affix(bool prefix, int slot) => (prefix ? Prefixes : Suffixes)[slot];
        internal override void ClearIdentified() { Flags &= ~16; T(11); }
        internal override object PropertyRecord(int id) { T(12, id); return id != 0 && input[10] != 0 ? (object)id : null; }
        internal override void ApplyProperties(object record) => T(13, (int)record);
        internal override void Finish() => T(14);
        internal int[] Result(int value) => [value, Flags, NamePrefix, NameSuffix, .. Prefixes, .. Suffixes, PrefixCount, SuffixCount, Draws];
    }
    public static void Configure() { Register("item-affix-check", false); Register("item-affix-reload-check", true); }
    private static void Register(string name, bool reload) => ConsoleInputHandler.RegisterCommand(name, "Source magic/rare outer orchestration; explicit nested services, no generated items", _ => Core.LoopContext.Post(() =>
    {
        try
        {
            Check();
            const string scope = "source-outer-services-only:no-item-or-actor";
            if (reload) { Require(File.ReadAllText(Manifest) == scope, "Original explicit scope manifest"); }
            else { File.WriteAllText(Manifest, scope); }
            Log.Information("{Name}: passed", name);
        }
        catch (Exception e) { Log.Error(e, "{Name}: failed", name); }
    }));
    private static void Require(bool ok, string why) { if (!ok) { throw new InvalidOperationException(why); } }
    private static void Check()
    {
        using var cases = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "item-affix-cases.json")));
        int count = 0;
        foreach (var row in cases.RootElement.EnumerateArray())
        {
            var input = row[0].EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var s = new Services(input);
            int result = input[0] == 0 ? SourceItemAffixes.Magic(s, input[8], input[9])
                : SourceItemAffixes.Rare(s, [input[8], input[8] + 1, input[8] + 2], [input[9], input[9] + 1, input[9] + 2]);
            Require(s.Result(result).SequenceEqual(row[1].EnumerateArray().Select(x => x.GetInt32())), "Source affix outer result/slots/flags/names/draws at " + count);
            Require(s.Trace.SequenceEqual(row[2].EnumerateArray().Select(x => x.GetInt32())), "Source affix complete service ordering at " + count);
            count++;
        }
        Require(count == 41472, "Incomplete magic/rare outer source matrix");
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior) && !AssassinCombat.Implemented(CombatAbility.ShadowMaster), "Outer services enabled incomplete Shadows");
        Log.Information("Item affix:41472 complete magic/rare outer results and service traces;selection/property phase order,empty-side retries,slots,names,flags,format and preferences;no generated NPC item");
    }
}
