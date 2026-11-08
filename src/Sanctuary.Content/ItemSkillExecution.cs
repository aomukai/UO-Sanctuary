// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sanctuary;

internal sealed record ItemExecutionMetadata(int Id, string Name, int Do, int Start, int ItemEffect, int ItemTarget,
    bool ItemTargetDo, bool ItemCheckStart, bool InTown, bool UseManaOnDo, bool Lob, bool DecrementQuantity,
    int Missile, int? Animation, Dictionary<string, JsonElement> Fields)
{
    internal int Number(string field) => !Fields.TryGetValue(field, out var v) ? 0
        : v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : throw new InvalidDataException("Noninteger execution field: " + field);
    internal string Text(string field) => !Fields.TryGetValue(field, out var v) ? "0"
        : v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
    internal bool HasDelay(string field) => Text(field) is not ("0" or "");
    internal SkillExecutionRecord LegacyRecord(bool finish, int unitType)
    {
        if (HasDelay("delay") || finish && (HasDelay("localdelay") || HasDelay("globaldelay")))
        { throw new NotSupportedException("Current delay fields need their distinct execution services"); }
        if (finish && unitType == 0 && Animation == null)
        { throw new NotSupportedException("Player finish needs an explicit source animation mode"); }
        // -1 identifies an unknown, unused mode; it is never offered to the
        // player finish branch. Missing current animation is not invented as0.
        return new(Do, ItemEffect, Missile, Start, InTown, UseManaOnDo, Lob, DecrementQuantity, Animation ?? -1, 0);
    }
}
internal sealed record ItemExecutionCatalog(string ReferenceVersion, Dictionary<string, string> SourceHashes,
    Dictionary<string, int> MissileIds, int[] UnresolvedAnimationIds, ItemExecutionMetadata[] Skills);

internal static class ItemSkillExecution
{
    private static readonly string[] Modes = ["DT","NU","WL","RN","GH","TN","TW","A1","A2","BL","SC","TH","KK","S1","S2","S3","S4","DD","SQ","KB"];
    private static readonly CombatAbility[] Curses = [CombatAbility.AmplifyDamage, CombatAbility.DimVision, CombatAbility.Weaken,
        CombatAbility.IronMaiden, CombatAbility.Terror, CombatAbility.LifeTap, CombatAbility.Decrepify, CombatAbility.LowerResist];
    private static readonly Lazy<ItemExecutionCatalog> Catalog = new(() =>
    {
        var c = BalanceCatalog.ReadObject<ItemExecutionCatalog>("item-skill-execution");
        if (c is not { ReferenceVersion: "3.3.93847", Skills.Length: 429, SourceHashes.Count: 3, MissileIds.Count: 742, UnresolvedAnimationIds.Length: 43 }
            || !c.MissileIds.Values.Order().SequenceEqual(Enumerable.Range(0,742)))
        { throw new InvalidDataException("Invalid current item execution catalogue"); }
        foreach (var r in c.Skills)
        {
            if (r.Id is < 0 or >= 429 || c.Skills[r.Id] != r || r.Fields == null || r.Number("*Id") != r.Id
                || r.Name != (r.Fields.ContainsKey("skill") ? r.Text("skill") : "") || (r.Name.Length == 0) != (r.Id == 362)
                || r.Do != r.Number("srvdofunc") || r.Do is < 0 or > 65535 || r.Start != r.Number("srvstfunc") || r.Start is < -32768 or > 32767
                || r.ItemEffect != r.Number("ItemEffect") || r.ItemEffect is < 0 or > 65535 || r.ItemTarget != r.Number("ItemTarget") || r.ItemTarget is < 0 or > 255
                || r.ItemTargetDo != (r.Number("ItemTgtDo") != 0) || r.ItemCheckStart != (r.Number("ItemCheckStart") != 0)
                || r.InTown != (r.Number("InTown") != 0) || r.UseManaOnDo != (r.Number("usemanaondo") != 0)
                || r.Lob != (r.Number("lob") != 0) || r.DecrementQuantity != (r.Number("decquant") != 0))
            { throw new InvalidDataException("Inconsistent item execution row"); }
            int missile = r.Fields.ContainsKey("srvmissile") ? c.MissileIds[r.Text("srvmissile")] : -1;
            int? animation = r.Fields.ContainsKey("anim") ? Array.IndexOf(Modes,r.Text("anim")) : null;
            if (r.Missile != missile || r.Animation != animation || animation < 0)
            { throw new InvalidDataException("Unresolved item execution reference"); }
        }
        if (!c.UnresolvedAnimationIds.SequenceEqual(c.Skills.Where(r=>r.Animation==null).Select(r=>r.Id)))
        { throw new InvalidDataException("Missing animation diagnostics changed"); }
        return c;
    });
    internal static IReadOnlyList<ItemExecutionMetadata> All => Catalog.Value.Skills;
    internal static int MissileCount => Catalog.Value.MissileIds.Count;
    internal static string MissileSourceHash=>Catalog.Value.SourceHashes["tools/reference/d2r-named-equipment/json/missiles.json"];
    internal static string MissileName(int id)=>Catalog.Value.MissileIds.FirstOrDefault(p=>p.Value==id).Key;
    internal static int MissileId(string key) => Catalog.Value.MissileIds.TryGetValue(key, out int id)
        ? id : throw new NotSupportedException("Unknown item execution missile: " + key);
    internal static ItemExecutionMetadata Skill(int id) => id is >= 0 and < 429 ? Catalog.Value.Skills[id] : null;
    internal static CombatAbility CurseAbility(int id)
    {
        foreach (var ability in Curses) { if (SkillMechanics.For(ability).SourceId == id) { return ability; } }
        throw new NotSupportedException("NPC item spell has no admitted curse body");
    }
    internal static ItemExecutionMetadata RequireCurse(int id)
    {
        _ = CurseAbility(id);
        var r = Skill(id);
        if (r is not { Do: 30, Start: 0, ItemEffect: 1, ItemTarget: 0, ItemTargetDo: false, ItemCheckStart: false,
                InTown: false, UseManaOnDo: false, Lob: false, DecrementQuantity: false, Missile: -1, Animation: 10 }
            || r.HasDelay("delay") || r.HasDelay("localdelay") || r.HasDelay("globaldelay") || r.HasDelay("perdelay"))
        { throw new NotSupportedException("NPC curse execution metadata needs its additional services"); }
        return r;
    }
}
