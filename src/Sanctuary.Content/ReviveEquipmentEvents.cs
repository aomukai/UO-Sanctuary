// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Explicit registered-event admission, separate from typed damage/defenses.
// Incomplete raw projections and unconsumed registrations remain refusals.
internal static class ReviveEquipmentEvents
{
    internal const string Profile="frozen-equipment-events-v1";
    private static EquipmentStatValue[] Stats(FrozenEquipmentContext[] items,int level)
    {
        return items.SelectMany(s=>s.NumericProperties(level))
            .GroupBy(s=>s.Packed).Select(g=>new EquipmentStatValue(g.Key,g.Aggregate(0,(sum,s)=>checked(sum+s.Value)))).OrderBy(s=>s.Packed).ToArray();
    }
    private static NpcCombatEvents Read(FrozenEquipmentContext[] items,int level,EquipmentStatValue[] projected=null)
    {
        var stats=projected??Stats(items,level);
        foreach(var pair in new[]{("thorns",78),("knock",81),("heal-kill",86),("rip",108),("howl",112),("stupidity",113),("dmg-to-mana",114),("light-thorns",128),("freeze",134),("openwounds",135),("crush",136),("mana-kill",138),("demon-heal",139),("slow",150),("dmg-ac",120)})
        {
            int typed=items.Aggregate(0,(sum,item)=>checked(sum+item.Bonus(pair.Item1,level)));
            if(typed!=EquipmentStatValues.Raw(stats,EquipmentStatKey.Pack(pair.Item2)))
            {throw new NotSupportedException("Frozen event needs derived stat attachment: "+pair.Item1);}
        }
        return NpcCombatEvents.FromStats(stats);
    }
    private static FrozenEquipmentContext[] Items(ReviveOriginalState original)=>Array.ConvertAll(ReviveBeforeDeath.Equipment(ReviveBeforeDeath.Decode(original.BeforeDeathJson)),FrozenEquipmentEffects.Bind);
    internal static ReviveNativeSetup Prepare(ReviveNativeSetup setup,ReviveOriginalState original)
    {
        var defenses=ReviveEquipmentDefenses.Prepare(setup,original);
        _=Read(Items(original),defenses.CombatPlan.Level,setup.EquipmentGraphProfile==ReviveEquipmentGraph.Profile?ReviveEquipmentGraph.Project(defenses,original).Stats:null);
        return defenses with{EquipmentEventProfile=Profile};
    }
    internal static void Validate(ReviveNativeState state)
    {
        if(state.Setup.EquipmentEventProfile==null){return;}
        try
        {
            if(state.Setup.EquipmentEventProfile!=Profile||Prepare(state.Setup,state.Original)!=state.Setup)
            {throw new InvalidDataException("Invalid frozen Revive combat-event profile");}
            _=Read(Items(state.Original),(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,state.Setup.EquipmentGraphProfile==ReviveEquipmentGraph.Profile?ReviveEquipmentGraph.Project(state.Setup with{LifePlan=state.FacetPlan?.Life??state.Setup.LifePlan,CombatPlan=state.FacetPlan?.Combat??state.Setup.CombatPlan},state.Original).Stats:null);
        }
        catch(Exception e)when(e is NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unavailable frozen Revive combat events",e);}
    }
    internal static bool Handles(BaseCreature actor)=>!World.Loading&&actor is{Deleted:false,ControlMaster:PlayerMobile owner}
        &&ReviveNativeActivation.State(actor)is{Setup.EquipmentEventProfile:Profile} state&&state.Owner==owner.Serial.Value;
    internal static NpcCombatEvents Registry(BaseCreature actor)
    {
        if(!Handles(actor)){throw new NotSupportedException("No admitted original Revive combat-event profile");}
        var state=ReviveNativeActivation.State(actor);
        return Read(ReviveEquipmentLife.Items(state),(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,state.Setup.EquipmentGraphProfile==ReviveEquipmentGraph.Profile?ReviveEquipmentGraph.Current(state).Stats:null);
    }
    internal static int? Bonus(BaseCreature actor,string code)
    {
        if(!Handles(actor)){return null;}
        if(code is not("thorns" or "knock" or "heal-kill" or "mana-kill" or "demon-heal" or "slow" or "rip" or "howl" or "stupidity" or "dmg-to-mana" or "light-thorns" or "freeze" or "openwounds" or "crush" or "dmg-ac"))
        {throw new NotSupportedException("Unadmitted frozen Revive event stat: "+code);}
        var state=ReviveNativeActivation.State(actor);
        int level=(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level;
        int stat=code switch{"thorns"=>78,"knock"=>81,"heal-kill"=>86,"mana-kill"=>138,"demon-heal"=>139,"slow"=>150,"rip"=>108,"howl"=>112,"stupidity"=>113,"dmg-to-mana"=>114,"light-thorns"=>128,"freeze"=>134,"openwounds"=>135,"dmg-ac"=>120,_=>136};
        return state.Setup.EquipmentGraphProfile==ReviveEquipmentGraph.Profile?ReviveEquipmentGraph.Current(state).Read(stat):EquipmentStatValues.Raw(Stats(ReviveEquipmentLife.Items(state),level),EquipmentStatKey.Pack(stat));
    }
}
