// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record BearSmiteWeapon(int Minimum,int Maximum,int DamagePercent,int FireMinimum256=0,int FireMaximum256=0,
    int LightningMinimum256=0,int LightningMaximum256=0,int ColdMinimum256=0,int ColdMaximum256=0,int ColdFrames=0);
internal readonly record struct BearSmiteMitigation(int Physical256,int Fire256,int Lightning256,int Cold256,int ColdFrames,int Absorbed256=0);
internal sealed record BearSmitePrepared(int ResultFlags,int Physical256,int Fire256,int Lightning256,int Cold256,
    int ColdFrames,int Total256,int StunFrames,int HitClass,int AbsorbedLife256,bool Critical);

// Source start32/AllocCombat snapshot only. Physical256 includes the separate
// post-allocation field addition; Total256 remains the earlier health total.
// No health/state/event/action or sequence execution is performed here.
internal static class DruidBearSmite
{
    internal const int Successful=1,WillDie=2,Knockback=8;
    private const int NoFill=128|256|512|32768;
    internal static BearSmitePrepared Prepare(int npcRank,int resultFlags,int targetLife256,BearSmiteWeapon weapon,
        Func<DireWolfPacket,BearSmiteMitigation> mitigate,Func<int,int> limited,Func<uint> raw)
    {
        ArgumentNullException.ThrowIfNull(weapon);ArgumentNullException.ThrowIfNull(mitigate);
        ArgumentNullException.ThrowIfNull(limited);ArgumentNullException.ThrowIfNull(raw);
        if(targetLife256<0||(resultFlags&~0xffff)!=0){throw new ArgumentOutOfRangeException();}
        var values=DruidNpcSkills.BearSmite(npcRank);bool hit=(resultFlags&Successful)!=0;
        if(hit)
        {
            resultFlags|=Knockback;
            // The source zero-element roller requests a zero-width value.
            // The common seed supplier returns0without consuming RNG.
            if(limited(0)!=0){throw new NotSupportedException("Zero-element BearSmite roll must be zero");}
        }
        BearSmiteMitigation reduced=default;bool critical=false;
        if(hit&&(resultFlags&NoFill)==0)
        {
            var packet=DruidDireWolfHit.Packet(weapon.Minimum,weapon.Maximum,checked(weapon.DamagePercent+(int)values.Eval("calc1")),
                weapon.FireMinimum256,weapon.FireMaximum256,weapon.LightningMinimum256,weapon.LightningMaximum256,
                weapon.ColdMinimum256,weapon.ColdMaximum256,weapon.ColdFrames,NecromancerSummons.Source("druidbear","Crit"),limited,raw);
            reduced=mitigate(packet);critical=packet.Critical;
            if(reduced is{Physical256:<0}or{Fire256:<0}or{Lightning256:<0}or{Cold256:<0}or{ColdFrames:<0}or{Absorbed256:<0})
            {throw new NotSupportedException("Negative prepared BearSmite mitigation");}
        }
        int total=checked(reduced.Physical256+reduced.Fire256+reduced.Lightning256+reduced.Cold256);
        if(hit&&(resultFlags&NoFill)==0&&(total&~255)>(targetLife256&~255)){resultFlags|=WillDie;}
        int physical=checked(reduced.Physical256+(int)values.Eval("calc2")*256);
        return new(resultFlags,physical,reduced.Fire256,reduced.Lightning256,reduced.Cold256,reduced.ColdFrames,
            total,0,critical?0x10:0,reduced.Absorbed256,critical);
    }
}
