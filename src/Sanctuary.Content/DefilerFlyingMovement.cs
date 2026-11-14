// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

internal sealed record DefilerFlyingSource(int ClassId,int Size,int Velocity,string WalkCode,int WalkFrames,int WalkSpeed,byte Flags241,bool Flying);

// Current species suppliers for the shared motion kernel. Native terrain,
// fresh flying path construction and automatic host admission remain separate.
internal static class DefilerFlyingMovement
{
    private static readonly Lazy<DefilerFlyingSource> Source=new(()=>
    {
        var row=BalanceCatalog.Read<DefilerFlyingSource>("defiler-flying-motion").Single();
        if(row!=new DefilerFlyingSource(744,2,6,"45WLHTH",10,256,0,true)
            ||NecromancerSummons.Source("warputriddefiler","Velocity")!=row.Velocity)
        {throw new NotSupportedException("Unknown captured Defiler flying movement profile");}
        return row;
    });
    internal static DefilerFlyingSource Species=>Source.Value;
    internal static (int Velocity,int Animation) Speed(WarlockDefilerBody body,DateTime at,int extraPercent=0)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Defiler motion clock must be UTC");}
        if(body?.Prepared is not{} state){throw new NotSupportedException("Defiler motion needs its original captured body");}
        var row=Species;
        int percent=checked(100+state.Snapshot.Birth.Suppliers.VelocityPercent+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlow(body)+extraPercent);
        return SourceActorMotion.Speed(row.Velocity,row.WalkSpeed,percent,0);
    }
}
