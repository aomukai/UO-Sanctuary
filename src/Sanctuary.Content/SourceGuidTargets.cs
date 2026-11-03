// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
namespace Sanctuary;

// An opaque unit handle is separate from its source GUID. Null entries model
// the original callback's null-unit path; ordinary world enumeration omits them.
internal sealed record SourceGuidTarget(int Handle,uint Id);
internal sealed record SourceGuidFindRequest(int X,int Y,int Range,uint Filter,uint Anchor,bool ReportCount=false);
internal static class SourceGuidTargets
{
    internal static SourceGuidTarget Find(SourceGuidFindRequest request,
        System.Func<int,int,int,uint,IEnumerable<SourceGuidTarget>> enumerate,ref int count)
    {
        SourceGuidTarget first=null,wrapped=null;uint limit=uint.MaxValue,low=request.Anchor;int seen=0;
        foreach(var unit in enumerate(request.X,request.Y,request.Range,request.Filter|0xa783u))
        {
            uint id=unit?.Id??uint.MaxValue;
            if(id<=request.Anchor||id>=limit)
            {
                if(id<=low){low=id;wrapped=unit;}
            }
            else{limit=id;first=unit;}
            seen=unchecked(seen+1);
        }
        // The source only writes the optional count on the preferred branch,
        // even though both branches counted every enumerated callback.
        if(first==null){return wrapped;}
        if(request.ReportCount){count=seen;}return first;
    }
}
