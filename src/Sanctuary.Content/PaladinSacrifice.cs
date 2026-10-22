// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class PaladinSacrifice
{
    internal static Func<int,int> Roll=Utility.Random;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static int Cost256(int physical256,int targetLife256,int percent)
        =>(int)Math.Clamp((long)Math.Min(Math.Max(0,physical256),Math.Max(0,targetLife256))*Math.Max(0,percent)/100,0,int.MaxValue);
    public static bool ValidTarget(PlayerMobile owner,Serial selected)=>PaladinVengeance.ValidTarget(owner,selected);
    public static bool Strike(PlayerMobile owner,Mobile target,SkillValues values)
    {
        owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing weapon sound placeholder.
        using var attempted=PaladinThorns.Attempt(owner,target,false);
        if(ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll)!=true){return true;}
        GearEffects.Strike(owner,target,ElementRoll(),skillPhysicalPercent:(int)values.Eval("calc1"),physicalRoll:Roll,
            beforeCommit:physical256=>
            {
                int life256=Math.Max(0,target.Hits*256-SkillHealth.Remainder256(target));
                int cost=Cost256(physical256,life256,(int)values.Eval("calc2"));
                // Do64 commits direct self damage before target events; self resistance is not reapplied.
                SkillHealth.PayLife(owner,cost);
                return owner is{Alive:true,Deleted:false};
            });
        if(owner is{Alive:true,Deleted:false}){owner.FixedEffect(0x37B9,5,5,0x21,0);} // Placeholder; source blood1 art is separately registered.
        return true;
    }
}
