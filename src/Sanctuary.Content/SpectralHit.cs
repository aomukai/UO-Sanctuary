// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record SpectralHitRoll(int Amount,int Channel)
{
    internal int ColdMs=>Channel==3?1600:0;
    internal int PoisonTotal=>Channel==4?Amount:0;
}

// Existing native encounter affix: amount first,then magic/fire/lightning/
// cold/poison choice. Poison is a later aggregate effect,not initial HP.
internal static class SpectralHit
{
    internal static SpectralHitRoll Roll(int minimum,int maximum,Func<int,int,int> inclusive,Func<int,int> choose)
    {
        if(minimum<0||maximum<minimum){throw new ArgumentOutOfRangeException();}
        int low=checked(minimum*66)/100,amount=inclusive(low,maximum),channel=choose(5);
        if(amount<low||amount>maximum||channel is<0 or>4){throw new InvalidOperationException("Spectral Hit draw outside its declared range");}
        return new(amount,channel);
    }
    internal static GearDamage Direct(GearDamage value,SpectralHitRoll roll)=>roll==null?value:value with
    {
        Magic=value.Magic+(roll.Channel==0?roll.Amount:0),Fire=value.Fire+(roll.Channel==1?roll.Amount:0),
        Lightning=value.Lightning+(roll.Channel==2?roll.Amount:0),Cold=value.Cold+(roll.Channel==3?roll.Amount:0)
    };
}
