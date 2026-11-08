// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;
public static class WarlockCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.LevitationMastery or CombatAbility.SigilLethargy;
    public static bool Handles(CombatAbility ability)=>ability==CombatAbility.SigilLethargy;
    public static bool Available(Server.Mobiles.PlayerMobile owner,CombatAbility ability)=>Handles(ability)
        &&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Warlock&&SkillMechanics.Rank(owner,ability)>0;
    private static bool Point(Server.Mobiles.PlayerMobile owner,Server.Serial selected,Server.Point3D requested,out Server.Point3D point)
    {
        point=requested;var entity=Server.World.FindEntity(selected);
        if(entity is{Deleted:false}){if(entity.Map!=owner.Map){return false;}point=entity.Location;}
        return SigilActors.TargetPoint(owner,point,out point);
    }
    public static bool Begin(Server.Mobiles.PlayerMobile owner,CombatAbility ability,Server.Serial selected,Server.Point3D point,long now)
        =>Handles(ability)&&Point(owner,selected,point,out var ground)
            &&SkillCombat.Begin(owner,ability,selected,ground,now,(p,a,id,at)=>Point(p,id,at,out _));
    public static bool Release(Server.Mobiles.PlayerMobile owner,CombatAbility ability,Server.Serial selected)
    {
        if(!Available(owner,ability)||!Point(owner,selected,ClassCombat.For(owner).PendingPoint,out var point)){return false;}
        var values=SkillMechanics.Values(owner,ability);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)||SigilActors.CreatePaid(owner,values,point,TimedEffects.Now,values.Mana256)==null){return false;}
        owner.PlaySound(0x1E5); // Identified Sigil cast placeholder;final sound pending.
        return true;
    }
}
