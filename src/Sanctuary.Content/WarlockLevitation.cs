// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record LevitationBonuses(int AttackPercent,int DamagePercent,int CriticalPercent,int RequirementPercent);
// The trained mastery is distinct from Warlock's innate two-hand/offhand rule.
// Owner-specific requirements never become a persisted item modifier.
public static class WarlockLevitation
{
    internal static LevitationBonuses Values(int rank)
    {
        if(rank is <0 or >255){throw new NotSupportedException("Unsupported Levitation rank");}
        var row=SkillMechanics.For(CombatAbility.LevitationMastery);
        if(row.SourceId!=383||row.Text("passiveitype")!="weap"||row.Number("passivereqweaponcount")!=1
            ||row.Text("passivestat1")!="passive_mastery_melee_crit"||row.Text("passivestat2")!="passive_mastery_item_req_percent"
            ||row.Text("passivestat3")!="passive_mastery_melee_th"||row.Text("passivestat4")!="passive_mastery_melee_dmg")
        {throw new NotSupportedException("Unsupported Levitation passive route");}
        if(rank==0){return new(0,0,0,0);}
        var v=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,99);
        return new((int)v.Eval("passivecalc3"),(int)v.Eval("passivecalc4"),(int)v.Eval("passivecalc1"),(int)v.Eval("passivecalc2"));
    }
    private static int Rank(PlayerMobile owner)=>owner is{Deleted:false}&&ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true&&profile.Class==DiabloClass.Warlock
        ?SkillMechanics.Rank(owner,CombatAbility.LevitationMastery):0;
    internal static bool Weapon(Item item)=>item is BaseWeapon&&ClassGear.Slot(item)==GearSlot.Weapon&&ClassGear.Base(item) is{} basis&&ClassItemTypes.Is(basis.Type,"weap");
    public static int Bonus(PlayerMobile owner,BaseWeapon weapon,int stat)
    {
        if(stat is <1 or >3||owner==null||weapon==null||!Weapon(weapon)||!ClassGear.Active(owner,weapon)
            ||CombatTiming.Weapon(weapon).Family==AttackFamily.Throw){return 0;}
        // Explicit thrown-skill selection uses a different mastery layer in
        // the older selector. Eldritch/throw-specific overrides remain open.
        int rank=Rank(owner);if(rank<=0){return 0;}
        var v=Values(rank);return stat switch{1=>v.AttackPercent,2=>v.DamagePercent,_=>v.CriticalPercent};
    }
    internal static bool Applies(PlayerMobile owner,Item item)=>Rank(owner)>0&&Weapon(item);
    internal static int NormalRequirement(int basis,int percent)
    {
        if(basis<0||percent is < -50 or >0){throw new ArgumentOutOfRangeException();}
        // Current user70->68at -4% distinguishes truncating the reduction
        // from flooring the remaining percentage. Signed division truncates0.
        return checked(basis+(int)((long)basis*percent/100));
    }
    internal static bool TryRequirements(PlayerMobile owner,Item item,out int strength,out int dexterity)
    {
        strength=dexterity=0;if(!Applies(owner,item)){return false;}
        // Do not invent the modern combination with item-ease or ethereal10.
        if(NamedGear.Ethereal(item)||ClassGear.Bonus(item,"ease")!=0){return false;}
        var basis=ClassGear.Base(item);int percent=Values(Rank(owner)).RequirementPercent;
        strength=NormalRequirement(basis.Strength,percent);dexterity=NormalRequirement(basis.Dexterity,percent);return true;
    }
    internal static void InvalidateProperties(PlayerMobile owner)
    {
        if(ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true||profile.Class!=DiabloClass.Warlock){return;}
        foreach(var item in owner.Items){if(item is BaseWeapon){item.InvalidateProperties();}}
        void Visit(Container pack)
        {if(pack==null){return;}foreach(var item in pack.Items){if(item is BaseWeapon){item.InvalidateProperties();}else if(item is Container nested){Visit(nested);}}}
        Visit(owner.Backpack);
    }
    internal static void Properties(Item item,IPropertyList list)
    {
        if(item.RootParent is PlayerMobile owner&&TryRequirements(owner,item,out int strength,out int dexterity))
        {list.Add($"Owner's requirements: Strength {strength}; Dexterity {dexterity}");}
    }
}
