// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct SourceDirectionResult(int X,int Y,int Heading);
internal sealed record SourceDirectionServices(Func<float,float,float> Angle,Func<float,float,float,float> Span,Func<float,float> Round);

// Current285590. Its three protected math callees are explicit services, not
// assumed atan2/wrap/round contracts. No approximate live binding is installed.
internal static class SourceDirection64
{
    private static int Truncate(float value)=>value>=-2147483648f&&value<2147483648f?(int)value:int.MinValue;
    internal static SourceDirectionResult Calculate(SourceGridPoint from,SourceGridPoint to,SourceDirectionServices services)
    {
        ArgumentNullException.ThrowIfNull(services);ArgumentNullException.ThrowIfNull(services.Angle);
        ArgumentNullException.ThrowIfNull(services.Span);ArgumentNullException.ThrowIfNull(services.Round);
        uint ax=unchecked((uint)from.X),ay=unchecked((uint)from.Y),bx=unchecked((uint)to.X),by=unchecked((uint)to.Y);
        // The source compares unsigned packed lanes and converts the positive
        // uint32 difference through a zero-extended64-bit integer, then signs it.
        float x=ax>bx?-(float)(ax-bx):(float)(bx-ax),y=ay>by?-(float)(ay-by):(float)(by-ay);
        x*=1f/65536f;y*=1f/65536f;
        float xx=x*x,yy=y*y,length=yy+xx;int stepX=0,stepY=0;
        if(length>BitConverter.Int32BitsToSingle(0x057A0000))
        {
            float inverse=1f/MathF.Sqrt(length);x=inverse*x;y=inverse*y;
            stepY=Truncate(y*4096f);stepX=Truncate(x*4096f);
        }
        float angle=services.Angle(x,y);
        angle=-angle-2.356194496154785f;
        const float minimum=-3.1415927410125732f,maximum=3.1415927410125732f;
        float heading=services.Span(angle,minimum,maximum)-minimum;
        heading*=10.18591594696045f;heading+=0f;
        // COMISS's unordered path retains NaN;Math.Clamp would not describe
        // the source comparison order or the subsequent CVTTSS2SI sentinel.
        float limited=heading<0?0:heading<=64||float.IsNaN(heading)?heading:64;
        return new(stepX,stepY,Truncate(services.Round(limited))&63);
    }
}
