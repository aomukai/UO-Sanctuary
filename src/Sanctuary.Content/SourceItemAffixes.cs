// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

// Item-owned services are deliberately explicit: selection may consume property
// RNG in Magic, whereas Rare defers properties until all slots are selected.
// No supported-property filter, player loot policy, or item factory is implied.
internal abstract class SourceItemAffixServices
{
    internal abstract bool CanBeRare { get; }
    internal abstract uint Format { get; }
    internal abstract int LegacyRare(ReadOnlySpan<int> prefixes, ReadOnlySpan<int> suffixes);
    internal abstract int RareName(bool prefix);
    internal abstract void AssignRareName(bool prefix, int id);
    internal abstract bool Jewel { get; }
    internal abstract uint Random();
    internal abstract int Limited(int bound);
    internal abstract int Pick(bool spawn, bool force, bool assignProperties, bool prefix, int preferred);
    internal abstract void Assign(bool prefix, int slot, int id);
    internal abstract int Affix(bool prefix, int slot);
    internal abstract void ClearIdentified();
    internal abstract object PropertyRecord(int id);
    internal abstract void ApplyProperties(object record);
    internal abstract void Finish();
}

// Complete older sub_6FC4D5E0 / D2GAME_RollRareItem_6FC53360 outer
// orchestration. Caller supplies a valid item and all nested source services;
// a successful return is not certification of generated NPC equipment.
internal static class SourceItemAffixes
{
    internal static int Magic(SourceItemAffixServices s, int preferredPrefix, int preferredSuffix)
    {
        ArgumentNullException.ThrowIfNull(s);
        int prefix = 0;
        bool forceSuffix = false;
        if (preferredPrefix >= 0)
        {
            prefix = s.Pick(true, preferredPrefix > 0, true, true, preferredPrefix);
            s.Assign(true, 0, prefix);
            if (prefix != 0)
            {
                if (preferredSuffix < 0)
                {
                    s.ClearIdentified();
                    s.Finish();
                    return 1;
                }
            }
            else { forceSuffix = true; }
        }
        else { forceSuffix = true; }
        if (preferredSuffix > 0) { forceSuffix = true; }
        int suffix = s.Pick(true, forceSuffix, true, false, preferredSuffix);
        s.Assign(false, 0, suffix);
        if (prefix == 0 && suffix == 0) { return 0; }
        s.ClearIdentified();
        s.Finish();
        return 1;
    }

    internal static int Rare(SourceItemAffixServices s, ReadOnlySpan<int> prefixes, ReadOnlySpan<int> suffixes)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (prefixes.Length != 3 || suffixes.Length != 3) { throw new ArgumentException("Three preferred slots required per affix side"); }
        if (!s.CanBeRare) { return 0; }
        if (s.Format < 1) { return s.LegacyRare(prefixes, suffixes); }
        int prefix = s.RareName(true), suffix = s.RareName(false);
        if (prefix == 0 || suffix == 0) { return 0; }
        s.AssignRareName(true, prefix);
        s.AssignRareName(false, suffix);
        int count = s.Jewel ? s.Limited(2) + 3 : ((s.Random() & 7) switch
        {
            0 => 3, 1 or 2 => 4, 3 or 4 or 5 => 5, _ => 6
        });
        bool prefixFull = false, suffixFull = false;
        int prefixIndex = 0, suffixIndex = 0;
        for (int i = 0; i < count; i++)
        {
            if (prefixFull && suffixFull) { break; }
            bool isPrefix = suffixFull || !(prefixFull || (s.Random() & 1) != 0);
            int index = isPrefix ? prefixIndex : suffixIndex;
            int id = s.Pick(true, true, false, isPrefix, isPrefix ? prefixes[index] : suffixes[index]);
            if (id != 0)
            {
                s.Assign(isPrefix, index, id);
                if (isPrefix) { prefixFull = ++prefixIndex >= 3; }
                else { suffixFull = ++suffixIndex >= 3; }
            }
            else
            {
                if (isPrefix) { prefixFull = true; } else { suffixFull = true; }
                i--;
            }
        }
        if (prefixIndex == 0 && suffixIndex == 0) { return 0; }
        s.ClearIdentified();
        for (int i = 0; i < 3; i++)
        {
            if (s.PropertyRecord(s.Affix(true, i)) is { } p) { s.ApplyProperties(p); }
            if (s.PropertyRecord(s.Affix(false, i)) is { } q) { s.ApplyProperties(q); }
        }
        s.Finish();
        return 1;
    }
}
