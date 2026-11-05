// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Approved Blood hard20 slot uses item function20's pre-health timing and
// positive chance/hitflag/RNG gates. Arbitrary item proc writer11 is separate.
internal static class NpcHitSkills
{
    internal static int BloodChance(SkeletonGuardian actor)=>NpcOpenWounds.Blood(actor)
        &&actor.ControlMaster is PlayerMobile{Deleted:false} owner
        &&ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.bloodgolem")>=20?5:0;
    internal static bool Chance(int chance,uint hitFlags,bool hasPacket,uint roll)
        =>ItemProcCallbacks.Chance(chance,hasPacket,hitFlags,roll);
    internal static bool BloodLifeTap(SkeletonGuardian actor,BaseCreature target,PreparedMonsterDamagePacket packet,DateTime at)
    {
        int chance=BloodChance(actor);
        if(!ItemProcCallbacks.OutgoingGate(chance,packet!=null,packet?.HitFlags??0)){return false;}
        // Cast failure (including uninterruptible) is after this draw. No
        // player class, learned Life Tap, soft rank or mana supplier is read.
        uint roll=NpcEquipmentRandom.Raw(actor);
        if(!Chance(chance,packet?.HitFlags??0,packet!=null,roll)){return false;}
        return NpcItemSpellCasts.LifeTap(actor,target,5,at);
    }
}
