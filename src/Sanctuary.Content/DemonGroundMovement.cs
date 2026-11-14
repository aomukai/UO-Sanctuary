// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

internal sealed record TaintedGroundSource(int ClassId,int Size,int Velocity,string WalkCode,int WalkFrames,int WalkSpeed,byte Flags241);

// Species inputs for the existing ground movement adapter. Defiler's flying
// path and other summons are deliberately outside this two-species binding.
internal static class DemonGroundMovement
{
    private static readonly Lazy<TaintedGroundSource> Source=new(()=>
    {
        var row=BalanceCatalog.Read<TaintedGroundSource>("tainted-ground-motion").Single();
        if(row!=new TaintedGroundSource(743,3,6,"BHWLHTH",8,176,0)
            ||NecromancerSummons.Source("warbighead","Velocity")!=row.Velocity)
        {throw new NotSupportedException("Unknown captured Tainted ground movement profile");}
        return row;
    });
    internal static TaintedGroundSource Tainted=>Source.Value;
    internal static int Size(SkeletonGuardian body)=>body switch
    {
        WarlockGoatmanBody{Prepared:not null}=>2,
        WarlockTaintedBody{Prepared:not null}=>Tainted.Size,
        _=>throw new NotSupportedException("Ground movement requires a captured Goatman or Tainted")
    };
    internal static (int Velocity,int Animation) Speed(SkeletonGuardian body,DateTime at,int extraPercent=0)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Demon ground clock must be UTC");}
        if(body is WarlockGoatmanBody goat){return GoatmanNpcBodyActions.Movement(goat,at,extraPercent);}
        if(body is not WarlockTaintedBody{Prepared:{} state} tainted){throw new NotSupportedException("Unmapped demon ground speed");}
        var row=Tainted;
        int percent=checked(100+state.Snapshot.Birth.Suppliers.VelocityPercent+PaladinVigor.VelocityPercent(tainted)-GearEffects.MovementSlow(tainted)+extraPercent);
        return SourceActorMotion.Speed(row.Velocity,row.WalkSpeed,percent,0);
    }
}
