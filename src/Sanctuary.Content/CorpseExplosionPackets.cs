// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal sealed record CorpseExplosionPacket(int Physical256,int Fire256,int InnerSquared,int OuterRange)
{
    internal (int Physical256,int Fire256) Recipient(int squaredDistance)
        =>(squaredDistance>InnerSquared?0:Physical256,Fire256);
}
// The complete do55 packet order with base-life, random and elemental services
// explicit. Corpse eligibility/consumption, area enumeration and defenses are
// separate consumers; this component is not a learned skill implementation.
internal static class CorpseExplosionPackets
{
    internal static int MonsterBase256(int minimum,int maximum)=>checked((int)(((long)minimum+maximum)*128));
    internal static CorpseExplosionPacket Create(int maximum256,int casterLevel,int corpseLevel,int range,
        int minimumPercent,int maximumPercent,int firePercent,Func<int,int> roll)
    {
        ArgumentNullException.ThrowIfNull(roll);
        if(maximum256<0||casterLevel<0||corpseLevel<0||range<0||minimumPercent<0||maximumPercent<minimumPercent)
        {throw new NotSupportedException("Unsupported corpse-explosion packet context");}
        int minimum=SourcePercentage.Calculate(minimumPercent,maximum256,100);
        int maximum=SourcePercentage.Calculate(maximumPercent,maximum256,100);
        int offset=roll(maximum-minimum);
        if(offset<0||offset>=maximum-minimum&&offset!=0){throw new ArgumentOutOfRangeException(nameof(roll));}
        int amount=checked(minimum+offset);
        if(corpseLevel!=0&&casterLevel<corpseLevel){amount=SourcePercentage.Calculate(amount,casterLevel,corpseLevel);}
        firePercent=Math.Clamp(firePercent,0,100);
        int fire=firePercent>0?SourcePercentage.Calculate(amount,firePercent,100):0;
        int physical=firePercent>0?SourcePercentage.Calculate(amount,100-firePercent,100):amount;
        int half=range/2;return new(physical,fire,checked(half*half),(range+1)/2);
    }
}
