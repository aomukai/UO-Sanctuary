// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class DireWolfUpgrades
{
    internal static int HardRank(Mobile actor)=>actor is DruidDireWolfBody{ControlMaster:PlayerMobile{Deleted:false} owner}
        ?ClassCreation.Profiles.GetRank(owner.Serial,"druid.summondirewolf"):0;
    internal static int MovementPercent(Mobile actor)=>HardRank(actor)>=10?EncounterProfiles.ModifierMovementPercent(EliteModifier.ExtraFast):0;
    private static (int DamagePercent,int AttackPercent) Strength(BaseCreature actor)=>actor is DruidDireWolfBody wolf&&HardRank(wolf)>=20
        ?EncounterProfiles.ExtraStrong(wolf.SummonDifficulty,false):default;
    internal static int DamagePercent(BaseCreature actor)=>Strength(actor).DamagePercent;
    internal static int AttackPercent(BaseCreature actor)=>Strength(actor).AttackPercent;
}
