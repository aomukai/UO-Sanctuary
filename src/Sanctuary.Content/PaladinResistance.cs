// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class PaladinResistance
{
    public static bool Single(CombatAbility ability) => ability is CombatAbility.ResistFire or CombatAbility.ResistCold or CombatAbility.ResistLightning;
    public static CombatAbility Ability(ResistanceType type) => type switch
    {
        ResistanceType.Fire => CombatAbility.ResistFire, ResistanceType.Cold => CombatAbility.ResistCold,
        ResistanceType.Energy => CombatAbility.ResistLightning, _ => CombatAbility.None
    };
    public static int Bonus(Mobile target, ResistanceType type) => Ability(type) is var ability && ability!=CombatAbility.None
        ? (PaladinAuras.Effect(target,ability)?.Percent ?? 0)+(PaladinAuras.Effect(target,CombatAbility.Salvation)?.Percent ?? 0) : 0;
    public static int Maximum(Mobile target, ResistanceType type)
    {
        var ability=Ability(type);if(ability==CombatAbility.None){return 0;}
        if(PaladinAuras.Effect(target,ability) is {} active){return active.MaximumResistance;}
        return target is PlayerMobile player && ClassCreation.Profiles.TryGet(player.Serial,out var profile) && profile.Class==DiabloClass.Paladin
            ? ClassCreation.Profiles.GetRank(player.Serial,SkillMechanics.For(ability).TalentId)/2 : 0;
    }
    public static bool Active(Mobile target) => PaladinAuras.Effect(target,CombatAbility.ResistFire)!=null
        || PaladinAuras.Effect(target,CombatAbility.ResistCold)!=null || PaladinAuras.Effect(target,CombatAbility.ResistLightning)!=null
        || PaladinAuras.Effect(target,CombatAbility.Salvation)!=null;
}
