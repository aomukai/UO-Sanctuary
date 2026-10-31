// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Retain the already verified Amplify API while sharing the do30 receiver.
internal static class AmplifyDamageCasts
{
    internal static bool WalkMode(string source)=>NecromancerCurseCasts.WalkMode(source);
    internal static bool Eligible(BaseCreature target)=>NecromancerCurseCasts.Eligible(target);
    internal static int Penalty(int baseResistance,bool monsterImmunity)=>monsterImmunity&&baseResistance>=100?-20:-100;
    internal static NecromancerCurse Replacement(NecromancerCurse old,uint owner,int rank,int penalty,DateTime until,DateTime at,int x,int y)
        =>NecromancerCurseCasts.Replacement(old,CombatAbility.AmplifyDamage,owner,rank,penalty,until,at,x,y);
    internal static bool Apply(PlayerMobile owner,Mobile target,int rank)=>NecromancerCurseCasts.Apply(owner,target,CombatAbility.AmplifyDamage,rank);
    internal static bool Release(PlayerMobile owner,Point3D center,SkillValues values)=>NecromancerCurseCasts.Release(owner,CombatAbility.AmplifyDamage,center,values);
}
