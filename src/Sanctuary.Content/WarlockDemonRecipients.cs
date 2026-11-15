// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Skill recipients,not the replacement pool:unqualified/parked members must
// still occupy their saved slots even when they cannot receive an effect.
internal static class WarlockDemonRecipients
{
    internal static bool Owned(PlayerMobile owner,BaseCreature body)
    {
        if(owner is not{Alive:true,Deleted:false}||body is not{Alive:true,Deleted:false,Controlled:true}
            ||body.ControlMaster!=owner||!PersistentMinions.Active(owner,body)
            ||!PersistentMinions.Permitted(owner,body,DiabloClass.Warlock)||BattleCommand.PetOrder(body)<=0
            ||NpcEquipmentCombat.Context(body)!=null)
        {return false;}
        if(body is WarlockGoatmanBody or WarlockTaintedBody or WarlockDefilerBody)
        {var original=(SkeletonGuardian)body;return DemonNativeState.Learned(original)&&DemonNativeState.Controller(original)is{Fault:null};}
        return BoundDemonHosts.State(body)!=null;
    }
    internal static BaseCreature[] Roster(PlayerMobile owner)=>owner==null?[]:WarlockGoatmanWorld.Demons(owner).Cast<BaseCreature>()
        .Concat(BoundDemonHosts.For(owner)).Where(b=>Owned(owner,b)).OrderBy(BattleCommand.PetOrder).ThenBy(b=>b.Serial.Value).ToArray();
    internal static bool Active(PlayerMobile owner,BaseCreature body)
    {
        if(!Owned(owner,body)||owner.NetState is not{Running:true}||owner.Map==null||owner.Map==Map.Internal
            ||body.Blessed||body.Map!=owner.Map||MinionSafeAreas.Parked(body)||MinionSafeAreas.Contains(owner)
            ||SpellHelper.IsTown(body.Location,owner))
        {return false;}
        return body is WarlockGoatmanBody or WarlockTaintedBody or WarlockDefilerBody
            ?DeathMarkWorld.State((SkeletonGuardian)body)is{Selector:1}:BoundDemonHosts.CanAct(body);
    }
    internal static bool InRange(PlayerMobile owner,BaseCreature body,double range)=>Active(owner,body)&&body.GetDistanceToSqrt(owner)<=range;
}
