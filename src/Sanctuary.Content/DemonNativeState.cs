// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// The two original body schemas stay distinct. Only their shared controller
// and recovery hooks are projected here;birth/passives are never recaptured.
internal static class DemonNativeState
{
    internal static string Profile(SkeletonGuardian body)=>body switch
    {WarlockGoatmanBody=>"native-goatman-host-v1",WarlockTaintedBody=>"native-tainted-host-v1",_=>throw new NotSupportedException("No ground demon controller profile")};
    internal static GoatmanHostSnapshot Controller(SkeletonGuardian body)=>body switch
    {WarlockGoatmanBody b=>b.Prepared?.Controller,WarlockTaintedBody b=>b.Prepared?.Controller,_=>throw new NotSupportedException("No ground demon controller store")};
    internal static bool Learned(SkeletonGuardian body)=>body switch
    {WarlockGoatmanBody b=>b.Prepared?.LearnedCast==true,WarlockTaintedBody b=>b.Prepared?.LearnedCast==true,_=>false};
    internal static void Controller(SkeletonGuardian body,GoatmanHostSnapshot value)
    {
        if(body is WarlockGoatmanBody goat){goat.RestorePrepared(goat.Prepared with{Controller=value});}
        else if(body is WarlockTaintedBody tainted){tainted.RestorePrepared(tainted.Prepared with{Controller=value});}
        else{throw new NotSupportedException("No ground demon controller store");}
    }
    internal static void Admit(SkeletonGuardian body)
    {
        if(body is WarlockGoatmanBody goat){goat.RestorePrepared(goat.Prepared with{LearnedCast=true});}
        else if(body is WarlockTaintedBody tainted){tainted.RestorePrepared(tainted.Prepared with{LearnedCast=true});}
        else{throw new NotSupportedException("No learned ground demon actor");}
    }
    internal static void Recovery(SkeletonGuardian body,DateTime next)
    {
        if(body is WarlockGoatmanBody goat){goat.RestorePrepared(goat.Prepared with{NextRecoveryUtc=next});}
        else if(body is WarlockTaintedBody tainted){tainted.RestorePrepared(tainted.Prepared with{NextRecoveryUtc=next});}
        else{throw new NotSupportedException("No ground demon recovery store");}
    }
    internal static void Recover(SkeletonGuardian body,DateTime at)
    {
        Recovery(body,at);
        if(body is WarlockGoatmanBody goat){goat.AdvanceRecovery(at,true);}
        else if(body is WarlockTaintedBody tainted){tainted.AdvanceRecovery(at,true);}
    }
}
