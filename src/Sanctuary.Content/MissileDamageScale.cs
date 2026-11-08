// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record MissileDamageScaleContext(bool Present,int Kind,int Id,int Selector,bool HasData,short Word0e,bool HasOwner,int OwnerType,int OwnerId);
internal sealed record MissileDamageFormulaContext(int Selector,bool Missile,uint Owner,int Token,int MissileId,int Word0e);
internal sealed record MissileDamageScaleServices(Func<int,int,int?> RowToken,Func<int,int,uint> ResolveOwner,Func<MissileDamageFormulaContext,int> Formula);

// Complete current candidate0x3fa2f0 composition. Raw offsets are deliberate:
// dispatcher linkage and current gameplay field names are not yet qualified.
// No learned cast or world damage path calls this prepared component yet.
internal static class MissileDamageScale
{
    internal static void Run(MissileDamageScaleContext c,MissileDamageScaleServices s,Span<int> packet)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        int id=c.Present?c.Id:-1;
        int? token=s.RowToken(c.Selector,id);if(token is null or -1){return;}
        int word=c.Present&&c.Kind==3&&c.HasData?c.Word0e:0;
        uint owner=c.Present&&c.HasOwner?s.ResolveOwner(c.OwnerType,c.OwnerId):0;
        int percent=s.Formula(new(c.Selector,c.Present,owner,token.Value,id,word));
        ScaleRawPacket(packet,percent);
    }

    private static void ScaleRawPacket(Span<int> packet,int percent)
    {
        if(packet.Length<15){throw new ArgumentException("Current damage packet requires fields through offset0x38",nameof(packet));}
        float factor=(float)(Math.Max(0,percent)/100.0);
        foreach(int i in new ReadOnlySpan<int>([6,8,9,11,12,13,14]))
        {
            float scaled=(float)packet[i]*factor;
            // x64 CVTTSS2SI returns integer-indefinite on overflow. Modern
            // managed casts need not share that behavior, so specify it.
            packet[i]=!float.IsFinite(scaled)||scaled>=2147483648f||scaled< -2147483648f
                ?int.MinValue:unchecked((int)scaled);
        }
    }
}
