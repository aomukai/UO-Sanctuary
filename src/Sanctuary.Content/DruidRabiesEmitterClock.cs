// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal static class DruidRabiesEmitterClock
{
    // Complete do30 sees elapsed age before generic collision decrements
    // current. It ignores generic ending results and retires at current<0.
    internal static (bool Retire,bool Emit,int Age) Step(int total,int age,bool casterPresent=true,bool hostAlive=true)
    {
        if(total is <1 or >2640||age<0||age>total+1){throw new ArgumentOutOfRangeException();}
        if(!casterPresent||!hostAlive||age>total){return(true,false,age);}
        return(false,age%4==0,age+1);
    }
}
