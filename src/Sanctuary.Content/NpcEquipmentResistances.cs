// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class NpcEquipmentResistances
{
    internal static int Pool(int basis,int specific,int all,bool elemental)=>checked(basis+specific+(elemental?all:0));
    internal static int Bonus(ItemStatContext item,ResistanceType type)=>Pool(0,item.Bonus(type switch
    {
        ResistanceType.Fire=>"res-fire",ResistanceType.Cold=>"res-cold",ResistanceType.Poison=>"res-pois",
        ResistanceType.Energy=>"res-ltng",_=>"res-physical"
    }),item.Bonus("res-all"),type!=ResistanceType.Physical);
    internal static int Resolve(SkeletonGuardian actor,ResistanceType type,int native)
    {
        if(NpcEquipmentCombat.Context(actor) is not{} item){return native;}
        // Native Mobile.ComputeResistances already includes the item's UO
        // appearance resistances. Replace that contribution, retaining the
        // actor's own seeds/mods. Read D2 bonuses live so broken/unidentified
        // originals cannot leave cached protection behind.
        int appearance=item.Item.CheckPropertyConflict(actor)?0:type switch
        {
            ResistanceType.Physical=>item.Item.PhysicalResistance,ResistanceType.Fire=>item.Item.FireResistance,
            ResistanceType.Cold=>item.Item.ColdResistance,ResistanceType.Poison=>item.Item.PoisonResistance,
            ResistanceType.Energy=>item.Item.EnergyResistance,_=>0
        };
        return checked(native-appearance+Bonus(item,type));
    }
}

public partial class SkeletonGuardian
{
    public override int GetResistance(ResistanceType type)=>NpcEquipmentResistances.Resolve(this,type,base.GetResistance(type));
}
