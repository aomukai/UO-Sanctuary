// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal static class WarlockDemonPlacement
{
    internal static bool Prepared(SkeletonGuardian body)=>body is WarlockGoatmanBody{Prepared:not null}
        or WarlockTaintedBody{Prepared:not null} or WarlockDefilerBody{Prepared:not null};
    internal static uint Owner(SkeletonGuardian body)=>body switch
    {WarlockGoatmanBody{Prepared:{} s}=>s.Owner,WarlockTaintedBody{Prepared:{} s}=>s.Owner,WarlockDefilerBody{Prepared:{} s}=>s.Owner,
        _=>throw new NotSupportedException("Original prepared demon state is required")};
    internal static int ClassId(SkeletonGuardian body)=>GenericPetSearchTables.SourceId(body switch
    {WarlockGoatmanBody=>"wargoatman",WarlockTaintedBody=>"warbighead",WarlockDefilerBody=>"warputriddefiler",
        _=>throw new NotSupportedException("No original Warlock demon metadata")})??throw new NotSupportedException("Missing owned demon source row");
    internal static bool ClassIdAllowed(int id)=>id==GenericPetSearchTables.SourceId("wargoatman")
        ||id==GenericPetSearchTables.SourceId("warbighead")||id==GenericPetSearchTables.SourceId("warputriddefiler");
}
