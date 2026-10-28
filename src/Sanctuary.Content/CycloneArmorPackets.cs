// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal readonly record struct CycloneArmorPool(int Remaining256,int Maximum256,int DisplayPercent=0);
internal readonly record struct CycloneArmorHit(CycloneArmorPool Pool,int Fire256,int Cold256,int Lightning256,
    bool Applied,bool Removed,bool DisplayChanged);
// Event25's numeric consumer only. Native buff/state ownership, raw incoming
// event routing and player casting must supply the pool and commit the result.
internal static class CycloneArmorPackets
{
    internal static CycloneArmorHit Absorb(CycloneArmorPool pool,int fire256,int cold256,int lightning256)
    {
        if(pool.Remaining256<0 || pool.Maximum256<0 || fire256<0 || cold256<0 || lightning256<0)
        {throw new NotSupportedException("Unsupported negative Cyclone Armor context");}
        if(pool.Maximum256==0){return new(pool,fire256,cold256,lightning256,false,false,false);}
        int remaining=pool.Remaining256;
        int Part(int damage){var step=SourceAbsorptionPool.Take(remaining,damage);remaining=step.Remaining256;return step.Damage256;}
        fire256=Part(fire256);cold256=Part(cold256);lightning256=Part(lightning256);
        bool removed=remaining==0;
        int percent=removed?0:SourcePercentage.Calculate(100,remaining,pool.Maximum256);
        // Preserve the older routine's directed comparison in this source
        // component. It is not a claim about modern UI/overlay behavior.
        bool changed=!removed && (long)percent-pool.DisplayPercent>=5;
        return new(pool with{Remaining256=remaining,DisplayPercent=changed?percent:pool.DisplayPercent},
            fire256,cold256,lightning256,true,removed,changed);
    }
}
