// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ShadowOwnerSnapshot(CombatAbility Ability,int Level,int Rank,int ColdSunder,int FireSunder,
    int LightningSunder,int PoisonSunder,int PhysicalSunder,string Head,string Armor)
{
    internal static ShadowOwnerSnapshot Capture(PlayerMobile owner,CombatAbility ability)
    {
        if(ability is not(CombatAbility.ShadowWarrior or CombatAbility.ShadowMaster)||owner is not{Alive:true,Deleted:false}
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Assassin
            ||ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(ability).TalentId)<1)
        {throw new NotSupportedException("Shadow birth needs its living trained Assassin");}
        string Code(GearSlot slot)
        {
            var items=owner.Items.Where(i=>ClassGear.Slot(i)==slot).ToArray();
            if(items.Length>1){throw new NotSupportedException("Ambiguous original Shadow owner body slot");}
            return items.Length==0?null:ClassGear.Base(items[0])?.Code;
        }
        var result=new ShadowOwnerSnapshot(ability,profile.Level,SkillMechanics.Rank(owner,ability),
            ClassGear.Bonus(owner,"pierce-immunity-cold"),ClassGear.Bonus(owner,"pierce-immunity-fire"),ClassGear.Bonus(owner,"pierce-immunity-light"),
            ClassGear.Bonus(owner,"pierce-immunity-poison"),ClassGear.Bonus(owner,"pierce-immunity-damage"),Code(GearSlot.Head),Code(GearSlot.Body));
        result.Validate();return result;
    }
    internal void Validate()
    {
        if(Ability is not(CombatAbility.ShadowWarrior or CombatAbility.ShadowMaster)||Level is<1 or>99||Rank is<1 or>255)
        {throw new NotSupportedException("Invalid captured Shadow skill/level");}
        foreach(var pair in new[]{(Head,1),(Armor,3)})
        {
            if(pair.Item1==null){continue;}
            var type=GeneratedNpcEquipment.Type(pair.Item1);
            if(type.Body1!=pair.Item2&&type.Body2!=pair.Item2){throw new NotSupportedException("Captured owner base lost its original body slot");}
        }
        _=Plan(376*256);
    }
    internal string OwnerBase(int slot)=>slot switch{1=>Head,3=>Armor,_=>throw new NotSupportedException("Unexpected Shadow owner equipment slot")};
    internal int Accrued(string name)=>name switch
    {
        "item_pierce_cold_immunity"=>ColdSunder,"item_pierce_fire_immunity"=>FireSunder,"item_pierce_light_immunity"=>LightningSunder,
        "item_pierce_poison_immunity"=>PoisonSunder,"item_pierce_damage_immunity"=>PhysicalSunder,
        _=>throw new NotSupportedException("Uncaptured Shadow owner stat: "+name)
    };
    internal SkillValues Values()
    {
        var d=SkillMechanics.For(Ability);
        return new(d,key=>key==d.SourceKey?Rank:throw new NotSupportedException("Uncaptured Shadow skill: "+key),
            key=>throw new NotSupportedException("Uncaptured Shadow synergy: "+key),Level);
    }
    internal ShadowCreationStats Plan(int spawnLife256)=>ShadowCreation.Prepare(Values(),Values(),spawnLife256,true,Accrued,
        name=>throw new NotSupportedException("Uncaptured Shadow modifier stat: "+name));
}
