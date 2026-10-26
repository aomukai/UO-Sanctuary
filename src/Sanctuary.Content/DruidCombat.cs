// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;
public static class DruidCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.CycloneArmor or CombatAbility.Firestorm or CombatAbility.MoltenBoulder or CombatAbility.Fissure or CombatAbility.Twister;
    public static bool Handles(CombatAbility ability)=>Implemented(ability);
    public static bool Available(PlayerMobile owner,CombatAbility ability)=>owner!=null && Handles(ability)
        && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Druid
        && SkillMechanics.Rank(owner,ability) is >0 and <=255;
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
        =>ability==CombatAbility.Twister?DruidTwister.Begin(owner,selected,point,now):ability==CombatAbility.Fissure?DruidFissure.Begin(owner,selected,point,now):ability==CombatAbility.MoltenBoulder?DruidMoltenBoulder.Begin(owner,selected,point,now):ability==CombatAbility.Firestorm?DruidFirestorm.Begin(owner,selected,point,now):SkillCombat.Begin(owner,ability,selected,point,now,(p,_,_,_)=>p.Map!=null && p.Map!=Map.Internal);
    public static bool Release(PlayerMobile owner,CombatAbility ability)
        =>owner is{Alive:true,Deleted:false} && Available(owner,ability) && owner.Map!=null && owner.Map!=Map.Internal
            && (ability==CombatAbility.Twister?DruidTwister.Release(owner):ability==CombatAbility.Fissure?DruidFissure.Release(owner):ability==CombatAbility.MoltenBoulder?DruidMoltenBoulder.Release(owner):ability==CombatAbility.Firestorm?DruidFirestorm.Release(owner):DruidCycloneArmor.Cast(owner,SkillMechanics.Values(owner,ability)));
}
