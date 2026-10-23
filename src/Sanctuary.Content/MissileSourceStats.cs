// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

// Raw source fixed-point endpoints/rates and frame/count metadata, before impact mitigation.
// Keep poison rate, aggregate duration and count separate until the impact reader averages duration.
public sealed record MissileSourceStats(int FireMin=0,int FireMax=0,int LightningMin=0,int LightningMax=0,
    int MagicMin=0,int MagicMax=0,int ColdMin=0,int ColdMax=0,int ColdFrames=0,
    int PoisonMin=0,int PoisonMax=0,int PoisonFrames=0,int PoisonCount=0,
    int LifeDrain=0,int ManaDrain=0,int BurnMin=0,int BurnMax=0,int BurnFrames=0)
{
    public int PoisonImpactFrames=>PoisonCount>1?PoisonFrames/PoisonCount:PoisonFrames;
    public MissileSourceStats Scale(int sourceDamage)
    {
        if(sourceDamage is <0 or >128){throw new ArgumentOutOfRangeException(nameof(sourceDamage));}
        int Scaled(int value)=>checked((int)((long)value*sourceDamage/128));
        return this with
        {
            FireMin=Scaled(FireMin),FireMax=Scaled(FireMax),LightningMin=Scaled(LightningMin),LightningMax=Scaled(LightningMax),
            MagicMin=Scaled(MagicMin),MagicMax=Scaled(MagicMax),ColdMin=Scaled(ColdMin),ColdMax=Scaled(ColdMax),ColdFrames=Scaled(ColdFrames),
            PoisonMin=Scaled(PoisonMin),PoisonMax=Scaled(PoisonMax),PoisonCount=Scaled(PoisonCount),
            LifeDrain=Scaled(LifeDrain),ManaDrain=Scaled(ManaDrain),BurnMin=Scaled(BurnMin),BurnMax=Scaled(BurnMax),BurnFrames=Scaled(BurnFrames)
        };
    }
}
