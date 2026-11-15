// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;
namespace Sanctuary;

// Explicit prepared native-role mapping; never add a synthetic D2 species.
// Adoption belongs to the complete native Lich world rule, not capture alone.
internal static class NativeReviveRules
{
    internal static bool Approved=>true;
    internal static int? ClassId(string source)=>GenericPetSearchTables.SourceId(source)
        ??(NativeCorpseSources.ForSource(source)?.ReviveAiSource is{} role?GenericPetSearchTables.SourceId(role):null);
    internal static string Source(BaseCreature actor)=>CreatureProfiles.Definition(actor).Reference
        ??(NativeCorpseSources.For(actor)is{ReviveAiSource:not null} native?native.Source:null);
    internal static bool CanCapture(BaseCreature actor,string source)
        =>source!=null&&Source(actor)==source&&ClassId(source)is{} cls&&ReviveSpecies.ForClass(cls)==true
            &&SourceAiEligibility.CanSwitch(cls,SourceAiEligibility.CurrentMonster);
}
