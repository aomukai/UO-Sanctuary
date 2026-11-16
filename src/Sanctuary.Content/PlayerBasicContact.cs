// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Human common Attack with registered item providers joins the same ordered
// weapon consumer as learned melee skills. Other producers keep their own
// charge/form/projectile delivery contracts rather than fabricated packets.
internal static class PlayerBasicContact
{
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=>Random.Shared;
    internal static bool Handles(PlayerMobile source,Mobile target)=>target is BaseCreature
        &&PlayerItemProcs.ActiveProvider(source)&&!DruidFormAttacks.Active(source)&&!AssassinCharges.Any(source)
        &&CombatTiming.Weapon(CombatTiming.Equipped(source)).Family is not(AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw);
    internal static bool Release(PlayerMobile source,BaseCreature target,Func<bool> range,DateTime at)
    {
        if(!Handles(source,target)){throw new NotSupportedException("Common item melee needs its actual human melee producer");}
        try
        {
            var prepared=PlayerWeaponContact.PrepareOrdinary(source,target,0,0,range,HitRoll,ElementRoll,PhysicalRoll,PoisonRoll,Effects,true)
                with{AttemptScoped=true,RangeCheck=bonus=>bonus==1&&range()};
            var result=PlayerWeaponContact.Release(source,target,prepared,at,true);
            return result.Commit!=null&&(result.Packet.ResultFlags&1)!=0;
        }
        catch(NotSupportedException e){source.SendMessage(0x22,e.Message);return false;}
    }
}
