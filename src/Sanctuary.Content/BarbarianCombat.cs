// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class BarbarianCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.IncreasedStamina or CombatAbility.IronSkin
        or CombatAbility.IncreasedSpeed or CombatAbility.NaturalResistance or CombatAbility.SwordMastery or CombatAbility.AxeMastery
        or CombatAbility.MaceMastery or CombatAbility.PolearmMastery or CombatAbility.SpearMastery or CombatAbility.Bash or CombatAbility.Stun or CombatAbility.Concentrate or CombatAbility.Berserk or CombatAbility.Shout or CombatAbility.BattleOrders or CombatAbility.BattleCommand or CombatAbility.Howl or CombatAbility.BattleCry or CombatAbility.Taunt or CombatAbility.WarCry or CombatAbility.FindItem;
    public static bool Handles(CombatAbility ability)=>Implemented(ability) && ClassAbilities.Find(ability) is{Passive:false};
    public static bool Available(PlayerMobile owner,CombatAbility ability)=>Handles(ability)
        && ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true && profile.Class==DiabloClass.Barbarian
        && SkillMechanics.Rank(owner,ability)>0 && (ability!=CombatAbility.Howl || SkillMechanics.Rank(owner,ability)<=80) && (ability!=CombatAbility.Taunt || SkillMechanics.Rank(owner,ability)<=48);
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)=>ability==CombatAbility.FindItem?BarbarianFindItem.Begin(owner,selected,point,now):ability==CombatAbility.WarCry?BarbarianWarCry.Begin(owner,selected,point,now):ability==CombatAbility.Taunt?BarbarianTaunt.Begin(owner,selected,point,now):ability==CombatAbility.BattleCry?BarbarianBattleCry.Begin(owner,selected,point,now):ability==CombatAbility.Howl?BarbarianHowl.Begin(owner,selected,point,now):ability==CombatAbility.BattleCommand?BattleCommand.Begin(owner,selected,point,now):ability==CombatAbility.BattleOrders?BattleOrders.Begin(owner,selected,point,now):ability==CombatAbility.Shout?BarbarianShout.Begin(owner,selected,point,now):BarbarianMelee.Begin(owner,ability,selected,point,now);
    public static bool Release(PlayerMobile owner,CombatAbility ability,Serial selected,long now)=>ability==CombatAbility.FindItem?BarbarianFindItem.Release(owner,selected):ability==CombatAbility.WarCry?BarbarianWarCry.Release(owner):ability==CombatAbility.Taunt?BarbarianTaunt.Release(owner,selected):ability==CombatAbility.BattleCry?BarbarianBattleCry.Release(owner):ability==CombatAbility.Howl?BarbarianHowl.Release(owner):ability==CombatAbility.BattleCommand?BattleCommand.Release(owner):ability==CombatAbility.BattleOrders?BattleOrders.Release(owner):ability==CombatAbility.Shout?BarbarianShout.Release(owner):BarbarianMelee.Release(owner,ability,selected,now);
    public static int Passive(PlayerMobile owner,CombatAbility ability)
    {
        if(!Implemented(ability) || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Barbarian){return 0;}
        int rank=SkillMechanics.Rank(owner,ability);if(rank<=0){return 0;}
        var row=SkillMechanics.For(ability);
        return (int)new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,profile.Level).Eval("passivecalc1");
    }
    public static int Resistance(PlayerMobile owner,ResistanceType type)=>type is ResistanceType.Fire or ResistanceType.Cold or ResistanceType.Energy or ResistanceType.Poison
        ?Passive(owner,CombatAbility.NaturalResistance):0;
    public static void Refresh(PlayerMobile owner)
    {
        ClassGear.Refresh(owner);owner.CheckStatTimers();StatNetwork.Send(owner);
    }
}
