// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;
using System;
namespace Sanctuary;

// Poison Dagger keeps its skill packet and knife hit class while sharing the
// ordered actor adapter with other individually integrated player weapons.
internal static class PoisonDaggerContact
{
    internal static void Require(PlayerMobile source,BaseCreature target)=>PlayerWeaponContact.Require(source,target);
    internal static MonsterContactResult Release(PlayerMobile source,BaseCreature target,PoisonDaggerCombat.Prepared prepared,DateTime at)
        =>PlayerWeaponContact.Release(source,target,new(prepared.Flags,prepared.Impact,prepared.Drain,prepared.ColdFrames,6,PoisonDaggerCombat.Effects,0x50),at);
}
